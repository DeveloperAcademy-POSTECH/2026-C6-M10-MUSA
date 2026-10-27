using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 피버타임 (#343) 시작 연출. 피버가 시작되는 순간 모든 기기 화면에
    /// - 화면 전체가 하양·오색으로 몇 번 번쩍이고,
    /// - 가운데에 "피버타임!" 문구가 크게 튀어나와 오색구슬(#329)처럼 색이 바뀌며 잠시 머물고,
    /// - 부적(`Resources/Combo/Bujeok`, 콤보 보상과 같은 그림)이 한 번 쏟아지고,
    /// 그 뒤로도 피버 내내 부적이 공격 영역(조합판 위)에서만 조금씩 흘러내린다. 던지기를 가리지 않게 양은 적다.
    /// 번쩍임·문구는 피버 시작 뒤 조금 지나 들어온 기기(StartWindowSeconds 넘음)에는 띄우지 않고, 흐르는 부적만 보인다.
    /// 화면 표시만 하며 터치를 막지 않는다.
    /// 구미호 피버 타임(#334)과는 따로 돌아가고, 야광귀 스테이지에서만 켜진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangFeverHud : MonoBehaviour
    {
        /// <summary>피버가 시작된 지 이 초 안에 본 기기만 시작 연출을 띄운다.</summary>
        public const double StartWindowSeconds = 1.5d;
        private static readonly Color InkColor = new Color32(43, 36, 32, 255);

        [Header("문구")]
        [SerializeField] private string titleText = "피버타임!";
        [Tooltip("문구 밑 작은 줄. 비우면(기본) 안 띄운다.")]
        [SerializeField] private string subtitleText = "";
        [Tooltip("문구 위치 (0~1, 왼쪽 아래가 0)")]
        [SerializeField] private Vector2 bannerAnchor = new Vector2(.5f, .6f);
        [SerializeField, Range(24, 200)] private int titleSize = 96;
        [SerializeField, Range(12, 100)] private int subtitleSize = 34;
        [Tooltip("문구가 떠 있는 시간 (초). 마지막 0.5초 동안 흐려진다.")]
        [SerializeField, Range(.5f, 6f)] private float bannerSeconds = 2.6f;
        [Tooltip("문구 색이 다음 색으로 넘어가는 시간 (초)")]
        [SerializeField, Min(.05f)] private float colorSeconds = .18f;
        [Header("번쩍임")]
        [Tooltip("화면이 번쩍이는 시간 (초)")]
        [SerializeField, Range(.2f, 3f)] private float flashSeconds = 1.2f;
        [Tooltip("그 동안 번쩍이는 횟수")]
        [SerializeField, Range(1, 8)] private int flashCount = 3;
        [Tooltip("가장 밝을 때 불투명도")]
        [SerializeField, Range(0f, 1f)] private float flashAlpha = .55f;
        [Header("부적 (공격 영역에서만, 피버 내내)")]
        [Tooltip("피버 동안 초당 떨어지는 수 (부적은 이 값의 절반). 던지기를 가리지 않게 적게 둔다.")]
        [SerializeField, Min(0f)] private float pourPerSecond = 5f;
        [Tooltip("피버가 시작될 때 한꺼번에 쏟아지는 수 (부적은 이 값의 절반)")]
        [SerializeField, Range(0, 112)] private int pourBurst = 16;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer, banner;
        private Image flash;
        private Text title, subtitle;
        private LabComboRewardStars talismans;
        private bool fever, showing;
        private float since;

        /// <summary>시작 연출을 띄운 횟수 (테스트·확인용)</summary>
        public int ShowCount { get; private set; }
        /// <summary>지금 시작 연출이 화면에 있는지</summary>
        public bool Showing => showing;

        public void Bind(LabNetwork target, Canvas uiCanvas)
        {
            network = target;
            canvas = uiCanvas;
        }

        private void LateUpdate()
        {
            double elapsed = network != null ? network.YaguangFeverElapsed(network.Snapshot) : -1d;
            bool now = elapsed >= 0d;
            if (now != fever)
            {
                fever = now;
                if (fever) Begin((float)elapsed, ShouldShow(elapsed));
                else Stop();
            }
            if (fever && layer != null && layer.gameObject.activeSelf) Step(Time.unscaledDeltaTime);
        }

        private void OnDisable() => Stop();

        /// <summary>피버가 시작된 지 elapsed초 지난 때 처음 봤다면 시작 연출을 띄울지 (늦게 들어온 기기는 안 띄운다).</summary>
        public static bool ShouldShow(double elapsed) => elapsed >= 0d && elapsed < StartWindowSeconds;

        /// <summary>피버가 켜졌다. 처음부터 본 기기면(show) 번쩍임·문구·한꺼번에 쏟아지는 부적까지, 아니면 흐르는 부적만.</summary>
        private void Begin(float elapsed, bool show)
        {
            if (!EnsureLayer()) return;
            showing = show;
            since = show ? Mathf.Max(0f, elapsed) : float.MaxValue;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
            talismans.Clear();
            talismans.SetRates(pourPerSecond, pourPerSecond, pourBurst);
            if (!show) return;
            ShowCount++;
            talismans.Burst(TalismanBottom());
        }

        /// <summary>
        /// 부적이 사라지는 높이: 조합판 위쪽 가장자리 (콤보 보상 부적과 같다). 그래서 부적은 공격 영역에서만 보인다.
        /// 판을 못 찾으면 화면 가운데쯤.
        /// </summary>
        private float TalismanBottom()
        {
            Rect area = layer.rect;
            float fallback = Mathf.Lerp(area.yMin, area.yMax, .45f);
            if (network == null || network.board == null || !network.board.TryGetInputScreenRect(out Rect screen))
                return fallback;
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(talismans.Area,
                new Vector2(screen.center.x, screen.yMax), uiCamera, out Vector2 local) ? local.y : fallback;
        }

        private void Stop()
        {
            showing = false;
            if (talismans != null) talismans.Clear();
            if (layer != null) layer.gameObject.SetActive(false);
        }

        private void Step(float deltaTime)
        {
            since += deltaTime;
            float time = Time.unscaledTime;
            // 번쩍임: 하양과 오색을 번갈아 몇 번 번쩍이고 점점 약해진다.
            float flashAmount = FlashAmount(since, flashSeconds, flashCount);
            flash.gameObject.SetActive(flashAmount > 0f);
            if (flashAmount > 0f)
            {
                int beat = Mathf.FloorToInt(since / Mathf.Max(.01f, flashSeconds) * flashCount);
                Color color = beat % 2 == 0 ? Color.white
                    : Color.Lerp(LabFiveColorSparkle.ColorAt(time), Color.white, .35f);
                color.a = flashAlpha * flashAmount;
                flash.color = color;
            }
            // 문구: 크게 튀어나왔다가 제 크기로 돌아와 살짝 두근거리고, 끝에 흐려진다.
            float bannerAlpha = BannerAlpha(since, bannerSeconds);
            banner.gameObject.SetActive(bannerAlpha > 0f);
            if (bannerAlpha > 0f)
            {
                banner.localScale = Vector3.one * BannerScale(since);
                Color titleColor = LabFiveColorSparkle.ColorAt(Mathf.Repeat(time / Mathf.Max(.05f, colorSeconds), 5f) * .9f);
                titleColor.a = bannerAlpha;
                title.color = titleColor;
                subtitle.color = new Color(1f, .97f, .88f, bannerAlpha);
            }
            // 부적: 피버 내내 공격 영역에서 같은 양으로 조금씩 흘러내린다.
            talismans.Step(true, 1f, deltaTime, TalismanBottom());
            if (bannerAlpha <= 0f && flashAmount <= 0f) showing = false;
        }

        /// <summary>시작한 지 t초에 번쩍이는 세기 (0~1): count번 번쩍이며 seconds초 동안 점점 약해진다.</summary>
        public static float FlashAmount(float t, float seconds, int count)
        {
            if (seconds <= 0f || t < 0f || t >= seconds) return 0f;
            float u = t / seconds;
            float pulse = Mathf.Abs(Mathf.Sin(Mathf.PI * u * Mathf.Max(1, count)));
            return pulse * (1f - u);
        }

        /// <summary>시작한 지 t초의 문구 크기 배율: 작게 시작해 1.25배까지 튀었다가 1배로 돌아와 살짝 두근거린다.</summary>
        public static float BannerScale(float t)
        {
            if (t < .22f) return Mathf.Lerp(.3f, 1.25f, Mathf.Max(0f, t) / .22f);
            if (t < .4f) return Mathf.Lerp(1.25f, 1f, (t - .22f) / .18f);
            return 1f + .04f * Mathf.Sin((t - .4f) * 9f);
        }

        /// <summary>시작한 지 t초의 문구 불투명도: seconds초 동안 보이고 마지막 0.5초에 흐려진다.</summary>
        public static float BannerAlpha(float t, float seconds)
        {
            if (t < 0f || t >= seconds) return 0f;
            float fade = Mathf.Min(.5f, seconds * .5f);
            return t < seconds - fade ? 1f : Mathf.Clamp01((seconds - t) / fade);
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null) return false;
            layer = NewRect("Yaguang Fever Start", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            RectTransform flashRect = NewRect("Flash", layer);
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            flash = flashRect.gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flashRect.gameObject.SetActive(false);

            // 부적은 번쩍임 위, 문구 아래로 쏟아진다.
            talismans = layer.gameObject.AddComponent<LabComboRewardStars>();
            float unit = Mathf.Clamp(canvasRect.rect.height / 1000f, .6f, 2.5f);
            talismans.Initialize(layer, unit);

            Font font = ShillaFont(canvas);
            banner = NewRect("Banner", layer);
            banner.anchorMin = banner.anchorMax = bannerAnchor;
            banner.sizeDelta = new Vector2(800f, titleSize + subtitleSize + 40f);
            bool sub = !string.IsNullOrEmpty(subtitleText);
            title = Words("Title", font, titleSize, new Vector2(0f, sub ? subtitleSize * .6f : 0f), titleText);
            subtitle = Words("Subtitle", font, subtitleSize, new Vector2(0f, -titleSize * .55f), subtitleText);
            subtitle.gameObject.SetActive(sub);
            banner.gameObject.SetActive(false);
            layer.gameObject.SetActive(false);
            return true;
        }

        /// <summary>전투 화면이 쓰는 신라문화체(ShillaCulture, 굵은 것 먼저). 화면에 없으면 화면의 첫 글꼴.</summary>
        public static Font ShillaFont(Canvas canvas)
        {
            Text[] texts = canvas != null ? canvas.GetComponentsInChildren<Text>(true) : new Text[0];
            Font found = null;
            foreach (Text text in texts)
            {
                if (text.font == null || text.font.name.IndexOf("Shilla", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (text.font.name.IndexOf("Bold", System.StringComparison.OrdinalIgnoreCase) >= 0) return text.font;
                if (found == null) found = text.font;
            }
            return found != null ? found : LabGuideText.FontOf(canvas);
        }

        private Text Words(string name, Font font, int size, Vector2 offset, string words)
        {
            RectTransform rect = NewRect(name, banner);
            rect.sizeDelta = new Vector2(800f, size + 16f);
            rect.anchoredPosition = offset;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = words;
            // 번쩍이는 하얀 화면 위에서도 읽히게 진한 테두리를 두 겹 두른다.
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(3f, -3f);
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(InkColor.r, InkColor.g, InkColor.b, .6f);
            shadow.effectDistance = new Vector2(0f, -6f);
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            return rect;
        }

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
