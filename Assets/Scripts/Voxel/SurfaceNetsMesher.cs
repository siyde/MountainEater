using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>
    /// Bir chunk için "Naive Surface Nets" yüzeyi üretir.
    ///
    /// Örnek noktaları = voxel merkezleri. Hücre = 8 komşu voxel merkezinin oluşturduğu küp.
    ///   1) İçinde hem dolu hem boş köşe olan her hücreye TEK köşe (vertex) konur: hücre kenarlarındaki
    ///      yüzey geçiş noktalarının ortalaması (geçiş noktası density ile enterpolasyonla bulunur).
    ///   2) Dolu ↔ boş geçen her voxel-voxel kenarı için, o kenarı çevreleyen 4 hücrenin köşeleri bir dörtgen olur.
    /// Sonuç: kapalı, yumuşak, küp görünmeyen bir yüzey. Dolu her voxel merkezi yüzeyin içinde, boş her voxel
    /// merkezi dışında kalır → görüntü ile voxel verisi her zaman tutarlıdır.
    ///
    /// Chunk sınırları: chunk, taban örneği kendi içinde olan kenarların dörtgenlerini üretir ve bunun için
    /// her yönde 1 voxel komşu veri okur (18³ örnek). Aynı hücre köşesi komşu chunk'ta da aynı veriden
    /// hesaplandığından dikiş (seam) oluşmaz. Normaller de yalnızca hücrenin 8 köşesinden hesaplanır.
    ///
    /// Bellek: tüm tamponlar yeniden kullanılır; Build çağrısı çöp (GC) üretmez.
    /// </summary>
    public sealed class SurfaceNetsMesher
    {
        private const int N = VoxelWorld.ChunkSize; // 16
        private const int S = N + 2;                // örnek: yerel -1 .. 16
        private const int C = N + 1;                // hücre: yerel -1 .. 15
        private const float Iso = VoxelWorld.IsoLevel - 0.5f;

        private static readonly int[] EdgeCornerA = new int[12];
        private static readonly int[] EdgeCornerB = new int[12];

        /// <summary>Tamamen hasar görmüş voxelin rengi bu oranda koyulaşır (görsel hasar göstergesi).</summary>
        private const float MaxDamageDarkening = 0.5f;

        private readonly float[] densities = new float[S * S * S];
        private readonly byte[] materials = new byte[S * S * S];
        private readonly ushort[] damages = new ushort[S * S * S];
        private readonly byte[] regions = new byte[S * S * S];
        private readonly int[] cellVertex = new int[C * C * C];
        private readonly float[] corners = new float[8];

        private float voxelSize;
        private VoxelMaterialTable materialTable;
        private Vector3Int sampleOrigin; // örnek indeksi 0'ın dünya voxel koordinatı

        public List<Vector3> Vertices { get; } = new List<Vector3>(4096);
        public List<Vector3> Normals { get; } = new List<Vector3>(4096);
        public List<Color32> Colors { get; } = new List<Color32>(4096);
        public List<int> Triangles { get; } = new List<int>(16384);

        /// <summary>UV2 (TEXCOORD1): x = bölge kimliği, y = 1 ise bölge sınırı. Bölge görünümü shader'da çizilir.</summary>
        public List<Vector2> RegionData { get; } = new List<Vector2>(4096);

        static SurfaceNetsMesher()
        {
            // Köşe k'nın bitleri: 1 = +x, 2 = +y, 4 = +z. Tek bitte farklı köşe çiftleri = 12 kenar.
            int e = 0;
            for (int k = 0; k < 8; k++)
            {
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((k & bit) != 0) continue;
                    EdgeCornerA[e] = k;
                    EdgeCornerB[e] = k | bit;
                    e++;
                }
            }
        }

        /// <summary>
        /// Chunk mesh'ini Vertices/Normals/Colors/Triangles listelerine yazar.
        /// Mesh pozisyonları chunk'ın yerel uzayındadır (chunk köşesi = 0).
        /// Yüzey yoksa false döner.
        /// </summary>
        public bool Build(VoxelWorld world, Vector3Int chunkCoord, float voxelSizeWorld, VoxelMaterialTable table)
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Triangles.Clear();
            RegionData.Clear();

            voxelSize = voxelSizeWorld;
            materialTable = table;
            sampleOrigin = chunkCoord * N - Vector3Int.one;

            if (!LoadSamples(world))
            {
                return false; // tamamen dolu veya tamamen boş: yüzey yok
            }

            Array.Fill(cellVertex, -1);

            for (int z = 1; z <= N; z++)
            for (int y = 1; y <= N; y++)
            for (int x = 1; x <= N; x++)
            {
                bool solidP = materials[SampleIndex(x, y, z)] != 0;

                for (int axis = 0; axis < 3; axis++)
                {
                    Step(axis, out int ax, out int ay, out int az);
                    bool solidQ = materials[SampleIndex(x + ax, y + ay, z + az)] != 0;
                    if (solidP == solidQ) continue;

                    Step((axis + 1) % 3, out int ux, out int uy, out int uz);
                    Step((axis + 2) % 3, out int vx, out int vy, out int vz);

                    int a = GetOrCreateVertex(x - ux - vx, y - uy - vy, z - uz - vz);
                    int b = GetOrCreateVertex(x - vx, y - vy, z - vz);
                    int c = GetOrCreateVertex(x, y, z);
                    int d = GetOrCreateVertex(x - ux, y - uy, z - uz);

                    // Normal dolu taraftan boş tarafa bakmalı.
                    if (solidP) AddQuad(a, b, c, d);
                    else AddQuad(a, d, c, b);
                }
            }

            return Triangles.Count > 0;
        }

        private bool LoadSamples(VoxelWorld world)
        {
            bool anySolid = false;
            bool anyAir = false;

            for (int z = 0; z < S; z++)
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int wx = sampleOrigin.x + x;
                int wy = sampleOrigin.y + y;
                int wz = sampleOrigin.z + z;
                int i = SampleIndex(x, y, z);

                if (world.InBounds(wx, wy, wz))
                {
                    int wi = world.GetIndex(wx, wy, wz);
                    materials[i] = world.GetMaterialAt(wi);
                    densities[i] = world.GetDensityAt(wi);
                    damages[i] = world.GetDamageAt(wi);
                    regions[i] = world.GetRegionAt(wi);
                }
                else
                {
                    materials[i] = 0;
                    densities[i] = 0f;
                    damages[i] = 0;
                    regions[i] = 0;
                }

                if (materials[i] != 0) anySolid = true;
                else anyAir = true;
            }

            return anySolid && anyAir;
        }

        private int GetOrCreateVertex(int cx, int cy, int cz)
        {
            int cellIndex = cx + C * (cy + C * cz);
            int existing = cellVertex[cellIndex];
            if (existing >= 0) return existing;

            byte bestMaterial = 0;
            float damage01 = 0f;
            int region = -1;
            bool regionBorder = false;
            for (int k = 0; k < 8; k++)
            {
                int si = SampleIndex(cx + (k & 1), cy + ((k >> 1) & 1), cz + ((k >> 2) & 1));
                corners[k] = densities[si];

                byte material = materials[si];
                if (material == 0) continue;
                if (material > bestMaterial) bestMaterial = material; // nadir malzeme baskın

                // Bölge: dolu köşelerin en küçük kimliği; farklı bölgeler varsa sınır hücresi.
                byte sampleRegion = regions[si];
                if (region < 0) region = sampleRegion;
                else if (sampleRegion != region)
                {
                    regionBorder = true;
                    if (sampleRegion < region) region = sampleRegion;
                }

                if (damages[si] > 0 && materialTable != null)
                {
                    float ratio = damages[si] / (float)Mathf.Max(1, materialTable.GetMaxHealth(material));
                    if (ratio > damage01) damage01 = ratio;
                }
            }

            // 1) Konum: kenar geçişlerinin ortalaması
            Vector3 sum = Vector3.zero;
            int crossings = 0;
            for (int e = 0; e < 12; e++)
            {
                int ka = EdgeCornerA[e];
                int kb = EdgeCornerB[e];
                float da = corners[ka];
                float db = corners[kb];
                if ((da > Iso) == (db > Iso)) continue;

                float t = (Iso - da) / (db - da);
                Vector3 pa = CornerOffset(ka);
                sum += pa + (CornerOffset(kb) - pa) * t;
                crossings++;
            }

            Vector3 inCell = crossings > 0 ? sum / crossings : new Vector3(0.5f, 0.5f, 0.5f);

            // Örnek indeksi s, voxel merkezi (s - 0.5) * voxelSize konumundadır (chunk yerel uzayı).
            Vector3 position = (new Vector3(cx, cy, cz) + inCell - new Vector3(0.5f, 0.5f, 0.5f)) * voxelSize;

            // 2) Normal: density gradyanının tersi (dolu içeride, normal dışarı bakar)
            float gx = (corners[1] + corners[3] + corners[5] + corners[7]) - (corners[0] + corners[2] + corners[4] + corners[6]);
            float gy = (corners[2] + corners[3] + corners[6] + corners[7]) - (corners[0] + corners[1] + corners[4] + corners[5]);
            float gz = (corners[4] + corners[5] + corners[6] + corners[7]) - (corners[0] + corners[1] + corners[2] + corners[3]);
            var normal = new Vector3(-gx, -gy, -gz);
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;

            // 3) Renk: malzeme rengi + hafif doğal ton farkı + hasar koyulaşması
            Color32 baseColor = materialTable != null ? materialTable.GetColor(bestMaterial) : new Color32(128, 122, 115, 255);
            float gxWorld = sampleOrigin.x + cx + inCell.x;
            float gyWorld = sampleOrigin.y + cy + inCell.y;
            float gzWorld = sampleOrigin.z + cz + inCell.z;
            float shade = 0.85f + 0.3f * Mathf.PerlinNoise(gxWorld * 0.17f + gyWorld * 0.07f, gzWorld * 0.17f - gyWorld * 0.11f);
            shade *= 1f - MaxDamageDarkening * Mathf.Clamp01(damage01);

            int index = Vertices.Count;
            Vertices.Add(position);
            Normals.Add(normal);
            Colors.Add(new Color32(
                (byte)Mathf.Clamp(baseColor.r * shade, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.g * shade, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.b * shade, 0f, 255f),
                materialTable != null ? materialTable.GetSmoothness(bestMaterial) : (byte)0)); // alfa = parlaklık
            RegionData.Add(new Vector2(Mathf.Max(0, region), regionBorder ? 1f : 0f));

            cellVertex[cellIndex] = index;
            return index;
        }

        private void AddQuad(int a, int b, int c, int d)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);

            Triangles.Add(a);
            Triangles.Add(c);
            Triangles.Add(d);
        }

        private static Vector3 CornerOffset(int k) => new Vector3(k & 1, (k >> 1) & 1, (k >> 2) & 1);

        private static int SampleIndex(int x, int y, int z) => x + S * (y + S * z);

        private static void Step(int axis, out int x, out int y, out int z)
        {
            x = axis == 0 ? 1 : 0;
            y = axis == 1 ? 1 : 0;
            z = axis == 2 ? 1 : 0;
        }
    }
}
