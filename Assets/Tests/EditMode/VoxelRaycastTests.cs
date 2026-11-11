using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class VoxelRaycastTests
    {
        private static VoxelWorld World(params Vector3Int[] solids)
        {
            var world = new VoxelWorld(2, 2, 2);
            foreach (Vector3Int v in solids) world.SetGeneratedVoxel(v.x, v.y, v.z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void RayFromOutsideWorld_HitsFirstSolidVoxel()
        {
            var near = new Vector3Int(10, 5, 5);
            var far = new Vector3Int(20, 5, 5);
            VoxelWorld world = World(near, far);

            var origin = new Vector3(-10f, 5.5f, 5.5f);
            Assert.IsTrue(VoxelRaycast.Raycast(world, origin, Vector3.right, 1000f, out VoxelRaycastHit hit));
            Assert.AreEqual(near, hit.Voxel);
            Assert.AreEqual(new Vector3Int(-1, 0, 0), hit.FaceNormal);
            Assert.AreEqual(20f, hit.Distance, 1e-3f); // x = 10 yüzüne kadar
        }

        [Test]
        public void DiagonalRay_FromAbove_HitsVoxel()
        {
            var target = new Vector3Int(16, 3, 16);
            VoxelWorld world = World(target);

            Vector3 center = (Vector3)target + new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 origin = center + new Vector3(-20f, 40f, -15f);
            Assert.IsTrue(VoxelRaycast.Raycast(world, origin, center - origin, 1000f, out VoxelRaycastHit hit));
            Assert.AreEqual(target, hit.Voxel);
            Assert.AreEqual(1, hit.FaceNormal.sqrMagnitude, "Yüz normali eksen yönlü birim vektör olmalı");
        }

        [Test]
        public void RayMissingEverything_ReturnsFalse()
        {
            VoxelWorld world = World(new Vector3Int(5, 5, 5));
            Assert.IsFalse(VoxelRaycast.Raycast(world, new Vector3(-5f, 20f, 20f), Vector3.right, 1000f, out _));
            Assert.IsFalse(VoxelRaycast.Raycast(world, new Vector3(-5f, 5.5f, 5.5f), Vector3.left, 1000f, out _));
        }

        [Test]
        public void MaxDistance_IsRespected()
        {
            VoxelWorld world = World(new Vector3Int(20, 5, 5));
            Assert.IsFalse(VoxelRaycast.Raycast(world, new Vector3(0.5f, 5.5f, 5.5f), Vector3.right, 10f, out _));
            Assert.IsTrue(VoxelRaycast.Raycast(world, new Vector3(0.5f, 5.5f, 5.5f), Vector3.right, 30f, out _));
        }

        [Test]
        public void RemovedVoxel_IsSkipped()
        {
            var first = new Vector3Int(10, 5, 5);
            var second = new Vector3Int(11, 5, 5);
            VoxelWorld world = World(first, second);
            world.RemoveVoxel(first.x, first.y, first.z);

            Assert.IsTrue(VoxelRaycast.Raycast(world, new Vector3(0.5f, 5.5f, 5.5f), Vector3.right, 100f, out VoxelRaycastHit hit));
            Assert.AreEqual(second, hit.Voxel);
        }
    }
}
