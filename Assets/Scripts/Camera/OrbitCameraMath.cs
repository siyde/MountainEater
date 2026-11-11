using UnityEngine;

namespace MountainEater.Cameras
{
    /// <summary>
    /// Kameranın saf matematik kısmı. MonoBehaviour'dan ayrıldı ki EditMode testleriyle
    /// sahne/kamera olmadan doğrulanabilsin.
    /// </summary>
    public static class OrbitCameraMath
    {
        /// <summary>Windows'ta bir tekerlek "çentiği" bazı ayarlarda 120 birim olarak gelir.</summary>
        public const float LegacyScrollUnitsPerNotch = 120f;

        /// <summary>Odak noktası, açı ve mesafeden kamera konumu.</summary>
        public static Vector3 ComputePosition(Vector3 focus, float yawDegrees, float pitchDegrees, float distance)
        {
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            return focus - rotation * Vector3.forward * distance;
        }

        /// <summary>
        /// Kare hızından bağımsız üstel yumuşatma. sharpness büyüdükçe hedefe daha hızlı yaklaşır.
        /// (Lerp(a, b, k) kullanmak FPS'e bağlı olurdu; 1 - e^(-s*dt) kullanmak olmaz.)
        /// </summary>
        public static float Damp(float current, float target, float sharpness, float deltaTime)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float deltaTime)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        /// <summary>
        /// Input System scroll değerini "çentik" sayısına çevirir.
        /// Input System sürümüne/ayarına göre bir çentik ±1 veya ±120 gelebilir; ikisini de destekler.
        /// </summary>
        public static float ScrollToNotches(float rawScroll)
        {
            if (Mathf.Abs(rawScroll) >= 20f)
            {
                return rawScroll / LegacyScrollUnitsPerNotch;
            }

            return rawScroll;
        }

        /// <summary>
        /// Çarpımsal zoom: her çentik mesafeyi yüzde olarak değiştirir. Böylece yakındayken hassas,
        /// uzaktayken hızlı zoom elde edilir. Pozitif çentik = yakınlaş.
        /// </summary>
        public static float ApplyZoom(float distance, float notches, float stepPercent, float minDistance, float maxDistance)
        {
            float factor = Mathf.Pow(1f - Mathf.Clamp(stepPercent, 0.001f, 0.9f), notches);
            return Mathf.Clamp(distance * factor, minDistance, maxDistance);
        }

        /// <summary>
        /// Odak düzleminde bir ekran pikselinin dünya birimi karşılığı. Kaydırma bununla çarpılınca
        /// tutulan nokta imleci takip eder ve sonuç ekran çözünürlüğünden bağımsız olur.
        /// </summary>
        public static float WorldUnitsPerPixel(float distance, float verticalFovDegrees, float screenHeightPixels)
        {
            float viewHeight = 2f * distance * Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            return viewHeight / Mathf.Max(1f, screenHeightPixels);
        }

        /// <summary>
        /// Verilen yarıçaptaki bir küreyi dikey ve yatay FOV içine sığdırmak için gereken mesafe.
        /// </summary>
        public static float DistanceToFitSphere(float radius, float verticalFovDegrees, float aspect)
        {
            float halfV = verticalFovDegrees * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * Mathf.Max(0.01f, aspect));
            float halfMin = Mathf.Min(halfV, halfH);
            return radius / Mathf.Sin(halfMin);
        }
    }
}
