using System;
using System.Collections.Generic;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>
    /// Voxel hasarı ve kırılmanın TEK kaynağı. Oyuncu (Faz 3) ve makineler (Faz 7) aynı ApplyHit'i çağırır,
    /// böylece kurallar tek yerde ve aynıdır. Saf C#: sahne olmadan test edilebilir.
    ///
    /// Kurallar:
    /// - Boş voxel hasar almaz. Kilitli bölgedeki voxel hasar almaz (küre kilitli bölgeye taşmaz).
    /// - Etki alanındaki her uygun voxel, vuruş hasarı kadar hasar alır.
    /// - Toplam hasar malzeme HP'sine ulaşınca voxel kırılır; ödül yalnızca o anda ve yalnızca bir kez verilir
    ///   (VoxelWorld.RemoveVoxel zaten boş voxel için false döner → çift ödül imkânsız).
    /// </summary>
    public sealed class MiningSystem
    {
        private readonly VoxelMaterialTable materials;

        public VoxelWorld World { get; }

        /// <summary>
        /// Bölge kilidi kontrolü (Faz 6'da RegionManager bağlanacak). null → tüm bölgeler açık.
        /// Parametre: voxel'in bölge kimliği.
        /// </summary>
        public Func<byte, bool> IsRegionUnlocked { get; set; }

        /// <summary>Hasar veren her vuruştan sonra tetiklenir (ekonomi, istatistik, efektler dinler).</summary>
        public event Action<MiningResult> Mined;

        public MiningSystem(VoxelWorld world, VoxelMaterialTable materialTable)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            materials = materialTable ?? throw new ArgumentNullException(nameof(materialTable));
        }

        /// <summary>Voxel şu an kazılabilir mi? (dolu ve bölgesi açık)</summary>
        public bool CanMine(int x, int y, int z)
        {
            if (!World.IsSolid(x, y, z)) return false;
            Func<byte, bool> unlocked = IsRegionUnlocked;
            return unlocked == null || unlocked(World.GetRegion(x, y, z));
        }

        public bool CanMine(Vector3Int voxel) => CanMine(voxel.x, voxel.y, voxel.z);

        /// <summary>Kalan HP (boş voxel için 0).</summary>
        public int GetRemainingHealth(Vector3Int voxel)
        {
            if (!World.IsSolid(voxel.x, voxel.y, voxel.z)) return 0;
            int maxHealth = materials.GetMaxHealth(World.GetMaterial(voxel.x, voxel.y, voxel.z));
            return Mathf.Max(0, maxHealth - World.GetDamage(voxel.x, voxel.y, voxel.z));
        }

        /// <summary>
        /// Tek vuruş: target merkezli, radius yarıçaplı küredeki her kazılabilir voxele damage kadar hasar verir.
        /// Hedefin kendisi kazılabilir değilse hiçbir şey olmaz.
        /// regionFilter &gt;= 0 ise yalnızca o bölgedeki voxeller etkilenir (makineler kendi bölgesinde kalır).
        /// </summary>
        public MiningResult ApplyHit(Vector3Int target, int radius, int damage, MiningSource source, int regionFilter = -1)
        {
            if (damage <= 0 || !CanMine(target) ||
                (regionFilter >= 0 && World.GetRegion(target.x, target.y, target.z) != regionFilter))
            {
                return MiningResult.Nothing(source, target);
            }

            int damaged = 0;
            int broken = 0;
            long reward = 0;
            byte topMaterial = 0;

            IReadOnlyList<Vector3Int> offsets = SphereOffsets.Get(radius);
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector3Int v = target + offsets[i];
                if (!CanMine(v.x, v.y, v.z)) continue;
                if (regionFilter >= 0 && World.GetRegion(v.x, v.y, v.z) != regionFilter) continue;

                VoxelMaterialId material = World.GetMaterial(v.x, v.y, v.z);
                int totalDamage = World.GetDamage(v.x, v.y, v.z) + damage;
                damaged++;
                if ((byte)material > topMaterial) topMaterial = (byte)material;

                if (totalDamage >= materials.GetMaxHealth(material))
                {
                    if (World.RemoveVoxel(v.x, v.y, v.z))
                    {
                        broken++;
                        reward += materials.GetReward(material);
                    }
                }
                else
                {
                    World.SetDamage(v.x, v.y, v.z, totalDamage);
                }
            }

            var result = new MiningResult(source, target, damaged, broken, reward, (VoxelMaterialId)topMaterial);
            if (damaged > 0) Mined?.Invoke(result);
            return result;
        }
    }
}
