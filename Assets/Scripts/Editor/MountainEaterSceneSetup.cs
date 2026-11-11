using System.IO;
using System.Linq;
using MountainEater.Cameras;
using MountainEater.Core;
using MountainEater.Machines;
using MountainEater.Mining;
using MountainEater.Regions;
using MountainEater.UI;
using MountainEater.Upgrades;
using MountainEater.Voxel;
using MountainEater.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MountainEater.EditorTools
{
    /// <summary>
    /// Menü: Mountain Eater > Setup Main Scene
    /// Klasör yapısını, materyalleri, ayar asset'lerini ve Main sahnesini tek tıkla oluşturur.
    /// Her faz sonunda güncellenir; elle kurulum adımları Docs/ klasöründeki faz rehberlerinde.
    /// Tekrar çalıştırmak güvenlidir: mevcut asset'ler korunur, sahne için onay istenir.
    /// </summary>
    public static class MountainEaterSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ShapeSettingsPath = "Assets/ScriptableObjects/World/MountainShapeSettings.asset";
        private const string MaterialDatabasePath = "Assets/ScriptableObjects/Voxel/VoxelMaterialDatabase.asset";
        private const string MountainMaterialPath = "Assets/Materials/M_Mountain.mat";
        private const string GroundMaterialPath = "Assets/Materials/M_Ground.mat";
        private const string ParticleMaterialPath = "Assets/Materials/M_MiningParticles.mat";
        private const string BalanceConfigPath = "Assets/ScriptableObjects/GameBalanceConfig.asset";
        private const string RegionLayoutPath = "Assets/ScriptableObjects/Regions/RegionLayout.asset";
        private const string PickaxeUpgradePath = "Assets/ScriptableObjects/Upgrades/Upgrade_PickaxeDamage.asset";
        private const string RadiusUpgradePath = "Assets/ScriptableObjects/Upgrades/Upgrade_MiningRadius.asset";
        private const string DrillDamageUpgradePath = "Assets/ScriptableObjects/Upgrades/Upgrade_DrillDamage.asset";
        private const string DrillSpeedUpgradePath = "Assets/ScriptableObjects/Upgrades/Upgrade_DrillSpeed.asset";
        private const string MiniDrillPath = "Assets/ScriptableObjects/Machines/Machine_MiniDrill.asset";
        private const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";
        private const string ThemePath = "Assets/UI/MountainEaterTheme.tss";
        private const string HudUxmlPath = "Assets/UI/MainHud.uxml";
        private const float RecommendedShadowDistance = 400f;

        private static readonly string[] Folders =
        {
            "Assets/Scripts/Core",
            "Assets/Scripts/World",
            "Assets/Scripts/Voxel",
            "Assets/Scripts/Mining",
            "Assets/Scripts/Camera",
            "Assets/Scripts/Economy",
            "Assets/Scripts/Upgrades",
            "Assets/Scripts/Machines",
            "Assets/Scripts/Regions",
            "Assets/Scripts/UI",
            "Assets/Scripts/SaveSystem",
            "Assets/Scripts/Editor",
            "Assets/Prefabs",
            "Assets/Materials",
            "Assets/Meshes",
            "Assets/Shaders",
            "Assets/ScriptableObjects/World",
            "Assets/ScriptableObjects/Voxel",
            "Assets/ScriptableObjects/Upgrades",
            "Assets/ScriptableObjects/Regions",
            "Assets/ScriptableObjects/Machines",
            "Assets/UI",
            "Assets/Scenes",
            "Assets/Audio",
            "Assets/VFX",
            "Assets/Tests/EditMode",
        };

        [MenuItem("Mountain Eater/Setup Main Scene", priority = 0)]
        public static void SetupMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Mountain Eater",
                    $"{ScenePath} zaten var. Üzerine yazılsın mı?\n(Materyaller ve ayar asset'leri korunur.)",
                    "Üzerine yaz", "İptal"))
            {
                return;
            }

            CreateFolders();

            // Asset'ler önce oluşturulup diske yazılır...
            LoadOrCreateAsset<MountainShapeSettings>(ShapeSettingsPath);
            LoadOrCreateAsset<VoxelMaterialDatabase>(MaterialDatabasePath);
            LoadOrCreateMaterial(MountainMaterialPath, "MountainEater/VertexColorLit", Color.white);
            LoadOrCreateMaterial(GroundMaterialPath, "Universal Render Pipeline/Lit", new Color(0.36f, 0.42f, 0.27f));
            LoadOrCreateMaterial(ParticleMaterialPath, "Universal Render Pipeline/Particles/Simple Lit", Color.white);
            CreateBalanceAssets();
            LoadOrCreateAsset<RegionLayout>(RegionLayoutPath);
            CreatePanelSettings();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ...ve yeni sahne açıldıktan SONRA yeniden yüklenir. NewScene(Single) kullanılmayan asset'leri
            // bellekten boşaltır; önceden tutulan referanslar "null" olur ve sahneye boş atanırdı.
            var shapeSettings = LoadRequired<MountainShapeSettings>(ShapeSettingsPath);
            var materialDatabase = LoadRequired<VoxelMaterialDatabase>(MaterialDatabasePath);
            var mountainMaterial = LoadRequired<Material>(MountainMaterialPath);
            var groundMaterial = LoadRequired<Material>(GroundMaterialPath);
            var particleMaterial = LoadRequired<Material>(ParticleMaterialPath);
            var balanceConfig = LoadRequired<GameBalanceConfig>(BalanceConfigPath);
            var panelSettings = LoadRequired<PanelSettings>(PanelSettingsPath);
            var hudTree = LoadRequired<VisualTreeAsset>(HudUxmlPath);
            var regionLayout = LoadRequired<RegionLayout>(RegionLayoutPath);
            if (shapeSettings == null || materialDatabase == null || mountainMaterial == null ||
                groundMaterial == null || particleMaterial == null || balanceConfig == null ||
                panelSettings == null || hudTree == null || regionLayout == null)
            {
                return;
            }

            Light sun = CreateSun();
            ConfigureEnvironment(sun);
            CreateGround(groundMaterial);
            ChunkManager chunkManager = CreateVoxelMountain(mountainMaterial);
            OrbitCameraController orbitCamera = CreateCamera();
            CreateEventSystem();
            (PlayerMiningController playerMining, MiningEffects effects) =
                CreateMining(orbitCamera, particleMaterial);
            HudController hud = CreateHud(panelSettings, hudTree);
            var regionOverlay = new GameObject("Regions").AddComponent<RegionOverlayController>();
            var machinesObject = new GameObject("Machines");
            var machineController = machinesObject.AddComponent<MachineController>();
            var placementController = machinesObject.AddComponent<MachinePlacementController>();
            CreateBootstrap(shapeSettings, materialDatabase, balanceConfig, regionLayout, chunkManager, playerMining,
                effects, orbitCamera, regionOverlay, hud);

            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            SetProperty(bootstrap, "machineController", machineController);
            SetProperty(bootstrap, "placementController", placementController);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            IncreaseShadowDistance();
            AssetDatabase.SaveAssets();

            Debug.Log("[Mountain Eater] Main sahnesi hazır: " + ScenePath + " — Play'e basarak test edebilirsin.");
        }

        private static void CreateFolders()
        {
            foreach (string folder in Folders)
            {
                EnsureFolder(folder);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"[Mountain Eater] Asset yüklenemedi: {path}. Kurulum durduruldu.");
            }

            return asset;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName, Color baseColor)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Mountain Eater] '{shaderName}' bulunamadı, URP/Lit kullanılıyor.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Light CreateSun()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static void ConfigureEnvironment(Light sun)
        {
            RenderSettings.sun = sun;
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");

            // Trilight ortam ışığı: Lighting > Generate gerektirmeden hemen doğru görünür.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.64f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.45f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.20f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.78f, 0.86f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 1400f;
        }

        private static void CreateGround(Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.05f, 0f); // dağ kenarıyla z-fighting olmasın
            ground.transform.localScale = new Vector3(60f, 1f, 60f);  // 600 x 600 m
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static ChunkManager CreateVoxelMountain(Material material)
        {
            var go = new GameObject("VoxelMountain");
            var chunkManager = go.AddComponent<ChunkManager>();
            SetProperty(chunkManager, "chunkMaterial", material);
            return chunkManager;
        }

        private static OrbitCameraController CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(new Vector3(0f, 80f, -170f), Quaternion.Euler(22f, 0f, 0f));
            return go.AddComponent<OrbitCameraController>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static (PlayerMiningController, MiningEffects) CreateMining(OrbitCameraController orbitCamera, Material particleMaterial)
        {
            var go = new GameObject("Mining");
            var effects = go.AddComponent<MiningEffects>();
            SetProperty(effects, "particleMaterial", particleMaterial);

            var controller = go.AddComponent<PlayerMiningController>();
            SetProperty(controller, "targetCamera", orbitCamera.GetComponent<Camera>());
            SetProperty(controller, "orbitCamera", orbitCamera);
            SetProperty(controller, "effects", effects);
            return (controller, effects);
        }

        private static HudController CreateHud(PanelSettings panelSettings, VisualTreeAsset hudTree)
        {
            var go = new GameObject("HUD");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = hudTree;
            return go.AddComponent<HudController>();
        }

        /// <summary>
        /// Geliştirme ve makine tanımları + merkezi denge yapılandırması. Mevcut asset'ler ve değerleri korunur;
        /// yalnızca eksik olan tanımlar oluşturulur ve config listelerine eklenir (önceki fazlardan yükseltme).
        /// </summary>
        private static void CreateBalanceAssets()
        {
            UpgradeDefinition[] upgrades =
            {
                LoadOrCreateUpgrade(PickaxeUpgradePath, UpgradeType.PickaxeDamage),
                LoadOrCreateUpgrade(RadiusUpgradePath, UpgradeType.MiningRadius),
                LoadOrCreateUpgrade(DrillDamageUpgradePath, UpgradeType.DrillDamage),
                LoadOrCreateUpgrade(DrillSpeedUpgradePath, UpgradeType.DrillSpeed),
            };

            var miniDrill = AssetDatabase.LoadAssetAtPath<MachineDefinition>(MiniDrillPath);
            if (miniDrill == null)
            {
                miniDrill = ScriptableObject.CreateInstance<MachineDefinition>();
                miniDrill.ApplyMiniDrillDefaults();
                AssetDatabase.CreateAsset(miniDrill, MiniDrillPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalanceConfigPath);
            bool isNew = config == null;
            if (isNew) config = ScriptableObject.CreateInstance<GameBalanceConfig>();

            bool changed = false;
            foreach (UpgradeDefinition upgrade in upgrades)
            {
                if (config.upgrades.Contains(upgrade)) continue;
                config.upgrades.Add(upgrade);
                changed = true;
            }

            if (!config.machines.Contains(miniDrill))
            {
                config.machines.Add(miniDrill);
                changed = true;
            }

            if (isNew) AssetDatabase.CreateAsset(config, BalanceConfigPath);
            else if (changed) EditorUtility.SetDirty(config);
        }

        private static UpgradeDefinition LoadOrCreateUpgrade(string path, UpgradeType type)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (definition != null) return definition;

            definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
            definition.ApplyDefaults(type);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        /// <summary>UI Toolkit panel ayarları: 1920×1080 referanslı ölçekleme + varsayılan çalışma zamanı teması.</summary>
        private static void CreatePanelSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) != null) return;

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (settings.themeStyleSheet == null)
            {
                Debug.LogWarning($"[Mountain Eater] Tema bulunamadı: {ThemePath}. UI varsayılan stilsiz görünebilir.");
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
        }

        private static void CreateBootstrap(MountainShapeSettings settings, VoxelMaterialDatabase materialDatabase,
            GameBalanceConfig balanceConfig, RegionLayout regionLayout, ChunkManager chunkManager,
            PlayerMiningController playerMining, MiningEffects effects, OrbitCameraController orbitCamera,
            RegionOverlayController regionOverlay, HudController hud)
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            SetProperty(bootstrap, "mountainSettings", settings);
            SetProperty(bootstrap, "materialDatabase", materialDatabase);
            SetProperty(bootstrap, "balanceConfig", balanceConfig);
            SetProperty(bootstrap, "regionLayout", regionLayout);
            SetProperty(bootstrap, "regionOverlay", regionOverlay);
            SetProperty(bootstrap, "chunkManager", chunkManager);
            SetProperty(bootstrap, "playerMining", playerMining);
            SetProperty(bootstrap, "miningEffects", effects);
            SetProperty(bootstrap, "orbitCamera", orbitCamera);
            SetProperty(bootstrap, "hud", hud);
            var overlay = go.AddComponent<DebugOverlay>();
            SetProperty(overlay, "chunkManager", chunkManager);
            SetProperty(overlay, "playerMining", playerMining);
        }

        private static void SetProperty(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[Mountain Eater] {target.GetType().Name}.{propertyName} alanı bulunamadı.");
                return;
            }

            if (value == null)
            {
                Debug.LogError($"[Mountain Eater] {target.GetType().Name}.{propertyName} için atanacak referans null.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// URP varsayılan gölge mesafesi (~50 m) dağ ölçeği için kısa; uzaktan bakınca gölge kaybolur.
        /// Aktif URP asset'inde "Max Distance" değerini en az 400 m yapar.
        /// </summary>
        private static void IncreaseShadowDistance()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;

            var serializedObject = new SerializedObject(pipeline);
            SerializedProperty property = serializedObject.FindProperty("m_ShadowDistance");
            if (property == null || property.floatValue >= RecommendedShadowDistance) return;

            property.floatValue = RecommendedShadowDistance;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipeline);
            Debug.Log($"[Mountain Eater] {pipeline.name} gölge mesafesi {RecommendedShadowDistance} m yapıldı.");
        }
    }
}
