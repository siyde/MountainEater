using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.World
{
    /// <summary>
    /// MountainShapeSettings'ten deterministik voxel dağ üretir.
    ///
    /// Her voxel merkezi için işaretli mesafe (dünya birimi):
    ///     s = yükseklik(x, z) - y + kayaDetayı(x, y, z) × kenarMaskesi
    /// s > 0 → kaya içindeyiz. density = 0.5 + 0.5 × s / voxelSize (0..1'e sıkıştırılır)
    ///   dolu ↔ density >= 0.5. Ara değerler yüzeyin voxel merkezleri arasındaki tam yerini taşır;
    ///   Surface Nets bu sayede dağı basamaksız, yumuşak çizer.
    ///
    /// Izgara kuralı: XZ ızgarası merkezde (voxel x'in merkezi = (x + 0.5 - SizeX/2) × voxelSize),
    /// Y = 0 zemin. ChunkManager aynı kuralla konumlanır.
    /// </summary>
    public static class MountainGenerator
    {
        private const int AirPaddingVoxels = 2;

        public static VoxelWorld Generate(MountainShapeSettings settings)
        {
            var shape = new MountainShape(settings);
            float voxelSize = Mathf.Max(0.1f, settings.voxelSize);
            const int chunkSize = VoxelWorld.ChunkSize;

            // 1) Yatay boyut: alan bükme tabanı yarıçapın biraz dışına taşıyabilir.
            float extent = shape.FootprintRadius * (1f + settings.warpStrength * 1.5f) + voxelSize * AirPaddingVoxels;
            int voxelsXZ = Mathf.CeilToInt(2f * extent / voxelSize);
            int chunksXZ = Mathf.Max(1, CeilDiv(voxelsXZ, chunkSize));
            int sizeXZ = chunksXZ * chunkSize;
            float half = sizeXZ * 0.5f;

            // 2) Sütun yükseklikleri (bir kez hesaplanır, Y boyunca tekrar kullanılır).
            var heights = new float[sizeXZ * sizeXZ];
            float maxHeight = 0f;
            for (int z = 0; z < sizeXZ; z++)
            {
                float wz = (z + 0.5f - half) * voxelSize;
                for (int x = 0; x < sizeXZ; x++)
                {
                    float wx = (x + 0.5f - half) * voxelSize;
                    float h = shape.SampleHeight(wx, wz);
                    heights[x + z * sizeXZ] = h;
                    if (h > maxHeight) maxHeight = h;
                }
            }

            // 3) Dikey boyut gerçek en yüksek noktaya göre seçilir.
            float detail = shape.RockDetailStrength;
            int topVoxel = Mathf.CeilToInt((maxHeight + detail) / voxelSize) + AirPaddingVoxels;
            int chunksY = Mathf.Max(1, CeilDiv(topVoxel, chunkSize));

            var world = new VoxelWorld(chunksXZ, chunksY, chunksXZ);

            // 4) Voxeller
            for (int z = 0; z < sizeXZ; z++)
            {
                float wz = (z + 0.5f - half) * voxelSize;
                for (int x = 0; x < sizeXZ; x++)
                {
                    float h = heights[x + z * sizeXZ];
                    if (h <= 0f) continue; // taban dışı: hava (diziler zaten 0)

                    float wx = (x + 0.5f - half) * voxelSize;

                    // Kaya detayı dağ eteğinde sönümlenir: düz zeminde tek başına tümsekler oluşmasın.
                    float detailMask = Mathf.Clamp01(h / (detail + 2f * voxelSize));
                    int yTop = Mathf.Min(world.SizeY - 1, Mathf.CeilToInt((h + detail) / voxelSize) + 1);

                    for (int y = 0; y <= yTop; y++)
                    {
                        float wy = (y + 0.5f) * voxelSize;
                        float signedDistance = h - wy + shape.SampleRockDetail(wx, wy, wz) * detailMask;
                        float density = Mathf.Clamp01(0.5f + 0.5f * signedDistance / voxelSize);
                        if (density <= 0f) continue;

                        VoxelMaterialId material = density >= 0.5f ? VoxelMaterialId.Stone : VoxelMaterialId.Air;
                        world.SetGeneratedVoxel(x, y, z, material, (byte)Mathf.RoundToInt(density * 255f));
                    }
                }
            }

            // 5) Madenler: şekil hazır olduktan sonra taşın bir kısmı madene çevrilir (dolu sayısı değişmez).
            OreGenerator.Distribute(world, settings.ores, settings.seed, voxelSize);

            world.FinalizeGeneration();
            return world;
        }

        private static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;
    }
}
