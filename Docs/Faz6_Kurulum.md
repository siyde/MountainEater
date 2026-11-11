# Mountain Eater 3D — FAZ 6: Kazı Bölgeleri

## 1. Bu fazda ne yapıyoruz?

- Dağ mantıksal **bölgelere** ayrılır; oyuncu başta yalnızca küçük bir bölgede kazabilir.
- Para ile yeni bölgeler satın alınır.
- **Bölge yönetimi modu** (`R` veya alttaki **Regions** düğmesi): dağ bölge renkleriyle görünür,
  dağa tıklayınca bölge seçilir, sağ panelde fiyat ve "Bölgeyi aç" düğmesi.
- Kilitli bölgeye normal modda tıklanırsa "Kilitli: Batı Yamacı · 1.000 $" yazısı çıkar.

| # | Bölge | Fiyat | Hacim |
|---|---|---:|---|
| 1 | Ön Yamaç | açık | 175°–255° yönü, dış kabuk (merkezden %40 dışarısı) |
| 2 | Batı Yamacı | 1.000 $ | 255°–335° yönü, dış kabuk |
| 3 | Dağın Kalbi | 6.000 $ | geri kalan her şey (diğer yamaçlar + kristalce zengin çekirdek) |

Kamera başlangıçta otomatik olarak 1. bölgeye bakar.

---

## 2. Mimari kararlar

**Bölge = voxel verisinde 1 byte.** Faz 2'den beri her voxelde `region` alanı vardı. Artık
`RegionAssigner` üretimden sonra her dolu voxele bölge kimliği yazıyor. Sınırın tek doğruluk kaynağı
bu veri: oyuncu, makineler (Faz 7), görünüm ve kayıt (Faz 9) aynı sınırı kullanır; tutarsızlık olamaz.

**Kilit kontrolü tek yerde.** `MiningSystem.IsRegionUnlocked = RegionManager.IsUnlocked`. Faz 3'ten beri
küredeki her voxel tek tek kontrol ediliyor → kazı yarıçapı kilitli bölgeye **taşamaz** (testli).
Kontrol bir dizi araması: vuruş başına ek maliyet yok.

**Bölge hacimleri (prototip):** açı × yükseklik × yarıçap aralıkları; liste sırasıyla ilk uyan bölge kazanır,
son bölge "kalan her şey". 20–30 bölgeye çıkmak = `RegionLayout` asset'ine satır eklemek. Düzensiz,
dağın şekline uyan bölgeler istediğimizde yalnızca `RegionAssigner`'daki eşleştirme değişecek.

**Görünüm sıfır yeniden-mesh ile.** Mesher her köşeye UV2 olarak (bölge kimliği, sınır mı) yazar.
Shader global bir renk dizisinden bölge rengini alır:
- Bölge kimliği `nointerpolation` → her üçgen tek bölge rengi, net sınırlar.
- Sınır bayrağı enterpolasyonlu → bölgeler arasında açık renkli çizgi.
- Mod açma/kapama ve satın alma yalnızca shader global değerlerini değiştirir; hiçbir chunk yeniden
  mesh'lenmez (1M voxellik dağda da anlık).

| Renk | Anlam |
|---|---|
| Yeşil | açık bölge |
| Turuncu | kilitli, paran yetiyor |
| Kırmızı | kilitli, paran yetmiyor |
| Gri | hiçbir bölgeye ait değil |
| Daha parlak | seçili bölge |

**VoxelPicker.** "Ekran noktası → voxel" mantığı PlayerMiningController'dan ortak bir sınıfa taşındı;
kazı, bölge seçimi ve Faz 7'de makine yerleştirme aynı kodu kullanır.

**Modlar.** Bölge modu açıkken sol tık kazmaz (`PlayerMiningController.MiningEnabled`). Faz 7'deki
yerleştirme modu da aynı bayrağı kullanacak.

**Ekonomi kuralı kontrolü.** Açılışta Console'a: "açık bölgelerin değeri X $, en ucuz kilitli bölge Y $".
X < Y ise uyarı verilir (tasarım belgesi: ilk bölge sonrakini açmaya yetmeli). Ayrıca testli.

