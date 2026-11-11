using UnityEngine;

namespace MountainEater.Machines
{
    /// <summary>
    /// Bir makine türünün verisi. Hasar ve vuruş aralığı tüm matkaplar için ortak yükseltmelerden
    /// (Drill Damage / Drill Speed) gelir; tür bunları çarpanlarla farklılaştırır.
    /// Yeni makine türü = yeni MachineType + yeni asset (+ gerekirse farklı görünüm).
    /// </summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Machine Definition", fileName = "Machine")]
    public sealed class MachineDefinition : ScriptableObject
    {
        public MachineType type;
        public string displayName = "Mini Drill";
        [TextArea] public string description = "Yerleştirildiği bölgede, yakınındaki açıkta kalan kayaları otomatik kazar.";

        [Header("Fiyat")]
        [Min(0)] public long basePrice = 250;

        [Tooltip("Aynı türden her yeni makinenin fiyat çarpanı (1 = hep aynı fiyat).")]
        [Min(1f)] public float priceGrowth = 1.15f;

        [Header("Kazı")]
        [Tooltip("Vuruş başına etkilenen kürenin voxel yarıçapı (0 = tek voxel).")]
        [Range(0, 8)] public int hitRadius;

        [Tooltip("Makinenin yerleştirildiği noktadan en fazla kaç voxel uzağa kadar kazar. Bu alan biterse durur.")]
        [Range(1, 16)] public int workRadius = 12;

        [Tooltip("Ortak matkap hasarı × bu çarpan.")]
        [Min(0.01f)] public float damageMultiplier = 1f;

        [Tooltip("Ortak vuruş aralığı × bu çarpan (küçük = hızlı).")]
        [Min(0.01f)] public float intervalMultiplier = 1f;

        public MachineSpec ToSpec()
        {
            return new MachineSpec(type, displayName, description, basePrice, priceGrowth, hitRadius, workRadius,
                damageMultiplier, intervalMultiplier);
        }

        /// <summary>Tasarım belgesindeki Mini Drill değerleri.</summary>
        public void ApplyMiniDrillDefaults()
        {
            type = MachineType.MiniDrill;
            displayName = "Mini Drill";
            description = "Yerleştirildiği bölgede, yakınındaki açıkta kalan kayaları otomatik kazar.";
            basePrice = 250;
            priceGrowth = 1.15f;
            hitRadius = 0;
            workRadius = 12;
            damageMultiplier = 1f;
            intervalMultiplier = 1f;
        }
    }
}
