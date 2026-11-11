using System.Collections.Generic;
using MountainEater.Voxel;
using MountainEater.World;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class VoxelAnalysisTests
    {
        [Test]
        public void SurfaceDistance_OfCube_GrowsTowardsCenter()
        {
            // 7×7×7 küp, havada (y = 4..10, zemine değmiyor).
            var world = new VoxelWorld(1, 1, 1);
            for (int z = 4; z <= 10; z++)
            for (int y = 4; y <= 10; y++)
            for (int x = 4; x <= 10; x++)
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();

            byte[] depth = VoxelAnalysis.ComputeSurfaceDistance(world);

            Assert.AreEqual(1, depth[world.GetIndex(4, 7, 7)], "yüzey");
            Assert.AreEqual(2, depth[world.GetIndex(5, 7, 7)]);
            Assert.AreEqual(4, depth[world.GetIndex(7, 7, 7)], "merkez");
            Assert.AreEqual(0, depth[world.GetIndex(0, 0, 0)], "hava");
        }

        [Test]
        public void SurfaceDistance_TreatsBelowWorldAsGround()
        {
            // Tabana oturan tek sıra: y = 0 voxelinin altı zemin, üstü hava.
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(5, 0, 5, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(5, 1, 5, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();

            byte[] depth = VoxelAnalysis.ComputeSurfaceDistance(world);
            Assert.AreEqual(1, depth[world.GetIndex(5, 0, 5)], "yanları hava olduğu için yine yüzey");
            Assert.AreEqual(1, depth[world.GetIndex(5, 1, 5)]);
        }

        [Test]
        public void CountMaterials_CountsEachMaterial()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(0, 0, 0, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(1, 0, 0, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(2, 0, 0, VoxelMaterialId.Gold, 255);
            world.FinalizeGeneration();

            int[] counts = VoxelAnalysis.CountMaterials(world);
            Assert.AreEqual(2, counts[(byte)VoxelMaterialId.Stone]);
            Assert.AreEqual(1, counts[(byte)VoxelMaterialId.Gold]);
            Assert.AreEqual(0, counts[0]);
        }
    }

    public sealed class OreGeneratorTests
    {
        /// <summary>32×32×32 tamamen taş blok (zemine oturuyor). Derinlik 16'ya kadar çıkar.</summary>
        private static VoxelWorld SolidBlock()
        {
            var world = new VoxelWorld(2, 2, 2);
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        private static List<OreLayer> TestLayers() => new List<OreLayer>
        {
            new OreLayer(VoxelMaterialId.Crystal, 0.01f, minDepth: 6, OreShape.Blob, 0.15f, 1f, 6, 0.5f),
            new OreLayer(VoxelMaterialId.Gold, 0.04f, minDepth: 3, OreShape.Vein, 0.05f, 0.6f, 8, 0.5f),
            new OreLayer(VoxelMaterialId.Copper, 0.10f, minDepth: 1, OreShape.Blob, 0.09f, 0.3f, 10, 0.5f),
        };

        [Test]
        public void Distribute_HitsTargetFractions()
        {
            VoxelWorld world = SolidBlock();
            int total = world.InitialSolidCount;
            OreGenerator.Distribute(world, TestLayers(), seed: 42, voxelSize: 1f);
            int[] counts = VoxelAnalysis.CountMaterials(world);

            Assert.AreEqual(Mathf.RoundToInt(0.01f * total), counts[(byte)VoxelMaterialId.Crystal]);
            Assert.AreEqual(Mathf.RoundToInt(0.04f * total), counts[(byte)VoxelMaterialId.Gold]);
            Assert.AreEqual(Mathf.RoundToInt(0.10f * total), counts[(byte)VoxelMaterialId.Copper]);
            Assert.Greater(counts[(byte)VoxelMaterialId.Stone], total / 2, "Taş çoğunlukta olmalı");
        }

        [Test]
        public void Distribute_DoesNotChangeShapeOrSolidCount()
        {
            VoxelWorld world = SolidBlock();
            int before = world.InitialSolidCount;
            OreGenerator.Distribute(world, TestLayers(), 42, 1f);
            world.FinalizeGeneration();

            Assert.AreEqual(before, world.InitialSolidCount);
            int[] counts = VoxelAnalysis.CountMaterials(world);
            int sum = 0;
            for (int i = 1; i < counts.Length; i++) sum += counts[i];
            Assert.AreEqual(before, sum);
        }

        [Test]
        public void Distribute_IsDeterministic_AndSeedDependent()
        {
            VoxelWorld a = SolidBlock();
            VoxelWorld b = SolidBlock();
            VoxelWorld c = SolidBlock();
            OreGenerator.Distribute(a, TestLayers(), 42, 1f);
            OreGenerator.Distribute(b, TestLayers(), 42, 1f);
            OreGenerator.Distribute(c, TestLayers(), 43, 1f);

            int differences = 0;
            int total = a.SizeX * a.SizeY * a.SizeZ;
            for (int i = 0; i < total; i++)
            {
                Assert.AreEqual(a.GetMaterialAt(i), b.GetMaterialAt(i));
                if (a.GetMaterialAt(i) != c.GetMaterialAt(i)) differences++;
            }

            Assert.Greater(differences, 100, "Farklı seed farklı maden dağılımı vermeli");
        }

        [Test]
        public void Distribute_RespectsMinDepth()
        {
            VoxelWorld world = SolidBlock();
            byte[] depth = VoxelAnalysis.ComputeSurfaceDistance(world);
            OreGenerator.Distribute(world, TestLayers(), 42, 1f);

            int total = world.SizeX * world.SizeY * world.SizeZ;
            for (int i = 0; i < total; i++)
            {
                var material = (VoxelMaterialId)world.GetMaterialAt(i);
                if (material == VoxelMaterialId.Crystal) Assert.GreaterOrEqual(depth[i], 6);
                if (material == VoxelMaterialId.Gold) Assert.GreaterOrEqual(depth[i], 3);
            }
        }

        [Test]
        public void Distribute_FavoursInterior_WhenDepthWeighted()
        {
            VoxelWorld world = SolidBlock();
            byte[] depth = VoxelAnalysis.ComputeSurfaceDistance(world);
            var layers = new List<OreLayer>
            {
                new OreLayer(VoxelMaterialId.Gold, 0.05f, minDepth: 1, OreShape.Blob, 0.1f, 2f, 10, 0f),
            };
            OreGenerator.Distribute(world, layers, 7, 1f);

            double goldDepth = 0, stoneDepth = 0;
            int goldCount = 0, stoneCount = 0;
            int total = world.SizeX * world.SizeY * world.SizeZ;
            for (int i = 0; i < total; i++)
            {
                if (world.GetMaterialAt(i) == (byte)VoxelMaterialId.Gold) { goldDepth += depth[i]; goldCount++; }
                else if (world.GetMaterialAt(i) == (byte)VoxelMaterialId.Stone) { stoneDepth += depth[i]; stoneCount++; }
            }

            Assert.Greater(goldDepth / goldCount, stoneDepth / stoneCount, "Altın ortalamada daha derinde olmalı");
        }

        [Test]
        public void Distribute_RespectsHeightRange()
        {
            VoxelWorld world = SolidBlock();
            var layers = new List<OreLayer>
            {
                new OreLayer(VoxelMaterialId.Copper, 0.05f, 1, OreShape.Blob, 0.1f, 0f, 10, 0f, maxHeight01: 0.25f),
            };
            OreGenerator.Distribute(world, layers, 3, 1f);

            int maxAllowedY = Mathf.FloorToInt(0.25f * (world.SizeY - 1));
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = maxAllowedY + 1; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
                Assert.AreNotEqual(VoxelMaterialId.Copper, world.GetMaterial(x, y, z));
        }

        [Test]
        public void EmptyLayerList_LeavesAllStone()
        {
            VoxelWorld world = SolidBlock();
            OreGenerator.Distribute(world, new List<OreLayer>(), 1, 1f);
            Assert.AreEqual(world.InitialSolidCount, VoxelAnalysis.CountMaterials(world)[(byte)VoxelMaterialId.Stone]);
        }

        [Test]
        public void GeneratedMountain_ContainsAllOres_WithStoneMajority()
        {
            var settings = ScriptableObject.CreateInstance<MountainShapeSettings>();
            try
            {
                settings.footprintRadius = 60f;
                settings.peakHeight = 45f;
                settings.voxelSize = 2f;

                VoxelWorld world = MountainGenerator.Generate(settings);
                int[] counts = VoxelAnalysis.CountMaterials(world);
                int total = world.InitialSolidCount;

                Assert.Greater(counts[(byte)VoxelMaterialId.Copper], 0);
                Assert.Greater(counts[(byte)VoxelMaterialId.Gold], 0);
                Assert.Greater(counts[(byte)VoxelMaterialId.Crystal], 0);
                Assert.Greater(counts[(byte)VoxelMaterialId.Stone], total * 0.7f);
                Assert.Greater(counts[(byte)VoxelMaterialId.Copper], counts[(byte)VoxelMaterialId.Gold]);
                Assert.Greater(counts[(byte)VoxelMaterialId.Gold], counts[(byte)VoxelMaterialId.Crystal]);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }

    public sealed class MaterialAppearanceTests
    {
        [Test]
        public void Smoothness_GoesToVertexAlpha_AndOresShineMoreThanStone()
        {
            VoxelMaterialTable table = VoxelMaterialTable.CreateDefault();
            Assert.Less(table.GetSmoothness((byte)VoxelMaterialId.Stone), table.GetSmoothness((byte)VoxelMaterialId.Copper));
            Assert.Less(table.GetSmoothness((byte)VoxelMaterialId.Copper), table.GetSmoothness((byte)VoxelMaterialId.Gold));
            Assert.Less(table.GetSmoothness((byte)VoxelMaterialId.Gold), table.GetSmoothness((byte)VoxelMaterialId.Crystal));

            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(8, 8, 8, VoxelMaterialId.Crystal, 255);
            world.FinalizeGeneration();
            var mesher = new SurfaceNetsMesher();
            mesher.Build(world, Vector3Int.zero, 1f, table);

            Assert.AreEqual(table.GetSmoothness((byte)VoxelMaterialId.Crystal), mesher.Colors[0].a);
        }
    }
}
