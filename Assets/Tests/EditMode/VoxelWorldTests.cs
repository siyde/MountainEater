using System.Collections.Generic;
using MountainEater.Voxel;
using NUnit.Framework;

namespace MountainEater.Tests
{
    public sealed class VoxelWorldTests
    {
        private static VoxelWorld CreateWorldWithSolid(int chunksX, params (int x, int y, int z)[] solids)
        {
            var world = new VoxelWorld(chunksX, 1, 1);
            foreach (var (x, y, z) in solids)
            {
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            }

            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void Sizes_AreChunkMultiples()
        {
            var world = new VoxelWorld(3, 2, 4);
            Assert.AreEqual(48, world.SizeX);
            Assert.AreEqual(32, world.SizeY);
            Assert.AreEqual(64, world.SizeZ);
            Assert.AreEqual(24, world.ChunkCount);
        }

        [Test]
        public void OutOfBounds_IsAir()
        {
            var world = CreateWorldWithSolid(1, (0, 0, 0));
            Assert.IsFalse(world.IsSolid(-1, 0, 0));
            Assert.IsFalse(world.IsSolid(16, 0, 0));
            Assert.AreEqual(VoxelMaterialId.Air, world.GetMaterial(0, -1, 0));
            Assert.AreEqual(0, world.GetDensity(0, 0, 99));
        }

        [Test]
        public void FinalizeGeneration_CountsSolidVoxels_AndContentBounds()
        {
            var world = CreateWorldWithSolid(1, (2, 3, 4), (10, 1, 7), (5, 5, 5));
            Assert.AreEqual(3, world.InitialSolidCount);
            Assert.AreEqual(3, world.SolidCount);
            Assert.AreEqual(new UnityEngine.Vector3Int(2, 1, 4), world.ContentMin);
            Assert.AreEqual(new UnityEngine.Vector3Int(10, 5, 7), world.ContentMax);
        }

        [Test]
        public void GeneratedDensity_IsConsistentWithMaterial()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(1, 1, 1, VoxelMaterialId.Stone, 10);   // dolu ama düşük density verilmiş
            world.SetGeneratedVoxel(2, 2, 2, VoxelMaterialId.Air, 250);    // boş ama yüksek density verilmiş

            Assert.GreaterOrEqual(world.GetDensity(1, 1, 1), VoxelWorld.IsoLevel);
            Assert.Less(world.GetDensity(2, 2, 2), VoxelWorld.IsoLevel);
        }

        [Test]
        public void RemoveVoxel_RemovesOnce_AndDecrementsCount()
        {
            var world = CreateWorldWithSolid(1, (4, 4, 4));

            Assert.IsTrue(world.RemoveVoxel(4, 4, 4));
            Assert.IsFalse(world.IsSolid(4, 4, 4));
            Assert.AreEqual(0, world.GetDensity(4, 4, 4));
            Assert.AreEqual(0, world.SolidCount);
            Assert.AreEqual(1, world.InitialSolidCount);

            // İkinci deneme: sayaç tekrar düşmemeli (çift sayım / çift ödül yok).
            Assert.IsFalse(world.RemoveVoxel(4, 4, 4));
            Assert.AreEqual(0, world.SolidCount);
        }

        [Test]
        public void RemoveVoxel_OnAirOrOutOfBounds_ReturnsFalse()
        {
            var world = CreateWorldWithSolid(1, (4, 4, 4));
            Assert.IsFalse(world.RemoveVoxel(5, 5, 5));
            Assert.IsFalse(world.RemoveVoxel(-1, 0, 0));
            Assert.AreEqual(1, world.SolidCount);
        }

        [Test]
        public void RemoveVoxel_InsideChunk_NotifiesOnlyThatChunk()
        {
            var world = CreateWorldWithSolid(2, (5, 5, 5));
            var notified = new List<int>();
            world.ChunkChanged += notified.Add;

            world.RemoveVoxel(5, 5, 5);

            CollectionAssert.AreEquivalent(new[] { 0 }, notified);
        }

        [Test]
        public void RemoveVoxel_OnChunkBorder_AlsoNotifiesNeighbour()
        {
            var world = CreateWorldWithSolid(3, (16, 5, 5), (15, 5, 5));
            var notified = new List<int>();
            world.ChunkChanged += notified.Add;

            world.RemoveVoxel(16, 5, 5); // chunk 1, yerel x = 0 → chunk 0 da etkilenir
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, notified);

            notified.Clear();
            world.RemoveVoxel(15, 5, 5); // chunk 0, yerel x = 15 → chunk 1 de etkilenir
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, notified);
        }

        [Test]
        public void SetDamage_NotifiesOnlyWhenChanged_AndIgnoresAir()
        {
            var world = CreateWorldWithSolid(1, (5, 5, 5));
            var notified = new List<int>();
            world.ChunkChanged += notified.Add;

            world.SetDamage(5, 5, 5, 2);
            world.SetDamage(5, 5, 5, 2); // aynı değer: tekrar güncelleme yok
            world.SetDamage(6, 6, 6, 2); // boş voxel: yok sayılır

            Assert.AreEqual(1, notified.Count);
            Assert.AreEqual(2, world.GetDamage(5, 5, 5));
            Assert.AreEqual(0, world.GetDamage(6, 6, 6));
        }

        [Test]
        public void GetTopSolidY_ReturnsHighestSolid_AndUpdatesAfterRemoval()
        {
            var world = CreateWorldWithSolid(1, (3, 0, 3), (3, 1, 3), (3, 7, 3));
            Assert.AreEqual(7, world.GetTopSolidY(3, 3));
            Assert.AreEqual(-1, world.GetTopSolidY(4, 4));
            Assert.AreEqual(-1, world.GetTopSolidY(-1, 3));

            world.RemoveVoxel(3, 7, 3);
            Assert.AreEqual(1, world.GetTopSolidY(3, 3));
        }

        [Test]
        public void ChunkCoord_RoundTrips()
        {
            var world = new VoxelWorld(3, 2, 4);
            for (int i = 0; i < world.ChunkCount; i++)
            {
                var c = world.GetChunkCoord(i);
                Assert.AreEqual(i, world.GetChunkIndex(c.x, c.y, c.z));
            }
        }
    }
}
