using MountainEater.World;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class MountainShapeTests
    {
        private MountainShapeSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MountainShapeSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void SameSeed_ProducesIdenticalHeights()
        {
            var a = new MountainShape(settings);
            var b = new MountainShape(settings);

            for (int i = -60; i <= 60; i += 7)
            {
                for (int j = -60; j <= 60; j += 7)
                {
                    Assert.AreEqual(a.SampleHeight(i, j), b.SampleHeight(i, j));
                }
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentMountain()
        {
            var a = new MountainShape(settings);
            settings.seed += 1;
            var b = new MountainShape(settings);

            float difference = 0f;
            for (int i = -40; i <= 40; i += 10)
            {
                for (int j = -40; j <= 40; j += 10)
                {
                    difference += Mathf.Abs(a.SampleHeight(i, j) - b.SampleHeight(i, j));
                }
            }

            Assert.Greater(difference, 1f);
        }

        [Test]
        public void HeightIsZero_FarOutsideFootprint()
        {
            var shape = new MountainShape(settings);
            float far = settings.footprintRadius * 2f;

            Assert.AreEqual(0f, shape.SampleHeight(far, 0f));
            Assert.AreEqual(0f, shape.SampleHeight(-far, far));
        }

        [Test]
        public void HeightIsNeverNegative_AndMountainHasRealHeight()
        {
            var shape = new MountainShape(settings);
            float max = 0f;
            float r = settings.footprintRadius;

            for (float x = -r; x <= r; x += 3f)
            {
                for (float z = -r; z <= r; z += 3f)
                {
                    float h = shape.SampleHeight(x, z);
                    Assert.GreaterOrEqual(h, 0f);
                    max = Mathf.Max(max, h);
                }
            }

            Assert.Greater(max, settings.peakHeight * 0.4f, "Dağ beklenenden çok alçak.");
        }
    }
}
