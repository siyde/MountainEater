using MountainEater.Voxel;
using NUnit.Framework;
using UnityEngine;

namespace MountainEater.Tests
{
    public sealed class SurfaceNetsMesherTests
    {
        private const float VoxelSize = 1f;
        private readonly VoxelMaterialTable palette = VoxelMaterialTable.CreateDefault();

        private static VoxelWorld WorldWithSingleVoxel(int chunksX, int x, int y, int z)
        {
            var world = new VoxelWorld(chunksX, 1, 1);
            world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();
            return world;
        }

        [Test]
        public void EmptyChunk_ProducesNoMesh()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.FinalizeGeneration();
            Assert.IsFalse(new SurfaceNetsMesher().Build(world, Vector3Int.zero, VoxelSize, palette));
        }

        [Test]
        public void FullySolidChunk_ProducesNoMesh_WhenNeighboursAreSolidToo()
        {
            var world = new VoxelWorld(3, 3, 3);
            for (int z = 0; z < world.SizeZ; z++)
            for (int y = 0; y < world.SizeY; y++)
            for (int x = 0; x < world.SizeX; x++)
                world.SetGeneratedVoxel(x, y, z, VoxelMaterialId.Stone, 255);
            world.FinalizeGeneration();

            // Ortadaki chunk'ın etrafı tamamen dolu: görünür yüzey yok.
            Assert.IsFalse(new SurfaceNetsMesher().Build(world, Vector3Int.one, VoxelSize, palette));
        }

        [Test]
        public void SingleVoxel_ProducesClosedOctahedron()
        {
            var world = WorldWithSingleVoxel(1, 8, 8, 8);
            var mesher = new SurfaceNetsMesher();

            Assert.IsTrue(mesher.Build(world, Vector3Int.zero, VoxelSize, palette));
            Assert.AreEqual(8, mesher.Vertices.Count, "8 hücre köşesi");
            Assert.AreEqual(36, mesher.Triangles.Count, "6 dörtgen × 2 üçgen × 3 indeks");
        }

        [Test]
        public void SingleVoxel_TrianglesAndNormalsFaceOutward()
        {
            var world = WorldWithSingleVoxel(1, 8, 8, 8);
            var mesher = new SurfaceNetsMesher();
            mesher.Build(world, Vector3Int.zero, VoxelSize, palette);

            var center = new Vector3(8.5f, 8.5f, 8.5f) * VoxelSize;

            for (int i = 0; i < mesher.Triangles.Count; i += 3)
            {
                Vector3 a = mesher.Vertices[mesher.Triangles[i]];
                Vector3 b = mesher.Vertices[mesher.Triangles[i + 1]];
                Vector3 c = mesher.Vertices[mesher.Triangles[i + 2]];
                Vector3 faceNormal = Vector3.Cross(b - a, c - a); // Unity ön yüz yönü
                Vector3 outward = (a + b + c) / 3f - center;
                Assert.Greater(Vector3.Dot(faceNormal, outward), 0f, $"Üçgen {i / 3} içe bakıyor");
            }

            for (int i = 0; i < mesher.Vertices.Count; i++)
            {
                Vector3 outward = mesher.Vertices[i] - center;
                Assert.Greater(Vector3.Dot(mesher.Normals[i], outward), 0f, $"Normal {i} içe bakıyor");
            }
        }

        [Test]
        public void SingleVoxel_SurfaceEnclosesVoxelCenter()
        {
            var world = WorldWithSingleVoxel(1, 8, 8, 8);
            var mesher = new SurfaceNetsMesher();
            mesher.Build(world, Vector3Int.zero, VoxelSize, palette);

            var center = new Vector3(8.5f, 8.5f, 8.5f) * VoxelSize;
            foreach (Vector3 v in mesher.Vertices)
            {
                // Tüm köşeler voxel merkezine 1 voxel'den yakın: yüzey bu voxel'i sarar.
                Assert.Less(Vector3.Distance(v, center), VoxelSize);
            }
        }

        [Test]
        public void VoxelOnChunkBorder_IsMeshedExactlyOnceAcrossChunks()
        {
            // x = 16 → chunk 1'in ilk voxel'i; bir yüzü chunk 0'a düşer.
            var world = WorldWithSingleVoxel(2, 16, 8, 8);
            var mesher = new SurfaceNetsMesher();

            int totalTriangles = 0;
            for (int cx = 0; cx < 2; cx++)
            {
                if (mesher.Build(world, new Vector3Int(cx, 0, 0), VoxelSize, palette))
                {
                    totalTriangles += mesher.Triangles.Count;
                }
            }

            Assert.AreEqual(36, totalTriangles, "Sınırdaki voxel eksik veya çift çizildi");
        }

        [Test]
        public void DamagedVoxel_IsDrawnDarker()
        {
            var world = WorldWithSingleVoxel(1, 8, 8, 8);
            var mesher = new SurfaceNetsMesher();

            mesher.Build(world, Vector3Int.zero, VoxelSize, palette);
            int intactBrightness = mesher.Colors[0].r + mesher.Colors[0].g + mesher.Colors[0].b;

            world.SetDamage(8, 8, 8, 2); // taş: 3 HP
            mesher.Build(world, Vector3Int.zero, VoxelSize, palette);
            int damagedBrightness = mesher.Colors[0].r + mesher.Colors[0].g + mesher.Colors[0].b;

            Assert.Less(damagedBrightness, intactBrightness);
        }

        [Test]
        public void VertexColor_UsesMaterialPalette()
        {
            var world = new VoxelWorld(1, 1, 1);
            world.SetGeneratedVoxel(8, 8, 8, VoxelMaterialId.Gold, 255);
            world.FinalizeGeneration();

            var mesher = new SurfaceNetsMesher();
            mesher.Build(world, Vector3Int.zero, VoxelSize, palette);

            Color32 gold = palette.GetColor(VoxelMaterialId.Gold);
            Color32 c = mesher.Colors[0];
            // Hafif ton farkı (±%15) uygulanır; renk ailesi korunmalı (altın: kırmızı > mavi).
            Assert.Greater(c.r, c.b);
            Assert.AreEqual(gold.r > gold.b, c.r > c.b);
        }
    }
}
