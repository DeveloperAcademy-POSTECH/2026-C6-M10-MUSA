using C6.Prototype.Orbs;
using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.Battle
{
    /// <summary>Pointer-following artwork only. The owned 2D body stays held until Host approval.</summary>
    public sealed class ThrowHeldPreview : MonoBehaviour
    {
        private RectTransform root;
        private OrbView source;
        private Renderer[] hidden;
        private Image[] images;
        public bool Visible => root != null && root.gameObject.activeSelf;
        public void Show(Canvas canvas, OrbView view, Vector2 screen, float radius)
        {
            if (canvas == null || view == null) return;
            if (source != view) { Hide(); source = view; hidden = view.GetComponentsInChildren<Renderer>(); }
            if (root == null)
            {
                root = new GameObject("Held Throw Preview", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                root.SetParent(canvas.transform, false);
                var group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                Disc("Hover Glow", view.RingRenderer.sprite, new Color(.3f, 1f, .85f, .23f), 1.55f);
                Disc("Held Combined", view.RingRenderer.sprite, new Color(.8f, 1f, .96f), 1f);
                Disc("Core", view.RingRenderer.sprite, new Color(.1f, .5f, .46f), .78f);
                images = root.GetComponentsInChildren<Image>();
            }
            foreach (var image in images) image.sprite = view.RingRenderer.sprite;
            foreach (var renderer in hidden) if (renderer != null) renderer.forceRenderingOff = true;
            root.gameObject.SetActive(true); root.SetAsLastSibling();
            root.sizeDelta = Vector2.one * radius * 2.5f / Mathf.Max(.1f, canvas.scaleFactor);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            root.anchoredPosition = local;
        }
        private void Disc(string label, Sprite sprite, Color color, float size)
        {
            var child = new GameObject(label, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(root, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one * (.5f - size * .5f); rect.anchorMax = Vector2.one * (.5f + size * .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false;
        }
        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
            if (hidden != null) foreach (var renderer in hidden) if (renderer != null) renderer.forceRenderingOff = false;
            hidden = null; source = null;
        }
        private void LateUpdate()
        {
            if (source == null) { Hide(); return; }
            if (root != null) root.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * .025f);
        }
        private void OnDisable() => Hide();
        private void OnDestroy() { Hide(); if (root != null) Destroy(root.gameObject); }
    }
}
