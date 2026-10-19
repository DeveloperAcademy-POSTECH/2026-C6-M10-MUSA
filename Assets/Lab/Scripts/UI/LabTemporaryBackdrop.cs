using UnityEngine;

namespace C6Lab
{
    /// <summary>Camera-facing artwork that can be removed when the 3D environment is ready.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LabTemporaryBackdrop : MonoBehaviour
    {
        public bool alignToScreenTop;
        public Camera compositionCamera;

        private void LateUpdate()
        {
            FitToCamera();
        }

        public void FitToCamera()
        {
            var view = GetComponentInParent<Camera>();
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            var artwork = renderer.sprite;
            if (view == null || artwork == null) return;

            // Width-fit and top-align the original artwork without distorting it.
            // The combination board covers its lower portion in the portrait layout.
            float depth = Mathf.Lerp(view.nearClipPlane, view.farClipPlane, .9f);
            float viewHeight = view.orthographic ? view.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float width = viewHeight * view.aspect;
            float scale = width / artwork.bounds.size.x;
            float height = artwork.bounds.size.y * scale;
            // On wider windows, cover the viewport vertically as well.
            float minimumHeight = alignToScreenTop && compositionCamera != null && view.pixelHeight > 0
                ? viewHeight * compositionCamera.pixelHeight / view.pixelHeight : viewHeight;
            if (height < minimumHeight)
            {
                scale *= minimumHeight / height;
                height = minimumHeight;
            }
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;
            float top = viewHeight * .5f;
            if (alignToScreenTop && view.pixelHeight > 0)
                top += (Screen.height - view.pixelRect.yMax) * viewHeight / view.pixelHeight;
            transform.localPosition = new Vector3(0f, top - height * .5f, depth);
        }
    }
}
