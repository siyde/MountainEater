using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Voxel
{
    /// <summary>
    /// Malzeme kimliğiyle (byte) doğrudan indekslenen hızlı arama tablosu: renk, maksimum HP, ödül.
    /// VoxelMaterialDatabase (Inspector'da düzenlenen veri) → çalışma zamanında bu tabloya çevrilir.
    /// Saf C#: mesher ve kazı sistemi testlerde ScriptableObject olmadan kullanabilir.
    /// </summary>
    public sealed class VoxelMaterialTable
    {
        private const int Capacity = 256;
        private static readonly Color32 UndefinedColor = new Color32(255, 0, 255, 255);

        private readonly Color32[] colors = new Color32[Capacity];
        private readonly int[] maxHealth = new int[Capacity];
        private readonly int[] rewards = new int[Capacity];
        private readonly byte[] smoothness = new byte[Capacity];

        public VoxelMaterialTable(IEnumerable<VoxelMaterialDefinition> definitions)
        {
            for (int i = 0; i < Capacity; i++)
            {
                colors[i] = UndefinedColor;
                maxHealth[i] = 1;
                rewards[i] = 0;
            }

            maxHealth[(byte)VoxelMaterialId.Air] = 0;

            if (definitions == null) return;

            foreach (VoxelMaterialDefinition definition in definitions)
            {
                if (definition == null || definition.id == VoxelMaterialId.Air) continue;
                byte id = (byte)definition.id;
                colors[id] = definition.color;
                maxHealth[id] = Mathf.Clamp(definition.maxHealth, 1, ushort.MaxValue);
                rewards[id] = Mathf.Max(0, definition.reward);
                smoothness[id] = (byte)Mathf.RoundToInt(Mathf.Clamp01(definition.smoothness) * 255f);
            }
        }

        /// <summary>Parlaklık 0-255 (mesh köşe renginin alfa kanalına yazılır).</summary>
        public byte GetSmoothness(byte materialId) => smoothness[materialId];

        /// <summary>Tasarım belgesindeki varsayılan değerlerle tablo (testler ve yedek).</summary>
        public static VoxelMaterialTable CreateDefault()
        {
            return new VoxelMaterialTable(VoxelMaterialDatabase.CreateDefaultDefinitions());
        }

        public Color32 GetColor(byte materialId) => colors[materialId];
        public Color32 GetColor(VoxelMaterialId materialId) => colors[(byte)materialId];

        public int GetMaxHealth(byte materialId) => maxHealth[materialId];
        public int GetMaxHealth(VoxelMaterialId materialId) => maxHealth[(byte)materialId];

        public int GetReward(byte materialId) => rewards[materialId];
        public int GetReward(VoxelMaterialId materialId) => rewards[(byte)materialId];
    }
}
