using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountainEater.Regions
{
    /// <summary>
    /// Bir kazı bölgesinin tanımı. Prototip hacim: dağ merkezine göre
    ///   açı aralığı (pusula: 0° = +Z, 90° = +X) × yükseklik aralığı (0 taban, 1 zirve) × yarıçap aralığı (0 merkez, 1 dış kenar).
    /// Bölgeler listede SIRAYLA denenir; voxel ilk uyan bölgeye aittir (öncelik). Son bölgeyi tam aralıklı
    /// bırakmak "kalan her şey" anlamına gelir. Hiçbir bölgeye uymayan voxel kimlik 0 alır (sahipsiz → kilitli).
    /// İleride düzensiz bölgeler için yalnızca RegionAssigner'daki eşleştirme değişir.
    /// </summary>
    [Serializable]
    public sealed class RegionDefinition
    {
        [Tooltip("Benzersiz kimlik (1-63). Kayıt dosyası kullanır; sonradan DEĞİŞTİRME. 0 = sahipsiz.")]
        [Range(1, 63)] public int id = 1;

        public string displayName = "Bölge";

        [Tooltip("Açma fiyatı.")]
        [Min(0)] public long price;

        public bool unlockedAtStart;

        [Header("Hacim")]
        [Range(0f, 360f)] public float angleStart;
        [Range(0f, 360f)] public float angleEnd = 360f;
        [Range(0f, 1f)] public float minHeight01;
        [Range(0f, 1f)] public float maxHeight01 = 1f;
        [Range(0f, 1f)] public float minRadius01;
        [Range(0f, 1f)] public float maxRadius01 = 1f;

        public RegionDefinition()
        {
        }

        public RegionDefinition(int id, string displayName, long price, bool unlockedAtStart,
            float angleStart = 0f, float angleEnd = 360f, float minHeight01 = 0f, float maxHeight01 = 1f,
            float minRadius01 = 0f, float maxRadius01 = 1f)
        {
            this.id = id;
            this.displayName = displayName;
            this.price = price;
            this.unlockedAtStart = unlockedAtStart;
            this.angleStart = angleStart;
            this.angleEnd = angleEnd;
            this.minHeight01 = minHeight01;
            this.maxHeight01 = maxHeight01;
            this.minRadius01 = minRadius01;
            this.maxRadius01 = maxRadius01;
        }

        /// <summary>Açı aralığı 360°'yi kapsıyorsa veya bitiş &lt; başlangıçsa (ör. 300 → 60) sarmalı.</summary>
        public bool ContainsAngle(float angle)
        {
            float span = angleEnd - angleStart;
            if (span >= 360f || Mathf.Approximately(span, 360f)) return true;

            angle = Mathf.Repeat(angle, 360f);
            float start = Mathf.Repeat(angleStart, 360f);
            float end = Mathf.Repeat(angleEnd, 360f);
            return start <= end ? angle >= start && angle < end : angle >= start || angle < end;
        }

        public bool Contains(float angle, float height01, float radius01)
        {
            return ContainsAngle(angle) &&
                   height01 >= minHeight01 && height01 <= maxHeight01 &&
                   radius01 >= minRadius01 && radius01 <= maxRadius01;
        }

        /// <summary>Açı aralığının ortası (kameranın bölgeye bakması için).</summary>
        public float CenterAngle
        {
            get
            {
                float span = angleEnd - angleStart;
                if (span < 0f) span += 360f;
                return Mathf.Repeat(angleStart + span * 0.5f, 360f);
            }
        }
    }
}
