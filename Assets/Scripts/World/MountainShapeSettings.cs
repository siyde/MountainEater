using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.World
{
    /// <summary>
    /// Dağın şeklini belirleyen veri. Kod içine dağınık sayı yazmamak için ScriptableObject.
    /// MountainGenerator bu ayarlardan voxel dağı üretir (aynı seed → aynı dağ).
    /// </summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Mountain Shape Settings", fileName = "MountainShapeSettings")]
    public sealed class MountainShapeSettings : ScriptableObject
    {
        [Header("Genel")]
        [Tooltip("Aynı seed her zaman aynı dağı üretir (kayıt sistemi buna dayanacak).")]
        public int seed = 12345;

        [Tooltip("Dağ tabanının dünya birimi cinsinden yarıçapı.")]
        [Min(5f)] public float footprintRadius = 75f;

        [Tooltip("Gürültü öncesi ana zirvenin yaklaşık yüksekliği.")]
        [Min(1f)] public float peakHeight = 60f;

        [Header("Silüet")]
        [Tooltip("Ana zirvenin merkezden kayma oranı (asimetri).")]
        [Range(0f, 1f)] public float peakOffset = 0.35f;

        [Tooltip("İkinci, daha alçak zirvenin gücü.")]
        [Range(0f, 1f)] public float secondaryPeakStrength = 0.55f;

        [Tooltip("1 = düz koni, >1 = geniş taban ve sivri zirve.")]
        [Range(0.5f, 4f)] public float falloffPower = 1.4f;

        [Tooltip("Taban çizgisini düzensizleştiren alan bükme miktarı.")]
        [Range(0f, 0.4f)] public float warpStrength = 0.15f;

        [Header("Yüzey gürültüsü")]
        [Min(0.0001f)] public float noiseScale = 0.03f;
        [Range(1, 8)] public int noiseOctaves = 5;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Min(1f)] public float lacunarity = 2f;
        [Range(0f, 1f)] public float noiseStrength = 0.35f;

        [Tooltip("Sırt / kayalık çıkıntı etkisi.")]
        [Range(0f, 1f)] public float ridgeStrength = 0.3f;

        [Header("3D kaya detayı (voxel dağ)")]
        [Tooltip("Yüzeydeki 3D kaya çıkıntılarının dünya birimi cinsinden yüksekliği.")]
        [Min(0f)] public float rockDetailStrength = 3f;

        [Tooltip("3D kaya gürültüsünün frekansı. strength × scale < 0.3 tutulursa havada asılı parçalar oluşmaz.")]
        [Min(0.0001f)] public float rockDetailScale = 0.05f;

        [Header("Voxel çözünürlüğü")]
        [Tooltip("Bir voxel'in dünya birimi cinsinden kenar uzunluğu. Küçüldükçe voxel sayısı ~1/size³ artar.\n" +
                 "Prototip: 2 (≈ on binlerce voxel). Nihai 1M hedefi için ≈ 0.6-0.7 (Faz 12).")]
        [Min(0.1f)] public float voxelSize = 2f;

        [Header("Madenler (sıra önemli: üstteki önce yer seçer)")]
        [Tooltip("Boş liste = tamamen taş dağ. Oranlar dolu voxel sayısına göredir.")]
        public List<OreLayer> ores = OreLayer.CreateDefaults();

        /// <summary>Inspector: asset'in ⋮ menüsü → maden katmanlarını varsayılana döndürür.</summary>
        [ContextMenu("Reset Ores To Defaults")]
        public void ResetOresToDefaults()
        {
            ores = OreLayer.CreateDefaults();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
