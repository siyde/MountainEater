using System;
using MountainEater.Cameras;
using MountainEater.Core;
using MountainEater.Economy;
using MountainEater.Mining;
using MountainEater.Voxel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MountainEater.Regions
{
    /// <summary>
    /// Bölge yönetimi modu: dağı bölge renkleriyle gösterir ve tıklanan bölgeyi seçer.
    ///   Açık bölge yeşil · alınabilir kilitli bölge turuncu · parası yetmeyen kilitli bölge kırmızı · sahipsiz gri.
    ///   Seçili bölge daha belirgin; bölge sınırları açık renk çizgiyle.
    ///
    /// Performans: renkler shader'a global dizi olarak gönderilir; mod açma/kapama veya bölge satın alma
    /// hiçbir chunk'ı yeniden mesh'lemez (bölge kimliği mesh'in UV2 kanalında zaten var).
    /// R: aç/kapat · Esc: kapat · Sol tık (mod açıkken): bölge seç.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegionOverlayController : MonoBehaviour
    {
        private const int MaxRegions = RegionStats.Capacity;
        private static readonly int RegionColorsId = Shader.PropertyToID("_ME_RegionColors");
        private static readonly int RegionOverlayId = Shader.PropertyToID("_ME_RegionOverlay");

        [Header("Kısayollar")]
        [SerializeField] private Key toggleKey = Key.R;
        [SerializeField] private Key exitKey = Key.Escape;

        [Header("Renkler")]
        [SerializeField] private Color unlockedColor = new Color(0.25f, 0.85f, 0.35f);
        [SerializeField] private Color affordableColor = new Color(1.00f, 0.60f, 0.15f);
        [SerializeField] private Color expensiveColor = new Color(0.90f, 0.22f, 0.20f);
        [SerializeField] private Color unassignedColor = new Color(0.45f, 0.45f, 0.48f);

        private readonly Vector4[] colors = new Vector4[MaxRegions];

        private RegionManager regions;
        private EconomyManager economy;
        private VoxelPicker picker;
        private OrbitCameraController orbitCamera;
        private ChunkManager chunkManager;

        public bool IsActive { get; private set; }

        /// <summary>Seçili bölge kimliği; 0 = seçim yok.</summary>
        public byte SelectedRegion { get; private set; }

        public event Action<bool> ActiveChanged;
        public event Action<byte> SelectionChanged;

        public void Initialize(RegionManager regionManager, EconomyManager economyManager, VoxelPicker voxelPicker,
            OrbitCameraController cameraController, ChunkManager chunks)
        {
            Unsubscribe();

            regions = regionManager;
            economy = economyManager;
            picker = voxelPicker;
            orbitCamera = cameraController;
            chunkManager = chunks;

            regions.RegionUnlocked += OnRegionUnlocked;
            economy.MoneyChanged += OnMoneyChanged;

            RefreshColors();
            ApplyActive(false, notify: false);
        }

        public void Toggle() => SetActive(!IsActive);

        public void SetActive(bool active)
        {
            if (active == IsActive) return;
            ApplyActive(active, notify: true);
        }

        /// <summary>Bölgeyi seçer; focusCamera ise kamera bölgenin ortasına kayar.</summary>
        public void Select(byte regionId, bool focusCamera)
        {
            if (SelectedRegion != regionId)
            {
                SelectedRegion = regionId;
                RefreshColors();
                SelectionChanged?.Invoke(regionId);
            }

            if (focusCamera && orbitCamera != null && chunkManager != null)
            {
                RegionState region = regions?.Find(regionId);
                if (region != null && region.VoxelCount > 0)
                {
                    orbitCamera.SetFocus(chunkManager.VoxelSpaceToWorld(region.Centroid), snap: false);
                }
            }
        }

        private void Update()
        {
            if (regions == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame) Toggle();
                else if (IsActive && exitKey != Key.None && keyboard[exitKey].wasPressedThisFrame) SetActive(false);
            }

            if (!IsActive) return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || !Application.isFocused) return;
            if (PointerUtility.IsPointerOverUI()) return;
            if (orbitCamera != null && orbitCamera.IsDragging) return;

            if (picker != null && picker.TryPick(mouse.position.ReadValue(), out VoxelPick pick, out _))
            {
                Vector3Int v = pick.Voxel;
                Select(picker.World.GetRegion(v.x, v.y, v.z), focusCamera: false);
            }
        }

        private void ApplyActive(bool active, bool notify)
        {
            IsActive = active;
            Shader.SetGlobalFloat(RegionOverlayId, active ? 1f : 0f);
            if (notify) ActiveChanged?.Invoke(active);
        }

        private void OnRegionUnlocked(RegionState region) => RefreshColors();

        private void OnMoneyChanged(long money)
        {
            if (IsActive) RefreshColors(); // "alınabilir" (turuncu) / "pahalı" (kırmızı) ayrımı paraya bağlı
        }

        private void RefreshColors()
        {
            for (int i = 0; i < MaxRegions; i++) colors[i] = ToVector(unassignedColor, 0f);

            if (regions != null)
            {
                foreach (RegionState region in regions.Regions)
                {
                    Color color = region.Unlocked ? unlockedColor
                        : economy != null && economy.CanAfford(region.Price) ? affordableColor
                        : expensiveColor;
                    colors[region.Id] = ToVector(color, region.Id == SelectedRegion ? 1f : 0.25f);
                }
            }

            Shader.SetGlobalVectorArray(RegionColorsId, colors);
        }

        private static Vector4 ToVector(Color color, float emphasis) => new Vector4(color.r, color.g, color.b, emphasis);

        private void Unsubscribe()
        {
            if (regions != null) regions.RegionUnlocked -= OnRegionUnlocked;
            if (economy != null) economy.MoneyChanged -= OnMoneyChanged;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Shader.SetGlobalFloat(RegionOverlayId, 0f); // Play'den çıkınca sahne görünümü normale dönsün
        }
    }
}
