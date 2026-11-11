using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Upgrades
{
    /// <summary>
    /// Bir geliştirmenin Inspector'da düzenlenen verisi. Yeni seviye eklemek = levels listesine satır eklemek.
    /// Çalışma zamanında CreateTable() ile saf C# UpgradeTable'a çevrilir.
    /// </summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Upgrade Definition", fileName = "Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public UpgradeType type;

        [Tooltip("Arayüzde görünen ad.")]
        public string displayName = "Upgrade";

        [Tooltip("Değerin adı (ör. Hasar, Yarıçap).")]
        public string valueLabel = "Değer";

        [TextArea] public string description;

        [Tooltip("Seviye 1'den başlayarak: değer ve o seviyeye çıkma fiyatı. İlk satırın fiyatı yok sayılır (0).")]
        public List<UpgradeLevel> levels = new List<UpgradeLevel> { new UpgradeLevel(1, 0) };

        [Header("Tablo sonrası (otomatik seviyeler)")]
        public bool extendBeyondTable;

        [Tooltip("Her yeni seviyede değer çarpanı.")]
        public float valueGrowth = 1.6f;

        [Tooltip("Her yeni seviyede fiyat çarpanı (>= 1).")]
        [Min(1f)] public float costGrowth = 3.8f;

        [Tooltip("0 = tablo uzunluğu (veya otomatik seviyelerde 1000).")]
        [Min(0)] public int maxLevel;

        [Tooltip("Değerler tam sayı mı (hasar, yarıçap)? Faz 7'deki vuruş aralığı gibi değerler için kapat.")]
        public bool integerValues = true;

        public UpgradeTable CreateTable()
        {
            return new UpgradeTable(levels, maxLevel, extendBeyondTable, valueGrowth, costGrowth, integerValues);
        }

        /// <summary>Tasarım belgesindeki varsayılanlarla doldurur (editör kurulum aracı kullanır).</summary>
        public void ApplyDefaults(UpgradeType upgradeType)
        {
            type = upgradeType;
            integerValues = true;

            switch (upgradeType)
            {
                case UpgradeType.PickaxeDamage:
                    displayName = "Pickaxe Damage";
                    valueLabel = "Hasar";
                    description = "Her tıklamada etkilenen voxellere verilen hasar.";
                    levels = new List<UpgradeLevel>(DefaultUpgradeData.PickaxeDamageLevels);
                    extendBeyondTable = true;
                    valueGrowth = DefaultUpgradeData.PickaxeValueGrowth;
                    costGrowth = DefaultUpgradeData.PickaxeCostGrowth;
                    maxLevel = DefaultUpgradeData.PickaxeMaxLevel;
                    break;

                case UpgradeType.MiningRadius:
                    displayName = "Mining Radius";
                    valueLabel = "Yarıçap";
                    description = "Tek tıklamada etkilenen 3D kürenin voxel yarıçapı (0 → 1, 1 → 7, 2 → 33, 3 → 123 voxel).";
                    levels = new List<UpgradeLevel>(DefaultUpgradeData.MiningRadiusLevels);
                    extendBeyondTable = false;
                    valueGrowth = 1f;
                    costGrowth = 1f;
                    maxLevel = 0;
                    break;

                case UpgradeType.DrillDamage:
                    displayName = "Drill Damage";
                    valueLabel = "Matkap hasarı";
                    description = "Bütün matkapların vuruş başına hasarı.";
                    levels = new List<UpgradeLevel>(DefaultUpgradeData.DrillDamageLevels);
                    extendBeyondTable = true;
                    valueGrowth = DefaultUpgradeData.DrillDamageValueGrowth;
                    costGrowth = DefaultUpgradeData.DrillDamageCostGrowth;
                    maxLevel = DefaultUpgradeData.DrillDamageMaxLevel;
                    break;

                case UpgradeType.DrillSpeed:
                    displayName = "Drill Speed";
                    valueLabel = "Vuruş aralığı (sn)";
                    description = "Bütün matkapların iki vuruş arasındaki süre (küçük = hızlı).";
                    levels = new List<UpgradeLevel>(DefaultUpgradeData.DrillSpeedLevels);
                    extendBeyondTable = true;
                    valueGrowth = DefaultUpgradeData.DrillSpeedValueGrowth;
                    costGrowth = DefaultUpgradeData.DrillSpeedCostGrowth;
                    maxLevel = DefaultUpgradeData.DrillSpeedMaxLevel;
                    integerValues = false;
                    break;
            }
        }
    }
}
