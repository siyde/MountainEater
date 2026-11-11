using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>
    /// Mesh üzerindeki bir tıklama noktasını hedef voxele çevirir.
    ///
    /// Surface Nets yüzeyi dolu voxel merkezleri ile boş voxel merkezleri ARASINDAN geçer; yani tıklanan nokta
    /// her zaman bir dolu voxel merkezine 1 voxelden yakındır. Noktayı normalin tersine biraz içeri itip
    /// 3×3×3 komşulukta en yakın dolu voxeli seçiyoruz. Böylece hedef hep görünen yüzeyin hemen arkasındaki
    /// voxeldir; yüzeyin arkasına gizlenmiş bir voxel seçilemez.
    /// </summary>
    public static class MiningTargetResolver
    {
        private const float InwardBias = 0.25f; // voxel cinsinden

        /// <param name="voxelSpacePoint">Voxel uzayında nokta (voxel v'nin merkezi = v + 0.5).</param>
        /// <param name="surfaceNormal">Yüzey normali (dışa doğru).</param>
        public static bool TryResolve(VoxelWorld world, Vector3 voxelSpacePoint, Vector3 surfaceNormal, out Vector3Int voxel)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.zero;
            Vector3 probe = voxelSpacePoint - normal * InwardBias;
            var basis = new Vector3Int(Mathf.FloorToInt(probe.x), Mathf.FloorToInt(probe.y), Mathf.FloorToInt(probe.z));

            voxel = default;
            float bestDistance = float.MaxValue;
            bool found = false;

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var candidate = new Vector3Int(basis.x + dx, basis.y + dy, basis.z + dz);
                if (!world.IsSolid(candidate.x, candidate.y, candidate.z)) continue;

                Vector3 center = (Vector3)candidate + new Vector3(0.5f, 0.5f, 0.5f);
                float distance = (center - probe).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    voxel = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
