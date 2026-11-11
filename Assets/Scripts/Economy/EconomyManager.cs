using System;
using MountainEater.Mining;

namespace MountainEater.Economy
{
    /// <summary>
    /// Oyunun tek para birimini yönetir. Saf C#: test edilebilir, Faz 9'da kaydedilebilir,
    /// Faz 10'da çevrimdışı simülasyonda sahne olmadan kullanılabilir.
    ///
    /// Para kaynağı: MiningSystem.Mined olayı (oyuncu + makineler, aynı yol).
    /// Ödül zaten yalnızca voxel kırıldığında ve bir kez hesaplanır; burada sadece cüzdana eklenir.
    /// </summary>
    public sealed class EconomyManager
    {
        private double rewardMultiplier = 1.0;

        public long Money { get; private set; }

        /// <summary>Oyun boyunca kazanılan toplam para (harcamalar düşülmez). İstatistik / dengeleme için.</summary>
        public long TotalEarned { get; private set; }

        /// <summary>
        /// Maden ödülü çarpanı. İleride "maden değeri" geliştirmesi bunu artıracak.
        /// </summary>
        public double RewardMultiplier
        {
            get => rewardMultiplier;
            set => rewardMultiplier = Math.Max(0.0, value);
        }

        /// <summary>Para değiştiğinde yeni bakiye ile tetiklenir.</summary>
        public event Action<long> MoneyChanged;

        public EconomyManager(long startingMoney = 0)
        {
            Money = Math.Max(0, startingMoney);
        }

        public void Attach(MiningSystem mining)
        {
            if (mining == null) throw new ArgumentNullException(nameof(mining));
            mining.Mined += OnMined;
        }

        public void Detach(MiningSystem mining)
        {
            if (mining != null) mining.Mined -= OnMined;
        }

        public bool CanAfford(long cost) => cost >= 0 && Money >= cost;

        public void Add(long amount)
        {
            if (amount <= 0) return;
            Money = SaturatingAdd(Money, amount);
            TotalEarned = SaturatingAdd(TotalEarned, amount);
            MoneyChanged?.Invoke(Money);
        }

        /// <summary>Yeterli para varsa düşer ve true döner; yoksa hiçbir şey değişmez.</summary>
        public bool TrySpend(long cost)
        {
            if (!CanAfford(cost)) return false;
            if (cost == 0) return true;

            Money -= cost;
            MoneyChanged?.Invoke(Money);
            return true;
        }

        /// <summary>Kayıttan yükleme için (Faz 9).</summary>
        public void Restore(long money, long totalEarned)
        {
            Money = Math.Max(0, money);
            TotalEarned = Math.Max(0, totalEarned);
            MoneyChanged?.Invoke(Money);
        }

        private void OnMined(MiningResult result)
        {
            if (result.Reward <= 0) return;
            Add((long)Math.Round(result.Reward * rewardMultiplier, MidpointRounding.AwayFromZero));
        }

        private static long SaturatingAdd(long a, long b)
        {
            long sum = a + b;
            return sum < a ? long.MaxValue : sum;
        }
    }
}
