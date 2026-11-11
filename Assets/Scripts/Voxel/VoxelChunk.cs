using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>
    /// Bir chunk'ın sahnedeki temsili: mesh + renderer + collider.
    /// Voxel verisi burada değil VoxelWorld'dedir; bu bileşen yalnızca görüntüyü tutar.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class VoxelChunk : MonoBehaviour
    {
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        public Vector3Int Coord { get; private set; }
        public bool HasMesh { get; private set; }

        public void Initialize(Vector3Int coord, Material material, bool useCollider)
        {
            Coord = coord;

            mesh = new Mesh { name = $"Chunk {coord.x}_{coord.y}_{coord.z}" };
            mesh.MarkDynamic(); // sık güncellenecek

            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;

            if (useCollider)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.enabled = false;
            }
        }

        public void ApplyMesh(SurfaceNetsMesher mesher)
        {
            mesh.Clear();
            mesh.SetVertices(mesher.Vertices);
            mesh.SetNormals(mesher.Normals);
            mesh.SetColors(mesher.Colors);
            mesh.SetUVs(1, mesher.RegionData);
            mesh.SetTriangles(mesher.Triangles, 0, true);

            HasMesh = true;
            meshRenderer.enabled = true;

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // yeniden "cook" edilmesini garanti eder
                meshCollider.sharedMesh = mesh;
                meshCollider.enabled = true;
            }
        }

        public void ClearMesh()
        {
            mesh.Clear();
            HasMesh = false;
            meshRenderer.enabled = false;

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
