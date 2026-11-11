using System.Collections.Generic;
using MountainEater.Economy;
using MountainEater.UI;
using MountainEater.Upgrades;
using NUnit.Framework;

namespace MountainEater.Tests
{
    public sealed class UpgradeTableTests
    {
        [Test]
        public void PickaxeTable_MatchesDesignDocument()
        {
            UpgradeTable table = DefaultUpgradeData.CreatePickaxeDamageTable();
            float[] values = { 1, 2, 3, 5, 8, 13 };
            long[] costs = { 0, 20, 65, 220, 850, 3_200 };

            for (int level = 1; level <= 6; level++)
            {
                Assert.AreEqual(values[level - 1], table.GetValue(level), $"Seviye {level} değeri");
                Assert.AreEqual(costs[level - 1], table.GetCost(level), $"Seviye {level} fiyatı");
            }
        }

        [Test]
        public void PickaxeTable_ExtendsBeyondTable_WithGrowingValuesAndCosts()
        {
            UpgradeTable table = DefaultUpgradeData.CreatePickaxeDamageTable();

            Assert.AreEqual(21f, table.GetValue(7));   // 13 × 1.6 = 20.8 → 21
            Assert.AreEqual(12_000, table.GetCost(7)); // 3200 × 3.8 = 12160 → 12000

            for (int level = 7; level <= table.MaxLevel; level++)
            {
                Assert.Greater(table.GetValue(level), table.GetValue(level - 1), $"Seviye {level} değeri artmalı");
                Assert.Greater(table.GetCost(level), table.GetCost(level - 1), $"Seviye {level} fiyatı artmalı");
            }
        }

        [Test]
        public void RadiusTable_MatchesDesignDocument_AndStopsAtLevel4()
        {
            UpgradeTable table = DefaultUpgradeData.CreateMiningRadiusTable();
            Assert.AreEqual(4, table.MaxLevel);
            Assert.AreEqual(0f, table.GetValue(1));
            Assert.AreEqual(3f, table.GetValue(4));
            Assert.AreEqual(600, table.GetCost(2));
            Assert.AreEqual(9_000, table.GetCost(3));
            Assert.AreEqual(150_000, table.GetCost(4));
            Assert.AreEqual(long.MaxValue, table.GetCost(5));
            Assert.IsTrue(table.IsMaxLevel(4));
        }

        [Test]
        public void FirstLevelIsAlwaysFree()
        {
            var table = new UpgradeTable(new List<UpgradeLevel> { new UpgradeLevel(1, 999) });
            Assert.AreEqual(0, table.GetCost(1));
        }

        [TestCase(87.0, 87.0)]
        [TestCase(1234.0, 1200.0)]
        [TestCase(12160.0, 12000.0)]
        [TestCase(45678.0, 46000.0)]
        public void RoundToSignificant_GivesFriendlyPrices(double input, double expected)
        {
            Assert.AreEqual(expected, UpgradeTable.RoundToSignificant(input, 2), 0.001);
        }
    }

    public sealed class UpgradeManagerTests
    {
        private static UpgradeManager Create(EconomyManager economy)
        {
            var manager = new UpgradeManager(economy);
            manager.Register(UpgradeType.PickaxeDamage, DefaultUpgradeData.CreatePickaxeDamageTable(),
                new UpgradeInfo("Pickaxe Damage", "Hasar", ""));
            manager.Register(UpgradeType.MiningRadius, DefaultUpgradeData.CreateMiningRadiusTable(),
                new UpgradeInfo("Mining Radius", "Yarıçap", ""));
            return manager;
        }

        [Test]
        public void StartsAtLevelOne()
        {
            UpgradeManager manager = Create(new EconomyManager());
            Assert.AreEqual(1, manager.GetLevel(UpgradeType.PickaxeDamage));
            Assert.AreEqual(1f, manager.GetCurrentValue(UpgradeType.PickaxeDamage));
            Assert.AreEqual(0f, manager.GetCurrentValue(UpgradeType.MiningRadius));
            Assert.AreEqual(20, manager.GetNextCost(UpgradeType.PickaxeDamage));
        }

        [Test]
        public void Purchase_WithoutMoney_Fails()
        {
            var economy = new EconomyManager(19);
            UpgradeManager manager = Create(economy);

            Assert.IsFalse(manager.CanPurchase(UpgradeType.PickaxeDamage));
            Assert.IsFalse(manager.TryPurchase(UpgradeType.PickaxeDamage));
            Assert.AreEqual(1, manager.GetLevel(UpgradeType.PickaxeDamage));
            Assert.AreEqual(19, economy.Money);
        }

