# Mountain Eater 3D — FAZ 4: Para ve Kazma Yükseltmeleri

## 1. Bu fazda ne yapıyoruz?

- **EconomyManager:** kırılan voxelin ödülü cüzdana eklenir (oyuncu ve ileride makineler aynı yol).
- **UpgradeManager + UpgradeDefinition:** veri odaklı seviye tabloları; Pickaxe Damage ve Mining Radius.
- **HUD (UI Toolkit):** para, dağ ilerlemesi (kalan blok, yüzde, çubuk), yükseltme paneli, "+5 $" uçan yazılar.
- **GameBalanceConfig:** başlangıç parası, ödül çarpanı, geliştirme listesi — tek merkez.

```
MiningSystem.Mined ──► EconomyManager.Add(ödül × çarpan) ──► MoneyChanged ──► HUD (para + buton durumları)
HUD "Satın al" ──► UpgradeManager.TryPurchase ──► EconomyManager.TrySpend
                                              └► LevelChanged ──► GameBootstrap.ApplyUpgrade ──► PlayerMiningController
```

---

## 2. Mimari kararlar

**Saf C# çekirdek.** `EconomyManager` ve `UpgradeManager` MonoBehaviour değil: sahnesiz test edilir,
Faz 9'da doğrudan kaydedilir, Faz 10'da çevrimdışı simülasyonda kullanılır.

**UpgradeManager geliştirmenin etkisini bilmez.** Sadece seviye ve fiyatı yönetir, `LevelChanged` yayınlar.
Etkiyi uygulayan tek yer `GameBootstrap.ApplyUpgrade`. Faz 7'de Drill Damage / Drill Speed eklemek =
yeni `UpgradeType` + yeni asset + `ApplyUpgrade`'e bir `case`. HUD satırları otomatik oluşur.

**Veri odaklı tablolar.** `UpgradeDefinition` asset'inde her satır = (değer, o seviyeye çıkma fiyatı).
Tablo bitince isteğe bağlı otomatik seviyeler: değer × `valueGrowth`, fiyat × `costGrowth`
(fiyatlar 2 anlamlı basamağa yuvarlanır: 12.160 → 12.000).

| Pickaxe Damage | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | Lv6 | Lv7 (otomatik) | … Lv25 |
|---|---|---|---|---|---|---|---|---|
| Hasar | 1 | 2 | 3 | 5 | 8 | 13 | 21 | ×1.6 |
| Fiyat | – | 20 | 65 | 220 | 850 | 3.200 | 12.000 | ×3.8 |

| Mining Radius | Lv1 | Lv2 | Lv3 | Lv4 (MAX) |
|---|---|---|---|---|
| Yarıçap (voxel) | 0 (1) | 1 (7) | 2 (33) | 3 (123) |
| Fiyat | – | 600 | 9.000 | 150.000 |

**Neden UI Toolkit (uGUI değil)?**
- Unity 6'nın önerdiği çalışma zamanı UI sistemi; düzen UXML'de, stil USS'te (CSS benzeri) → kod sade.
- TextMeshPro içe aktarma adımı gerekmez; Türkçe karakterler varsayılan fontla çalışır.
- Yükseltme satırları gibi tekrarlayan öğeler koddan kolay üretilir; Faz 8'de paneller büyüyecek.
- Çözünürlük: `HudPanelSettings` 1920×1080 referansla ölçeklenir.

**UI üzerinde tıklama kazmaz.** HUD panelleri `PointerUtility`'ye kaydolur; imleç görünür bir panelin
içindeyse kazı ve kamera sürüklemesi başlamaz. Tam ekran kapsayıcılar `picking-mode="Ignore"` ile işaretli,
yani paneller dışındaki her yer dağa tıklanabilir.

**Sayı biçimi Türkçe:** `847.261 / 1.000.000 blok kaldı`, `%15,27 yok edildi`, 10 milyonun üstü `12,5 Mn $`.

---

## 3. Dosyalar

