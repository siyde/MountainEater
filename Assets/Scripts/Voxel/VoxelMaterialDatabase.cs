using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>Tek bir malzemenin oyun verisi.</summary>
    [Serializable]
    public sealed class VoxelMaterialDefinition
    {
        public VoxelMaterialId id = VoxelMaterialId.Stone;
        public string displayName = "Yeni Malzeme";
        public Color color = Color.gray;

        [Tooltip("Parlaklık (0 = mat taş, 1 = çok parlak kristal). Köşe renginin alfa kanalında shader'a gider.")]
        [Range(0f, 1f)] public float smoothness = 0.05f;

        [Tooltip("Kırılması için gereken toplam hasar.")]
        [Min(1)] public int maxHealth = 1;

        [Tooltip("Tamamen kırıldığında kazanılan para.")]
        [Min(0)] public int reward;

        /// <summary>Inspector'da listeye eleman eklenirken kullanılır.</summary>
        public VoxelMaterialDefinition()
        {
        }

        public VoxelMaterialDefinition(VoxelMaterialId id, string displayName, Color color, float smoothness,
            int maxHealth, int reward)
        {
            this.id = id;
            this.displayName = displayName;
            this.color = color;
            this.smoothness = smoothness;
            this.maxHealth = maxHealth;
            this.reward = reward;
        }
    }

    /// <summary>
    /// Malzemelerin merkezi tablosu (renk, parlaklık, dayanıklılık, ödül).
    /// Voxel'in kendisi yalnızca malzeme kimliğini saklar; HP ve ödül buradan türetilir.
    /// </summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Voxel Material Database", fileName = "VoxelMaterialDatabase")]
    public sealed class VoxelMaterialDatabase : ScriptableObject
    {
        [SerializeField] private List<VoxelMaterialDefinition> materials = CreateDefaultDefinitions();

        public IReadOnlyList<VoxelMaterialDefinition> Materials => materials;

        public bool TryGet(VoxelMaterialId id, out VoxelMaterialDefinition definition)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                if (materials[i].id == id)
                {
                    definition = materials[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>Çalışma zamanı arama tablosunu oluşturur.</summary>
        public VoxelMaterialTable CreateTable() => new VoxelMaterialTable(materials);

        /// <summary>Inspector: asset'in ⋮ menüsü → tasarım belgesindeki değerlere döndürür.</summary>
        [ContextMenu("Reset To Design Defaults")]
        public void ResetToDesignDefaults()
        {
            materials = CreateDefaultDefinitions();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Tasarım belgesindeki başlangıç değerleri.</summary>
        public static List<VoxelMaterialDefinition> CreateDefaultDefinitions()
        {
            return new List<VoxelMaterialDefinition>
            {
                new VoxelMaterialDefinition(VoxelMaterialId.Stone, "Normal Taş", new Color(0.50f, 0.48f, 0.45f), 0.05f, 3, 5),
                new VoxelMaterialDefinition(VoxelMaterialId.Copper, "Bakır", new Color(0.78f, 0.42f, 0.20f), 0.45f, 7, 15),
                new VoxelMaterialDefinition(VoxelMaterialId.Gold, "Altın", new Color(1.00f, 0.80f, 0.18f), 0.70f, 18, 45),
                new VoxelMaterialDefinition(VoxelMaterialId.Crystal, "Kristal", new Color(0.42f, 0.88f, 1.00f), 0.90f, 45, 120),
            };
        }
    }
}
