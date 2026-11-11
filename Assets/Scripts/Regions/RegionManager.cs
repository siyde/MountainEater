using System;
using System.Collections.Generic;
using MountainEater.Economy;
using UnityEngine;

namespace MountainEater.Regions
{
    /// <summary>Bir bölgenin çalışma zamanı durumu.</summary>
    public sealed class RegionState
    {
        public byte Id;
        public string DisplayName;
        public long Price;
        public bool Unlocked;

        /// <summary>Üretimdeki voxel sayısı ve toplam maden değeri.</summary>
        public int VoxelCount;
        public long TotalValue;

        /// <summary>Voxel uzayında ortalama konum.</summary>
        public Vector3 Centroid;

        public float CenterAngle;
    }

    /// <summary>
    /// Bölge kilitlerini ve satın almayı yönetir. Saf C# (testli).
    /// MiningSystem.IsRegionUnlocked = IsUnlocked olarak bağlanır: kilitli bölgede oyuncu da makine de kazamaz.
    /// Kimlik 0 (hiçbir bölgeye ait olmayan voxel) her zaman kilitlidir.
    /// </summary>
    public sealed class RegionManager
    {
        private readonly EconomyManager economy;
        private readonly List<RegionState> regions = new List<RegionState>();
        private readonly bool[] unlockedById = new bool[RegionStats.Capacity];

        /// <summary>Bir bölge açıldığında (satın alma veya kayıttan yükleme).</summary>
        public event Action<RegionState> RegionUnlocked;

        public RegionManager(EconomyManager economy, IReadOnlyList<RegionDefinition> definitions, RegionStats stats)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            if (definitions == null) return;

            foreach (RegionDefinition definition in definitions)
            {
                if (definition == null || definition.id <= 0 || definition.id >= RegionStats.Capacity) continue;
                if (Find((byte)definition.id) != null) continue; // yinelenen kimlik

                var state = new RegionState
                {
                    Id = (byte)definition.id,
                    DisplayName = definition.displayName,
                    Price = Math.Max(0, definition.price),
                    Unlocked = definition.unlockedAtStart,
                    VoxelCount = stats != null ? stats.VoxelCount[definition.id] : 0,
                    TotalValue = stats != null ? stats.TotalValue[definition.id] : 0,
                    Centroid = stats != null ? stats.Centroid[definition.id] : Vector3.zero,
                    CenterAngle = definition.CenterAngle,
                };

                regions.Add(state);
                unlockedById[state.Id] = state.Unlocked;
            }
        }

        public IReadOnlyList<RegionState> Regions => regions;

        /// <summary>MiningSystem kancası. Çok sık çağrılır: dizi araması, tahsis yok.</summary>
        public bool IsUnlocked(byte regionId) => regionId < unlockedById.Length && unlockedById[regionId];

        public RegionState Find(byte regionId)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Id == regionId) return regions[i];
            }

            return null;
        }

        public bool CanPurchase(byte regionId)
        {
            RegionState region = Find(regionId);
            return region != null && !region.Unlocked && economy.CanAfford(region.Price);
        }

        public bool TryPurchase(byte regionId)
        {
            RegionState region = Find(regionId);
            if (region == null || region.Unlocked) return false;
            if (!economy.TrySpend(region.Price)) return false;

            Unlock(region);
            return true;
        }

        /// <summary>Kayıttan yükleme için (Faz 9). Para düşmez.</summary>
        public void RestoreUnlocked(byte regionId)
        {
            RegionState region = Find(regionId);
            if (region != null && !region.Unlocked) Unlock(region);
        }

        /// <summary>En ucuz kilitli bölgenin fiyatı; yoksa -1. (Dengeleme kontrolü için.)</summary>
        public long CheapestLockedPrice()
        {
            long cheapest = -1;
            foreach (RegionState region in regions)
            {
                if (region.Unlocked) continue;
                if (cheapest < 0 || region.Price < cheapest) cheapest = region.Price;
            }

            return cheapest;
        }

        /// <summary>Başlangıçta açık bölgelerin toplam maden değeri.</summary>
        public long UnlockedValue()
        {
            long total = 0;
            foreach (RegionState region in regions)
            {
                if (region.Unlocked) total += region.TotalValue;
            }

            return total;
        }

        private void Unlock(RegionState region)
        {
            region.Unlocked = true;
            unlockedById[region.Id] = true;
            RegionUnlocked?.Invoke(region);
        }
    }
}
