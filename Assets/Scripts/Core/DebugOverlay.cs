using MountainEater.Mining;
using MountainEater.Voxel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MountainEater.Core
{
    /// <summary>
    /// GELİŞTİRME ARACI: FPS ve kontrol ipuçlarını gösterir. F1 ile açılıp kapanır.
    /// Asıl oyun arayüzü Faz 8'de uGUI ile yapılacak; bu bileşen o zaman kaldırılabilir.
    /// Metin saniyede 4 kez güncellenir, böylece her karede string üretilmez.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private Key toggleKey = Key.F1;
        [Tooltip("İsteğe bağlı: voxel istatistiklerini göstermek için.")]
        [SerializeField] private ChunkManager chunkManager;
        [Tooltip("İsteğe bağlı: kazma bilgilerini göstermek için.")]
        [SerializeField] private PlayerMiningController playerMining;

        private const float RefreshInterval = 0.25f;

        private float smoothedDeltaTime = 1f / 60f;
        private float refreshTimer;
        private string text = string.Empty;
        private GUIStyle style;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, dt, 0.05f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                visible = !visible;
            }

            refreshTimer -= dt;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                float fps = 1f / Mathf.Max(0.0001f, smoothedDeltaTime);
                string voxelLine = chunkManager != null && chunkManager.World != null
                    ? $"Voxel: {chunkManager.World.SolidCount:N0} / {chunkManager.World.InitialSolidCount:N0}\n" +
                      $"Chunk: {chunkManager.MeshedChunkCount} yüzeyli, {chunkManager.PendingRebuildCount} bekleyen\n"
                    : string.Empty;
                string miningLine = string.Empty;
                if (playerMining != null)
                {
                    MiningResult last = playerMining.LastResult;
                    miningLine = $"Kazma: hasar {playerMining.PickaxeDamage}, yarıçap {playerMining.MiningRadius}\n" +
                                 $"Son vuruş: {last.VoxelsDamaged} hasar, {last.VoxelsBroken} kırık, +{last.Reward} $\n";
                }

                text = $"FPS: {fps:0}  ({smoothedDeltaTime * 1000f:0.0} ms)\n" + voxelLine + miningLine +
                       "Sol tık           : Kaz (tek vuruş)\n" +
                       "Sağ tık + sürükle : Döndür\n" +
                       "Orta tık + sürükle: Kaydır\n" +
                       "Tekerlek          : Yakınlaş / Uzaklaş\n" +
                       "F                 : Kamerayı sıfırla\n" +
                       "F1                : Bu paneli gizle";
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white }
            };

            // Sol alt köşe: üst köşeler HUD panellerine ait.
            const float width = 310f;
            const float height = 225f;
            GUI.Box(new Rect(10f, Screen.height - height - 10f, width, height), text, style);
        }
    }
}
