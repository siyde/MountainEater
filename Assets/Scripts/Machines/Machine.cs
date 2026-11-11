using UnityEngine;

namespace MountainEater.Machines
{
    /// <summary>Makine türünün çalışma zamanı verisi (ScriptableObject'ten bağımsız, testlerde doğrudan oluşturulur).</summary>
    public readonly struct MachineSpec
    {
        public readonly MachineType Type;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly long BasePrice;
        public readonly float PriceGrowth;
        public readonly int HitRadius;
        public readonly int WorkRadius;
        public readonly float DamageMultiplier;
        public readonly float IntervalMultiplier;

        public MachineSpec(MachineType type, string displayName, string description, long basePrice, float priceGrowth,
            int hitRadius, int workRadius, float damageMultiplier = 1f, float intervalMultiplier = 1f)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
            BasePrice = basePrice;
            PriceGrowth = Mathf.Max(1f, priceGrowth);
            HitRadius = Mathf.Max(0, hitRadius);
            WorkRadius = Mathf.Max(1, workRadius);
            DamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
            IntervalMultiplier = Mathf.Max(0.01f, intervalMultiplier);
        }

        public static MachineSpec MiniDrillDefault =>
            new MachineSpec(MachineType.MiniDrill, "Mini Drill", "", 250, 1.15f, 0, 12);
    }

    /// <summary>
    /// Yerleştirilmiş bir makinenin durumu. Saf veri: davranışı MachineManager yönetir (merkezi sistem),
    /// görüntüsü MachineController'daki MachineView'dır. Faz 9'da doğrudan kaydedilir.
    /// </summary>
    public sealed class Machine
    {
        public int Id { get; internal set; }
        public MachineType Type { get; internal set; }

        /// <summary>Yerleştirildiği voxel; çalışma alanının merkezi.</summary>
        public Vector3Int Anchor { get; internal set; }

        /// <summary>Yerleştirildiği yüzeyin normali (görünümün "yukarı" yönü).</summary>
        public Vector3 Normal { get; internal set; }

        /// <summary>Atandığı bölge. Makine yalnızca bu bölgede kazar.</summary>
        public byte Region { get; internal set; }

        public MachineState State { get; internal set; }

        public bool HasTarget { get; internal set; }
        public Vector3Int Target { get; internal set; }

        /// <summary>Bir sonraki vuruşa kadar biriken süre.</summary>
        public float Timer { get; internal set; }

        public int BlocksMined { get; internal set; }
        public long MoneyEarned { get; internal set; }
    }
}
