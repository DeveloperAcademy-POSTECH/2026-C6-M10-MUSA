using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 전투 화면 오른쪽 아래(오른쪽 이웃 동그라미 바로 위, 오른쪽 테두리가 화면 안에 보이도록 조금 안쪽)의 미니맵. 플레이어마다 원 하나를 정다각형 꼭짓점에 놓고
    /// 이웃끼리 선으로 잇는다. 폰마다 "나"를 아래 가운데에 두고, 내 화면의 오른쪽 이웃(다음 번호)은 오른쪽 위,
    /// 왼쪽 이웃(이전 번호)은 왼쪽 위에 놓아 화면에서 넘기는 방향과 미니맵 방향이 같다.
    /// 북을 가진 사람의 원은 제공된 태극 무늬로 덮고, 북이 넘어가면 태극이 선을 따라 미끄러진다.
    /// Scene의 새 디자인 에셋을 쓰며, 저장 컴포넌트가 없는 실험 Scene은 런타임에 붙인다. 전투 중(Playing)에만 보인다.
    /// LabConfig의 Minimap Enabled(Inspector · DEV "Minimap")가 꺼져 있으면 보이지 않고,
    /// Minimap Size(%)만큼 오른쪽 아래 모서리를 기준으로 크기가 바뀌고, Minimap Move X/Y만큼 기본 자리에서 옮겨진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabDrumMinimap : MonoBehaviour
    {
        public Vector2 size = new Vector2(128f, 116f);
        /// <summary>오른쪽 이웃 동그라미 위로 띄우는 간격. 그 사이에 매혹 말풍선(#251)이 뜰 자리를 남긴다.</summary>
        public float gapAboveBadge = 120f;
        /// <summary>
        /// 미니맵 오른쪽 끝을 오른쪽 이웃 동그라미의 오른쪽 끝보다 이만큼 안쪽(왼쪽)에 둔다.
        /// 동그라미가 화면 오른쪽 끝에 걸쳐 있어서, 그대로 맞추면 미니맵 오른쪽 테두리가 화면 밖으로 잘린다.
        /// </summary>
        public float insetFromBadgeRight = 16f;
        public float lineWidth = 3f;
        public int labelSize = 11;
        /// <summary>북이 이웃 한 칸을 건너가는 시간(초)</summary>
        public float slideSeconds = .35f;
        public const string TaegeukResource = "Minimap/Taegeuk";

        // 결과 카드와 같은 한지색 배경·갈색 테두리, "나"는 버튼·대시보드의 "내 줄"과 같은 갈색
        public Color backgroundColor = new Color(.973f, .914f, .804f, .96f);
        public Color borderColor = new Color(.867f, .733f, .565f, 1f);
        public Color lineColor = new Color(.549f, .478f, .4f, 1f);
        public Color otherColor = new Color(.851f, .824f, .773f, 1f);
        public Color otherTextColor = new Color(.29f, .247f, .212f, 1f);
        public Color meColor = new Color(.565f, .337f, .247f, 1f);
        public Color meTextColor = Color.white;

        [Header("Saved concept artwork")]
        public Sprite[] playerSprites;
        public Sprite drumIconSprite;
        public Sprite soloDrumSprite;
        private bool HasConceptArt => playerSprites != null && playerSprites.Length >= 5
            && playerSprites.All(sprite => sprite != null) && drumIconSprite != null;
        private readonly Dictionary<ulong, Image> seatImages = new Dictionary<ulong, Image>();
        private readonly Dictionary<ulong, int> seatNumbers = new Dictionary<ulong, int>();

        private LabNetwork network;
        private RectTransform anchor;
        private Font font;
        private RectTransform panel;
        private RectTransform content;
        private Sprite circleSprite;
        private Sprite taegeukSprite;
        private RectTransform lineLayer, seatLayer, drumLayer, labelLayer;
        private readonly List<ulong> ringOrder = new List<ulong>();
        private readonly Dictionary<string, DrumMarker> drums = new Dictionary<string, DrumMarker>();

        /// <summary>북 하나의 태극 표시. 지나갈 점(waypoints)을 차례로 따라 미끄러진다.</summary>
        private sealed class DrumMarker
        {
            public RectTransform Rect;
            public ulong Owner;
            public readonly List<Vector2> Path = new List<Vector2>();
            public float Progress; // 0..Path.Count-1, 정수 부분이 지금 지나는 구간
        }
        private string layoutKey;
        private Vector2 homePosition;
        private readonly Dictionary<ulong, Vector2> seatPositions = new Dictionary<ulong, Vector2>();

        public bool IsShowing => panel != null && panel.gameObject.activeSelf;
        /// <summary>The minimap's panel on the Canvas; the charm gauge (#251) sits just above it.</summary>
        public RectTransform Panel => panel;
        /// <summary>마지막으로 그린 플레이어별 원 중심 (미니맵 안 좌표, 테스트용)</summary>
        public IReadOnlyDictionary<ulong, Vector2> SeatPositions => seatPositions;
        /// <summary>북 ID → 지금 태극이 그려진 위치 (테스트용)</summary>
        public Vector2? DrumPosition(string drumId) =>
            drums.TryGetValue(drumId ?? string.Empty, out var marker) && marker.Rect != null
                ? marker.Rect.anchoredPosition : (Vector2?)null;
        /// <summary>북 ID → 미니맵이 알고 있는 주인 (테스트용)</summary>
        public ulong? DrumOwner(string drumId) =>
            drums.TryGetValue(drumId ?? string.Empty, out var marker) ? marker.Owner : (ulong?)null;

        /// <summary>badge: 오른쪽 이웃 동그라미. 미니맵은 그 바로 위, 같은 오른쪽 끝에 맞춘다.</summary>
        public void Bind(LabNetwork target, RectTransform badge, Font textFont)
        {
            network = target;
            anchor = badge;
            font = textFont != null ? textFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool enabledByConfig = network == null || network.config == null || network.config.MinimapEnabled;
            bool show = enabledByConfig && snapshot != null && snapshot.phase == LabPhase.Playing
                && snapshot.players != null && snapshot.players.Length > 0;
            if (!show)
            {
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                return;
            }
            EnsurePanel();
            if (panel == null) return;
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            float scale = network != null && network.config != null ? network.config.MinimapSizePercent / 100f : 1f;
            if (!Mathf.Approximately(panel.localScale.x, scale)) panel.localScale = new Vector3(scale, scale, 1f);
            Vector2 move = network != null && network.config != null ? network.config.MinimapMove : Vector2.zero;
            if (panel.anchoredPosition != homePosition + move) panel.anchoredPosition = homePosition + move;
            // 2:2 (#281): only my team shares boards, so only its two players and its drum are shown.
            Rebuild(LabTeams.VisibleTeammates(snapshot, network.LocalPlayerId), network.LocalPlayerId);
            SyncDrums(snapshot.drums);
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 나를 기준으로 몇 번째 P자리인지(0 = 나, 1 = 왼쪽 이웃, n-1 = 오른쪽 이웃)에 따른 정다각형 위치.
        /// 반지름 1 기준이며, 나는 항상 아래(270°)이고 다음 P번호부터 시계 방향(왼쪽 위 → 위 → 오른쪽 위)으로 돈다.
        /// </summary>
        public static Vector2 UnitPosition(int offsetFromMe, int count)
        {
            if (count <= 1) return Vector2.zero;
            float degrees = -90f - 360f * offsetFromMe / count;
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>플레이어 목록이 바뀌었을 때만 원·선을 다시 그린다.</summary>
        public void Rebuild(LabPlayerState[] players, ulong localId)
        {
            EnsurePanel();
            if (panel == null) return;
            var seats = (players ?? new LabPlayerState[0]).Where(p => p != null).OrderBy(p => p.seat).ToArray();
            string key = localId + ":" + string.Join(",", seats.Select(p => p.id + "@" + p.seat));
            if (key == layoutKey) return;
            layoutKey = key;
            for (int i = content.childCount - 1; i >= 0; i--) Remove(content.GetChild(i).gameObject);
            seatPositions.Clear();
            seatImages.Clear();
            seatNumbers.Clear();
            ringOrder.Clear();
            drums.Clear();
            // 선 → 번호가 포함된 배지 → 태극. 구형 단색 원만 별도 번호 텍스트를 쓴다.
            lineLayer = AddLayer("Lines");
            seatLayer = AddLayer("Seats");
            drumLayer = AddLayer("Drums");
            labelLayer = AddLayer("Labels");

            int n = seats.Length;
            int mine = System.Array.FindIndex(seats, p => p.id == localId);
            if (mine < 0) mine = 0; // 관전 등 내가 없으면 첫 번째 자리를 아래에 둔다
            float radius = CircleRadius(n);

            // 정다각형을 상자 안 가운데에 오도록 크기·위치를 맞춘다 (3인 삼각형은 위아래가 비대칭이라 중심을 옮긴다).
            var unit = new Vector2[n];
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = UnitPosition(k, n);
                unit[k] = p;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 span = max - min;
            Vector2 inner = size - Vector2.one * (HasConceptArt ? 23f : 12f) - Vector2.one * (2f * radius);
            float scale = Mathf.Min(span.x > .001f ? inner.x / span.x : float.MaxValue,
                span.y > .001f ? inner.y / span.y : float.MaxValue);
            if (float.IsInfinity(scale) || scale == float.MaxValue) scale = 0f;
            Vector2 center = (min + max) * .5f;

            var points = new Vector2[n];
            for (int k = 0; k < n; k++) points[k] = (unit[k] - center) * scale;
            // 선 먼저 (원 아래에 깔리도록). 2인은 선 하나, 3인 이상은 이웃끼리 둘레를 잇는다.
            if (n == 2) AddLine(points[0], points[1]);
            else if (n > 2) for (int k = 0; k < n; k++) AddLine(points[k], points[(k + 1) % n]);
            for (int k = 0; k < n; k++)
            {
                LabPlayerState player = seats[(mine + k) % n];
                bool me = player.id == localId;
                seatPositions[player.id] = points[k];
                ringOrder.Add(player.id);
                int number = player.seat + 1;
                Image circle = AddCircle(points[k], radius, me ? meColor : otherColor);
                seatImages[player.id] = circle;
                seatNumbers[player.id] = number;
                if (HasConceptArt && number >= 1 && number <= playerSprites.Length)
                {
                    circle.sprite = playerSprites[number - 1];
                    circle.color = Color.white;
                    circle.preserveAspect = true;
                }
                else AddLabel(LabResultStats.PlayerName(player), points[k], radius, me ? meTextColor : otherTextColor);
            }
        }

        /// <summary>
        /// 스냅샷의 북 주인을 반영한다. 처음 보는 북은 주인 원에 바로 놓고, 주인이 바뀐 북은
        /// 이웃 선을 따라(통과한 북은 중간 원을 거쳐) 새 주인 원까지 미끄러지게 한다.
        /// </summary>
        public void SyncDrums(LabOrbState[] snapshotDrums)
        {
            if (drumLayer == null) return;
            var seen = new HashSet<string>();
            foreach (var drum in snapshotDrums ?? new LabOrbState[0])
            {
                if (drum == null || string.IsNullOrEmpty(drum.id) || !seatPositions.ContainsKey(drum.owner)) continue;
                seen.Add(drum.id);
                if (!drums.TryGetValue(drum.id, out var marker))
                {
                    marker = new DrumMarker { Rect = AddTaegeuk(seatPositions[drum.owner]), Owner = drum.owner };
                    drums.Add(drum.id, marker);
                    continue;
                }
                if (marker.Owner == drum.owner) continue;
                Vector2 from = marker.Rect.anchoredPosition;
                marker.Path.Clear();
                marker.Path.Add(from);
                foreach (ulong seat in RingSteps(marker.Owner, drum.owner)) marker.Path.Add(seatPositions[seat]);
                marker.Progress = 0f;
                marker.Owner = drum.owner;
            }
            if (HasConceptArt && ringOrder.Count == 1 && soloDrumSprite != null)
            {
                ulong owner = ringOrder[0];
                seatImages[owner].sprite = drums.Any(pair => pair.Value.Owner == owner && seen.Contains(pair.Key)) ? soloDrumSprite : playerSprites[seatNumbers[owner] - 1];
            }
            foreach (string gone in drums.Keys.Where(id => !seen.Contains(id)).ToArray())
            {
                Remove(drums[gone].Rect != null ? drums[gone].Rect.gameObject : null);
                drums.Remove(gone);
            }
        }

        /// <summary>태극을 경로를 따라 움직인다. 한 구간(이웃 한 칸)에 slideSeconds가 걸린다.</summary>
        public void Advance(float deltaSeconds)
        {
            foreach (var marker in drums.Values)
            {
                if (marker.Rect == null || marker.Path.Count < 2) continue;
                marker.Progress += deltaSeconds / Mathf.Max(.01f, slideSeconds);
                int last = marker.Path.Count - 1;
                if (marker.Progress >= last)
                {
                    marker.Rect.anchoredPosition = marker.Path[last];
                    marker.Path.Clear();
                    continue;
                }
                int index = Mathf.FloorToInt(marker.Progress);
                float t = Mathf.SmoothStep(0f, 1f, marker.Progress - index);
                marker.Rect.anchoredPosition = Vector2.Lerp(marker.Path[index], marker.Path[index + 1], t);
            }
        }

        /// <summary>
        /// from에서 to까지 둘레를 따라 지나는 자리들 (from 제외, to 포함). 가까운 쪽으로 돌고,
        /// 같으면 시계(다음 번호) 방향. 3인은 언제나 한 칸이다.
        /// </summary>
        private IEnumerable<ulong> RingSteps(ulong from, ulong to)
        {
            int n = ringOrder.Count;
            int a = ringOrder.IndexOf(from), b = ringOrder.IndexOf(to);
            if (n == 0 || b < 0) yield break;
            if (a < 0) { yield return to; yield break; }
            int forward = (b - a + n) % n, backward = (a - b + n) % n;
            int step = forward <= backward ? 1 : -1;
            for (int i = a; i != b;)
            {
                i = (i + step + n) % n;
                yield return ringOrder[i];
            }
        }

        private float CircleRadius(int count) => HasConceptArt
            ? (count <= 1 ? 35.5f : 18.5f)
            : count <= 3 ? 17f : count == 4 ? 15f : 13f;

        private void EnsurePanel()
        {
            if (panel != null) return;
            if (anchor == null || anchor.parent == null) return;
            var border = new GameObject("DrumMinimap", typeof(RectTransform));
            panel = (RectTransform)border.transform;
            panel.SetParent(anchor.parent, false);
            // 오른쪽 이웃 동그라미의 오른쪽 끝에 맞추고 그 위로 띄운다.
            panel.anchorMin = panel.anchorMax = anchor.anchorMin;
            panel.pivot = new Vector2(1f, 0f);
            homePosition = anchor.anchoredPosition
                + new Vector2(anchor.sizeDelta.x * (1f - anchor.pivot.x) - insetFromBadgeRight,
                    anchor.sizeDelta.y * (1f - anchor.pivot.y) + gapAboveBadge);
            panel.anchoredPosition = homePosition;
            panel.sizeDelta = size;
            if (!HasConceptArt)
            {
                var borderImage = border.AddComponent<Image>();
                borderImage.color = borderColor;
                borderImage.raycastTarget = false;
                var inner = new GameObject("Background", typeof(RectTransform), typeof(Image));
                var innerRect = (RectTransform)inner.transform;
                innerRect.SetParent(panel, false);
                innerRect.anchorMin = Vector2.zero;
                innerRect.anchorMax = Vector2.one;
                innerRect.offsetMin = new Vector2(3f, 3f);
                innerRect.offsetMax = new Vector2(-3f, -3f);
                var innerImage = inner.GetComponent<Image>();
                innerImage.color = backgroundColor;
                innerImage.raycastTarget = false;
            }

            var layer = new GameObject("Content", typeof(RectTransform));
            content = (RectTransform)layer.transform;
            content.SetParent(panel, false);
            content.anchorMin = content.anchorMax = new Vector2(.5f, .5f);
            content.sizeDelta = Vector2.zero;
            panel.gameObject.SetActive(false);
        }

        private void AddLine(Vector2 from, Vector2 to)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(lineLayer, false);
            Vector2 delta = to - from;
            rect.anchoredPosition = (from + to) * .5f;
            rect.sizeDelta = new Vector2(delta.magnitude, lineWidth);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = go.GetComponent<Image>();
            image.color = lineColor;
            image.raycastTarget = false;
        }

        private Image AddCircle(Vector2 at, float radius, Color color)
        {
            var go = new GameObject("Seat", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(seatLayer, false);
            rect.anchoredPosition = at;
            rect.sizeDelta = Vector2.one * (radius * 2f);
            var image = go.GetComponent<Image>();
            image.sprite = HasConceptArt ? playerSprites[0] : CircleSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform AddLayer(string layerName)
        {
            var go = new GameObject(layerName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        /// <summary>북을 가진 사람 원 안쪽의 태극. 솔로는 제공된 완성 배지를 쓴다.</summary>
        private RectTransform AddTaegeuk(Vector2 at)
        {
            var go = new GameObject("Drum", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(drumLayer, false);
            rect.anchoredPosition = at;
            rect.sizeDelta = Vector2.one * (CircleRadius(ringOrder.Count) * 2f * (HasConceptArt ? .746f : 1f));
            var image = go.GetComponent<Image>();
            image.sprite = HasConceptArt ? drumIconSprite : TaegeukSprite();
            image.enabled = !(HasConceptArt && ringOrder.Count == 1 && soloDrumSprite != null);
            image.color = image.sprite != null ? Color.white : new Color(.8f, .18f, .23f, 1f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>Resources/Minimap/Taegeuk.png. 스프라이트로 가져왔든 텍스처로 가져왔든 쓴다. 없으면 원으로 대신한다.</summary>
        private Sprite TaegeukSprite()
        {
            if (taegeukSprite != null) return taegeukSprite;
            taegeukSprite = Resources.Load<Sprite>(TaegeukResource);
            if (taegeukSprite != null) return taegeukSprite;
            var texture = Resources.Load<Texture2D>(TaegeukResource);
            if (texture == null) return CircleSprite();
            taegeukSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            return taegeukSprite;
        }

        /// <summary>원 안쪽 위에 "P1" 같은 번호</summary>
        private void AddLabel(string value, Vector2 at, float radius, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(labelLayer, false);
            rect.anchoredPosition = at + new Vector2(0f, radius * .42f);
            rect.sizeDelta = new Vector2(radius * 2f, labelSize + 4f);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = labelSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
        }

        /// <summary>테두리가 부드러운 흰 원 (코드로 한 번만 만든다)</summary>
        private Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int pixels = 128;
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                name = "MinimapCircle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var colors = new Color32[pixels * pixels];
            float r = pixels * .5f;
            for (int y = 0; y < pixels; y++)
            for (int x = 0; x < pixels; x++)
            {
                float d = new Vector2(x + .5f - r, y + .5f - r).magnitude;
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f);
                colors[y * pixels + x] = new Color32(255, 255, 255, a);
            }
            texture.SetPixels32(colors);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0, 0, pixels, pixels), new Vector2(.5f, .5f), 100f);
            return circleSprite;
        }

        private void OnDestroy()
        {
            if (circleSprite != null)
            {
                Remove(circleSprite.texture);
                Remove(circleSprite);
            }
        }

        // 에디터 테스트(Play 아님)에서도 지울 수 있게
        private static void Remove(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
