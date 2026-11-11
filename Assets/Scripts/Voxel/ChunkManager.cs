using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>
    /// VoxelWorld'ün görüntüsünü yönetir: chunk nesnelerini oluşturur, değişen chunk'ları kuyruğa alır ve
    /// kare başına sınırlı sayıda yeniden mesh'ler. Ayrıca dünya ↔ voxel koordinat dönüşümlerini sağlar.
    ///
    /// - Hiç yüzeyi olmayan chunk'lar (tamamen hava veya tamamen iç kaya) için GameObject oluşturulmaz;
    ///   iç kaya chunk'ı kazı ile yüzey kazanınca o anda oluşturulur.
    /// - Bir voxel kırılınca yalnızca etkilenen chunk (ve sınırdaysa komşuları) güncellenir.
    /// - Voxel ızgarası XZ'de bu nesnenin konumuna göre ortalanır, Y = 0 zemindir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkManager : MonoBehaviour
    {
        [SerializeField] private Material chunkMaterial;
        [Tooltip("Chunk'lara MeshCollider ekler. Gerekmiyor: kazı (VoxelRaycast) ve kamera (GetSurfaceHeight) " +
                 "doğrudan voxel verisini kullanır. Kapalı tutmak her kazıdaki collider maliyetini ortadan kaldırır.")]
        [SerializeField] private bool generateColliders;

        [Tooltip("Bir karede en fazla kaç chunk yeniden mesh'lenir. Kalanlar sonraki karelere kalır.")]
        [SerializeField, Min(1)] private int maxChunkRebuildsPerFrame = 8;

        private readonly SurfaceNetsMesher mesher = new SurfaceNetsMesher();
        private readonly Queue<int> dirtyQueue = new Queue<int>();

        private VoxelChunk[] chunks;
        private bool[] dirtyFlags;
        private VoxelMaterialTable materialTable;
        private float voxelSize = 1f;

        public VoxelWorld World { get; private set; }
        public float VoxelSize => voxelSize;
        public Bounds ContentBounds { get; private set; }
        public int MeshedChunkCount { get; private set; }
        public int PendingRebuildCount => dirtyQueue.Count;

        /// <summary>Üretilmiş bir dünyayı bağlar ve tüm chunk'ları hemen mesh'ler.</summary>
        public void Initialize(VoxelWorld world, float worldVoxelSize, VoxelMaterialTable table)
        {
            Release();

            World = world;
            voxelSize = worldVoxelSize;
            materialTable = table ?? VoxelMaterialTable.CreateDefault();

            if (chunkMaterial == null)
            {
                Debug.LogWarning("[ChunkManager] Chunk Material atanmamış, URP/Lit kullanılıyor (vertex renkleri görünmez).", this);
                chunkMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            // Izgarayı XZ'de ortala (MountainGenerator da aynı kuralı kullanır).
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(-world.SizeX * voxelSize * 0.5f, 0f, -world.SizeZ * voxelSize * 0.5f);

            chunks = new VoxelChunk[world.ChunkCount];
            dirtyFlags = new bool[world.ChunkCount];
            MeshedChunkCount = 0;

            World.ChunkChanged += MarkDirty;

            for (int i = 0; i < world.ChunkCount; i++)
            {
                RebuildChunk(i);
            }

            Vector3 min = VoxelCornerToWorld(world.ContentMin);
            Vector3 max = VoxelCornerToWorld(world.ContentMax + Vector3Int.one);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            ContentBounds = bounds;

        }

        /// <summary>
        /// Dünya konumunun altındaki arazi yüzeyinin yaklaşık yüksekliği (dünya birimi).
        /// columnRadius kadar komşu sütunun en yükseği alınır (kamera kenarlardan içeri girmesin diye).
        /// Altında kaya yoksa float.NegativeInfinity.
        /// </summary>
        public float GetSurfaceHeight(Vector3 worldPosition, int columnRadius = 1)
        {
            if (World == null) return float.NegativeInfinity;

            Vector3Int center = WorldToVoxel(worldPosition);
            int top = -1;
            for (int dz = -columnRadius; dz <= columnRadius; dz++)
            for (int dx = -columnRadius; dx <= columnRadius; dx++)
            {
                int y = World.GetTopSolidY(center.x + dx, center.z + dz);
                if (y > top) top = y;
            }

            if (top < 0) return float.NegativeInfinity;
            return transform.TransformPoint(new Vector3(0f, (top + 1) * voxelSize, 0f)).y;
        }

        [ContextMenu("Debug: Collider Report")]
        private void DebugColliderReport() => LogColliderHealth("elle");

        /// <summary>
        /// TANI: chunk collider'larının fizik sorgularında görünüp görünmediğini raporlar.
        /// Dağın ortasına yukarıdan aşağı bir ışın atar; hem sahne sorgusu (Physics.Raycast) hem de
        /// her collider'ın kendi Raycast'i denenir. İkisi farklıysa sorun collider'ın sahneye kaydındadır.
        /// </summary>
        private void LogColliderHealth(string when)
        {
            if (chunks == null) return;

            int colliderCount = 0;
            int enabledCount = 0;
            int withMesh = 0;
            int directHits = 0;
            var ray = new Ray(ContentBounds.center + Vector3.up * (ContentBounds.extents.y + 50f), Vector3.down);

            foreach (VoxelChunk chunk in chunks)
            {
                if (chunk == null) continue;
                var meshCollider = chunk.GetComponent<MeshCollider>();
                if (meshCollider == null) continue;
                colliderCount++;
                if (meshCollider.enabled) enabledCount++;
                if (meshCollider.sharedMesh != null && meshCollider.sharedMesh.vertexCount > 0) withMesh++;
                if (meshCollider.Raycast(ray, out _, 1000f)) directHits++;
            }

            string sceneHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore)
                ? $"'{hit.collider.name}' @ {hit.point}"
                : "hiçbir şey";

            Debug.Log($"[ChunkManager] Collider kontrolü ({when}): {colliderCount} collider, {enabledCount} aktif, " +
                      $"{withMesh} mesh'li. Dağ ortasına dikey ışın → sahne sorgusu: {sceneHit}; " +
                      $"doğrudan collider testi: {directHits} isabet.", this);
        }

        private void LateUpdate()
        {
            ProcessDirty(maxChunkRebuildsPerFrame);
        }

        /// <summary>Kuyruktaki bütün chunk'ları hemen günceller (testler / anlık ihtiyaçlar için).</summary>
        public void RebuildDirtyNow() => ProcessDirty(int.MaxValue);

        public void MarkDirty(int chunkIndex)
        {
            if (dirtyFlags == null || dirtyFlags[chunkIndex]) return;
            dirtyFlags[chunkIndex] = true;
            dirtyQueue.Enqueue(chunkIndex);
        }

        // ---------- Koordinat dönüşümleri (Faz 3 kazı sistemi kullanacak) ----------

        /// <summary>Dünya konumunu içeren voxel'in koordinatı.</summary>
        public Vector3Int WorldToVoxel(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) / voxelSize;
            return new Vector3Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y), Mathf.FloorToInt(local.z));
        }

        /// <summary>Dünya konumunu sürekli voxel uzayına çevirir (voxel v'nin merkezi = v + 0.5).</summary>
        public Vector3 WorldToVoxelSpace(Vector3 worldPosition)
        {
            return transform.InverseTransformPoint(worldPosition) / voxelSize;
        }

        public Vector3 WorldToVoxelDirection(Vector3 worldDirection)
        {
            return transform.InverseTransformDirection(worldDirection);
        }

        public Vector3 VoxelSpaceToWorld(Vector3 voxelSpacePoint)
        {
            return transform.TransformPoint(voxelSpacePoint * voxelSize);
        }

        public Vector3 VoxelDirectionToWorld(Vector3 voxelDirection)
        {
            return transform.TransformDirection(voxelDirection);
        }

        public Vector3 VoxelCenterToWorld(Vector3Int voxel)
        {
            return transform.TransformPoint(((Vector3)voxel + new Vector3(0.5f, 0.5f, 0.5f)) * voxelSize);
        }

        public Vector3 VoxelCornerToWorld(Vector3Int voxel)
        {
            return transform.TransformPoint((Vector3)voxel * voxelSize);
        }

        // ---------- İç işler ----------

        private void ProcessDirty(int budget)
        {
            if (World == null) return;

            while (budget-- > 0 && dirtyQueue.Count > 0)
            {
                int index = dirtyQueue.Dequeue();
                dirtyFlags[index] = false;
                RebuildChunk(index);
            }
        }

        private void RebuildChunk(int index)
        {
            Vector3Int coord = World.GetChunkCoord(index);
            bool hasSurface = mesher.Build(World, coord, voxelSize, materialTable);
            VoxelChunk chunk = chunks[index];

            if (hasSurface)
            {
                if (chunk == null)
                {
                    chunk = CreateChunk(coord);
                    chunks[index] = chunk;
                }

                if (!chunk.HasMesh) MeshedChunkCount++;
                chunk.ApplyMesh(mesher);
            }
            else if (chunk != null && chunk.HasMesh)
            {
                chunk.ClearMesh();
                MeshedChunkCount--;
            }
        }

        private VoxelChunk CreateChunk(Vector3Int coord)
        {
            var go = new GameObject($"Chunk {coord.x}_{coord.y}_{coord.z}", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(coord * VoxelWorld.ChunkSize) * voxelSize;

            var chunk = go.AddComponent<VoxelChunk>();
            chunk.Initialize(coord, chunkMaterial, generateColliders);
            return chunk;
        }

        private void Release()
        {
            if (World != null) World.ChunkChanged -= MarkDirty;

            if (chunks != null)
            {
                foreach (VoxelChunk chunk in chunks)
                {
                    if (chunk != null) Destroy(chunk.gameObject);
                }
            }

            chunks = null;
            dirtyFlags = null;
            dirtyQueue.Clear();
            World = null;
            MeshedChunkCount = 0;
        }

        private void OnDestroy()
        {
            if (World != null) World.ChunkChanged -= MarkDirty;
        }

        // ---------- Geliştirme aracı ----------

        /// <summary>
        /// GELİŞTİRME: Play modunda Inspector'daki ⋮ menüsünden çalıştır. Dağın içinden X ekseni boyunca
        /// bir tünel açar; chunk güncellemesini ve chunk sınırlarındaki dikişleri test etmek için.
        /// </summary>
        [ContextMenu("Debug: Carve Test Tunnel")]
        private void DebugCarveTestTunnel()
        {
            if (World == null)
            {
                Debug.LogWarning("[ChunkManager] Önce Play moduna gir.", this);
                return;
            }

            Vector3Int min = World.ContentMin;
            Vector3Int max = World.ContentMax;
            int centerY = min.y + (max.y - min.y) / 4;
            int centerZ = (min.z + max.z) / 2;
            const int radius = 3;
            int removed = 0;

            for (int x = min.x; x <= max.x; x++)
            for (int dy = -radius; dy <= radius; dy++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (dy * dy + dz * dz > radius * radius) continue;
                if (World.RemoveVoxel(x, centerY + dy, centerZ + dz)) removed++;
            }

            Debug.Log($"[ChunkManager] Test tüneli: {removed} voxel silindi. Kalan: {World.SolidCount:N0}", this);
        }
    }
}
