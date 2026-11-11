using MountainEater.Mining;
using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class MiningTargetResolverTests
    {
        private static VoxelWorld World(params Vector3Int[] solids)
        {
            var world = new VoxelWorld(1, 1, 1);
            foreach (Vector3Int v in solids) world.SetGeneratedVoxel(v.x, v.y, v.z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void PointOnSurfaceOfSingleVoxel_ResolvesToThatVoxel()
        {
            var voxel = new Vector3Int(8, 8, 8);
            VoxelWorld world = World(voxel);

            // Voxel merkezinin +X tarafındaki yüzey noktası (yüzey merkez ile komşu merkezin arasında).
            var point = new Vector3(8.5f + 0.45f, 8.5f, 8.5f);
            Assert.IsTrue(MiningTargetResolver.TryResolve(world, point, Vector3.right, out Vector3Int target));
            Assert.AreEqual(voxel, target);
        }

        [Test]
        public void PicksVoxelBehindSurface_NotTheOneBesideIt()
        {
            // Yan yana iki voxel; üstten (+Y) tıklanan nokta sağdakinin üstünde.
            var left = new Vector3Int(7, 8, 8);
            var right = new Vector3Int(8, 8, 8);
            VoxelWorld world = World(left, right);

            var point = new Vector3(8.7f, 9.0f, 8.5f);
            Assert.IsTrue(MiningTargetResolver.TryResolve(world, point, Vector3.up, out Vector3Int target));
            Assert.AreEqual(right, target);
        }

        [Test]
        public void NoSolidNearby_ReturnsFalse()
        {
            VoxelWorld world = World(new Vector3Int(1, 1, 1));
            Assert.IsFalse(MiningTargetResolver.TryResolve(world, new Vector3(10f, 10f, 10f), Vector3.up, out _));
        }

        [Test]
        public void MissingNormal_StillResolves()
        {
            var voxel = new Vector3Int(4, 4, 4);
            VoxelWorld world = World(voxel);
            Assert.IsTrue(MiningTargetResolver.TryResolve(world, new Vector3(4.6f, 5.1f, 4.4f), Vector3.zero, out Vector3Int target));
            Assert.AreEqual(voxel, target);
        }
    }
}
