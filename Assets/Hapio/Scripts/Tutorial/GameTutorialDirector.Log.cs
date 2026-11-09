using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>대화 기록의 한 줄: 누가, 무엇을 말했는지 (대본 그대로, 줄바꿈 포함).</summary>
    public readonly struct GameTutorialLogEntry
    {
        public readonly GameTutorialSpeaker Speaker;
        public readonly string Text;

        public GameTutorialLogEntry(GameTutorialSpeaker speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    /// <summary>
    /// 튜토리얼 대화 기록 (#567).
    /// - 대본 대사를 넘긴 순간 그 대사가 기록에 쌓인다. 동작 중에 잠깐 뜨는 말("다시 해 봐!"), 안내 띠, "성공!"은 담지 않는다.
    /// - 기록이 하나라도 생기면 건너뛰기 바로 아래에 "기록" 버튼이 건너뛰기와 같은 빠르기로 서서히 나타난다.
    /// - 기록은 이 판의 것이다. RESTART는 Battle을 다시 불러와 새 기록으로 시작한다.
    /// - 기록 버튼을 누르면 뒤가 어두워지고 튜토리얼 시계와 판 조작이 멈춘다. 막대가 위에서 아래로 쌓이고(오래된 대사가 위),
    ///   막대마다 왼쪽에 얼굴과 이름, 가운데에 대사가 있다. 처음 열면 가장 최근 대사가 보이는 맨 아래다. 한 번에 3개쯤 보이고
    ///   오른쪽 드래그 바로 움직인다. "뒤로 가기"를 누르면 닫고 보던 대사나 하던 동작을 잇는다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private readonly List<GameTutorialLogEntry> dialogueLog = new List<GameTutorialLogEntry>();
        private RectTransform logButton;
        private CanvasGroup logButtonGroup;
        private Button logButtonControl;
        private float logShownAt = -1f;

        /// <summary>The words' box is this many font sizes tall: room for two lines, never for three.</summary>
        public const float LogWordsLineHeights = 2.7f;

        private sealed class LogRow
        {
            public RectTransform Root, WordsBox;
            public Image Face;
            public Text Name, Words;
        }

        private readonly List<LogRow> logRows = new List<LogRow>();
        private RectTransform logScreen, logList, logContent, logScrollBar, logBack;
        private CanvasGroup logScreenGroup;
        private ScrollRect logScroll;
        private Button logBackButton;
        private bool logOpen, logClosing;
        private float logOpenedAt, logClosedAt;
        private LabBoardGestures gesturesBeforeLog;

        /// <summary>The lines moved on from so far, oldest first.</summary>
        public IReadOnlyList<GameTutorialLogEntry> DialogueLog => dialogueLog;
        /// <summary>How far the "기록" button has faded in (0 hidden, 1 fully shown).</summary>
        public float LogButtonAlpha => logButton != null && logButton.gameObject.activeSelf ? logButtonGroup.alpha : 0f;
        /// <summary>The dialogue log screen is up (or closing).</summary>
        public bool IsDialogueLogOpen => logOpen;
        /// <summary>The words shown on each bar of the open log, oldest first.</summary>
        public IEnumerable<string> DialogueLogShownLines
        {
            get { for (int i = 0; i < dialogueLog.Count && i < logRows.Count; i++) yield return logRows[i].Words.text; }
        }
        /// <summary>How far the list is scrolled: 0 at the newest line (the bottom), 1 at the oldest.</summary>
        public float DialogueLogScroll => logScroll != null ? logScroll.verticalNormalizedPosition : 0f;

        /// <summary>The line being moved on from goes into the log (an action or the very start adds nothing).</summary>
        private void NoteLinePassed()
        {
            if (!HasBeat || !CurrentBeat.IsLine) return;
            dialogueLog.Add(new GameTutorialLogEntry(CurrentBeat.Speaker, CurrentBeat.Text));
            if (logShownAt < 0f) logShownAt = Time.unscaledTime;
        }

        private void BuildLogButton()
        {
            // The same frame and lettering as the skip button above it.
            Image frame = AddImage("Log Button", overlay, Color.white, art.skipButton, true);
            Slice(frame);
            logButton = frame.rectTransform;
            logButton.sizeDelta = art.logButtonSize;
            logButtonGroup = frame.gameObject.AddComponent<CanvasGroup>();
            logButtonGroup.alpha = 0f;
            logButtonControl = frame.gameObject.AddComponent<Button>();
            logButtonControl.onClick.AddListener(OpenDialogueLog);
            Label(logButton, GameTutorialScript.LogButtonLabel, art.dialogueFont, art.skipButtonFontSize);
            frame.gameObject.SetActive(false);
            BuildLogScreen();
        }

        private void BuildLogScreen()
        {
            // Over everything, the whole screen catches touches so nothing under the log can be pressed.
            Image dim = AddImage("Log Screen", overlay, art.skipDimColor, null, true);
            Stretch(dim.rectTransform);
            logScreen = dim.rectTransform;
            logScreenGroup = dim.gameObject.AddComponent<CanvasGroup>();

            logList = TopCentered("Log List", logScreen);
            Image viewport = AddImage("Log Viewport", logList, Color.clear, null, true);
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            logContent = new GameObject("Log Content", typeof(RectTransform)).GetComponent<RectTransform>();
            logContent.SetParent(viewport.rectTransform, false);
            logContent.anchorMin = new Vector2(0f, 1f);
            logContent.anchorMax = Vector2.one;
            logContent.pivot = new Vector2(.5f, 1f);

            logScroll = logList.gameObject.AddComponent<ScrollRect>();
            logScroll.horizontal = false;
            logScroll.vertical = true;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.viewport = viewport.rectTransform;
            logScroll.content = logContent;
            logScroll.verticalScrollbar = BuildLogScrollBar();
            logScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            Image frame = AddImage("Log Back", logScreen, Color.white, art.skipButton, true);
            Slice(frame);
            logBack = frame.rectTransform;
            logBack.anchorMin = logBack.anchorMax = new Vector2(.5f, 1f);
            logBack.pivot = new Vector2(.5f, 1f);
            logBack.sizeDelta = art.skipChoiceSize;
            Label(logBack, GameTutorialScript.LogBackLabel, LogFont, art.skipChoiceFontSize);
            logBackButton = frame.gameObject.AddComponent<Button>();
            logBackButton.onClick.AddListener(CloseDialogueLog);
            GameTutorialPressScale lift = frame.gameObject.AddComponent<GameTutorialPressScale>();
            lift.pressedScale = art.skipChoicePressScale;
            lift.seconds = art.skipChoicePressSeconds;
            logScreen.gameObject.SetActive(false);
        }

        /// <summary>The bar beside the list: a thin grip you can take hold of over a wider strip.</summary>
        private Scrollbar BuildLogScrollBar()
        {
            Image track = AddImage("Log Scroll Bar", logScreen, Color.clear, null, true);
            logScrollBar = track.rectTransform;
            logScrollBar.anchorMin = logScrollBar.anchorMax = new Vector2(.5f, 1f);
            logScrollBar.pivot = new Vector2(.5f, 1f);
            var area = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(logScrollBar, false);
            Stretch(area);
            Image grip = AddImage("Handle", area, Color.clear, null, true);
            Stretch(grip.rectTransform);
            var drawn = new GameObject("Grip", typeof(RectTransform), typeof(GameLoadingShape)).GetComponent<GameLoadingShape>();
            RectTransform drawnRect = drawn.rectTransform;
            drawnRect.SetParent(grip.rectTransform, false);
            drawnRect.anchorMin = new Vector2(.5f, 0f);
            drawnRect.anchorMax = new Vector2(.5f, 1f);
            drawnRect.sizeDelta = new Vector2(art.logHandleWidth, 0f);
            drawn.color = art.logHandleColor;
            drawn.raycastTarget = false;
            drawn.SetShape(-1f);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = grip.rectTransform;
            bar.targetGraphic = grip;
            bar.transition = Selectable.Transition.None;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private static RectTransform TopCentered(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            return rect;
        }

        private LogRow AddLogRow()
        {
            Image bar = AddImage("Log Row", logContent, Color.white, art.logBar, false);
            RectTransform root = bar.rectTransform;
            root.anchorMin = root.anchorMax = new Vector2(.5f, 1f);
            root.pivot = new Vector2(.5f, 1f);
            root.sizeDelta = art.logBarSize;

            Image face = AddImage("Face", root, Color.white, null, false);
            face.preserveAspect = true;
            RectTransform faceRect = face.rectTransform;
            faceRect.anchorMin = faceRect.anchorMax = new Vector2(0f, 1f);
            faceRect.pivot = new Vector2(.5f, 1f);
            faceRect.sizeDelta = Vector2.one * art.logFaceSize;
            faceRect.anchoredPosition = new Vector2(art.logFaceCenterX, -art.logFaceTop);

            Text name = AddText("Name", root, LogFont, art.logNameFontSize, art.textColor);
            name.resizeTextMinSize = Mathf.Min(8, art.logNameFontSize);
            RectTransform nameRect = name.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(.5f, 1f);
            nameRect.sizeDelta = new Vector2(art.logFaceCenterX * 2f, art.logNameFontSize * 1.3f);
            nameRect.anchoredPosition = new Vector2(art.logFaceCenterX, -(art.logFaceTop + art.logFaceSize));

            Text words = AddText("Words", root, LogFont, art.logTextFontSize, art.textColor);
            words.resizeTextMinSize = Mathf.Min(art.logMinTextFontSize, art.logTextFontSize);
            return new LogRow { Root = root, Face = face, Name = name, Words = words, WordsBox = words.rectTransform };
        }

        /// <summary>The mockup's bold lettering on the log's bars and its back button.</summary>
        private Font LogFont => art.bannerFont != null ? art.bannerFont : art.dialogueFont;

        private Sprite FaceOf(GameTutorialSpeaker speaker) =>
            speaker == GameTutorialSpeaker.Muk ? art.muk : speaker == GameTutorialSpeaker.Geumok ? art.geumok : art.bul;

        private void FillLogRow(LogRow row, GameTutorialLogEntry entry)
        {
            row.Face.sprite = FaceOf(entry.Speaker);
            row.Name.text = GameTutorialScript.NameOf(entry.Speaker);
            row.Words.text = GameTutorialScript.LogLine(entry.Text);
            Vector2 span = LogWordsSpan(art.logBarSize.x, art.logTextLeft, art.logTextRight, row.Words.preferredWidth);
            // Only two lines' height, so a long line shrinks to fit two lines instead of breaking into a third.
            float pad = Mathf.Max(0f, (art.logBarSize.y - art.logTextFontSize * LogWordsLineHeights) * .5f);
            row.WordsBox.offsetMin = new Vector2(span.x, pad);
            row.WordsBox.offsetMax = new Vector2(span.y - art.logBarSize.x, -pad);
        }

        /// <summary>
        /// Where the words go across a bar (x from its left, to x from its left): centered on the bar when they fit beside the
        /// face on one line, otherwise the wider space up to the bar's right side.
        /// </summary>
        public static Vector2 LogWordsSpan(float barWidth, float left, float right, float wordsWidth) =>
            wordsWidth <= barWidth - left * 2f ? new Vector2(left, barWidth - left) : new Vector2(left, barWidth - right);

        /// <summary>The height of <paramref name="rows"/> bars stacked with <paramref name="gap"/> between them.</summary>
        public static float LogStackHeight(int rows, float barHeight, float gap) =>
            rows <= 0 ? 0f : rows * barHeight + (rows - 1) * gap;

        /// <summary>How far the content moves up to show its bottom in a viewport (0 when it all fits).</summary>
        public static float LogScrollToEnd(float contentHeight, float viewportHeight) =>
            Mathf.Max(0f, contentHeight - viewportHeight);

        private void OpenDialogueLog()
        {
            if (logOpen || skipOpen || finishing || BattlePaused || dialogueLog.Count == 0) return;
            logOpen = true;
            logClosing = false;
            logOpenedAt = Time.unscaledTime;
            gesturesBeforeLog = board.AllowedGestures;
            board.CancelDrag();
            board.SetAllowedGestures(LabBoardGestures.None);
            logButtonControl.interactable = false;
            logBackButton.interactable = true;
            logScreenGroup.alpha = 0f;
            logScreen.gameObject.SetActive(true);
            logScreen.SetAsLastSibling();
            LayOutLogScreen();
        }

        private void LayOutLogScreen()
        {
            while (logRows.Count < dialogueLog.Count) logRows.Add(AddLogRow());
            float step = art.logBarSize.y + art.logBarGap;
            for (int i = 0; i < dialogueLog.Count; i++)
            {
                logRows[i].Root.anchoredPosition = new Vector2(0f, -i * step);
                FillLogRow(logRows[i], dialogueLog[i]);
            }
            float view = LogStackHeight(art.logVisibleBars, art.logBarSize.y, art.logBarGap);
            float content = LogStackHeight(dialogueLog.Count, art.logBarSize.y, art.logBarGap);
            float top = overlay.rect.height * art.logListTopFraction;
            logList.sizeDelta = new Vector2(art.logBarSize.x, view);
            logList.anchoredPosition = new Vector2(0f, -top);
            logScrollBar.sizeDelta = new Vector2(art.logHandleTouchWidth, view);
            logScrollBar.anchoredPosition = new Vector2(art.logBarSize.x * .5f + art.logHandleGap + art.logHandleWidth * .5f, -top);
            logBack.anchoredPosition = new Vector2(0f, -(top + view + art.logBackGap));
            logContent.sizeDelta = new Vector2(0f, content);
            logContent.anchoredPosition = new Vector2(0f, LogScrollToEnd(content, view));
            logScroll.StopMovement();
        }

        private void CloseDialogueLog()
        {
            if (!logOpen || logClosing) return;
            logClosing = true;
            logClosedAt = Time.unscaledTime;
            logBackButton.interactable = false;
        }

        private void FinishClosingLog()
        {
            logOpen = logClosing = false;
            logScreen.gameObject.SetActive(false);
            board.SetAllowedGestures(gesturesBeforeLog);
            logButtonControl.interactable = true;
        }

        /// <summary>Plays the log screen. True while it is up: the rest of the screen waits.</summary>
        private bool UpdateLogScreen()
        {
            if (!logOpen) return false;
            if (logClosing)
            {
                float left = 1f - Mathf.Clamp01((Time.unscaledTime - logClosedAt) / art.skipCloseSeconds);
                logScreenGroup.alpha = left;
                if (left <= 0f) FinishClosingLog();
                return logOpen;
            }
            logScreenGroup.alpha = Mathf.Clamp01((Time.unscaledTime - logOpenedAt) / art.dimFadeSeconds);
            return true;
        }

        /// <summary>Shows the "기록" button once there is a log, right under the skip button (laid out just before this).</summary>
        private void UpdateLogButton()
        {
            if (logButton == null) return;
            bool up = dialogueLog.Count > 0 && !finishing;
            if (logButton.gameObject.activeSelf != up) logButton.gameObject.SetActive(up);
            if (!up) return;
            logButtonGroup.alpha = SkipFade(Time.unscaledTime - logShownAt, art.skipFadeSeconds);
            logButton.anchoredPosition = LogButtonCenter(skipButton.anchoredPosition, art.skipButtonSize.y, art.logButtonSize.y,
                art.logButtonGap);
        }

        /// <summary>The "기록" button's center (#586): <paramref name="gap"/> under the skip button, on its middle, clear of the notch.</summary>
        public static Vector2 LogButtonCenter(Vector2 skipCenter, float skipHeight, float logHeight, float gap) =>
            new Vector2(skipCenter.x, skipCenter.y - skipHeight * .5f - gap - logHeight * .5f);
    }
}
