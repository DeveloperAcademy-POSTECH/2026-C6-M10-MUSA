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
            if (fitter == null || image == null) return;
            Vector2 frame = GameSession.Instance != null ? GameSession.Instance.PreviewFrameScale : Vector2.one;
            RefreshFrame(frame);
            if (image.texture == null || image.texture.height <= 0) return;
            // A larger frame keeps the authored square: fit its centre part, and draw the rest around it.
            float ratio = image.texture.width / frame.x / (image.texture.height / frame.y);
            if (!Mathf.Approximately(fitter.aspectRatio, ratio)) fitter.aspectRatio = ratio;
        }

        private UnityEngine.UI.RawImage frameImage;
        private Color imageColor;

        /// <summary>
        /// The fitted RawImage keeps its rect for gestures (rotation is measured in its size) and turns transparent;
        /// a non-raycast copy behind it shows the whole larger texture at the same scale and centre.
        /// </summary>
        private void RefreshFrame(Vector2 frame)
        {
            bool extended = image.texture != null && frame != Vector2.one;
            if (extended && frameImage == null)
            {
                imageColor = image.color;
                var rect = (RectTransform)new GameObject("Gourd preview frame", typeof(RectTransform)).transform;
                rect.gameObject.layer = gameObject.layer;
                rect.SetParent(image.rectTransform.parent, false);
                rect.SetSiblingIndex(image.rectTransform.GetSiblingIndex());
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                frameImage = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                frameImage.raycastTarget = false;
            }
            if (frameImage == null) return;
            frameImage.enabled = extended && image.enabled;
            image.color = extended ? Color.clear : imageColor;
            if (!extended) return;
            frameImage.texture = image.texture;
            frameImage.color = imageColor;
            frameImage.rectTransform.anchoredPosition = image.rectTransform.anchoredPosition;
            frameImage.rectTransform.sizeDelta = Vector2.Scale(image.rectTransform.rect.size, frame);
        }
    }
}
