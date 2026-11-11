# Mountain Eater 3D — FAZ 3: Gerçek Kazı

## 1. Bu fazda ne yapıyoruz?

Sol tıklama artık gerçek bir kazma vuruşu:

```
Sol tık (basıldığı kare) ──► UI üzerinde mi? / kamera sürükleniyor mu? ──► hayır
   └► kameradan raycast ──► chunk collider'ı ──► MiningTargetResolver: yüzeyin arkasındaki voxel
        └► MiningSystem.ApplyHit(hedef, yarıçap, hasar)
              ├► küredeki her kazılabilir voxel hasar alır  (VoxelWorld.damage)
              ├► HP'si biten voxel kırılır                  (VoxelWorld.RemoveVoxel → sayaç −1, ödül 1 kez)
              └► etkilenen chunk'lar dirty ──► aynı karede LateUpdate'te mesh + collider güncellenir
        └► MiningEffects: toz/parça + ses
```

- **Hasar göstergesi:** hasarlı voxeller kırılana kadar giderek koyulaşır (vertex rengi, ek maliyet yok).
- Para henüz cüzdana gitmiyor; ödül hesaplanıyor ve overlay'de görünüyor. Faz 4'te `MiningSystem.Mined`
  olayına EconomyManager bağlanacak.

---

## 2. Mimari kararlar

