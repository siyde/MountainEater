using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>
    /// Kazı yarıçapı için 3D küre içindeki voxel ofsetleri: x² + y² + z² ≤ r².
    /// r = 0 → 1, r = 1 → 7, r = 2 → 33, r = 3 → 123 voxel (tasarım belgesindeki tablo).
    /// Ofsetler merkezden dışa doğru sıralıdır ve yarıçap başına bir kez hesaplanıp önbelleğe alınır.
    /// </summary>
    public static class SphereOffsets
    {
        public const int MaxRadius = 16;

        private static readonly Dictionary<int, Vector3Int[]> Cache = new Dictionary<int, Vector3Int[]>();

        public static IReadOnlyList<Vector3Int> Get(int radius)
        {
            radius = Mathf.Clamp(radius, 0, MaxRadius);
            if (Cache.TryGetValue(radius, out Vector3Int[] offsets)) return offsets;

            var list = new List<Vector3Int>();
            int r2 = radius * radius;
            for (int z = -radius; z <= radius; z++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y + z * z <= r2) list.Add(new Vector3Int(x, y, z));
            }

            offsets = list.ToArray();
            Array.Sort(offsets, (a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
            Cache[radius] = offsets;
            return offsets;
        }
    }
}
