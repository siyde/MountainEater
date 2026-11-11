using System;
using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>
    /// Dağın bütün voxel verisi. Saf C# (MonoBehaviour değil): görsel sistemden bağımsız test edilebilir.
    ///
    /// Saklama (voxel başına 5 byte, dünya boyunca düz diziler):
    ///   materials : byte   — malzeme kimliği, 0 = Air. "Dolu mu?" bilgisi buradan gelir.
    ///   densities : byte   — yüzeyin voxel merkezleri arasındaki konumu (yumuşak görünüm için).
    ///                        Kural: dolu ↔ density >= IsoLevel. Kırılan voxel'in density'si 0 olur.
    ///   regions   : byte   — kazı bölgesi kimliği (Faz 6).
    ///   damage    : ushort — alınan hasar; 0 = hasarsız. Kalan HP = malzeme HP'si - damage (Faz 3).
    ///
    /// Neden chunk başına ayrı dizi değil de dünya boyunca tek dizi?
    /// Chunk sınırındaki komşu okumaları basitleşir ve kayıt (Faz 9) tek bir bitset ile yapılabilir.
    /// ~1M voxellik dağın sınır kutusu ~7M hücre × 5 byte ≈ 35 MB: PC için kabul edilebilir.
    /// Chunk'lar bu veriye kendi aralıklarından (16³) erişir.
    /// </summary>
    public sealed class VoxelWorld
    {
        public const int ChunkSize = 16;
        public const byte IsoLevel = 128;

        private readonly byte[] materials;
        private readonly byte[] densities;
        private readonly byte[] regions;
        private readonly ushort[] damage;

        public int ChunksX { get; }
        public int ChunksY { get; }
        public int ChunksZ { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int ChunkCount => ChunksX * ChunksY * ChunksZ;

        /// <summary>Şu anda dünyada kalan dolu voxel sayısı.</summary>
        public int SolidCount { get; private set; }

        /// <summary>Üretim sonrası gerçek dolu voxel sayısı (ilerleme sayacının toplamı).</summary>
        public int InitialSolidCount { get; private set; }

        /// <summary>Üretimdeki dolu voxellerin kapsadığı en küçük / en büyük voxel koordinatı.</summary>
        public Vector3Int ContentMin { get; private set; }
        public Vector3Int ContentMax { get; private set; }

        /// <summary>Bir voxel değiştiğinde, görüntüsü etkilenen her chunk için çağrılır (chunk indeksi).</summary>
        public event Action<int> ChunkChanged;

        public VoxelWorld(int chunksX, int chunksY, int chunksZ)
        {
            if (chunksX <= 0 || chunksY <= 0 || chunksZ <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunksX), "Chunk sayıları pozitif olmalı.");
            }

            ChunksX = chunksX;
            ChunksY = chunksY;
            ChunksZ = chunksZ;
            SizeX = chunksX * ChunkSize;
            SizeY = chunksY * ChunkSize;
            SizeZ = chunksZ * ChunkSize;

            long total = (long)SizeX * SizeY * SizeZ;
            if (total > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(chunksX), "Dünya çok büyük.");

            materials = new byte[total];
            densities = new byte[total];
            regions = new byte[total];
            damage = new ushort[total];
        }

        // ---------- Erişim ----------

        public bool InBounds(int x, int y, int z)
        {
            return (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;
        }

        public int GetIndex(int x, int y, int z) => x + SizeX * (y + SizeY * z);

        public VoxelMaterialId GetMaterial(int x, int y, int z)
        {
            return InBounds(x, y, z) ? (VoxelMaterialId)materials[GetIndex(x, y, z)] : VoxelMaterialId.Air;
        }

        public bool IsSolid(int x, int y, int z)
        {
            return InBounds(x, y, z) && materials[GetIndex(x, y, z)] != 0;
        }

        public byte GetDensity(int x, int y, int z)
        {
            return InBounds(x, y, z) ? densities[GetIndex(x, y, z)] : (byte)0;
        }

        /// <summary>Hızlı yol: sınır kontrolü çağıranda yapılmış indeksle okuma (mesher kullanır).</summary>
        public byte GetMaterialAt(int index) => materials[index];
        public byte GetDensityAt(int index) => densities[index];

        /// <summary>
        /// (x, z) sütunundaki en üst dolu voxel'in y'si; sütun boşsa veya dünya dışındaysa -1.
        /// Kamera arazi koruması kullanır (yukarıdan aşağı tarama, en fazla SizeY adım).
        /// </summary>
        public int GetTopSolidY(int x, int z)
        {
            if ((uint)x >= (uint)SizeX || (uint)z >= (uint)SizeZ) return -1;

            for (int y = SizeY - 1; y >= 0; y--)
            {
                if (materials[GetIndex(x, y, z)] != 0) return y;
            }

            return -1;
        }

        public byte GetRegionAt(int index) => regions[index];

        /// <summary>Üretim: bölge ataması (RegionAssigner). Olay tetiklemez.</summary>
        public void SetRegionAt(int index, byte regionId) => regions[index] = regionId;

        public byte GetRegion(int x, int y, int z)
        {
            return InBounds(x, y, z) ? regions[GetIndex(x, y, z)] : (byte)0;
        }

        public void SetRegion(int x, int y, int z, byte regionId)
        {
            if (InBounds(x, y, z)) regions[GetIndex(x, y, z)] = regionId;
        }

        public int GetDamage(int x, int y, int z)
        {
            return InBounds(x, y, z) ? damage[GetIndex(x, y, z)] : 0;
        }

        public ushort GetDamageAt(int index) => damage[index];

        /// <summary>
        /// Hasarı ayarlar. Hasar görsel olarak gösterildiği (vertex rengi koyulaşır) için
        /// değer değişirse ilgili chunk'lar güncellenir. Boş voxel'e hasar yazılmaz.
        /// </summary>
        public void SetDamage(int x, int y, int z, int value)
        {
            if (!InBounds(x, y, z)) return;

            int i = GetIndex(x, y, z);
            if (materials[i] == 0) return;

            var clamped = (ushort)Mathf.Clamp(value, 0, ushort.MaxValue);
            if (damage[i] == clamped) return;

            damage[i] = clamped;
            NotifyChunksAround(x, y, z);
        }

        // ---------- Üretim ----------

        /// <summary>
        /// Üretici tarafından kullanılır; olay tetiklemez. Density, malzeme ile tutarlı olacak şekilde
        /// sınırlandırılır (dolu → >= IsoLevel, Air → < IsoLevel).
        /// </summary>
        public void SetGeneratedVoxel(int x, int y, int z, VoxelMaterialId material, byte density)
        {
            int i = GetIndex(x, y, z);
            materials[i] = (byte)material;
            densities[i] = material == VoxelMaterialId.Air
                ? (byte)Math.Min((int)density, IsoLevel - 1)
                : (byte)Math.Max((int)density, (int)IsoLevel);
            damage[i] = 0;
        }

        /// <summary>
        /// Üretim: dolu bir voxelin malzemesini değiştirir (density korunur). Maden dağıtımı kullanır.
        /// Boş voxele veya Air'e çevirmeye izin vermez (dolu sayısı değişmemeli). Olay tetiklemez.
        /// </summary>
        public bool SetGeneratedMaterialAt(int index, VoxelMaterialId material)
        {
            if (material == VoxelMaterialId.Air || materials[index] == 0) return false;
            materials[index] = (byte)material;
            return true;
        }

        /// <summary>Üretim bittikten sonra bir kez çağrılır: sayaçları ve içerik sınırlarını hesaplar.</summary>
        public void FinalizeGeneration()
        {
            int count = 0;
            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            for (int z = 0; z < SizeZ; z++)
            {
                for (int y = 0; y < SizeY; y++)
                {
                    int row = SizeX * (y + SizeY * z);
                    for (int x = 0; x < SizeX; x++)
                    {
                        if (materials[row + x] == 0) continue;
                        count++;
                        min = Vector3Int.Min(min, new Vector3Int(x, y, z));
                        max = Vector3Int.Max(max, new Vector3Int(x, y, z));
                    }
                }
            }

            SolidCount = count;
            InitialSolidCount = count;
            ContentMin = count > 0 ? min : Vector3Int.zero;
            ContentMax = count > 0 ? max : Vector3Int.zero;
        }

        // ---------- Değişiklik ----------

        /// <summary>
        /// Voxel'i kalıcı olarak boşaltır. Zaten boşsa false döner — bu sayede aynı voxel
        /// iki kez sayılamaz / iki kez ödül veremez (Faz 3'te ödül bu dönüş değerine bağlanacak).
        /// </summary>
        public bool RemoveVoxel(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) return false;

            int i = GetIndex(x, y, z);
            if (materials[i] == 0) return false;

            materials[i] = 0;
            densities[i] = 0;
            damage[i] = 0;
            SolidCount--;

            NotifyChunksAround(x, y, z);
            return true;
        }

        public int GetChunkIndex(int cx, int cy, int cz) => cx + ChunksX * (cy + ChunksY * cz);

        public Vector3Int GetChunkCoord(int chunkIndex)
        {
            int cx = chunkIndex % ChunksX;
            int rest = chunkIndex / ChunksX;
            int cy = rest % ChunksY;
            int cz = rest / ChunksY;
            return new Vector3Int(cx, cy, cz);
        }

        /// <summary>
        /// Bir chunk'ın mesh'i kendi voxellerine ek olarak her yönde 1 voxel komşu veri okur.
        /// Bu yüzden chunk sınırındaki (yerel 0 veya 15) bir voxel değişince komşu chunk'lar da güncellenir.
        /// </summary>
        private void NotifyChunksAround(int x, int y, int z)
        {
            Action<int> handler = ChunkChanged;
            if (handler == null) return;

            GetAffectedRange(x, ChunksX, out int x0, out int x1);
            GetAffectedRange(y, ChunksY, out int y0, out int y1);
            GetAffectedRange(z, ChunksZ, out int z0, out int z1);

            for (int cz = z0; cz <= z1; cz++)
            for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                handler(GetChunkIndex(cx, cy, cz));
            }
        }

        private static void GetAffectedRange(int coord, int chunkCount, out int min, out int max)
        {
            int chunk = coord / ChunkSize;
            int local = coord - chunk * ChunkSize;
            min = local == 0 && chunk > 0 ? chunk - 1 : chunk;
            max = local == ChunkSize - 1 && chunk < chunkCount - 1 ? chunk + 1 : chunk;
        }
    }
}
