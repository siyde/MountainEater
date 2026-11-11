using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Regions
{
    /// <summary>Dağın bölge düzeni (bölge listesi + fiyatlar).</summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Region Layout", fileName = "RegionLayout")]
    public sealed class RegionLayout : ScriptableObject
    {
        public List<RegionDefinition> regions = CreateDefaults();

        /// <summary>Inspector ⋮ → varsayılan 3 bölge.</summary>
        [ContextMenu("Reset To Defaults")]
        public void ResetToDefaults()
        {
            regions = CreateDefaults();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Tasarım belgesindeki 3 bölge. Kamera başlangıçta 1. bölgeye bakar.
        /// 1: Ön yamacın dış kabuğu (açık) · 2: Yan yamacın dış kabuğu (1.000 $) · 3: Kalan dağ + çekirdek (6.000 $)
        /// </summary>
        public static List<RegionDefinition> CreateDefaults()
        {
            return new List<RegionDefinition>
            {
                new RegionDefinition(1, "Ön Yamaç", 0, unlockedAtStart: true,
                    angleStart: 175f, angleEnd: 255f, minRadius01: 0.4f),
                new RegionDefinition(2, "Batı Yamacı", 1_000, unlockedAtStart: false,
                    angleStart: 255f, angleEnd: 335f, minRadius01: 0.4f),
                new RegionDefinition(3, "Dağın Kalbi", 6_000, unlockedAtStart: false),
            };
        }
    }
}
