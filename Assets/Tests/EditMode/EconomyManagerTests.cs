using System.Collections.Generic;
using MountainEater.Economy;
using MountainEater.Mining;
using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class EconomyManagerTests
    {
        private static readonly VoxelMaterialTable Table = VoxelMaterialTable.CreateDefault();

        private static VoxelWorld StoneWorld(int count)
        {
            var world = new VoxelWorld(1, 1, 1);
            for (int i = 0; i < count; i++) world.SetGeneratedVoxel(i, 0, 0, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void StartsWithStartingMoney()
        {
            Assert.AreEqual(0, new EconomyManager().Money);
            Assert.AreEqual(250, new EconomyManager(250).Money);
            Assert.AreEqual(0, new EconomyManager(-5).Money);
        }

        [Test]
        public void TrySpend_FailsWithoutEnoughMoney_AndLeavesBalance()
        {
            var economy = new EconomyManager(10);
            Assert.IsFalse(economy.TrySpend(11));
            Assert.AreEqual(10, economy.Money);

            Assert.IsTrue(economy.TrySpend(10));
            Assert.AreEqual(0, economy.Money);
            Assert.IsFalse(economy.TrySpend(-1));
        }

        [Test]
        public void Add_IncreasesMoneyAndTotalEarned_SpendDoesNotReduceTotal()
        {
            var economy = new EconomyManager();
            economy.Add(100);
            economy.TrySpend(40);
            Assert.AreEqual(60, economy.Money);
            Assert.AreEqual(100, economy.TotalEarned);
        }

        [Test]
        public void MoneyChanged_FiresWithNewBalance()
        {
            var economy = new EconomyManager();
            var values = new List<long>();
            economy.MoneyChanged += values.Add;

            economy.Add(20);
            economy.TrySpend(5);
            economy.TrySpend(1000); // başarısız: olay yok
            economy.Add(0);         // anlamsız: olay yok

            CollectionAssert.AreEqual(new long[] { 20, 15 }, values);
        }

        [Test]
        public void BreakingVoxels_PaysExactlyTheirReward()
        {
            VoxelWorld world = StoneWorld(3);
            var mining = new MiningSystem(world, Table);
            var economy = new EconomyManager();
            economy.Attach(mining);

            mining.ApplyHit(new Vector3Int(0, 0, 0), 0, 2, MiningSource.Player); // hasar, ödül yok
            Assert.AreEqual(0, economy.Money);

            mining.ApplyHit(new Vector3Int(0, 0, 0), 0, 2, MiningSource.Player); // kırıldı: 5 $
            mining.ApplyHit(new Vector3Int(1, 0, 0), 0, 3, MiningSource.Machine); // kırıldı: 5 $
            mining.ApplyHit(new Vector3Int(1, 0, 0), 0, 3, MiningSource.Player);  // boş: 0 $

            Assert.AreEqual(10, economy.Money);
        }

        [Test]
        public void RewardMultiplier_IsApplied()
        {
            VoxelWorld world = StoneWorld(1);
            var mining = new MiningSystem(world, Table);
            var economy = new EconomyManager { RewardMultiplier = 2.5 };
            economy.Attach(mining);

            mining.ApplyHit(Vector3Int.zero, 0, 10, MiningSource.Player);
            Assert.AreEqual(13, economy.Money); // 5 × 2.5 = 12.5 → 13
        }

        [Test]
        public void Detach_StopsPayments()
        {
            VoxelWorld world = StoneWorld(1);
            var mining = new MiningSystem(world, Table);
            var economy = new EconomyManager();
            economy.Attach(mining);
            economy.Detach(mining);

            mining.ApplyHit(Vector3Int.zero, 0, 10, MiningSource.Player);
            Assert.AreEqual(0, economy.Money);
        }

        [Test]
        public void TotalRewardValue_SumsAllSolidVoxels()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(0, 0, 0, VoxelMaterialId.Stone, 255);
            world.SetGeneratedVoxel(1, 0, 0, VoxelMaterialId.Gold, 255);
            world.SetGeneratedVoxel(2, 0, 0, VoxelMaterialId.Crystal, 255);
            world.FinalizeGeneration();

            Assert.AreEqual(5 + 45 + 120, EconomyMath.TotalRewardValue(world, Table));
        }
    }
}
