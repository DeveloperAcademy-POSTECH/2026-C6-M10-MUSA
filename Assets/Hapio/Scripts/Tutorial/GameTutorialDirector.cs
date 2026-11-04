using System.Collections;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477) 전투 화면. GameBattleBindings가 연습 판일 때만 붙인다.
    /// 1) HUD: 자리 동그라미(P1·P2·P3)를 캐릭터 얼굴(묵이·불이·금옥이)로 바꾸고 타이머를 숨긴다. 북 미니맵도 얼굴로 그린다.
    /// 2) 대본: 말하는 사람 자리의 얼굴에서 말풍선이 살짝 커졌다가 제자리로 뜨고, 화면을 누르면 다음 대사로 넘어간다.
    ///    대사가 가리키는 곳(박)이 있으면 화면을 어둡게 하고 그곳만 동그랗게 밝히며, 말하는 사람 얼굴도 밝게 둔다.
    /// 3) 마지막 대사 뒤에는 어두워지며 방 목록으로 간다.
    /// 대사를 읽는 동안에는 화면 전체가 눌림을 받으므로 판의 구슬·북은 움직이지 않는다.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GameTutorialDirector : MonoBehaviour
    {
        /// <summary>Above the battle HUD and DEV panel, below the start cover (30000) and the scene fade (30100).</summary>
        public const int OverlaySortingOrder = 20000;
        private const int Seats = 3;

        private GameSession session;
        private GameBattleBindings battle;
        private LabHud hud;
        private GameTutorialArt art;
        private GameObject timerFrame;
        private readonly Image[] portraits = new Image[Seats];
        private readonly Vector3[] portraitScales = new Vector3[Seats];
        private readonly Vector2[] portraitPositions = new Vector2[Seats];
        private readonly Vector3[] corners = new Vector3[4];

        private RectTransform overlay;
        private Button tapCatcher;
        private CanvasGroup dimGroup;
        private GameTutorialSpotlight spotlight;
        private Image speakerPortrait;
        private RectTransform bubbleRoot;
        private RectTransform bubbleArt;
        private Text bubbleText;

        private int stepIndex;
        private int lineIndex = -1;
        private float playableAt = -1f;
        private float shownAt;
        private float popStartedAt;
        private float dimTarget;
        private bool finishing;
        private bool unavailable;

        /// <summary>The step being played (index into <see cref="GameTutorialScript.Steps"/>).</summary>
        public int StepIndex => stepIndex;
        /// <summary>The line on screen in that step, or -1 before the first line.</summary>
        public int LineIndex => lineIndex;
        public bool IsFinishing => finishing;
        public bool IsShowingLine => lineIndex >= 0 && !finishing && bubbleRoot != null && bubbleRoot.gameObject.activeSelf;

        public void Bind(GameSession owner, GameBattleBindings bindings)
        {
            session = owner;
            battle = bindings;
            hud = bindings != null ? bindings.hud : null;
            art = GameTutorialArt.Load();
            if (session == null || hud == null || art == null || art.bubble == null)
            {
                Debug.LogWarning("HAPIO_TUTORIAL_UNAVAILABLE: the tutorial art or the battle HUD is missing.", this);
                unavailable = true;
                return;
            }
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
            Vector2 saved = portraitPositions[seat];
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

        /// <summary>How far a span [low, high] must move to sit inside [from, to]; nothing when it is too large to fit.</summary>
        public static float Inward(float low, float high, float from, float to)
        {
            if (high - low > to - from) return 0f;
            if (low < from) return from - low;
            if (high > to) return to - high;
            return 0f;
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
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;
            CopyHudScaler(root.GetComponent<CanvasScaler>());
            overlay = (RectTransform)root.transform;

            // Every touch lands here while lines are read, so the board under it never moves.
            Image catcher = AddImage("Tap To Continue", overlay, Color.clear, null, true);
            Stretch(catcher.rectTransform);
            tapCatcher = catcher.gameObject.AddComponent<Button>();
            tapCatcher.transition = Selectable.Transition.None;
            tapCatcher.onClick.AddListener(OnTap);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasGroup));
            dim.transform.SetParent(overlay, false);
            Stretch((RectTransform)dim.transform);
            dimGroup = dim.GetComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.blocksRaycasts = dimGroup.interactable = false;
            // One mesh with a round hole: separate pieces would double-darken where they meet.
            spotlight = new GameObject("Spotlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameTutorialSpotlight))
                .GetComponent<GameTutorialSpotlight>();
            spotlight.rectTransform.SetParent(dim.transform, false);
            Stretch(spotlight.rectTransform);
            spotlight.color = art.dimColor;
            spotlight.raycastTarget = false;

            speakerPortrait = AddImage("Speaker", overlay, Color.white, null, false);
            speakerPortrait.preserveAspect = true;
            speakerPortrait.enabled = false;

            bubbleRoot = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
            bubbleRoot.SetParent(overlay, false);
            Center(bubbleRoot);
            bubbleRoot.sizeDelta = art.bubbleSize;
            Image bubbleImage = AddImage("Bubble Art", bubbleRoot, Color.white, art.bubble, false);
            bubbleArt = bubbleImage.rectTransform;
            Stretch(bubbleArt);
            bubbleText = new GameObject("Line", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            bubbleText.rectTransform.SetParent(bubbleRoot, false);
            bubbleText.font = art.dialogueFont != null ? art.dialogueFont
                : hud.timeText != null ? hud.timeText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bubbleText.fontSize = art.maxFontSize;
            bubbleText.resizeTextForBestFit = true;
            bubbleText.resizeTextMaxSize = art.maxFontSize;
            bubbleText.resizeTextMinSize = Mathf.Min(art.minFontSize, art.maxFontSize);
            bubbleText.alignment = TextAnchor.MiddleCenter;
            bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            bubbleText.supportRichText = false;
            bubbleText.raycastTarget = false;
            bubbleText.color = art.textColor;
            bubbleRoot.gameObject.SetActive(false);
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
            if (session == null) return;
            if (unavailable)
            {
                if (!finishing) { finishing = true; session.FinishTutorial(); }
                return;
            }
            if (finishing || lineIndex >= 0) return;
            if (!BattlePlayable()) { playableAt = -1f; return; }
            if (playableAt < 0f) playableAt = Time.unscaledTime;
            if (Time.unscaledTime - playableAt >= art.firstLineDelay) Advance();
        }

        private bool BattlePlayable() => session.IsTutorial && session.State.phase == GameFlowPhase.Playing
            && session.Network != null && !session.Network.GameStartHeld
            && (session.StartOverlay == null || !session.StartOverlay.Visible);

        private void OnTap()
        {
            if (finishing || lineIndex < 0 || Time.unscaledTime - shownAt < art.tapCooldown) return;
            Advance();
        }

        private void Advance()
        {
            IReadOnlyList<GameTutorialStep> steps = GameTutorialScript.Steps;
            lineIndex++;
            while (stepIndex < steps.Count && lineIndex >= steps[stepIndex].Lines.Count)
            {
                stepIndex++;
                lineIndex = 0;
            }
            if (stepIndex >= steps.Count) { BeginFinish(); return; }
            ShowLine(steps[stepIndex].Lines[lineIndex]);
        }

        private void ShowLine(GameTutorialLine line)
        {
            bool fromRight = line.Speaker == GameTutorialSpeaker.Geumok;
            bubbleText.text = line.Text;
            // The art's tail points down-left; the right neighbor's bubble is mirrored, its words are not.
            bubbleArt.localScale = new Vector3(fromRight ? -1f : 1f, 1f, 1f);
            Vector4 inset = art.textInset;
            RectTransform words = bubbleText.rectTransform;
            words.anchorMin = new Vector2(fromRight ? inset.z : inset.x, inset.y);
            words.anchorMax = new Vector2(1f - (fromRight ? inset.x : inset.z), 1f - inset.w);
            words.offsetMin = words.offsetMax = Vector2.zero;
            bubbleRoot.pivot = fromRight ? new Vector2(1f - art.tailTip.x, art.tailTip.y) : art.tailTip;
            bubbleRoot.sizeDelta = art.bubbleSize;
            Image face = portraits[GameTutorialScript.SeatOf(line.Speaker)];
            speakerPortrait.sprite = face != null ? face.sprite : null;
            dimTarget = line.Focus == GameTutorialFocus.None ? 0f : 1f;
            bubbleRoot.gameObject.SetActive(true);
            bubbleRoot.localScale = Vector3.zero;
            popStartedAt = shownAt = Time.unscaledTime;
            PlaceBubble(line.Speaker);
        }

        private void BeginFinish()
        {
            finishing = true;
            if (bubbleRoot != null) bubbleRoot.gameObject.SetActive(false);
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
            if (lineIndex >= 0 && !finishing)
            {
                GameTutorialLine line = GameTutorialScript.Steps[stepIndex].Lines[lineIndex];
                PlaceBubble(line.Speaker);
                bubbleRoot.localScale = Vector3.one * PopScale((Time.unscaledTime - popStartedAt) / art.popSeconds,
                    art.popOvershoot);
            }
            dimGroup.alpha = Mathf.MoveTowards(dimGroup.alpha, dimTarget, Time.unscaledDeltaTime / art.dimFadeSeconds);
            if (dimGroup.alpha > 0f) PlaceSpotlight();
            speakerPortrait.enabled = dimGroup.alpha > .01f && speakerPortrait.sprite != null && !finishing;
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
            }
            else tip = new Vector2(bounds.center.x, bounds.yMin + bounds.height * .3f);

            Vector2 size = art.bubbleSize, pivot = bubbleRoot.pivot;
            float margin = art.screenMargin;
            float left = tip.x - pivot.x * size.x, right = left + size.x;
            float top = tip.y + (1f - pivot.y) * size.y;
            if (left < bounds.xMin + margin) tip.x += bounds.xMin + margin - left;
            else if (right > bounds.xMax - margin) tip.x -= right - (bounds.xMax - margin);
            if (top > bounds.yMax - margin) tip.y -= top - (bounds.yMax - margin);
            bubbleRoot.anchoredPosition = tip;
        }

        private void PlaceSpotlight()
        {
            Rect bounds = overlay.rect;
            if (!TryGourdRect(out Rect target))
                target = new Rect(bounds.center.x - 60f, bounds.yMax - bounds.height * .4f, 120f, 120f);
            float radius = .5f * Mathf.Max(target.width, target.height) * art.spotlightPadding;
            // The spotlight stretches over the overlay with a centered pivot, so overlay points are its local points.
            spotlight.SetHole(target.center, radius, art.spotlightSoftness);
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
            var corners = new Vector3[4];
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
