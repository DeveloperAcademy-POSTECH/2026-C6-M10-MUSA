using UnityEngine;

namespace Hapio
{
    /// <summary>Menus stay inside the physical display cutout without changing saved child layout.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class GameSafeArea : MonoBehaviour
    {
        private Rect lastSafe;
        private int lastWidth, lastHeight;
        private void OnEnable() => Apply();
        private void Update()
        {
            if (lastSafe != Screen.safeArea || lastWidth != Screen.width || lastHeight != Screen.height) Apply();
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            lastSafe = safe; lastWidth = Screen.width; lastHeight = Screen.height;
        }
    }
}
