using System;
using UnityEngine;

namespace MountainEater.World
{
    /// <summary>
    /// Deterministik dağ şekli fonksiyonu: (x, z) → yükseklik.
    /// Saf C# sınıfıdır (MonoBehaviour değil): test edilebilir ve ayarlar kurucuda kopyalandığı için
    /// ileride Job System içinde de güvenle kullanılabilir.
    ///
    /// Şekil = iki "koni" zirvenin yumuşak birleşimi × (1 + fBm gürültü + sırt gürültüsü)
    /// Taban çizgisi alan bükme (domain warp) ile düzensizleştirilir, kenarda 0'a iner.
    /// </summary>
    public sealed class MountainShape
    {
        private const float EdgeFadeWidth = 0.12f;
        private const float OffsetRange = 1000f;

        private readonly float radius;
        private readonly float peakHeight;
        private readonly float falloffPower;
        private readonly float secondaryStrength;
        private readonly float warpStrength;
        private readonly float noiseScale;
        private readonly int octaves;
        private readonly float persistence;
        private readonly float lacunarity;
        private readonly float noiseStrength;
        private readonly float ridgeStrength;

        private readonly Vector2 noiseOffset;
        private readonly Vector2 ridgeOffset;
        private readonly Vector2 warpOffsetX;
        private readonly Vector2 warpOffsetZ;

        private readonly Vector2 mainPeak;
        private readonly Vector2 secondaryPeak;
        private readonly float mainRadius;
        private readonly float secondaryRadius;

        private readonly GradientNoise3D rockNoise;
        private readonly float rockStrength;
        private readonly float rockScale;

        public float FootprintRadius => radius;
        public int Seed { get; }

        /// <summary>SampleRockDetail'in mutlak değerinin üst sınırı (dünya birimi).</summary>
        public float RockDetailStrength => rockStrength;

        public MountainShape(MountainShapeSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Seed = settings.seed;
            radius = Mathf.Max(1f, settings.footprintRadius);
            peakHeight = Mathf.Max(0f, settings.peakHeight);
            falloffPower = Mathf.Max(0.01f, settings.falloffPower);
            secondaryStrength = Mathf.Clamp01(settings.secondaryPeakStrength);
            warpStrength = Mathf.Max(0f, settings.warpStrength);
            noiseScale = Mathf.Max(0.0001f, settings.noiseScale);
            octaves = Mathf.Clamp(settings.noiseOctaves, 1, 8);
            persistence = Mathf.Clamp01(settings.persistence);
            lacunarity = Mathf.Max(1f, settings.lacunarity);
            noiseStrength = Mathf.Clamp01(settings.noiseStrength);
            ridgeStrength = Mathf.Clamp01(settings.ridgeStrength);

            // System.Random(seed) platformlar arasında deterministiktir.
            var rng = new System.Random(settings.seed);
            noiseOffset = NextOffset(rng);
            ridgeOffset = NextOffset(rng);
            warpOffsetX = NextOffset(rng);
            warpOffsetZ = NextOffset(rng);

            float mainAngle = (float)(rng.NextDouble() * Math.PI * 2.0);
            mainPeak = new Vector2(Mathf.Cos(mainAngle), Mathf.Sin(mainAngle))
                       * (Mathf.Clamp01(settings.peakOffset) * radius * 0.35f);

            float secondaryAngle = mainAngle + Mathf.PI * (0.7f + (float)rng.NextDouble() * 0.6f);
            secondaryPeak = new Vector2(Mathf.Cos(secondaryAngle), Mathf.Sin(secondaryAngle)) * (radius * 0.45f);

            // Koni yarıçapları taban yarıçapını aşmayacak şekilde seçilir.
            mainRadius = radius - mainPeak.magnitude;
            secondaryRadius = radius - secondaryPeak.magnitude;

            // Ayrı bir seed türetilir; böylece 2D yükseklikler eklenen 3D detaydan etkilenmez.
            rockNoise = new GradientNoise3D(unchecked(settings.seed * 31 + 7919));
            rockStrength = Mathf.Max(0f, settings.rockDetailStrength);
            rockScale = Mathf.Max(0.0001f, settings.rockDetailScale);
        }

        /// <summary>
        /// Yüzeye eklenen 3D kaya detayı (dünya birimi, yaklaşık [-strength, +strength]).
        /// Pozitif = dolu tarafa doğru (çıkıntı), negatif = oyuk.
        /// </summary>
        public float SampleRockDetail(float x, float y, float z)
        {
            if (rockStrength <= 0f) return 0f;

            float n = rockNoise.Sample(x * rockScale, y * rockScale, z * rockScale)
                      + 0.5f * rockNoise.Sample(x * rockScale * 2.03f + 17.1f, y * rockScale * 2.03f - 9.7f, z * rockScale * 2.03f + 3.3f);
            return Mathf.Clamp(n / 1.5f, -1f, 1f) * rockStrength;
        }

        /// <summary>Verilen yatay noktadaki yüzey yüksekliği (>= 0). Taban dışında 0 döner.</summary>
        public float SampleHeight(float x, float z)
        {
            float warpScale = noiseScale * 0.5f;
            float wx = (Mathf.PerlinNoise(x * warpScale + warpOffsetX.x, z * warpScale + warpOffsetX.y) - 0.5f) * 2f;
            float wz = (Mathf.PerlinNoise(x * warpScale + warpOffsetZ.x, z * warpScale + warpOffsetZ.y) - 0.5f) * 2f;
            var p = new Vector2(x + wx * warpStrength * radius, z + wz * warpStrength * radius);

            float normalizedRadius = p.magnitude / radius;
            if (normalizedRadius >= 1f)
            {
                return 0f;
            }

            float main = Cone(p, mainPeak, mainRadius);
            float secondary = Cone(p, secondaryPeak, secondaryRadius) * secondaryStrength;
            float baseShape = main + secondary - main * secondary; // yumuşak birleşim (her zaman <= 1)

            float fbm = Fbm(p.x, p.y);           // [-1, 1]
            float ridge = Ridge(p.x, p.y);       // [0, 1]
            float detail = 1f + noiseStrength * fbm + ridgeStrength * (ridge - 0.5f);

            float edge = Mathf.Clamp01((1f - normalizedRadius) / EdgeFadeWidth);
            edge = edge * edge * (3f - 2f * edge); // smoothstep

            return Mathf.Max(0f, baseShape * detail * edge * peakHeight);
        }

        private float Cone(Vector2 p, Vector2 center, float coneRadius)
        {
            if (coneRadius <= 0f) return 0f;
            float t = Mathf.Clamp01(Vector2.Distance(p, center) / coneRadius);
            return Mathf.Pow(1f - t, falloffPower);
        }

        private float Fbm(float x, float z)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = noiseScale;
            float normalization = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(x * frequency + noiseOffset.x, z * frequency + noiseOffset.y) * 2f - 1f;
                sum += n * amplitude;
                normalization += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return sum / normalization;
        }

        private float Ridge(float x, float z)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = noiseScale * 1.5f;
            float normalization = 0f;

            for (int i = 0; i < 3; i++)
            {
                float n = Mathf.PerlinNoise(x * frequency + ridgeOffset.x, z * frequency + ridgeOffset.y);
                float r = 1f - Mathf.Abs(n * 2f - 1f);
                sum += r * r * amplitude;
                normalization += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / normalization;
        }

        private static Vector2 NextOffset(System.Random rng)
        {
            return new Vector2(
                (float)(rng.NextDouble() * 2.0 - 1.0) * OffsetRange,
                (float)(rng.NextDouble() * 2.0 - 1.0) * OffsetRange);
        }
    }
}
