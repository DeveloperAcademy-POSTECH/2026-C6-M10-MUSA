using UnityEngine;

namespace C6.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UISafeArea : MonoBehaviour
    {
        private RectTransform panel;
        private Rect lastArea;
        private Vector2Int lastScreen;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            ApplyScreenSafeArea();
        }

        private void LateUpdate()
        {
            if (lastArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
                ApplyScreenSafeArea();
        }

        private void ApplyScreenSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            lastArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            panel.anchorMin = new Vector2(
                Mathf.Clamp01(lastArea.xMin / Screen.width),
                Mathf.Clamp01(lastArea.yMin / Screen.height));
            panel.anchorMax = new Vector2(
                Mathf.Clamp01(lastArea.xMax / Screen.width),
                Mathf.Clamp01(lastArea.yMax / Screen.height));
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }
    }
}
