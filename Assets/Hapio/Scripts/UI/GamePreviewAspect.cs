using UnityEngine;

namespace Hapio
{
    /// <summary>Fits the result image inside its saved viewport without stretching the captured gourd.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed class GamePreviewAspect : MonoBehaviour
    {
        private UnityEngine.UI.RawImage image;
        private UnityEngine.UI.AspectRatioFitter fitter;

        public static void Attach(UnityEngine.UI.RawImage target)
        {
            if (target == null) return;
            var aspect = target.GetComponent<GamePreviewAspect>();
            if (aspect == null) aspect = target.gameObject.AddComponent<GamePreviewAspect>();
            aspect.Initialize();
        }

        private void Initialize()
        {
            if (fitter != null) return;
            image = GetComponent<UnityEngine.UI.RawImage>();
            RectTransform rect = image.rectTransform;
            Transform parent = rect.parent;
            if (parent == null) return;

            // The outer Rect keeps the Scene's authored anchors, size, rotation and sibling
            // position. The same RawImage and gesture component remain connected inside it.
            var viewport = (RectTransform)new GameObject("Gourd preview aspect viewport", typeof(RectTransform)).transform;
            viewport.gameObject.layer = gameObject.layer;
            viewport.SetParent(parent, false);
            viewport.SetSiblingIndex(rect.GetSiblingIndex());
            viewport.anchorMin = rect.anchorMin;
            viewport.anchorMax = rect.anchorMax;
            viewport.pivot = rect.pivot;
            viewport.sizeDelta = rect.sizeDelta;
            viewport.anchoredPosition3D = rect.anchoredPosition3D;
            viewport.localRotation = rect.localRotation;
            viewport.localScale = rect.localScale;
            rect.SetParent(viewport, false);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.pivot = Vector2.one * .5f;
            fitter = GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (fitter == null) fitter = gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            RefreshAspect();
        }

        private void LateUpdate() => RefreshAspect();

        private void RefreshAspect()
        {
            if (fitter == null || image == null || image.texture == null || image.texture.height <= 0) return;
            float ratio = image.texture.width / (float)image.texture.height;
            if (!Mathf.Approximately(fitter.aspectRatio, ratio)) fitter.aspectRatio = ratio;
        }
    }
}
