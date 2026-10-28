using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hapio
{
    public enum StoryExitReason { Completed, Skipped }

    /// <summary>Local presentation only. Call Play and await Finished before returning to the game flow.</summary>
    public sealed class StoryPlayer : MonoBehaviour
    {
        public StorySequence sequence;
        public RectTransform cutHost;
        public CanvasGroup dialogueGroup;
        public Text speakerText;
        public Text dialogueText;
        public Text progressText;
        public Button nextButton;
        public Button previousButton;
        public Button skipButton;
        public Button autoButton;
        public Text autoLabel;
        public Image speakerBadge;
        public AudioSource voiceSource;
        public bool playOnStart = true;
        [Tooltip("Game opening only: manual input; the authoring preview can still use AUTO.")]
        public bool touchOnly;
        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onSkipped = new UnityEvent();
        public event Action<StoryExitReason> Finished;

        public int CurrentCutIndex { get; private set; } = -1;
        public int CurrentPageIndex { get; private set; } = -1;
        public bool IsPlaying { get; private set; }
        public bool AutoAdvance { get; private set; }
        public bool IsReading { get; private set; }
        public StoryCutView CurrentView => currentView;

        private StoryCutView currentView;
        private float elapsed;
        private float readElapsed;
        private int lastInputFrame = -1;
        private bool suppressMultiTouch;
        private StoryDialoguePage currentPage;
        private Text cachedDialogue, cachedSpeaker;
        private Vector2 defaultDialoguePosition, defaultDialogueSize, defaultSpeakerPosition, defaultSpeakerSize;
        private Color defaultSpeakerColor;
        private int defaultDialogueFontSize;
        private Vector3 defaultDialogueScale;
        private HorizontalWrapMode defaultSpeakerWrap;
        private TextAnchor defaultSpeakerAlignment;

        private void OnEnable()
        {
            if (nextButton != null) nextButton.onClick.AddListener(RequestNext);
            if (previousButton != null) previousButton.onClick.AddListener(RequestPrevious);
            UpdatePreviousButton();
            if (skipButton != null) skipButton.onClick.AddListener(RequestSkip);
            if (autoButton != null) autoButton.onClick.AddListener(ToggleAuto);
        }

        private void Start()
        {
            if (playOnStart && !IsPlaying) Play(sequence);
        }

        private void OnDisable()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(RequestNext);
            if (previousButton != null) previousButton.onClick.RemoveListener(RequestPrevious);
            if (skipButton != null) skipButton.onClick.RemoveListener(RequestSkip);
            if (autoButton != null) autoButton.onClick.RemoveListener(ToggleAuto);
            Cancel();
        }

        public bool Play(StorySequence story)
        {
            // Validate before replacing an already playing story.
            if (story == null || cutHost == null)
            {
                Debug.LogError("StoryPlayer requires a sequence and cut host.", this);
                return false;
            }
            if (!story.Validate(out string error))
            {
                Debug.LogError(error, story);
                return false;
            }
            Cancel();
            sequence = story;
            AutoAdvance = false;
            lastInputFrame = -1;
            suppressMultiTouch = false;
            IsPlaying = true;
            UpdateAutoLabel();
            ShowCut(0);
            return true;
        }

        private void Update()
        {
            if (touchOnly && StoryTapAdvance.PressedTouchCount > 1) suppressMultiTouch = true;
            AdvanceTime(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Clear only after EventSystem has processed the last release in this frame.
            if (StoryTapAdvance.PressedTouchCount == 0) suppressMultiTouch = false;
        }

        internal void AdvanceTime(float seconds)
        {
            if (!IsPlaying || currentView == null) return;
            elapsed += seconds;
            currentView.Sample(elapsed);
            if (!IsReading)
            {
                if (elapsed >= currentView.EntranceDuration) BeginReading();
                return;
            }
            if (!AutoAdvance) return;
            readElapsed += seconds;
            float duration = Mathf.Max(.1f, currentPage.autoReadSeconds);
            if (readElapsed >= duration && (voiceSource == null || !voiceSource.isPlaying)) Next();
        }

        /// <summary>UI input shares one frame gate. Direct sequencing and preview AUTO keep their existing API.</summary>
        public void RequestNext()
        {
            if (AcceptUserInput()) Next();
        }

        public void RequestPrevious() { if (AcceptUserInput()) Previous(); }
        public void RequestSkip() { if (AcceptUserInput()) Skip(); }

        private bool AcceptUserInput()
        {
            if (!IsPlaying) return false;
            if (touchOnly)
            {
                if (StoryTapAdvance.PressedTouchCount > 0) suppressMultiTouch = true;
                if (suppressMultiTouch || lastInputFrame == Time.frameCount) return false;
            }
            lastInputFrame = Time.frameCount;
            return true;
        }

        public void Next()
        {
            if (!IsPlaying) return;
            if (!IsReading)
            {
                elapsed = currentView.EntranceDuration;
                currentView.Sample(elapsed);
                BeginReading();
                return;
            }
            var cut = sequence.cuts[CurrentCutIndex];
            if (CurrentPageIndex + 1 < cut.PageCount)
            {
                ShowPage(CurrentPageIndex + 1);
                return;
            }
            int next = CurrentCutIndex + 1;
            if (next >= sequence.cuts.Count) Finish(StoryExitReason.Completed);
            else ShowCut(next);
        }

        /// <summary>Return to the previous picture's first page and pause automatic advance.</summary>
        public void Previous()
        {
            if (!IsPlaying || CurrentCutIndex <= 0) return;
            AutoAdvance = false;
            UpdateAutoLabel();
            ShowCut(CurrentCutIndex - 1);
            // Reviewing a previous picture should not require waiting for its entrance again.
            elapsed = currentView.EntranceDuration;
            currentView.Sample(elapsed);
            BeginReading();
        }

        private void UpdatePreviousButton()
        {
            if (previousButton != null) previousButton.interactable = IsPlaying && CurrentCutIndex > 0;
        }

        public void Skip()
        {
            if (IsPlaying) Finish(StoryExitReason.Skipped);
        }

        public void Replay() => Play(sequence);

        public void ToggleAuto()
        {
            if (touchOnly) return;
            AutoAdvance = !AutoAdvance;
            readElapsed = 0f;
            UpdateAutoLabel();
        }

        private void UpdateAutoLabel()
        {
            if (autoLabel != null) autoLabel.text = AutoAdvance ? "자동 켜짐" : "자동 꺼짐";
        }

        private void ShowCut(int index)
        {
            var previousView = currentView;
            var cut = sequence.cuts[index];
            var nextView = Instantiate(cut.viewPrefab, cutHost, false);
            // Prepare the next background before removing the picture covering the paper base.
            nextView.Sample(0f);
            CurrentCutIndex = index;
            currentView = nextView;
            ApplyDialogueLayout();
            elapsed = readElapsed = 0f;
            CurrentPageIndex = 0;
            IsReading = false;
            currentPage = cut.GetPage(0);
            UpdatePageText();
            SetDialogueVisible(false);
            UpdatePreviousButton();
            ReleaseView(previousView);
        }

        private void BeginReading()
        {
            IsReading = true;
            readElapsed = 0f;
            SetDialogueVisible(!currentView.hideEmptyDialogue ||
                !string.IsNullOrWhiteSpace(currentPage.dialogue) || !string.IsNullOrWhiteSpace(currentPage.speaker));
            if (voiceSource != null && currentPage.voice != null)
            {
                voiceSource.clip = currentPage.voice;
                voiceSource.Play();
            }
        }

        private void ShowPage(int index)
        {
            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = null;
            }
            CurrentPageIndex = index;
            currentPage = sequence.cuts[CurrentCutIndex].GetPage(index);
            UpdatePageText();
            BeginReading();
        }

        private void UpdatePageText()
        {
            if (speakerText != null) speakerText.text = currentPage.speaker;
            if (speakerBadge != null)
                speakerBadge.enabled = currentView != null && currentView.overrideDialogueLayout &&
                    currentView.speakerBadge != null && !string.IsNullOrWhiteSpace(currentPage.speaker);
            if (dialogueText != null) dialogueText.text = currentPage.dialogue;
            if (progressText != null)
            {
                int pages = sequence.cuts[CurrentCutIndex].PageCount;
                progressText.text = pages > 1
                    ? $"{CurrentCutIndex + 1}/{sequence.cuts.Count} · {CurrentPageIndex + 1}/{pages}"
                    : $"{CurrentCutIndex + 1} / {sequence.cuts.Count}";
            }
        }

        private void ApplyDialogueLayout()
        {
            if (dialogueText != null)
            {
                var rect = dialogueText.rectTransform;
                if (cachedDialogue != dialogueText)
                {
                    cachedDialogue = dialogueText;
                    defaultDialoguePosition = rect.anchoredPosition;
                    defaultDialogueSize = rect.sizeDelta;
                    defaultDialogueFontSize = dialogueText.fontSize;
                    defaultDialogueScale = rect.localScale;
                }
                if (currentView.dialogueFontSize > 0)
                {
                    dialogueText.fontSize = Mathf.CeilToInt(currentView.dialogueFontSize);
                    float scale = currentView.dialogueFontSize / dialogueText.fontSize;
                    rect.localScale = new Vector3(scale, scale, 1);
                }
                else
                {
                    dialogueText.fontSize = defaultDialogueFontSize;
                    rect.localScale = defaultDialogueScale;
                }
                if (currentView.overrideDialogueLayout)
                {
                    var layout = ValidLayout(currentView.dialogueRect, new Rect(84, 740, 259, 56));
                    rect.anchoredPosition = new Vector2(layout.x, -layout.y);
                    rect.sizeDelta = new Vector2(layout.width / rect.localScale.x, layout.height / rect.localScale.y);
                }
                else
                {
                    rect.anchoredPosition = defaultDialoguePosition;
                    rect.sizeDelta = defaultDialogueSize;
                }
            }
            if (speakerText != null)
            {
                var rect = speakerText.rectTransform;
                if (cachedSpeaker != speakerText)
                {
                    cachedSpeaker = speakerText;
                    defaultSpeakerPosition = rect.anchoredPosition;
                    defaultSpeakerSize = rect.sizeDelta;
                    defaultSpeakerColor = speakerText.color;
                    defaultSpeakerWrap = speakerText.horizontalOverflow;
                    defaultSpeakerAlignment = speakerText.alignment;
                }
                var layout = ValidLayout(currentView.speakerRect, new Rect(84, 721, 36, 18));
                rect.anchoredPosition = currentView.overrideDialogueLayout
                    ? new Vector2(layout.x, -layout.y) : defaultSpeakerPosition;
                rect.sizeDelta = currentView.overrideDialogueLayout ? layout.size : defaultSpeakerSize;
                speakerText.color = currentView.overrideDialogueLayout ? currentView.speakerColor : defaultSpeakerColor;
                speakerText.alignment = currentView.overrideDialogueLayout && currentView.centerSpeakerName
                    ? TextAnchor.MiddleCenter : defaultSpeakerAlignment;
                // Figma's single-line name can exceed an integer text box by a rounding fraction.
                speakerText.horizontalOverflow = currentView.overrideDialogueLayout ? HorizontalWrapMode.Overflow : defaultSpeakerWrap;
            }
            if (speakerBadge != null)
            {
                speakerBadge.sprite = currentView.overrideDialogueLayout ? currentView.speakerBadge : null;
                if (currentView.overrideDialogueLayout)
                {
                    var layout = ValidLayout(currentView.speakerBadgeRect, new Rect(57, 713, 91, 33));
                    speakerBadge.rectTransform.anchoredPosition = new Vector2(layout.x, -layout.y);
                    speakerBadge.rectTransform.sizeDelta = layout.size;
                }
            }
        }

        private static Rect ValidLayout(Rect authored, Rect fallback) =>
            authored.width > 0 && authored.height > 0 ? authored : fallback;

        private void SetDialogueVisible(bool visible)
        {
            if (dialogueGroup == null) return;
            dialogueGroup.alpha = visible ? 1f : 0f;
            dialogueGroup.interactable = visible;
            dialogueGroup.blocksRaycasts = visible;
        }

        private void Finish(StoryExitReason reason)
        {
            // Clear state before invoking callbacks, so repeated clicks cannot finish twice.
            IsPlaying = false;
            UpdatePreviousButton();
            if (voiceSource != null) voiceSource.Stop();
            if (reason == StoryExitReason.Completed) onCompleted.Invoke();
            else onSkipped.Invoke();
            Finished?.Invoke(reason);
        }

        public void Cancel()
        {
            IsPlaying = IsReading = false;
            UpdatePreviousButton();
            ReleaseView();
            SetDialogueVisible(false);
        }

        private void ReleaseView() => ReleaseView(currentView);

        private void ReleaseView(StoryCutView view)
        {
            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.clip = null;
            }
            if (view == null) return;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
            if (currentView == view) currentView = null;
        }
    }
}
