namespace MountainEater.Upgrades
{
    /// <summary>
    /// Tasarım belgesindeki başlangıç dengeleme değerleri. Editör kurulum aracı asset'leri bunlarla doldurur,
    /// testler de aynı değerleri kullanır. Oyun içinde değerler asset'ten okunur (Inspector'dan değiştirilebilir).
    /// </summary>
    public static class DefaultUpgradeData
    {
        // Kazma gücü: 1, 2, 3, 5, 8, 13 → sonrası ×1.6 değer, ×3.8 fiyat
        public static UpgradeLevel[] PickaxeDamageLevels => new[]
        {
            new UpgradeLevel(1, 0),
            new UpgradeLevel(2, 20),
            new UpgradeLevel(3, 65),
            new UpgradeLevel(5, 220),
            new UpgradeLevel(8, 850),
            new UpgradeLevel(13, 3_200),
        };

        public const float PickaxeValueGrowth = 1.6f;
        public const float PickaxeCostGrowth = 3.8f;
        public const int PickaxeMaxLevel = 25;

        // Kazı yarıçapı: 0, 1, 2, 3 (1 / 7 / 33 / 123 voxel)
        public static UpgradeLevel[] MiningRadiusLevels => new[]
        {
            new UpgradeLevel(0, 0),
            new UpgradeLevel(1, 600),
            new UpgradeLevel(2, 9_000),
            new UpgradeLevel(3, 150_000),
        };

        // Matkap hasarı (tüm matkaplar): 3 (Mini Drill başlangıcı) → sonrası ×1.5 değer, ×3 fiyat
        public static UpgradeLevel[] DrillDamageLevels => new[]
        {
            new UpgradeLevel(3, 0),
            new UpgradeLevel(5, 400),
            new UpgradeLevel(8, 1_500),
            new UpgradeLevel(12, 5_000),
            new UpgradeLevel(18, 15_000),
            new UpgradeLevel(27, 45_000),
        };

        public const float DrillDamageValueGrowth = 1.5f;
        public const float DrillDamageCostGrowth = 3f;
        public const int DrillDamageMaxLevel = 25;

        // Matkap vuruş aralığı (saniye, küçük = hızlı): 3 → sonrası ×0.88 değer, ×3 fiyat
        public static UpgradeLevel[] DrillSpeedLevels => new[]
        {
            new UpgradeLevel(3.0f, 0),
            new UpgradeLevel(2.6f, 500),
            new UpgradeLevel(2.2f, 1_800),
            new UpgradeLevel(1.9f, 6_000),
            new UpgradeLevel(1.6f, 18_000),
            new UpgradeLevel(1.35f, 50_000),
        };

        public const float DrillSpeedValueGrowth = 0.88f;
        public const float DrillSpeedCostGrowth = 3f;
        public const int DrillSpeedMaxLevel = 20;

        public static UpgradeTable CreateDrillDamageTable()
        {
            return new UpgradeTable(DrillDamageLevels, DrillDamageMaxLevel, extendBeyondTable: true,
                valueGrowth: DrillDamageValueGrowth, costGrowth: DrillDamageCostGrowth, integerValues: true);
        }

        public static UpgradeTable CreateDrillSpeedTable()
        {
            return new UpgradeTable(DrillSpeedLevels, DrillSpeedMaxLevel, extendBeyondTable: true,
                valueGrowth: DrillSpeedValueGrowth, costGrowth: DrillSpeedCostGrowth, integerValues: false);
        }

        public static UpgradeTable CreatePickaxeDamageTable()
        {
            return new UpgradeTable(PickaxeDamageLevels, PickaxeMaxLevel, extendBeyondTable: true,
                valueGrowth: PickaxeValueGrowth, costGrowth: PickaxeCostGrowth, integerValues: true);
        }

        public static UpgradeTable CreateMiningRadiusTable()
        {
            return new UpgradeTable(MiningRadiusLevels);
        }
    }
}
