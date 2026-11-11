using MountainEater.Cameras;
using MountainEater.Core;
using MountainEater.Voxel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MountainEater.Mining
{
    /// <summary>
    /// Sol tıklamayı tek bir kazma vuruşuna çevirir:
    /// tıklama → VoxelPicker (imlecin altındaki voxel) → MiningSystem.ApplyHit → efektler.
    ///
    /// - Yalnızca tuşa BASILDIĞI karede vurur (wasPressedThisFrame); basılı tutmak tekrar vurmaz.
    /// - İmleç UI üzerindeyse, kamera sürükleniyorsa veya başka bir mod aktifse (MiningEnabled = false) vurmaz.
    /// - Kilitli bölgeye tıklanırsa LockedTargetClicked olayı (HUD "Kilitli" yazısı gösterir).
    /// - Hasar ve yarıçap UpgradeManager'dan gelir (GameBootstrap uygular).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMiningController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private OrbitCameraController orbitCamera;
        [SerializeField] private MiningEffects effects;

        [Header("Kazma (Play'de yükseltmeler bu değerleri ezer)")]
        [SerializeField, Min(1)] private int pickaxeDamage = 1;
        [SerializeField, Range(0, SphereOffsets.MaxRadius)] private int miningRadius;

        [Header("Raycast")]
        [SerializeField, Min(1f)] private float maxRayDistance = 2000f;
        [SerializeField] private LayerMask voxelMask = 1; // Default layer

        [Header("Geliştirme")]
        [Tooltip("Her sol tıklamada sonucu veya neden vurulmadığını Console'a yazar.")]
        [SerializeField] private bool logDiagnostics;

        private MiningSystem mining;

        public int PickaxeDamage
        {
            get => pickaxeDamage;
            set => pickaxeDamage = Mathf.Max(1, value);
        }

        public int MiningRadius
        {
            get => miningRadius;
            set => miningRadius = Mathf.Clamp(value, 0, SphereOffsets.MaxRadius);
        }

        /// <summary>false iken sol tık kazmaz (ör. bölge yönetimi modu, Faz 7 makine yerleştirme).</summary>
        public bool MiningEnabled { get; set; } = true;

        /// <summary>İmleç → voxel seçici. Diğer sistemler (bölge seçimi, makine yerleştirme) de kullanır.</summary>
        public VoxelPicker Picker { get; private set; }

        /// <summary>Son başarılı vuruşun sonucu (debug paneli için).</summary>
        public MiningResult LastResult { get; private set; }

        /// <summary>Oyuncunun hasar veren her vuruşu: sonuç + dünyadaki vuruş noktası (HUD uçan yazıları için).</summary>
        public event System.Action<MiningResult, Vector3> PlayerHit;

        /// <summary>Kilitli bir bölgedeki kayaya tıklandı: voxel + dünyadaki nokta.</summary>
        public event System.Action<Vector3Int, Vector3> LockedTargetClicked;

        public void Initialize(MiningSystem miningSystem, ChunkManager chunks)
        {
            mining = miningSystem;
            if (targetCamera == null) targetCamera = Camera.main;
            Picker = new VoxelPicker(targetCamera, chunks, maxRayDistance, voxelMask);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (mining == null || Picker == null)
            {
                Diagnose("başlatılmamış");
                return;
            }

            if (!MiningEnabled) { Diagnose("kazı modu kapalı (başka mod aktif)"); return; }
            if (!Application.isFocused) { Diagnose("oyun penceresi odakta değil"); return; }
            if (PointerUtility.IsPointerOverUI()) { Diagnose("imleç UI üzerinde"); return; }
            if (orbitCamera != null && orbitCamera.IsDragging) { Diagnose("kamera sürükleniyor"); return; }

            TryMineAtScreenPoint(mouse.position.ReadValue());
        }

        private void Diagnose(string message)
        {
            if (logDiagnostics) Debug.Log($"[PlayerMining] Vuruş yok: {message}", this);
        }

        /// <summary>Ekran noktasındaki kayaya bir vuruş yapar. Hasar verildiyse true.</summary>
        public bool TryMineAtScreenPoint(Vector2 screenPoint)
        {
            if (!Picker.TryPick(screenPoint, out VoxelPick pick, out string failReason))
            {
                Diagnose(failReason);
                return false;
            }

            Vector3Int target = pick.Voxel;
            if (!mining.CanMine(target))
            {
                Diagnose($"hedef {target} kilitli bölgede (bölge {mining.World.GetRegion(target.x, target.y, target.z)})");
                LockedTargetClicked?.Invoke(target, pick.WorldPoint);
                return false;
            }

            MiningResult result = mining.ApplyHit(target, miningRadius, pickaxeDamage, MiningSource.Player);
            if (!result.HitAnything)
            {
                Diagnose($"hedef {target} kazılamadı");
                return false;
            }

            if (logDiagnostics)
            {
                Debug.Log($"[PlayerMining] Vuruş ({pick.Method}): hedef {target}, {result.VoxelsDamaged} hasar, " +
                          $"{result.VoxelsBroken} kırık, +{result.Reward} $, kalan HP {mining.GetRemainingHealth(target)}", this);
            }

            LastResult = result;
            if (effects != null) effects.Play(pick.WorldPoint, pick.WorldNormal, result);
            PlayerHit?.Invoke(result, pick.WorldPoint);
            return true;
        }
    }
}
