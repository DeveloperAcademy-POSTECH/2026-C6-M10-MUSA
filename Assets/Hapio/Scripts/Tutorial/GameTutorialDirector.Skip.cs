using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 건너뛰기 (#477).
    /// - 튜토리얼 화면을 한 번이라도 누르면 왼쪽 위(일시정지 버튼과 같은 높이)에 "건너뛰기"가 서서히 나타난다.
    /// - 누르면 뒤가 어두워지며 경고 창이 말풍선처럼 살짝 커졌다 자리 잡는다. 창이 떠 있는 동안 튜토리얼 시계와 판 조작이
    ///   멈추고, 말풍선·안내 띠·손·밝히기는 창 뒤로 숨는다. 이미 미끄러지던 구슬은 멈출 때까지 움직인다.
    /// - "계속 배우기": 창이 빠르게 사라지고 하던 곳부터 잇는다.
    /// - "건너뛰기": 마지막 장면 뒤와 같이 어두워지며 방 목록으로 간다. 이번 실행에서 튜토리얼은 다시 나오지 않는다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private float clock;
        /// <summary>The tutorial's own clock: real time that stops while the skip dialog is open or the battle is paused.</summary>
        private float Now => clock;

        private RectTransform skipButton;
        private CanvasGroup skipButtonGroup;
        private Button skipButtonControl;
        private RectTransform skipDialog;
        private CanvasGroup skipDialogGroup;
        private RectTransform skipWindow;
        private Button stayButton, leaveButton;
        private float skipShownAt = -1f;
        private bool skipOpen, skipClosing;
        private float skipOpenedAt, skipClosedAt;
        private LabBoardGestures gesturesBeforeSkip;
        private readonly List<KeyValuePair<GameObject, bool>> hiddenBySkip = new List<KeyValuePair<GameObject, bool>>();

        /// <summary>The player has tapped the tutorial screen at least once, so the skip button is coming or up.</summary>
        public bool HasTappedOnce => skipShownAt >= 0f;
        /// <summary>How far the skip button has faded in (0 hidden, 1 fully shown).</summary>
        public float SkipButtonAlpha => skipButton != null && skipButton.gameObject.activeSelf ? skipButtonGroup.alpha : 0f;
        /// <summary>The "튜토리얼을 건너뛰겠소?" dialog is up (or closing).</summary>
        public bool IsSkipDialogOpen => skipOpen;
        /// <summary>The player chose to skip the rest of the tutorial.</summary>
        public bool Skipped { get; private set; }

        /// <summary>How far the skip button has faded in <paramref name="sinceTap"/> seconds after the first tap.</summary>
        public static float SkipFade(float sinceTap, float seconds) =>
            sinceTap < 0f ? 0f : seconds <= 0f ? 1f : Mathf.Clamp01(sinceTap / seconds);

        private void AdvanceClock()
        {
            if (!skipOpen && !BattlePaused) clock += Time.unscaledDeltaTime;
        }

        /// <summary>Any tap on the tutorial screen, taken or not, brings the skip button in.</summary>
        private void NoteTap()
        {
            if (skipShownAt < 0f) skipShownAt = Time.unscaledTime;
        }

        private void BuildSkip()
        {
            Image frame = AddImage("Skip Button", overlay, Color.white, art.skipButton, true);
            Slice(frame);
            skipButton = frame.rectTransform;
            skipButton.sizeDelta = art.skipButtonSize;
            skipButtonGroup = frame.gameObject.AddComponent<CanvasGroup>();
            skipButtonGroup.alpha = 0f;
            skipButtonControl = frame.gameObject.AddComponent<Button>();
            skipButtonControl.onClick.AddListener(OpenSkipDialog);
            Label(skipButton, GameTutorialScript.SkipButtonLabel, art.dialogueFont, art.skipButtonFontSize);
            frame.gameObject.SetActive(false);

            // Over everything, the whole screen catches touches so nothing under the dialog can be pressed.
            Image dim = AddImage("Skip Dialog", overlay, art.skipDimColor, null, true);
            Stretch(dim.rectTransform);
            skipDialog = dim.rectTransform;
            skipDialogGroup = dim.gameObject.AddComponent<CanvasGroup>();
            skipWindow = new GameObject("Window", typeof(RectTransform)).GetComponent<RectTransform>();
            skipWindow.SetParent(skipDialog, false);
            Center(skipWindow);
            skipWindow.sizeDelta = art.skipPanelSize;

            Image panel = AddImage("Panel", skipWindow, Color.white, art.skipPanel, false);
            Slice(panel);
            Stretch(panel.rectTransform);
            Text title = AddText("Title", panel.rectTransform, art.bannerFont != null ? art.bannerFont : art.dialogueFont,
                art.skipTitleFontSize, art.textColor);
            Place(title.rectTransform, .62f, .92f);
            Text body = AddText("Body", panel.rectTransform, art.dialogueFont, art.skipBodyFontSize, art.textColor);
            body.resizeTextMinSize = Mathf.Min(art.minFontSize, art.skipBodyFontSize);
            Place(body.rectTransform, .1f, .6f);
            title.text = GameTutorialScript.SkipTitle;
            body.text = GameTutorialScript.SkipBody;

            stayButton = Choice("Keep Learning", GameTutorialScript.SkipStay, -1f, StayInTutorial);
            leaveButton = Choice("Skip Tutorial", GameTutorialScript.SkipLeave, 1f, LeaveTutorial);
            skipDialog.gameObject.SetActive(false);
        }

        /// <summary>A button under the window, left (<paramref name="side"/> -1) or right (+1) of its middle.</summary>
        private Button Choice(string name, string label, float side, UnityEngine.Events.UnityAction pressed)
        {
            Image frame = AddImage(name, skipWindow, Color.white, art.skipButton, true);
            Slice(frame);
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = art.skipChoiceSize;
            rect.anchoredPosition = new Vector2(side * (art.skipChoiceSize.x + art.skipChoiceGap) * .5f, -art.skipChoiceDrop);
            Label(rect, label, art.dialogueFont, art.skipChoiceFontSize);
            Button button = frame.gameObject.AddComponent<Button>();
            button.onClick.AddListener(pressed);
            // A finger on it lifts it a little.
            GameTutorialPressScale lift = frame.gameObject.AddComponent<GameTutorialPressScale>();
            lift.pressedScale = art.skipChoicePressScale;
            lift.seconds = art.skipChoicePressSeconds;
            return button;
        }

        private void Slice(Image image)
        {
            if (image.sprite == null) return;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = art.skipArtPixelsPerPoint;
        }

        private void Label(RectTransform parent, string words, Font font, int size)
        {
            Text text = AddText("Label", parent, font, size, art.textColor);
            text.resizeTextMinSize = Mathf.Min(art.minFontSize, size);
            text.text = words;
        }

        private static void Place(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(.07f, bottom);
            rect.anchorMax = new Vector2(.93f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OpenSkipDialog()
        {
            if (skipOpen || finishing || !HasTappedOnce || skipButton == null) return;
            skipOpen = true;
            skipClosing = false;
            skipOpenedAt = Time.unscaledTime;
            gesturesBeforeSkip = board.AllowedGestures;
            board.CancelDrag();
            board.SetAllowedGestures(LabBoardGestures.None);
            hiddenBySkip.Clear();
            foreach (GameObject part in PartsUnderSkip())
            {
                hiddenBySkip.Add(new KeyValuePair<GameObject, bool>(part, part.activeSelf));
                part.SetActive(false);
            }
            skipButtonControl.interactable = false;
            stayButton.interactable = leaveButton.interactable = true;
            skipDialogGroup.alpha = 0f;
            skipWindow.localScale = Vector3.zero;
            skipDialog.gameObject.SetActive(true);
            skipDialog.SetAsLastSibling();
        }

        private void StayInTutorial()
        {
            if (!skipOpen || skipClosing || Skipped) return;
            skipClosing = true;
            skipClosedAt = Time.unscaledTime;
            stayButton.interactable = leaveButton.interactable = false;
        }

        private void LeaveTutorial()
        {
            if (!skipOpen || skipClosing || Skipped) return;
            Skipped = true;
            stayButton.interactable = leaveButton.interactable = false;
            Debug.Log("C6_LAB_TUTORIAL_SKIP step=" + stepIndex + " beat=" + beatIndex);
            // The same way out as after the last line: the screen darkens over the dialog and the room list comes.
            BeginFinish();
        }

        private void FinishClosing()
        {
            skipOpen = skipClosing = false;
            skipDialog.gameObject.SetActive(false);
            foreach (KeyValuePair<GameObject, bool> part in hiddenBySkip)
                if (part.Key != null) part.Key.SetActive(part.Value);
            hiddenBySkip.Clear();
            // A word said while it was up (a throw that landed as it opened) is still due.
            if (remarkAt >= 0f) bubbleRoot.gameObject.SetActive(true);
            board.SetAllowedGestures(gesturesBeforeSkip);
            skipButtonControl.interactable = true;
        }

        private IEnumerable<GameObject> PartsUnderSkip()
        {
            yield return bubbleRoot.gameObject;
            yield return banner.gameObject;
            yield return successText.gameObject;
            yield return hand.gameObject;
            yield return arrow.gameObject;
            yield return speakerPortrait.gameObject;
            yield return dimGroup.gameObject;
            if (mirror != null) yield return mirror.gameObject;
        }

        /// <summary>Lays out the skip button and plays the dialog. True while the dialog is up: the rest of the screen waits.</summary>
        private bool UpdateSkip()
        {
            if (skipButton == null) return false;
            bool buttonUp = HasTappedOnce && !finishing;
            if (skipButton.gameObject.activeSelf != buttonUp) skipButton.gameObject.SetActive(buttonUp);
            if (buttonUp)
            {
                skipButtonGroup.alpha = SkipFade(Time.unscaledTime - skipShownAt, art.skipFadeSeconds);
                PlaceSkipButton();
            }
            if (!skipOpen) return false;
            if (skipClosing)
            {
                float left = 1f - Mathf.Clamp01((Time.unscaledTime - skipClosedAt) / art.skipCloseSeconds);
                skipDialogGroup.alpha = left;
                if (left <= 0f) FinishClosing();
                return skipOpen;
            }
            float age = Time.unscaledTime - skipOpenedAt;
            skipDialogGroup.alpha = Mathf.Clamp01(age / art.dimFadeSeconds);
            skipWindow.localScale = Vector3.one * PopScale(age / art.popSeconds, art.popOvershoot);
            Rect bounds = overlay.rect;
            skipWindow.anchoredPosition = new Vector2(0f, bounds.height * (.5f - art.skipPanelTopFraction));
            // Something that came up behind the dialog (a remark for a throw that was already flying) stays hidden.
            foreach (KeyValuePair<GameObject, bool> part in hiddenBySkip)
                if (part.Key != null && part.Key.activeSelf) part.Key.SetActive(false);
            return true;
        }

        /// <summary>Top left, level with the pause button on the right.</summary>
        private void PlaceSkipButton()
        {
            Rect bounds = overlay.rect;
            float y = bounds.yMax - art.skipButtonMargin - art.skipButtonSize.y * .5f;
            if (hud != null && hud.pauseButton != null
                && TryOverlayRect(hud.pauseButton.transform as RectTransform, out Rect pause)) y = pause.center.y;
            skipButton.anchoredPosition = new Vector2(bounds.xMin + art.skipButtonMargin + art.skipButtonSize.x * .5f, y);
        }
    }
}
