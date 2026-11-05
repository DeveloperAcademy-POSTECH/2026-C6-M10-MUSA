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
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private CanvasGroup overlayGroup;
        private Image pauseProxy;

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
