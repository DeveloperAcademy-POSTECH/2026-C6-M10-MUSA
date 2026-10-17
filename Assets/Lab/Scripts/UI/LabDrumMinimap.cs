using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 전투 화면 오른쪽 아래(오른쪽 이웃 동그라미 바로 위)의 미니맵. 플레이어마다 원 하나를 정다각형 꼭짓점에 놓고
    /// 이웃끼리 선으로 잇는다. 폰마다 "나"를 아래 가운데에 두고, 내 화면의 오른쪽 이웃(다음 번호)은 오른쪽 위,
    /// 왼쪽 이웃(이전 번호)은 왼쪽 위에 놓아 화면에서 넘기는 방향과 미니맵 방향이 같다.
    /// 장면 수정 없이 LabSceneController가 실행 시 붙이고, 전투 중(Playing)에만 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabDrumMinimap : MonoBehaviour
    {
        public Vector2 size = new Vector2(128f, 116f);
        /// <summary>오른쪽 이웃 동그라미 위로 띄우는 간격</summary>
        public float gapAboveBadge = 8f;
        public float lineWidth = 3f;
        public int labelSize = 11;

        // 결과 카드와 같은 한지색 배경·갈색 테두리, "나"는 버튼·대시보드의 "내 줄"과 같은 갈색
        public Color backgroundColor = new Color(.973f, .914f, .804f, .96f);
        public Color borderColor = new Color(.867f, .733f, .565f, 1f);
        public Color lineColor = new Color(.549f, .478f, .4f, 1f);
        public Color otherColor = new Color(.851f, .824f, .773f, 1f);
        public Color otherTextColor = new Color(.29f, .247f, .212f, 1f);
        public Color meColor = new Color(.565f, .337f, .247f, 1f);
        public Color meTextColor = Color.white;

        private LabNetwork network;
        private RectTransform anchor;
        private Font font;
        private RectTransform panel;
        private RectTransform content;
        private Sprite circleSprite;
        private string layoutKey;
        private readonly Dictionary<ulong, Vector2> seatPositions = new Dictionary<ulong, Vector2>();

        public bool IsShowing => panel != null && panel.gameObject.activeSelf;
        /// <summary>마지막으로 그린 플레이어별 원 중심 (미니맵 안 좌표, 테스트용)</summary>
        public IReadOnlyDictionary<ulong, Vector2> SeatPositions => seatPositions;

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
            bool show = snapshot != null && snapshot.phase == LabPhase.Playing
                && snapshot.players != null && snapshot.players.Length > 0;
            if (!show)
            {
                if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
                return;
            }
            EnsurePanel();
            if (panel == null) return;
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            Rebuild(snapshot.players, network.LocalPlayerId);
        }

        /// <summary>
        /// 나를 기준으로 몇 번째 자리인지(0 = 나, 1 = 오른쪽 이웃, n-1 = 왼쪽 이웃)에 따른 정다각형 위치.
        /// 반지름 1 기준이며, 나는 항상 아래(270°)이고 오른쪽 이웃부터 반시계 방향(오른쪽 위 → 위 → 왼쪽 위)으로 돈다.
        /// </summary>
        public static Vector2 UnitPosition(int offsetFromMe, int count)
        {
            if (count <= 1) return Vector2.zero;
            float degrees = -90f + 360f * offsetFromMe / count;
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
            Vector2 inner = size - new Vector2(12f, 12f) - Vector2.one * (2f * radius);
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
                AddCircle(points[k], radius, me ? meColor : otherColor);
                AddLabel(LabResultStats.PlayerName(player), points[k], radius, me ? meTextColor : otherTextColor);
            }
        }

        private static float CircleRadius(int count) => count <= 3 ? 17f : count == 4 ? 15f : 13f;

        private void EnsurePanel()
        {
            if (panel != null) return;
            if (anchor == null || anchor.parent == null) return;
            var border = new GameObject("DrumMinimap", typeof(RectTransform), typeof(Image));
            panel = (RectTransform)border.transform;
            panel.SetParent(anchor.parent, false);
            // 오른쪽 이웃 동그라미의 오른쪽 끝에 맞추고 그 위로 띄운다.
            panel.anchorMin = panel.anchorMax = anchor.anchorMin;
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = anchor.anchoredPosition
                + new Vector2(anchor.sizeDelta.x * (1f - anchor.pivot.x), anchor.sizeDelta.y * (1f - anchor.pivot.y) + gapAboveBadge);
            panel.sizeDelta = size;
            var borderImage = border.GetComponent<Image>();
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
            rect.SetParent(content, false);
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
            rect.SetParent(content, false);
            rect.anchoredPosition = at;
            rect.sizeDelta = Vector2.one * (radius * 2f);
            var image = go.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>원 안쪽 위에 "P1" 같은 번호</summary>
        private void AddLabel(string value, Vector2 at, float radius, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
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