**Yeni**
```
Assets/Scripts/Economy/EconomyManager.cs     para, harcama, Mined olayına abone
Assets/Scripts/Economy/EconomyMath.cs        dağın toplam ödül değeri (dengeleme)
Assets/Scripts/Upgrades/UpgradeType.cs       enum (kayıt için sabit sayılar)
Assets/Scripts/Upgrades/UpgradeTable.cs      seviye → değer/fiyat (saf C#)
Assets/Scripts/Upgrades/UpgradeDefinition.cs Inspector'da düzenlenen tablo (ScriptableObject)
Assets/Scripts/Upgrades/DefaultUpgradeData.cs tasarım belgesi değerleri
Assets/Scripts/Upgrades/UpgradeManager.cs    seviyeler + satın alma
Assets/Scripts/Core/GameBalanceConfig.cs     merkezi denge yapılandırması
Assets/Scripts/UI/HudController.cs           HUD
Assets/Scripts/UI/NumberFormat.cs            Türkçe sayı biçimleri
Assets/UI/MainHud.uxml, MainHud.uss, MountainEaterTheme.tss
Assets/Tests/EditMode/EconomyManagerTests.cs, UpgradeTests.cs
```
Kurulum aracının oluşturduğu asset'ler:
```
Assets/ScriptableObjects/GameBalanceConfig.asset
Assets/ScriptableObjects/Upgrades/Upgrade_PickaxeDamage.asset
Assets/ScriptableObjects/Upgrades/Upgrade_MiningRadius.asset
Assets/UI/HudPanelSettings.asset
```

**Değişen**
| Dosya | Değişiklik |
|---|---|
| `GameBootstrap.cs` | Economy/Upgrades oluşturur, yükseltmeleri uygular, HUD'u başlatır; yeni alanlar **Balance Config**, **Hud** |
| `PointerUtility.cs` | UI Toolkit panelleri için engelleme |
| `PlayerMiningController.cs` | `PlayerHit` olayı; Log Diagnostics varsayılan kapalı; hasar/yarıçap artık yükseltmelerden |
| `DebugOverlay.cs` | sol alt köşeye taşındı |
| `MountainEaterSceneSetup.cs` | yeni asset'ler + HUD nesnesi |

