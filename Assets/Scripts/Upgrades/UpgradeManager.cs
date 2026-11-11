using System;
using System.Collections.Generic;
using MountainEater.Economy;

namespace MountainEater.Upgrades
{
    /// <summary>Arayüzde gösterilecek geliştirme bilgisi.</summary>
    public readonly struct UpgradeInfo
    {
        public readonly string DisplayName;
        public readonly string ValueLabel;
        public readonly string Description;

        public UpgradeInfo(string displayName, string valueLabel, string description)
        {
            DisplayName = displayName;
            ValueLabel = valueLabel;
            Description = description;
        }
    }

    /// <summary>
    /// Geliştirme seviyelerini tutar ve satın almayı yönetir. Saf C# (testli).
    /// Geliştirmenin oyuna etkisini (ör. kazma hasarı) BİLMEZ: LevelChanged olayını yayınlar,
    /// GameBootstrap bunu ilgili sisteme uygular. Böylece yeni geliştirme eklemek bu sınıfı değiştirmez.
    /// </summary>
    public sealed class UpgradeManager
    {
        private sealed class Entry
        {
            public UpgradeTable Table;
            public UpgradeInfo Info;
            public int Level = 1;
        }

        private readonly EconomyManager economy;
        private readonly Dictionary<UpgradeType, Entry> entries = new Dictionary<UpgradeType, Entry>();
        private readonly List<UpgradeType> order = new List<UpgradeType>();

        /// <summary>Seviye değiştiğinde (satın alma veya kayıttan yükleme).</summary>
        public event Action<UpgradeType, int> LevelChanged;

        public UpgradeManager(EconomyManager economy)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        /// <summary>Kayıt sırası = arayüzdeki sıra.</summary>
        public IReadOnlyList<UpgradeType> Types => order;

        public void Register(UpgradeType type, UpgradeTable table, UpgradeInfo info)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (entries.ContainsKey(type)) throw new InvalidOperationException($"{type} zaten kayıtlı.");

            entries[type] = new Entry { Table = table, Info = info };
            order.Add(type);
        }

        public bool IsRegistered(UpgradeType type) => entries.ContainsKey(type);

        public UpgradeInfo GetInfo(UpgradeType type) => Get(type).Info;
        public int GetLevel(UpgradeType type) => Get(type).Level;
        public float GetCurrentValue(UpgradeType type) => Get(type).Table.GetValue(Get(type).Level);

        public bool IsMaxed(UpgradeType type)
        {
            Entry entry = Get(type);
            return entry.Table.IsMaxLevel(entry.Level);
        }

        /// <summary>Bir sonraki seviyenin değeri (zaten maksimumdaysa mevcut değer).</summary>
        public float GetNextValue(UpgradeType type)
        {
            Entry entry = Get(type);
            return entry.Table.GetValue(entry.Table.IsMaxLevel(entry.Level) ? entry.Level : entry.Level + 1);
        }

        /// <summary>Bir sonraki seviyenin fiyatı; maksimumdaysa long.MaxValue.</summary>
        public long GetNextCost(UpgradeType type)
        {
            Entry entry = Get(type);
            return entry.Table.IsMaxLevel(entry.Level) ? long.MaxValue : entry.Table.GetCost(entry.Level + 1);
        }

        public bool CanPurchase(UpgradeType type)
        {
            return !IsMaxed(type) && economy.CanAfford(GetNextCost(type));
        }

        /// <summary>Para yetiyorsa bir seviye satın alır.</summary>
        public bool TryPurchase(UpgradeType type)
        {
            Entry entry = Get(type);
            if (entry.Table.IsMaxLevel(entry.Level)) return false;

            long cost = entry.Table.GetCost(entry.Level + 1);
            if (!economy.TrySpend(cost)) return false;

            entry.Level++;
            LevelChanged?.Invoke(type, entry.Level);
            return true;
        }

        /// <summary>Kayıttan yükleme için (Faz 9). Para düşmez.</summary>
        public void RestoreLevel(UpgradeType type, int level)
        {
            Entry entry = Get(type);
            entry.Level = Math.Max(1, Math.Min(level, entry.Table.MaxLevel));
            LevelChanged?.Invoke(type, entry.Level);
        }

        private Entry Get(UpgradeType type)
        {
            if (!entries.TryGetValue(type, out Entry entry))
            {
                throw new KeyNotFoundException($"Geliştirme kayıtlı değil: {type}");
            }

            return entry;
        }
    }
}
