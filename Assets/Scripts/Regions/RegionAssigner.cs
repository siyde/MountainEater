using System.Collections.Generic;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Regions
{
    /// <summary>Bölge başına üretim istatistikleri (kimliğe göre indeksli, 64 eleman).</summary>
    public sealed class RegionStats
    {
        public const int Capacity = 64;

        public readonly int[] VoxelCount = new int[Capacity];
        public readonly long[] TotalValue = new long[Capacity];

        /// <summary>Bölgedeki voxellerin ortalama konumu (voxel uzayı, merkezler). Kamera odaklama için.</summary>
        public readonly Vector3[] Centroid = new Vector3[Capacity];
    }

    /// <summary>
    /// Bölge tanımlarına göre her dolu voxelin bölge kimliğini VoxelWorld'e yazar (deterministik).
    /// Bölge sınırları veri katmanında olduğu için kazı, makineler ve görsel her zaman aynı sınırı kullanır.
    /// </summary>
    public static class RegionAssigner
    {
        public static RegionStats Assign(VoxelWorld world, IReadOnlyList<RegionDefinition> regions, VoxelMaterialTable materials)
        {
            var stats = new RegionStats();
            if (world == null || regions == null) return stats;

            // Dağın yatay merkezi, en geniş yarıçapı ve en yüksek noktası (dolu voxellerden).
            Vector3Int min = world.ContentMin;
            Vector3Int max = world.ContentMax;
            float centerX = (min.x + max.x + 1) * 0.5f;
            float centerZ = (min.z + max.z + 1) * 0.5f;
            float maxRadius = 0.0001f;
            int count = world.SizeX * world.SizeY * world.SizeZ;

            for (int i = 0; i < count; i++)
            {
                if (world.GetMaterialAt(i) == 0) continue;
                Decompose(world, i, out int x, out _, out int z);
                float dx = x + 0.5f - centerX;
                float dz = z + 0.5f - centerZ;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                if (r > maxRadius) maxRadius = r;
            }

            float heightScale = 1f / Mathf.Max(1, max.y);
            var sums = new Vector3[RegionStats.Capacity];

            for (int i = 0; i < count; i++)
            {
                byte material = world.GetMaterialAt(i);
                if (material == 0)
                {
                    world.SetRegionAt(i, 0);
                    continue;
                }

                Decompose(world, i, out int x, out int y, out int z);
                float dx = x + 0.5f - centerX;
                float dz = z + 0.5f - centerZ;
                float angle = Mathf.Repeat(Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 360f);
                float radius01 = Mathf.Sqrt(dx * dx + dz * dz) / maxRadius;
                float height01 = y * heightScale;

                byte id = 0;
                for (int r = 0; r < regions.Count; r++)
                {
                    RegionDefinition region = regions[r];
                    if (region == null || region.id <= 0 || region.id >= RegionStats.Capacity) continue;
                    if (!region.Contains(angle, height01, radius01)) continue;
                    id = (byte)region.id;
                    break;
                }

                world.SetRegionAt(i, id);
                stats.VoxelCount[id]++;
                stats.TotalValue[id] += materials != null ? materials.GetReward(material) : 0;
                sums[id] += new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
            }

            for (int id = 0; id < RegionStats.Capacity; id++)
            {
                if (stats.VoxelCount[id] > 0) stats.Centroid[id] = sums[id] / stats.VoxelCount[id];
            }

            return stats;
        }

        private static void Decompose(VoxelWorld world, int index, out int x, out int y, out int z)
        {
            x = index % world.SizeX;
            y = (index / world.SizeX) % world.SizeY;
            z = index / (world.SizeX * world.SizeY);
        }
    }
}
