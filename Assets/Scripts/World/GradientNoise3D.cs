using UnityEngine;

namespace MountainEater.World
{
    /// <summary>
    /// Seed'li 3D gradyan (Perlin "improved") gürültüsü. Unity'de yerleşik 3D Perlin olmadığı için.
    /// Çıktı yaklaşık [-1, 1]. Deterministik: aynı seed ve koordinat → aynı değer.
    /// </summary>
    public sealed class GradientNoise3D
    {
        private readonly int[] permutation = new int[512];

        public GradientNoise3D(int seed)
        {
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            var rng = new System.Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            for (int i = 0; i < 512; i++) permutation[i] = p[i & 255];
        }

        public float Sample(float x, float y, float z)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            int zi = Mathf.FloorToInt(z);
            x -= xi;
            y -= yi;
            z -= zi;
            xi &= 255;
            yi &= 255;
            zi &= 255;

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int[] perm = permutation;
            int a = perm[xi] + yi;
            int aa = perm[a] + zi;
            int ab = perm[a + 1] + zi;
            int b = perm[xi + 1] + yi;
            int ba = perm[b] + zi;
            int bb = perm[b + 1] + zi;

            return Lerp(w,
                Lerp(v,
                    Lerp(u, Grad(perm[aa], x, y, z), Grad(perm[ba], x - 1f, y, z)),
                    Lerp(u, Grad(perm[ab], x, y - 1f, z), Grad(perm[bb], x - 1f, y - 1f, z))),
                Lerp(v,
                    Lerp(u, Grad(perm[aa + 1], x, y, z - 1f), Grad(perm[ba + 1], x - 1f, y, z - 1f)),
                    Lerp(u, Grad(perm[ab + 1], x, y - 1f, z - 1f), Grad(perm[bb + 1], x - 1f, y - 1f, z - 1f))));
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
