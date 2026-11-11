# Mountain Eater 3D — FAZ 7: Otomatik Matkap

## 1. Bu fazda ne yapıyoruz?

- **Mini Drill**: 250 $, her 3 sn'de 3 hasar, yarıçap 0 — gerçek voxel kazar, para kazandırır, dağı değiştirir.
- **Mağaza (Shop)** → "Satın al ve yerleştir" → **yerleştirme modu** (yeşil/kırmızı önizleme) → açık bölgede yüzeye tıkla.
- **Makineler (Machines)** paneli: her matkabın bölgesi, durumu, kırdığı blok ve kazandığı para; **Odakla** ve **Taşı**.
- **Drill Damage** ve **Drill Speed** yükseltmeleri (yükseltme panelinde, otomatik).
- Çalışma alanında kaya kalmayınca matkap **durur**, kırmızıya döner ve "durdu" yazısı çıkar; taşınınca devam eder.

```
MachineController.Update ─► MachineManager.Tick(dt)  (bütün makineler TEK döngüde)
                                 └► her makine: zamanlayıcı ≥ aralık → hedef bul/koru → MiningSystem.ApplyHit(..., Machine, bölge)
                                                                                   └► Mined → EconomyManager (+$)
                                                                                   └► VoxelWorld → chunk yeniden mesh
```

---

## 2. Mimari kararlar

**Merkezi sistem.** `MachineManager` saf C#; makine başına MonoBehaviour, Update, collider veya fizik yok.
Makine verisi (`Machine`) sadece durum taşır. Yüzlerce makine = tek döngüde yüzlerce sayaç. Görünüm
(`MachineView`) ayrı ve mantıksız: konumu ve rengi `MachineController` ayarlar.

**Oyuncu ile aynı kurallar.** Makineler `MiningSystem.ApplyHit(…, MiningSource.Machine, bölge)` çağırır:
- kilitli bölge kazılamaz, kırılan voxel bir kez sayılır/ödenir (oyuncu ile aynı voxele vursalar bile),
- yeni `regionFilter` parametresi: makine **yalnızca atandığı bölgede** kazar, ileride yarıçaplı makineler de
  komşu bölgeye taşmaz (testli).

