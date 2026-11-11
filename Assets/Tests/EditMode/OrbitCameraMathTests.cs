using MountainEater.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class OrbitCameraMathTests
    {
        private const float Epsilon = 1e-3f;

        [Test]
        public void ComputePosition_IsAtRequestedDistanceFromFocus()
        {
            var focus = new Vector3(10f, 5f, -3f);
            Vector3 position = OrbitCameraMath.ComputePosition(focus, 37f, 25f, 50f);
            Assert.AreEqual(50f, Vector3.Distance(focus, position), Epsilon);
        }

        [Test]
        public void ComputePosition_PositivePitch_IsAboveFocus()
        {
            Vector3 position = OrbitCameraMath.ComputePosition(Vector3.zero, 0f, 30f, 10f);
            Assert.Greater(position.y, 0f);
        }

        [Test]
        public void ComputePosition_ZeroAngles_IsBehindFocus()
        {
            Vector3 position = OrbitCameraMath.ComputePosition(Vector3.zero, 0f, 0f, 10f);
            Assert.AreEqual(-10f, position.z, Epsilon);
            Assert.AreEqual(0f, position.y, Epsilon);
        }

        [TestCase(1f, 1f)]
        [TestCase(-1f, -1f)]
        [TestCase(120f, 1f)]
        [TestCase(-240f, -2f)]
        public void ScrollToNotches_HandlesBothScrollScales(float raw, float expected)
        {
            Assert.AreEqual(expected, OrbitCameraMath.ScrollToNotches(raw), Epsilon);
        }

        [Test]
        public void ApplyZoom_PositiveNotchesZoomIn_AndRespectsLimits()
        {
            float zoomedIn = OrbitCameraMath.ApplyZoom(100f, 1f, 0.1f, 5f, 500f);
            Assert.AreEqual(90f, zoomedIn, Epsilon);

            Assert.AreEqual(5f, OrbitCameraMath.ApplyZoom(6f, 100f, 0.1f, 5f, 500f), Epsilon);
            Assert.AreEqual(500f, OrbitCameraMath.ApplyZoom(400f, -100f, 0.1f, 5f, 500f), Epsilon);
        }

        [Test]
        public void Damp_ReachesTargetOverTime_IndependentOfFrameRate()
        {
            float slowFrames = 0f;
            for (int i = 0; i < 30; i++) slowFrames = OrbitCameraMath.Damp(slowFrames, 1f, 10f, 1f / 30f);

            float fastFrames = 0f;
            for (int i = 0; i < 144; i++) fastFrames = OrbitCameraMath.Damp(fastFrames, 1f, 10f, 1f / 144f);

            Assert.AreEqual(slowFrames, fastFrames, Epsilon);
            Assert.Greater(slowFrames, 0.99f);
        }

        [Test]
        public void WorldUnitsPerPixel_MatchesFrustumHeight()
        {
            // 90° FOV, 10 m mesafede görüş yüksekliği 20 m; 1000 piksel → 0.02 m/piksel.
            Assert.AreEqual(0.02f, OrbitCameraMath.WorldUnitsPerPixel(10f, 90f, 1000f), Epsilon);
        }

        [Test]
        public void DistanceToFitSphere_IsLargerThanRadius()
        {
            float d = OrbitCameraMath.DistanceToFitSphere(50f, 60f, 16f / 9f);
            Assert.AreEqual(100f, d, 0.01f); // sin(30°) = 0.5
        }
    }
}
