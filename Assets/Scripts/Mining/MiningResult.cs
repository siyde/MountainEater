using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>Kazıyı kimin yaptığı (istatistik ve ileride makine geliri ayrımı için).</summary>
    public enum MiningSource : byte
    {
        Player = 0,
        Machine = 1,
    }

    /// <summary>Tek bir vuruşun (oyuncu tıklaması veya makine vuruşu) sonucu.</summary>
    public readonly struct MiningResult
    {
        public readonly MiningSource Source;
        public readonly Vector3Int Target;

        /// <summary>Hasar alan voxel sayısı (kırılanlar dahil).</summary>
        public readonly int VoxelsDamaged;

        /// <summary>Bu vuruşla tamamen kırılan voxel sayısı. Kalan blok sayacı bu kadar azalır.</summary>
        public readonly int VoxelsBroken;

        /// <summary>Kırılan voxellerin toplam ödülü. Yalnızca kırılan voxeller ödül verir.</summary>
        public readonly long Reward;

        /// <summary>Etkilenen en değerli malzeme (efekt rengi için).</summary>
        public readonly VoxelMaterialId TopMaterial;

        public MiningResult(MiningSource source, Vector3Int target, int voxelsDamaged, int voxelsBroken, long reward,
            VoxelMaterialId topMaterial)
        {
            Source = source;
            Target = target;
            VoxelsDamaged = voxelsDamaged;
            VoxelsBroken = voxelsBroken;
            Reward = reward;
            TopMaterial = topMaterial;
        }

        public bool HitAnything => VoxelsDamaged > 0;

        public static MiningResult Nothing(MiningSource source, Vector3Int target)
        {
            return new MiningResult(source, target, 0, 0, 0, VoxelMaterialId.Air);
        }
    }
}
