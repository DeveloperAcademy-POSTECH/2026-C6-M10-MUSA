using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hapio
{
    public enum StoryExitReason { Completed, Skipped }
    public enum StoryContinueState { Default, Hidden, Locked, Ready }

    /// <summary>Local presentation only. Call Play and await Finished before returning to the game flow.</summary>
    public sealed partial class StoryPlayer : MonoBehaviour
    {
        public StorySequence sequence;
        public RectTransform cutHost;
        public CanvasGroup dialogueGroup;
        public Image dialoguePaper;
        public Text speakerText;
        public Text dialogueText;
        public Text progressText;
        /// <summary>The game hides the authoring page counter; previews keep it.</summary>
        public bool hideProgress;
        public Button nextButton;
        public Button previousButton;
        public Button skipButton;
        [Tooltip("Opening only: hide skip outside playback and during uninterrupted cinematic cuts.")]
        public bool hideSkipDuringCinematics;
        public Button autoButton;
        public Text autoLabel;
        public Image speakerBadge;
        public StorySpeakerPalette speakerPalette;
        public AudioSource voiceSource;
        public bool playOnStart = true;
        [Tooltip("Game opening only: manual input; the authoring preview can still use AUTO.")]
        public bool touchOnly;
        [Tooltip("Game results use their shared action group; the authoring preview keeps inline continue controls by default.")]
        public bool hideInlineContinueButtons;
        public UnityEvent onCompleted = new UnityEvent();
        public UnityEvent onSkipped = new UnityEvent();
        public event Action<StoryExitReason> Finished;
        public event Action<StoryCutView> ViewChanged;
        /// <summary>
        /// #629: optional (volume multiplier, playback speed) for the sound of a cut, by its cutId. The game sets it from
        /// StoryMusicArt's Cut Sounds table; previews leave it null and play every sound as authored.
        /// </summary>
        public static Func<string, Vector2> VoiceTuning;
        private float voiceBaseVolume = -1f;
        public bool UserInputBlocked { get; private set; }
        public void SetInputBlocked(bool value) => UserInputBlocked = value;
        /// <summary>Per-playback user policy; direct Skip remains available to authoring and test tools.</summary>
        public bool UserSkipAllowed { get; private set; } = true;
        private Button policyHiddenSkip;
        private bool skipWasActive, skipWasInteractable;
        private Func<StoryCutView, StoryContinueState> continuePolicy;
        private Button policyContinueButton;
        private bool continueWasInteractable;

        /// <summary>Optional local cut policy. Previews and ordinary stories retain direct Next; tool Skip is never gated.</summary>
        public void SetContinuePolicy(Func<StoryCutView, StoryContinueState> policy)
        {
            RestoreContinueButton();
            continuePolicy = policy;
            RefreshContinuePolicy();
        }

        private StoryContinueState ContinueState => continuePolicy != null && currentView != null
            ? continuePolicy(currentView) : StoryContinueState.Default;

        private bool CanContinue
        {
            get { StoryContinueState state = ContinueState; return state != StoryContinueState.Hidden && state != StoryContinueState.Locked; }
        }

        public void RefreshContinuePolicy()
        {
            Button button = currentView != null ? currentView.continueButton : null;
            if (policyContinueButton != button) RestoreContinueButton();
            if (button == null) return;
            StoryContinueState state = ContinueState;
            if (state == StoryContinueState.Default) RestoreContinueButton();
            else
            {
                if (policyContinueButton == null)
                { policyContinueButton = button; continueWasInteractable = button.interactable; }
                button.interactable = state == StoryContinueState.Ready;
            }
            button.gameObject.SetActive(IsPlaying && IsReading && (currentView.requireContinueButton || !hideInlineContinueButtons)
                && state != StoryContinueState.Hidden);
        }

        private void RestoreContinueButton()
        {
            if (policyContinueButton != null) policyContinueButton.interactable = continueWasInteractable;
            policyContinueButton = null;
        }

        public void SetUserSkipAllowed(bool value)
        {
            UserSkipAllowed = value;
            UpdateSkipButton();
        }

        public int CurrentCutIndex { get; private set; } = -1;
        public int CurrentPageIndex { get; private set; } = -1;
        public bool IsPlaying { get; private set; }
        public bool AutoAdvance { get; private set; }
        public bool UsesSideTapNavigation => sequence != null && sequence.sideTapNavigation;
        private bool navigationHidden, nextWasActive, previousWasActive;
        public bool IsReading { get; private set; }
        public StoryCutView CurrentView => currentView;
        public bool IsTransitioning => activeCloudWipe != null || outgoingView != null;
        private bool IsCinematicCut => currentView != null && currentView.autoContinueAfterSeconds > 0;

        private StoryCutView currentView;
        private StoryCloudWipe activeCloudWipe;
        private bool shownCardHint;
        private AudioSource musicSource;
        private float musicTarget;
        private float elapsed;
        private float readElapsed;
        private int lastInputFrame = -1;
        private bool suppressMultiTouch;
        private StoryDialoguePage currentPage;
        private Text cachedDialogue, cachedSpeaker;
        private Image cachedPaper;
        private Sprite defaultPaperSprite;
        private Vector2 defaultPaperPosition, defaultPaperSize;
        private Shadow paperShadow;
        private bool defaultPaperShadowEnabled;
        private int defaultSpeakerFontSize;
        private Vector3 defaultSpeakerScale;
        private Image cachedBadge;
        private Material defaultBadgeMaterial;
        private Color defaultBadgeColor;
        private Vector2 defaultDialoguePosition, defaultDialogueSize, defaultSpeakerPosition, defaultSpeakerSize;
        private Color defaultSpeakerColor;
        private int defaultDialogueFontSize;
        private HorizontalWrapMode defaultDialogueWrap;
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
            PrepareSideNavigation();
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
            if (continuePolicy != null) RefreshContinuePolicy();
            UpdateMusic(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Clear only after EventSystem has processed the last release in this frame.
            if (StoryTapAdvance.PressedTouchCount == 0) suppressMultiTouch = false;
            if (continuePolicy != null) RefreshContinuePolicy();
        }

        internal void AdvanceTime(float seconds)
        {
            if (!IsPlaying || currentView == null) return;
            if (activeCloudWipe != null)
            {
                AdvanceCloudWipe(seconds);
                return;
            }
            AdvanceOpeningBlend(seconds);
            elapsed += seconds;
            currentView.Sample(elapsed);
            AdvanceDialogueFade(seconds);
            if (IsCinematicCut)
            {
                float end = Mathf.Max(currentView.EntranceDuration, currentView.autoContinueAfterSeconds);
                if (elapsed >= end)
                {
                    float remainder = elapsed - end;
                    AdvanceToNextCut();
                    if (IsPlaying && remainder > 0) AdvanceTime(remainder);
                }
                else if (!IsReading && elapsed >= currentView.EntranceDuration) BeginReading();
                return;
            }
            if (!IsReading)
            {
                if (elapsed >= currentView.EntranceDuration)
                {
                    BeginReading();
                    AdvanceDialogueFade(elapsed - currentView.EntranceDuration);
                }
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
            if (!CanContinue || IsCinematicCut || (!IsReading && currentView != null && currentView.waitForEntrance) || !AcceptUserInput()) return;
            Next();
        }

        public void RequestSurfaceAdvance()
        {
            if (currentView != null && currentView.requireContinueButton) return;
            RequestNext();
        }

        public void RequestSurfacePrevious()
        {
            if (!UsesSideTapNavigation || IsCinematicCut ||
                CurrentCutIndex == 0 && CurrentPageIndex == 0 || !AcceptUserInput()) return;
            AutoAdvance = false;
            UpdateAutoLabel();
            if (CurrentPageIndex > 0) ShowPage(CurrentPageIndex - 1);
            else
            {
                Previous();
                if (!IsCinematicCut && sequence.cuts[CurrentCutIndex].PageCount > 1)
                    ShowPage(sequence.cuts[CurrentCutIndex].PageCount - 1);
            }
        }

        private void PrepareSideNavigation()
        {
            if (UsesSideTapNavigation && !navigationHidden)
            {
                nextWasActive = nextButton != null && nextButton.gameObject.activeSelf;
                previousWasActive = previousButton != null && previousButton.gameObject.activeSelf;
                if (nextButton != null) nextButton.gameObject.SetActive(false);
                if (previousButton != null) previousButton.gameObject.SetActive(false);
                navigationHidden = true;
            }
            else if (!UsesSideTapNavigation && navigationHidden)
            {
                if (nextButton != null) nextButton.gameObject.SetActive(nextWasActive);
                if (previousButton != null) previousButton.gameObject.SetActive(previousWasActive);
                navigationHidden = false;
            }
            if (!UsesSideTapNavigation || GetComponentInChildren<StoryTapAdvance>(true) != null) return;
            // Authoring previews use the same input surface as the game, beneath interactive controls.
            var surface = new GameObject("Story Tap Surface", typeof(RectTransform), typeof(Image), typeof(StoryTapAdvance));
            surface.transform.SetParent(cutHost.parent, false);
            surface.transform.SetAsFirstSibling();
            var rect = (RectTransform)surface.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            surface.GetComponent<Image>().color = Color.clear;
            surface.GetComponent<StoryTapAdvance>().player = this;
        }

        public void RequestPrevious() { if (AcceptUserInput()) Previous(); }
        public void RequestSkip()
        {
            if (UserSkipAllowed && !(hideSkipDuringCinematics && IsCinematicCut) && AcceptUserInput(true)) Skip();
        }

        private bool AcceptUserInput(bool allowOpeningBlend = false)
        {
            if (!IsPlaying || UserInputBlocked || activeCloudWipe != null ||
                outgoingView != null && !allowOpeningBlend) return false;
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
            if (!IsPlaying || activeCloudWipe != null || !CanContinue) return;
            // Direct sequencing is also used by editor tooling. UI requests are gated before reaching here.
            ClearOpeningBlend();
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
            AdvanceToNextCut();
        }

        private void AdvanceToNextCut()
        {
            int next = CurrentCutIndex + 1;
            if (next >= sequence.cuts.Count) Finish(StoryExitReason.Completed);
            else if (currentView.exitCloudWipe != null) StartCloudWipe(next);
            else ShowCut(next);
        }

        /// <summary>Return to the previous picture's first page and pause automatic advance.</summary>
        public void Previous()
        {
            if (!IsPlaying || IsTransitioning || CurrentCutIndex <= 0) return;
            AutoAdvance = false;
            UpdateAutoLabel();
            var previousPicture = currentView;
            ShowCut(CurrentCutIndex - 1);
            if (currentView == previousPicture || IsCinematicCut) return;
            // Reviewing a previous picture should not require waiting for its entrance again.
            elapsed = currentView.EntranceDuration;
            currentView.Sample(elapsed);
            BeginReading();
        }

        private void UpdatePreviousButton()
        {
            if (previousButton != null) previousButton.interactable = IsPlaying && CurrentCutIndex > 0;
            if (nextButton != null) nextButton.interactable = IsPlaying && !IsCinematicCut;
            UpdateSkipButton();
        }

        private void UpdateSkipButton()
        {
            // Restore the exact caller-owned state when this local policy is released or the button is replaced.
            if (policyHiddenSkip != null && (UserSkipAllowed || policyHiddenSkip != skipButton))
            {
                policyHiddenSkip.gameObject.SetActive(skipWasActive);
                policyHiddenSkip.interactable = skipWasInteractable;
                policyHiddenSkip = null;
            }
            if (skipButton == null) return;
            if (!UserSkipAllowed)
            {
                if (policyHiddenSkip == null)
                {
                    policyHiddenSkip = skipButton;
                    skipWasActive = skipButton.gameObject.activeSelf;
                    skipWasInteractable = skipButton.interactable;
                }
                skipButton.gameObject.SetActive(false);
                skipButton.interactable = false;
            }
            else if (hideSkipDuringCinematics)
                skipButton.gameObject.SetActive(IsPlaying && !IsCinematicCut);
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

        private void StartCloudWipe(int index)
        {
            var wipe = Instantiate(currentView.exitCloudWipe, cutHost, false);
            activeCloudWipe = wipe;
            if (wipe.BeginManual(() =>
            {
                ShowCut(index);
                if (activeCloudWipe == wipe) wipe.transform.SetAsLastSibling();
            })) return;

            ClearCloudWipe();
            ShowCut(index);
        }

        private void AdvanceCloudWipe(float seconds)
        {
            var wipe = activeCloudWipe;
            wipe.Advance(seconds);
            if (activeCloudWipe != wipe) return;

            // Let the new picture unfold behind the clouds. Text and sounds wait for the overlay.
            if (wipe.HasCovered)
            {
                elapsed = wipe.RevealElapsed;
                currentView.SampleArtwork(elapsed);
            }
            if (!wipe.IsPlaying) ClearCloudWipe();
        }

        private void ClearCloudWipe()
        {
            var wipe = activeCloudWipe;
            activeCloudWipe = null;
            if (wipe == null) return;
            wipe.gameObject.SetActive(false);
            Destroy(wipe.gameObject);
        }

        private void ShowCut(int index)
        {
            var previousView = currentView;
            var cut = sequence.cuts[index];
            if (TryChangeAuthoredDialogue(index, cut)) return;
            if (voiceSource != null) { voiceSource.Stop(); voiceSource.clip = null; }
            var nextView = Instantiate(cut.viewPrefab, cutHost, false);
            // Prepare the next background before removing the picture covering the paper base.
            PrepareOpeningEntrance(nextView, previousView);
            nextView.Sample(0f);
            CurrentCutIndex = index;
            currentView = nextView;
            var card = currentView.GetComponentInChildren<StoryCardFlip>();
            if (card != null && card.swipeHint != null)
            {
                card.SetHintVisible(!shownCardHint);
                shownCardHint = true;
            }
            if (currentView.continueButton != null)
            {
                currentView.continueButton.gameObject.SetActive(false);
                currentView.continueButton.onClick.AddListener(RequestNext);
            }
            SetMusic(currentView.music, currentView.musicVolume);
            ApplyDialogueLayout();
            elapsed = readElapsed = 0f;
            CurrentPageIndex = 0;
            IsReading = false;
            currentPage = cut.GetPage(0);
            UpdatePageText();
            SetDialogueVisible(false);
            UpdatePreviousButton();
            if (!StartOpeningBlend(previousView)) ReleaseView(previousView);
            ViewChanged?.Invoke(currentView);
        }

        private bool TryChangeAuthoredDialogue(int index, StoryCutDefinition cut)
        {
            var source = cut.viewPrefab;
            if (currentView == null || source == null || currentView.authoredDialogue == null ||
                source.authoredDialogue == null || string.IsNullOrEmpty(source.artworkContinuityKey) ||
                currentView.artworkContinuityKey != source.artworkContinuityKey) return false;

            // Keep the actual background/character instances and elapsed animation time, in both directions.
            var previousDialogue = currentView.authoredDialogue;
            var nextDialogue = Instantiate(source.authoredDialogue, currentView.transform, false);
            nextDialogue.transform.SetAsLastSibling();
            previousDialogue.gameObject.SetActive(false);
            Destroy(previousDialogue.gameObject);
            currentView.authoredDialogue = nextDialogue;
            currentView.sourceFigmaNumber = source.sourceFigmaNumber;
            currentView.sourceFigmaNodeId = source.sourceFigmaNodeId;
            CurrentCutIndex = index;
            CurrentPageIndex = 0;
            currentPage = cut.GetPage(0);
            if (voiceSource != null) { voiceSource.Stop(); voiceSource.clip = null; }
            ApplyDialogueLayout();
            UpdatePageText();
            UpdatePreviousButton();
            BeginReading();
            // Dialogue-only pages keep the paper visible; no second entrance or blank panel.
            fadingDialogue = null;
            nextDialogue.alpha = 1f;
            ViewChanged?.Invoke(currentView);
            return true;
        }

        private void BeginReading()
        {
            IsReading = true;
            RefreshContinuePolicy();
            readElapsed = 0f;
            SetDialogueVisible(!currentView.hideEmptyDialogue ||
                !string.IsNullOrWhiteSpace(currentPage.dialogue) || !string.IsNullOrWhiteSpace(currentPage.speaker));
            if (voiceSource != null && currentPage.voice != null)
            {
                if (voiceBaseVolume < 0f) voiceBaseVolume = voiceSource.volume;
                StoryCutDefinition cut = sequence != null && CurrentCutIndex >= 0 && CurrentCutIndex < sequence.cuts.Count
                    ? sequence.cuts[CurrentCutIndex] : null;
                Vector2 tuning = VoiceTuning != null && cut != null ? VoiceTuning(cut.cutId) : Vector2.one;
                voiceSource.volume = voiceBaseVolume * Mathf.Clamp01(tuning.x);
                voiceSource.pitch = tuning.y > 0f ? tuning.y : 1f;
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
            ApplySpeakerStyle();
            if (progressText != null && progressText.gameObject.activeSelf == hideProgress)
                progressText.gameObject.SetActive(!hideProgress);
            if (progressText != null && !hideProgress)
            {
                int pages = sequence.cuts[CurrentCutIndex].PageCount;
                progressText.text = pages > 1
                    ? $"{CurrentCutIndex + 1}/{sequence.cuts.Count} · {CurrentPageIndex + 1}/{pages}"
                    : $"{CurrentCutIndex + 1} / {sequence.cuts.Count}";
            }
        }

        private void ApplySpeakerStyle()
        {
            if (speakerPalette != null && speakerPalette.TryGet(currentPage.speaker, out var style))
            {
                if (speakerText != null) speakerText.color = style.textColor;
                if (speakerBadge != null)
                {
                    speakerBadge.material = speakerPalette.badgeMaterial;
                    speakerBadge.color = style.badgeColor;
                }
            }
            else
            {
                if (speakerText != null) speakerText.color = currentView.overrideDialogueLayout ? currentView.speakerColor : defaultSpeakerColor;
                if (speakerBadge != null)
                {
                    speakerBadge.material = defaultBadgeMaterial;
                    speakerBadge.color = defaultBadgeColor;
                }
            }
        }

        private void ApplyDialogueLayout()
        {
            if (dialoguePaper != null)
            {
                if (cachedPaper != dialoguePaper)
                {
                    cachedPaper = dialoguePaper;
                    defaultPaperSprite = dialoguePaper.sprite;
                    defaultPaperPosition = dialoguePaper.rectTransform.anchoredPosition;
                    defaultPaperSize = dialoguePaper.rectTransform.sizeDelta;
                    paperShadow = dialoguePaper.GetComponent<Shadow>();
                    defaultPaperShadowEnabled = paperShadow != null && paperShadow.enabled;
                }
                bool customPaper = currentView.dialoguePaper != null;
                var bounds = ValidLayout(currentView.dialoguePaperRect, new Rect(11, 677, 379, 181));
                dialoguePaper.sprite = customPaper ? currentView.dialoguePaper : defaultPaperSprite;
                dialoguePaper.rectTransform.anchoredPosition = customPaper
                    ? new Vector2(bounds.x, -bounds.y) : defaultPaperPosition;
                dialoguePaper.rectTransform.sizeDelta = customPaper ? bounds.size : defaultPaperSize;
                // The revised source art already includes its own shadow.
                if (paperShadow != null) paperShadow.enabled = !customPaper && defaultPaperShadowEnabled;
            }
            if (dialogueText != null)
            {
                var rect = dialogueText.rectTransform;
                if (cachedDialogue != dialogueText)
                {
                    cachedDialogue = dialogueText;
                    defaultDialoguePosition = rect.anchoredPosition;
                    defaultDialogueSize = rect.sizeDelta;
                    defaultDialogueFontSize = dialogueText.fontSize;
                    defaultDialogueWrap = dialogueText.horizontalOverflow;
                    defaultDialogueScale = rect.localScale;
                }
                dialogueText.horizontalOverflow = currentView.dialogueNoWrap ? HorizontalWrapMode.Overflow : defaultDialogueWrap;
                bool customSize = currentView.dialogueFontSize > 0;
                dialogueText.fontSize = customSize
                    ? Mathf.Max(1, Mathf.FloorToInt(currentView.dialogueFontSize + .5f)) : defaultDialogueFontSize;
                rect.localScale = customSize ? Vector3.one : defaultDialogueScale;
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
                    defaultSpeakerFontSize = speakerText.fontSize;
                    defaultSpeakerScale = rect.localScale;
                    defaultSpeakerWrap = speakerText.horizontalOverflow;
                    defaultSpeakerAlignment = speakerText.alignment;
                }
                bool customSize = currentView.speakerFontSize > 0;
                speakerText.fontSize = customSize
                    ? Mathf.Max(1, Mathf.FloorToInt(currentView.speakerFontSize + .5f)) : defaultSpeakerFontSize;
                rect.localScale = customSize ? Vector3.one : defaultSpeakerScale;
                var layout = ValidLayout(currentView.speakerRect, new Rect(84, 721, 36, 18));
                rect.anchoredPosition = currentView.overrideDialogueLayout
                    ? new Vector2(layout.x, -layout.y) : defaultSpeakerPosition;
                rect.sizeDelta = currentView.overrideDialogueLayout ? layout.size : defaultSpeakerSize;
                speakerText.color = currentView.overrideDialogueLayout ? currentView.speakerColor : defaultSpeakerColor;
                speakerText.alignment = currentView.overrideDialogueLayout && currentView.centerSpeakerName
                    ? TextAnchor.MiddleCenter : defaultSpeakerAlignment;
                // Keep an authored speaker name on one line.
                speakerText.horizontalOverflow = currentView.overrideDialogueLayout ? HorizontalWrapMode.Overflow : defaultSpeakerWrap;
            }
            if (speakerBadge != null)
            {
                if (cachedBadge != speakerBadge)
                {
                    cachedBadge = speakerBadge;
                    defaultBadgeMaterial = speakerBadge.material;
                    defaultBadgeColor = speakerBadge.color;
                }
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
            if (currentView != null && currentView.authoredDialogue != null)
            {
                currentView.authoredDialogue.alpha = visible ? 1f : 0f;
                currentView.authoredDialogue.blocksRaycasts = false;
                if (visible && sequence != null && sequence.smoothOpeningTransitions)
                {
                    fadingDialogue = currentView.authoredDialogue;
                    dialogueFadeElapsed = 0f;
                    fadingDialogue.alpha = 0f;
                }
                else fadingDialogue = null;
                visible = false;
            }
            if (dialogueGroup == null) return;
            dialogueGroup.alpha = visible ? 1f : 0f;
            dialogueGroup.interactable = visible;
            dialogueGroup.blocksRaycasts = visible;
        }

        private void SetMusic(AudioClip clip, float volume)
        {
            musicTarget = clip != null ? Mathf.Clamp01(volume) : 0;
            if (!Application.isPlaying || clip == null) return;
            if (musicSource == null)
            {
                var music = new GameObject("Story Music");
                music.transform.SetParent(transform, false);
                musicSource = music.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0;
            }
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.volume = 0;
            musicSource.Play();
        }

        private void UpdateMusic(float seconds)
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicTarget, seconds * .36f);
            if (musicTarget == 0 && musicSource.volume == 0) musicSource.Stop();
        }

        private void Finish(StoryExitReason reason)
        {
            // Clear state before invoking callbacks, so repeated clicks cannot finish twice.
            IsPlaying = false;
            if (currentView != null && currentView.requireContinueButton && currentView.continueButton != null)
                currentView.continueButton.gameObject.SetActive(false);
            ClearCloudWipe();
            ClearOpeningBlend();
            UpdatePreviousButton();
            if (voiceSource != null) voiceSource.Stop();
            musicTarget = 0;
            if (reason == StoryExitReason.Completed) onCompleted.Invoke();
            else onSkipped.Invoke();
            Finished?.Invoke(reason);
        }

        public void Cancel()
        {
            RestoreContinueButton();
            IsPlaying = IsReading = false;
            ClearCloudWipe();
            ClearOpeningBlend();
            fadingDialogue = null;
            musicTarget = 0;
            if (musicSource != null) musicSource.Stop();
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
