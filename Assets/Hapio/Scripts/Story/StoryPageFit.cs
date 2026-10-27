using UnityEngine;

namespace Hapio
{
    /// <summary>Keep Figma's authored coordinates while fitting the whole page into any viewport.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StoryPageFit : MonoBehaviour
    {
        public Vector2 referenceSize = new Vector2(402, 874);
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        public void Apply()
        {
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            if (parent == null || referenceSize.x <= 0 || referenceSize.y <= 0) return;
            float scale = Mathf.Min(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = referenceSize;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * Mathf.Max(0f, scale);
        }
    }
}
