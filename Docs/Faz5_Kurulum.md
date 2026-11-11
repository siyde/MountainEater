# Mountain Eater 3D — FAZ 5: Madenler

## 1. Bu fazda ne yapıyoruz?

- Dağın içine **bakır, altın ve kristal** dağıtılıyor; taş çoğunlukta kalıyor.
- Değerli madenler **iç kısımlarda** ve **belirli bölgelerde** daha yoğun.
- Madenler farklı renk ve **parlaklıkta** görünüyor (altın ve kristal ışığı yansıtır).
- Dağılım seed'e bağlı ve deterministik (Faz 9 kaydı için şart).

HP ve ödüller Faz 2'den beri tabloda; Faz 3–4 sistemleri onları zaten kullanıyor:

| Malzeme | HP | Ödül | Hedef oran | Nerede |
|---|---:|---:|---:|---|
| Normal Taş | 3 | 5 $ | ~%83,8 | her yer |
| Bakır | 7 | 15 $ | %11 | yüzeyden itibaren, kümeler |
| Altın | 18 | 45 $ | %4 | ≥3 voxel içeride, damarlar |
| Kristal | 45 | 120 $ | %1,2 | ≥5 voxel içeride, alt-orta kesim, küçük kümeler |

---

## 2. Algoritma: oran hedefli dağıtım

Saf eşik ("gürültü > 0.7 ise altın") yöntemi ile maden oranını tutturmak zordur: dağın şekli veya
voxel boyutu değişince oranlar kayar. Bunun yerine:

1. **Yüzey uzaklığı:** Her dolu voxelin en yakın havaya kaç adım olduğu çok kaynaklı BFS ile hesaplanır
   (`VoxelAnalysis.ComputeSurfaceDistance`). Dağın yan yüzleri de hesaba girer (sadece dikey derinlik değil);
   dünyanın altı zemin sayılır.
2. **Puan:** Her uygun voxel için
   `puan = şekilGürültüsü × (1 + zenginlik × bölgeselGürültü) + derinlikAğırlığı × derinlikFaktörü`
   - **Blob** (bakır, kristal): 3D gürültünün kendisi → yuvarlak kümeler.
   - **Vein** (altın): `1 − |gürültü|` → gürültünün sıfır yüzeyleri boyunca ince damarlar.
   - **Bölgesel zenginlik:** düşük frekanslı 2D gürültü → dağın bazı yamaçları daha zengin.
   - **Derinlik bonusu:** içeri girdikçe puan artar → değerli madenler iç kısımlarda yoğunlaşır.
3. **Seçim:** En yüksek puanlı `hedefOran × toplamDoluVoxel` kadar voxel madene çevrilir.
4. Katmanlar sırayla işlenir: **nadir olan önce** yer seçer, madenler üst üste binmez.

Dolu voxel sayısı ve dağın şekli değişmez; yalnızca malzeme değişir. Her katmanın gürültüsü
seed + maden türünden türetilir: bir katmanı değiştirmek diğerlerinin şeklini bozmaz.

**Görünüm:** Hücre köşesinin rengi komşu voxellerin en değerli malzemesinden alınır (madenler belirgin).
Parlaklık köşe renginin **alfa kanalında** shader'a gider: tek materyal, ek draw call yok.

---

## 3. Dosyalar

**Yeni**
```
Assets/Scripts/Voxel/VoxelAnalysis.cs    yüzey uzaklığı (BFS), malzeme sayımı
Assets/Scripts/World/OreLayer.cs         maden katmanı ayarları + varsayılanlar
Assets/Scripts/World/OreGenerator.cs     oran hedefli dağıtım
Assets/Tests/EditMode/OreGeneratorTests.cs
```

**Değişen**
| Dosya | Değişiklik |
|---|---|
| `MountainShapeSettings.cs` | **Madenler** listesi + ⋮ "Reset Ores To Defaults" |
| `MountainGenerator.cs` | şekilden sonra `OreGenerator.Distribute` |
| `VoxelMaterialDatabase.cs` | `smoothness` (parlaklık) alanı, yeni renkler, ⋮ "Reset To Design Defaults" |
| `VoxelMaterialTable.cs` | `GetSmoothness` |
| `SurfaceNetsMesher.cs` | köşe alfa = parlaklık |
| `VertexColorLit.shader` | parlaklık = max(materyal, köşe alfası) |
| `VoxelWorld.cs` | `SetGeneratedMaterialAt` |
| `GameBootstrap.cs` | Console'a malzeme dağılımı |

Sahne değişmedi — **sahneyi yeniden kurman gerekmiyor.**

---

## 4. Unity Editor'da yapılacaklar

