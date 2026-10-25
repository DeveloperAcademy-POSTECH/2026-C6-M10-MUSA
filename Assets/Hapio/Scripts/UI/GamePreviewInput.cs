using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Keeps the saved Scene reference while sharing the result viewer's gesture ownership.</summary>
    [DisallowMultipleComponent]
    public sealed class GamePreviewInput : MonoBehaviour
    {
        private GourdViewerGestureSurface gestures;
        private UnityEngine.UI.Button resetButton;

        private void Awake()
        {
            gestures = GetComponent<GourdViewerGestureSurface>();
            if (gestures == null) gestures = gameObject.AddComponent<GourdViewerGestureSurface>();
            gestures.Bind(Rotate, Zoom);
            GameScreen screen = GetComponentInParent<GameScreen>();
            resetButton = screen != null ? screen.resetViewerButton : null;
            if (resetButton != null) resetButton.onClick.AddListener(ResetGesture);
        }

        private void Rotate(Vector2 normalizedDelta)
        {
            GameSession session = GameSession.Instance;
            if (isActiveAndEnabled && session != null && session.IsViewerOpen) session.RotatePreview(normalizedDelta);
        }

        private void Zoom(float normalizedDelta)
        {
            GameSession session = GameSession.Instance;
            if (isActiveAndEnabled && session != null && session.IsViewerOpen) session.ZoomPreview(normalizedDelta);
        }

        public void ResetGesture() => gestures?.ResetGesture();
        private void OnEnable()
        {
            if (gestures == null) return;
            gestures.enabled = true;
            gestures.ResetGesture();
        }
        private void OnDisable()
        {
            if (gestures == null) return;
            gestures.ResetGesture();
            gestures.enabled = false;
        }
        private void OnDestroy()
        {
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetGesture);
        }
    }
}
