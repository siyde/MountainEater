namespace MountainEater.Machines
{
    /// <summary>
    /// Makine türleri. Sayısal değerleri DEĞİŞTİRME (kayıt dosyası kullanacak); yeni türü sona ekle.
    /// </summary>
    public enum MachineType : byte
    {
        MiniDrill = 0,
        // MultiDrill = 1, AreaDrill = 2, Excavator = 3  (ileride)
    }

    /// <summary>Makinenin çalışma durumu.</summary>
    public enum MachineState : byte
    {
        Working = 0,

        /// <summary>Çalışma alanında kazılabilir kaya kalmadı; taşınana kadar durur.</summary>
        NoTarget = 1,
    }
}