> ⚠ **Kırıcı değişiklik:** GameBootstrap'e zorunlu **Balance Config** alanı eklendi →
> **Mountain Eater > Setup Main Scene** ile sahneyi yeniden kur.
> Play sırasında PlayerMiningController'daki Pickaxe Damage / Mining Radius değerleri artık yükseltmelerden gelir
> (Inspector'da elle değiştirdiğin değer bir sonraki satın almada ezilir).

---

## 4. Unity Editor'da kurulum

1. Unity'ye geç, derlemeyi bekle (Console temiz).
2. **Mountain Eater > Setup Main Scene** → "Üzerine yaz".
3. **Play**.

### Elle kurulum (gerekirse)
1. `Create > Mountain Eater > Upgrade Definition` ×2 → Inspector'da Type ve tabloları doldur
   (veya kurulum aracının oluşturduğu asset'leri kullan).
2. `Create > Mountain Eater > Game Balance Config` → Upgrades listesine iki tanımı ekle.
3. `Create > UI Toolkit > Panel Settings Asset` → Scale Mode: Scale With Screen Size, 1920×1080, Match 0.5.
4. Boş GameObject **HUD** → `UIDocument` (Panel Settings, Source Asset: MainHud.uxml) + `HudController`.
5. GameBootstrap → Balance Config, Hud alanlarını ata.

### Inspector'da dengeleme
- **GameBalanceConfig:** Starting Money (test için örn. 100000), Reward Multiplier.
- **Upgrade_*.asset:** Levels tablosu, Extend Beyond Table, Value/Cost Growth, Max Level.
  Değişiklikler bir sonraki Play'de geçerli olur.

---

## 5. Test

### Otomatik
`Test Runner > EditMode > Run All` → **92 test** (65 eski + 27 yeni).

| Test | Kanıtladığı |
|---|---|
| `PickaxeTable_MatchesDesignDocument` | 1/2/3/5/8/13 hasar, 20/65/220/850/3.200 fiyat |
| `PickaxeTable_ExtendsBeyondTable…` | Lv7 = 21 hasar / 12.000 $, sonrası hep artıyor |
| `RadiusTable_MatchesDesignDocument_AndStopsAtLevel4` | 600 / 9.000 / 150.000, Lv4 MAX |
| `Purchase_WithoutMoney_Fails` | para yetmezse seviye ve bakiye değişmez |
| `BuyingWholePickaxeTable_CostsDesignTotal` | 4.355 $ ile Lv6, bakiye tam 0 |
| `MaxedUpgrade_CannotBePurchased` | MAX'ta para düşmez |
| `BreakingVoxels_PaysExactlyTheirReward` | yalnızca kırılan voxel ödenir; boşa vuruş 0 $ |
| `RewardMultiplier_IsApplied` | maden değeri çarpanı |
| `NumberFormat*` | Türkçe biçimler |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | Play | Sol üst: `0 $`, `28.626 / 28.626 blok kaldı`, `%0,00 yok edildi`. Sağ üst: iki yükseltme, butonlar gri |
| 2 | Bir taşı kır (3 tık) | `+5 $` yazısı yükselip kaybolur; para 5 $; sayaç 1 azalır; yüzde artar |
| 3 | 20 $'a ulaş | "Pickaxe Damage" butonu yeşil olur |
| 4 | Satın al | Para 20 düşer, `Lv 2`, `Hasar: 2 → 3`, fiyat 65 $. Taş artık 2 tıkta kırılır |
| 5 | Butona tıkla | Arkasındaki kaya kazılmaz |
| 6 | Panelin üzerinde sağ tıkla sürükle | Kamera dönmez; panel dışında döner |
| 7 | Balance Config → Starting Money 200000 → Play | Radius'u 3 kez al → `MAX`; tek tıkta büyük küre oyuk |
| 8 | Game view çözünürlüğünü değiştir | HUD orantılı ölçeklenir |
| 9 | Console | `Dağın toplam değeri: …$` satırı (prototip: ~143.000 $) |

---

## 6. Dengeleme notu (bilinen)

Prototip dağ (~28.600 taş × 5 $) toplam **~143.000 $** değerinde. Bu yüzden Mining Radius Lv4 (150.000 $)
şu an alınamaz. Bu bilinçli olarak bırakıldı: Faz 5 madenleri (bakır/altın/kristal) dağın değerini artıracak,
Faz 6 bölgeleri ve Faz 12 ekonomi simülasyonu fiyatları gerçek dağa göre ayarlayacak.

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `[GameBootstrap] 'Balance Config' atanmamış.` | Eski sahne → Setup Main Scene |
| HUD görünmüyor | HUD nesnesinde UIDocument'in Panel Settings ve Source Asset alanları dolu mu |
| HUD yazıları stilsiz / çok küçük | HudPanelSettings → Theme Style Sheet: MountainEaterTheme.tss |
| Her yere tıklayınca "imleç UI üzerinde" | Bir kapsayıcı tıklamayı yakalıyor: UXML'de tam ekran elemanlar `picking-mode="Ignore"` olmalı. Mining → Log Diagnostics'i açıp Console'u gönder |
| Satın alınca hasar değişmiyor | GameBootstrap → Player Mining bağlı mı |
| Console'da her tıklamada `[PlayerMining]` | Eski sahnede Log Diagnostics açık kalmış → Mining nesnesinde kapat |

---

## 8. FAZ 4 Kabul Kriterleri

- [ ] Kırılan voxel ödülü cüzdana ekleniyor (yalnızca kırılınca, bir kez)
- [ ] Para, kalan blok, yüzde ve ilerleme çubuğu gerçek zamanlı ve doğru
- [ ] Pickaxe Damage ve Mining Radius ayrı ayrı satın alınabiliyor; fiyatlar tablodaki gibi
- [ ] Para yetmeyen buton pasif; MAX seviyede buton "MAX"
- [ ] Satın alma anında etkili (taş daha az tıkta kırılıyor / daha geniş oyuk)
- [ ] UI'ya tıklamak kazmıyor, UI üzerinde kamera sürüklenmiyor
- [ ] "+N $" uçan yazıları görünüyor
- [ ] Dengeleme değerleri asset'lerden değiştirilebiliyor
- [ ] 92 EditMode testi geçiyor

Hepsi tamamsa **Faz 5: Madenler** (bakır, altın, kristal dağılımı; farklı HP ve ödüller).