1. Unity'ye geç, derlemeyi bekle (Console temiz).
2. **Malzeme veritabanını güncelle** (eski asset'te parlaklık alanı yok, hepsi mat kalır):
   `Assets/ScriptableObjects/Voxel/VoxelMaterialDatabase.asset` → Inspector sağ üst **⋮** →
   **Reset To Design Defaults**. Ctrl+S.
3. `MountainShapeSettings.asset` → Inspector'da **Madenler** listesinde 3 katman görünmeli
   (görünmüyorsa ⋮ → **Reset Ores To Defaults**).
4. **Play**.

### Inspector: OreLayer alanları
| Alan | Anlamı |
|---|---|
| Material | Bakır / Altın / Kristal |
| Target Fraction | dağdaki oran (0.04 = %4) |
| Min Depth | yüzeyden en az kaç voxel içeride (1 = yüzeyde görünebilir) |
| Min/Max Height01 | dağ yüksekliğine göre aralık (0 taban, 1 zirve) |
| Shape | Blob (küme) / Vein (damar) |
| Noise Scale | büyük = küçük ve sık kümeler |
| Depth Weight / Depth Range | iç kısım tercihi |
| Richness Strength / Scale | bölgesel zenginlik |

Değişiklikler bir sonraki Play'de geçerli olur. Seed aynı kaldıkça dağılım aynıdır.

---

## 5. Test

### Otomatik
`Test Runner > EditMode > Run All` → **104 test** (92 eski + 12 yeni).

| Test | Kanıtladığı |
|---|---|
| `Distribute_HitsTargetFractions` | oranlar tam tutuyor (%1 / %4 / %10) |
| `Distribute_DoesNotChangeShapeOrSolidCount` | dolu voxel sayısı ve sayaç değişmiyor |
| `Distribute_IsDeterministic_AndSeedDependent` | aynı seed = aynı madenler |
| `Distribute_RespectsMinDepth` | kristal ≥6, altın ≥3 içeride |
| `Distribute_FavoursInterior_WhenDepthWeighted` | değerli madenler ortalamada daha derinde |
| `Distribute_RespectsHeightRange` | yükseklik sınırı |
| `GeneratedMountain_ContainsAllOres_WithStoneMajority` | gerçek dağda tüm madenler var, taş >%70, bakır > altın > kristal |
| `SurfaceDistance_*` | BFS derinlik doğru; dünyanın altı zemin |
| `Smoothness_GoesToVertexAlpha…` | parlaklık köşe alfasına yazılıyor |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | Play → Console | `Malzemeler: Normal Taş … (%83,8) · Bakır … (%11,0) · Altın … (%4,0) · Kristal … (%1,2)` ve artmış toplam değer (~250–270 bin $) |
| 2 | Dağ yüzeyine bak | Gri taşta turuncu bakır lekeleri; altın/kristal yüzeyde nadir veya yok |
| 3 | Bir bakıra vur | 7 HP: Lv1 kazmayla 7 tık, `+15 $`, turuncu parçacıklar |
| 4 | İçeri doğru kaz (Starting Money'yi yükseltip radius al) | 3+ voxel derinlikte sarı altın damarları, daha derinde mavi kristal kümeleri |
| 5 | Kamerayı ışığa göre çevir | Altın ve kristal parlıyor, taş mat |
| 6 | Kristale Lv1 kazmayla vur | 45 tıkta kırılır, `+120 $`; hasar koyulaşması kademeli |
| 7 | Aynı seed ile iki kez Play | Madenler aynı yerlerde |

---

## 6. Dengeleme

Prototip dağın değeri ~143 bin $ → **~260 bin $** (tahmin; gerçek değer Console'da). Mining Radius Lv4
(150.000 $) artık ulaşılabilir. Erken oyunda yüzeydeki bakır kümeleri hızlı gelir verir; altın ve kristal
için içeri kazmak gerekir → derin kazıyı ödüllendiren ilerleme.

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| Madenler parlamıyor | Malzeme veritabanında parlaklık 0 → ⋮ Reset To Design Defaults |
| Hiç maden yok | MountainShapeSettings → Madenler listesi boş → ⋮ Reset Ores To Defaults |
| Pembe/magenta lekeler | Veritabanında listede olmayan bir malzeme (ör. Material alanında yanlış seçim) |
| Kristal hiç çıkmıyor | Dağ çok küçük/sığ: Min Depth veya Max Height01'i gevşet; Console'daki oranlara bak |
| Açılış yavaşladı | Maden dağıtımı tüm dolu voxelleri sıralar; prototipte birkaç ms beklenir. Console'daki "Üretim … ms"yi gönder |

---

## 8. FAZ 5 Kabul Kriterleri

- [ ] Dağda taş, bakır, altın ve kristal var; taş çoğunlukta
- [ ] Değerli madenler iç kısımlarda daha yoğun; bazı bölgeler daha zengin
- [ ] Her maden farklı renkte; altın/kristal parlıyor
- [ ] Farklı malzemeler farklı sayıda vuruşta kırılıyor ve farklı para veriyor
- [ ] Aynı seed aynı maden dağılımını veriyor
- [ ] Toplam blok sayısı madenlerden etkilenmiyor
- [ ] 104 EditMode testi geçiyor

Hepsi tamamsa **Faz 6: Kazı bölgeleri** (bölge tanımları, kilit, satın alma, sınır gösterimi).
