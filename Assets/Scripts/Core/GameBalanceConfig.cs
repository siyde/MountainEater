using System.Collections.Generic;
using MountainEater.Machines;
using MountainEater.Upgrades;
using UnityEngine;

namespace MountainEater.Core
{
    /// <summary>
    /// Ekonominin merkezi yapılandırması: başlangıç parası, ödül çarpanı ve geliştirme tanımları.
    /// Dengeleme değerleri kodda değil burada (ve bağlı UpgradeDefinition asset'lerinde) durur.
    /// Faz 7'de makine fiyatları, Faz 6'da bölge fiyatları da buraya eklenecek.
    /// </summary>
    [CreateAssetMenu(menuName = "Mountain Eater/Game Balance Config", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("Ekonomi")]
        [Min(0)] public long startingMoney;

        [Tooltip("Maden ödülü çarpanı (dengeleme testleri için).")]
        [Min(0f)] public float rewardMultiplier = 1f;

        [Header("Geliştirmeler (arayüzdeki sırayla)")]
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        [Header("Makineler (mağazadaki sırayla)")]
        public List<MachineDefinition> machines = new List<MachineDefinition>();
    }
}
