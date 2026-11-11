using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Upgrades
{
    /// <summary>Bir seviyenin değeri ve o seviyeye çıkmanın fiyatı. Seviye 1'in fiyatı 0'dır.</summary>
    [Serializable]
    public struct UpgradeLevel
    {
        [Tooltip("Bu seviyedeki değer (ör. hasar 3, yarıçap 2).")]
        public float value;

        [Tooltip("Önceki seviyeden BU seviyeye yükseltme fiyatı.")]
        [Min(0)] public long cost;

        public UpgradeLevel(float value, long cost)
        {
            this.value = value;
            this.cost = cost;
        }
    }

    /// <summary>
    /// Bir geliştirmenin seviye → değer / fiyat tablosu. Saf C# (testli).
    ///
    /// Tablodaki seviyeler aynen kullanılır. "Tablo sonrası" açıksa son seviyeden sonra
    /// değer valueGrowth, fiyat costGrowth ile çarpılarak üretilir (fiyatlar 2 anlamlı basamağa yuvarlanır).
    /// Böylece tasarım tablosu korunur ve yeni seviye eklemek için sadece tabloya satır eklemek yeter.
    /// </summary>
    public sealed class UpgradeTable
    {
        private readonly UpgradeLevel[] levels;
        private readonly bool extendBeyondTable;
        private readonly float valueGrowth;
        private readonly float costGrowth;
        private readonly bool integerValues;

        /// <summary>Ulaşılabilecek en yüksek seviye.</summary>
        public int MaxLevel { get; }

        public UpgradeTable(IReadOnlyList<UpgradeLevel> levels, int maxLevel = 0, bool extendBeyondTable = false,
            float valueGrowth = 1f, float costGrowth = 1f, bool integerValues = true)
        {
            if (levels == null || levels.Count == 0)
            {
                throw new ArgumentException("En az bir seviye gerekli.", nameof(levels));
            }

            this.levels = new UpgradeLevel[levels.Count];
            for (int i = 0; i < levels.Count; i++) this.levels[i] = levels[i];
            this.levels[0].cost = 0; // başlangıç seviyesi bedava

            this.extendBeyondTable = extendBeyondTable;
            this.valueGrowth = valueGrowth;
            this.costGrowth = Mathf.Max(1f, costGrowth);
            this.integerValues = integerValues;

            if (extendBeyondTable)
            {
                MaxLevel = maxLevel > 0 ? Mathf.Max(maxLevel, 1) : 1000;
            }
            else
            {
                MaxLevel = maxLevel > 0 ? Mathf.Min(maxLevel, this.levels.Length) : this.levels.Length;
            }
        }

        public int TableLevelCount => levels.Length;

        public bool IsMaxLevel(int level) => level >= MaxLevel;

        /// <summary>Seviyedeki değer (seviye 1'den başlar).</summary>
        public float GetValue(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            if (level <= levels.Length) return levels[level - 1].value;

            float value = levels[levels.Length - 1].value;
            for (int l = levels.Length + 1; l <= level; l++)
            {
                float next = value * valueGrowth;
                if (integerValues)
                {
                    // Tam sayı değerlerde her seviye en az +1 (veya -1) değişsin.
                    float rounded = Mathf.Round(next);
                    if (valueGrowth >= 1f && rounded <= value) rounded = value + 1f;
                    next = rounded;
                }

                value = next;
            }

            return value;
        }

        /// <summary>Önceki seviyeden bu seviyeye çıkma fiyatı. Seviye 1 → 0. Ulaşılamaz seviye → long.MaxValue.</summary>
        public long GetCost(int level)
        {
            if (level <= 1) return 0;
            if (level > MaxLevel) return long.MaxValue;
            if (level <= levels.Length) return levels[level - 1].cost;

            double cost = Math.Max(1, levels[levels.Length - 1].cost);
            for (int l = levels.Length + 1; l <= level; l++)
            {
                cost = RoundToSignificant(cost * costGrowth, 2);
                if (cost >= 9.0e18) return long.MaxValue; // long taşmasını önle
            }

            return (long)cost;
        }

        /// <summary>1234 → 1200, 12160 → 12000, 87 → 87 (iki anlamlı basamak; 100'ün altı tam sayı).</summary>
        public static double RoundToSignificant(double value, int digits)
        {
            if (value < 100) return Math.Round(value);
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)) - (digits - 1));
            return Math.Round(value / magnitude) * magnitude;
        }
    }
}
