using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>Ekran noktasından bulunan voxel.</summary>
    public readonly struct VoxelPick
    {
        public readonly Vector3Int Voxel;
        public readonly Vector3 WorldPoint;
        public readonly Vector3 WorldNormal;
        public readonly string Method;

        public VoxelPick(Vector3Int voxel, Vector3 worldPoint, Vector3 worldNormal, string method)
        {
            Voxel = voxel;
            WorldPoint = worldPoint;
            WorldNormal = worldNormal;
            Method = method;
        }
    }

    /// <summary>
    /// Ekran noktası → imlecin altındaki ilk dolu voxel. Kazı (PlayerMiningController), bölge seçimi ve
    /// Faz 7'de makine yerleştirme aynı nesneyi kullanır.
    ///
    /// Önce fizik raycast'i denenir (chunk collider'ları açıksa görünen yumuşak yüzeyle birebir);
    /// chunk'a çarpmazsa voxel verisi üzerinde DDA ışını. Işının önünde başka bir nesne varsa delip geçmez.
    /// </summary>
    public sealed class VoxelPicker
    {
        private readonly Camera camera;
        private readonly ChunkManager chunkManager;
        private readonly float maxRayDistance;
        private readonly LayerMask physicsMask;

        public VoxelPicker(Camera camera, ChunkManager chunkManager, float maxRayDistance, LayerMask physicsMask)
        {
            this.camera = camera;
            this.chunkManager = chunkManager;
            this.maxRayDistance = maxRayDistance;
            this.physicsMask = physicsMask;
        }

        public VoxelWorld World => chunkManager != null ? chunkManager.World : null;

        public bool TryPick(Vector2 screenPoint, out VoxelPick pick, out string failReason)
        {
            pick = default;
            failReason = string.Empty;

            VoxelWorld world = World;
            if (camera == null || world == null)
            {
                failReason = "kamera veya dünya yok";
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPoint);

            bool physicsHit = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, physicsMask, QueryTriggerInteraction.Ignore);
            if (physicsHit && hit.collider.TryGetComponent(out VoxelChunk _))
            {
                Vector3 voxelPoint = chunkManager.WorldToVoxelSpace(hit.point);
                Vector3 voxelNormal = chunkManager.WorldToVoxelDirection(hit.normal);
                if (MiningTargetResolver.TryResolve(world, voxelPoint, voxelNormal, out Vector3Int target))
                {
                    pick = new VoxelPick(target, hit.point, hit.normal, "fizik");
                    return true;
                }
            }

            float voxelSize = chunkManager.VoxelSize;
            Vector3 origin = chunkManager.WorldToVoxelSpace(ray.origin);
            Vector3 direction = chunkManager.WorldToVoxelDirection(ray.direction);
            if (VoxelRaycast.Raycast(world, origin, direction, maxRayDistance / voxelSize, out VoxelRaycastHit voxelHit))
            {
                float worldDistance = voxelHit.Distance * voxelSize;
                bool blockedByOtherObject = physicsHit && hit.distance + voxelSize < worldDistance;
                if (!blockedByOtherObject)
                {
                    pick = new VoxelPick(voxelHit.Voxel,
                        chunkManager.VoxelSpaceToWorld(voxelHit.Point),
                        chunkManager.VoxelDirectionToWorld(voxelHit.FaceNormal),
                        "voxel ışını");
                    return true;
                }
            }

            failReason = physicsHit
                ? $"ışın '{hit.collider.name}' nesnesine çarptı @ {hit.point}, önünde kaya yok"
                : "ışın kayaya çarpmadı";
            return false;
        }
    }
}
