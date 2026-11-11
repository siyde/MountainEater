namespace MountainEater.Voxel
{
    /// <summary>Voxel dünyası üzerinde toplu analizler (üretim ve dengeleme için). Saf C#, testli.</summary>
    public static class VoxelAnalysis
    {
        /// <summary>Hiç havaya ulaşamayan (tamamen kapalı) dolu voxellerin derinlik değeri.</summary>
        public const byte Unreachable = 255;

        /// <summary>
        /// Her dolu voxelin en yakın havaya (yüzeye) voxel adımı cinsinden uzaklığı; boş voxeller 0.
        /// Yüzey voxeli = 1. Dünyanın altı (y &lt; 0) zemin sayılır, hava değildir; yanlar ve üst hava sayılır.
        /// Çok kaynaklı BFS (6 komşuluk): O(voxel sayısı). En fazla 254, hiç ulaşılamayan = 255.
        /// </summary>
        public static byte[] ComputeSurfaceDistance(VoxelWorld world)
        {
            int sizeX = world.SizeX;
            int sizeY = world.SizeY;
            int sizeZ = world.SizeZ;
            int count = sizeX * sizeY * sizeZ;
            int strideY = sizeX;
            int strideZ = sizeX * sizeY;

            var distance = new byte[count];
            var queue = new int[count];
            int head = 0;
            int tail = 0;

            // 1) Havaya komşu dolu voxeller = 1, diğer dolular "ziyaret edilmedi" (255).
            for (int z = 0; z < sizeZ; z++)
            for (int y = 0; y < sizeY; y++)
            for (int x = 0; x < sizeX; x++)
            {
                int i = x + strideY * y + strideZ * z;
                if (world.GetMaterialAt(i) == 0) continue;

                bool touchesAir =
                    IsAir(world, x - 1, y, z) || IsAir(world, x + 1, y, z) ||
                    IsAir(world, x, y + 1, z) || IsAir(world, x, y, z - 1) || IsAir(world, x, y, z + 1) ||
                    (y > 0 && world.GetMaterialAt(i - strideY) == 0);

                if (touchesAir)
                {
                    distance[i] = 1;
                    queue[tail++] = i;
                }
                else
                {
                    distance[i] = Unreachable;
                }
            }

            // 2) İçeri doğru yay.
            while (head < tail)
            {
                int i = queue[head++];
                byte next = (byte)System.Math.Min(254, distance[i] + 1);

                int x = i % sizeX;
                int y = (i / strideY) % sizeY;
                int z = i / strideZ;

                if (x > 0) Visit(i - 1);
                if (x < sizeX - 1) Visit(i + 1);
                if (y > 0) Visit(i - strideY);
                if (y < sizeY - 1) Visit(i + strideY);
                if (z > 0) Visit(i - strideZ);
                if (z < sizeZ - 1) Visit(i + strideZ);

                void Visit(int n)
                {
                    if (distance[n] != Unreachable) return; // hava (0) veya zaten atanmış
                    distance[n] = next;
                    queue[tail++] = n;
                }
            }

            return distance;
        }

        /// <summary>Malzeme kimliğine göre dolu voxel sayıları (256 elemanlı).</summary>
        public static int[] CountMaterials(VoxelWorld world)
        {
            var counts = new int[256];
            int total = world.SizeX * world.SizeY * world.SizeZ;
            for (int i = 0; i < total; i++)
            {
                byte material = world.GetMaterialAt(i);
                if (material != 0) counts[material]++;
            }

            return counts;
        }

        /// <summary>Dünya dışı (y &lt; 0 hariç) veya boş voxel → hava.</summary>
        private static bool IsAir(VoxelWorld world, int x, int y, int z)
        {
            if (y < 0) return false; // zemin
            return !world.IsSolid(x, y, z);
        }
    }
}
