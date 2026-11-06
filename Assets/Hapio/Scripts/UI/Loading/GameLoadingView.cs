using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 공용 로딩 화면 (#478). 위에서부터 한지 배경, 가운데에서 제자리로 달리는 캐릭터(뜰 때마다 무작위, 지금은 불이·묵이), "로딩중",
    /// 진행 막대. 막대의 차오르는 끝 위에 % 말풍선이 붙어 따라가고, 움직이는 동안 살짝 뒤로 기울었다가 멈추면(100%) 똑바로 선다.
    /// 말풍선은 "로딩중" 아래 끝보다 아래에 오도록 막대 자리를 정한다(겹치지 않게).
    /// 보이는 진행은 실제 진행을 부드럽게 따라가고 뒤로 가지 않는다. 100%가 되면 캐릭터는 다음 착지에서 멈춰 선다.
    /// 이 화면은 그리기만 한다. 언제 띄우고 진행이 얼마인지는 부르는 쪽(GameSession)이 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameLoadingView : MonoBehaviour
    {
        /// <summary>
        /// Above the start cover (30000) and the scene fade (30100): while a screen loads, the loading screen is what shows,
        /// not the black cover under it.
        /// </summary>
        public const int SortingOrder = 30150;

        private GameLoadingArt art;
        private System.Random random;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform root;
        private RawImage paper;
        private Image runner;
        private Text caption;
        private GameLoadingShape trackEdge, track, fill, bubble;
        private RectTransform bubbleRoot;
        private Text percent;
        private Text detail;
        private Coroutine preview, fading;

        private GameLoadingArt.Runner current;
        private float target, displayed, previousDisplayed;
        private float runClock, squashAt = -1f, tilt;
        private int frame = -1;
        private bool resting;

        public bool Visible => group != null && group.alpha > 0f;
        /// <summary>The progress drawn now (0..1); it follows <see cref="TargetProgress"/> and never goes back.</summary>
        public float DisplayedProgress => displayed;
        public float TargetProgress => target;
        public string PercentText => percent != null ? percent.text : string.Empty;
        public float BubbleTilt => tilt;
        public string RunnerName => current != null ? current.name : string.Empty;
        public int RunnerFrame => frame;
        /// <summary>At 100% the runner has landed and stands still.</summary>
        public bool RunnerResting => resting;
        public RectTransform Bar => trackEdge != null ? trackEdge.rectTransform : null;
        public RectTransform Bubble => bubbleRoot;
        public RectTransform Caption => caption != null ? caption.rectTransform : null;
        /// <summary>The line under the bar (the ready count while a battle is prepared), empty when none.</summary>
        public string DetailText => detail != null && detail.gameObject.activeSelf ? detail.text : string.Empty;
        public bool FadingOut => fading != null;

        public static GameLoadingView Create(Transform parent, GameLoadingArt art = null, System.Random random = null)
        {
            art = art != null ? art : GameLoadingArt.Load();
            if (art == null) throw new InvalidOperationException("The loading art is missing: Resources/" + GameLoadingArt.ResourcePath);
            var go = new GameObject("GameLoadingView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<GameLoadingView>();
            view.Build(art, random ?? new System.Random());
            view.Hide();
            return view;
        }

        private void Build(GameLoadingArt loadingArt, System.Random rng)
        {
            art = loadingArt;
            random = rng;
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = .5f;
            group = GetComponent<CanvasGroup>();
            root = (RectTransform)transform;

            Image backing = Add<Image>("Background", root);
            Stretch(backing.rectTransform);
            backing.color = art.backgroundColor;
            backing.raycastTarget = true;
            paper = Add<RawImage>("Paper", root);
            Stretch(paper.rectTransform);
            paper.texture = art.paperTexture;
            paper.color = new Color(1f, 1f, 1f, art.paperTexture != null ? art.paperStrength : 0f);
            paper.raycastTarget = false;

            runner = Add<Image>("Runner", root);
            runner.preserveAspect = true;
            runner.raycastTarget = false;
            runner.rectTransform.pivot = new Vector2(.5f, 0f);

            caption = Add<Text>("Caption", root);
            caption.font = art.captionFont != null ? art.captionFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = art.captionFontSize;
            caption.color = art.captionColor;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            caption.raycastTarget = false;
            caption.text = art.caption;

            trackEdge = Shape("Bar", root, art.trackEdgeColor);
            track = Shape("Track", trackEdge.rectTransform, art.trackColor);
            fill = Shape("Fill", trackEdge.rectTransform, art.fillColor);
            fill.rectTransform.pivot = new Vector2(0f, .5f);

            bubbleRoot = new GameObject("Percent Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
            bubbleRoot.SetParent(root, false);
            // Pivot at the tail's tip, so it tilts about the point it shows.
            bubbleRoot.pivot = new Vector2(.5f, 0f);
            bubble = Shape("Bubble", bubbleRoot, art.bubbleColor);
            bubble.SetShape(art.bubbleCorner, art.bubbleTail, art.bubbleTailWidth);
            Stretch(bubble.rectTransform);
            percent = Add<Text>("Percent", bubbleRoot);
            RectTransform words = percent.rectTransform;
            words.anchorMin = new Vector2(0f, art.bubbleTail / Mathf.Max(1f, art.bubbleSize.y + art.bubbleTail));
            words.anchorMax = Vector2.one;
            words.offsetMin = words.offsetMax = Vector2.zero;
            percent.font = art.percentFont != null ? art.percentFont : caption.font;
            percent.fontSize = art.percentFontSize;
            percent.color = art.percentColor;
            percent.alignment = TextAnchor.MiddleCenter;
            percent.horizontalOverflow = HorizontalWrapMode.Overflow;
            percent.raycastTarget = false;

            detail = Add<Text>("Detail", root);
            detail.font = caption.font;
            detail.fontSize = art.detailFontSize;
            detail.color = art.detailColor;
            detail.alignment = TextAnchor.MiddleCenter;
            detail.horizontalOverflow = HorizontalWrapMode.Overflow;
            detail.verticalOverflow = VerticalWrapMode.Overflow;
            detail.raycastTarget = false;
            detail.gameObject.SetActive(false);
        }

        /// <summary>A short line under the bar ("준비 2/3"); empty hides it.</summary>
        public void SetDetail(string text)
        {
            bool shown = !string.IsNullOrEmpty(text);
            if (detail.gameObject.activeSelf != shown) detail.gameObject.SetActive(shown);
            if (shown && detail.text != text) detail.text = text;
        }

        /// <summary>Close with a short fade (<see cref="GameLoadingArt.fadeOutSeconds"/>); showing again cancels it.</summary>
        public void FadeOut()
        {
            if (!Visible || fading != null) return;
            group.blocksRaycasts = group.interactable = false;
            if (art.fadeOutSeconds <= 0f || !isActiveAndEnabled) { Hide(); return; }
            fading = StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < art.fadeOutSeconds)
            {
                group.alpha = 1f - (Time.unscaledTime - started) / art.fadeOutSeconds;
                yield return null;
            }
            fading = null;
            Hide();
        }

        /// <summary>Show from <paramref name="progress"/> (0..1) with a newly drawn runner.</summary>
        public void Show(float progress = 0f)
        {
            if (preview != null) { StopCoroutine(preview); preview = null; }
            if (fading != null) { StopCoroutine(fading); fading = null; }
            SetDetail(string.Empty);
            target = displayed = previousDisplayed = Mathf.Clamp01(progress);
            tilt = 0f;
            runClock = 0f;
            frame = -1;
            squashAt = -1f;
            resting = false;
            current = PickRunner(art.runners, random);
            // The first picture at once: an Image without a sprite would flash white for a frame.
            bool drawn = current != null && current.Usable;
            runner.enabled = drawn;
            runner.sprite = drawn ? current.frames[0] : null;
            frame = drawn ? 0 : -1;
            runner.rectTransform.localScale = Vector3.one;
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = false;
            Layout();
        }

        /// <summary>How far loading has got (0..1). The bar follows it smoothly and never goes back.</summary>
        public void SetProgress(float progress)
        {
            if (!float.IsNaN(progress)) target = Mathf.Max(target, Mathf.Clamp01(progress));
        }

        public void Hide()
        {
            if (preview != null) { StopCoroutine(preview); preview = null; }
            if (fading != null) { StopCoroutine(fading); fading = null; }
            if (group == null) return;
            group.alpha = 0f;
            group.blocksRaycasts = group.interactable = false;
        }

        /// <summary>Editor/DEV check: 0 → 100% over <paramref name="seconds"/> in uneven steps, then a short hold and hide.</summary>
        public void PlayPreview(float seconds)
        {
            Show(0f);
            preview = StartCoroutine(Preview(Mathf.Max(.5f, seconds)));
        }

        private IEnumerator Preview(float seconds)
        {
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < seconds)
            {
                float t = (Time.unscaledTime - started) / seconds;
                // Real loads come in uneven steps: hold, jump, creep.
                SetProgress(Mathf.Floor(t * 7f) / 7f * .92f + t * .08f);
                yield return null;
            }
            SetProgress(1f);
            while (displayed < 1f) yield return null;
            yield return new WaitForSecondsRealtime(art.previewHoldSeconds);
            preview = null;
            FadeOut();
        }

        private void Update()
        {
            if (!Visible) return;
            float dt = Time.unscaledDeltaTime;
            previousDisplayed = displayed;
            displayed = NextDisplayed(displayed, target, dt, art.displayFollow, art.displayMaxSpeed);
            float speed = dt > 0f ? (displayed - previousDisplayed) / dt : 0f;
            tilt = Mathf.Lerp(tilt, BubbleTiltFor(speed, art.bubbleTiltPerSpeed, art.bubbleMaxTilt),
                1f - Mathf.Exp(-art.bubbleTiltFollow * dt));
            if (displayed >= 1f && speed <= 0f && tilt < .05f) tilt = 0f;
            Animate(dt);
            Layout();
        }

        private void Animate(float dt)
        {
            if (current == null || !current.Usable) { runner.enabled = false; return; }
            runner.enabled = true;
            int count = current.frames.Length;
            if (!resting) runClock += dt;
            int next = FrameAt(runClock, current.frameSeconds, count);
            if (next != frame)
            {
                frame = next;
                runner.sprite = current.frames[frame];
                if (IsLanding(current, frame))
                {
                    squashAt = Time.unscaledTime;
                    // At 100% the runner finishes its stride and stands on this landing.
                    if (displayed >= 1f) resting = true;
                }
            }
            float squash = LandingSquash(Time.unscaledTime - squashAt, art.landingSquashSeconds, art.landingSquash);
            runner.rectTransform.localScale = new Vector3(1f + squash, 1f - squash, 1f);
        }

        private void Layout()
        {
            Rect area = root.rect;
            float top = area.yMax;
            Vector2 offset = current != null ? current.offset : Vector2.zero;
            float height = art.runnerHeight * (current != null ? current.scale : 1f);
            Sprite sprite = runner.sprite != null ? runner.sprite : current != null && current.Usable ? current.frames[0] : null;
            float aspect = sprite != null ? sprite.rect.width / Mathf.Max(1f, sprite.rect.height) : 1f;
            float runnerCenter = top - area.height * art.runnerCenterFromTop;
            float runnerBottom = runnerCenter - art.runnerHeight * .5f;
            RectTransform body = runner.rectTransform;
            body.anchoredPosition = new Vector2(offset.x, runnerBottom + offset.y);
            body.sizeDelta = new Vector2(height * aspect, height);

            float captionHeight = art.captionFontSize * 1.3f;
            float captionTop = runnerBottom - art.captionGapBelowRunner;
            caption.rectTransform.sizeDelta = new Vector2(area.width, captionHeight);
            caption.rectTransform.anchoredPosition = new Vector2(0f, captionTop - captionHeight * .5f);

            float barCenter = BarCenterBelow(captionTop - captionHeight, art.bubbleGapBelowCaption, art.bubbleSize.y,
                art.bubbleTail, art.bubbleGapAboveBar, art.barHeight);
            RectTransform bar = trackEdge.rectTransform;
            bar.sizeDelta = new Vector2(art.barWidth, art.barHeight);
            bar.anchoredPosition = new Vector2(0f, barCenter);
            trackEdge.SetShape(-1f);
            Inset(track.rectTransform, art.barBorder);
            track.SetShape(-1f);
            float innerHeight = Mathf.Max(1f, art.barHeight - art.fillInset * 2f);
            float innerWidth = Mathf.Max(innerHeight, art.barWidth - art.fillInset * 2f);
            RectTransform head = fill.rectTransform;
            head.anchorMin = head.anchorMax = new Vector2(0f, .5f);
            head.sizeDelta = new Vector2(FillWidth(displayed, innerWidth, innerHeight), innerHeight);
            head.anchoredPosition = new Vector2(art.fillInset, 0f);
            fill.SetShape(-1f);

            float tipX = -art.barWidth * .5f + art.fillInset + HeadCenter(displayed, innerWidth, innerHeight);
            bubbleRoot.sizeDelta = new Vector2(art.bubbleSize.x, art.bubbleSize.y + art.bubbleTail);
            bubbleRoot.anchoredPosition = new Vector2(tipX, barCenter + art.barHeight * .5f + art.bubbleGapAboveBar);
            bubbleRoot.localEulerAngles = new Vector3(0f, 0f, tilt);
            float detailHeight = art.detailFontSize * 1.3f;
            detail.rectTransform.sizeDelta = new Vector2(area.width, detailHeight);
            detail.rectTransform.anchoredPosition = new Vector2(0f,
                barCenter - art.barHeight * .5f - art.detailGapBelowBar - detailHeight * .5f);
            string label = PercentLabel(displayed);
            if (percent.text != label) percent.text = label;
            if (paper.texture != null) paper.uvRect = CoverUv(area.size, paper.texture.width / (float)Mathf.Max(1, paper.texture.height));
        }

        // ---------- Pure rules (tested) ----------

        /// <summary>The bar's middle so the % bubble on it ends <paramref name="gap"/> below the caption's bottom.</summary>
        public static float BarCenterBelow(float captionBottom, float gap, float bubbleHeight, float tail, float bubbleGap,
            float barHeight) => captionBottom - gap - bubbleHeight - tail - bubbleGap - barHeight * .5f;

        /// <summary>The fill's length: a round dot at 0, the whole inside at 1.</summary>
        public static float FillWidth(float progress, float innerWidth, float innerHeight) =>
            innerHeight + Mathf.Clamp01(progress) * Mathf.Max(0f, innerWidth - innerHeight);

        /// <summary>The middle of the fill's round head from the inside's left edge: where the bubble's tail points.</summary>
        public static float HeadCenter(float progress, float innerWidth, float innerHeight) =>
            FillWidth(progress, innerWidth, innerHeight) - innerHeight * .5f;

        /// <summary>Whole percent shown in the bubble; 100% only when it is really full.</summary>
        public static string PercentLabel(float progress) =>
            Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * 100f + 1e-4f), 0, 100) + "%";

        /// <summary>One frame of the shown progress easing toward the target: never back, never faster than maxSpeed.</summary>
        public static float NextDisplayed(float shown, float target, float deltaTime, float follow, float maxSpeed)
        {
            if (target <= shown) return shown;
            float step = (target - shown) * (1f - Mathf.Exp(-Mathf.Max(0f, follow) * deltaTime));
            step = Mathf.Min(step, Mathf.Max(0f, maxSpeed) * deltaTime);
            float next = shown + step;
            // The last half percent snaps, so an instant load fills 0→100% in about 0.8 s instead of creeping.
            return target - next < .005f ? target : next;
        }

        /// <summary>How far the bubble leans back while moving at <paramref name="speed"/> bar widths per second.</summary>
        public static float BubbleTiltFor(float speed, float perSpeed, float max) => Mathf.Clamp(speed * perSpeed, 0f, max);

        public static int FrameAt(float clock, float frameSeconds, int count) =>
            count <= 0 ? 0 : (int)(Mathf.Max(0f, clock) / Mathf.Max(.0001f, frameSeconds)) % count;

        public static bool IsLanding(GameLoadingArt.Runner runner, int frame) =>
            runner?.landingFrames != null && Array.IndexOf(runner.landingFrames, frame) >= 0;

        /// <summary>How flat the runner is <paramref name="since"/> seconds after landing: amount at once, easing back to 0.</summary>
        public static float LandingSquash(float since, float seconds, float amount) =>
            since < 0f || since >= seconds || seconds <= 0f ? 0f : amount * (1f - since / seconds);

        /// <summary>A runner with pictures, drawn at random each time (only usable ones; null if there is none).</summary>
        public static GameLoadingArt.Runner PickRunner(GameLoadingArt.Runner[] runners, System.Random random)
        {
            if (runners == null) return null;
            int usable = 0;
            foreach (GameLoadingArt.Runner candidate in runners) if (candidate != null && candidate.Usable) usable++;
            if (usable == 0) return null;
            int pick = (random ?? new System.Random()).Next(usable);
            foreach (GameLoadingArt.Runner candidate in runners)
                if (candidate != null && candidate.Usable && pick-- == 0) return candidate;
            return null;
        }

        /// <summary>The part of a texture that fills <paramref name="size"/> without stretching it.</summary>
        public static Rect CoverUv(Vector2 size, float textureAspect)
        {
            float screen = size.y > 0f ? size.x / size.y : 1f;
            textureAspect = Mathf.Max(.0001f, textureAspect);
            if (screen < textureAspect) { float w = screen / textureAspect; return new Rect((1f - w) * .5f, 0f, w, 1f); }
            float h = textureAspect / screen;
            return new Rect(0f, (1f - h) * .5f, 1f, h);
        }

        // ---------- Building ----------

        private static T Add<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return go.GetComponent<T>();
        }

        private static GameLoadingShape Shape(string name, Transform parent, Color color)
        {
            GameLoadingShape shape = Add<GameLoadingShape>(name, parent);
            shape.color = color;
            shape.raycastTarget = false;
            return shape;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Inset(RectTransform rect, float by)
        {
            Stretch(rect);
            rect.offsetMin = Vector2.one * by;
            rect.offsetMax = -Vector2.one * by;
        }
    }
}
