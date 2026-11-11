using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>Voxel ışın testinin sonucu (voxel uzayında).</summary>
    public struct VoxelRaycastHit
    {
        /// <summary>Işının ilk girdiği dolu voxel.</summary>
        public Vector3Int Voxel;

        /// <summary>Işının voxele girdiği yüzün normali (eksen yönlü, dışa doğru).</summary>
        public Vector3Int FaceNormal;

        /// <summary>Başlangıçtan çarpma noktasına mesafe (voxel birimi).</summary>
        public float Distance;

        /// <summary>Çarpma noktası (voxel uzayı).</summary>
        public Vector3 Point;
    }

    /// <summary>
    /// Fizik motoru olmadan, doğrudan voxel verisi üzerinde ışın testi (Amanatides–Woo "3D DDA").
    /// Işın ızgarada voxel voxel ilerler ve ilk dolu voxelde durur.
    ///
    /// Neden: Kazı hedefi her zaman gerçek veriden bulunur; collider'ın güncel olmasına bağlı kalmaz.
    /// Ayrıca Faz 7'de makineler de aynı fonksiyonla hedef arayabilir.
    /// Not: Görünen yüzey yumuşak olduğu için küp sınırından en fazla yarım voxel sapabilir.
    /// </summary>
    public static class VoxelRaycast
    {
        private const float Epsilon = 1e-4f;

        /// <param name="origin">Voxel uzayında başlangıç (voxel v'nin merkezi = v + 0.5).</param>
        /// <param name="direction">Voxel uzayında yön (normalize edilmesi gerekmez).</param>
        /// <param name="maxDistance">Voxel birimi cinsinden en uzun mesafe.</param>
        public static bool Raycast(VoxelWorld world, Vector3 origin, Vector3 direction, float maxDistance, out VoxelRaycastHit hit)
        {
            hit = default;
            if (direction.sqrMagnitude < 1e-12f || maxDistance <= 0f) return false;
            direction.Normalize();

            // 1) Işını dünya kutusuna kırp: [0, Size]
            var boxMax = new Vector3(world.SizeX, world.SizeY, world.SizeZ);
            float tEnter = 0f;
            float tExit = maxDistance;
            int enterAxis = -1;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = origin[axis];
                float d = direction[axis];
                if (Mathf.Abs(d) < 1e-9f)
                {
                    if (o < 0f || o > boxMax[axis]) return false;
                    continue;
                }

                float t1 = (0f - o) / d;
                float t2 = (boxMax[axis] - o) / d;
                if (t1 > t2) (t1, t2) = (t2, t1);
                if (t1 > tEnter)
                {
                    tEnter = t1;
                    enterAxis = axis;
                }

                if (t2 < tExit) tExit = t2;
                if (tEnter > tExit) return false;
            }

            // 2) Başlangıç voxeli
            Vector3 start = origin + direction * (tEnter + Epsilon);
            int x = Mathf.Clamp(Mathf.FloorToInt(start.x), 0, world.SizeX - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(start.y), 0, world.SizeY - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(start.z), 0, world.SizeZ - 1);

            int stepX = direction.x > 0f ? 1 : -1;
            int stepY = direction.y > 0f ? 1 : -1;
            int stepZ = direction.z > 0f ? 1 : -1;

            float tMaxX = NextBoundary(origin.x, direction.x, x);
            float tMaxY = NextBoundary(origin.y, direction.y, y);
            float tMaxZ = NextBoundary(origin.z, direction.z, z);
            float tDeltaX = Mathf.Abs(direction.x) < 1e-9f ? float.PositiveInfinity : Mathf.Abs(1f / direction.x);
            float tDeltaY = Mathf.Abs(direction.y) < 1e-9f ? float.PositiveInfinity : Mathf.Abs(1f / direction.y);
            float tDeltaZ = Mathf.Abs(direction.z) < 1e-9f ? float.PositiveInfinity : Mathf.Abs(1f / direction.z);

            Vector3Int normal = EntryNormal(enterAxis, direction);
            float t = tEnter;

            // 3) Voxel voxel ilerle
            while (t <= tExit)
            {
                if (world.IsSolid(x, y, z))
                {
                    hit = new VoxelRaycastHit
                    {
                        Voxel = new Vector3Int(x, y, z),
                        FaceNormal = normal,
                        Distance = t,
                        Point = origin + direction * t,
                    };
                    return true;
                }

                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    normal = new Vector3Int(-stepX, 0, 0);
                }
                else if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    normal = new Vector3Int(0, -stepY, 0);
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    normal = new Vector3Int(0, 0, -stepZ);
                }

                if (!world.InBounds(x, y, z)) return false;
            }

            return false;
        }

        private static float NextBoundary(float origin, float direction, int cell)
        {
            if (direction > 1e-9f) return (cell + 1 - origin) / direction;
            if (direction < -1e-9f) return (cell - origin) / direction;
            return float.PositiveInfinity;
        }

        private static Vector3Int EntryNormal(int enterAxis, Vector3 direction)
        {
            if (enterAxis < 0)
            {
                // Işın dünyanın içinden başladı: en baskın eksenin tersini kullan.
                Vector3 a = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
                enterAxis = a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
            }

            var normal = Vector3Int.zero;
            normal[enterAxis] = direction[enterAxis] > 0f ? -1 : 1;
            return normal;
        }
    }
}