**Hedef seçimi.** Yerleştirildiği noktadan yakından uzağa (Faz 3'ün sıralı küre ofsetleri), **açıkta kalan**
(bir yüzü havaya bakan), kazılabilir, kendi bölgesindeki ilk voxel → matkap yüzeyden içeri bir krater açar,
gizli iç voxelleri "delerek" atlamaz. Hedef kırılana kadar aynı hedefe vurur (her vuruşta arama yok).
Oyuncu hedefi kırarsa makine bir sonraki vuruşta yeni hedef seçer.

**Durma.** Çalışma alanı (`workRadius`, varsayılan 12 voxel) içinde hedef kalmazsa `NoTarget` olur,
`MachineStopped` yayınlanır (bir kez) ve bir daha arama yapmaz (CPU harcamaz). Taşınınca yeniden çalışır.
Yalnızca voxel silindiği için durmuş makine kendiliğinden yeni hedef bulamaz → bekleme döngüsü gerekmez.

**Takılma koruması.** Bir karede makine başına en fazla 8 vuruş; uzun takılmalarda biriken vuruşlar yığılmaz.
Faz 10 çevrimdışı ilerleme bunun yerine ayrı, hızlı bir simülasyon kullanacak.

**Ücret yalnızca başarıda.** Yerleştirme moduna girmek bedava; para, uygun bir yere tıklandığı anda düşer.
İptal (Esc) para kaybettirmez. Her yeni Mini Drill %15 daha pahalı (250 → 290 → 330 …; asset'ten değiştirilebilir).

**Modlar.** Kazı · Bölge modu · Yerleştirme modu. Bölge modu ile yerleştirme birbirini kapatır; ikisi de
kapalıyken sol tık kazar (`GameBootstrap.UpdateInteractionMode`).

**Performans önlemleri.** Makine vuruş efektleri kare başına en fazla 6, makine sesi saniyede ~8 ile sınırlı;
makine vuruşları "+$" yazısı göstermez (yüzlerce makinede ekran kirlenmesin).

---

## 3. Dosyalar

**Yeni**
```
Assets/Scripts/Machines/MachineType.cs              MachineType, MachineState
Assets/Scripts/Machines/MachineDefinition.cs        makine türü verisi (ScriptableObject)
Assets/Scripts/Machines/Machine.cs                  MachineSpec (saf veri) + Machine (durum)
Assets/Scripts/Machines/MachineManager.cs           merkezi sistem: yerleştirme, taşıma, Tick, hedef seçimi
Assets/Scripts/Machines/MachineView.cs              GEÇİCİ ilkel model (taban, gövde, dönen uç)
Assets/Scripts/Machines/MachineController.cs        Tick + görünümler + efektler
Assets/Scripts/Machines/MachinePlacementController.cs yerleştirme / taşıma modu
Assets/Scripts/UI/HudServices.cs                    HUD bağımlılıkları tek nesnede
Assets/Tests/EditMode/MachineTests.cs
```
Kurulum aracının oluşturduğu asset'ler:
```
Assets/ScriptableObjects/Machines/Machine_MiniDrill.asset
Assets/ScriptableObjects/Upgrades/Upgrade_DrillDamage.asset
Assets/ScriptableObjects/Upgrades/Upgrade_DrillSpeed.asset
(GameBalanceConfig'e otomatik eklenir; mevcut değerlerin korunur)
```

**Değişen**
| Dosya | Değişiklik |
|---|---|
| `MiningSystem.cs` | `ApplyHit(..., regionFilter)` |
| `UpgradeType.cs`, `DefaultUpgradeData.cs`, `UpgradeDefinition.cs` | Drill Damage, Drill Speed |
| `MiningEffects.cs` | `PlayMachineHit` (hafif, sınırlı ses) |
| `GameBalanceConfig.cs` | `machines` listesi |
| `GameBootstrap.cs` | MachineManager, mod düzeni, yükseltme uygulama; yeni alanlar **Machine Controller**, **Placement Controller** |
| `HudController.cs`, `MainHud.uxml/.uss` | Shop / Machines / Regions alt çubuğu, mağaza ve makine panelleri, yerleştirme bandı |
| `MountainEaterSceneSetup.cs` | Machines nesnesi + yeni asset'ler + eski config'i tamamlama |

> ⚠ **Kırıcı değişiklik:** GameBootstrap'e iki zorunlu alan, HUD'a yeni elemanlar eklendi →
> **Mountain Eater > Setup Main Scene** ile sahneyi yeniden kur.

---

## 4. Unity Editor'da kurulum

1. Unity'ye geç, derlemeyi bekle (Console temiz).
2. **Mountain Eater > Setup Main Scene** → "Üzerine yaz". (Eksik asset'ler oluşturulur, GameBalanceConfig'e eklenir.)
3. `GameBalanceConfig` → **Upgrades** listesinde 4, **Machines** listesinde 1 eleman olduğunu kontrol et.
4. **Play**.

### Inspector
**Machine_MiniDrill.asset**
| Alan | Varsayılan | Anlamı |
|---|---|---|
| Base Price | 250 | ilk matkap fiyatı |
| Price Growth | 1.15 | her yeni matkapta fiyat çarpanı (1 = sabit fiyat) |
| Hit Radius | 0 | vuruş başına etkilenen küre |
| Work Radius | 12 | yerleştirme noktasından en fazla kaç voxel uzağı kazar |
| Damage / Interval Multiplier | 1 | ortak matkap yükseltmelerine çarpan (ileride farklı makine türleri) |

**Upgrade_DrillDamage**: 3 → 5 → 8 → 12 → 18 → 27 (400 / 1.500 / 5.000 / 15.000 / 45.000 $), sonra ×1,5.
**Upgrade_DrillSpeed**: 3,0 → 2,6 → 2,2 → 1,9 → 1,6 → 1,35 sn (500 / 1.800 / 6.000 / 18.000 / 50.000 $), sonra ×0,88.

---

## 5. Test

### Otomatik
`Test Runner > EditMode > Run All` → **137 test** (122 eski + 15 yeni).

| Test | Kanıtladığı |
|---|---|
| `MiniDrill_BreaksOneStoneEveryThreeSeconds_AndPays` | 2,9 sn'de vuruş yok, 3 sn'de 1 taş, 5 $; 11 taş = 55 $ |
| `CanPlace_OnlyOnExposedSolidVoxelInUnlockedRegion` | hava / iç voxel / kilitli bölgeye yerleştirilemez |
| `BuyAndPlace_ChargesOnlyOnSuccess_AndPriceGrows` | uygunsuz yerde para düşmez; 250 → 290 $ |
| `Drill_StaysInsideItsOwnRegion…` | komşu bölge açık olsa bile kazmaz |
| `Drill_WithRadius_DoesNotSpillIntoOtherRegion` | yarıçaplı makine de taşmaz |
| `Drill_StopsWhenWorkAreaIsEmpty_AndNotifiesOnce` | alan bitince durur, tek bildirim, sonra kazmaz |
| `MovingStoppedDrill_ResumesWork` / `MoveToLockedRegion_IsRejected` | taşıma |
| `PlayerBreakingDrillTarget_NoDoublePay_DrillRetargets` | manuel + makine aynı voxel → tek ödeme |
| `LongFrame_IsCapped_NoFreeze` | 10.000 sn'lik kare bile en fazla 8 vuruş |
| `DrillDamage/Speed tables` | yükseltme tabloları |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | 250 $ topla → **Shop** | Mini Drill kartı: "Hasar 3 · her 3 sn · alan 12 voxel", "Satın al ve yerleştir 250 $" |
| 2 | Satın al | Üstte "Makine yerleştir" bandı; imleci dağda gezdir → yeşil önizleme; kilitli bölgede kırmızı + "Bölge kilitli" |
| 3 | Esc | İptal, para düşmedi |
| 4 | Tekrar → açık bölgeye tıkla | "Mini Drill #1 yerleştirildi", para 250 düştü, sonraki fiyat 290 $ |
| 5 | Bekle | Matkap ucu dönüyor, her 3 sn'de parçacık; taş 3 sn'de kırılıyor, para ve blok sayacı değişiyor; matkap kraterin içine ilerliyor |
| 6 | Aynı anda kendin başka yerde kaz | İkisi bağımsız çalışıyor (fazın başarı kriteri) |
| 7 | **Machines** | Satır: "Mini Drill #1 · Ön Yamaç · Çalışıyor · Kırılan N blok · M $"; **Odakla** kamerayı matkaba çevirir |
| 8 | **Taşı** → başka açık bölgeye tıkla | Matkap ışınlanır, yeni bölgede kazar; Regions panelinde bölgelerin makine sayısı |
| 9 | Drill Speed / Damage satın al | Vuruşlar hızlanır / bakır gibi sert taşlar daha az vuruşta kırılır |
| 10 | (Test için Work Radius'u 2 yap, Play) | Kısa sürede alan biter: gövde kırmızı, "Mini Drill #1 durdu: kaya kalmadı", panelde "Durdu" |
| 11 | Bölge modunu (R) aç | Yerleştirme iptal olur; sol tık bölge seçer |
| 12 | 10+ matkap (Starting Money yüksek) | FPS akıcı; Console'da hata yok |

---

## 6. Bilinen sınırlamalar

- Makineler ve konumları Play'den çıkınca kaybolur (Faz 9 kayıt).
- Çevrimdışı kazı yok (Faz 10).
- Model geçici (ilkel şekiller), animasyon basit (Faz 11).
- Makine dünyada tıklanarak seçilemez; seçim Machines panelinden (Odakla).
- Makine satılamaz / kaldırılamaz (tasarımda istenmedi).
- Çok sayıda hızlı makinede chunk yeniden mesh kuyruğu uzayabilir (Faz 12 ölçüm + optimizasyon).

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `'Machine Controller' atanmamış` | Eski sahne → Setup Main Scene |
| `MainHud.uxml'de beklenen elemanlar bulunamadı` | UXML eski: Unity'nin dosyayı yeniden içe aktardığından emin ol (Assets → Refresh) |
| Mağaza boş | GameBalanceConfig → Machines listesi boş → Setup Main Scene'i tekrar çalıştır |
| Drill yükseltmeleri görünmüyor | GameBalanceConfig → Upgrades listesine Upgrade_DrillDamage/Speed ekle |
| Matkap pembe | URP/Lit shader bulunamadı (çok nadir) → Console'u gönder |
| Önizleme hep kırmızı "Açıkta bir yüzey seç" | İmleç bir oyuğun dibinde değil, gerçekten yüzeye bakıyor mu? Yüzey olmayan iç voxeller seçilemez |
| Matkap kazmıyor ama "Çalışıyor" | Hedef kırılması uzun sürüyor olabilir (kristal 45 HP = 15 vuruş). Machines panelinde "Kırılan" artıyor mu? |

---

## 8. FAZ 7 Kabul Kriterleri

- [ ] Mini Drill 250 $'a mağazadan satın alınıp açık bölgeye yerleştirilebiliyor
- [ ] Matkap gerçek voxel kazıyor: para, blok sayacı ve dağın şekli değişiyor
- [ ] Oyuncu bir yerde kazarken matkap başka yerde bağımsız çalışıyor
- [ ] Matkap yalnızca kendi (açık) bölgesinde çalışıyor
- [ ] Drill Damage ve Drill Speed satın alınabiliyor ve etkili
- [ ] Matkap seçilebiliyor (Odakla) ve başka açık bölgeye taşınabiliyor
- [ ] Alan bitince matkap duruyor ve oyuncuya bildiriyor
- [ ] Manuel ve makine kazısı aynı voxelde çift ödeme yapmıyor
- [ ] 137 EditMode testi geçiyor

Hepsi tamamsa **Faz 8: UI ve ilerleme** (arayüzün bütünlenmesi ve cilası).
