# Mountain Eater 3D — FAZ 1: Proje, Sahne ve Orbit Kamera

## 1. Bu fazda ne yapıyoruz?

- Proje klasör yapısı ve **assembly definition** (asmdef) dosyaları
- Seed tabanlı, **deterministik** prosedürel dağ şekli (`MountainShape`)
- Bu şekilden üretilen **geçici** yükseklik haritası dağ mesh'i (Faz 2'de voxel dağ ile değişecek)
- Köşe rengi kullanan URP shader'ı (çimen / kaya / kar)
- Orbit kamera: sağ tık döndürme, tekerlek zoom, orta tık kaydırma, sınırlar, zemin/arazi koruması
- Tek tıkla sahne kuran editör aracı
- EditMode birim testleri

Voxel, kazı ve ekonomi yok — ama mimari onlara hazır.

---

## 2. Mevcut proje durumu (tespit edilen)

| Ayar | Değer | Not |
|---|---|---|
| Unity | 6000.3.21f1 (Unity 6.3 LTS) | ✔ |
| Render Pipeline | URP 17.3 (PC_RPAsset / Mobile_RPAsset) | ✔ |
| Input | `activeInputHandler: 1` → **yalnızca yeni Input System** | Kodlar `Mouse.current` kullanır |
| Color Space | Linear | ✔ |
| Test Framework | 1.6.0 kurulu | ✔ |

Ek paket gerekmiyor.

