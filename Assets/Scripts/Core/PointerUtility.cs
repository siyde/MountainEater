using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MountainEater.Core
{
    /// <summary>
    /// Fare imleci ile ilgili ortak yardımcılar.
    /// Kamera ve kazı aynı "UI üzerindeyken oyun girdisi yok" kuralını kullanır.
    ///
    /// İki tür UI desteklenir:
    /// - uGUI: EventSystem.IsPointerOverGameObject
    /// - UI Toolkit: HUD panelleri RegisterBlockingElement ile kaydolur; imleç görünür bir panelin
    ///   dikdörtgenindeyse UI üzerinde sayılır. (Tam ekran kapsayıcılar kaydedilmez, dağ tıklanabilir kalır.)
    /// </summary>
    public static class PointerUtility
    {
        private static readonly List<VisualElement> BlockingElements = new List<VisualElement>();

        public static void RegisterBlockingElement(VisualElement element)
        {
            if (element != null && !BlockingElements.Contains(element)) BlockingElements.Add(element);
        }

        public static void UnregisterBlockingElement(VisualElement element)
        {
            BlockingElements.Remove(element);
        }

        /// <summary>İmleç bir UI elemanının üzerindeyse true döner.</summary>
        public static bool IsPointerOverUI()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return true;

            if (BlockingElements.Count == 0) return false;

            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            Vector2 screen = mouse.position.ReadValue();
            // Input System: sol-alt köken. UI Toolkit panel dönüşümü sol-üst köken bekler.
            var screenTopLeft = new Vector2(screen.x, Screen.height - screen.y);

            for (int i = BlockingElements.Count - 1; i >= 0; i--)
            {
                VisualElement element = BlockingElements[i];
                if (element == null)
                {
                    BlockingElements.RemoveAt(i);
                    continue;
                }

                if (element.panel == null) continue;
                if (element.resolvedStyle.display == DisplayStyle.None) continue;
                if (element.resolvedStyle.visibility == Visibility.Hidden) continue;

                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(element.panel, screenTopLeft);
                if (element.worldBound.Contains(panelPosition)) return true;
            }

            return false;
        }
    }
}
