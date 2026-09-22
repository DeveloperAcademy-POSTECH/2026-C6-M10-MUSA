using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Positions scene-authored, non-interactive artwork around the area in which orbs move.
    /// Colors, thickness and corner details remain editable on its RectTransform children.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class OrbWorkspaceBoundaryView : MonoBehaviour
    {
        [SerializeField] private T09Hud hud;
        [SerializeField] private RectTransform frame;

        public T09Hud Hud => hud;
        public RectTransform Frame => frame;

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Call after changing the camera split or footer layout.</summary>
        public void Refresh()
        {
            if (hud == null || hud.Canvas == null || frame == null || frame.parent is not RectTransform parent)
                return;

            Rect screenRect = hud.OrbWorkspaceScreenRect;
            if (screenRect.width <= 0f || screenRect.height <= 0f)
                return;

            Camera eventCamera = hud.Canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : hud.Canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenRect.min, eventCamera, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenRect.max, eventCamera, out Vector2 max))
                return;

            Vector2 position = (min + max) * 0.5f;
            Vector2 size = max - min;
            if ((frame.anchoredPosition - position).sqrMagnitude > 0.0001f)
                frame.anchoredPosition = position;
            if ((frame.sizeDelta - size).sqrMagnitude > 0.0001f)
                frame.sizeDelta = size;
        }

        public void SetReferences(T09Hud targetHud, RectTransform targetFrame)
        {
            hud = targetHud;
            frame = targetFrame;
        }
    }
}
