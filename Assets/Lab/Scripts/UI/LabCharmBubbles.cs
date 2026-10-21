using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 매혹 (#251): 홀린 사람의 양옆 사람 화면. 내 화면에서 그 사람 이름표 곁에 하트 눈 말풍선이 떠 살랑거리고,
    /// 누를 때마다 살짝 커졌다 줄어들며 Host에 깨우기 한 번을 보낸다. 말풍선 안쪽은 양옆 사람이 함께 누른 만큼 연한 노란색이
    /// 아래부터 차오르고, 다 차면 풀린다. 옆 사람들이 깨워서 풀리면 말풍선 자리에 놀란 얼굴의
    /// 뾰족 말풍선이 톡 튀어나왔다 사라지고, 시간이 다 되어 풀리면 하트 말풍선이 서서히 사라진다. 두 말풍선은 이름표 위에
    /// 좌우 대칭으로 뜬다 (북 미니맵은 그 위로 올려 두었다). 둘이 하는 판에서는 상대가 양쪽 이웃이라 양쪽 이름표에 다 뜨고
    /// 어느 쪽을 눌러도 된다. 말풍선 밖의 터치는 막지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabCharmBubbles : MonoBehaviour
    {
        public const string LeftBubblePath = "Sabotage/CharmBubbleLeft";
        public const string RightBubblePath = "Sabotage/CharmBubbleRight";
        public const string WokenPath = "Sabotage/CharmWoken";
        // 말풍선 그림에서 꼬리 끝의 자리 (그림 크기 비율, 아래가 0).
        private static readonly Vector2 LeftTail = new Vector2(.36f, .08f);
        private static readonly Vector2 RightTail = new Vector2(.64f, .08f);
        private const float PulseSeconds = .15f, PulseScale = 1.2f;
        private const float FadeSeconds = .6f, WokenSeconds = .9f;
        private const float BobPixels = 3f, BobHz = 1.2f;
        // A woken player's spiky bubble is a little wider than the heart bubble it replaces.
        private const float WokenScale = 1.15f;
        // How long after a woken notice a charm ending on that player still counts as woken.
        private const float WokenWindowSeconds = 2f;
        // A bubble is kept this far inside the screen's edges, so it is never cut off (bob and swell included).
        private const float ScreenMargin = 6f;
        // The wake-up fill: a light yellow over the bubble, rising from the bottom of its cloud to the top.
        private static readonly Color WakeFillColor = new Color(1f, .9f, .35f, .6f);
        private const float FillBottom = .12f, FillTop = .9f, FillRisePerSecond = 4f;
        private static readonly Vector3[] Corners = new Vector3[4];

        [Tooltip("말풍선 크기 (Canvas 기준 픽셀)")]
        [SerializeField, Min(10f)] private float bubbleSize = 95f;

        private sealed class Side
        {
            public bool Right;
            public RectTransform Badge;
            public RectTransform Bubble;
            public Image BubbleImage;
            public RectTransform Fill;
            public Image FillImage;
            /// <summary>Wake-up taps this side shows: the Host's count, or one more just after this screen tapped.</summary>
            public int Taps;
            public float FillTarget, FillShown;
            public RectTransform Woken;
            public Image WokenImage;
            /// <summary>The charmed player this side shows, while showing or ending.</summary>
            public ulong Player;
            public bool Showing;
            public bool WokenEnd;
            public float PulseAge = -1f;
            public float ReleaseAge = -1f;
            public Vector2 BubbleCenter;
        }

        private readonly Side left = new Side();
        private readonly Side right = new Side { Right = true };
        private LabNetwork network;
        private Canvas canvas;
        private RectTransform layer;
        private float bobPhase;
        private double seenServerTime = -1d;
        private float seenAt;
        private ulong wokenPlayer;
        private float wokenAt = float.NegativeInfinity;

        /// <summary>A charm is on the player beside this screen's player on that side.</summary>
        public bool LeftShowing => left.Showing && Visible(left.Bubble);
        public bool RightShowing => right.Showing && Visible(right.Bubble);
        /// <summary>The spiky surprised bubble is out on that side: the players beside woke the charmed one.</summary>
        public bool LeftWokenShowing => Visible(left.Woken);
        public bool RightWokenShowing => Visible(right.Woken);
        /// <summary>The heart bubble on that side is fading away: the charm ran out by itself.</summary>
        public bool LeftFading => !left.Showing && !left.WokenEnd && left.ReleaseAge >= 0f && Visible(left.Bubble);
        public bool RightFading => !right.Showing && !right.WokenEnd && right.ReleaseAge >= 0f && Visible(right.Bubble);
        public RectTransform LeftBubble => left.Bubble;
        public RectTransform RightBubble => right.Bubble;
        public bool Pulsing(bool rightSide) => (rightSide ? right : left).PulseAge >= 0f;
        /// <summary>How full the wake-up fill is heading on that side, 0 to 1.</summary>
        public float LeftWakeFill => left.FillTarget;
        public float RightWakeFill => right.FillTarget;
        /// <summary>Wake-up taps this screen sent.</summary>
        public int TapsSent { get; private set; }

        /// <summary>스냅샷을 읽을 네트워크, 말풍선을 그릴 Canvas, 내 화면의 왼쪽·오른쪽 이웃 이름표를 연결한다.</summary>
        public void Bind(LabNetwork target, Canvas uiCanvas, GameObject leftBadge, GameObject rightBadge)
        {
            if (network != null) network.CharmWoken -= ShowWoken;
            network = target;
            if (network != null) network.CharmWoken += ShowWoken;
            canvas = uiCanvas != null ? uiCanvas.rootCanvas : null;
            left.Badge = leftBadge != null ? leftBadge.GetComponent<RectTransform>() : null;
            right.Badge = rightBadge != null ? rightBadge.GetComponent<RectTransform>() : null;
        }

        private void LateUpdate()
        {
            if (network == null) return;
            LabSnapshot snapshot = network.Snapshot;
            Present(snapshot, network.LocalPlayerId, snapshot != null ? ServerNow(snapshot) : 0d,
                network.config != null ? network.config.CharmRescueTaps : 0);
        }

        /// <summary>
        /// 이 스냅샷을 이 사람 화면에서 보여 준다. 매혹된 사람이 내 왼쪽·오른쪽 이웃이면 그쪽 말풍선을 띄운다.
        /// rescueTaps는 깨우는 데 드는 탭 수이고, 0이면 (깨우기 꺼짐) 말풍선은 보이기만 하고 눌리지 않는다.
        /// </summary>
        public void Present(LabSnapshot snapshot, ulong localId, double now, int rescueTaps)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.paused)
            {
                Reset(left);
                Reset(right);
                return;
            }
            if (!EnsureLayer()) return;
            CharmedSides(snapshot, localId, now, out bool onLeft, out bool onRight);
            bobPhase = Mathf.Repeat(bobPhase + Time.deltaTime * BobHz, 1f);
            Step(left, onLeft, snapshot.charmedPlayer, snapshot.charmTaps, rescueTaps);
            Step(right, onRight, snapshot.charmedPlayer, snapshot.charmTaps, rescueTaps);
            layer.SetAsLastSibling();
        }

        /// <summary>
        /// The players beside woke this player (#251): a charm ending on them now shows the surprised bubble. The notice
        /// may come just before or just after the snapshot that ends the charm; either way the side switches to it.
        /// </summary>
        public void ShowWoken(ulong player)
        {
            wokenPlayer = player;
            wokenAt = Time.time;
            foreach (Side side in new[] { left, right })
                if (!side.Showing && !side.WokenEnd && side.ReleaseAge >= 0f && side.Player == player)
                {
                    side.WokenEnd = true;
                    side.ReleaseAge = 0f;
                }
        }

        private static void CharmedSides(LabSnapshot snapshot, ulong localId, double now, out bool onLeft, out bool onRight)
        {
            onLeft = onRight = false;
            LabPlayerState[] players = snapshot.players;
            if (players == null || players.Length < 2 || snapshot.charmedPlayer == localId
                || !(snapshot.charmUntil > now && now >= snapshot.charmStartedAt)) return;
            LabPlayerState me = Array.Find(players, player => player != null && player.id == localId);
            LabPlayerState charmed = Array.Find(players, player => player != null && player.id == snapshot.charmedPlayer);
            if (me == null || charmed == null) return;
            int count = players.Length;
            onLeft = charmed.seat == (me.seat + count - 1) % count;
            onRight = charmed.seat == (me.seat + 1) % count;
        }

        private void Step(Side side, bool charmedHere, ulong charmed, int taps, int rescueTaps)
        {
            bool tappable = rescueTaps > 0;
            if (charmedHere && !side.Showing)
            {
                side.Showing = true;
                side.Player = charmed;
                side.WokenEnd = false;
                side.PulseAge = side.ReleaseAge = -1f;
                side.Taps = 0;
                side.FillShown = 0f;
            }
            else if (!charmedHere && side.Showing)
            {
                // The charm ended: woken by the players beside, or run out by itself.
                side.Showing = false;
                side.ReleaseAge = 0f;
                side.WokenEnd = side.Player == wokenPlayer && Time.time - wokenAt <= WokenWindowSeconds;
            }
            bool badge = side.Badge != null && side.Badge.gameObject.activeInHierarchy && side.BubbleImage.sprite != null;
            if (side.Showing && badge)
            {
                Place(side);
                side.Bubble.gameObject.SetActive(true);
                side.Woken.gameObject.SetActive(false);
                side.BubbleImage.raycastTarget = tappable;
                side.BubbleImage.color = Color.white;
                // Both neighbours' taps count together (the Host's count); this screen's own tap shows at once.
                side.Taps = Mathf.Max(side.Taps, taps);
                side.FillTarget = tappable ? Mathf.Clamp01(side.Taps / (float)rescueTaps) : 0f;
                side.FillShown = Mathf.MoveTowards(side.FillShown, side.FillTarget, FillRisePerSecond * Time.deltaTime);
                side.Fill.gameObject.SetActive(tappable && side.FillShown > 0f);
                side.Fill.anchorMax = new Vector2(1f, Mathf.Lerp(FillBottom, FillTop, side.FillShown));
                side.FillImage.color = WakeFillColor;
                float pulse = 1f;
                if (side.PulseAge >= 0f)
                {
                    side.PulseAge += Time.deltaTime;
                    if (side.PulseAge >= PulseSeconds) side.PulseAge = -1f;
                    else pulse = 1f + (PulseScale - 1f) * Mathf.Sin(side.PulseAge / PulseSeconds * Mathf.PI);
                }
                side.Bubble.localScale = Vector3.one * pulse;
                return;
            }
            if (side.ReleaseAge >= 0f && badge)
            {
                side.ReleaseAge += Time.deltaTime;
                float age = side.ReleaseAge;
                side.BubbleImage.raycastTarget = false;
                if (side.WokenEnd && age < WokenSeconds && side.WokenImage.sprite != null)
                {
                    // 깨웠다: 하트 말풍선 자리에 놀란 얼굴의 뾰족 말풍선이 톡 튀어나왔다가 끝에 흐려진다.
                    side.Bubble.gameObject.SetActive(false);
                    side.Woken.gameObject.SetActive(true);
                    float half = bubbleSize * WokenScale * .5f * 1.15f;
                    side.Woken.anchoredPosition = KeepOnScreen(side.BubbleCenter, new Vector2(-half, -half), new Vector2(half, half));
                    float pop = age < .15f ? Mathf.Lerp(.3f, 1.15f, age / .15f)
                        : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - .15f) / .1f));
                    side.Woken.localScale = Vector3.one * pop;
                    side.WokenImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01((WokenSeconds - age) / .25f));
                    return;
                }
                if (!side.WokenEnd && age < FadeSeconds)
                {
                    // 시간이 다 되어 풀렸다: 하트 말풍선이 제자리에서 서서히 사라진다.
                    side.Bubble.gameObject.SetActive(true);
                    side.Woken.gameObject.SetActive(false);
                    side.Bubble.localScale = Vector3.one;
                    side.BubbleImage.color = new Color(1f, 1f, 1f, 1f - age / FadeSeconds);
                    side.FillImage.color = new Color(WakeFillColor.r, WakeFillColor.g, WakeFillColor.b,
                        WakeFillColor.a * (1f - age / FadeSeconds));
                    return;
                }
                side.ReleaseAge = -1f;
            }
            side.Bubble.gameObject.SetActive(false);
            side.Woken.gameObject.SetActive(false);
        }

        // 두 말풍선 모두 이름표 위에, 꼬리가 이름표 쪽 안쪽을 가리키도록 좌우 대칭으로 둔다.
        private void Place(Side side)
        {
            side.Badge.GetWorldCorners(Corners);
            Vector2 min = layer.InverseTransformPoint(Corners[0]), max = layer.InverseTransformPoint(Corners[2]);
            Vector2 center = (min + max) * .5f, size = max - min;
            Vector2 tail = side.Right ? RightTail : LeftTail;
            Vector2 point = new Vector2(center.x + (side.Right ? -.15f : .15f) * size.x, center.y + size.y * .4f);
            point = KeepOnScreen(point, -tail * bubbleSize, (Vector2.one - tail) * bubbleSize);
            float bob = Mathf.Sin(bobPhase * 2f * Mathf.PI) * BobPixels;
            side.Bubble.anchoredPosition = point + new Vector2(0f, bob);
            side.BubbleCenter = point + new Vector2((.5f - tail.x) * bubbleSize, (.5f - tail.y) * bubbleSize);
        }

        /// <summary>
        /// Moves a point in just far enough that the box from point + below to point + above stays inside the screen.
        /// The swell of a tap grows the bubble about its tail, so a little room is kept for it too.
        /// </summary>
        private Vector2 KeepOnScreen(Vector2 point, Vector2 below, Vector2 above)
        {
            Rect area = layer.rect;
            float room = ScreenMargin + BobPixels;
            float swell = (PulseScale - 1f) * .5f;
            below *= 1f + swell;
            above *= 1f + swell;
            float xMin = area.xMin + room - below.x, xMax = area.xMax - room - above.x;
            float yMin = area.yMin + room - below.y, yMax = area.yMax - room - above.y;
            return new Vector2(Mathf.Clamp(point.x, xMin, Mathf.Max(xMin, xMax)), Mathf.Clamp(point.y, yMin, Mathf.Max(yMin, yMax)));
        }

        private void OnTap(Side side)
        {
            if (!side.Showing) return;
            side.PulseAge = 0f;
            side.Taps++;
            TapsSent++;
            if (network != null) network.TapCharm();
        }

        private void Reset(Side side)
        {
            side.Showing = side.WokenEnd = false;
            side.PulseAge = side.ReleaseAge = -1f;
            if (side.Bubble != null) side.Bubble.gameObject.SetActive(false);
            if (side.Woken != null) side.Woken.gameObject.SetActive(false);
        }

        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.serverTime != seenServerTime)
            {
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.serverTime + Math.Min(1d, realtime - seenAt);
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
            layer = NewRect("Charm Bubbles", canvasRect);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            Build(left, Resources.Load<Sprite>(LeftBubblePath), LeftTail);
            Build(right, Resources.Load<Sprite>(RightBubblePath), RightTail);
            return true;
        }

        private void Build(Side side, Sprite bubble, Vector2 tail)
        {
            Sprite woken = Resources.Load<Sprite>(WokenPath);
            side.Bubble = NewRect(side.Right ? "Charm Bubble Right" : "Charm Bubble Left", layer);
            side.Bubble.pivot = tail;
            side.Bubble.sizeDelta = new Vector2(bubbleSize, bubbleSize);
            side.BubbleImage = side.Bubble.gameObject.AddComponent<Image>();
            side.BubbleImage.sprite = bubble;
            side.BubbleImage.preserveAspect = true;
            var button = side.Bubble.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = side.BubbleImage;
            button.onClick.AddListener(() => OnTap(side));
            // The wake-up fill stays inside the bubble's drawn shape.
            side.Bubble.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            side.Fill = NewRect("Charm Wake Fill", side.Bubble);
            side.Fill.anchorMin = Vector2.zero;
            side.Fill.anchorMax = new Vector2(1f, FillBottom);
            side.Fill.offsetMin = side.Fill.offsetMax = Vector2.zero;
            side.FillImage = side.Fill.gameObject.AddComponent<Image>();
            side.FillImage.color = WakeFillColor;
            side.FillImage.raycastTarget = false;
            side.Fill.gameObject.SetActive(false);
            side.Bubble.gameObject.SetActive(false);

            side.Woken = NewRect(side.Right ? "Charm Woken Right" : "Charm Woken Left", layer);
            side.Woken.sizeDelta = new Vector2(bubbleSize * WokenScale, bubbleSize * WokenScale);
            side.WokenImage = side.Woken.gameObject.AddComponent<Image>();
            side.WokenImage.sprite = woken;
            side.WokenImage.preserveAspect = true;
            side.WokenImage.raycastTarget = false;
            side.Woken.gameObject.SetActive(false);
        }

        private static bool Visible(RectTransform rect) => rect != null && rect.gameObject.activeInHierarchy;

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private void OnDestroy()
        {
            if (network != null) network.CharmWoken -= ShowWoken;
            if (layer != null) Destroy(layer.gameObject);
        }
    }
}