### Önerilen Unity ayarları
- **Edit > Project Settings > Player > Company Name**: kendi adın (kayıt dosyası yolu `Application.persistentDataPath` buna bağlı olacak — Faz 9'dan önce belirlemek iyi olur).
- **Quality**: PC kalite seviyesi aktif olsun (Windows için varsayılan).
- Kurulum aracı aktif URP asset'inde **Shadows > Max Distance** değerini 400 m'ye yükseltir (varsayılan ~50 m dağ ölçeği için kısa).
- İsteğe bağlı: `Assets/TutorialInfo` ve `Assets/Readme.asset` şablon dosyalarıdır, silinebilir. `SampleScene` artık kullanılmayacak.

---

## 3. Klasör ve dosya yapısı

```
Assets/
  Scripts/
    MountainEater.Runtime.asmdef      ← oyun kodu assembly'si
    Core/
      GameBootstrap.cs                ← başlatma sırası (composition root)
      PointerUtility.cs               ← "imleç UI üzerinde mi?" (kamera + ileride kazı)
      DebugOverlay.cs                 ← GELİŞTİRME: FPS + kontroller (F1)
    Camera/
      OrbitCameraController.cs        ← kamera davranışı ve girdi
      OrbitCameraMath.cs              ← saf matematik (test edilebilir)
    World/
      MountainShapeSettings.cs        ← dağ ayarları (ScriptableObject)
      MountainShape.cs                ← deterministik (x,z) → yükseklik
      PlaceholderMountainGenerator.cs ← GEÇİCİ mesh dağ (Faz 2'de kalkacak)
    Editor/
      MountainEater.Editor.asmdef
      MountainEaterSceneSetup.cs      ← Menü: Mountain Eater > Setup Phase 1 Scene
    Voxel/ Mining/ Economy/ Upgrades/ Machines/ Regions/ UI/ SaveSystem/   (sonraki fazlar)
  Shaders/
    VertexColorLit.shader
  Tests/EditMode/
    MountainEater.Tests.EditMode.asmdef
    OrbitCameraMathTests.cs
    MountainShapeTests.cs
  Materials/          M_Mountain.mat, M_Ground.mat      (araç oluşturur)
  ScriptableObjects/World/MountainShapeSettings.asset   (araç oluşturur)
  Scenes/Main.unity                                     (araç oluşturur)
  Prefabs/ Meshes/ Audio/ VFX/
Docs/
  Faz1_Kurulum.md
```

> Not: Klasör adı `Camera`, namespace ise `MountainEater.Cameras`. `MountainEater.Camera` namespace'i
> Unity'nin `Camera` sınıfını gölgelerdi.

---

## 4. Mimari kararlar

**Assembly Definition'lar.** Oyun kodu `MountainEater.Runtime`, editör kodu `MountainEater.Editor`,
testler `MountainEater.Tests.EditMode`. Faydası: daha hızlı derleme, testlerin oyun koduna referans
verebilmesi (Assembly-CSharp'a test assembly'sinden referans verilemez) ve bağımlılıkların açık olması.

**GameBootstrap (composition root).** Sistemler birbirini `FindObjectOfType` ile aramıyor; sahnedeki tek bir
nesne referansları tutar ve başlatma sırasını belirler. Şu an: `mountain.Generate()` → `camera.FrameBounds(...)`.
Faz 2'de buraya VoxelWorld, sonra Economy/Regions/Save eklenecek. Singleton kullanmıyoruz.

**MountainShape saf C#.** Dağ şekli MonoBehaviour'dan ayrı, ayarları kurucuda kopyalıyor:
- Aynı seed → aynı dağ (testle doğrulanıyor). **Faz 9 kayıt sistemi** bunun üzerine kurulacak:
  dağın tamamını değil, yalnızca seed + değişen voxelleri kaydedeceğiz.
- Faz 2'de voxel üreticisi aynı fonksiyonu "yoğunluk" hesabı için kullanacak.
- Kopyalanmış alanlar sayesinde ileride Job System'e taşımak kolay.

**Kamera: hedef + yumuşatma.** Girdi yalnızca hedef değerleri (yaw, pitch, distance, focus) değiştirir;
gerçek değerler `1 - e^(-sharpness·dt)` ile yaklaşır → FPS'ten bağımsız yumuşaklık (testli).
- Zoom çarpımsaldır: yakında hassas, uzakta hızlı.
- Kaydırma, odak düzleminde **piksel başına dünya birimi** ile ölçeklenir: tutulan nokta imleci izler,
  çözünürlükten bağımsızdır. Döndürme hassasiyeti de 1080p'ye göre normalize edilir.
- **Sol tık kullanılmıyor** — Faz 3'te kazı için ayrıldı. `IsDragging` özelliği, kazı kontrolcüsünün
  kamera sürüklenirken vuruş yapmamasını sağlamak için hazır.
- UI üzerinde başlayan sürükleme / tekerlek yok sayılır (`PointerUtility`).

**Dağın altına geçme engeli.** Üç katman:
1. `minPitch = 3°` → kamera ufkun altına inmez.
2. `groundHeight + minClearance` → zemin altına inmez.
3. `keepAboveTerrain`: kameranın tam üstünden aşağı raycast atılır, kamera arazi yüzeyinin üstünde tutulur.
   Yukarı kaldırma anında (içine girmesin), geri bırakma yumuşak.

   ⚠ Faz 3'te tüneller açıldığında bu kural kameranın tünel *içine* girmesini engeller (tünelin girişini
   dışarıdan görmek mümkün). Tünel içine kamera sokmak istersek o fazda bu kuralı değiştireceğiz.

**Vertex color shader.** URP/Lit köşe rengini göstermez. Tüm dağı tek materyal + köşe renkleriyle çizmek,
Faz 5'te maden renkleri için de doğru yaklaşım (az draw call). Shader; ana ışık, gölge alma/verme, ortam
ışığı, sis, DepthOnly ve DepthNormals (SSAO) pass'lerini destekler ve SRP Batcher uyumludur.

**Geçici vs. kalıcı:**

| Kalıcı | Geçici |
|---|---|
| OrbitCameraController, OrbitCameraMath | PlaceholderMountainGenerator (Faz 2) |
| MountainShape, MountainShapeSettings | DebugOverlay (Faz 8) |
| GameBootstrap, PointerUtility | Ground (düz plane) |
| VertexColorLit.shader | |

---

## 5. Unity Editor'da kurulum

