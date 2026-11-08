using System.Collections.Generic;
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
    /// - 기록이 하나라도 생기면 건너뛰기 바로 오른쪽에 "기록" 버튼이 건너뛰기와 같은 빠르기로 서서히 나타난다.
    /// - 기록은 이 판의 것이다. RESTART는 Battle을 다시 불러와 새 기록으로 시작한다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private readonly List<GameTutorialLogEntry> dialogueLog = new List<GameTutorialLogEntry>();
        private RectTransform logButton;
        private CanvasGroup logButtonGroup;
        private Button logButtonControl;
        private float logShownAt = -1f;

        /// <summary>The lines moved on from so far, oldest first.</summary>
        public IReadOnlyList<GameTutorialLogEntry> DialogueLog => dialogueLog;
        /// <summary>How far the "기록" button has faded in (0 hidden, 1 fully shown).</summary>
        public float LogButtonAlpha => logButton != null && logButton.gameObject.activeSelf ? logButtonGroup.alpha : 0f;

        /// <summary>The line being moved on from goes into the log (an action or the very start adds nothing).</summary>
        private void NoteLinePassed()
        {
            if (!HasBeat || !CurrentBeat.IsLine) return;
            dialogueLog.Add(new GameTutorialLogEntry(CurrentBeat.Speaker, CurrentBeat.Text));
            if (logShownAt < 0f) logShownAt = Time.unscaledTime;
        }

        private void BuildLogButton()
        {
            // The same frame and lettering as the skip button beside it.
            Image frame = AddImage("Log Button", overlay, Color.white, art.skipButton, true);
            Slice(frame);
            logButton = frame.rectTransform;
            logButton.sizeDelta = art.logButtonSize;
            logButtonGroup = frame.gameObject.AddComponent<CanvasGroup>();
            logButtonGroup.alpha = 0f;
            logButtonControl = frame.gameObject.AddComponent<Button>();
            Label(logButton, GameTutorialScript.LogButtonLabel, art.dialogueFont, art.skipButtonFontSize);
            frame.gameObject.SetActive(false);
        }

        /// <summary>Shows the "기록" button once there is a log, right beside the skip button (laid out just before this).</summary>
        private void UpdateLogButton()
        {
            if (logButton == null) return;
            bool up = dialogueLog.Count > 0 && !finishing;
            if (logButton.gameObject.activeSelf != up) logButton.gameObject.SetActive(up);
            if (!up) return;
            logButtonGroup.alpha = SkipFade(Time.unscaledTime - logShownAt, art.skipFadeSeconds);
            logButton.anchoredPosition = LogButtonCenter(skipButton.anchoredPosition, art.skipButtonSize.x, art.logButtonSize.x,
                art.logButtonGap);
        }

        /// <summary>The "기록" button's center: level with the skip button, <paramref name="gap"/> to its right.</summary>
        public static Vector2 LogButtonCenter(Vector2 skipCenter, float skipWidth, float logWidth, float gap) =>
            new Vector2(skipCenter.x + skipWidth * .5f + gap + logWidth * .5f, skipCenter.y);
    }
}
