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
