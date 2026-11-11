namespace MountainEater.Voxel
{
    /// <summary>
    /// Voxel malzeme kimliği. Voxel başına 1 byte olarak saklanır.
    /// 0 = Air (boş / kırılmış). Değerleri DEĞİŞTİRME: kayıt dosyaları bu sayıları kullanacak.
    /// Yeni malzeme eklerken sona yeni bir sayı ver.
    /// </summary>
    public enum VoxelMaterialId : byte
    {
        Air = 0,
        Stone = 1,
        Copper = 2,
        Gold = 3,
        Crystal = 4,
    }
}