---

## 3. Dosyalar

**Yeni**
```
Assets/Scripts/Regions/RegionDefinition.cs        bölge tanımı (hacim, fiyat)
Assets/Scripts/Regions/RegionLayout.cs            bölge listesi (ScriptableObject) + varsayılan 3 bölge
Assets/Scripts/Regions/RegionAssigner.cs          voxellere bölge yazma + istatistik (voxel, değer, merkez)
Assets/Scripts/Regions/RegionManager.cs           kilit, satın alma, olaylar
Assets/Scripts/Regions/RegionOverlayController.cs bölge modu, renkler, tıklayarak seçim
Assets/Scripts/Mining/VoxelPicker.cs              ekran → voxel (ortak)
Assets/Tests/EditMode/RegionTests.cs
```

**Değişen**
| Dosya | Değişiklik |
|---|---|
| `VoxelWorld.cs` | `GetRegionAt`, `SetRegionAt` |
| `SurfaceNetsMesher.cs`, `VoxelChunk.cs` | UV2 = bölge kimliği + sınır |
| `VertexColorLit.shader` | bölge görünümü (global renk dizisi) |
| `PlayerMiningController.cs` | VoxelPicker'ı kullanır; `MiningEnabled`; `LockedTargetClicked` |
| `OrbitCameraController.cs` | `SetStartYaw` |
| `HudController.cs`, `MainHud.uxml/.uss` | bölge paneli, alt çubuk, "Kilitli" yazısı |
| `GameBootstrap.cs` | bölge atama → mesh → RegionManager → kilit kancası; yeni alanlar **Region Layout**, **Region Overlay** |
| `MountainEaterSceneSetup.cs` | RegionLayout asset'i + Regions nesnesi |

