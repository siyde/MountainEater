using System;
using System.Collections.Generic;
using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.World
{
    /// <summary>Maden şekli: Blob = yuvarlak kümeler, Vein = ince damarlar/levhalar.</summary>
    public enum OreShape
    {
        Blob = 0,
        Vein = 1,
    }

    /// <summary>
    /// Bir madenin dağ içindeki dağılım kuralı. Dağıtım "oran hedefli"dir: her dolu voxel için bir puan
    /// hesaplanır (gürültü × bölgesel zenginlik + derinlik bonusu) ve uygun voxeller arasından en yüksek
    /// puanlı targetFraction kadarı madene çevrilir. Böylece maden oranı tutturulur, şekli gürültü belirler.
    /// </summary>
    [Serializable]
    public sealed class OreLayer
    {
        public VoxelMaterialId material = VoxelMaterialId.Copper;

        [Tooltip("Dağdaki tüm dolu voxellerin ne kadarı bu maden olsun (0.1 = %10).")]
        [Range(0f, 0.5f)] public float targetFraction = 0.1f;

        [Header("Nerede?")]
        [Tooltip("Yüzeyden en az kaç voxel içeride (1 = yüzeyde görünebilir).")]
        [Min(1)] public int minDepth = 1;

        [Tooltip("Dağın yüksekliğine göre izin verilen aralık (0 = taban, 1 = zirve).")]
        [Range(0f, 1f)] public float minHeight01;
        [Range(0f, 1f)] public float maxHeight01 = 1f;

        [Header("Şekil")]
        public OreShape shape = OreShape.Blob;

        [Tooltip("Gürültü frekansı (dünya birimi). Büyük = küçük ve sık kümeler.")]
        [Min(0.0001f)] public float noiseScale = 0.08f;

        [Header("Yoğunlaşma")]
        [Tooltip("İç kısımları ne kadar tercih etsin (0 = derinlik fark etmez).")]
        [Range(0f, 2f)] public float depthWeight = 0.4f;

        [Tooltip("Derinlik bonusunun tam etkiye ulaştığı mesafe (minDepth'ten itibaren voxel).")]
        [Min(1)] public int depthRange = 10;

        [Tooltip("Bazı bölgelerin daha zengin olması (0 = her yer eşit).")]
        [Range(0f, 1f)] public float richnessStrength = 0.5f;

        [Tooltip("Zengin bölgelerin büyüklüğü (küçük = geniş bölgeler).")]
        [Min(0.0001f)] public float richnessScale = 0.012f;

        public OreLayer()
        {
        }

        public OreLayer(VoxelMaterialId material, float targetFraction, int minDepth, OreShape shape, float noiseScale,
            float depthWeight, int depthRange, float richnessStrength, float maxHeight01 = 1f)
        {
            this.material = material;
            this.targetFraction = targetFraction;
            this.minDepth = minDepth;
            this.shape = shape;
            this.noiseScale = noiseScale;
            this.depthWeight = depthWeight;
            this.depthRange = depthRange;
            this.richnessStrength = richnessStrength;
            this.maxHeight01 = maxHeight01;
        }

        /// <summary>
        /// Varsayılan dağılım (sıra önemli: nadir olan önce yer seçer).
        /// Kristal: derin, küçük kümeler. Altın: iç kısımlarda damarlar. Bakır: yüzeye yakın kümeler.
        /// Kalan ~%84 normal taş.
        /// </summary>
        public static List<OreLayer> CreateDefaults()
        {
            return new List<OreLayer>
            {
                new OreLayer(VoxelMaterialId.Crystal, 0.012f, minDepth: 5, OreShape.Blob, noiseScale: 0.14f,
                    depthWeight: 1.0f, depthRange: 6, richnessStrength: 0.6f, maxHeight01: 0.75f),
                new OreLayer(VoxelMaterialId.Gold, 0.04f, minDepth: 3, OreShape.Vein, noiseScale: 0.05f,
                    depthWeight: 0.6f, depthRange: 8, richnessStrength: 0.6f),
                new OreLayer(VoxelMaterialId.Copper, 0.11f, minDepth: 1, OreShape.Blob, noiseScale: 0.09f,
                    depthWeight: 0.3f, depthRange: 10, richnessStrength: 0.5f),
            };
        }
    }
}
