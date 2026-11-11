using MountainEater.Cameras;
using MountainEater.Economy;
using MountainEater.Machines;
using MountainEater.Mining;
using MountainEater.Regions;
using MountainEater.Upgrades;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.UI
{
    /// <summary>
    /// HUD'un kullandığı sistemler. GameBootstrap doldurur. Yeni faz yeni sistem eklediğinde
    /// Initialize imzası değişmesin diye tek nesnede toplanır. null olan alanın paneli devre dışı kalır.
    /// </summary>
    public sealed class HudServices
    {
        public EconomyManager Economy;
        public UpgradeManager Upgrades;
        public RegionManager Regions;
        public RegionOverlayController RegionOverlay;
        public MachineManager Machines;
        public MachineController MachineController;
        public MachinePlacementController Placement;
        public VoxelWorld World;
        public ChunkManager ChunkManager;
        public PlayerMiningController PlayerMining;
        public OrbitCameraController OrbitCamera;
        public Camera Camera;
    }
}
