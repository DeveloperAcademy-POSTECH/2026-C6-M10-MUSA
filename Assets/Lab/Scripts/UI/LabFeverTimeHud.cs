using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 피버 타임 (#334) 연출. "두배" 말풍선: 피버 타임이 시작되면 스태미나 막대 오른쪽 끝 위에서 연기가 퍼지고, 그 안에서
    /// 말풍선이 톡 튀어나온다. 피버 동안 커졌다 작아졌다를 되풀이하고, 전투가 끝나면 사라진다. 늦게 들어와 이미 한참 지난 피버는
    /// 연기 없이 바로 보인다. 긴박함: 시작할 때 화면 가운데에 "피버타임!" / "시간이 없어! 마구 던져버리자!!"가 크게 튀어나왔다
    /// 사라지고, 피버 동안 화면 가장자리가 진한 분홍빛 빨강으로 숨 쉬며 남은 시간 숫자가 빨개진다. 가장자리 빛은 2:2 피버
    /// (<see cref="LabTeamFever"/>)와 같은 그림을 쓰고, 다른 전투 표시 아래에 깔린다. 화면 표시만 하며 터치를 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFeverTimeHud : MonoBehaviour
    {
        public const string DoublePath = "Fever/FeverDouble";
        // 말풍선은 막대 높이의 이만큼 높고, 가운데가 막대 오른쪽 위 모서리에서 (막대 높이 비율로) 이만큼 안쪽·위에 온다.
        private const float BubbleHeightScale = 1.3f;
        private static readonly Vector2 BubbleOffset = new Vector2(-.35f, .45f);
        // 숨쉬기: 0.9~1.1배를 1초에 두 번.
        private const float PulseAmount = .1f, PulseHz = 2f;
        // 등장: 연기가 먼저 퍼지고, 이만큼 뒤에 말풍선이 작게 시작해 살짝 넘쳤다가 제 크기로 돌아온다.
        private const float SmokeSeconds = .7f, PopDelay = .12f, PopSeconds = .3f;
        // A fever seen later than this after it began, for example after a reconnect, appears without the puff.
        private const double LateSeconds = 1d;
        // 연기 덩이: 말풍선 가운데에서의 자리(말풍선 높이 비율)와 크기(말풍선 높이 비율).
        private static readonly Vector3[] Puffs =
        {
            new Vector3(0f, 0f, 1.1f), new Vector3(-.55f, -.1f, .8f), new Vector3(.55f, -.05f, .8f),
            new Vector3(-.3f, .35f, .7f), new Vector3(.3f, .4f, .7f), new Vector3(0f, -.35f, .75f)
        };
        private static readonly Color SmokeColor = new Color(.96f, .93f, .86f, .95f);
        /// <summary>The fever's colour: the edge glow, the banner and the time left.</summary>
        public static readonly Color FeverColor = new Color(.9f, .14f, .3f);
        public const string BannerTitle = "피버타임!", BannerLine = "시간이 없어! 마구 던져버리자!!";
        // 시작 문구는 이만큼 떠 있고, 처음에 튀어나오며 끝에 흐려진다. 늦게 본 피버에는 띄우지 않는다.
        private const float BannerSeconds = 2f, BannerPopSeconds = .3f, BannerFadeSeconds = .4f;
        private const double BannerWindowSeconds = 1.5d;
        // 가장자리 빛: 이 사이를 1초에 두 번 오가며 숨 쉬고, 켜지고 꺼질 때는 이만큼에 걸쳐 바뀐다.
        private const float GlowLow = .45f, GlowHigh = .85f, GlowHz = 2f, GlowFadeSeconds = .4f;

        private LabNetwork network;
        private Canvas canvas;
        private RectTransform staminaBar;
        private RectTransform battleUi;
        private Text timer;
        private Color timerColor;
        private Image glow;
        private float glowAlpha;
        private RectTransform banner;
        private Text title, subtitle;
        private float bannerAge = float.PositiveInfinity;
        private RectTransform layer;
        private RectTransform bubble;
        private Image bubbleImage;
        private readonly Image[] puffs = new Image[Puffs.Length];
        private float age = -1f;
        private bool puffing;
        private double seenServerTime = -1d;
        private float seenAt;
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>The bubble is on screen (#334).</summary>
        public bool Showing => bubble != null && bubble.gameObject.activeInHierarchy;
        public RectTransform Bubble => bubble;
        /// <summary>The puff it appears in is still drifting.</summary>
        public bool SmokeShowing => puffs[0] != null && puffs[0].gameObject.activeInHierarchy;
        public float Scale => bubble != null ? bubble.localScale.x : 0f;
        /// <summary>The start banner is up, and what it says.</summary>
        public bool BannerShowing => banner != null && banner.gameObject.activeInHierarchy;
        public string BannerShown => title != null ? title.text + "\n" + subtitle.text : string.Empty;
        /// <summary>The edge glow's opacity now; above zero only around fever time.</summary>
        public float GlowAlpha => glow != null && glow.gameObject.activeInHierarchy ? glow.color.a : 0f;
        public Color GlowColor => glow != null ? glow.color : Color.clear;

        /// <summary>
        /// 스냅샷을 읽을 네트워크, 말풍선·문구를 그릴 Canvas, 말풍선이 붙을 스태미나 막대, 가장자리 빛을 깔 전투 화면, 빨갛게 바꿀
        /// 남은 시간 글자를 연결한다.
        /// </summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, RectTransform bar, RectTransform battle = null, Text time = null)
        {
            network = target;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            staminaBar = bar;
            battleUi = battle;
            if (timer != null) timer.color = timerColor;
            timer = time;
            if (timer != null) timerColor = timer.color;
        }

        private void LateUpdate()
        {
            if (network == null) return;
            LabSnapshot snapshot = network.Snapshot;
            Present(snapshot, snapshot != null ? ServerNow(snapshot) : 0d);
        }

        /// <summary>이 스냅샷의 이 Host 시각에 맞춰 말풍선을 보여 주거나 감춘다.</summary>
        public void Present(LabSnapshot snapshot, double now)
        {
            bool on = snapshot != null && snapshot.FeverTimeOn(now) && !snapshot.paused;
            DrawGlow(on);
            if (timer != null) timer.color = on ? FeverColor : timerColor;
            if (!on)
            {
                // A pause keeps the fever's place; the bubble comes back as it was without another puff.
                if (snapshot == null || snapshot.phase != LabPhase.Playing || !snapshot.FeverTimeOn(now)) age = -1f;
                bannerAge = float.PositiveInfinity;
                if (layer != null) layer.gameObject.SetActive(false);
                return;
            }
            if (!EnsureLayer()) return;
            layer.gameObject.SetActive(true);
            layer.SetAsLastSibling();
            if (age < 0f)
            {
                age = 0f;
                puffing = now - snapshot.feverTimeAt <= LateSeconds;
                bannerAge = now - snapshot.feverTimeAt <= BannerWindowSeconds ? 0f : float.PositiveInfinity;
            }
            else
            {
                age += Time.deltaTime;
                bannerAge += Time.deltaTime;
            }
            Vector2 center = Place();
            DrawSmoke(center);
            DrawBubble(center);
            DrawBanner();
        }

        // 가장자리 빛: 피버 동안 숨 쉬고, 끝나면 서서히 꺼진다. 다른 전투 표시 아래(전투 화면 맨 뒤)에 깔린다.
        private void DrawGlow(bool on)
        {
            if (glow == null)
            {
                if (!on || battleUi == null) return;
                var item = new GameObject("Fever Time Edge Glow", typeof(RectTransform));
                item.layer = battleUi.gameObject.layer;
                RectTransform rect = (RectTransform)item.transform;
                rect.SetParent(battleUi, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                glow = item.AddComponent<Image>();
                glow.sprite = LabTeamFever.GlowSprite();
                glow.raycastTarget = false;
                glow.color = new Color(FeverColor.r, FeverColor.g, FeverColor.b, 0f);
            }
            float breathe = GlowLow + (GlowHigh - GlowLow) * (.5f + .5f * Mathf.Sin(Time.unscaledTime * GlowHz * 2f * Mathf.PI));
            float step = Time.unscaledDeltaTime * GlowHigh / GlowFadeSeconds;
            // It fades in to its breathing range, breathes, and fades out once fever time is over.
            if (on) glowAlpha = glowAlpha < GlowLow ? Mathf.MoveTowards(glowAlpha, breathe, step) : breathe;
            else glowAlpha = Mathf.MoveTowards(glowAlpha, 0f, step);
            glow.color = new Color(FeverColor.r, FeverColor.g, FeverColor.b, glowAlpha);
            if (glow.gameObject.activeSelf != glowAlpha > 0f) glow.gameObject.SetActive(glowAlpha > 0f);
            if (glowAlpha > 0f) glow.transform.SetAsFirstSibling();
        }

        // 시작 문구: 튀어나왔다가 자리를 잡고, 끝에 흐려진다.
        private void DrawBanner()
        {
            bool shown = bannerAge < BannerSeconds;
            banner.gameObject.SetActive(shown);
            if (!shown) return;
            float pop = Mathf.Clamp01(bannerAge / BannerPopSeconds);
            float scale = pop < 1f ? Mathf.Lerp(.6f, 1.15f, pop)
                : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((bannerAge - BannerPopSeconds) / .15f));
            banner.localScale = Vector3.one * scale;
            float alpha = Mathf.Clamp01((BannerSeconds - bannerAge) / BannerFadeSeconds);
            title.color = subtitle.color = new Color(FeverColor.r, FeverColor.g, FeverColor.b, alpha);
            banner.SetAsLastSibling();
        }

        // 말풍선 가운데를 막대 오른쪽 위 모서리 근처에 둔다. 막대는 움직일 수 있으니 매번 맞춘다.
        private Vector2 Place()
        {
            float barHeight = 44f;
            Vector2 corner = layer.rect.max - new Vector2(40f, 40f);
            if (staminaBar != null)
            {
                staminaBar.GetWorldCorners(Corners);
                Vector2 min = layer.InverseTransformPoint(Corners[0]), max = layer.InverseTransformPoint(Corners[2]);
                barHeight = Mathf.Max(1f, max.y - min.y);
                corner = max;
            }
            float height = barHeight * BubbleHeightScale;
            Sprite sprite = bubbleImage.sprite;
            float aspect = sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1.47f;
            bubble.sizeDelta = new Vector2(height * aspect, height);
            Vector2 center = corner + BubbleOffset * barHeight;
            // Kept inside the screen even at the top of its swell.
            Vector2 half = bubble.sizeDelta * .5f * (1f + PulseAmount);
            Rect area = layer.rect;
            center.x = Mathf.Clamp(center.x, area.xMin + half.x, Mathf.Max(area.xMin + half.x, area.xMax - half.x));
            center.y = Mathf.Clamp(center.y, area.yMin + half.y, Mathf.Max(area.yMin + half.y, area.yMax - half.y));
            bubble.anchoredPosition = center;
            return center;
        }

        // 연기가 말풍선 자리에서 짙게 퍼졌다가 부풀며 옅어진다.
        private void DrawSmoke(Vector2 center)
        {
            bool shown = puffing && age < SmokeSeconds;
            float t = Mathf.Clamp01(age / SmokeSeconds);
            float height = bubble.sizeDelta.y;
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i].gameObject.SetActive(shown);
                if (!shown) continue;
                Vector3 puff = Puffs[i];
                float grow = Mathf.Lerp(.4f, 1.25f, Mathf.Sqrt(t));
                RectTransform rect = puffs[i].rectTransform;
                rect.anchoredPosition = center + new Vector2(puff.x * (1f + .5f * t), puff.y + .25f * t) * height;
                rect.sizeDelta = Vector2.one * (puff.z * height * grow);
                puffs[i].color = new Color(SmokeColor.r, SmokeColor.g, SmokeColor.b, SmokeColor.a * Mathf.Pow(1f - t, 1.2f));
            }
        }

        // 연기 속에서 작게 시작해 살짝 넘쳤다가 제 크기가 되고, 그 뒤로는 숨 쉬듯 커졌다 작아진다.
        private void DrawBubble(Vector2 center)
        {
            float since = puffing ? age - PopDelay : PopSeconds + age;
            bubble.gameObject.SetActive(since >= 0f);
            if (since < 0f) return;
            float scale;
            if (since < PopSeconds)
            {
                float t = since / PopSeconds;
                scale = t < .6f ? Mathf.Lerp(.2f, 1.15f, t / .6f) : Mathf.Lerp(1.15f, 1f, (t - .6f) / .4f);
            }
            else scale = 1f + PulseAmount * Mathf.Sin((since - PopSeconds) * PulseHz * 2f * Mathf.PI);
            bubble.localScale = Vector3.one * scale;
        }

        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.serverTime != seenServerTime)
            {
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }

        private bool EnsureLayer()
        {
            if (layer != null) return true;
            if (canvas == null)
            {
                Canvas found = GetComponentInParent<Canvas>();
                canvas = found != null ? found.rootCanvas : null;
            }
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null) return false;
            layer = NewRect("Fever Time", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            bubble = NewRect("Fever Double", layer);
            bubbleImage = bubble.gameObject.AddComponent<Image>();
            bubbleImage.sprite = Resources.Load<Sprite>(DoublePath);
            bubbleImage.preserveAspect = true;
            bubbleImage.raycastTarget = false;
            bubble.gameObject.SetActive(false);
            // The start banner sits above the gourd, where the other battle announcements appear.
            Font font = LabGuideText.FontOf(canvas);
            banner = NewRect("Fever Time Banner", layer);
            banner.anchorMin = banner.anchorMax = new Vector2(.5f, .62f);
            banner.sizeDelta = new Vector2(370f, 110f);
            title = Words("Title", font, 46, new Vector2(0f, 16f), BannerTitle);
            subtitle = Words("Subtitle", font, 20, new Vector2(0f, -28f), BannerLine);
            banner.gameObject.SetActive(false);
            // The puff is drawn over the bubble, so the bubble seems to come out of it as it thins.
            Sprite puffSprite = LabOrbVanishSmoke.PuffSprite();
            for (int i = 0; i < puffs.Length; i++)
            {
                RectTransform rect = NewRect("Fever Smoke " + i, layer);
                puffs[i] = rect.gameObject.AddComponent<Image>();
                puffs[i].sprite = puffSprite;
                puffs[i].raycastTarget = false;
                rect.gameObject.SetActive(false);
            }
            return true;
        }

        private Text Words(string name, Font font, int size, Vector2 offset, string words)
        {
            RectTransform rect = NewRect(name, banner);
            rect.sizeDelta = new Vector2(370f, size + 12f);
            rect.anchoredPosition = offset;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = words;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, .98f, .93f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        // Children of a full-screen layer are placed from its centre, as anchoredPosition in its local space.
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
            if (timer != null) timer.color = timerColor;
            if (layer != null) Destroy(layer.gameObject);
            if (glow != null) Destroy(glow.gameObject);
        }
    }
}
