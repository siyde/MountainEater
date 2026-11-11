using System.Collections.Generic;
using MountainEater.Economy;
using MountainEater.Machines;
using MountainEater.Mining;
using MountainEater.Upgrades;
using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class MachineManagerTests
    {
        private static readonly VoxelMaterialTable Table = VoxelMaterialTable.CreateDefault();

        private VoxelWorld world;
        private MiningSystem mining;
        private EconomyManager economy;
        private MachineManager machines;
        private HashSet<byte> unlockedRegions;

        /// <summary>
        /// 32×16×32 dünya; y = 0..7 dolu taş blok. x &lt; 16 bölge 1 (açık), x ≥ 16 bölge 2 (kilitli).
        /// Üst yüzey y = 7 (açıkta), içi kapalı.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            world = new VoxelWorld(2, 1, 2);
            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 32; x++)
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();

            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 32; x++)
                world.SetRegion(x, y, z, (byte)(x < 16 ? 1 : 2));

            unlockedRegions = new HashSet<byte> { 1 };
            mining = new MiningSystem(world, Table) { IsRegionUnlocked = id => unlockedRegions.Contains(id) };
            economy = new EconomyManager();
            economy.Attach(mining);
            machines = new MachineManager(mining, new[] { MachineSpec.MiniDrillDefault });
        }

        private static readonly Vector3Int SurfaceVoxel = new Vector3Int(8, 7, 8);

        [Test]
        public void CanPlace_OnlyOnExposedSolidVoxelInUnlockedRegion()
        {
            Assert.IsTrue(machines.CanPlaceAt(SurfaceVoxel, out _));
            Assert.IsFalse(machines.CanPlaceAt(new Vector3Int(8, 8, 8), out _), "hava");
            Assert.IsFalse(machines.CanPlaceAt(new Vector3Int(8, 3, 8), out _), "içeride, açıkta değil");
            Assert.IsFalse(machines.CanPlaceAt(new Vector3Int(24, 7, 8), out string reason), "kilitli bölge");
            Assert.AreEqual("Bölge kilitli", reason);
        }

        [Test]
        public void BuyAndPlace_ChargesOnlyOnSuccess_AndPriceGrows()
        {
            economy.Add(1_000);

            Assert.IsNull(machines.TryBuyAndPlace(MachineType.MiniDrill, new Vector3Int(24, 7, 8), Vector3.up, economy, out _));
            Assert.AreEqual(1_000, economy.Money, "uygunsuz yerde para düşmemeli");

            Assert.AreEqual(250, machines.GetNextPrice(MachineType.MiniDrill));
            Machine first = machines.TryBuyAndPlace(MachineType.MiniDrill, SurfaceVoxel, Vector3.up, economy, out _);
            Assert.IsNotNull(first);
            Assert.AreEqual(750, economy.Money);
            Assert.AreEqual(1, first.Region);
            Assert.AreEqual(MachineState.Working, first.State);

            Assert.AreEqual(290, machines.GetNextPrice(MachineType.MiniDrill)); // 250 × 1.15 = 287.5 → 290
        }

        [Test]
        public void BuyWithoutMoney_Fails()
        {
            economy.Add(249);
            Assert.IsNull(machines.TryBuyAndPlace(MachineType.MiniDrill, SurfaceVoxel, Vector3.up, economy, out string reason));
            Assert.AreEqual("Yetersiz para", reason);
            Assert.AreEqual(0, machines.Machines.Count);
        }

        [Test]
        public void MiniDrill_BreaksOneStoneEveryThreeSeconds_AndPays()
        {
            machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            int start = world.SolidCount;

            machines.Tick(2.9f);
            Assert.AreEqual(start, world.SolidCount, "3 saniyeden önce vuruş yok");

            machines.Tick(0.11f);
            Assert.AreEqual(start - 1, world.SolidCount, "3 HP taş, 3 hasar → 1 vuruşta kırılır");
            Assert.AreEqual(5, economy.Money);

            for (int i = 0; i < 10; i++) machines.Tick(3f);
            Assert.AreEqual(start - 11, world.SolidCount);
            Assert.AreEqual(55, economy.Money);
            Assert.AreEqual(11, machines.Machines[0].BlocksMined);
        }

        [Test]
        public void Drill_DigsFromSurfaceInwards_NearestFirst()
        {
            Machine drill = machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            machines.Tick(3f);
            Assert.IsFalse(world.IsSolid(SurfaceVoxel.x, SurfaceVoxel.y, SurfaceVoxel.z), "ilk hedef yerleştirildiği voxel");

            // Sonraki hedef komşu (mesafe 1) olmalı; içerideki kapalı voxeller açılmadan hedeflenmemeli.
            machines.Tick(3f);
            Assert.AreEqual(1, (drill.Target - SurfaceVoxel).sqrMagnitude <= 1 ? 1 : 0, "ikinci kırılan voxel bitişik olmalı");
        }

        [Test]
        public void Drill_StaysInsideItsOwnRegion_EvenWhenOtherRegionIsUnlocked()
        {
            unlockedRegions.Add(2);
            var anchor = new Vector3Int(15, 7, 8); // bölge 1'in kenarı, komşusu x=16 bölge 2
            machines.Place(MachineType.MiniDrill, anchor, Vector3.up);

            for (int i = 0; i < 60; i++) machines.Tick(3f);

            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 16; x < 32; x++)
                Assert.IsTrue(world.IsSolid(x, y, z), $"Makine başka bölgeyi kazdı: {x},{y},{z}");
        }

        [Test]
        public void Drill_WithRadius_DoesNotSpillIntoOtherRegion()
        {
            unlockedRegions.Add(2);
            var specs = new[] { new MachineSpec(MachineType.MiniDrill, "Area", "", 0, 1f, hitRadius: 3, workRadius: 4) };
            var areaMachines = new MachineManager(mining, specs);
            areaMachines.SetDrillStats(100, 1f);
            areaMachines.Place(MachineType.MiniDrill, new Vector3Int(15, 7, 8), Vector3.up);

            for (int i = 0; i < 20; i++) areaMachines.Tick(1f);

            for (int z = 0; z < 32; z++)
            for (int y = 0; y < 8; y++)
            for (int x = 16; x < 32; x++)
                Assert.AreEqual(0, world.GetDamage(x, y, z) + (world.IsSolid(x, y, z) ? 0 : 1), $"Taşma: {x},{y},{z}");
        }

        [Test]
        public void Drill_StopsWhenWorkAreaIsEmpty_AndNotifiesOnce()
        {
            var specs = new[] { new MachineSpec(MachineType.MiniDrill, "Tiny", "", 0, 1f, 0, workRadius: 1) };
            var tinyMachines = new MachineManager(mining, specs);
            tinyMachines.SetDrillStats(100, 1f);
            var stopped = new List<int>();
            tinyMachines.MachineStopped += m => stopped.Add(m.Id);

            Machine drill = tinyMachines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            for (int i = 0; i < 30; i++) tinyMachines.Tick(1f);

            Assert.AreEqual(MachineState.NoTarget, drill.State);
            Assert.AreEqual(1, stopped.Count, "Durma bildirimi bir kez");
            Assert.Greater(drill.BlocksMined, 0);
            Assert.LessOrEqual(drill.BlocksMined, 7, "Yarıçap 1 küre en fazla 7 voxel");

            int solidAfterStop = world.SolidCount;
            tinyMachines.Tick(100f);
            Assert.AreEqual(solidAfterStop, world.SolidCount, "Durmuş makine kazmaz");
        }

        [Test]
        public void MovingStoppedDrill_ResumesWork()
        {
            var specs = new[] { new MachineSpec(MachineType.MiniDrill, "Tiny", "", 0, 1f, 0, workRadius: 1) };
            var tinyMachines = new MachineManager(mining, specs);
            tinyMachines.SetDrillStats(100, 1f);
            Machine drill = tinyMachines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            for (int i = 0; i < 30; i++) tinyMachines.Tick(1f);
            Assert.AreEqual(MachineState.NoTarget, drill.State);

            Assert.IsTrue(tinyMachines.TryMove(drill.Id, new Vector3Int(2, 7, 2), Vector3.up, out _));
            Assert.AreEqual(MachineState.Working, drill.State);

            int before = world.SolidCount;
            tinyMachines.Tick(1f);
            Assert.AreEqual(before - 1, world.SolidCount);
        }

        [Test]
        public void MoveToLockedRegion_IsRejected()
        {
            Machine drill = machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            Assert.IsFalse(machines.TryMove(drill.Id, new Vector3Int(24, 7, 8), Vector3.up, out _));
            Assert.AreEqual(SurfaceVoxel, drill.Anchor);
        }

        [Test]
        public void PlayerBreakingDrillTarget_NoDoublePay_DrillRetargets()
        {
            Machine drill = machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            machines.SetDrillStats(1, 3f); // taşı 3 vuruşta kırar

            machines.Tick(3f); // hedefe 1 hasar
            Assert.IsTrue(drill.HasTarget);
            Vector3Int target = drill.Target;

            MiningResult player = mining.ApplyHit(target, 0, 10, MiningSource.Player); // oyuncu kırar
            Assert.AreEqual(1, player.VoxelsBroken);
            Assert.AreEqual(5, economy.Money);

            machines.Tick(3f); // makine yeni hedef seçmeli, eski hedef için ödeme olmamalı
            Assert.AreNotEqual(target, drill.Target);
            Assert.AreEqual(5, economy.Money, "Kırılmış voxel için makine ödül almamalı");
        }

        [Test]
        public void LongFrame_IsCapped_NoFreeze()
        {
            machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            int before = world.SolidCount;
            machines.Tick(10_000f);
            Assert.LessOrEqual(before - world.SolidCount, MachineManager.MaxHitsPerTick);
        }

        [Test]
        public void DrillStats_ComeFromUpgrades()
        {
            machines.SetDrillStats(8, 1.5f);
            Assert.AreEqual(8, machines.GetDamage(MachineType.MiniDrill));
            Assert.AreEqual(1.5f, machines.GetInterval(MachineType.MiniDrill), 1e-4f);

            machines.SetDrillStats(0, 0f);
            Assert.AreEqual(1, machines.GetDamage(MachineType.MiniDrill), "hasar en az 1");
            Assert.AreEqual(MachineManager.MinInterval, machines.GetInterval(MachineType.MiniDrill), 1e-4f);
        }

        [Test]
        public void CountInRegion_CountsPlacedMachines()
        {
            machines.Place(MachineType.MiniDrill, SurfaceVoxel, Vector3.up);
            machines.Place(MachineType.MiniDrill, new Vector3Int(3, 7, 3), Vector3.up);
            Assert.AreEqual(2, machines.CountInRegion(1));
            Assert.AreEqual(0, machines.CountInRegion(2));
        }
    }

    public sealed class DrillUpgradeTableTests
    {
        [Test]
        public void DrillDamage_StartsAtMiniDrillDamage_AndGrows()
        {
            UpgradeTable table = DefaultUpgradeData.CreateDrillDamageTable();
            Assert.AreEqual(3f, table.GetValue(1));
            Assert.AreEqual(5f, table.GetValue(2));
            Assert.AreEqual(400, table.GetCost(2));
            for (int level = 2; level <= table.MaxLevel; level++)
            {
                Assert.Greater(table.GetValue(level), table.GetValue(level - 1));
            }
        }

        [Test]
        public void DrillSpeed_StartsAtThreeSeconds_AndGetsFaster()
        {
            UpgradeTable table = DefaultUpgradeData.CreateDrillSpeedTable();
            Assert.AreEqual(3f, table.GetValue(1), 1e-4f);
            Assert.AreEqual(2.6f, table.GetValue(2), 1e-4f);
            for (int level = 2; level <= table.MaxLevel; level++)
            {
                Assert.Less(table.GetValue(level), table.GetValue(level - 1), $"Seviye {level} daha hızlı olmalı");
                Assert.Greater(table.GetValue(level), 0.05f);
            }
        }
    }
}