**Collider'sız tasarım (test sırasında değiştirildi).** İlk sürüm tıklamayı chunk MeshCollider'larına
fizik raycast'i ile buluyordu. Testte collider'lara mesh atanmasının sessizce başarısız olduğu görüldü
(28 collider, 0 mesh'li; kök neden belirlenemedi). Artık:
- Kazı hedefi **VoxelRaycast** (3D DDA) ile doğrudan voxel verisinden bulunur. Fizik ışını bir chunk'a
  çarparsa önce o kullanılır (görünen yüzeyle birebir), çarpmazsa voxel ışını devreye girer.
- Kamera arazi koruması **ChunkManager.GetSurfaceHeight** ile voxel sütunlarından okunur.
- `ChunkManager > Generate Colliders` varsayılan olarak **kapalı**: her kazıda collider "cook" maliyeti yok
  (Faz 7'de yüzlerce matkap için önemli). Voxel ışını, görünen yumuşak yüzeyden en fazla yarım voxel sapabilir.
- Collider tanısı gerekirse: Play'de VoxelMountain → ChunkManager ⋮ → **Debug: Collider Report**.

**MiningSystem tek kural kaynağı.** Saf C# sınıfı; oyuncu ve (Faz 7'de) makineler aynı `ApplyHit`'i çağırır.
Kurallar tek yerde: boş voxel vurulmaz, kilitli bölge hasar almaz, ödül yalnızca kırılma anında ve bir kez.

**Çift ödül neden imkânsız?** Ödül, `VoxelWorld.RemoveVoxel` **true** döndüğünde eklenir. Zaten kırılmış
voxel için bu metot false döner. Oyuncu ve makine aynı voxeli aynı karede vursa bile (oyun tek iş
parçacıklı) sadece ilk kıran ödül alır. Testle kanıtlandı (`PlayerAndMachine_OnSameVoxel_NeverDoublePay`).

**Bölge kilidi şimdiden kancalı.** `MiningSystem.IsRegionUnlocked` (bölge kimliği → bool). Şu an null =
her yer açık. Faz 6'da RegionManager bağlanacak; küresel hasarın kilitli bölgeye taşmadığı bugünden test ediliyor.

**Hedef seçimi.** Surface Nets yüzeyi dolu ve boş voxel merkezlerinin arasından geçer. Tıklanan nokta
normalin tersine çeyrek voxel içeri itilir, 3×3×3 komşulukta en yakın dolu voxel seçilir. Hedef her zaman
görünen yüzeyin hemen arkasındaki voxeldir.

**Tek vuruş = tek tık.** `leftButton.wasPressedThisFrame`: basılı tutmak tekrar vurmaz.
Kamera sürüklenirken (`OrbitCameraController.IsDragging`) ve UI üzerindeyken vuruş yok.

**Efektler havuzsuz ama allocation'sız.** İki kalıcı ParticleSystem tıklanan noktaya taşınıp `Emit()`
ile parçacık basar (dünya uzayı simülasyonu). Vuruş başına yeni GameObject yok → pooling gerekmez.
Ses dosyası yoksa kısa bir ses kodla üretilir (geçici, Faz 11'de gerçek sesler).

**Mesh güncellemesi aynı karede.** Tıklama `Update`'te, chunk yeniden mesh'i `LateUpdate`'te
(kare başına 8 chunk bütçesi). Yarıçap 3'lük vuruş en fazla 8 chunk'a dokunur.

---

## 3. Dosyalar

**Yeni**
```
Assets/Scripts/Mining/
  MiningResult.cs             vuruş sonucu + MiningSource (Player/Machine)
  SphereOffsets.cs            yarıçap → küre ofsetleri (1 / 7 / 33 / 123), önbellekli
  MiningSystem.cs             hasar, kırma, ödül, bölge kontrolü, Mined olayı
  MiningTargetResolver.cs     tıklama noktası → hedef voxel
  PlayerMiningController.cs   sol tık → raycast → vuruş
  MiningEffects.cs            parçacık + ses
Assets/Scripts/Voxel/
  VoxelMaterialTable.cs       malzeme id → renk / HP / ödül (hızlı tablo)
Assets/Tests/EditMode/
  MiningSystemTests.cs, MiningTargetResolverTests.cs
```

**Değişen**
| Dosya | Değişiklik |
|---|---|
| `VoxelMaterialDatabase.cs` | `BuildColorPalette()` → `CreateTable()` |
| `VoxelWorld.cs` | `SetDamage` artık chunk'ı günceller (hasar görünür), boş voxele yazmaz; `GetDamageAt` |
| `SurfaceNetsMesher.cs` | renk dizisi yerine `VoxelMaterialTable`; hasar koyulaşması |
| `ChunkManager.cs` | `Initialize(world, size, table)`; `WorldToVoxelSpace`, `WorldToVoxelDirection` |
| `GameBootstrap.cs` | MiningSystem oluşturur; yeni alanlar: Player Mining, Mining Effects |
| `DebugOverlay.cs` | kazma ve son vuruş bilgisi |
| `MountainEaterSceneSetup.cs` | Mining nesnesi + `M_MiningParticles` materyali |

> ⚠ **Kırıcı değişiklik:** `GameBootstrap`'e yeni zorunlu alan (Player Mining) eklendi.
> Sahneyi **Mountain Eater > Setup Main Scene** ile yeniden kur.

---

## 4. Unity Editor'da kurulum

### A) Otomatik (önerilen)
1. Unity'ye geç, derlemeyi bekle (Console temiz).
2. **Mountain Eater > Setup Main Scene** → "Üzerine yaz".
3. **Play**.

### B) Elle (mevcut sahneyi korumak istersen)
1. `Create > Material` → Shader `Universal Render Pipeline/Particles/Simple Lit` → `M_MiningParticles`.
2. Boş GameObject **Mining** → `MiningEffects` (Particle Material: M_MiningParticles) ve
   `PlayerMiningController` ekle (Target Camera: Main Camera, Orbit Camera: Main Camera, Effects: Mining).
3. **GameBootstrap** → Player Mining: Mining, Mining Effects: Mining.
4. **DebugOverlay** → Player Mining: Mining.

### Inspector (PlayerMiningController)
| Alan | Varsayılan | Not |
|---|---|---|
| Pickaxe Damage | 1 | Faz 4'te yükseltmeden gelecek |
| Mining Radius | 0 | 0 → 1 voxel, 1 → 7, 2 → 33, 3 → 123 |
| Max Ray Distance | 2000 | |
| Voxel Mask | Default | chunk'ların layer'ı |

Play sırasında bu iki değeri Inspector'dan değiştirip hemen deneyebilirsin.

**MiningEffects:** Hit Particles 6, Break Particles Per Voxel 5, Max Break Particles Per Hit 60, Volume 0.5.

---

## 5. Test

### Otomatik
`Window > General > Test Runner > EditMode > Run All` → **65 test** (36 eski + 29 yeni).

| Test | Kanıtladığı |
|---|---|
| `SphereOffsets_MatchDesignTable` | yarıçap 0/1/2/3 → 1/7/33/123 voxel |
| `Hit_DamagesEveryVoxelInSphere` | 3D küre doğru hesaplanıyor |
| `Stone_BreaksAfterThreeHitsOfOne_AndPaysOnce` | 3 HP taş 3 vuruşta kırılır, 5 $ bir kez, 4. vuruş boşa |
| `PlayerAndMachine_OnSameVoxel_NeverDoublePay` | manuel + makine aynı voxel → tek ödül |
| `LockedRegion_IsNeverDamaged_EvenInsideRadius` | kazı kilitli bölgeye taşmıyor |
| `TargetInLockedRegion_DoesNothing` | kilitli bölgeye vuruş yok |
| `RadiusHit_BreaksWholeSphere_AndRewardIsSum` | sayaç ve ödül toplamı tutarlı |
| `DamagedVoxel_IsDrawnDarker` | hasar göstergesi |
| Resolver testleri | doğru voxel seçimi, boşlukta hedef yok |

### Elle
| # | Test | Beklenen |
|---|---|---|
| 1 | Dağa 1 kez sol tık | Küçük toz + "tık". Overlay: `1 hasar, 0 kırık`. Yüzey hafif koyulaşır |
| 2 | Aynı yere 2 tık daha | 3. tıkta parça patlaması + "çatırtı", **yüzeyde oyuk**, Voxel sayacı −1, `+5 $` |
| 3 | Sol tuşu basılı tut | Yalnızca 1 vuruş |
| 4 | Sürekli tıklayarak kaz | Oyuk derinleşir; tünel açılabilir; komşu chunk sınırında çatlak yok |
| 5 | Mining Radius = 2, Damage = 3 (Play'de Inspector'dan) | Tek tıkta ~yarım küre oyuk, ~15–33 voxel |
| 6 | Zemine tıkla | Hiçbir şey olmaz |
| 7 | Sağ tıkla sürüklerken sol tık | Vuruş yok |
| 8 | Oyuğun içine tıkla | Oyuğun dibindeki/kenarındaki voxel kırılır (yüzeyin arkası değil) |
| 9 | Dağın en alt katmanını kaz | Zemin görünür; voxel sayacı 0'ın altına inmez |
| 10 | Hızlı tıklama sırasında FPS | Belirgin düşüş yok (Profiler: `ChunkManager.LateUpdate`) |

---

## 6. Bilinen sınırlamalar (sonraki fazlarda)

- **Kamera tünele giremez:** Faz 1'deki arazi koruması kamerayı yüzeyin üstünde tutar. Tünel ağzını dışarıdan
  görebilirsin; tünel içine kamera sokmak istersen söyle, o zaman koruma yöntemini değiştiririz.
- **Havada kalan parçalar:** Altı kazılmış kaya düşmez (tasarım gereği; dağ voxel'leri bağımsız).
- **Para birikmiyor:** Faz 4.
- **Dağın tabanı açık:** En alt katman kazılınca altında mesh yok, zemin görünür (beklenen davranış).

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `[GameBootstrap] 'Player Mining' atanmamış.` | Eski sahne → Setup Main Scene |
| Tıklayınca hiçbir şey olmuyor | Overlay'de "Son vuruş" değişiyor mu? Değişmiyorsa: Game penceresine odak ver; Voxel Mask'ta Default seçili mi; sahnede ekranı kaplayan UI var mı |
| Parçacıklar pembe | Particle materyali yok/shader bulunamadı → M_MiningParticles ata |
| Ses yok | Main Camera'da AudioListener var mı; Game penceresinde "Mute Audio" kapalı mı |
| Oyuk görünmüyor ama sayaç azalıyor | Chunk rebuild kuyruğu birikiyor (overlay "bekleyen"); Max Chunk Rebuilds Per Frame'i artır |

---

## 8. FAZ 3 Kabul Kriterleri

- [ ] Sol tık = tek vuruş; basılı tutmak ek vuruş yapmıyor
- [ ] UI üzerinde / kamera sürüklenirken vuruş yok
- [ ] Taş 3 vuruşta kırılıyor, yüzeyde gerçek oyuk açılıyor
- [ ] Kırılan voxel veriden siliniyor, sayaç tam 1 azalıyor, aynı voxel tekrar sayılmıyor
- [ ] Yarıçap 3D küre olarak çalışıyor
- [ ] Mesh ve collider tutarlı: oyuğun içine tıklayınca doğru voxel kırılıyor
- [ ] Hasarlı voxel koyulaşıyor; vuruş ve kırılma efektleri + ses
- [ ] Hızlı kazıda kamera akıcı
- [ ] 65 EditMode testi geçiyor

Hepsi tamamsa **Faz 4: Kazma yükseltmeleri + para** (EconomyManager, UpgradeManager, veri odaklı seviye tabloları).
