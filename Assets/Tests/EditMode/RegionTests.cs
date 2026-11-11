using System.Collections.Generic;
using MountainEater.Economy;
using MountainEater.Mining;
using MountainEater.Regions;
using MountainEater.Voxel;
using MountainEater.World;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class RegionDefinitionTests
    {
        [TestCase(0f, 90f, 45f, true)]
        [TestCase(0f, 90f, 90f, false)]
        [TestCase(300f, 60f, 330f, true)]   // sarmalı aralık
        [TestCase(300f, 60f, 10f, true)]
        [TestCase(300f, 60f, 180f, false)]
        [TestCase(0f, 360f, 359f, true)]    // tam tur
        public void ContainsAngle(float start, float end, float angle, bool expected)
        {
            var region = new RegionDefinition(1, "R", 0, false, start, end);
            Assert.AreEqual(expected, region.ContainsAngle(angle));
        }

        [Test]
        public void CenterAngle_HandlesWrap()
        {
            Assert.AreEqual(215f, new RegionDefinition(1, "R", 0, false, 175f, 255f).CenterAngle, 0.01f);
            Assert.AreEqual(0f, new RegionDefinition(1, "R", 0, false, 300f, 60f).CenterAngle, 0.01f);
        }
    }

    public sealed class RegionAssignerTests
    {
        private static readonly VoxelMaterialTable Table = VoxelMaterialTable.CreateDefault();

        /// <summary>32×16×32 taban üzerinde 32×8×32 dolu blok.</summary>
        private static VoxelWorld Block()
        {
            var world = new VoxelWorld(2, 1, 2);
            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 32; x++)
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void FirstMatchingRegionWins_AndCatchAllTakesRest()
        {
            VoxelWorld world = Block();
            var regions = new List<RegionDefinition>
            {
                new RegionDefinition(1, "+X yarı", 0, true, angleStart: 0f, angleEnd: 180f),
                new RegionDefinition(2, "Kalan", 100, false),
            };

            RegionStats stats = RegionAssigner.Assign(world, regions, Table);

            // Atan2(dx, dz): +X tarafı 0..180°. Merkez x = 16.
            Assert.AreEqual(1, world.GetRegion(30, 2, 16));
            Assert.AreEqual(2, world.GetRegion(1, 2, 16));
            Assert.AreEqual(world.InitialSolidCount, stats.VoxelCount[1] + stats.VoxelCount[2]);
            Assert.AreEqual(0, stats.VoxelCount[0]);
        }

        [Test]
        public void UncoveredVoxels_GetRegionZero()
        {
            VoxelWorld world = Block();
            var regions = new List<RegionDefinition> { new RegionDefinition(5, "Üst", 0, true, minHeight01: 0.5f) };
            RegionStats stats = RegionAssigner.Assign(world, regions, Table);

            Assert.AreEqual(0, world.GetRegion(5, 0, 5));
            Assert.AreEqual(5, world.GetRegion(5, 7, 5));
            Assert.Greater(stats.VoxelCount[0], 0);
            Assert.AreEqual(world.InitialSolidCount, stats.VoxelCount[0] + stats.VoxelCount[5]);
        }

        [Test]
        public void RadiusRange_SeparatesCoreFromShell()
        {
            VoxelWorld world = Block();
            var regions = new List<RegionDefinition>
            {
                new RegionDefinition(1, "Kabuk", 0, true, minRadius01: 0.5f),
                new RegionDefinition(2, "Çekirdek", 0, false),
            };
            RegionAssigner.Assign(world, regions, Table);

            Assert.AreEqual(2, world.GetRegion(16, 3, 16), "merkez çekirdekte");
            Assert.AreEqual(1, world.GetRegion(0, 3, 0), "köşe kabukta");
        }

        [Test]
        public void Stats_ValueAndCentroid()
        {
            VoxelWorld world = Block();
            var regions = new List<RegionDefinition> { new RegionDefinition(1, "Hepsi", 0, true) };
            RegionStats stats = RegionAssigner.Assign(world, regions, Table);

            Assert.AreEqual(world.InitialSolidCount * 5L, stats.TotalValue[1]);
            Assert.AreEqual(16f, stats.Centroid[1].x, 0.01f);
            Assert.AreEqual(4f, stats.Centroid[1].y, 0.01f);
        }

        [Test]
        public void Assign_IsDeterministic()
        {
            VoxelWorld a = Block();
            VoxelWorld b = Block();
            List<RegionDefinition> regions = RegionLayout.CreateDefaults();
            RegionAssigner.Assign(a, regions, Table);
            RegionAssigner.Assign(b, regions, Table);

            int total = a.SizeX * a.SizeY * a.SizeZ;
            for (int i = 0; i < total; i++) Assert.AreEqual(a.GetRegionAt(i), b.GetRegionAt(i));
        }
    }

    public sealed class RegionManagerTests
    {
        private static RegionManager Create(EconomyManager economy) =>
            new RegionManager(economy, RegionLayout.CreateDefaults(), stats: null);

        [Test]
        public void StartState_MatchesDesign()
        {
            RegionManager manager = Create(new EconomyManager());
            Assert.AreEqual(3, manager.Regions.Count);
            Assert.IsTrue(manager.IsUnlocked(1));
            Assert.IsFalse(manager.IsUnlocked(2));
            Assert.IsFalse(manager.IsUnlocked(3));
            Assert.IsFalse(manager.IsUnlocked(0), "sahipsiz voxel her zaman kilitli");
            Assert.AreEqual(1_000, manager.Find(2).Price);
            Assert.AreEqual(6_000, manager.Find(3).Price);
            Assert.AreEqual(1_000, manager.CheapestLockedPrice());
        }

        [Test]
        public void Purchase_RequiresMoney_DeductsPrice_UnlocksOnce()
        {
            var economy = new EconomyManager(999);
            RegionManager manager = Create(economy);
            var unlocked = new List<byte>();
            manager.RegionUnlocked += r => unlocked.Add(r.Id);

            Assert.IsFalse(manager.TryPurchase(2));
            Assert.AreEqual(999, economy.Money);

            economy.Add(1);
            Assert.IsTrue(manager.CanPurchase(2));
            Assert.IsTrue(manager.TryPurchase(2));
            Assert.AreEqual(0, economy.Money);
            Assert.IsTrue(manager.IsUnlocked(2));

            economy.Add(5_000);
            Assert.IsFalse(manager.TryPurchase(2), "açık bölge tekrar satın alınamaz");
            Assert.IsFalse(manager.TryPurchase(1), "başlangıçta açık bölge satın alınamaz");
            Assert.AreEqual(5_000, economy.Money);
            CollectionAssert.AreEqual(new byte[] { 2 }, unlocked);
        }

        [Test]
        public void RestoreUnlocked_DoesNotCharge()
        {
            var economy = new EconomyManager();
            RegionManager manager = Create(economy);
            manager.RestoreUnlocked(3);
            Assert.IsTrue(manager.IsUnlocked(3));
            Assert.AreEqual(0, economy.Money);
        }

        [Test]
        public void MiningRespectsRegionLocks_UntilPurchased()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(2, 2, 2, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(3, 2, 2, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            world.SetRegion(2, 2, 2, 1);
            world.SetRegion(3, 2, 2, 2);

            var economy = new EconomyManager(1_000);
            RegionManager regions = Create(economy);
            var mining = new MiningSystem(world, VoxelMaterialTable.CreateDefault()) { IsRegionUnlocked = regions.IsUnlocked };

            // Yarıçap 1: kilitli komşu (bölge 2) hasar almaz.
            mining.ApplyHit(new Vector3Int(2, 2, 2), 1, 100, MiningSource.Player);
            Assert.IsFalse(world.IsSolid(2, 2, 2));
            Assert.IsTrue(world.IsSolid(3, 2, 2));
            Assert.AreEqual(0, world.GetDamage(3, 2, 2));

            Assert.IsFalse(mining.ApplyHit(new Vector3Int(3, 2, 2), 0, 100, MiningSource.Machine).HitAnything);

            regions.TryPurchase(2);
            Assert.IsTrue(mining.ApplyHit(new Vector3Int(3, 2, 2), 0, 100, MiningSource.Player).HitAnything);
            Assert.IsFalse(world.IsSolid(3, 2, 2));
        }

        [Test]
        public void DefaultLayout_FirstRegionPaysForSecond_OnGeneratedMountain()
        {
            var settings = ScriptableObject.CreateInstance<MountainShapeSettings>();
            try
            {
                settings.footprintRadius = 60f;
                settings.peakHeight = 45f;
                settings.voxelSize = 2f;

                VoxelWorld world = MountainGenerator.Generate(settings);
                VoxelMaterialTable table = VoxelMaterialTable.CreateDefault();
                List<RegionDefinition> layout = RegionLayout.CreateDefaults();
                RegionStats stats = RegionAssigner.Assign(world, layout, table);
                var manager = new RegionManager(new EconomyManager(), layout, stats);

                Assert.Greater(stats.VoxelCount[1], 0);
                Assert.Greater(stats.VoxelCount[2], 0);
                Assert.Greater(stats.VoxelCount[3], 0);
                Assert.AreEqual(0, stats.VoxelCount[0], "Varsayılan düzende sahipsiz voxel olmamalı");
                Assert.GreaterOrEqual(manager.UnlockedValue(), manager.CheapestLockedPrice(),
                    "1. bölgenin değeri 2. bölgeyi açmaya yetmeli");
                Assert.Less(stats.VoxelCount[1], world.InitialSolidCount / 2, "Başlangıç bölgesi küçük olmalı");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }

    public sealed class RegionMeshTests
    {
        [Test]
        public void Mesher_WritesRegionIdAndBorderFlag()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(8, 8, 8, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(9, 8, 8, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            world.SetRegion(8, 8, 8, 4);
            world.SetRegion(9, 8, 8, 7);

            var mesher = new SurfaceNetsMesher();
            mesher.Build(world, Vector3Int.zero, 1f, VoxelMaterialTable.CreateDefault());

            Assert.AreEqual(mesher.Vertices.Count, mesher.RegionData.Count);

            bool sawBorder = false, sawRegion4 = false, sawRegion7 = false;
            foreach (Vector2 data in mesher.RegionData)
            {
                if (data.y > 0.5f) sawBorder = true;
                if (Mathf.Approximately(data.x, 4f)) sawRegion4 = true;
                if (Mathf.Approximately(data.x, 7f)) sawRegion7 = true;
            }

            Assert.IsTrue(sawBorder, "İki bölgenin arasındaki hücreler sınır olarak işaretlenmeli");
            Assert.IsTrue(sawRegion4);
            Assert.IsTrue(sawRegion7);
        }
    }
}
