using UnityEngine;

namespace Hapio
{
    public sealed class GamePauseView : MonoBehaviour
    {
        public RectTransform design;
        public UnityEngine.UI.Button restartButton;
        private GameSession session;

        private void Awake()
        {
            session = GameSession.EnsureExists();
            restartButton.onClick.AddListener(Restart);
        }

        internal void ApplyArt(UnityEngine.UI.Button leaveButton)
        {
            ApplyActionArt(leaveButton);
            ApplyActionArt(restartButton);
        }

        private static void ApplyActionArt(UnityEngine.UI.Button button)
        {
            if (button == null || !(button.targetGraphic is UnityEngine.UI.Image original)) return;
            var sprite = GameFigmaStrokeLibrary.Find("PauseAction")?.sprite;
            if (sprite == null || sprite.texture == null) return;
            var surface = GameViewerHelpUi.Surface(button.transform, "PauseAction", 0, 0, 147, 48);
            GameViewerHelpUi.Stretch(surface.rectTransform);
            surface.transform.SetAsFirstSibling();
            surface.color = original.color;
            surface.raycastTarget = original.raycastTarget;
            button.targetGraphic = surface;
            original.enabled = false;
        }

        private void Restart()
        {
            if (session.RestartPausedBattle()) restartButton.interactable = false;
        }

        private void OnEnable()
        {
            restartButton.interactable = session != null && session.CanRestartPausedBattle;
        }

        private void LateUpdate()
        {
            restartButton.interactable = session != null && session.CanRestartPausedBattle;
            var canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1;
            design.localScale = Vector3.one * Mathf.Min(Screen.safeArea.width / factor / 402f, Screen.safeArea.height / factor / 874f);
        }
    }
}