        [Test]
        public void Purchase_DeductsCost_RaisesLevel_AndFiresEvent()
        {
            var economy = new EconomyManager(100);
            UpgradeManager manager = Create(economy);
            var changes = new List<(UpgradeType, int)>();
            manager.LevelChanged += (type, level) => changes.Add((type, level));

            Assert.IsTrue(manager.TryPurchase(UpgradeType.PickaxeDamage));

            Assert.AreEqual(80, economy.Money);
            Assert.AreEqual(2, manager.GetLevel(UpgradeType.PickaxeDamage));
            Assert.AreEqual(2f, manager.GetCurrentValue(UpgradeType.PickaxeDamage));
            Assert.AreEqual(65, manager.GetNextCost(UpgradeType.PickaxeDamage));
            CollectionAssert.AreEqual(new[] { (UpgradeType.PickaxeDamage, 2) }, changes);
        }

        [Test]
        public void BuyingWholePickaxeTable_CostsDesignTotal()
        {
            var economy = new EconomyManager(20 + 65 + 220 + 850 + 3_200);
            UpgradeManager manager = Create(economy);

            for (int i = 0; i < 5; i++) Assert.IsTrue(manager.TryPurchase(UpgradeType.PickaxeDamage));

            Assert.AreEqual(0, economy.Money);
            Assert.AreEqual(6, manager.GetLevel(UpgradeType.PickaxeDamage));
            Assert.AreEqual(13f, manager.GetCurrentValue(UpgradeType.PickaxeDamage));
        }

        [Test]
        public void MaxedUpgrade_CannotBePurchased()
        {
            var economy = new EconomyManager(1_000_000);
            UpgradeManager manager = Create(economy);

            for (int i = 0; i < 3; i++) Assert.IsTrue(manager.TryPurchase(UpgradeType.MiningRadius));
            long moneyAtMax = economy.Money;

            Assert.IsTrue(manager.IsMaxed(UpgradeType.MiningRadius));
            Assert.IsFalse(manager.CanPurchase(UpgradeType.MiningRadius));
            Assert.IsFalse(manager.TryPurchase(UpgradeType.MiningRadius));
            Assert.AreEqual(moneyAtMax, economy.Money);
            Assert.AreEqual(3f, manager.GetCurrentValue(UpgradeType.MiningRadius));
        }

        [Test]
        public void UpgradesAreIndependent()
        {
            var economy = new EconomyManager(1_000);
            UpgradeManager manager = Create(economy);

            manager.TryPurchase(UpgradeType.MiningRadius);
            Assert.AreEqual(1, manager.GetLevel(UpgradeType.PickaxeDamage));
            Assert.AreEqual(2, manager.GetLevel(UpgradeType.MiningRadius));
        }

        [Test]
        public void RestoreLevel_DoesNotCharge_AndClamps()
        {
            var economy = new EconomyManager();
            UpgradeManager manager = Create(economy);

            manager.RestoreLevel(UpgradeType.MiningRadius, 99);
            Assert.AreEqual(4, manager.GetLevel(UpgradeType.MiningRadius));
            Assert.AreEqual(0, economy.Money);
        }

        [Test]
        public void Types_KeepRegistrationOrder()
        {
            UpgradeManager manager = Create(new EconomyManager());
            CollectionAssert.AreEqual(new[] { UpgradeType.PickaxeDamage, UpgradeType.MiningRadius }, manager.Types);
        }
    }

    public sealed class NumberFormatTests
    {
        [Test]
        public void UsesTurkishSeparators()
        {
            Assert.AreEqual("847.261", NumberFormat.Integer(847_261));
            Assert.AreEqual("1.234 $", NumberFormat.Money(1_234));
            Assert.AreEqual("%15,27", NumberFormat.Percent(15.27));
        }

        [Test]
        public void AbbreviatesLargeMoney()
        {
            Assert.AreEqual("9.999.999 $", NumberFormat.Money(9_999_999));
            Assert.AreEqual("12,5 Mn $", NumberFormat.Money(12_500_000));
            Assert.AreEqual("3,4 Mr $", NumberFormat.Money(3_400_000_000));
        }

        [Test]
        public void Value_DropsDecimalsForIntegers()
        {
            Assert.AreEqual("13", NumberFormat.Value(13f));
            Assert.AreEqual("2,5", NumberFormat.Value(2.5f));
        }
    }
}
