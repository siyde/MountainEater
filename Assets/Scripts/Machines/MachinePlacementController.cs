using System;
using MountainEater.Cameras;
using MountainEater.Core;
using MountainEater.Economy;
using MountainEater.Mining;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MountainEater.Machines
{
    /// <summary>
    /// Makine yerleştirme / taşıma modu.
    ///   BeginBuy(tür): imleci takip eden önizleme; yeşil = uygun, kırmızı = uygun değil.
    ///   Sol tık uygun yere: satın alır ve yerleştirir (para ancak başarılı yerleştirmede düşer).
    ///   BeginMove(id): aynı akış, makineyi yeni yere taşır (ücretsiz).
    ///   Esc: iptal.
    /// Mod açıkken oyuncu kazısı ve bölge modu kapalıdır (GameBootstrap düzenler).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MachinePlacementController : MonoBehaviour
    {
        [SerializeField] private Key cancelKey = Key.Escape;
        [SerializeField] private Color validColor = new Color(0.30f, 0.90f, 0.40f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.25f, 0.20f);

        private MachineManager machines;
        private MachineController machineController;
        private EconomyManager economy;
        private VoxelPicker picker;
        private OrbitCameraController orbitCamera;

        private MachineView ghost;
        private MachineType buyType;
        private int moveMachineId = -1;
        private bool hasValidSpot;
        private VoxelPick currentPick;

        public bool IsActive { get; private set; }
        public bool IsMoving => IsActive && moveMachineId >= 0;

        /// <summary>Önizlemenin altındaki yer neden uygun değil (HUD gösterir); uygunsa boş.</summary>
        public string CurrentProblem { get; private set; } = string.Empty;

        public event Action<bool> ActiveChanged;

        /// <summary>Başarılı yerleştirme / taşıma veya hata mesajı (HUD gösterir).</summary>
        public event Action<string> Message;

        public void Initialize(MachineManager machineManager, MachineController controller, EconomyManager economyManager,
            VoxelPicker voxelPicker, OrbitCameraController cameraController)
        {
            machines = machineManager;
            machineController = controller;
            economy = economyManager;
            picker = voxelPicker;
            orbitCamera = cameraController;
        }

        public void BeginBuy(MachineType type)
        {
            if (machines == null || !machines.HasSpec(type)) return;

            if (!economy.CanAfford(machines.GetNextPrice(type)))
            {
                Message?.Invoke("Yetersiz para");
                return;
            }

            buyType = type;
            moveMachineId = -1;
            Activate();
        }

        public void BeginMove(int machineId)
        {
            if (machines?.Find(machineId) == null) return;
            moveMachineId = machineId;
            Activate();
        }

        public void Cancel()
        {
            if (!IsActive) return;
            IsActive = false;
            moveMachineId = -1;
            if (ghost != null) ghost.gameObject.SetActive(false);
            ActiveChanged?.Invoke(false);
        }

        private void Activate()
        {
            if (ghost == null) ghost = machineController.CreateView("PlacementPreview");
            ghost.gameObject.SetActive(false);
            hasValidSpot = false;
            CurrentProblem = "Bir kaya yüzeyi seç";

            bool wasActive = IsActive;
            IsActive = true;
            if (!wasActive) ActiveChanged?.Invoke(true);
        }

        private void Update()
        {
            if (!IsActive) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && cancelKey != Key.None && keyboard[cancelKey].wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            UpdatePreview(mouse.position.ReadValue());

            if (!mouse.leftButton.wasPressedThisFrame || !Application.isFocused) return;
            if (PointerUtility.IsPointerOverUI()) return;
            if (orbitCamera != null && orbitCamera.IsDragging) return;

            Confirm();
        }

        private void UpdatePreview(Vector2 screenPoint)
        {
            if (PointerUtility.IsPointerOverUI() ||
                !picker.TryPick(screenPoint, out currentPick, out _))
            {
                hasValidSpot = false;
                CurrentProblem = "Bir kaya yüzeyi seç";
                ghost.gameObject.SetActive(false);
                return;
            }

            hasValidSpot = machines.CanPlaceAt(currentPick.Voxel, out string reason);
            if (hasValidSpot && !IsMoving && !economy.CanAfford(machines.GetNextPrice(buyType)))
            {
                hasValidSpot = false;
                reason = "Yetersiz para";
            }

            CurrentProblem = hasValidSpot ? string.Empty : reason;
            ghost.gameObject.SetActive(true);
            ghost.SetPose(currentPick.WorldPoint, currentPick.WorldNormal);
            ghost.SetBodyColor(hasValidSpot ? validColor : invalidColor);
        }

        private void Confirm()
        {
            if (!hasValidSpot)
            {
                Message?.Invoke(string.IsNullOrEmpty(CurrentProblem) ? "Buraya yerleştirilemez" : CurrentProblem);
                return;
            }

            string reason;
            if (IsMoving)
            {
                if (machines.TryMove(moveMachineId, currentPick.Voxel, currentPick.WorldNormal, out reason))
                {
                    Message?.Invoke("Makine taşındı");
                    Cancel();
                    return;
                }
            }
            else
            {
                Machine placed = machines.TryBuyAndPlace(buyType, currentPick.Voxel, currentPick.WorldNormal, economy, out reason);
                if (placed != null)
                {
                    Message?.Invoke($"{machines.GetSpec(buyType).DisplayName} #{placed.Id} yerleştirildi");
                    Cancel();
                    return;
                }
            }

            Message?.Invoke(reason);
        }
    }
}
