using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 북 넘기기 안내 (#718). 베타테스트에서 북을 넘겨야 하는 줄 모르고 가만히 있던 플레이어를 위해, 북이 내 판에 온 뒤
    /// hintAfterSeconds(기본 8초) 동안 넘기지 않으면 튜토리얼 4·6단계와 같은 손과 초록 화살표로 북을 옆으로 미는 모습을 보여 준다.
    /// - 북을 두드려 구슬을 만들어도 시간은 계속 센다. 북이 내 판을 떠나야(넘겨야) 그 북의 시간이 없어지고, 북이 다시 오면 처음부터 센다.
    ///   일시정지·시작 전·전투가 끝난 뒤에는 세지 않는다. 북이 여러 개면 가장 오래 있던 북을 가리킨다.
    /// - 넘길 수 있는 쪽만 가리킨다: 양쪽이 열려 있으면 손이 한 번 밀 때마다 오른쪽·왼쪽을 번갈아, 방해 기믹으로 한쪽이 막혔거나
    ///   땅따먹기처럼 한쪽으로만 넘기면 열린 쪽만. 넘길 곳이 없으면(땅따먹기 1:1) 띄우지 않는다.
    /// - 개발용 솔로에서도 확인할 수 있게 띄운다: 옆 사람은 없지만 양쪽이 열린 것처럼 번갈아 가리킨다(실제로 넘어가지는 않는다).
    /// - 매혹으로 판이 멈췄을 때와 손가락으로 판 위의 구슬·북을 잡고 있는 동안은 숨긴다(놓았는데 북이 아직 있으면 다시 보인다).
    /// - 글자 없이 손과 화살표만. 내 화면에만 보이는 표시라 Host 규칙·DEV 값·접속 버전과 무관하고, 터치를 막지 않는다.
    /// 연습판(튜토리얼)에는 붙지 않는다(자체 안내가 있다). 그림과 크기는 튜토리얼 그림(GameTutorialArt)을 그대로 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDrumPassHint : MonoBehaviour
    {
        /// <summary>전투 HUD(1~3) 위, 시작·끝 문구와 화면 전환 덮개(5000 이상) 아래.</summary>
        public const int OverlaySortingOrder = 50;
        public const int RightSide = 1, LeftSide = 2;

        [Tooltip("북이 내 판에 온 뒤 이 시간(초) 동안 넘기지 않으면 안내를 띄운다. 북을 두드려도 시간은 계속 센다.")]
        [SerializeField, Min(.5f)] private float hintAfterSeconds = 8f;

        private GameSession session;
        private LabOrbBoard board;
        private LabHud hud;
        private GameTutorialArt art;
        private Canvas overlayCanvas;
        private RectTransform overlay;
        private Image hand;
        private GameTutorialArrow arrow;
        private readonly Dictionary<string, float> heldSeconds = new Dictionary<string, float>();
        private readonly List<string> gone = new List<string>();
        private float shownFor = -1f;

        /// <summary>안내가 보이는지</summary>
        public bool Visible => hand != null && hand.gameObject.activeSelf;
        /// <summary>지금 가리키는 쪽 (1 오른쪽, -1 왼쪽, 0 안 보임)</summary>
        public int PointingSide { get; private set; }
        /// <summary>지금 가리키는 북</summary>
        public string PointedDrumId { get; private set; }
        public float HintAfterSeconds => hintAfterSeconds;

        public void Bind(GameBattleBindings bindings, GameSession owner)
        {
            session = owner;
            board = bindings != null ? bindings.board : null;
            hud = bindings != null ? bindings.hud : null;
            art = Resources.Load<GameTutorialArt>(GameTutorialArt.ResourcePath);
            if (art == null || art.hand == null || board == null) { enabled = false; return; }
            BuildOverlay();
        }

        private void LateUpdate()
        {
            LabNetwork network = session != null ? session.Network : null;
            if (network == null) { Present(null, 0UL, 0f, false); return; }
            LabSnapshot snapshot = network.Snapshot;
            bool live = snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused && !network.GameStartHeld;
            Present(snapshot, network.LocalPlayerId, Time.unscaledDeltaTime, live, network.IsDeveloperSolo);
        }

        /// <summary>
        /// 그 스냅샷으로 북 시간을 deltaTime만큼 세고 안내를 그린다 (검사에서 직접 부를 수 있게 공개한다). live가 아니면 세지 않고 숨긴다.
        /// solo는 개발용 솔로: 옆 사람이 없어도 양쪽으로 가리킨다.
        /// </summary>
        public void Present(LabSnapshot snapshot, ulong localPlayer, float deltaTime, bool live, bool solo = false)
        {
            Track(heldSeconds, gone, snapshot, localPlayer, live ? deltaTime : 0f);
            string drum = Longest(heldSeconds, out float seconds);
            int open = live ? OpenSides(snapshot, localPlayer, solo) : 0;
            bool show = open != 0 && drum != null && seconds >= hintAfterSeconds && board != null && !board.IsHolding
                && TryDrumCircle(drum, out Vector2 center, out float radius);
            if (!show)
            {
                Hide();
                return;
            }
            shownFor = shownFor < 0f ? 0f : shownFor + Mathf.Max(0f, deltaTime);
            TryDrumCircle(drum, out center, out radius);
            int side = SideFor(open, shownFor, art.handDragSeconds);
            PointingSide = side;
            PointedDrumId = drum;
            Place(center, radius, side, shownFor);
        }

        /// <summary>
        /// 내 판에 있는 북마다 머문 시간을 deltaTime만큼 더한다. 내 판을 떠난(넘긴) 북은 지운다. 같은 스냅샷이면 늘 같은 값이다.
        /// </summary>
        public static void Track(Dictionary<string, float> held, List<string> scratch, LabSnapshot snapshot, ulong player,
            float deltaTime)
        {
            float step = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : Mathf.Max(0f, deltaTime);
            scratch.Clear();
            foreach (string id in held.Keys)
                if (!Owns(snapshot, player, id)) scratch.Add(id);
            foreach (string id in scratch) held.Remove(id);
            if (snapshot?.drums == null) return;
            foreach (LabOrbState drum in snapshot.drums)
            {
                if (drum == null || drum.owner != player || string.IsNullOrEmpty(drum.id)) continue;
                held[drum.id] = (held.TryGetValue(drum.id, out float seconds) ? seconds : 0f) + step;
            }
        }

        /// <summary>가장 오래 내 판에 있던 북과 그 시간. 북이 없으면 null.</summary>
        public static string Longest(Dictionary<string, float> held, out float seconds)
        {
            string longest = null;
            seconds = 0f;
            foreach (KeyValuePair<string, float> entry in held)
                if (longest == null || entry.Value > seconds || entry.Value == seconds && string.CompareOrdinal(entry.Key, longest) < 0)
                { longest = entry.Key; seconds = entry.Value; }
            return longest;
        }

        /// <summary>
        /// 이 사람이 지금 북을 넘길 수 있는 쪽 (RightSide | LeftSide). 매혹으로 판이 멈췄거나 두 쪽 다 막혔으면 0. 혼자면 0이지만,
        /// 개발용 솔로(solo)는 안내를 볼 수 있게 양쪽이 열린 것으로 친다.
        /// 막는 것: 땅따먹기의 팀 벽(1:1은 양쪽, 2:2·3:3은 팀원이 아닌 쪽), 박의 한쪽 넘기기 막기(#215).
        /// </summary>
        public static int OpenSides(LabSnapshot snapshot, ulong player, bool solo = false)
        {
            if (snapshot?.players == null || snapshot.players.Length == 0 || snapshot.CharmOn(player)) return 0;
            if (snapshot.players.Length < 2 && !solo) return 0;
            int closed = LabTeams.ClosedSide(snapshot, player);
            bool blocked = snapshot.SwipeBlockOn(player);
            bool right = !LabTeams.SideClosed(closed, true) && !(blocked && snapshot.swipeBlockedRight);
            bool left = !LabTeams.SideClosed(closed, false) && !(blocked && !snapshot.swipeBlockedRight);
            return (right ? RightSide : 0) | (left ? LeftSide : 0);
        }

        /// <summary>
        /// 손이 가리킬 쪽 (1 오른쪽, -1 왼쪽, 0 없음). 양쪽이 열려 있으면 손이 한 번 밀 때마다(cycleSeconds) 오른쪽부터 번갈아 간다.
        /// </summary>
        public static int SideFor(int openSides, float shownSeconds, float cycleSeconds)
        {
            bool right = (openSides & RightSide) != 0, left = (openSides & LeftSide) != 0;
            if (right && left)
                return Mathf.FloorToInt(Mathf.Max(0f, shownSeconds) / Mathf.Max(.1f, cycleSeconds)) % 2 == 0 ? 1 : -1;
            return right ? 1 : left ? -1 : 0;
        }

        private static bool Owns(LabSnapshot snapshot, ulong player, string id)
        {
            if (snapshot?.drums == null) return false;
            foreach (LabOrbState drum in snapshot.drums)
                if (drum != null && drum.id == id && drum.owner == player) return true;
            return false;
        }

        private void Hide()
        {
            shownFor = -1f;
            PointingSide = 0;
            PointedDrumId = null;
            if (hand != null && hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
            if (arrow != null && arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
        }

        /// <summary>The tutorial's drag guide (4·6단계): an arrow from the drum's edge to the screen's edge, a hand pushing along it.</summary>
        private void Place(Vector2 from, float radius, int side, float elapsed)
        {
            if (!hand.gameObject.activeSelf) hand.gameObject.SetActive(true);
            if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
            float way = side < 0 ? -1f : 1f;
            Vector2 to = new Vector2(way > 0f ? overlay.rect.xMax - art.screenMargin : overlay.rect.xMin + art.screenMargin, from.y);
            float start = from.x + way * (radius + art.arrowGap);
            float end = way > 0f ? Mathf.Max(start, to.x) : Mathf.Min(start, to.x);
            arrow.Set(new Vector2(start, from.y), new Vector2(end, from.y), art.arrowWidth, art.arrowHeadLength, art.arrowHeadWidth);
            GameTutorialDirector.HandDrag(elapsed / art.handDragSeconds, out float travel, out float press, out float alpha);
            RectTransform rect = hand.rectTransform;
            rect.anchoredPosition = Vector2.Lerp(from, to, travel) + art.handRestOffset * (1f - press);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, art.handPressScale, press);
            hand.color = new Color(1f, 1f, 1f, alpha);
        }

        private bool TryDrumCircle(string drumId, out Vector2 center, out float radius)
        {
            center = default;
            radius = 0f;
            if (board == null || overlay == null || !board.TryGetScreenPoint(drumId, out Vector2 screen)) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out center);
            float scale = overlayCanvas != null && overlayCanvas.scaleFactor > 0f ? overlayCanvas.scaleFactor : 1f;
            radius = board.TryGetDrumScreenDiameter(out float pixels) ? pixels * .5f / scale : 40f;
            return true;
        }

        private void BuildOverlay()
        {
            var root = new GameObject("Drum Pass Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            overlayCanvas = root.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = OverlaySortingOrder;
            CopyHudScaler(root.GetComponent<CanvasScaler>());
            overlay = (RectTransform)root.transform;

            arrow = new GameObject("Pass Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(GameTutorialArrow))
                .GetComponent<GameTutorialArrow>();
            arrow.rectTransform.SetParent(overlay, false);
            arrow.rectTransform.anchorMin = Vector2.zero;
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.offsetMin = arrow.rectTransform.offsetMax = Vector2.zero;
            arrow.color = art.arrowColor;
            arrow.raycastTarget = false;
            arrow.gameObject.SetActive(false);

            hand = new GameObject("Pass Hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            hand.rectTransform.SetParent(overlay, false);
            hand.sprite = art.hand;
            hand.preserveAspect = true;
            hand.raycastTarget = false;
            hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(.5f, .5f);
            hand.rectTransform.pivot = art.handTip;
            hand.rectTransform.sizeDelta = art.handSize;
            hand.gameObject.SetActive(false);
        }

        /// <summary>Same reference size as the battle HUD, so the hand and arrow match the tutorial's on every screen.</summary>
        private void CopyHudScaler(CanvasScaler target)
        {
            Graphic sample = hud != null ? (hud.localPlayerCircle != null ? (Graphic)hud.localPlayerCircle : hud.timeText) : null;
            CanvasScaler source = sample != null && sample.canvas != null ? sample.canvas.rootCanvas.GetComponent<CanvasScaler>() : null;
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
            target.scaleFactor = source.scaleFactor;
            target.referencePixelsPerUnit = source.referencePixelsPerUnit;
        }

        private void OnDisable() => Hide();
    }
}
