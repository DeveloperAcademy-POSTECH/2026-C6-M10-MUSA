using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 일시정지 (#477): 오른쪽 위 일시정지 버튼은 전투 HUD에 있어 튜토리얼 화면(그 위)에 가려진다.
    /// - 그 자리에 보이지 않는 버튼을 두어 대사가 떠 있을 때도 눌리게 하고, 누르면 HUD 버튼을 그대로 누른다.
    /// - 일시정지 중에는 튜토리얼 화면 전체가 투명해지고 눌림을 받지 않아, 원래 일시정지 메뉴(계속·나가기)가 보이고 눌린다.
    ///   튜토리얼 시계도 멈추고, 계속하면 그대로 이어진다.
    /// - 일시정지 메뉴의 DEV 설정(개발 빌드 전용)은 혼자 판을 다시 시작할 수 있어 튜토리얼에서는 숨긴다.
    /// - 메뉴에 RESTART를 더한다: QUIT RUN 자리에 놓고 QUIT RUN은 한 칸 아래(숨긴 DEV 자리)로 내린다. 누르면 연습 판이 처음부터
    ///   다시 열린다. QUIT RUN은 튜토리얼에서 타이틀 화면으로 간다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private CanvasGroup overlayGroup;
        private Image pauseProxy;
        private Button restartButton;

        /// <summary>The battle is paused: the tutorial screen has stepped aside for the pause menu.</summary>
        public bool IsSteppedAsideForPause { get; private set; }

        private bool BattlePaused => session != null && session.Network != null && session.Network.IsBattlePaused;

        private void BuildPause(GameObject root)
        {
            overlayGroup = root.AddComponent<CanvasGroup>();
            pauseProxy = AddImage("Pause", overlay, Color.clear, null, true);
            Button press = pauseProxy.gameObject.AddComponent<Button>();
            press.transition = Selectable.Transition.None;
            press.onClick.AddListener(PressPause);
            pauseProxy.gameObject.SetActive(false);
        }

        private void PressPause()
        {
            Button pause = hud != null ? hud.pauseButton : null;
            if (pause == null || !pause.isActiveAndEnabled || !pause.interactable || finishing || skipOpen) return;
            pause.onClick.Invoke();
        }

        /// <summary>Steps the tutorial screen aside while paused. True while it is: the rest of the screen waits.</summary>
        private bool UpdatePause()
        {
            bool paused = BattlePaused && !finishing;
            if (paused) EnsureRestartButton();
            if (paused != IsSteppedAsideForPause)
            {
                IsSteppedAsideForPause = paused;
                overlayGroup.alpha = paused ? 0f : 1f;
                overlayGroup.blocksRaycasts = !paused;
                if (paused) board.CancelDrag();
            }
            if (hud.developerSettingsButton != null && hud.developerSettingsButton.gameObject.activeSelf)
                hud.developerSettingsButton.gameObject.SetActive(false);
            if (paused) return true;
            PlacePauseProxy();
            return false;
        }

        /// <summary>The pause menu's RESTART, made once from its QUIT RUN button: in QUIT RUN's place, which moves one row down.</summary>
        private void EnsureRestartButton()
        {
            if (restartButton != null || hud.returnLobbyButton == null || hud.resumeButton == null) return;
            var quit = (RectTransform)hud.returnLobbyButton.transform;
            var resume = (RectTransform)hud.resumeButton.transform;
            GameObject copy = Instantiate(hud.returnLobbyButton.gameObject, quit.parent, false);
            copy.name = "Tutorial Restart Button";
            restartButton = copy.GetComponent<Button>();
            restartButton.onClick = new Button.ButtonClickedEvent();
            restartButton.onClick.AddListener(PressRestart);
            restartButton.interactable = true;
            foreach (Text label in copy.GetComponentsInChildren<Text>(true)) label.text = GameTutorialScript.RestartLabel;
            copy.SetActive(true);
            copy.transform.SetSiblingIndex(quit.GetSiblingIndex());
            if (resume.parent == quit.parent)
            {
                Vector2 row = quit.anchoredPosition - resume.anchoredPosition;
                ((RectTransform)copy.transform).anchoredPosition = quit.anchoredPosition;
                quit.anchoredPosition += row;
            }
        }

        /// <summary>The practice board starts over from its loading cover and first line.</summary>
        private void PressRestart()
        {
            if (restartButton == null || !restartButton.interactable || !session.RestartTutorial()) return;
            restartButton.interactable = false;
            Debug.Log("C6_LAB_TUTORIAL_RESTART step=" + stepIndex + " beat=" + beatIndex);
        }

        /// <summary>The invisible button lies exactly over the HUD's pause button, above the screen-wide tap catcher.</summary>
        private void PlacePauseProxy()
        {
            Button pause = hud.pauseButton;
            bool shown = pause != null && !finishing && !skipOpen
                && TryOverlayRect(pause.transform as RectTransform, out Rect area);
            if (pauseProxy.gameObject.activeSelf != shown) pauseProxy.gameObject.SetActive(shown);
            if (!shown) return;
            TryOverlayRect(pause.transform as RectTransform, out area);
            RectTransform rect = pauseProxy.rectTransform;
            rect.anchoredPosition = area.center;
            rect.sizeDelta = area.size;
        }
    }
}
