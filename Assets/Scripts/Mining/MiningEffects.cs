using MountainEater.Voxel;
using UnityEngine;

namespace MountainEater.Mining
{
    /// <summary>
    /// Kazı efektleri: vuruşta küçük toz/kıvılcım, kırılmada büyük kaya parçaları, kısa ses.
    ///
    /// Performans: her efekt için yeni nesne oluşturulmaz. İki kalıcı ParticleSystem vardır; her vuruşta
    /// tıklanan noktaya taşınıp Emit() ile parçacık basılır (simülasyon dünya uzayında, eski parçacıklar
    /// yerinde kalır). Parçacık sayısı üst sınırlıdır. Bu, nesne havuzu (pooling) ihtiyacını ortadan kaldırır.
    ///
    /// Ses dosyası atanmazsa kısa bir "tık" ve "çatırtı" sesi kodla üretilir (geçici; Faz 11'de gerçek sesler).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiningEffects : MonoBehaviour
    {
        [SerializeField] private Material particleMaterial;

        [Header("Ses (boşsa geçici ses üretilir)")]
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        [Header("Parçacıklar")]
        [SerializeField, Min(0)] private int hitParticles = 6;
        [SerializeField, Min(0)] private int breakParticlesPerVoxel = 5;
        [SerializeField, Min(1)] private int maxBreakParticlesPerHit = 60;

        private ParticleSystem hitSystem;
        private ParticleSystem breakSystem;
        private AudioSource audioSource;
        private VoxelMaterialTable materialTable;

        /// <summary>Malzeme renkleri ve voxel boyutuna göre efektleri hazırlar.</summary>
        public void Initialize(VoxelMaterialTable table, float voxelSize)
        {
            materialTable = table;

            Material material = ResolveMaterial();
            Mesh cube = GetCubeMesh();

            if (hitSystem == null)
            {
                hitSystem = CreateSystem("HitParticles", material, cube, maxParticles: 200, coneAngle: 40f, gravity: 1.2f);
                breakSystem = CreateSystem("BreakParticles", material, cube, maxParticles: 600, coneAngle: 70f, gravity: 2f);
            }

            ConfigureScale(hitSystem, voxelSize, sizeMin: 0.06f, sizeMax: 0.14f, speedMin: 1.5f, speedMax: 4f, lifeMin: 0.3f, lifeMax: 0.6f);
            ConfigureScale(breakSystem, voxelSize, sizeMin: 0.15f, sizeMax: 0.35f, speedMin: 2f, speedMax: 6f, lifeMin: 0.6f, lifeMax: 1.2f);

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            if (hitClip == null) hitClip = CreateClip("GeneratedHit", 0.10f, 900f, 45f, 1);
            if (breakClip == null) breakClip = CreateClip("GeneratedBreak", 0.30f, 140f, 14f, 2);
        }

        public void Play(Vector3 point, Vector3 normal, in MiningResult result)
        {
            if (hitSystem == null) return;

            Color32 color = materialTable != null ? materialTable.GetColor(result.TopMaterial) : new Color32(128, 122, 115, 255);
            Quaternion rotation = normal.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(normal) : Quaternion.identity;

            Emit(hitSystem, point, rotation, color, hitParticles);

            if (result.VoxelsBroken > 0)
            {
                int count = Mathf.Min(maxBreakParticlesPerHit, result.VoxelsBroken * breakParticlesPerVoxel);
                Emit(breakSystem, point, rotation, color, count);
                PlaySound(breakClip, 0.85f, 1.1f);
            }
            else
            {
                PlaySound(hitClip, 0.9f, 1.15f);
            }
        }

        /// <summary>
        /// Makine vuruşu: daha az parçacık, sessiz vuruş; kırılmada kısık ses (çok sayıda makinede gürültü olmasın,
        /// en fazla saniyede ~8 makine sesi).
        /// </summary>
        public void PlayMachineHit(Vector3 point, Vector3 normal, in MiningResult result)
        {
            if (hitSystem == null) return;

            Color32 color = materialTable != null ? materialTable.GetColor(result.TopMaterial) : new Color32(128, 122, 115, 255);
            Quaternion rotation = normal.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(normal) : Quaternion.identity;
            Emit(hitSystem, point, rotation, color, Mathf.Max(1, hitParticles / 2));

            if (result.VoxelsBroken <= 0) return;
            Emit(breakSystem, point, rotation, color, Mathf.Min(maxBreakParticlesPerHit, result.VoxelsBroken * 3));

            if (Time.unscaledTime - lastMachineSoundTime >= 0.12f && breakClip != null && audioSource != null)
            {
                lastMachineSoundTime = Time.unscaledTime;
                audioSource.pitch = Random.Range(1.05f, 1.25f);
                audioSource.PlayOneShot(breakClip, volume * 0.35f);
            }
        }

        private float lastMachineSoundTime = -1f;

        private static void Emit(ParticleSystem system, Vector3 point, Quaternion rotation, Color32 color, int count)
        {
            if (count <= 0) return;
            system.transform.SetPositionAndRotation(point, rotation);
            var emitParams = new ParticleSystem.EmitParams { startColor = color };
            system.Emit(emitParams, count);
        }

        private void PlaySound(AudioClip clip, float pitchMin, float pitchMax)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = Random.Range(pitchMin, pitchMax);
            audioSource.PlayOneShot(clip, volume);
        }

        private ParticleSystem CreateSystem(string systemName, Material material, Mesh mesh, int maxParticles, float coneAngle, float gravity)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false; // yalnızca Emit() ile basılır

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = mesh;
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            return system;
        }

        private static void ConfigureScale(ParticleSystem system, float voxelSize, float sizeMin, float sizeMax,
            float speedMin, float speedMax, float lifeMin, float lifeMax)
        {
            float scale = Mathf.Max(0.1f, voxelSize);
            ParticleSystem.MainModule main = system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin * scale, sizeMax * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin * scale, speedMax * scale);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        }

        private Material ResolveMaterial()
        {
            if (particleMaterial != null) return particleMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            particleMaterial = new Material(shader) { name = "GeneratedParticleMaterial" };
            return particleMaterial;
        }

        private static Mesh GetCubeMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        /// <summary>GEÇİCİ: gürültü + düşük ton, üstel sönümlü kısa ses.</summary>
        private static AudioClip CreateClip(string clipName, float duration, float toneHz, float decay, int seed)
        {
            const int sampleRate = 44100;
            int length = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[length];
            var rng = new System.Random(seed);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * decay);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float tone = Mathf.Sin(2f * Mathf.PI * toneHz * t);
                data[i] = (0.6f * noise + 0.4f * tone) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create(clipName, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
