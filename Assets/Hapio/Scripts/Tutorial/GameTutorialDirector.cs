using System.Collections;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477) 전투 화면. GameBattleBindings가 연습 판일 때만 붙인다.
    /// 1) HUD: 자리 동그라미(P1·P2·P3)를 캐릭터 얼굴(금옥이·불이·묵이)로 바꾸고 타이머를 숨긴다. 북 미니맵도 얼굴로 그린다.
    /// 2) 대사: 말하는 사람 자리의 얼굴에서 말풍선이 살짝 커졌다가 제자리로 뜨고, 화면을 누르면 다음 장면으로 넘어간다.
    ///    이때는 화면 전체가 눌림을 받으므로 판의 구슬·북은 움직이지 않는다.
    /// 3) 동작: 말풍선 대신 검은 신라문화체 안내 띠와 손 그림을 보여 주고, 판에는 그 동작만 열어 둔 채 해낼 때까지 기다린다.
    ///    해내면 "성공!"이 잠깐 뜬다.
    /// 장면이 가리키는 곳(박·북·기운 막대)이 있으면 화면을 어둡게 하고 그곳만 밝힌다. 마지막 장면 뒤에는 어두워지며 방 목록으로 간다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed partial class GameTutorialDirector : MonoBehaviour
    {
        /// <summary>Above the battle HUD and DEV panel, below the start cover (30000) and the scene fade (30100).</summary>
        public const int OverlaySortingOrder = 20000;
        private const int Seats = 3;

        private GameSession session;
        private GameBattleBindings battle;
        private LabHud hud;
        private LabOrbBoard board;
        private GameTutorialArt art;
        private GameObject timerFrame;
        private readonly Image[] portraits = new Image[Seats];
        private readonly Vector3[] portraitScales = new Vector3[Seats];
        private readonly Vector2[] portraitPositions = new Vector2[Seats];
        private readonly Vector3[] corners = new Vector3[4];

        private Canvas overlayCanvas;
        private RectTransform overlay;
        private Image tapCatcher;
        private CanvasGroup dimGroup;
        private GameTutorialSpotlight spotlight;
        private Image speakerPortrait;
        private RectTransform bubbleRoot;
        private RectTransform bubbleArt;
        private Text bubbleText;
        private Image tapHint;
        private Image lineTapArea;
        private float hintNudgeAt = -1f;
        // The bubble hangs below its speaker's face (tail up) to keep a lit area above clear.
        private bool bubbleBelow;
        private RectTransform banner;
        private Text bannerText;
        private Text successText;
        private Image hand;
        private GameTutorialArrow arrow;
        private string pairYinId, pairYangId;

        private int stepIndex;
        private int beatIndex = -1;
        private float playableAt = -1f;
        private float shownAt;
        private float popStartedAt;
        private float dimTarget;
        private bool finishing;
        private bool unavailable;
        private int actionBaseline;
        private bool throwHit;
        private bool listeningToThrows;
        private float remarkAt = -1f;
        private GameTutorialSpeaker remarkSpeaker;
        private float remarkSeconds;
        // The tease stays up even while an orb is held, and the word that follows it once it ends.
        private bool remarkHolds;
        private string remarkThen;
        private int giveBackStage;
        private float giveBackAt = -1f;
        private float wrongSideReturnAt = -1f;
        private float bombReturnAt = -1f;

        /// <summary>How many times the drum went to 묵이 by mistake in 4단계 (each came straight back).</summary>
        public int WrongSidePasses { get; private set; }
        /// <summary>금옥이's mirror window is showing the drum arriving.</summary>
        public bool IsShowingMirror => mirrorAt >= 0f && mirror != null && mirror.gameObject.activeSelf;

        /// <summary>How many throws missed in 3단계 (each gave the red orb back).</summary>
        public int ThrowMisses { get; private set; }
        /// <summary>How many times the fifth-miss tease has been said (once at most).</summary>
        public int TeasesShown { get; private set; }
        /// <summary>A short word ("다시 해 봐!") is up during an action.</summary>
        public bool IsShowingRemark => remarkAt >= 0f && bubbleRoot != null && bubbleRoot.gameObject.activeSelf;
        private int actionProgress;
        private float actionDoneAt = -1f;
        private float successAt = -1f;
        private bool spotlightPlaced;
        private Rect spotRect;
        private float spotCorner, spotSoft, spotAngle;

        /// <summary>The step being played (index into <see cref="GameTutorialScript.Steps"/>).</summary>
        public int StepIndex => stepIndex;
        /// <summary>The beat on screen in that step, or -1 before the first one.</summary>
        public int BeatIndex => beatIndex;
        public bool IsFinishing => finishing;
        public bool HasBeat => beatIndex >= 0 && !finishing && stepIndex < GameTutorialScript.Steps.Count;
        public GameTutorialBeat CurrentBeat => GameTutorialScript.Steps[stepIndex].Beats[beatIndex];
        public bool IsShowingLine => HasBeat && CurrentBeat.IsLine && bubbleRoot != null && bubbleRoot.gameObject.activeSelf;
        /// <summary>The ▽ on the bubble: up only on a line that a tap moves on from, once it can.</summary>
        public bool IsShowingTapHint => tapHint != null && tapHint.gameObject.activeInHierarchy;
        /// <summary>The ▽ is jumping because the screen was tapped off the bubble.</summary>
        public bool IsNudgingTapHint => hintNudgeAt >= 0f && Now - hintNudgeAt < art.tapHintNudgeSeconds;
        /// <summary>Taps off the bubble while a line was up (they never move it on).</summary>
        public int ScreenTapsOffBubble { get; private set; }
        /// <summary>Waiting for the player (or the gauge) to finish the current action.</summary>
        public bool IsWaitingForAction => HasBeat && !CurrentBeat.IsLine && actionDoneAt < 0f;
        /// <summary>How many times the current action has been done.</summary>
        public int ActionProgress => actionProgress;

        public void Bind(GameSession owner, GameBattleBindings bindings)
        {
            session = owner;
            battle = bindings;
            hud = bindings != null ? bindings.hud : null;
            board = bindings != null ? bindings.board : null;
            art = GameTutorialArt.Load();
            if (session == null || hud == null || board == null || art == null || art.bubble == null)
            {
                Debug.LogWarning("HAPIO_TUTORIAL_UNAVAILABLE: the tutorial art or the battle HUD is missing.", this);
                unavailable = true;
                return;
            }
            // Lines own the screen; each action opens only its own gesture.
            board.SetAllowedGestures(LabBoardGestures.None);
            DressHud();
            BuildOverlay();
        }

        /// <summary>Bubble scale over its pop, t = 0..1: up past full size, then settle back (#477).</summary>
        public static float PopScale(float t, float overshoot)
        {
            t = Mathf.Clamp01(t);
            const float rise = .6f;
            if (t < rise)
            {
                float u = 1f - t / rise;
                return overshoot * (1f - u * u * u);
            }
            float v = (t - rise) / (1f - rise);
            return Mathf.Lerp(overshoot, 1f, v * v * (3f - 2f * v));
        }

        /// <summary>How far the pointing hand has pressed in, 0..1, over one tap cycle (t = 0..1).</summary>
        public static float HandPress(float t)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < .15f) return t / .15f;
            if (t < .35f) return 1f - (t - .15f) / .2f;
            return 0f;
        }

        /// <summary>The sliver of stamina left for the short cue: a share of one tap's cost, always short of a tap.</summary>
        public static float ShortStaminaFor(float generateCost, float shareOfTap) =>
            Mathf.Max(0f, generateCost) * Mathf.Clamp(shareOfTap, .05f, .95f);

        /// <summary>The guide banner's words; a counted action adds its progress, e.g. "(1/3)".</summary>
        public static string BannerText(GameTutorialBeat beat, int done) => beat.Count > 1
            ? beat.Text + " (" + Mathf.Clamp(done, 0, beat.Count) + "/" + beat.Count + ")" : beat.Text;

        // ---------- HUD ----------

        private void DressHud()
        {
            // Seat order: P1 right neighbor (금옥이), P2 me (불이), P3 left neighbor (묵이).
            Sprite[] faces = { art.geumok, art.bul, art.muk };
            hud.playerCircleSprites = WithFaces(hud.playerCircleSprites, faces);
            LabDrumMinimap minimap = FindInBattle<LabDrumMinimap>();
            if (minimap != null) minimap.playerSprites = WithFaces(minimap.playerSprites, faces);
            Transform frame = hud.timeText != null ? hud.timeText.transform.parent : null;
            if (frame != null && (hud.battleUi == null || frame != hud.battleUi.transform)) timerFrame = frame.gameObject;
            portraits[0] = hud.rightPlayerCircle;
            portraits[1] = hud.localPlayerCircle;
            portraits[2] = hud.leftPlayerCircle;
            for (int seat = 0; seat < Seats; seat++)
            {
                if (portraits[seat] == null) continue;
                portraits[seat].preserveAspect = true;
                portraitScales[seat] = portraits[seat].rectTransform.localScale
                    * (seat == 1 ? art.localPortraitScale : art.neighborPortraitScale);
                portraitPositions[seat] = portraits[seat].rectTransform.anchoredPosition;
            }
            ApplyHudDressing();
        }

        private static Sprite[] WithFaces(Sprite[] original, Sprite[] faces)
        {
            var result = new Sprite[Mathf.Max(original != null ? original.Length : 0, faces.Length)];
            if (original != null) System.Array.Copy(original, result, original.Length);
            for (int i = 0; i < faces.Length; i++) if (faces[i] != null) result[i] = faces[i];
            return result;
        }

        /// <summary>LabHud refreshes its texts and samples every frame; this runs after it.</summary>
        private void ApplyHudDressing()
        {
            if (timerFrame != null && timerFrame.activeSelf) timerFrame.SetActive(false);
            Hide(hud.leftNeighborText);
            Hide(hud.rightNeighborText);
            Hide(hud.localPlayerPatternSample);
            Hide(hud.leftPlayerPatternSample);
            Hide(hud.rightPlayerPatternSample);
            for (int seat = 0; seat < Seats; seat++)
            {
                if (portraits[seat] == null) continue;
                if (portraits[seat].rectTransform.localScale != portraitScales[seat])
                    portraits[seat].rectTransform.localScale = portraitScales[seat];
                FitPortraitOnScreen(seat);
            }
        }

        /// <summary>
        /// The neighbor circles are saved half over the screen edge. A face must show whole, so its scaled circle is
        /// moved inward just enough to keep <see cref="GameTutorialArt.portraitScreenMargin"/> from every edge.
        /// Measured from the saved position each frame, so the shift never accumulates and follows any screen size.
        /// </summary>
        private void FitPortraitOnScreen(int seat)
        {
            RectTransform rect = portraits[seat].rectTransform;
            Canvas canvas = portraits[seat].canvas;
            var root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            var parent = rect.parent as RectTransform;
            if (root == null || parent == null) return;
            // The tutorial's own nudge (불이 only for now), in screen points, before keeping it on screen.
            Vector2 nudge = seat == 1 ? art.localPortraitOffset : Vector2.zero;
            Vector2 saved = portraitPositions[seat] + (Vector2)parent.InverseTransformVector(root.TransformVector(nudge));
            Vector3 fromSaved = parent.TransformVector(rect.anchoredPosition - saved);
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = root.InverseTransformPoint(corner - fromSaved);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            Rect area = root.rect;
            float margin = art.portraitScreenMargin;
            var shift = new Vector2(Inward(min.x, max.x, area.xMin + margin, area.xMax - margin),
                Inward(min.y, max.y, area.yMin + margin, area.yMax - margin));
            Vector2 target = saved + (Vector2)parent.InverseTransformVector(root.TransformVector(shift));
            if ((rect.anchoredPosition - target).sqrMagnitude > 1e-4f) rect.anchoredPosition = target;
        }

        private readonly List<Rect> keepOutCircles = new List<Rect>(Seats);
        private readonly List<Rect> keepOutBoxes = new List<Rect>(1);
        private LabStaminaShortFeedback shortFeedback;

        /// <summary>
        /// Orbs never cover the three faces or the stamina bar in the tutorial: the board keeps invisible walls over
        /// them (a little larger by <see cref="GameTutorialArt.keepOutPadding"/>), following the HUD as it is laid out.
        /// </summary>
        private void UpdateKeepOut()
        {
            // The short cue shakes the whole gauge for a moment; the walls stay where the gauge rests.
            if (shortFeedback == null) shortFeedback = FindInBattle<LabStaminaShortFeedback>();
            if (shortFeedback != null && shortFeedback.Playing) return;
            keepOutCircles.Clear();
            keepOutBoxes.Clear();
            for (int seat = 0; seat < Seats; seat++)
                if (portraits[seat] != null && TryScreenRect(portraits[seat].rectTransform, out Rect face))
                    keepOutCircles.Add(Padded(face, portraits[seat].canvas));
            var bar = hud.staminaFill != null ? hud.staminaFill.transform.parent as RectTransform : null;
            if (bar != null && TryScreenRect(bar, out Rect gauge)) keepOutBoxes.Add(Padded(gauge, hud.staminaFill.canvas));
            // Nor over the names under the faces.
            foreach (Text label in nameLabels)
                if (label != null && TryScreenRect(label.rectTransform, out Rect name)) keepOutBoxes.Add(Padded(name, overlayCanvas));
            board.SetKeepOut(keepOutCircles, keepOutBoxes);
        }

        private Rect Padded(Rect screen, Canvas canvas)
        {
            float pad = art.keepOutPadding * (canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
            return new Rect(screen.xMin - pad, screen.yMin - pad, screen.width + pad * 2f, screen.height + pad * 2f);
        }

        /// <summary>A visible HUD element's area in screen pixels.</summary>
        private bool TryScreenRect(RectTransform target, out Rect rect)
        {
            rect = default;
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.rootCanvas.worldCamera;
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 0f && rect.height > 0f;
        }

        /// <summary>How far a span [low, high] must move to sit inside [from, to]; nothing when it is too large to fit.</summary>
        public static float Inward(float low, float high, float from, float to)
        {
            if (high - low > to - from) return 0f;
            if (low < from) return from - low;
            if (high > to) return to - high;
            return 0f;
        }

        private static void Hide(Graphic graphic)
        {
            if (graphic != null && graphic.enabled) graphic.enabled = false;
        }

        private T FindInBattle<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        // ---------- Overlay ----------

        private void BuildOverlay()
        {
            var root = new GameObject("Tutorial Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            overlayCanvas = root.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = OverlaySortingOrder;
            CopyHudScaler(root.GetComponent<CanvasScaler>());
            overlay = (RectTransform)root.transform;

            // While a line is read every touch lands here, so the board under it never moves. It no longer moves the line
            // on (#536): only the bubble does; a tap here nudges the ▽ to show where.
            tapCatcher = AddImage("Screen Blocker", overlay, Color.clear, null, true);
            Stretch(tapCatcher.rectTransform);
            Button blocker = tapCatcher.gameObject.AddComponent<Button>();
            blocker.transition = Selectable.Transition.None;
            blocker.onClick.AddListener(OnScreenTap);
            // Names go under the dim, so they darken with the faces.
            BuildNames();

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasGroup));
            dim.transform.SetParent(overlay, false);
            Stretch((RectTransform)dim.transform);
            dimGroup = dim.GetComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.blocksRaycasts = dimGroup.interactable = false;
            // One mesh with a hole: separate pieces would double-darken where they meet.
            spotlight = new GameObject("Spotlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameTutorialSpotlight))
                .GetComponent<GameTutorialSpotlight>();
            spotlight.rectTransform.SetParent(dim.transform, false);
            Stretch(spotlight.rectTransform);
            spotlight.color = art.dimColor;
            spotlight.raycastTarget = false;

            speakerPortrait = AddImage("Speaker", overlay, Color.white, null, false);
            speakerPortrait.preserveAspect = true;
            speakerPortrait.enabled = false;

            BuildBanner();
            BuildMirror();
            arrow = new GameObject("Guide Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameTutorialArrow))
                .GetComponent<GameTutorialArrow>();
            arrow.rectTransform.SetParent(overlay, false);
            Stretch(arrow.rectTransform);
            arrow.color = art.arrowColor;
            arrow.raycastTarget = false;
            arrow.gameObject.SetActive(false);
            hand = AddImage("Pointing Hand", overlay, Color.white, art.hand, false);
            hand.preserveAspect = true;
            hand.rectTransform.pivot = art.handTip;
            hand.rectTransform.sizeDelta = art.handSize;
            hand.gameObject.SetActive(false);

            bubbleRoot = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
            bubbleRoot.SetParent(overlay, false);
            Center(bubbleRoot);
            bubbleRoot.sizeDelta = art.bubbleSize;
            Image bubbleImage = AddImage("Bubble Art", bubbleRoot, Color.white, art.bubble, false);
            bubbleArt = bubbleImage.rectTransform;
            Stretch(bubbleArt);
            // The line moves on from the bubble only: the bubble and a little around it, following it wherever it hangs.
            lineTapArea = AddImage("Tap To Continue", bubbleRoot, Color.clear, null, true);
            RectTransform area = lineTapArea.rectTransform;
            Stretch(area);
            area.offsetMin = -Vector2.one * art.lineTapPadding;
            area.offsetMax = Vector2.one * art.lineTapPadding;
            area.SetAsFirstSibling();
            Button next = lineTapArea.gameObject.AddComponent<Button>();
            next.transition = Selectable.Transition.None;
            next.onClick.AddListener(OnTap);
            bubbleText = AddText("Line", bubbleRoot, art.dialogueFont, art.maxFontSize, art.textColor);
            bubbleText.resizeTextMinSize = Mathf.Min(art.minFontSize, art.maxFontSize);
            // ▽ at the words' bottom-right corner: on the bubble, not its (mirrored) art, so it is never flipped.
            tapHint = AddImage("Tap Hint", bubbleRoot, Color.white, art.tapHint, false);
            tapHint.preserveAspect = true;
            tapHint.rectTransform.pivot = new Vector2(1f, 0f);
            tapHint.rectTransform.sizeDelta = art.tapHintSize;
            tapHint.gameObject.SetActive(false);
            bubbleRoot.gameObject.SetActive(false);
            BuildSkip();
            BuildLogButton();
            BuildPause(root);
        }

        private void BuildBanner()
        {
            banner = AddImage("Guide Banner", overlay, art.bannerColor, null, false).rectTransform;
            banner.pivot = new Vector2(.5f, .5f);
            banner.sizeDelta = new Vector2(0f, art.bannerHeight);
            bannerText = AddText("Guide", banner, art.bannerFont, art.bannerFontSize, art.bannerTextColor);
            bannerText.rectTransform.offsetMin = new Vector2(12f, 0f);
            bannerText.rectTransform.offsetMax = new Vector2(-12f, 0f);
            bannerText.resizeTextMinSize = Mathf.Min(12, art.bannerFontSize);
            banner.gameObject.SetActive(false);

            successText = AddText("Success", overlay, art.bannerFont, art.successFontSize, art.successColor);
            RectTransform success = successText.rectTransform;
            success.pivot = new Vector2(.5f, .5f);
            success.sizeDelta = new Vector2(240f, art.successFontSize * 1.6f);
            successText.resizeTextForBestFit = false;
            successText.text = GameTutorialScript.SuccessText;
            if (art.successStroke > 0f)
            {
                // Figma's stroke, drawn as an outline that far out on every side.
                Outline stroke = successText.gameObject.AddComponent<Outline>();
                stroke.effectColor = art.successStrokeColor;
                stroke.effectDistance = new Vector2(art.successStroke, -art.successStroke);
                stroke.useGraphicAlpha = true;
            }
            successText.gameObject.SetActive(false);
            PlaceBanner(art.bannerTopFraction);
        }

        /// <summary>The guide banner, and "성공!" in its place, centered this far down the screen (0 top, 1 bottom).</summary>
        private void PlaceBanner(float topFraction)
        {
            float y = 1f - Mathf.Clamp01(topFraction);
            banner.anchorMin = new Vector2(0f, y);
            banner.anchorMax = new Vector2(1f, y);
            banner.sizeDelta = new Vector2(0f, art.bannerHeight);
            banner.anchoredPosition = Vector2.zero;
            RectTransform success = successText.rectTransform;
            success.anchorMin = success.anchorMax = new Vector2(.5f, y);
            success.anchoredPosition = Vector2.zero;
        }

        private Text AddText(string name, Transform parent, Font font, int size, Color color)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            Stretch(text.rectTransform);
            text.font = font != null ? font
                : hud.timeText != null ? hud.timeText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.color = color;
            return text;
        }

        private void CopyHudScaler(CanvasScaler target)
        {
            Graphic sample = hud.localPlayerCircle != null ? (Graphic)hud.localPlayerCircle : hud.timeText;
            CanvasScaler source = sample != null && sample.canvas != null
                ? sample.canvas.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (source == null)
            {
                target.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                target.referenceResolution = new Vector2(390f, 844f);
                target.matchWidthOrHeight = .5f;
                return;
            }
            target.uiScaleMode = source.uiScaleMode;
            target.referenceResolution = source.referenceResolution;
            target.screenMatchMode = source.screenMatchMode;
            target.matchWidthOrHeight = source.matchWidthOrHeight;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
            target.scaleFactor = source.scaleFactor;
        }

        private static Image AddImage(string name, Transform parent, Color color, Sprite sprite, bool raycast)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            Center(image.rectTransform);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // ---------- Script ----------

        private void Update()
        {
            AdvanceClock();
            if (session == null) return;
            if (unavailable)
            {
                if (!finishing) { finishing = true; session.FinishTutorial(); }
                return;
            }
            if (finishing) return;
            // The skip dialog and the pause menu stop the tutorial where it is: nothing moves on behind them.
            if (skipOpen || BattlePaused) return;
            UpdateHandOvers();
            if (beatIndex < 0)
            {
                if (!BattlePlayable()) { playableAt = -1f; return; }
                if (playableAt < 0f) playableAt = Now;
                if (Now - playableAt >= art.firstLineDelay) Advance();
                return;
            }
            if (!CurrentBeat.IsLine) UpdateAction(CurrentBeat);
        }

        private bool BattlePlayable() => session.IsTutorial && session.State.phase == GameFlowPhase.Playing
            && session.Network != null && !session.Network.GameStartHeld
            && (session.StartOverlay == null || !session.StartOverlay.Visible)
            // The first line waits until the loading screen (#478) has gone.
            && !session.IsLoadingScreenUp;

        /// <summary>A tap on the bubble: the line moves on.</summary>
        private void OnTap()
        {
            NoteTap();
            if (skipOpen) return;
            if (!HasBeat || !CurrentBeat.IsLine || Now - shownAt < art.tapCooldown) return;
            Advance();
        }

        /// <summary>A tap anywhere else while a line is up: nothing moves on, and the ▽ jumps once to show where to tap.</summary>
        private void OnScreenTap()
        {
            NoteTap();
            if (skipOpen || !HasBeat || !CurrentBeat.IsLine) return;
            ScreenTapsOffBubble++;
            if (tapHint.gameObject.activeSelf) hintNudgeAt = Now;
        }

        private void Advance()
        {
            NoteLinePassed();
            IReadOnlyList<GameTutorialStep> steps = GameTutorialScript.Steps;
            beatIndex++;
            while (stepIndex < steps.Count && beatIndex >= steps[stepIndex].Beats.Count)
            {
                stepIndex++;
                beatIndex = 0;
            }
            if (stepIndex >= steps.Count) { BeginFinish(); return; }
            ShowBeat(steps[stepIndex].Beats[beatIndex]);
        }

        private void ShowBeat(GameTutorialBeat beat)
        {
            shownAt = Now;
            actionDoneAt = successAt = -1f;
            actionProgress = 0;
            bubbleRoot.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            successText.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            hand.color = Color.white;
            ClearRemark();
            PlaceBanner(art.bannerTopFraction);
            // Nothing crosses to a neighbor unless this beat passes it: an orb that reaches an edge bounces back.
            session.Network.TutorialAllowPasses();
            dimTarget = beat.Focus == GameTutorialFocus.None ? 0f : 1f;
            if (beat.IsLine)
            {
                board.SetAllowedGestures(LabBoardGestures.None);
                tapCatcher.raycastTarget = true;
                ShowBubble(beat.Speaker, beat.Text);
                if (beat.Cue == GameTutorialCue.StaminaShort) session.Network.TutorialPlayStaminaShort();
                // "고마워! 대신 내가 만든 거 줄게~": the yang, then the drum, slide back in from 금옥이's side.
                if (beat.Cue == GameTutorialCue.GiveBack) { giveBackStage = 1; giveBackAt = Now + art.giveBackDelay; }
                // The yokai drops a two-lock bomb on my board, with the gourd's warning.
                if (beat.Cue == GameTutorialCue.DropBomb)
                    session.Network.TutorialDropGourdBomb(GameTutorialScript.BombLanding, GameTutorialScript.BombLocks);
                // "앗 뜨거! 도로 받아!": 금옥이 passes the bomb back, and its last lock comes off on the way. It turns rainbow but
                // waits on my board, unopened, for "풀렸다!".
                if (beat.Cue == GameTutorialCue.ReturnBomb)
                {
                    board.SetBombOpeningHeld(true);
                    bombReturnAt = Now + art.bombReturnDelay;
                }
                // "풀렸다!": now it opens into its two attack orbs.
                if (beat.Cue == GameTutorialCue.OpenBomb) board.SetBombOpeningHeld(false);
                return;
            }
            BeginAction(beat);
        }

        /// <summary>
        /// A short word during an action (3단계 "다시 해 봐!"): the speaker's bubble pops up without taking the screen,
        /// and goes when the player takes hold of an orb again or after <see cref="GameTutorialArt.remarkSeconds"/>.
        /// </summary>
        private void ShowRemark(GameTutorialSpeaker speaker, string text) =>
            ShowRemark(speaker, text, art.remarkSeconds, false, null);

        /// <summary>
        /// The same, up for <paramref name="seconds"/>; one that <paramref name="holds"/> stays while an orb is held, and
        /// <paramref name="then"/> pops up from the same speaker once it ends (the tease, then "다시 해 봐!").
        /// </summary>
        private void ShowRemark(GameTutorialSpeaker speaker, string text, float seconds, bool holds, string then)
        {
            remarkSpeaker = speaker;
            remarkAt = Now;
            remarkSeconds = seconds;
            remarkHolds = holds;
            remarkThen = then;
            ShowBubble(speaker, text);
        }

        private void ClearRemark()
        {
            remarkAt = -1f;
            remarkThen = null;
        }

        private void ShowBubble(GameTutorialSpeaker speaker, string text)
        {
            bool fromRight = speaker == GameTutorialSpeaker.Geumok;
            bubbleText.text = text;
            LayoutBubble(fromRight, false);
            tapHint.gameObject.SetActive(false);
            hintNudgeAt = -1f;
            bubbleRoot.sizeDelta = art.bubbleSize;
            Image face = portraits[GameTutorialScript.SeatOf(speaker)];
            speakerPortrait.sprite = face != null ? face.sprite : null;
            bubbleRoot.gameObject.SetActive(true);
            bubbleRoot.localScale = Vector3.zero;
            popStartedAt = Now;
            PlaceBubble(speaker);
        }

        /// <summary>
        /// The bubble's art, words and tail for a speaker on the left or right. Normally it rises from the face with its tail
        /// down; <paramref name="below"/> hangs it from the face with the art turned upside down (tail up), its words still
        /// upright, so it keeps clear of a lit area above the face (the drum map).
        /// </summary>
        private void LayoutBubble(bool fromRight, bool below)
        {
            bubbleBelow = below;
            // The art's tail points down-left; the right neighbor's bubble is mirrored, its words are not.
            bubbleArt.localScale = new Vector3(fromRight ? -1f : 1f, below ? -1f : 1f, 1f);
            Vector4 inset = art.textInset;
            float bottom = below ? inset.w : inset.y, top = below ? inset.y : inset.w;
            RectTransform words = bubbleText.rectTransform;
            words.anchorMin = new Vector2(fromRight ? inset.z : inset.x, bottom);
            words.anchorMax = new Vector2(1f - (fromRight ? inset.x : inset.z), 1f - top);
            words.offsetMin = words.offsetMax = Vector2.zero;
            RectTransform hint = tapHint.rectTransform;
            hint.anchorMin = hint.anchorMax = new Vector2(words.anchorMax.x, words.anchorMin.y);
            Vector2 tail = fromRight ? new Vector2(1f - art.tailTip.x, art.tailTip.y) : art.tailTip;
            bubbleRoot.pivot = below ? new Vector2(tail.x, 1f - tail.y) : tail;
        }

        private void BeginAction(GameTutorialBeat beat)
        {
            LabNetwork network = session.Network;
            bool playerActs = beat.Action == GameTutorialAction.TapDrum || beat.Action == GameTutorialAction.CombineOrbs
                || beat.Action == GameTutorialAction.ThrowOrb || beat.Action == GameTutorialAction.PassDrum
                || beat.Action == GameTutorialAction.PassBomb || beat.Action == GameTutorialAction.PassDrumLeft;
            // The player's own action goes through to the board; waiting on the gauge still keeps the screen.
            tapCatcher.raycastTarget = !playerActs;
            if (!string.IsNullOrEmpty(beat.Text))
            {
                bannerText.text = BannerText(beat, 0);
                banner.gameObject.SetActive(true);
            }
            switch (beat.Action)
            {
                case GameTutorialAction.TapDrum:
                    // Fixed kinds and fixed resting places (yin left, yang right, yin just above the yang). An orb
                    // leaves from just outside the drum, so it slides only what is left after that clearance.
                    var results = new LabOrbKind[GameTutorialScript.DrumResults.Count];
                    var launches = new Vector2[results.Length];
                    LabConfig tuning = network.config;
                    float slowing = tuning != null ? tuning.OrbFloorDeceleration : 0f;
                    float clearance = tuning != null ? GameTutorialScript.DrumClearance(tuning.DrumRadius, tuning.OrbRadius,
                        board.AreaShape != null ? board.AreaShape.CenterBounds.width : 0f) : 0f;
                    for (int i = 0; i < results.Length; i++)
                    {
                        results[i] = GameTutorialScript.DrumResults[i];
                        (float angle, float distance) = GameTutorialScript.DrumLaunches[i];
                        float slide = GameTutorialScript.SlideAfterDrum(distance, clearance, art.drumLaunchExtra, angle,
                            art.drumLaunchEdgeMargin);
                        launches[i] = GameTutorialScript.LaunchVelocity(angle, slide, slowing);
                    }
                    network.TutorialScriptGenerations(results, slowing > 0f ? launches : null);
                    actionBaseline = LocalPlayer()?.generatedTotal ?? 0;
                    board.SetAllowedGestures(LabBoardGestures.DrumTap);
                    hand.gameObject.SetActive(art.hand != null);
                    break;
                case GameTutorialAction.RefillStamina:
                    board.SetAllowedGestures(LabBoardGestures.None);
                    network.TutorialHoldStamina(false);
                    break;
                case GameTutorialAction.CombineOrbs:
                    network.TutorialScriptCombinations(GameTutorialScript.CombineResult);
                    actionBaseline = LocalPlayer()?.combinedTotal ?? 0;
                    // Only orbs move: the drum stays put and nothing is thrown yet.
                    board.SetAllowedGestures(LabBoardGestures.OrbMove);
                    hand.gameObject.SetActive(art.hand != null);
                    arrow.gameObject.SetActive(true);
                    break;
                case GameTutorialAction.PassDrum:
                    // Only the drum moves now; tapping it makes nothing and orbs stay put. Only the drum may cross,
                    // so an orb it bumps off the edge bounces back instead of reaching a neighbor.
                    board.SetAllowedGestures(LabBoardGestures.DrumMove);
                    network.TutorialAllowPasses(LabOrbKind.Drum);
                    hand.gameObject.SetActive(art.hand != null);
                    arrow.gameObject.SetActive(true);
                    break;
                case GameTutorialAction.PassBomb:
                    // Orbs can be pushed, the drum cannot; only the bomb crosses, and only over the right edge to 금옥이,
                    // so its lock count goes 2 → 1 → 0 as told.
                    board.SetAllowedGestures(LabBoardGestures.OrbMove);
                    network.TutorialAllowPasses(1, LabOrbKind.Bomb);
                    hand.gameObject.SetActive(art.hand != null);
                    arrow.gameObject.SetActive(true);
                    break;
                case GameTutorialAction.WaitBombBack:
                    board.SetAllowedGestures(LabBoardGestures.None);
                    break;
                case GameTutorialAction.WatchBlock:
                    // The yokai shuts my right edge: it flashes red three times before anyone speaks.
                    board.SetAllowedGestures(LabBoardGestures.None);
                    network.TutorialBlockSwipe(GameTutorialScript.BlockedSide);
                    break;
                case GameTutorialAction.PassDrumLeft:
                    // Only the drum moves. The right edge is shut, so pushed that way it bounces back off the red edge.
                    board.SetAllowedGestures(LabBoardGestures.DrumMove);
                    network.TutorialAllowPasses(-1, LabOrbKind.Drum);
                    hand.gameObject.SetActive(art.hand != null);
                    arrow.gameObject.SetActive(true);
                    break;
                case GameTutorialAction.ThrowOrb:
                    board.SetAllowedGestures(LabBoardGestures.Throw);
                    throwHit = false;
                    ListenToThrows(true);
                    hand.gameObject.SetActive(art.hand != null);
                    arrow.gameObject.SetActive(true);
                    break;
            }
        }

        private void ListenToThrows(bool listen)
        {
            LabNetwork network = session != null ? session.Network : null;
            if (network == null || listeningToThrows == listen) return;
            listeningToThrows = listen;
            if (listen)
            {
                network.LocalThrowHit += OnThrowHit;
                network.LocalThrowMissed += OnThrowMissed;
            }
            else
            {
                network.LocalThrowHit -= OnThrowHit;
                network.LocalThrowMissed -= OnThrowMissed;
            }
        }

        private void OnThrowHit(int _) => throwHit = true;

        /// <summary>A miss is no failure: 금옥이 says try again and the red orb falls back onto the board from above.</summary>
        private void OnThrowMissed()
        {
            if (!HasBeat || CurrentBeat.Action != GameTutorialAction.ThrowOrb || actionDoneAt >= 0f) return;
            ThrowMisses++;
            session.Network.TutorialDropOrb(LabOrbKind.Combined, GameTutorialScript.CombineResult, GameTutorialScript.RetryLanding);
            string first = GameTutorialScript.MissLine(ThrowMisses);
            // The fifth miss, once: "...... 너 일부러 그러는 거지?" stays its full time, then "다시 해 봐!" as ever.
            if (first != GameTutorialScript.RetryLine)
            {
                TeasesShown++;
                ShowRemark(GameTutorialScript.RetrySpeaker, first, art.teaseSeconds, true, GameTutorialScript.RetryLine);
            }
            else ShowRemark(GameTutorialScript.RetrySpeaker, first);
        }

        private void OnDestroy()
        {
            ListenToThrows(false);
            if (hud != null) hud.developerSettingsHidden = false;
        }

        // ---------- 4단계: passing with the story neighbors ----------

        /// <summary>Who holds the drum right now (0 when nobody is known).</summary>
        private bool TryDrumOwner(out LabOrbState drum)
        {
            drum = null;
            LabSnapshot snapshot = session.Network != null ? session.Network.Snapshot : null;
            if (snapshot?.drums == null || snapshot.drums.Length == 0) return false;
            drum = snapshot.drums[0];
            return drum != null;
        }

        /// <summary>
        /// The story neighbors' own hands: 금옥이 gives her yang and then the drum back after "고마워!", and 묵이 hands
        /// back a drum that came to him by mistake. Runs whatever the beat, so a quick tap never strands the drum.
        /// </summary>
        private void UpdateHandOvers()
        {
            LabNetwork network = session.Network;
            LabConfig config = network.config;
            float slowing = config != null ? config.OrbFloorDeceleration : 2f;
            float slide = GameTutorialScript.LaunchVelocity(90f, GameTutorialScript.GiveBackSlide, slowing).magnitude;
            if (giveBackStage > 0 && Now >= giveBackAt)
            {
                if (giveBackStage == 1)
                {
                    network.TutorialGiveFrom(LabNetwork.TutorialRightSeatId, LabOrbKind.Yang, LabOrbElement.None,
                        GameTutorialScript.GiveBackYangHeight, slide);
                    giveBackStage = 2;
                    giveBackAt = Now + art.giveBackGap;
                }
                else
                {
                    if (TryDrumOwner(out LabOrbState drum) && drum.owner == LabNetwork.TutorialRightSeatId)
                        network.TutorialHandOver(LabNetwork.TutorialRightSeatId, drum.id, GameTutorialScript.GiveBackDrumHeight, slide);
                    giveBackStage = 0;
                }
            }
            if (wrongSideReturnAt >= 0f && Now >= wrongSideReturnAt)
            {
                wrongSideReturnAt = -1f;
                if (TryDrumOwner(out LabOrbState drum) && drum.owner == LabNetwork.TutorialLeftSeatId)
                    network.TutorialHandOver(LabNetwork.TutorialLeftSeatId, drum.id, GameTutorialScript.GiveBackDrumHeight, slide);
            }
            if (bombReturnAt >= 0f && Now >= bombReturnAt)
            {
                bombReturnAt = -1f;
                // Through the real pass, so the last lock comes off as it crosses and it opens where it stops.
                if (TryBomb(out LabOrbState bomb) && bomb.owner == LabNetwork.TutorialRightSeatId)
                    network.TutorialHandOver(LabNetwork.TutorialRightSeatId, bomb.id, GameTutorialScript.GiveBackDrumHeight, slide);
            }
        }

        /// <summary>The one bomb on the practice board, whoever holds it.</summary>
        private bool TryBomb(out LabOrbState bomb)
        {
            bomb = null;
            LabSnapshot snapshot = session.Network != null ? session.Network.Snapshot : null;
            foreach (LabOrbState orb in snapshot?.orbs ?? System.Array.Empty<LabOrbState>())
                if (orb != null && orb.kind == LabOrbKind.Bomb) { bomb = orb; return true; }
            return false;
        }

        /// <summary>5단계: the hand drags the bomb to the right edge, faster than the drum (it needs a harder push).</summary>
        private void PlaceBombGuide()
        {
            Vector2 from = default;
            float radius = 0f;
            bool shown = !board.IsHolding && TryBomb(out LabOrbState bomb) && bomb.owner == session.Network.LocalPlayerId
                && !board.IsDropping(bomb.id) && TryOrbCircle(bomb.id, out from, out radius);
            arrow.gameObject.SetActive(shown);
            hand.gameObject.SetActive(shown && art.hand != null);
            if (!shown) return;
            Vector2 to = new Vector2(overlay.rect.xMax - art.screenMargin, from.y);
            float start = radius + art.arrowGap;
            arrow.Set(new Vector2(from.x + start, from.y), new Vector2(Mathf.Max(from.x + start, to.x), from.y),
                art.arrowWidth, art.arrowHeadLength, art.arrowHeadWidth);
            HandDrag((Now - shownAt) / art.bombHandDragSeconds, out float travel, out float press, out float alpha);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = Vector2.Lerp(from, to, travel) + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
            hand.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>4·6단계: the hand drags the drum to one side's edge (+1 right to 금옥이, -1 left to 묵이).</summary>
        private void PlacePassGuide(int side)
        {
            bool shown = !board.IsHolding && TryDrumOwner(out LabOrbState drum) && drum.owner == session.Network.LocalPlayerId
                && TryDrumCircle(out Vector2 from, out float radius);
            arrow.gameObject.SetActive(shown);
            hand.gameObject.SetActive(shown && art.hand != null);
            if (!shown) return;
            TryDrumCircle(out from, out radius);
            float way = side < 0 ? -1f : 1f;
            Vector2 to = new Vector2(way > 0f ? overlay.rect.xMax - art.screenMargin : overlay.rect.xMin + art.screenMargin, from.y);
            float start = from.x + way * (radius + art.arrowGap);
            float end = way > 0f ? Mathf.Max(start, to.x) : Mathf.Min(start, to.x);
            arrow.Set(new Vector2(start, from.y), new Vector2(end, from.y),
                art.arrowWidth, art.arrowHeadLength, art.arrowHeadWidth);
            HandDrag((Now - shownAt) / art.handDragSeconds, out float travel, out float press, out float alpha);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = Vector2.Lerp(from, to, travel) + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
            hand.color = new Color(1f, 1f, 1f, alpha);
        }

        private void UpdateAction(GameTutorialBeat beat)
        {
            if (actionDoneAt >= 0f)
            {
                float wait = successAt >= 0f ? art.afterActionSeconds : art.afterRefillSeconds;
                // The drum is seen arriving in 금옥이's mirror before she thanks.
                if (beat.Action == GameTutorialAction.PassDrum) wait = Mathf.Max(wait, mirrorDuration);
                if (Now - actionDoneAt >= wait) Advance();
                return;
            }
            switch (beat.Action)
            {
                case GameTutorialAction.TapDrum:
                    int made = (LocalPlayer()?.generatedTotal ?? actionBaseline) - actionBaseline;
                    if (made != actionProgress)
                    {
                        actionProgress = made;
                        bannerText.text = BannerText(beat, made);
                    }
                    if (made >= beat.Count) CompleteAction(true);
                    break;
                case GameTutorialAction.RefillStamina:
                    LabPlayerState me = LocalPlayer();
                    LabConfig config = session.Network.config;
                    if (me != null && config != null && me.stamina >= config.StaminaMax - .001f
                        && Now - shownAt >= .3f) CompleteAction(false);
                    break;
                case GameTutorialAction.CombineOrbs:
                    int combined = (LocalPlayer()?.combinedTotal ?? actionBaseline) - actionBaseline;
                    if (combined != actionProgress)
                    {
                        actionProgress = combined;
                        bannerText.text = BannerText(beat, combined);
                    }
                    if (combined >= beat.Count) CompleteAction(true);
                    break;
                case GameTutorialAction.ThrowOrb:
                    if (throwHit)
                    {
                        actionProgress = 1;
                        CompleteAction(true);
                    }
                    break;
                case GameTutorialAction.PassBomb:
                    if (TryBomb(out LabOrbState passing) && passing.owner == LabNetwork.TutorialRightSeatId)
                    {
                        actionProgress = 1;
                        CompleteAction(true);
                    }
                    break;
                case GameTutorialAction.WaitBombBack:
                    // Done once the unlocked bomb is back on my board and has come to rest (held unopened), or if no bomb
                    // is left at all.
                    if (Now - shownAt >= .3f && (!TryBomb(out LabOrbState back) || back.owner == session.Network.LocalPlayerId
                        && back.lockSwipes == 0 && board.IsBombAtRest(back.id))) CompleteAction(false);
                    break;
                case GameTutorialAction.WatchBlock:
                    if (Now - shownAt >= art.blockWarningSeconds) CompleteAction(false);
                    break;
                case GameTutorialAction.PassDrumLeft:
                    if (TryDrumOwner(out LabOrbState given) && given.owner == LabNetwork.TutorialLeftSeatId)
                    {
                        actionProgress = 1;
                        CompleteAction(true);
                        // The block held until now, however long it took.
                        session.Network.TutorialBlockSwipe(0);
                    }
                    break;
                case GameTutorialAction.PassDrum:
                    if (!TryDrumOwner(out LabOrbState drum)) break;
                    if (drum.owner == LabNetwork.TutorialRightSeatId)
                    {
                        actionProgress = 1;
                        CompleteAction(true);
                        StartMirror(drum);
                    }
                    else if (drum.owner == LabNetwork.TutorialLeftSeatId && wrongSideReturnAt < 0f)
                    {
                        // Not this way: 묵이 sends it straight back, and the player tries again.
                        WrongSidePasses++;
                        ShowRemark(GameTutorialScript.WrongSideSpeaker, GameTutorialScript.WrongSideLine);
                        wrongSideReturnAt = Now + art.wrongSideReturnDelay;
                    }
                    break;
            }
        }

        private void CompleteAction(bool success)
        {
            actionDoneAt = Now;
            board.SetAllowedGestures(LabBoardGestures.None);
            session.Network.TutorialAllowPasses();
            tapCatcher.raycastTarget = true;
            hand.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            ListenToThrows(false);
            // A hit during the tease ends it there; "다시 해 봐!" does not follow.
            ClearRemark();
            bubbleRoot.gameObject.SetActive(false);
            // The empty gauge breathes back only a sliver, still short of a tap, so the short cue's red shows on it.
            if (CurrentBeat.Action == GameTutorialAction.TapDrum && session.Network.config != null)
                session.Network.TutorialCapStamina(ShortStaminaFor(session.Network.config.GenerateCost, art.shortStaminaShareOfTap));
            if (!success) return;
            successAt = actionDoneAt;
            // The task is done: its banner goes and "성공!" pops up where it was.
            banner.gameObject.SetActive(false);
            successText.gameObject.SetActive(true);
            successText.rectTransform.localScale = Vector3.zero;
        }

        private LabPlayerState LocalPlayer()
        {
            LabNetwork network = session.Network;
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (snapshot?.players == null) return null;
            foreach (LabPlayerState player in snapshot.players)
                if (player != null && player.id == network.LocalPlayerId) return player;
            return null;
        }

        private void BeginFinish()
        {
            finishing = true;
            board.SetBombOpeningHeld(false);
            ListenToThrows(false);
            board.SetAllowedGestures(LabBoardGestures.None);
            session.Network.TutorialAllowPasses();
            tapCatcher.raycastTarget = true;
            bubbleRoot.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            successText.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            arrow.gameObject.SetActive(false);
            dimTarget = 0f;
            StartCoroutine(FadeOutAndLeave());
        }

        private IEnumerator FadeOutAndLeave()
        {
            float alpha = 0f;
            while (alpha < 1f)
            {
                alpha = Mathf.Min(1f, alpha + Time.unscaledDeltaTime / art.exitFadeSeconds);
                session.SetTutorialFade(alpha);
                yield return null;
            }
            session.FinishTutorial();
        }

        // ---------- Layout every frame ----------

        private void LateUpdate()
        {
            if (unavailable || hud == null || overlay == null) return;
            ApplyHudDressing();
            FaceYokaiToMe();
            PlaceNames();
            UpdateKeepOut();
            if (UpdatePause()) return;
            bool skipDialogUp = UpdateSkip();
            UpdateLogButton();
            if (skipDialogUp) return;
            bool line = HasBeat && CurrentBeat.IsLine;
            if (remarkAt >= 0f && (Now - remarkAt > remarkSeconds || !remarkHolds && board.IsHolding))
            {
                string then = remarkThen;
                ClearRemark();
                bubbleRoot.gameObject.SetActive(false);
                if (then != null) ShowRemark(remarkSpeaker, then);
            }
            if (line || remarkAt >= 0f)
            {
                PlaceBubble(line ? CurrentBeat.Speaker : remarkSpeaker);
                bubbleRoot.localScale = Vector3.one * PopScale((Now - popStartedAt) / art.popSeconds,
                    art.popOvershoot);
            }
            PlaceTapHint(line);
            if (successAt >= 0f)
                successText.rectTransform.localScale = Vector3.one * PopScale((Now - successAt) / art.popSeconds,
                    art.popOvershoot);
            bool waiting = HasBeat && actionDoneAt < 0f;
            if (waiting && CurrentBeat.Action == GameTutorialAction.CombineOrbs) PlaceDragGuide();
            else if (waiting && CurrentBeat.Action == GameTutorialAction.ThrowOrb) PlaceThrowGuide();
            else if (waiting && CurrentBeat.Action == GameTutorialAction.PassDrum) PlacePassGuide(1);
            else if (waiting && CurrentBeat.Action == GameTutorialAction.PassDrumLeft) PlacePassGuide(-1);
            else if (waiting && CurrentBeat.Action == GameTutorialAction.PassBomb) PlaceBombGuide();
            else if (hand.gameObject.activeSelf) PlaceHand();
            dimGroup.alpha = Mathf.MoveTowards(dimGroup.alpha, dimTarget, Time.unscaledDeltaTime / art.dimFadeSeconds);
            if (HasBeat && CurrentBeat.Focus != GameTutorialFocus.None) PlaceSpotlight(CurrentBeat.Focus);
            speakerPortrait.enabled = line && dimGroup.alpha > .01f && speakerPortrait.sprite != null;
            PlaceMirror();
        }

        /// <summary>When a tap first moves the line on: the bubble has popped to size and the tap cooldown is over.</summary>
        private float TapReadyAt => Mathf.Max(popStartedAt + art.popSeconds, shownAt + art.tapCooldown);

        /// <summary>The ▽ shows on a line (never on a remark) from the moment a tap moves it on, bobbing down and up.</summary>
        private void PlaceTapHint(bool line)
        {
            // Only a line's bubble moves it on; a remark's bubble lets touches through to the board as before.
            if (lineTapArea.raycastTarget != line) lineTapArea.raycastTarget = line;
            bool shown = line && art.tapHint != null && Now >= TapReadyAt;
            if (tapHint.gameObject.activeSelf != shown) tapHint.gameObject.SetActive(shown);
            if (!shown) return;
            tapHint.rectTransform.anchoredPosition = art.tapHintOffset
                + new Vector2(0f, TapHintDrop(Now - TapReadyAt, art.tapHintSeconds, art.tapHintDepth));
            float nudge = hintNudgeAt >= 0f
                ? TapHintNudge(Now - hintNudgeAt, art.tapHintNudgeSeconds, art.tapHintNudgeScale) : 1f;
            tapHint.rectTransform.localScale = Vector3.one * nudge;
        }

        /// <summary>
        /// The ▽'s size <paramref name="t"/> seconds after a tap off the bubble: it swells to <paramref name="peak"/> and back
        /// once over <paramref name="seconds"/>, then stays at 1.
        /// </summary>
        public static float TapHintNudge(float t, float seconds, float peak)
        {
            if (t < 0f || seconds <= 0f || t >= seconds) return 1f;
            return 1f + (peak - 1f) * Mathf.Sin(Mathf.PI * t / seconds);
        }

        /// <summary>
        /// How far the ▽ sits below its rest <paramref name="t"/> seconds after it appeared: it starts at rest, eases down
        /// <paramref name="depth"/> by half a <paramref name="period"/> and eases back up, over and over.
        /// </summary>
        public static float TapHintDrop(float t, float period, float depth)
        {
            if (period <= 0f || depth <= 0f || t <= 0f) return 0f;
            float s = Mathf.Sin(Mathf.PI * (t / period % 1f));
            return -depth * s * s;
        }

        private void PlaceBubble(GameTutorialSpeaker speaker)
        {
            Rect bounds = overlay.rect;
            Image face = portraits[GameTutorialScript.SeatOf(speaker)];
            bool fromRight = speaker == GameTutorialSpeaker.Geumok;
            Vector2 tip;
            if (face != null && TryOverlayRect(face.rectTransform, out Rect faceRect))
            {
                // The tail touches the face's upper inner shoulder: up-right of a left face, up-left of the right one.
                float shoulder = .15f * Mathf.Min(faceRect.width, faceRect.height);
                tip = fromRight
                    ? new Vector2(faceRect.xMin + shoulder - art.tailGap.x, faceRect.yMax - shoulder + art.tailGap.y)
                    : new Vector2(faceRect.xMax - shoulder + art.tailGap.x, faceRect.yMax - shoulder + art.tailGap.y);
                RectTransform copy = speakerPortrait.rectTransform;
                copy.anchoredPosition = faceRect.center;
                copy.sizeDelta = faceRect.size;
                // A line that lights the drum map must not cover it: if rising from the face would, hang it below instead,
                // beside the face (its tail up at the face's upper side), clear of the face itself.
                bool below = HasBeat && CurrentBeat.IsLine && CurrentBeat.Speaker == speaker && remarkAt < 0f
                    && KeepsClear(CurrentBeat.Focus, out Rect lit)
                    && RiseRect(tip, fromRight).Overlaps(lit);
                if (below != bubbleBelow) LayoutBubble(fromRight, below);
                if (below)
                {
                    float reach = art.tailTip.x * art.bubbleSize.x;
                    tip = fromRight
                        ? new Vector2(faceRect.xMin - art.tailGap.x - reach, faceRect.yMax - shoulder)
                        : new Vector2(faceRect.xMax + art.tailGap.x + reach, faceRect.yMax - shoulder);
                }
            }
            else tip = new Vector2(bounds.center.x, bounds.yMin + bounds.height * .3f);
            bubbleRoot.anchoredPosition = KeepOnScreen(tip, bubbleRoot.pivot, art.bubbleSize);
        }

        /// <summary>Where the bubble would lie rising from <paramref name="tip"/> as usual (tail down).</summary>
        private Rect RiseRect(Vector2 tip, bool fromRight)
        {
            Vector2 pivot = fromRight ? new Vector2(1f - art.tailTip.x, art.tailTip.y) : art.tailTip;
            tip = KeepOnScreen(tip, pivot, art.bubbleSize);
            return new Rect(tip.x - pivot.x * art.bubbleSize.x, tip.y - pivot.y * art.bubbleSize.y, art.bubbleSize.x, art.bubbleSize.y);
        }

        /// <summary>The lit area a line's bubble must keep clear of: only the drum map for now.</summary>
        private bool KeepsClear(GameTutorialFocus focus, out Rect lit)
        {
            lit = default;
            return focus == GameTutorialFocus.Minimap && TryMinimapRect(out lit);
        }

        /// <summary>The drum map at the top right, with the light's padding.</summary>
        private bool TryMinimapRect(out Rect rect)
        {
            rect = default;
            LabDrumMinimap minimap = FindInBattle<LabDrumMinimap>();
            if (minimap == null || !minimap.IsShowing || !TryOverlayRect(minimap.Panel, out Rect panel)) return false;
            float pad = art.minimapSpotlightPadding;
            rect = new Rect(panel.xMin - pad, panel.yMin - pad, panel.width + pad * 2f, panel.height + pad * 2f);
            return true;
        }

        /// <summary>The bubble's tail point, moved only as far as needed to keep the whole bubble on screen.</summary>
        private Vector2 KeepOnScreen(Vector2 tip, Vector2 pivot, Vector2 size)
        {
            Rect bounds = overlay.rect;
            float margin = art.screenMargin;
            float left = tip.x - pivot.x * size.x, right = left + size.x;
            float top = tip.y + (1f - pivot.y) * size.y;
            if (left < bounds.xMin + margin) tip.x += bounds.xMin + margin - left;
            else if (right > bounds.xMax - margin) tip.x -= right - (bounds.xMax - margin);
            if (top > bounds.yMax - margin) tip.y -= top - (bounds.yMax - margin);
            return tip;
        }

        /// <summary>Where a speaker's bubble tail touches their face (the upper inner shoulder), or a fallback.</summary>
        private Vector2 TailTip(GameTutorialSpeaker speaker)
        {
            Image face = portraits[GameTutorialScript.SeatOf(speaker)];
            bool fromRight = speaker == GameTutorialSpeaker.Geumok;
            if (face == null || !TryOverlayRect(face.rectTransform, out Rect faceRect))
                return new Vector2(overlay.rect.center.x, overlay.rect.yMin + overlay.rect.height * .3f);
            float shoulder = .15f * Mathf.Min(faceRect.width, faceRect.height);
            return fromRight
                ? new Vector2(faceRect.xMin + shoulder - art.tailGap.x, faceRect.yMax - shoulder + art.tailGap.y)
                : new Vector2(faceRect.xMax - shoulder + art.tailGap.x, faceRect.yMax - shoulder + art.tailGap.y);
        }

        private void PlaceHand()
        {
            if (!TryDrumCircle(out Vector2 center, out _)) { hand.enabled = false; return; }
            hand.enabled = true;
            float press = HandPress((Now - shownAt) / art.handTapSeconds);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = center + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
        }

        /// <summary>
        /// 2단계: the hand presses on the yin, drags it along the arrow onto the yang and lets go, again and again.
        /// Both step aside while the player's own finger holds an orb.
        /// </summary>
        private void PlaceDragGuide()
        {
            Vector2 from = default, to = default;
            float fromRadius = 0f, toRadius = 0f;
            bool shown = !board.IsHolding && TryPair(out string yinId, out string yangId)
                && TryOrbCircle(yinId, out from, out fromRadius) && TryOrbCircle(yangId, out to, out toRadius);
            arrow.gameObject.SetActive(shown);
            hand.gameObject.SetActive(shown && art.hand != null);
            if (!shown) return;
            Vector2 line = to - from;
            float length = line.magnitude;
            Vector2 along = length > 1e-3f ? line / length : Vector2.right;
            float start = fromRadius + art.arrowGap, end = Mathf.Max(start, length - toRadius - art.arrowGap);
            arrow.Set(from + along * start, from + along * end, art.arrowWidth, art.arrowHeadLength, art.arrowHeadWidth);
            HandDrag((Now - shownAt) / art.handDragSeconds, out float travel, out float press, out float alpha);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = Vector2.Lerp(from, to, travel) + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
            hand.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// 3단계: the hand presses on my red orb and flicks it up toward the gourd along the arrow, again and again.
        /// Both step aside while a finger holds an orb, and while no elemental orb is on the board (in flight or falling back).
        /// </summary>
        private void PlaceThrowGuide()
        {
            LabOrbState fire = null;
            foreach (LabOrbState orb in LocalOrbs()) if (orb.kind == LabOrbKind.Combined) { fire = orb; break; }
            Vector2 from = default;
            float radius = 0f;
            bool shown = fire != null && !board.IsHolding && !board.IsDropping(fire.id)
                && TryOrbCircle(fire.id, out from, out radius);
            arrow.gameObject.SetActive(shown);
            hand.gameObject.SetActive(shown && art.hand != null);
            if (!shown) return;
            Vector2 aim = TryGourdRect(out Rect gourd) ? gourd.center - from : Vector2.up;
            Vector2 along = aim.sqrMagnitude > 1e-4f ? aim.normalized : Vector2.up;
            float start = radius + art.arrowGap;
            arrow.Set(from + along * start, from + along * (start + art.throwGuideLength), art.arrowWidth,
                art.arrowHeadLength, art.arrowHeadWidth);
            HandSwipe((Now - shownAt) / art.handSwipeSeconds, out float travel, out float press, out float alpha);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = from + along * (art.throwGuideLength * travel) + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
            hand.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// The throw guide over one cycle (t = 0..1): fade in pressing on the orb, flick quickly up (speeding up), let go
        /// fading at the top, then rest unseen before the next flick.
        /// </summary>
        public static void HandSwipe(float t, out float travel, out float press, out float alpha)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < .12f) { travel = 0f; press = alpha = t / .12f; return; }
            if (t < .4f)
            {
                float u = (t - .12f) / .28f;
                travel = u * u;
                press = alpha = 1f;
                return;
            }
            travel = 1f;
            press = 0f;
            alpha = t < .55f ? 1f - (t - .4f) / .15f : 0f;
        }

        /// <summary>
        /// The drag guide over one cycle (t = 0..1): fade in pressing on the start, travel to the end while pressed,
        /// let go, then fade out. travel and press are 0..1.
        /// </summary>
        public static void HandDrag(float t, out float travel, out float press, out float alpha)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < .12f) { travel = 0f; press = alpha = t / .12f; return; }
            if (t < .72f)
            {
                float u = (t - .12f) / .6f;
                travel = u * u * (3f - 2f * u);
                press = alpha = 1f;
                return;
            }
            travel = 1f;
            if (t < .84f) { press = 1f - (t - .72f) / .12f; alpha = 1f; return; }
            press = 0f;
            alpha = 1f - (t - .84f) / .16f;
        }

        private void PlaceSpotlight(GameTutorialFocus focus)
        {
            Rect bounds = overlay.rect;
            Rect target;
            float corner;
            // A round or level light needs no turn; keep the current one so a circle never spins.
            float degrees = focus == GameTutorialFocus.Stamina ? 0f : spotAngle;
            if (focus == GameTutorialFocus.Stamina && TryStaminaRect(out Rect bar))
            {
                float pad = art.barSpotlightPadding;
                target = new Rect(bar.xMin - pad, bar.yMin - pad, bar.width + pad * 2f, bar.height + pad * 2f);
                corner = Mathf.Min(target.width, target.height) * .5f;
            }
            else if (focus == GameTutorialFocus.Minimap && TryMinimapRect(out Rect map))
            {
                // The drum map: one rounded box around it.
                target = map;
                corner = Mathf.Min(art.minimapSpotlightCorner, Mathf.Min(map.width, map.height) * .5f);
                degrees = 0f;
            }
            else if (focus == GameTutorialFocus.Progress && TryProgressRect(out Rect progress))
            {
                // The paint bar and its % together, as one long rounded light.
                float pad = art.progressSpotlightPadding;
                target = new Rect(progress.xMin - pad, progress.yMin - pad, progress.width + pad * 2f, progress.height + pad * 2f);
                corner = Mathf.Min(target.width, target.height) * .5f;
                degrees = 0f;
            }
            else if (focus == GameTutorialFocus.Pair && TryPairCircle(out Vector2 pairCenter, out float pairRadius))
            {
                // The two orbs to combine sit side by side: one round light around both.
                float radius = pairRadius + art.orbSpotlightPadding;
                target = new Rect(pairCenter.x - radius, pairCenter.y - radius, radius * 2f, radius * 2f);
                corner = radius;
            }
            else if (focus == GameTutorialFocus.Yins && TryYinCapsule(out Vector2 first, out Vector2 second, out float yinRadius))
            {
                // Only the two black orbs, not the white: a capsule tilted along the line between them.
                CapsuleBetween(first, second, yinRadius + art.orbSpotlightPadding, out target, out degrees);
                corner = target.height * .5f;
            }
            else
            {
                Vector2 center;
                float radius;
                if (focus == GameTutorialFocus.Drum && TryDrumCircle(out center, out radius)) { }
                else if (focus == GameTutorialFocus.Bomb && TryBomb(out LabOrbState lit) && TryOrbCircle(lit.id, out center, out radius)) { }
                else if (TryGourdRect(out Rect gourd))
                {
                    center = gourd.center;
                    radius = .5f * Mathf.Max(gourd.width, gourd.height);
                }
                else
                {
                    center = new Vector2(bounds.center.x, bounds.yMax - bounds.height * .3f);
                    radius = 60f;
                }
                radius *= art.spotlightPadding;
                target = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
                corner = radius;
            }
            float soft = corner * art.spotlightSoftness;
            // Glide to a new target while the screen stays dark; jump there when it was not dark yet.
            float follow = !spotlightPlaced || dimGroup.alpha < .05f ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime * art.spotlightFollow);
            spotRect = new Rect(Mathf.Lerp(spotRect.x, target.x, follow), Mathf.Lerp(spotRect.y, target.y, follow),
                Mathf.Lerp(spotRect.width, target.width, follow), Mathf.Lerp(spotRect.height, target.height, follow));
            spotCorner = Mathf.Lerp(spotCorner, corner, follow);
            spotSoft = Mathf.Lerp(spotSoft, soft, follow);
            spotAngle = Mathf.LerpAngle(spotAngle, degrees, follow);
            spotlightPlaced = true;
            spotlight.SetHole(spotRect, spotCorner, spotSoft, spotAngle);
        }

        /// <summary>
        /// A capsule of <paramref name="radius"/> from <paramref name="a"/> to <paramref name="b"/>: its level rectangle
        /// around the middle and the turn (degrees, -90..90) that lays it along the line.
        /// </summary>
        public static void CapsuleBetween(Vector2 a, Vector2 b, float radius, out Rect level, out float degrees)
        {
            float length = Vector2.Distance(a, b);
            Vector2 middle = (a + b) * .5f;
            level = new Rect(middle.x - length * .5f - radius, middle.y - radius, length + radius * 2f, radius * 2f);
            degrees = length > 1e-4f ? Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg : 0f;
            // A capsule looks the same turned half a circle; keep the smaller turn so it never flips.
            if (degrees > 90f) degrees -= 180f;
            else if (degrees <= -90f) degrees += 180f;
        }

        /// <summary>My two black orbs (oldest first) and their radius, in overlay units; one alone gives a circle.</summary>
        private bool TryYinCapsule(out Vector2 first, out Vector2 second, out float radius)
        {
            first = second = default;
            radius = 0f;
            int found = 0;
            var yins = new List<LabOrbState>();
            foreach (LabOrbState orb in LocalOrbs()) if (orb.kind == LabOrbKind.Yin) yins.Add(orb);
            yins.Sort((x, y) => x.createdAt.CompareTo(y.createdAt));
            foreach (LabOrbState yin in yins)
            {
                if (found == 2 || !TryOrbCircle(yin.id, out Vector2 center, out float size)) continue;
                if (found == 0) first = second = center; else second = center;
                radius = Mathf.Max(radius, size);
                found++;
            }
            return found > 0;
        }

        /// <summary>The smallest circle around both orbs of the pair (centered between them), in overlay units.</summary>
        private bool TryPairCircle(out Vector2 center, out float radius)
        {
            center = default;
            radius = 0f;
            if (!TryPair(out string yinId, out string yangId) || !TryOrbCircle(yinId, out Vector2 yin, out float yinRadius)
                || !TryOrbCircle(yangId, out Vector2 yang, out float yangRadius)) return false;
            PairCircle(yin, yinRadius, yang, yangRadius, out center, out radius);
            return true;
        }

        /// <summary>The smallest circle holding two circles: along the line through their centers.</summary>
        public static void PairCircle(Vector2 a, float aRadius, Vector2 b, float bRadius, out Vector2 center, out float radius)
        {
            float gap = Vector2.Distance(a, b);
            if (gap + bRadius <= aRadius) { center = a; radius = aRadius; return; }
            if (gap + aRadius <= bRadius) { center = b; radius = bRadius; return; }
            radius = (gap + aRadius + bRadius) * .5f;
            Vector2 along = (b - a) / gap;
            center = a + along * (radius - aRadius);
        }

        /// <summary>
        /// The yin and yang to combine: the first yang and the yin nearest to it on screen. Kept while both are on the
        /// board, so the guide never jumps between two equally near yins.
        /// </summary>
        private bool TryPair(out string yinId, out string yangId)
        {
            yinId = yangId = null;
            LabOrbState yin = null, yang = null;
            foreach (LabOrbState orb in LocalOrbs())
            {
                if (orb.id == pairYinId && orb.kind == LabOrbKind.Yin) yin = orb;
                if (orb.id == pairYangId && orb.kind == LabOrbKind.Yang) yang = orb;
            }
            if (yin == null || yang == null)
            {
                yang = null;
                foreach (LabOrbState orb in LocalOrbs()) if (orb.kind == LabOrbKind.Yang) { yang = orb; break; }
                if (yang == null || !TryOrbCircle(yang.id, out Vector2 yangCenter, out _)) return false;
                float nearest = float.MaxValue;
                yin = null;
                foreach (LabOrbState orb in LocalOrbs())
                {
                    if (orb.kind != LabOrbKind.Yin || !TryOrbCircle(orb.id, out Vector2 center, out _)) continue;
                    float distance = (center - yangCenter).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; yin = orb; }
                }
                if (yin == null) return false;
                pairYinId = yin.id;
                pairYangId = yang.id;
            }
            yinId = pairYinId;
            yangId = pairYangId;
            return true;
        }

        private IEnumerable<LabOrbState> LocalOrbs()
        {
            LabNetwork network = session.Network;
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (snapshot?.orbs == null) yield break;
            foreach (LabOrbState orb in snapshot.orbs)
                if (orb != null && orb.owner == network.LocalPlayerId && !orb.inFlight) yield return orb;
        }

        /// <summary>An orb on my board: center and radius in overlay units.</summary>
        private bool TryOrbCircle(string orbId, out Vector2 center, out float radius)
        {
            center = default;
            radius = 0f;
            if (orbId == null || board == null || !board.TryGetScreenCircle(orbId, out Vector2 screen, out float pixels))
                return false;
            center = ScreenToOverlay(screen);
            radius = pixels / overlayCanvas.scaleFactor;
            return true;
        }

        /// <summary>The local drum's center and radius in overlay units.</summary>
        private bool TryDrumCircle(out Vector2 center, out float radius)
        {
            center = default;
            radius = 0f;
            LabSnapshot snapshot = session.Network != null ? session.Network.Snapshot : null;
            if (snapshot?.drums == null || board == null) return false;
            foreach (LabOrbState drum in snapshot.drums)
            {
                if (drum == null || drum.owner != session.Network.LocalPlayerId
                    || !board.TryGetScreenPoint(drum.id, out Vector2 screen)) continue;
                center = ScreenToOverlay(screen);
                radius = board.TryGetDrumScreenDiameter(out float pixels) ? pixels * .5f / overlayCanvas.scaleFactor : 40f;
                return true;
            }
            return false;
        }

        /// <summary>The gourd paint bar at the top together with its "0.0%" text, in overlay units.</summary>
        private bool TryProgressRect(out Rect rect)
        {
            rect = default;
            var bar = hud.yokaiHpFill != null ? hud.yokaiHpFill.transform.parent as RectTransform : null;
            if (bar == null || !TryOverlayRect(bar, out rect)) return false;
            if (hud.hpText != null && hud.hpText.enabled && TryOverlayRect(hud.hpText.rectTransform, out Rect box))
            {
                // The % text's box is much wider than "1.9%": light only the words actually drawn.
                Rect words = DrawnText(box, hud.hpText.rectTransform.rect.width, hud.hpText.preferredWidth, hud.hpText.alignment);
                rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, words.xMin), Mathf.Min(rect.yMin, words.yMin),
                    Mathf.Max(rect.xMax, words.xMax), Mathf.Max(rect.yMax, words.yMax));
            }
            return true;
        }

        /// <summary>
        /// The part of a text box (overlay units) its words take: <paramref name="preferredWidth"/> of the box's
        /// <paramref name="localWidth"/>, placed by the text's alignment.
        /// </summary>
        public static Rect DrawnText(Rect box, float localWidth, float preferredWidth, TextAnchor alignment)
        {
            if (localWidth <= 0f || preferredWidth <= 0f) return box;
            float width = Mathf.Min(box.width, box.width * preferredWidth / localWidth);
            bool left = alignment == TextAnchor.UpperLeft || alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.LowerLeft;
            bool right = alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight;
            float xMin = left ? box.xMin : right ? box.xMax - width : box.center.x - width * .5f;
            return new Rect(xMin, box.yMin, width, box.height);
        }

        private bool TryStaminaRect(out Rect rect)
        {
            rect = default;
            var bar = hud.staminaFill != null ? hud.staminaFill.transform.parent as RectTransform : null;
            return bar != null && TryOverlayRect(bar, out rect);
        }

        private bool TryGourdRect(out Rect rect)
        {
            rect = default;
            Camera view = battle != null ? battle.battleCamera : null;
            MeshCollider box = battle != null && battle.target != null ? battle.target.Hitbox : null;
            if (view == null || box == null) return false;
            Bounds world = box.bounds;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            int seen = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 screen = view.WorldToScreenPoint(corner);
                if (screen.z <= 0f) continue;
                Vector2 local = ScreenToOverlay(screen);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                seen++;
            }
            if (seen == 0) return false;
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private bool TryOverlayRect(RectTransform target, out Rect rect)
        {
            rect = default;
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.rootCanvas.worldCamera;
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = ScreenToOverlay(RectTransformUtility.WorldToScreenPoint(eventCamera, corner));
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private Vector2 ScreenToOverlay(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out Vector2 local);
            return local;
        }
    }
}