### A) Otomatik (önerilen)
1. Unity'ye geç; script'lerin derlenmesini bekle (Console'da kırmızı hata olmamalı).
2. Üst menü: **Mountain Eater > Setup Phase 1 Scene**
3. Araç şunları yapar:
   - Eksik klasörleri oluşturur
   - `MountainShapeSettings.asset`, `M_Mountain.mat` (VertexColorLit), `M_Ground.mat` (URP/Lit) oluşturur
   - `Assets/Scenes/Main.unity` sahnesini kurar ve Build Settings'te ilk sıraya ekler
   - URP gölge mesafesini 400 m yapar
4. **Play**.

### B) Elle kurulum (aracın ne yaptığını anlamak için)
1. `File > New Scene > Empty` → `Assets/Scenes/Main.unity` olarak kaydet.
2. **Directional Light**: Rotation (45, -35, 0), Intensity 1.3, Shadow Type: Soft Shadows.
3. **Window > Rendering > Lighting > Environment**: Sun Source = Directional Light,
   Environment Lighting Source = **Gradient** (Sky 0.55/0.64/0.78, Equator 0.44/0.45/0.43, Ground 0.24/0.22/0.20).
   Fog açık, Linear, Start 350, End 1400.
4. **Ground**: `3D Object > Plane`, Position (0, -0.05, 0), Scale (60, 1, 60), materyal: URP/Lit yeşilimsi.
5. **Project**: `Create > Mountain Eater > Mountain Shape Settings` → `Assets/ScriptableObjects/World/`.
6. **Project**: `Create > Material` → Shader: `MountainEater/VertexColorLit` → `M_Mountain`.
7. **Mountain**: boş GameObject (0,0,0) → `PlaceholderMountainGenerator` ekle
   (MeshFilter + MeshRenderer otomatik gelir). Shape Settings ve Material alanlarını ata.
8. **Main Camera**: Tag `MainCamera`, FOV 50, Near 0.3, Far 2000, `OrbitCameraController` ekle.
9. **EventSystem**: `UI > Event System` (Input System kullandığı için Unity "Replace with InputSystemUIInputModule"
   önerirse kabul et).
10. **GameBootstrap**: boş GameObject → `GameBootstrap` + `DebugOverlay`.
    Mountain → Mountain alanına, Main Camera → Orbit Camera alanına sürükle.
11. URP asset (`Assets/Settings/PC_RPAsset`) → Shadows → Max Distance: 400.

### Inspector değerleri (varsayılanlar zaten doğru)

**OrbitCameraController** — `FrameBounds` Play'de başlangıç odağı/mesafe/sınırları dağa göre yeniden hesaplar.

| Alan | Değer | Açıklama |
|---|---|---|
| Orbit Sensitivity | 0.25 | 1080p'de derece/piksel |
| Min / Max Pitch | 3 / 85 | dikey açı sınırı |
| Min / Max Distance | 6 / 350 | Max, dağ boyutuna göre gerekirse büyütülür |
| Zoom Step | 0.12 | çentik başına %12 |
| Rotation / Zoom / Pan Sharpness | 14 / 10 / 14 | büyük = daha çabuk |
| Min Clearance | 1.5 | zemin/arazi üstü güvenlik payı |
| Keep Above Terrain | ✔ | Terrain Mask: Default |
| Ignore Input Over UI | ✔ | |
| Reset Key | F | |

**MountainShapeSettings** — `Seed` değiştirip Play'e basınca farklı dağ gelir. Footprint Radius 75, Peak Height 60.

**PlaceholderMountainGenerator** — Resolution 256. Edit modda önizleme: bileşenin ⋮ menüsü →
*Generate Preview (Edit Mode)* (sahneye kaydedilmez).

---

## 6. Test

### Otomatik testler
`Window > General > Test Runner` → **EditMode** → **Run All**. Beklenen: 15 test yeşil.
- Kamera: mesafe, açı yönü, scroll normalizasyonu (±1 ve ±120), zoom sınırları, FPS'ten bağımsız yumuşatma, piksel→dünya dönüşümü
- Dağ: aynı seed = aynı dağ, farklı seed = farklı dağ, taban dışında yükseklik 0, negatif yükseklik yok

### Elle test listesi
| # | Test | Beklenen |
|---|---|---|
| 1 | Play | Dağ ekranı dolduruyor, gölgeler var, yamaçlarda çimen, tepede kar |
| 2 | Sağ tık + sürükle sağa/sola | Dağın etrafında yumuşak dönüş |
| 3 | Sağ tık + sürükle yukarı/aşağı | Açı değişir; tam tepeye (85°) ve ufka (3°) kadar, öteye geçmez |
| 4 | Tekerlek | Yakınlaş/uzaklaş, min ve max'ta durur |
| 5 | Orta tık + sürükle | Tutulan nokta imleci izler; odak sınırların dışına kaçmaz |
| 6 | Alçak açıyla dağa yakınlaşıp etrafında dön | Kamera dağın/zeminin içine girmez, sırtların üstünden geçer |
| 7 | Sol tık | Hiçbir şey olmaz (Faz 3'e ayrıldı) |
| 8 | F | Başlangıç görünümüne yumuşakça döner |
| 9 | F1 | FPS paneli gizlenir/görünür |
| 10 | Game view çözünürlüğünü değiştir (1080p ↔ 4K) | Döndürme/kaydırma hissi aynı kalır |
| 11 | Inspector'da Seed değiştir + Play | Farklı dağ |

---

## 7. Sık karşılaşılabilecek hatalar

| Belirti | Sebep / Çözüm |
|---|---|
| `Mountain Eater` menüsü yok | Console'da derleme hatası var; önce onu düzelt. |
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class` | Eski Input API kullanan bir script var (ör. başka bir şablon). Bizim kodlar yalnızca Input System kullanır. |
| Dağ pembe/mor | Shader derlenemedi. `M_Mountain` materyalinin shader'ını kontrol et; Console'daki shader hatasını bana ilet. Geçici çözüm: materyalin shader'ını `Universal Render Pipeline/Lit` yap (renkler kaybolur). |
| Dağ siyah / çok karanlık | Lighting > Environment Source Gradient değil ya da ışık yok. |
| Uzaktan bakınca gölge yok | URP asset'inde Shadows > Max Distance küçük (400 yap). |
| Sağ tık çalışmıyor | Sahnede tam ekran bir UI paneli imleci yakalıyor olabilir; ya da Game penceresine odak verilmemiş (bir kez tıkla). |
| Zoom çok hızlı/yavaş | Zoom Step değerini ayarla (scroll ölçeği otomatik normalize ediliyor). |
| Kamera dağa yakınken zıplıyor | Keep Above Terrain kamerayı sırt üstüne kaldırıyor; Min Clearance'ı küçült veya Lift Release Sharpness'ı düşür. |

---

## 8. FAZ 1 Kabul Kriterleri

- [ ] Proje derleniyor, Console'da hata yok
- [ ] `Main.unity` Build Settings'te ilk sahne
- [ ] Seed'den prosedürel, asimetrik, geniş tabanlı dağ üretiliyor
- [ ] Sağ tık + sürükle ile dağın etrafında dönülüyor
- [ ] Tekerlekle zoom; min/max sınırları çalışıyor
- [ ] Orta tık + sürükle ile kaydırma; odak sınırları çalışıyor
- [ ] Kamera zeminin ve dağ yüzeyinin altına geçmiyor
- [ ] Sol tık kamera tarafından kullanılmıyor
- [ ] Hareketler yumuşak ve FPS'ten bağımsız
- [ ] EditMode testlerinin tamamı geçiyor
- [ ] 1080p'de ~60 FPS (DebugOverlay ile gözlem; gerçek ölçüm Profiler ile)

Hepsi tamamsa **Faz 2: Voxel dünya sistemi**ne geçebiliriz.
