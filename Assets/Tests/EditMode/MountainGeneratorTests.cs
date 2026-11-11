using MountainEater.Voxel;
using MountainEater.World;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class MountainGeneratorTests
    {
        private MountainShapeSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MountainShapeSettings>();
            // Testler hızlı olsun diye küçük dağ.
            settings.footprintRadius = 30f;
            settings.peakHeight = 24f;
            settings.voxelSize = 2f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Generate_ProducesSolidVoxels()
        {
            VoxelWorld world = MountainGenerator.Generate(settings);
            Assert.Greater(world.InitialSolidCount, 100);
            Assert.AreEqual(world.InitialSolidCount, world.SolidCount);
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            VoxelWorld a = MountainGenerator.Generate(settings);
            VoxelWorld b = MountainGenerator.Generate(settings);

            Assert.AreEqual(a.InitialSolidCount, b.InitialSolidCount);
            for (int z = 0; z < a.SizeZ; z += 3)
            for (int y = 0; y < a.SizeY; y += 3)
            for (int x = 0; x < a.SizeX; x += 3)
            {
                Assert.AreEqual(a.GetMaterial(x, y, z), b.GetMaterial(x, y, z));
                Assert.AreEqual(a.GetDensity(x, y, z), b.GetDensity(x, y, z));
            }
        }

        [Test]
        public void InitialSolidCount_MatchesRealVoxelData()
        {
            VoxelWorld world = MountainGenerator.Generate(settings);
            int count = 0;
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
                if (world.IsSolid(x, y, z)) count++;

            Assert.AreEqual(count, world.InitialSolidCount);
        }

        [Test]
        public void Mountain_DoesNotTouchWorldSidesOrTop()
        {
            VoxelWorld world = MountainGenerator.Generate(settings);
            Assert.Greater(world.ContentMin.x, 0);
            Assert.Greater(world.ContentMin.z, 0);
            Assert.Less(world.ContentMax.x, world.SizeX - 1);
            Assert.Less(world.ContentMax.z, world.SizeZ - 1);
            Assert.Less(world.ContentMax.y, world.SizeY - 1);
            Assert.AreEqual(0, world.ContentMin.y, "Dağ zemine (y = 0) oturmalı");
        }

        [Test]
        public void Columns_HaveNoFloatingVoxels()
        {
            // Her sütunda dolu voxeller alttan kesintisiz olmalı (havada asılı parça yok).
            VoxelWorld world = MountainGenerator.Generate(settings);
            for (int z = 0; z < world.SizeZ; z++)
            for (int x = 0; x < world.SizeX; x++)
            {
                bool seenAir = false;
                for (int y = 0; y < world.SizeY; y++)
                {
                    bool solid = world.IsSolid(x, y, z);
                    if (!solid) seenAir = true;
                    else Assert.IsFalse(seenAir, $"Sütun ({x},{z}) y={y}'de havada dolu voxel");
                }
            }
        }
    }
}
