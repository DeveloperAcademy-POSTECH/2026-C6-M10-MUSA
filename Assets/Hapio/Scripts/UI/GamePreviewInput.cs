using C6Lab;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hapio
{
    /// <summary>Keeps the saved Scene reference while sharing the result viewer's gesture ownership.</summary>
    [DisallowMultipleComponent]
    public sealed class GamePreviewInput : MonoBehaviour, IPointerDownHandler, IScrollHandler
    {
        private GourdViewerGestureSurface gestures;
        private UnityEngine.UI.Button resetButton;
        private GameScreen screen;

        private void Awake()
        {
            gestures = GetComponent<GourdViewerGestureSurface>();
            if (gestures == null) gestures = gameObject.AddComponent<GourdViewerGestureSurface>();
            gestures.Bind(Rotate, Zoom);
            screen = GetComponentInParent<GameScreen>();
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

        // Presentation only: the shared gesture surface still owns pointer acquisition, rotation and pinch.
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
                DismissRotationHintAt(data.position, data.pressEventCamera);
        }

        public void OnScroll(PointerEventData data)
        {
            if (data.scrollDelta.y != 0f)
                DismissRotationHintAt(data.position, data.enterEventCamera);
        }

        private void DismissRotationHintAt(Vector2 point, Camera eventCamera)
        {
            if (!isActiveAndEnabled || screen == null || screen.viewerPanel == null
                || !screen.viewerPanel.activeInHierarchy
                || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, point, eventCamera)) return;
            screen.viewerPanel.GetComponent<GameSealedGourdView>()?.DismissRotationHint();
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
