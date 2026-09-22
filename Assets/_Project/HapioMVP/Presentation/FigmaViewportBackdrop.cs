using UnityEngine;

namespace C6.Prototype.Presentation
{
    /// <summary>
    /// Keeps one slice of the front artwork behind a camera's 3D content.
    /// The two split cameras sample the same full-screen image so their seam stays aligned.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class FigmaViewportBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Texture2D fullScreenArtwork;
        [SerializeField, Range(1f, 49f)] private float distance = 40f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");

        public void Configure(Camera camera, Texture2D artwork)
        {
            targetCamera = camera;
            fullScreenArtwork = artwork;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (targetCamera == null || fullScreenArtwork == null) return;
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            float depth = Mathf.Min(distance, targetCamera.farClipPlane - .5f);
            if (depth <= targetCamera.nearClipPlane) return;
            float viewportAspect = targetCamera.pixelHeight > 0
                ? (float)targetCamera.pixelWidth / targetCamera.pixelHeight
                : targetCamera.aspect;
            if (viewportAspect <= 0f) return;
            float height = targetCamera.orthographic
                ? 2f * targetCamera.orthographicSize
                : 2f * depth * Mathf.Tan(targetCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            transform.localPosition = new Vector3(0f, 0f, depth);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(height * viewportAspect, height, 1f);

            float displayAspect = Screen.height > 0
                ? (float)Screen.width / Screen.height
                : fullScreenArtwork.width / (float)fullScreenArtwork.height;
            float artAspect = fullScreenArtwork.width / (float)fullScreenArtwork.height;
            float cropWidth = Mathf.Min(1f, displayAspect / artAspect);
            float cropHeight = Mathf.Min(1f, artAspect / displayAspect);
            Rect view = targetCamera.rect;
            var tilingAndOffset = new Vector4(
                cropWidth * view.width,
                cropHeight * view.height,
                (1f - cropWidth) * .5f + cropWidth * view.x,
                (1f - cropHeight) * .5f + cropHeight * view.y);
            if (properties == null) properties = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(properties);
            properties.SetVector(BaseMapTransform, tilingAndOffset);
            meshRenderer.SetPropertyBlock(properties);
        }
    }
}
