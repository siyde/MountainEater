namespace MountainEater.Upgrades
{
    /// <summary>
    /// Geliştirme türleri. Sayısal değerleri DEĞİŞTİRME: kayıt dosyası (Faz 9) bunları kullanacak.
    /// Yeni geliştirme eklerken sona yeni bir sayı ver.
    /// </summary>
    public enum UpgradeType : byte
    {
        PickaxeDamage = 0,
        MiningRadius = 1,
        DrillDamage = 2,
        DrillSpeed = 3,
    }
}
