using MountainEater.Voxel;

namespace MountainEater.Economy
{
    /// <summary>Dengeleme için yardımcı hesaplar (görselden bağımsız, testli).</summary>
    public static class EconomyMath
    {
        /// <summary>Dünyada kalan bütün dolu voxeller kırılırsa kazanılacak toplam ödül (çarpansız).</summary>
        public static long TotalRewardValue(VoxelWorld world, VoxelMaterialTable materials)
        {
            long total = 0;
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
            {
                VoxelMaterialId material = world.GetMaterial(x, y, z);
                if (material != VoxelMaterialId.Air) total += materials.GetReward(material);
            }

            return total;
        }
    }
}
