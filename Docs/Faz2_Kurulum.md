# Mountain Eater 3D — FAZ 2: Voxel Dünya Sistemi

## 1. Bu fazda ne yapıyoruz?

Faz 1'deki geçici yükseklik haritası dağını, **gerçek voxel verisinden** üretilen dağ ile değiştiriyoruz:

- Kompakt voxel verisi (`VoxelWorld`) — her voxel için malzeme, density, bölge, hasar
- 16×16×16 chunk sistemi (`ChunkManager`, `VoxelChunk`)
- Seed'li prosedürel voxel dağ (`MountainGenerator`) — 2D şekil + 3D kaya detayı
- **Surface Nets** ile yumuşak, küp görünmeyen chunk mesh'leri (`SurfaceNetsMesher`)
- Malzeme tablosu (`VoxelMaterialDatabase`) — Taş / Bakır / Altın / Kristal (HP, ödül, renk)
- Yalnızca değişen chunk'ları güncelleyen kirli (dirty) kuyruk
- 21 yeni birim testi

Her voxel için GameObject **yok**: dağ, yüzeyi olan birkaç düzine chunk mesh'inden oluşur.

---

## 2. Algoritma seçimi: neden Surface Nets?

| | Küp (Minecraft) | Marching Cubes | **Surface Nets (seçilen)** |
|---|---|---|---|
| Görünüm | Bloklu | Yumuşak | Yumuşak |
| Kod | Çok basit | 256 durumlu tablolar, belirsiz durumlar | Kısa, tablosuz |
| Üçgen sayısı | Yüksek | Yüksek (hücre başına ≤5) | **Düşük** (hücre başına 1 köşe, köşeler paylaşılır) |
| Chunk dikişleri | Kolay | Dikkat ister | Kolay (aynı hücre köşesi iki chunk'ta aynı hesaplanır) |
| Voxel kırma ile tutarlılık | Tam | Tam | **Tam** |

Surface Nets'te her "karışık" hücreye (köşelerinde hem dolu hem boş voxel olan) tek bir köşe konur;
dolu↔boş geçen her voxel komşuluğu için bir dörtgen çizilir. Köşenin hücre içindeki konumu **density**
ile belirlenir: üretimde density yüzeyin voxel merkezleri arasındaki gerçek konumunu taşır, bu yüzden
ilk dağ basamaksız ve doğal görünür. Kırılan voxel'in density'si 0 olur → orada yüzey içeri çekilir
ve tam bir voxel boyutunda oyuk açılır.

**Tutarlılık garantisi:** dolu ↔ `density >= 128` kuralı veri katmanında zorlanır (`SetGeneratedVoxel`).
Böylece her dolu voxel merkezi yüzeyin içinde, her boş voxel merkezi dışında kalır. Görüntü asla veriden
sapmaz; Faz 3'te tıkladığın yüzeyin arkasında gerçekten kırılabilir bir voxel vardır.

---

## 3. Veri yapısı

```
VoxelWorld (saf C#)
  materials : byte[]    0 = Air → "dolu mu?" buradan
  densities : byte[]    yüzey konumu (görsel)
  regions   : byte[]    bölge kimliği (Faz 6)
  damage    : ushort[]  alınan hasar, 0 = sağlam (Faz 3)   → kalan HP = malzeme HP - damage
  SolidCount / InitialSolidCount  → ilerleme sayacı (Faz 8)
  event ChunkChanged(int)          → ChunkManager dinler
```

- **Voxel başına 5 byte.** HP ve ödül voxel'de saklanmaz, malzeme tablosundan türetilir.
- Diziler dünya boyunca tek parça; chunk'lar 16³'lük aralıklarına erişir. Sınır komşuluğu basit kalır,
  Faz 9 kaydı tek bir bitset ile yapılabilir. 1M voxellik dağ için ≈35 MB — PC için sorun değil.
- `RemoveVoxel` zaten boş voxel için **false** döner → aynı voxel iki kez sayılamaz / ödül veremez
  (Faz 3'te ödül bu dönüş değerine bağlanacak).
- Sınırdaki (yerel 0 veya 15) bir voxel değişince komşu chunk'lar da güncellenir (testli).

### Bağımlılık yönü
```
Core (GameBootstrap) ──► World (MountainGenerator) ──► Voxel (VoxelWorld, ChunkManager, Mesher)
                    └──► Cameras
```
Voxel katmanı dağ üretimini bilmez; Faz 9'da kayıttan yüklenen dünya da aynı `ChunkManager.Initialize`
ile bağlanabilir.

---

## 4. Değişen / eklenen dosyalar

**Yeni**
```
Assets/Scripts/Voxel/
  VoxelMaterialId.cs          malzeme kimlikleri (byte enum; sayıları değiştirme, kayıt kullanacak)
  VoxelMaterialDatabase.cs    malzeme tablosu (ScriptableObject)
  VoxelWorld.cs               voxel verisi
  SurfaceNetsMesher.cs        chunk mesh üretimi (GC üretmez)
  VoxelChunk.cs               chunk'ın mesh/renderer/collider'ı
  ChunkManager.cs             chunk oluşturma, dirty kuyruk, koordinat dönüşümleri
Assets/Scripts/World/
  GradientNoise3D.cs          seed'li 3D Perlin gürültüsü
  MountainGenerator.cs        voxel dağ üretimi
Assets/Tests/EditMode/
  VoxelWorldTests.cs, SurfaceNetsMesherTests.cs, MountainGeneratorTests.cs
```

**Değişen**
- `MountainShapeSettings.cs` → yeni alanlar: `rockDetailStrength`, `rockDetailScale`, `voxelSize`
- `MountainShape.cs` → `SampleRockDetail(x, y, z)` eklendi (2D yükseklikler değişmedi, eski testler geçerli)
- `GameBootstrap.cs` → artık voxel dağı üretiyor (alanlar değişti!)
- `DebugOverlay.cs` → voxel ve chunk istatistikleri
- `MountainEaterSceneSetup.cs` → menü adı **Mountain Eater > Setup Main Scene**

**Silinen**
- `PlaceholderMountainGenerator.cs` (Faz 1 geçici dağı)

> ⚠ **Kırıcı değişiklik:** Mevcut `Main.unity` içindeki *Mountain* nesnesinin script'i silindi ve
> `GameBootstrap` alanları değişti. Sahneyi aşağıdaki gibi **yeniden kur**.

---

## 5. Unity Editor'da kurulum

### A) Otomatik (önerilen)
1. Unity'ye geç, derlemeyi bekle (Console temiz olmalı).
2. **Mountain Eater > Setup Main Scene** → "Üzerine yaz".
   - Yeni: `Assets/ScriptableObjects/Voxel/VoxelMaterialDatabase.asset`
   - Sahnede *Mountain* yerine **VoxelMountain** (ChunkManager)
   - GameBootstrap: Mountain Settings, Material Database, Chunk Manager, Orbit Camera bağlı
3. **Play**.

### B) Elle kurulum (A'yı yaptıysan gerekmez)
1. Eski *Mountain* nesnesini sil.
2. `Create > Mountain Eater > Voxel Material Database` → `Assets/ScriptableObjects/Voxel/`.
3. Boş GameObject **VoxelMountain** (0,0,0) → `ChunkManager` ekle → Chunk Material: `M_Mountain`.
4. **GameBootstrap** → Mountain Settings: `MountainShapeSettings`, Material Database, Chunk Manager:
   VoxelMountain, Orbit Camera: Main Camera.
5. **DebugOverlay** → Chunk Manager: VoxelMountain.

### Inspector değerleri

**MountainShapeSettings**
| Alan | Değer | Not |
|---|---|---|
| Footprint Radius | 75 | |
| Peak Height | 60 | |
| Rock Detail Strength | 3 | yüzey çıkıntıları (m) |
| Rock Detail Scale | 0.05 | strength × scale < 0.3 tut (havada kaya oluşmasın) |
| **Voxel Size** | **2** | prototip: on binlerce voxel. Faz 12'de ≈0.6–0.7 → ~1M |

Voxel sayısı yaklaşık `1 / voxelSize³` ile ölçeklenir. Gerçek sayı Play'de Console'a yazılır:
`[Mountain Eater] Dağ hazır: 41.237 dolu voxel, ızgara 96×48×96 ...` (örnek biçim).
Hedef aralık 20.000–50.000. Dışındaysa `Voxel Size`'ı biraz değiştir (2.2 → daha az, 1.8 → daha çok).

**ChunkManager**
| Alan | Değer |
|---|---|
| Chunk Material | M_Mountain |
| Generate Colliders | ✔ (Faz 3 raycast + kamera arazi koruması) |
| Max Chunk Rebuilds Per Frame | 8 |

---

## 6. Test

### Otomatik
`Window > General > Test Runner > EditMode > Run All` → **36 test** (15 eski + 21 yeni) yeşil olmalı.

| Test grubu | Neyi kanıtlıyor |
|---|---|
| VoxelWorld | sınırlar, sayaç, density↔malzeme tutarlılığı, **tek sefer silme**, sınırda komşu chunk bildirimi |
| SurfaceNetsMesher | boş/tam dolu chunk mesh üretmez, tek voxel = kapalı 8 köşe/12 üçgen, üçgen ve normaller dışa bakar, **chunk sınırında voxel tam bir kez çizilir**, malzeme rengi |
| MountainGenerator | deterministik, `InitialSolidCount` gerçek veriyle eşleşir, dağ ızgaraya taşmaz, **havada asılı voxel yok** |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | Play | Gri, yumuşak, doğal dağ. Küp görünmüyor. Console'da "Dağ hazır" satırı |
| 2 | Hierarchy > VoxelMountain | Altında `Chunk x_y_z` nesneleri (yalnızca yüzeyi olanlar). Voxel başına nesne YOK |
| 3 | Scene view → Wireframe (Shading Mode) | Chunk'lar arasında boşluk/çatlak yok |
| 4 | DebugOverlay | `Voxel: N / N`, chunk sayısı, FPS |
| 5 | Play'deyken VoxelMountain → ChunkManager ⋮ → **Debug: Carve Test Tunnel** | Dağın içinden geçen bir tünel açılır; birden fazla chunk'tan geçtiği halde dikişsiz; Voxel sayacı azalır |
| 6 | Tünel sonrası kamerayla incele | Tünel ağzı yuvarlak, iç yüzeyler aydınlatılmış, chunk sınırlarında ışık kırılması yok |
| 7 | Seed değiştir + Play | Farklı dağ, voxel sayısı değişir |
| 8 | Kamera dağın etrafında | Faz 1'deki gibi akıcı; kamera dağ yüzeyinin altına girmiyor |

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `The referenced script on this Behaviour (Game Object 'Mountain') is missing!` | Eski sahne. **Setup Main Scene** ile yeniden kur. |
| `[GameBootstrap] '...' atanmamış.` | Eski sahne veya elle kurulumda eksik alan. |
| Dağ pembe/magenta lekeli | Malzeme tablosunda tanımsız bir malzeme kimliği; VoxelMaterialDatabase listesini kontrol et. |
| Dağ tek renk, gölgesiz, gri-beyaz | Chunk Material atanmamış (URP/Lit fallback). M_Mountain ata. |
| Voxel sayısı çok yüksek, açılış yavaş | Voxel Size küçük. Prototipte 2 kullan. |
| Chunk sınırlarında ince çizgiler | Olmaması gerekir (testli). Görürsen ekran görüntüsü + seed gönder. |
| Kamera dağa çok yakın / uzak başlıyor | Dağ boyutu değişti; `FrameBounds` otomatik ayarlar. F ile sıfırla. |

---

## 8. Performans notları (ölçülecek, varsayılmayacak)

- Mesher tamponları yeniden kullanılır → kazı sırasında GC yok.
- Tamamen iç (yüzeysiz) ve tamamen boş chunk'lar için nesne/mesh yok.
- Bir voxel değişince 1–8 chunk yeniden mesh'lenir (16³ chunk ≈ birkaç yüz mikro saniye beklenir).
- Collider her güncellemede yeniden "cook" edilir; çok sayıda makinede (Faz 7) ölçüp gerekirse
  collider güncellemesini geciktireceğiz.
- 1M voxele geçişte (Faz 12) ilk üretim için Job System/Burst değerlendirilecek.

Profiler ile ölç: `Window > Analysis > Profiler`, Play'de Carve Test Tunnel çalıştır, `ChunkManager.LateUpdate`
süresine bak.

---

## 9. FAZ 2 Kabul Kriterleri

- [ ] Console'da derleme/shader hatası yok
- [ ] Dağ voxel verisinden üretiliyor, voxel başına GameObject yok
- [ ] Gerçek dolu voxel sayısı Console'da ve overlay'de; prototip aralığında (20k–50k)
- [ ] Yüzey yumuşak; küp görünmüyor; chunk dikişi yok
- [ ] Aynı seed → aynı dağ; farklı seed → farklı dağ
- [ ] Test tüneli: yalnızca etkilenen chunk'lar güncelleniyor, mesh ve collider tutarlı, sayaç azalıyor
- [ ] Kamera Faz 1'deki gibi çalışıyor
- [ ] 36 EditMode testi geçiyor

Hepsi tamamsa **Faz 3: Gerçek kazı** (tıklama → raycast → voxel hasarı → kırılma → mesh güncelleme).
