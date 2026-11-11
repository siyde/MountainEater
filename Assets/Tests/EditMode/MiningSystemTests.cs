using System.Collections.Generic;
using MountainEater.Mining;
using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class MiningSystemTests
    {
        // Varsayılan tablo: Taş 3 HP / 5 $, Bakır 7 HP / 15 $, Altın 18 HP / 45 $, Kristal 45 HP / 120 $
        private static readonly VoxelMaterialTable Table = VoxelMaterialTable.CreateDefault();
        private static readonly Vector3Int Center = new Vector3Int(8, 8, 8);

        private static VoxelWorld SolidWorld(VoxelMaterialId material = VoxelMaterialId.Stone)
        {
            var world = new VoxelWorld(1, 1, 1);
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
                world.SetGeneratedVoxel(x, y, z, material, 255);
            world.FinalizeGeneration();
            return world;
        }

        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 33)]
        [TestCase(3, 123)]
        public void SphereOffsets_MatchDesignTable(int radius, int expectedCount)
        {
            Assert.AreEqual(expectedCount, SphereOffsets.Get(radius).Count);
            Assert.AreEqual(Vector3Int.zero, SphereOffsets.Get(radius)[0], "Merkez ilk sırada olmalı");
        }

        [TestCase(0, 1)]
        [TestCase(1, 7)]
        [TestCase(2, 33)]
        [TestCase(3, 123)]
        public void Hit_DamagesEveryVoxelInSphere(int radius, int expected)
        {
            var mining = new MiningSystem(SolidWorld(), Table);
            MiningResult result = mining.ApplyHit(Center, radius, 1, MiningSource.Player);
            Assert.AreEqual(expected, result.VoxelsDamaged);
            Assert.AreEqual(0, result.VoxelsBroken);
        }

        [Test]
        public void Stone_BreaksAfterThreeHitsOfOne_AndPaysOnce()
        {
            VoxelWorld world = SolidWorld();
            var mining = new MiningSystem(world, Table);
            int startCount = world.SolidCount;

            MiningResult first = mining.ApplyHit(Center, 0, 1, MiningSource.Player);
            MiningResult second = mining.ApplyHit(Center, 0, 1, MiningSource.Player);
            Assert.AreEqual(0, first.VoxelsBroken);
            Assert.AreEqual(0, second.VoxelsBroken);
            Assert.AreEqual(1, mining.GetRemainingHealth(Center));
            Assert.IsTrue(world.IsSolid(Center.x, Center.y, Center.z));

            MiningResult third = mining.ApplyHit(Center, 0, 1, MiningSource.Player);
            Assert.AreEqual(1, third.VoxelsBroken);
            Assert.AreEqual(5, third.Reward);
            Assert.IsFalse(world.IsSolid(Center.x, Center.y, Center.z));
            Assert.AreEqual(startCount - 1, world.SolidCount);

            // Kırılmış voxel tekrar vurulamaz, tekrar ödül vermez.
            MiningResult fourth = mining.ApplyHit(Center, 0, 1, MiningSource.Player);
            Assert.IsFalse(fourth.HitAnything);
            Assert.AreEqual(0, fourth.Reward);
            Assert.AreEqual(startCount - 1, world.SolidCount);
        }

        [Test]
        public void OverkillDamage_BreaksOnce_WithSingleReward()
        {
            var mining = new MiningSystem(SolidWorld(VoxelMaterialId.Crystal), Table);
            MiningResult result = mining.ApplyHit(Center, 0, 10_000, MiningSource.Player);
            Assert.AreEqual(1, result.VoxelsBroken);
            Assert.AreEqual(120, result.Reward);
        }

        [Test]
        public void PlayerAndMachine_OnSameVoxel_NeverDoublePay()
        {
            VoxelWorld world = SolidWorld();
            var mining = new MiningSystem(world, Table);

            mining.ApplyHit(Center, 0, 2, MiningSource.Machine);                        // 2/3
            MiningResult player = mining.ApplyHit(Center, 0, 5, MiningSource.Player);   // kırar
            MiningResult machine = mining.ApplyHit(Center, 0, 5, MiningSource.Machine); // boşluğa

            Assert.AreEqual(5, player.Reward + machine.Reward);
            Assert.AreEqual(1, player.VoxelsBroken + machine.VoxelsBroken);
        }

        [Test]
        public void RadiusHit_BreaksWholeSphere_AndRewardIsSum()
        {
            VoxelWorld world = SolidWorld();
            var mining = new MiningSystem(world, Table);
            MiningResult result = mining.ApplyHit(Center, 1, 3, MiningSource.Player);

            Assert.AreEqual(7, result.VoxelsBroken);
            Assert.AreEqual(7 * 5, result.Reward);
            Assert.AreEqual(world.InitialSolidCount - 7, world.SolidCount);
        }

        [Test]
        public void LockedRegion_IsNeverDamaged_EvenInsideRadius()
        {
            VoxelWorld world = SolidWorld();
            // x >= 9 olan her şey bölge 1 (kilitli).
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 9; x < world.SizeX; x++)
                world.SetRegion(x, y, z, 1);

            var mining = new MiningSystem(world, Table) { IsRegionUnlocked = region => region == 0 };
            MiningResult result = mining.ApplyHit(Center, 2, 100, MiningSource.Player);

            Assert.Greater(result.VoxelsBroken, 0);
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 9; x < world.SizeX; x++)
            {
                Assert.IsTrue(world.IsSolid(x, y, z), $"Kilitli voxel kırıldı: {x},{y},{z}");
                Assert.AreEqual(0, world.GetDamage(x, y, z), $"Kilitli voxel hasar aldı: {x},{y},{z}");
            }
        }

        [Test]
        public void TargetInLockedRegion_DoesNothing()
        {
            VoxelWorld world = SolidWorld();
            world.SetRegion(Center.x, Center.y, Center.z, 3);
            var mining = new MiningSystem(world, Table) { IsRegionUnlocked = region => region == 0 };

            Assert.IsFalse(mining.ApplyHit(Center, 2, 100, MiningSource.Player).HitAnything);
            Assert.AreEqual(world.InitialSolidCount, world.SolidCount);
        }

        [Test]
        public void HitOnAir_DoesNothing()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.FinalizeGeneration();
            var mining = new MiningSystem(world, Table);
            Assert.IsFalse(mining.ApplyHit(Center, 3, 5, MiningSource.Player).HitAnything);
        }

        [Test]
        public void MinedEvent_FiresOncePerHit_WithResult()
        {
            var mining = new MiningSystem(SolidWorld(), Table);
            var results = new List<MiningResult>();
            mining.Mined += results.Add;

            mining.ApplyHit(Center, 1, 3, MiningSource.Player);
            mining.ApplyHit(new Vector3Int(-5, 0, 0), 1, 3, MiningSource.Player); // dünya dışı: olay yok

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(7, results[0].VoxelsBroken);
        }

        [Test]
        public void Damage_IsStoredInWorld_AndDrivesRemainingHealth()
        {
            VoxelWorld world = SolidWorld(VoxelMaterialId.Gold);
            var mining = new MiningSystem(world, Table);
            mining.ApplyHit(Center, 0, 5, MiningSource.Player);

            Assert.AreEqual(5, world.GetDamage(Center.x, Center.y, Center.z));
            Assert.AreEqual(13, mining.GetRemainingHealth(Center));
        }
    }
}
