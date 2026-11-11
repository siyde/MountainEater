using System;
using System.Collections.Generic;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.World
{
    /// <summary>
    /// Taş dağın içine madenleri deterministik olarak dağıtır (aynı seed + aynı ayarlar → aynı madenler).
    ///
    /// Her katman için:
    ///   1) Uygun voxeller: hâlâ Taş, yüzeyden en az minDepth içeride, yükseklik aralığında.
    ///   2) Puan = şekil gürültüsü × bölgesel zenginlik + derinlikBonusu
    ///        Blob: 3D gürültü (kümeler)   Vein: 1 - |gürültü| (gürültünün sıfır yüzeyleri boyunca damarlar)
    ///   3) En yüksek puanlı (targetFraction × toplam dolu voxel) kadarı madene çevrilir.
    /// Katmanlar sırayla işlenir; önce gelen (nadir) maden yerini önce seçer, üst üste binme olmaz.
    /// Dolu voxel sayısı ve şekil değişmez, yalnızca malzeme değişir.
    /// </summary>
    public static class OreGenerator
    {
        public static void Distribute(VoxelWorld world, IReadOnlyList<OreLayer> layers, int seed, float voxelSize)
        {
            if (world == null || layers == null || layers.Count == 0) return;

            int totalSolid = 0;
            int maxSolidY = 0;
            int count = world.SizeX * world.SizeY * world.SizeZ;
            for (int i = 0; i < count; i++)
            {
                if (world.GetMaterialAt(i) == 0) continue;
                totalSolid++;
                int y = (i / world.SizeX) % world.SizeY;
                if (y > maxSolidY) maxSolidY = y;
            }

            if (totalSolid == 0) return;

            byte[] depth = VoxelAnalysis.ComputeSurfaceDistance(world);

            var scores = new List<float>(totalSolid);
            var indices = new List<int>(totalSolid);

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                OreLayer layer = layers[layerIndex];
                if (layer == null || layer.material == VoxelMaterialId.Air || layer.material == VoxelMaterialId.Stone)
                {
                    continue;
                }

                int target = Mathf.RoundToInt(Mathf.Clamp01(layer.targetFraction) * totalSolid);
                if (target <= 0) continue;

                CollectCandidates(world, layer, layerIndex, seed, voxelSize, depth, maxSolidY, scores, indices);
                if (indices.Count == 0) continue;

                int[] chosen = SelectTop(scores, indices, target);
                foreach (int index in chosen)
                {
                    world.SetGeneratedMaterialAt(index, layer.material);
                }
            }
        }

        private static void CollectCandidates(VoxelWorld world, OreLayer layer, int layerIndex, int seed, float voxelSize,
            byte[] depth, int maxSolidY, List<float> scores, List<int> indices)
        {
            scores.Clear();
            indices.Clear();

            // Katman ve madene özgü seed'ler: katman eklemek/çıkarmak diğerlerinin şeklini bozmaz.
            var shapeNoise = new GradientNoise3D(unchecked(seed * 7919 + (int)layer.material * 104729 + 11));
            var richnessNoise = new GradientNoise3D(unchecked(seed * 6271 + (int)layer.material * 1301 + 29));

            float shapeFrequency = layer.noiseScale * voxelSize;
            float richnessFrequency = layer.richnessScale * voxelSize;
            float minHeight = Mathf.Min(layer.minHeight01, layer.maxHeight01);
            float maxHeight = Mathf.Max(layer.minHeight01, layer.maxHeight01);
            float heightScale = 1f / Mathf.Max(1, maxSolidY);
            float depthRange = Mathf.Max(1, layer.depthRange);

            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            {
                float height01 = y * heightScale;
                if (height01 < minHeight || height01 > maxHeight) continue;

                int row = world.GetIndex(0, y, z);
                for (int x = 0; x < world.SizeX; x++)
                {
                    int i = row + x;
                    if (world.GetMaterialAt(i) != (byte)VoxelMaterialId.Stone) continue;

                    int d = depth[i];
                    if (d < layer.minDepth) continue;

                    float n = shapeNoise.Sample(x * shapeFrequency, y * shapeFrequency, z * shapeFrequency);
                    float shapeScore = layer.shape == OreShape.Vein ? 1f - Mathf.Abs(n) : n * 0.5f + 0.5f;

                    float richness = richnessNoise.Sample(x * richnessFrequency, 0.37f, z * richnessFrequency); // [-1, 1]
                    float richnessFactor = 1f + layer.richnessStrength * Mathf.Clamp(richness, -1f, 1f);

                    float depthFactor = Mathf.Clamp01((d - layer.minDepth) / depthRange);
                    float score = shapeScore * richnessFactor + layer.depthWeight * depthFactor;

                    scores.Add(score);
                    indices.Add(i);
                }
            }
        }

        /// <summary>En yüksek puanlı 'target' adayın indeksleri (aday azsa hepsi).</summary>
        private static int[] SelectTop(List<float> scores, List<int> indices, int target)
        {
            int[] indexArray = indices.ToArray();
            if (indexArray.Length <= target) return indexArray;

            float[] scoreArray = scores.ToArray();
            Array.Sort(scoreArray, indexArray); // artan; deterministik (aynı girdi → aynı sıra)

            var result = new int[target];
            Array.Copy(indexArray, indexArray.Length - target, result, 0, target);
            return result;
        }
    }
}
