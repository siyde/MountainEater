using MountainEater.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MountainEater.Cameras
{
    /// <summary>
    /// Dağın etrafında dönen bağımsız kamera.
    ///   Sağ tık + sürükle  : döndürme (yaw / pitch)
    ///   Orta tık + sürükle : odak noktasını ekran düzleminde kaydırma
    ///   Tekerlek           : yakınlaş / uzaklaş
    ///   F                  : başlangıç görünümüne dön
    ///
    /// Tasarım:
    /// - Girdi "hedef" değerleri değiştirir; gerçek değerler her karede hedefe yumuşakça yaklaşır.
    /// - Sol tık BİLİNÇLİ OLARAK kullanılmaz; Faz 3'te kazı için ayrılmıştır.
    /// - Zemin ve arazi koruması kameranın dağın/zeminin altına geçmesini engeller.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class OrbitCameraController : MonoBehaviour
    {
        [Header("Başlangıç görünümü")]
        [SerializeField] private Vector3 startFocus = new Vector3(0f, 20f, 0f);
        [SerializeField] private float startYaw = 35f;
        [SerializeField, Range(-89f, 89f)] private float startPitch = 22f;
        [SerializeField, Min(0.1f)] private float startDistance = 170f;

        [Header("Odak sınırları (FrameBounds çağrılınca otomatik ayarlanır)")]
        [SerializeField] private Bounds focusBounds = new Bounds(new Vector3(0f, 35f, 0f), new Vector3(220f, 70f, 220f));

        [Header("Döndürme - Sağ tık")]
        [Tooltip("1080p ekranda piksel başına derece. Diğer çözünürlüklerde otomatik ölçeklenir.")]
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.25f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(-89f, 89f)] private float minPitch = 3f;
        [SerializeField, Range(-89f, 89f)] private float maxPitch = 85f;

        [Header("Zoom - Tekerlek")]
        [SerializeField, Min(0.1f)] private float minDistance = 6f;
        [SerializeField, Min(1f)] private float maxDistance = 350f;
        [Tooltip("Her tekerlek çentiğinde mesafenin değişme oranı.")]
        [SerializeField, Range(0.01f, 0.5f)] private float zoomStep = 0.12f;

        [Header("Kaydırma - Orta tık")]
        [SerializeField, Min(0.01f)] private float panSensitivity = 1f;

        [Header("Yumuşatma (büyük = daha hızlı)")]
        [SerializeField, Min(0.1f)] private float rotationSharpness = 14f;
        [SerializeField, Min(0.1f)] private float zoomSharpness = 10f;
        [SerializeField, Min(0.1f)] private float panSharpness = 14f;

        [Header("Zemin / arazi koruması")]
        [SerializeField] private float groundHeight;
        [SerializeField, Min(0f)] private float minClearance = 1.5f;
        [Tooltip("Kameranın altındaki arazi yüzeyini raycast ile bulur ve kamerayı yüzeyin üstünde tutar.")]
        [SerializeField] private bool keepAboveTerrain = true;
        [SerializeField] private LayerMask terrainMask = 1; // Default layer
        [SerializeField, Min(0.1f)] private float liftReleaseSharpness = 6f;

        [Header("Girdi")]
        [Tooltip("UI üzerinde başlayan sürükleme ve tekerlek hareketleri yok sayılır.")]
        [SerializeField] private bool ignoreInputOverUI = true;
        [SerializeField] private Key resetKey = Key.F;

        private const float ReferenceScreenHeight = 1080f;
        private const float ProbeHeightMargin = 200f;

        private Camera cam;

        // Gerçek (yumuşatılmış) değerler
        private float yaw;
        private float pitch;
        private float distance;
        private Vector3 focus;
        private float currentLift;

        // Girdinin değiştirdiği hedef değerler
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;
        private Vector3 targetFocus;

        private bool isOrbiting;
        private bool isPanning;

        public Vector3 Focus => focus;
        public float Distance => distance;

        /// <summary>
        /// İsteğe bağlı arazi yüksekliği kaynağı: dünya konumu → altındaki yüzeyin yüksekliği
        /// (yoksa float.NegativeInfinity). Atanırsa fizik raycast'i yerine kullanılır.
        /// GameBootstrap bunu voxel verisine (ChunkManager.GetSurfaceHeight) bağlar; kamera voxel'i bilmez.
        /// </summary>
        public System.Func<Vector3, float> TerrainHeightProvider { get; set; }

        /// <summary>
        /// Kamera sürüklenirken true. Faz 3'te kazı kontrolcüsü bunu kontrol ederek
        /// kamera hareketi sırasında yanlış vuruşları engelleyebilir.
        /// </summary>
        public bool IsDragging => isOrbiting || isPanning;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ResetView(snap: true);
        }

        private void OnValidate()
        {
            if (maxPitch < minPitch) maxPitch = minPitch;
            if (maxDistance < minDistance) maxDistance = minDistance;
        }

        /// <summary>
        /// Kamerayı verilen sınırlara (ör. dağın Bounds'u) göre yeniden yapılandırır:
        /// odak sınırları, başlangıç odağı, başlangıç mesafesi ve maksimum zoom.
        /// </summary>
        public void FrameBounds(Bounds bounds, bool snap)
        {
            if (cam == null) cam = GetComponent<Camera>();

            groundHeight = bounds.min.y;

            // Odak noktası dağın biraz dışına kadar gidebilir ama sonsuza kaçamaz.
            Vector3 size = bounds.size;
            focusBounds = new Bounds(bounds.center, new Vector3(size.x * 1.3f, size.y, size.z * 1.3f));

            startFocus = new Vector3(bounds.center.x, bounds.min.y + size.y * 0.35f, bounds.center.z);

            float fitDistance = OrbitCameraMath.DistanceToFitSphere(bounds.extents.magnitude, cam.fieldOfView, cam.aspect);
            startDistance = fitDistance * 0.75f;
            maxDistance = Mathf.Max(maxDistance, fitDistance * 1.5f);
            minDistance = Mathf.Min(minDistance, maxDistance);

            float farNeeded = maxDistance + bounds.extents.magnitude * 2f;
            if (cam.farClipPlane < farNeeded) cam.farClipPlane = farNeeded;

            ResetView(snap);
        }

        /// <summary>Başlangıç yatay açısını ayarlar (ör. kamera başlangıçta açık bölgeye baksın). FrameBounds'tan önce çağır.</summary>
        public void SetStartYaw(float yawDegrees)
        {
            startYaw = Mathf.Repeat(yawDegrees, 360f);
        }

        /// <summary>Başlangıç görünümüne döner.</summary>
        public void ResetView(bool snap)
        {
            targetFocus = ClampFocus(startFocus);
            targetYaw = startYaw;
            targetPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
            targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);

            if (snap)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                distance = targetDistance;
                focus = targetFocus;
                currentLift = 0f;
                ApplyTransform(0f);
            }
        }

        /// <summary>Odak noktasını değiştirir (ileride ör. seçilen makineye odaklanmak için).</summary>
        public void SetFocus(Vector3 worldPoint, bool snap)
        {
            targetFocus = ClampFocus(worldPoint);
            if (snap) focus = targetFocus;
        }

        private void LateUpdate()
        {
            // unscaledDeltaTime: oyun duraklatılsa (timeScale = 0) bile kamera çalışmaya devam eder.
            float dt = Time.unscaledDeltaTime;

            HandleInput();
            SmoothTowardsTargets(dt);
            ApplyTransform(dt);
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !Application.isFocused)
            {
                isOrbiting = false;
                isPanning = false;
                return;
            }

            bool overUI = ignoreInputOverUI && PointerUtility.IsPointerOverUI();

            // Sürükleme yalnızca tuşa basıldığı karede UI üzerinde değilse başlar;
            // başladıktan sonra imleç UI üzerinden geçse de devam eder.
            if (mouse.rightButton.wasPressedThisFrame && !overUI) isOrbiting = true;
            if (!mouse.rightButton.isPressed) isOrbiting = false;

            if (mouse.middleButton.wasPressedThisFrame && !overUI) isPanning = true;
            if (!mouse.middleButton.isPressed) isPanning = false;

            Vector2 delta = mouse.delta.ReadValue();
            float screenHeight = Mathf.Max(1, Screen.height);

            if (isOrbiting && delta.sqrMagnitude > 0f)
            {
                float scale = orbitSensitivity * (ReferenceScreenHeight / screenHeight);
                targetYaw += delta.x * scale;
                float pitchDelta = delta.y * scale * (invertY ? 1f : -1f);
                targetPitch = Mathf.Clamp(targetPitch + pitchDelta, minPitch, maxPitch);
            }

            if (isPanning && delta.sqrMagnitude > 0f)
            {
                float unitsPerPixel = OrbitCameraMath.WorldUnitsPerPixel(distance, cam.fieldOfView, screenHeight);
                Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 move = (rotation * Vector3.right * delta.x + rotation * Vector3.up * delta.y)
                               * (unitsPerPixel * panSensitivity);
                // "Dünyayı tut ve çek" hissi: fare sağa giderse dünya sağa, odak sola kayar.
                targetFocus = ClampFocus(targetFocus - move);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f) && !overUI)
            {
                float notches = OrbitCameraMath.ScrollToNotches(scroll);
                targetDistance = OrbitCameraMath.ApplyZoom(targetDistance, notches, zoomStep, minDistance, maxDistance);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && resetKey != Key.None && keyboard[resetKey].wasPressedThisFrame)
            {
                ResetView(snap: false);
            }
        }

        private void SmoothTowardsTargets(float dt)
        {
            // Yaw sınırsız büyümesin; hedef ve mevcut değer aynı miktarda kaydırılır, görüntü değişmez.
            if (Mathf.Abs(targetYaw) > 360f)
            {
                float wrap = Mathf.Floor(targetYaw / 360f) * 360f;
                targetYaw -= wrap;
                yaw -= wrap;
            }

            yaw = OrbitCameraMath.Damp(yaw, targetYaw, rotationSharpness, dt);
            pitch = OrbitCameraMath.Damp(pitch, targetPitch, rotationSharpness, dt);
            distance = OrbitCameraMath.Damp(distance, targetDistance, zoomSharpness, dt);
            focus = OrbitCameraMath.Damp(focus, targetFocus, panSharpness, dt);
        }

        private void ApplyTransform(float dt)
        {
            Vector3 position = OrbitCameraMath.ComputePosition(focus, yaw, pitch, distance);

            float minY = groundHeight + minClearance;
            if (keepAboveTerrain && TerrainHeightProvider != null)
            {
                float surface = TerrainHeightProvider(position);
                if (!float.IsNegativeInfinity(surface)) minY = Mathf.Max(minY, surface + minClearance);
            }
            else if (keepAboveTerrain)
            {
                float probeTop = Mathf.Max(position.y, focusBounds.max.y) + ProbeHeightMargin;
                var origin = new Vector3(position.x, probeTop, position.z);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeTop - groundHeight + ProbeHeightMargin,
                        terrainMask, QueryTriggerInteraction.Ignore))
                {
                    minY = Mathf.Max(minY, hit.point.y + minClearance);
                }
            }

            // Arazinin içine girmemek için yukarı kaldırma anında, geri bırakma yumuşak yapılır.
            float requiredLift = Mathf.Max(0f, minY - position.y);
            currentLift = requiredLift >= currentLift
                ? requiredLift
                : OrbitCameraMath.Damp(currentLift, requiredLift, liftReleaseSharpness, dt);

            position.y += currentLift;

            Vector3 lookDirection = focus - position;
            Quaternion rotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection, Vector3.up)
                : Quaternion.Euler(pitch, yaw, 0f);

            transform.SetPositionAndRotation(position, rotation);
        }

        private Vector3 ClampFocus(Vector3 point)
        {
            Vector3 clamped = focusBounds.ClosestPoint(point);
            clamped.y = Mathf.Max(clamped.y, groundHeight);
            return clamped;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(focusBounds.center, focusBounds.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Application.isPlaying ? focus : startFocus, 1f);
        }
    }
}
