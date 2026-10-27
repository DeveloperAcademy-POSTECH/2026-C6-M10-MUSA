using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 매혹 (#251, #305): 홀린 사람의 양옆 사람 화면. 내 화면에서 그 사람 이름표 곁에 말풍선이 떠 살랑거리고, 누를 때마다
    /// 살짝 커졌다 줄어들며 Host에 깨우기 한 번을 보낸다. 말풍선은 빈 말풍선 → 노란 게이지 → 하트 눈 얼굴 순서로 겹쳐
    /// 게이지가 차도 얼굴이 보인다. 게이지는 내가 누른 만큼만 아래부터 차오르고, 내가 먼저 다 채우면 말풍선 위에 체크가
    /// 뜬 채 다른 한 사람을 기다린다. 양옆이 모두 채워 풀리면 두 사람 화면에서 함께 놀란 얼굴의 뾰족 말풍선이 톡
    /// 튀어나왔다 사라지고, 시간이 다 되어 풀리면 말풍선이 서서히 사라진다. 홀린 사람 자신의 화면에는 양옆 이름표 곁에
    /// 그 사람이 얼마나 채웠는지가 같은 말풍선(누를 수 없음)과 체크로 실시간으로 보인다. 두 말풍선은 이름표 위에 좌우
    /// 대칭으로 뜬다 (북 미니맵은 그 위로 올려 두었다). 둘이 하는 판에서는 상대가 양쪽 이웃이라 양쪽 이름표에 다 뜨고
    /// 어느 쪽을 눌러도 된다. 말풍선 밖의 터치는 막지 않는다.
    /// 안내 (#328): 아직 다 누르지 않은 양옆 사람 화면에는 매번 말풍선 위쪽 안쪽에서 손가락이 비스듬히 말풍선을 콕콕
    /// 누른다. 박 위의 "옆 사람이 매혹에 걸렸다! 말풍선을 빠르게 눌러서 풀어주자!"는 이 플레이어가 처음 옆 사람을 풀어 줄
    /// 때 한 번만 뜬다. 내가 다 눌러 체크가 뜨면 둘 다 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabCharmBubbles : MonoBehaviour
    {
        public const string LeftBubblePath = "Sabotage/CharmBubbleLeft";
        public const string RightBubblePath = "Sabotage/CharmBubbleRight";
        public const string WokenPath = "Sabotage/CharmWoken";
        public const string FacePath = "Sabotage/CharmFace";
        public const string CheckPath = "Sabotage/CharmCheck";
        public const string LeftHandPath = "Sabotage/CharmHandLeft";
        public const string RightHandPath = "Sabotage/CharmHandRight";
        public const string HelperGuideText = "옆 사람이 매혹에 걸렸다!\n말풍선을 빠르게 눌러서 풀어주자!";
        /// <summary>The helper guide shows once per player (#328), under this name.</summary>
        public const string HelperGuideOnceKey = "charm-helper";
        // 손가락 (#328): 말풍선 안쪽 위에서 살짝 기울어 말풍선을 가리키고, 한 번에 두 번씩 콕콕 누른다
        // (말풍선 크기 비율·도·초·픽셀). 너비는 처음 0.9에서 0.75배로 줄였다.
        private const float HandWidthScale = .675f, HandOverlap = .12f, HandTiltDegrees = 20f;
        private const float PokeCycleSeconds = .9f, PokeSeconds = .2f, PokeGap = .27f, PokePixels = 9f;
        // 하트 눈 얼굴이 빈 말풍선 안에 앉는 자리 (말풍선 크기 비율, 아래가 0). 오른쪽 말풍선은 좌우 반전 그림이다.
        private static readonly Vector2 LeftFaceMin = new Vector2(.267f, .216f), LeftFaceMax = new Vector2(.736f, .683f);
        private static readonly Vector2 RightFaceMin = new Vector2(.264f, .216f), RightFaceMax = new Vector2(.733f, .683f);
        // 체크는 구름 위쪽 가운데에서 붉은 표시의 반대쪽으로 조금 비켜 살짝 걸친다 (말풍선 크기 비율, 가운데 기준).
        private static readonly Vector2 CheckOffset = new Vector2(.12f, .42f);
        private const float CheckScale = .38f, CheckPopSeconds = .25f;
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
            public Image FaceImage;
            public RectTransform Check;
            public Image CheckImage;
            public RectTransform Hand;
            public Image HandImage;
            public float CheckAge = -1f;
            /// <summary>The charmed player's own screen: this side shows how far the player beside has tapped (#305).</summary>
            public bool OwnView;
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
        // This screen's own taps on the current charm (#305): shown at once, before the Host's count catches up.
        private int localTaps;
        private double localTapsCharmAt = double.NaN;
        private LabGuideText guide;
        // This screen still has to tap a bubble beside it (#328): the hand shows, and the guide on its one charm.
        private bool helperWaiting;
        // The charm (session, round, start) on which this player's one helper guide shows; null before it has.
        private string guideCharm;

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
        /// <summary>The check above that side's bubble: that player beside has tapped enough and waits for the other (#305).</summary>
        public bool LeftCheckShowing => Visible(left.Check);
        public bool RightCheckShowing => Visible(right.Check);
        /// <summary>That side shows the charmed player's own screen: the progress of the player beside, not to be tapped.</summary>
        public bool OwnView(bool rightSide) => (rightSide ? right : left).OwnView;
        /// <summary>The poking hand beside that side's bubble (#328).</summary>
        public bool LeftHandShowing => Visible(left.Hand);
        public bool RightHandShowing => Visible(right.Hand);
        public RectTransform LeftHand => left.Hand;
        public RectTransform RightHand => right.Hand;
        /// <summary>The guide above the gourd telling the players beside to tap (#328).</summary>
        public bool GuideShowing => guide != null && guide.Showing;
        public string GuideShown => guide != null ? guide.Shown : string.Empty;
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
                guide?.Draw(false, null);
                return;
            }
            if (!EnsureLayer()) return;
            if (!snapshot.charmStartedAt.Equals(localTapsCharmAt))
            {
                localTapsCharmAt = snapshot.charmStartedAt;
                localTaps = 0;
            }
            CharmedSides(snapshot, localId, now, out bool onLeft, out bool onRight, out bool ownView,
                out ulong leftHelper, out ulong rightHelper);
            bobPhase = Mathf.Repeat(bobPhase + Time.deltaTime * BobHz, 1f);
            helperWaiting = false;
            Step(left, onLeft, snapshot.charmedPlayer, ownView, snapshot.CharmTapsBy(leftHelper), rescueTaps);
            Step(right, onRight, snapshot.charmedPlayer, ownView, snapshot.CharmTapsBy(rightHelper), rescueTaps);
            // The guide is for a first time (#328): this player's first charm beside someone, until they finish tapping.
            string charm = snapshot.sessionId + "/" + snapshot.round + "/" + snapshot.charmStartedAt.ToString("R");
            if (helperWaiting && guideCharm == null && LabGuideText.TryShowOnce(HelperGuideOnceKey)) guideCharm = charm;
            guide.Draw(helperWaiting && guideCharm == charm, HelperGuideText);
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

        /// <summary>
        /// Which badges show a bubble, and whose taps each one shows: this screen's own beside the charmed player, or,
        /// on the charmed player's own screen, each player beside them (#305).
        /// </summary>
        private static void CharmedSides(LabSnapshot snapshot, ulong localId, double now, out bool onLeft, out bool onRight,
            out bool ownView, out ulong leftHelper, out ulong rightHelper)
        {
            onLeft = onRight = ownView = false;
            leftHelper = rightHelper = localId;
            LabPlayerState[] players = snapshot.players;
            if (players == null || players.Length < 2 || !(snapshot.charmUntil > now && now >= snapshot.charmStartedAt)) return;
            LabPlayerState me = Array.Find(players, player => player != null && player.id == localId);
            LabPlayerState charmed = Array.Find(players, player => player != null && player.id == snapshot.charmedPlayer);
            if (me == null || charmed == null) return;
            int count = players.Length;
            if (me.seat < 0 || me.seat >= count || charmed.seat < 0 || charmed.seat >= count) return;
            // Preserve the separate team-seat presentation; story rooms use the same
            // clockwise neighbours as the lobby, HUD and actual Host handoff.
            bool team = LabTeams.FourPlayerTeams(snapshot);
            int leftSeat = team ? (me.seat + count - 1) % count : LabPlayerSlots.NeighborSeat(me.seat, count, false);
            int rightSeat = team ? (me.seat + 1) % count : LabPlayerSlots.NeighborSeat(me.seat, count, true);
            if (charmed == me)
            {
                LabPlayerState before = Array.Find(players, player => player != null && player.seat == leftSeat);
                LabPlayerState after = Array.Find(players, player => player != null && player.seat == rightSeat);
                ownView = true;
                onLeft = before != null;
                onRight = after != null;
                if (before != null) leftHelper = before.id;
                if (after != null) rightHelper = after.id;
                return;
            }
            onLeft = charmed.seat == leftSeat;
            onRight = charmed.seat == rightSeat;
        }

        private void Step(Side side, bool charmedHere, ulong charmed, bool ownView, int hostTaps, int rescueTaps)
        {
            if (charmedHere && (!side.Showing || side.Player != charmed || side.OwnView != ownView))
            {
                side.Showing = true;
                side.Player = charmed;
                side.OwnView = ownView;
                side.WokenEnd = false;
                side.PulseAge = side.ReleaseAge = side.CheckAge = -1f;
                side.Taps = 0;
                side.FillShown = 0f;
            }
            else if (!charmedHere && side.Showing)
            {
                // The charm ended: woken by the players beside, or run out by itself. The charmed player's own
                // progress bubbles only fade; "정신 차림!" tells them.
                side.Showing = false;
                side.ReleaseAge = 0f;
                side.WokenEnd = !side.OwnView && side.Player == wokenPlayer && Time.time - wokenAt <= WokenWindowSeconds;
                side.CheckAge = -1f;
                side.Check.gameObject.SetActive(false);
            }
            bool tappable = rescueTaps > 0 && !side.OwnView;
            bool badge = side.Badge != null && side.Badge.gameObject.activeInHierarchy && side.BubbleImage.sprite != null;
            if (side.Showing && badge)
            {
                Place(side);
                side.Bubble.gameObject.SetActive(true);
                side.Woken.gameObject.SetActive(false);
                side.BubbleImage.color = side.FaceImage.color = Color.white;
                // Each player beside fills their own gauge (#305): the Host's count, and this screen's own taps at once.
                side.Taps = side.OwnView ? hostTaps : Mathf.Max(localTaps, hostTaps);
                bool done = rescueTaps > 0 && side.Taps >= rescueTaps;
                side.BubbleImage.raycastTarget = tappable && !done;
                side.FillTarget = rescueTaps > 0 ? Mathf.Clamp01(side.Taps / (float)rescueTaps) : 0f;
                side.FillShown = Mathf.MoveTowards(side.FillShown, side.FillTarget, FillRisePerSecond * Time.deltaTime);
                side.Fill.gameObject.SetActive(rescueTaps > 0 && side.FillShown > 0f);
                DrawCheck(side, done);
                DrawHand(side, tappable && !done);
                helperWaiting |= tappable && !done;
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
            DrawHand(side, false);
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
                    side.BubbleImage.color = side.FaceImage.color = new Color(1f, 1f, 1f, 1f - age / FadeSeconds);
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
            if (!side.Showing || side.OwnView) return;
            side.PulseAge = 0f;
            localTaps++;
            TapsSent++;
            if (network != null) network.TapCharm();
        }

        // 내가 다 채웠다 (#305): 말풍선 위에 체크가 톡 튀어나와 다른 한 사람을 기다린다.
        private void DrawCheck(Side side, bool done)
        {
            bool shown = done && side.Check != null && side.CheckImage.sprite != null;
            if (!shown)
            {
                side.CheckAge = -1f;
                if (side.Check != null) side.Check.gameObject.SetActive(false);
                return;
            }
            if (side.CheckAge < 0f) side.CheckAge = 0f;
            else side.CheckAge += Time.deltaTime;
            side.Check.gameObject.SetActive(true);
            float size = bubbleSize * CheckScale, half = size * .5f;
            var offset = new Vector2((side.Right ? -CheckOffset.x : CheckOffset.x) * bubbleSize, CheckOffset.y * bubbleSize);
            side.Check.anchoredPosition = KeepOnScreen(side.BubbleCenter + offset, new Vector2(-half, -half), new Vector2(half, half));
            float t = Mathf.Clamp01(side.CheckAge / CheckPopSeconds);
            float pop = t < .6f ? Mathf.Lerp(.3f, 1.15f, t / .6f) : Mathf.Lerp(1.15f, 1f, (t - .6f) / .4f);
            side.Check.localScale = Vector3.one * pop;
        }

        // 손가락 (#328): 말풍선 안쪽 위에서 살짝 기울어 말풍선을 가리키며 콕콕 두 번씩 누른다. 말풍선과 함께 살랑거린다.
        private void DrawHand(Side side, bool shown)
        {
            shown &= side.Hand != null && side.HandImage.sprite != null;
            if (side.Hand == null) return;
            side.Hand.gameObject.SetActive(shown);
            if (!shown) return;
            Vector2 size = side.Hand.sizeDelta;
            // It points a little downward at the bubble from its inner side: down and right toward a bubble on the right.
            float tilt = HandTiltDegrees * Mathf.Deg2Rad;
            var pointing = new Vector2((side.Right ? 1f : -1f) * Mathf.Cos(tilt), -Mathf.Sin(tilt));
            float t = Mathf.Repeat(Time.time / PokeCycleSeconds, 1f);
            float poke = Poke(t, 0f) + Poke(t, PokeGap);
            float reach = bubbleSize * .5f + size.x * (.5f - HandOverlap);
            Vector2 center = KeepOnScreen(side.BubbleCenter - pointing * reach, -size * .5f, size * .5f);
            side.Hand.anchoredPosition = center + pointing * (PokePixels * poke);
        }

        private static float Poke(float t, float start)
        {
            float u = (t - start) / PokeSeconds;
            return u > 0f && u < 1f ? Mathf.Sin(u * Mathf.PI) : 0f;
        }

        private void Reset(Side side)
        {
            if (side.Hand != null) side.Hand.gameObject.SetActive(false);
            side.Showing = side.WokenEnd = false;
            side.PulseAge = side.ReleaseAge = -1f;
            if (side.Bubble != null) side.Bubble.gameObject.SetActive(false);
            if (side.Woken != null) side.Woken.gameObject.SetActive(false);
            side.CheckAge = -1f;
            if (side.Check != null) side.Check.gameObject.SetActive(false);
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
            guide = LabGuideText.Create(layer, LabGuideText.FontOf(canvas), "Charm Helper Guide");
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
            // The heart-eyed face sits over the fill (#305), so the gauge never hides it.
            RectTransform face = NewRect("Charm Face", side.Bubble);
            face.anchorMin = side.Right ? RightFaceMin : LeftFaceMin;
            face.anchorMax = side.Right ? RightFaceMax : LeftFaceMax;
            face.offsetMin = face.offsetMax = Vector2.zero;
            side.FaceImage = face.gameObject.AddComponent<Image>();
            side.FaceImage.sprite = Resources.Load<Sprite>(FacePath);
            side.FaceImage.preserveAspect = true;
            side.FaceImage.raycastTarget = false;
            side.Bubble.gameObject.SetActive(false);

            // The check stands just above the cloud, so it is not inside the bubble's mask.
            side.Check = NewRect(side.Right ? "Charm Check Right" : "Charm Check Left", layer);
            side.Check.sizeDelta = new Vector2(bubbleSize * CheckScale, bubbleSize * CheckScale);
            side.CheckImage = side.Check.gameObject.AddComponent<Image>();
            side.CheckImage.sprite = Resources.Load<Sprite>(CheckPath);
            side.CheckImage.preserveAspect = true;
            side.CheckImage.raycastTarget = false;
            side.Check.gameObject.SetActive(false);

            side.Woken = NewRect(side.Right ? "Charm Woken Right" : "Charm Woken Left", layer);
            side.Woken.sizeDelta = new Vector2(bubbleSize * WokenScale, bubbleSize * WokenScale);
            side.WokenImage = side.Woken.gameObject.AddComponent<Image>();
            side.WokenImage.sprite = woken;
            side.WokenImage.preserveAspect = true;
            side.WokenImage.raycastTarget = false;
            side.Woken.gameObject.SetActive(false);

            // The hand points at the bubble from its inner side (#328): left-pointing beside the left bubble.
            Sprite hand = Resources.Load<Sprite>(side.Right ? RightHandPath : LeftHandPath);
            side.Hand = NewRect(side.Right ? "Charm Hand Right" : "Charm Hand Left", layer);
            float handWidth = bubbleSize * HandWidthScale;
            float aspect = hand != null && hand.rect.width > 0f ? hand.rect.height / hand.rect.width : .5f;
            side.Hand.sizeDelta = new Vector2(handWidth, handWidth * aspect);
            side.HandImage = side.Hand.gameObject.AddComponent<Image>();
            side.HandImage.sprite = hand;
            side.HandImage.preserveAspect = true;
            side.HandImage.raycastTarget = false;
            // A left-pointing hand turns counterclockwise to point down-left; the right one mirrors it.
            side.Hand.localRotation = Quaternion.Euler(0f, 0f, side.Right ? -HandTiltDegrees : HandTiltDegrees);
            side.Hand.gameObject.SetActive(false);
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
