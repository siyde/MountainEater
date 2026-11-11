using System.Collections.Generic;
using System.Diagnostics;
using MountainEater.Cameras;
using MountainEater.Economy;
using MountainEater.Machines;
using MountainEater.Mining;
using MountainEater.Regions;
using MountainEater.UI;
using MountainEater.Upgrades;
using MountainEater.Voxel;
using MountainEater.World;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MountainEater.Core
{
    /// <summary>
    /// Sahnenin "composition root"u: sistemleri doğru sırayla oluşturur ve birbirine bağlar.
    /// Faz 4: dağ → chunk mesh'leri → kazı → ekonomi + yükseltmeler → oyuncu kazısı → HUD → kamera.
    /// Saf C# sistemler (MiningSystem, EconomyManager, UpgradeManager) burada oluşturulur ve
    /// Faz 9 kayıt sistemi bunlara buradan erişecek.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] private MountainShapeSettings mountainSettings;
        [SerializeField] private VoxelMaterialDatabase materialDatabase;
        [SerializeField] private GameBalanceConfig balanceConfig;
        [SerializeField] private RegionLayout regionLayout;

        [Header("Sahne sistemleri")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private PlayerMiningController playerMining;
        [SerializeField] private MiningEffects miningEffects;
        [SerializeField] private OrbitCameraController orbitCamera;
        [SerializeField] private RegionOverlayController regionOverlay;
        [SerializeField] private MachineController machineController;
        [SerializeField] private MachinePlacementController placementController;
        [SerializeField] private HudController hud;

        public MiningSystem Mining { get; private set; }
        public EconomyManager Economy { get; private set; }
        public UpgradeManager Upgrades { get; private set; }
        public RegionManager Regions { get; private set; }
        public MachineManager Machines { get; private set; }

        private void Start()
        {
            if (!ValidateReferences()) return;

            // 1) Dünya
            var stopwatch = Stopwatch.StartNew();
            VoxelWorld world = MountainGenerator.Generate(mountainSettings);
            long generateMs = stopwatch.ElapsedMilliseconds;

            VoxelMaterialTable materialTable = materialDatabase.CreateTable();

            // Bölge kimlikleri mesh'ten ÖNCE yazılır (mesh UV2'de bölge kimliği taşır).
            RegionStats regionStats = RegionAssigner.Assign(world, regionLayout.regions, materialTable);

            stopwatch.Restart();
            chunkManager.Initialize(world, mountainSettings.voxelSize, materialTable);
            long meshMs = stopwatch.ElapsedMilliseconds;

            // 2) Kurallar ve ekonomi (saf C#)
            Mining = new MiningSystem(world, materialTable);
            Economy = new EconomyManager(balanceConfig.startingMoney) { RewardMultiplier = balanceConfig.rewardMultiplier };
            Economy.Attach(Mining);
            Upgrades = CreateUpgrades(Economy);
            Regions = new RegionManager(Economy, regionLayout.regions, regionStats);
            Mining.IsRegionUnlocked = Regions.IsUnlocked; // kilitli bölgede kazı yok (oyuncu + makineler)
            Machines = new MachineManager(Mining, CreateMachineSpecs());

            // 3) Oyuncu ve makineler
            if (miningEffects != null) miningEffects.Initialize(materialTable, mountainSettings.voxelSize);
            playerMining.Initialize(Mining, chunkManager);
            Upgrades.LevelChanged += (type, level) => ApplyUpgrade(type);
            foreach (UpgradeType type in Upgrades.Types) ApplyUpgrade(type);

            machineController.Initialize(Machines, chunkManager, miningEffects);
            placementController.Initialize(Machines, machineController, Economy, playerMining.Picker, orbitCamera);

            if (regionOverlay != null)
            {
                regionOverlay.Initialize(Regions, Economy, playerMining.Picker, orbitCamera, chunkManager);
            }

            // Modlar: bölge modu ve yerleştirme modu birbirini kapatır; ikisi de kapalıyken sol tık kazar.
            if (regionOverlay != null)
            {
                regionOverlay.ActiveChanged += active =>
                {
                    if (active) placementController.Cancel();
                    UpdateInteractionMode();
                };
            }

            placementController.ActiveChanged += active =>
            {
                if (active && regionOverlay != null) regionOverlay.SetActive(false);
                UpdateInteractionMode();
            };

            // 4) Arayüz ve kamera
            if (hud != null)
            {
                hud.Initialize(new HudServices
                {
                    Economy = Economy,
                    Upgrades = Upgrades,
                    Regions = Regions,
                    RegionOverlay = regionOverlay,
                    Machines = Machines,
                    MachineController = machineController,
                    Placement = placementController,
                    World = world,
                    ChunkManager = chunkManager,
                    PlayerMining = playerMining,
                    OrbitCamera = orbitCamera,
                    Camera = orbitCamera.GetComponent<Camera>(),
                });
            }

            orbitCamera.TerrainHeightProvider = worldPosition => chunkManager.GetSurfaceHeight(worldPosition);
            FaceFirstUnlockedRegion();
            orbitCamera.FrameBounds(chunkManager.ContentBounds, snap: true);
            CheckRegionEconomy();

            Debug.Log($"[Mountain Eater] Dağ hazır: {world.InitialSolidCount:N0} dolu voxel, " +
                      $"ızgara {world.SizeX}×{world.SizeY}×{world.SizeZ} ({world.ChunkCount} chunk, " +
                      $"{chunkManager.MeshedChunkCount} yüzeyli). Üretim {generateMs} ms, mesh {meshMs} ms. " +
                      $"Dağın toplam değeri: {EconomyMath.TotalRewardValue(world, materialTable):N0} $.\n" +
                      DescribeMaterials(world), this);
        }

        private void UpdateInteractionMode()
        {
            bool regionMode = regionOverlay != null && regionOverlay.IsActive;
            playerMining.MiningEnabled = !regionMode && !placementController.IsActive;
        }

        private List<MachineSpec> CreateMachineSpecs()
        {
            var specs = new List<MachineSpec>();
            foreach (MachineDefinition definition in balanceConfig.machines)
            {
                if (definition != null) specs.Add(definition.ToSpec());
            }

            if (specs.Count == 0)
            {
                Debug.LogWarning("[GameBootstrap] Balance Config'te makine tanımı yok; varsayılan Mini Drill kullanılıyor.", this);
                specs.Add(MachineSpec.MiniDrillDefault);
            }

            return specs;
        }

        /// <summary>Kamera başlangıçta ilk açık bölgenin bulunduğu yamaca baksın.</summary>
        private void FaceFirstUnlockedRegion()
        {
            foreach (RegionState region in Regions.Regions)
            {
                if (!region.Unlocked) continue;
                // Kamera bölge yönünde durur, merkeze bakar: yaw = bölge açısı + 180°.
                orbitCamera.SetStartYaw(region.CenterAngle + 180f);
                return;
            }
        }

        /// <summary>Tasarım kuralı: açık bölgelerin değeri, en ucuz kilitli bölgeyi açmaya yetmeli.</summary>
        private void CheckRegionEconomy()
        {
            long unlockedValue = Regions.UnlockedValue();
            long cheapest = Regions.CheapestLockedPrice();
            string summary = $"[Mountain Eater] Bölgeler: açık bölgelerin değeri {unlockedValue:N0} $, " +
                             $"en ucuz kilitli bölge {(cheapest < 0 ? "-" : cheapest.ToString("N0") + " $")}.";

            if (cheapest >= 0 && unlockedValue + balanceConfig.startingMoney < cheapest)
            {
                Debug.LogWarning(summary + " Açık bölge sonraki bölgeyi açmaya YETMİYOR — fiyatları veya bölge sınırlarını ayarla.", this);
            }
            else
            {
                Debug.Log(summary, this);
            }
        }

        /// <summary>"Taş 24.050 (%84,0) · Bakır 3.149 (%11,0) · ..." — dengeleme için.</summary>
        private string DescribeMaterials(VoxelWorld world)
        {
            int[] counts = VoxelAnalysis.CountMaterials(world);
            int total = Mathf.Max(1, world.InitialSolidCount);
            var builder = new System.Text.StringBuilder("Malzemeler: ");
            bool first = true;

            foreach (VoxelMaterialDefinition definition in materialDatabase.Materials)
            {
                int count = counts[(byte)definition.id];
                if (count == 0) continue;
                if (!first) builder.Append(" · ");
                builder.Append($"{definition.displayName} {count:N0} (%{count * 100f / total:0.0})");
                first = false;
            }

            return builder.ToString();
        }

        private UpgradeManager CreateUpgrades(EconomyManager economy)
        {
            var manager = new UpgradeManager(economy);
            foreach (UpgradeDefinition definition in balanceConfig.upgrades)
            {
                if (definition == null) continue;
                if (manager.IsRegistered(definition.type))
                {
                    Debug.LogWarning($"[GameBootstrap] {definition.type} iki kez tanımlanmış; ikincisi yok sayıldı.", definition);
                    continue;
                }

                manager.Register(definition.type, definition.CreateTable(),
                    new UpgradeInfo(definition.displayName, definition.valueLabel, definition.description));
            }

            return manager;
        }

        /// <summary>Bir geliştirmenin güncel değerini ilgili oyun sistemine uygular.</summary>
        private void ApplyUpgrade(UpgradeType type)
        {
            int value = Mathf.RoundToInt(Upgrades.GetCurrentValue(type));
            switch (type)
            {
                case UpgradeType.PickaxeDamage:
                    playerMining.PickaxeDamage = value;
                    break;
                case UpgradeType.MiningRadius:
                    playerMining.MiningRadius = value;
                    break;
                case UpgradeType.DrillDamage:
                    Machines.SetDrillStats(value, Machines.BaseInterval);
                    break;
                case UpgradeType.DrillSpeed:
                    Machines.SetDrillStats(Machines.BaseDamage, Upgrades.GetCurrentValue(type)); // ondalıklı saniye
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Economy != null && Mining != null) Economy.Detach(Mining);
        }

        private bool ValidateReferences()
        {
            bool ok = true;
            if (mountainSettings == null) { Debug.LogError("[GameBootstrap] 'Mountain Settings' atanmamış.", this); ok = false; }
            if (materialDatabase == null) { Debug.LogError("[GameBootstrap] 'Material Database' atanmamış.", this); ok = false; }
            if (balanceConfig == null) { Debug.LogError("[GameBootstrap] 'Balance Config' atanmamış.", this); ok = false; }
            if (regionLayout == null) { Debug.LogError("[GameBootstrap] 'Region Layout' atanmamış.", this); ok = false; }
            if (machineController == null) { Debug.LogError("[GameBootstrap] 'Machine Controller' atanmamış.", this); ok = false; }
            if (placementController == null) { Debug.LogError("[GameBootstrap] 'Placement Controller' atanmamış.", this); ok = false; }
            if (regionOverlay == null) Debug.LogWarning("[GameBootstrap] 'Region Overlay' atanmamış; bölge modu kullanılamaz.", this);
            if (chunkManager == null) { Debug.LogError("[GameBootstrap] 'Chunk Manager' atanmamış.", this); ok = false; }
            if (playerMining == null) { Debug.LogError("[GameBootstrap] 'Player Mining' atanmamış.", this); ok = false; }
            if (orbitCamera == null) { Debug.LogError("[GameBootstrap] 'Orbit Camera' atanmamış.", this); ok = false; }
            if (miningEffects == null) Debug.LogWarning("[GameBootstrap] 'Mining Effects' atanmamış; efekt olmadan devam ediliyor.", this);
            if (hud == null) Debug.LogWarning("[GameBootstrap] 'Hud' atanmamış; arayüz olmadan devam ediliyor.", this);
            return ok;
        }
    }
}