> ⚠ **Kırıcı değişiklik:** GameBootstrap'e zorunlu **Region Layout** alanı eklendi →
> **Mountain Eater > Setup Main Scene** ile sahneyi yeniden kur. (Asset'lerin korunur; Starting Money dahil.)

---

## 4. Unity Editor'da kurulum

1. Unity'ye geç, derlemeyi bekle (Console temiz).
2. **Mountain Eater > Setup Main Scene** → "Üzerine yaz".
3. Test için `GameBalanceConfig` → Starting Money'i 0 yap (bölge ekonomisini gerçek haliyle görmek için).
4. **Play**.

### Inspector: RegionLayout (`Assets/ScriptableObjects/Regions/RegionLayout.asset`)
| Alan | Anlamı |
|---|---|
| Id | 1–63, benzersiz; sonradan değiştirme (kayıt kullanacak) |
| Display Name, Price, Unlocked At Start | |
| Angle Start / End | pusula açısı: 0° = +Z, 90° = +X; bitiş < başlangıç ise sarmalı (ör. 300 → 60) |
| Min/Max Height01 | 0 taban, 1 zirve |
| Min/Max Radius01 | 0 merkez, 1 dış kenar |

Sıra önemli: üstteki bölge önce seçer. ⋮ → **Reset To Defaults** ile 3 varsayılan bölgeye döner.

---

## 5. Test

### Otomatik
`Test Runner > EditMode > Run All` → **122 test** (104 eski + 18 yeni).

| Test | Kanıtladığı |
|---|---|
| `MiningRespectsRegionLocks_UntilPurchased` | yarıçaplı vuruş kilitli komşuya taşmıyor; makine de kazamıyor; satın alınca kazılıyor |
| `Purchase_RequiresMoney_DeductsPrice_UnlocksOnce` | 999 $ ile alınamaz, 1.000 $ ile alınır, ikinci kez alınamaz |
| `DefaultLayout_FirstRegionPaysForSecond…` | gerçek dağda 1. bölge 2. bölgeyi açmaya yetiyor; sahipsiz voxel yok; başlangıç bölgesi küçük |
| `FirstMatchingRegionWins…`, `UncoveredVoxels…`, `RadiusRange…` | bölge hacimleri doğru |
| `Mesher_WritesRegionIdAndBorderFlag` | görünüm verisi mesh'te |
| `ContainsAngle` (6 durum) | sarmalı açı aralıkları |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | Play | Kamera 1. bölgeye (Ön Yamaç) bakıyor. Console: bölge değerleri satırı (uyarı yok) |
| 2 | Ön yamacı kaz | Normal çalışıyor |
| 3 | Yan yamaca tıkla | Kazmıyor; turuncu "Kilitli: Batı Yamacı · 1.000 $" yazısı |
| 4 | Yarıçap ≥1 ile bölge sınırına tıkla | Oyuk sınırda kesiliyor; kilitli tarafa taşmıyor |
| 5 | `R` veya **Regions (R)** | Dağ renklenir: ön yamaç yeşil, diğerleri kırmızı (para yok); sınırlarda açık çizgi; sağ panel "BÖLGELER" |
| 6 | Bölge modunda dağa tıkla | Tıklanan bölge seçilir (beyaz çerçeve + daha parlak renk); kazı olmaz |
| 7 | Paneldeki bir satıra tıkla | Kamera o bölgenin ortasına kayar |
| 8 | 1.000 $ biriktir | Batı Yamacı turuncuya döner, "Bölgeyi aç" aktif |
| 9 | Satın al | Para 1.000 düşer, bölge yeşil, "Açık"; artık kazılabiliyor |
| 10 | `Esc` veya `R` | Normal görünüm, sağda yükseltmeler, sol tık yine kazar |
| 11 | FPS | Mod açıp kapatırken takılma yok (yeniden mesh yok) |

---

## 6. Bilinen sınırlamalar

- **Satın alımlar kalıcı değil:** Play'den çıkınca sıfırlanır (Faz 9 kayıt).
- **Bölge başına kalan blok sayacı yok:** panel üretimdeki blok sayısını gösterir (tasarım: "ileride").
- **Sınırlar düz açılı dilimler:** prototip; düzensiz bölgeler sonra.
- Bölge sınırı çizgisi voxel çözünürlüğünde (2 m) olduğu için hafif tırtıklı.

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `'Region Layout' atanmamış` | Eski sahne → Setup Main Scene |
| Her yer kilitli / hiçbir yer kazılmıyor | RegionLayout'ta "Unlocked At Start" işaretli bölge yok veya kimlikler 0 |
| Bölge modunda renk yok | Shader derlenmedi → Console'da shader hatası var mı? M_Mountain materyali VertexColorLit mi? |
| Bölge modunda dağ tamamen gri | Bölge kimlikleri yazılmamış (bootstrap sırası) → Console'daki hatayı gönder |
| Console uyarısı "sonraki bölgeyi açmaya YETMİYOR" | Bölge 1 çok küçük veya fiyat yüksek → RegionLayout'ta açıyı genişlet / fiyatı düşür |
| Kamera başlangıçta bölgeye bakmıyor | 1. bölgenin açı aralığını kontrol et (kamera aralığın ortasına bakar) |

---

## 8. FAZ 6 Kabul Kriterleri

- [ ] 3 bölge: 1 açık, 2 = 1.000 $, 3 = 6.000 $
- [ ] Yalnızca açık bölgelerde kazılabiliyor; kazı yarıçapı kilitli bölgeye taşmıyor
- [ ] Bölge modunda sınırlar renkli (açık yeşil, kilitli turuncu/kırmızı)
- [ ] Bölgeye tıklayınca fiyat görünüyor; para yetiyorsa satın alınabiliyor
- [ ] Satın alınca o bölgenin doğru voxelleri açılıyor
- [ ] İlk bölgenin değeri ikinci bölgeyi açmaya yetiyor (Console + test)
- [ ] Bölge modu açıkken sol tık kazmıyor
- [ ] 122 EditMode testi geçiyor

Hepsi tamamsa **Faz 7: Otomatik matkap** (Mini Drill satın alma, yerleştirme, otomatik kazı, Drill Damage/Speed).
