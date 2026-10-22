using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281) 전투 화면 위쪽 막대. 기존 도색 % 막대 자리에서 화면 가로 대부분을 쓰며, 박 전체 칸 대비
    /// 청 칸은 왼쪽부터 파랑으로, 홍 칸은 오른쪽부터 빨강으로 채우고 가운데 빈 칸은 한지색으로 남긴다. 청·홍 깃발은 막대 위에서
    /// 각 팀 칸의 끝(경계)을 따라 움직이고, 천은 자기 팀 쪽으로 날린다.
    /// 내 이름표(아래 P 동그라미)에는 우리 팀 색 고리를 두른다. 깃발 그림을 받기 전까지 깃발은 깃대·천·글자로 그린다.
    /// 막대 아래에는 팀별 피버 게이지(맞힌 수, 피버 중에는 남은 시간)를 둔다. 결과 화면에서는 최종 비율을 숫자와 함께 바로 보인다.
    /// 그림은 모두 실행 중에 만들므로 저장된 Scene은 바뀌지 않는다.
    /// </summary>
    public sealed class LabTeamBar : MonoBehaviour
    {
        private const float Width = 336f, Height = 29.5f, Inset = 3f;
        /// <summary>The Figma bar sits where the paint bar was, centred across the 402-wide design.</summary>
        private static readonly Vector2 Anchor = new Vector2(201f / 402f, 1f - 118.5f / 874f);
        private const float FillSpeed = .8f;
        public static readonly Color Blue = LabPaintPalette.SrgbColorFor(LabOrbElement.Water);
        public static readonly Color Red = LabPaintPalette.SrgbColorFor(LabOrbElement.Fire);
        private static readonly Color Paper = new Color(.96f, .93f, .86f), Ink = new Color(.2f, .16f, .12f);
        private static Sprite frameSprite, maskSprite, ringSprite, solidSprite;

        private Image blueFill, redFill, ring, blueGauge, redGauge;
        private Image ringOwner;
        private Text blueGaugeText, redGaugeText, blueNumber, redNumber, emptyNumber;
        private GameObject gauges, numbers;
        private RectTransform blueFlag, redFlag;
        private float blueShown, redShown;

        /// <summary>Share of the whole gourd the bar shows for each team now (it eases toward the snapshot).</summary>
        public float BlueShare => blueShown;
        public float RedShare => redShown;
        public bool Visible => gameObject.activeSelf;
        /// <summary>The team-coloured ring around this player's own badge, or null before a team battle.</summary>
        public Image LocalRing => ring;
        /// <summary>Where each flag stands along the bar (0 left end, 1 right end): the edge of that team's cells.</summary>
        public float BlueFlagAt => blueFlag.anchorMin.x;
        public float RedFlagAt => redFlag.anchorMin.x;
        /// <summary>Fever gauges under the bar: hits toward the fever, then the fever's time left.</summary>
        public bool GaugesShown => gauges != null && gauges.activeSelf;
        public float BlueGauge => blueGauge.fillAmount;
        public float RedGauge => redGauge.fillAmount;
        public string BlueGaugeText => blueGaugeText.text;
        public string RedGaugeText => redGaugeText.text;
        /// <summary>The final shares in words, shown on the result screen only.</summary>
        public string NumbersText => numbers != null && numbers.activeSelf
            ? blueNumber.text + " / " + emptyNumber.text + " / " + redNumber.text : string.Empty;

        public static LabTeamBar Create(RectTransform battleUi, Font font) => Create(battleUi, font, Anchor);

        /// <summary>The same bar at another normalized anchor of its parent, such as the result screen.</summary>
        public static LabTeamBar Create(RectTransform battleUi, Font font, Vector2 anchor)
        {
            var root = new GameObject("Team Territory Bar", typeof(RectTransform));
            root.layer = battleUi.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(battleUi, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = Vector2.zero;
            var bar = root.AddComponent<LabTeamBar>();
            bar.Build(font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            return bar;
        }

        /// <summary>
        /// 청 칸·홍 칸 비율을 채우고, 내 이름표에 우리 팀 고리를 두른다. feverHitsNeeded가 0이면 피버 게이지를 숨긴다.
        /// </summary>
        public void Present(LabSnapshot snapshot, LabTeam localTeam, Image localBadge, int feverHitsNeeded = 0)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            int cells = snapshot != null ? snapshot.paintCellCount : 0;
            float blue = Share(snapshot != null ? snapshot.blueCells : 0, cells);
            float red = Share(snapshot != null ? snapshot.redCells : 0, cells);
            float step = FillSpeed * Time.unscaledDeltaTime;
            blueShown = Mathf.MoveTowards(blueShown, blue, step);
            redShown = Mathf.MoveTowards(redShown, red, step);
            blueFill.fillAmount = blueShown;
            redFill.fillAmount = redShown;
            PlaceFlags();
            SetRing(localBadge, localTeam);
            numbers.SetActive(false);
            bool fever = snapshot != null && feverHitsNeeded > 0;
            if (gauges.activeSelf != fever) gauges.SetActive(fever);
            if (!fever) return;
            Gauge(blueGauge, blueGaugeText, Blue, snapshot.blueFeverHits, feverHitsNeeded,
                snapshot.blueFeverStartedAt, snapshot.blueFeverUntil, snapshot.serverTime);
            Gauge(redGauge, redGaugeText, Red, snapshot.redFeverHits, feverHitsNeeded,
                snapshot.redFeverStartedAt, snapshot.redFeverUntil, snapshot.serverTime);
        }

        /// <summary>
        /// 결과 화면: 끝난 판의 비율을 바로 채우고 청·빈 칸·홍 숫자를 보인다. 피버 게이지와 고리는 없다.
        /// textColor는 그 화면의 글자색이다. 어두운 배경에서도 숫자가 읽히도록 빈 칸은 그 색, 팀 숫자는 팀 색과 섞는다.
        /// </summary>
        public void ShowFinal(LabSnapshot snapshot, Color? textColor = null)
        {
            Color neutral = textColor ?? Ink;
            blueNumber.color = textColor.HasValue ? Color.Lerp(Blue, neutral, .35f) : Blue;
            redNumber.color = textColor.HasValue ? Color.Lerp(Red, neutral, .35f) : Red;
            emptyNumber.color = neutral;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            int cells = snapshot != null ? snapshot.paintCellCount : 0;
            int blueCells = snapshot != null ? snapshot.blueCells : 0, redCells = snapshot != null ? snapshot.redCells : 0;
            blueFill.fillAmount = blueShown = Share(blueCells, cells);
            redFill.fillAmount = redShown = Share(redCells, cells);
            PlaceFlags();
            gauges.SetActive(false);
            numbers.SetActive(true);
            blueNumber.text = "청 " + Percent(blueCells, cells);
            redNumber.text = "홍 " + Percent(redCells, cells);
            emptyNumber.text = "빈 칸 " + Percent(Mathf.Max(0, cells - blueCells - redCells), cells);
        }

        private static string Percent(int count, int cells) => cells > 0
            ? (count * 100f / cells).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%" : "0.0%";

        /// <summary>
        /// One team's gauge: hits toward the fever, or while the fever lasts its time running down, pulsing light.
        /// </summary>
        private static void Gauge(Image fill, Text label, Color team, int hits, int needed, double startedAt,
            double until, double now)
        {
            bool fever = until > now;
            fill.fillAmount = fever ? Mathf.Clamp01((float)((until - now) / System.Math.Max(.001d, until - startedAt)))
                : Mathf.Clamp01(hits / (float)needed);
            float pulse = fever ? .5f + .5f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
            fill.color = Color.Lerp(team, Color.white, .45f * pulse);
            label.text = fever ? "피버! " + Mathf.CeilToInt((float)(until - now)) + "초" : "피버 " + hits + "/" + needed;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
            blueShown = redShown = 0f;
        }

        /// <summary>A team's share of the whole gourd, 0 when nothing is known yet.</summary>
        public static float Share(int teamCells, int cells) => cells > 0 ? Mathf.Clamp01(teamCells / (float)cells) : 0f;

        /// <summary>The finished battle in words: the winning team, or a draw.</summary>
        public static string ResultText(LabSnapshot snapshot) =>
            snapshot == null || snapshot.phase != LabPhase.Victory ? string.Empty
            : snapshot.teamWinner == LabTeam.Blue ? "청팀 승리!" : snapshot.teamWinner == LabTeam.Red ? "홍팀 승리!" : "무승부";

        private void Build(Font font)
        {
            Image frame = Part<Image>("Frame", transform);
            Stretch(frame.rectTransform, 0f);
            Sliced(frame, FrameSprite(), 2.2f);
            frame.color = Color.white;

            Image inner = Part<Image>("Fill Mask", transform);
            Stretch(inner.rectTransform, Inset);
            Sliced(inner, MaskSprite(), 2.64f);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            blueFill = Fill("Blue Fill", inner.transform, Blue, Image.OriginHorizontal.Left);
            redFill = Fill("Red Fill", inner.transform, Red, Image.OriginHorizontal.Right);

            foreach (float at in new[] { .25f, .5f, .75f })
            {
                Image tick = Part<Image>("Tick " + at, transform);
                RectTransform rect = tick.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(at, .5f);
                rect.sizeDelta = new Vector2(2f, at == .5f ? 15f : 9f);
                tick.color = new Color(Ink.r, Ink.g, Ink.b, .45f);
            }

            blueFlag = Flag("Blue Flag", "청", Blue, font, false);
            redFlag = Flag("Red Flag", "홍", Red, font, true);
            PlaceFlags();

            gauges = new GameObject("Fever Gauges", typeof(RectTransform));
            gauges.layer = gameObject.layer;
            var gaugeRoot = (RectTransform)gauges.transform;
            gaugeRoot.SetParent(transform, false);
            Stretch(gaugeRoot, 0f);
            blueGauge = GaugeBar("Blue Fever", gaugeRoot, 0f, .42f, Blue, Image.OriginHorizontal.Left, font,
                TextAnchor.UpperLeft, out blueGaugeText);
            redGauge = GaugeBar("Red Fever", gaugeRoot, .58f, 1f, Red, Image.OriginHorizontal.Right, font,
                TextAnchor.UpperRight, out redGaugeText);
            gauges.SetActive(false);

            numbers = new GameObject("Final Shares", typeof(RectTransform));
            numbers.layer = gameObject.layer;
            var numberRoot = (RectTransform)numbers.transform;
            numberRoot.SetParent(transform, false);
            Stretch(numberRoot, 0f);
            blueNumber = Number("Blue Share", numberRoot, font, 0f, TextAnchor.UpperLeft, Blue);
            emptyNumber = Number("Empty Share", numberRoot, font, .5f, TextAnchor.UpperCenter, Ink);
            redNumber = Number("Red Share", numberRoot, font, 1f, TextAnchor.UpperRight, Red);
            numbers.SetActive(false);
        }

        /// <summary>A thin gauge just under the bar between two shares of its width, with its words beneath.</summary>
        private static Image GaugeBar(string name, RectTransform parent, float from, float to, Color team,
            Image.OriginHorizontal origin, Font font, TextAnchor align, out Text label)
        {
            Image track = Part<Image>(name + " Track", parent);
            RectTransform rect = track.rectTransform;
            rect.anchorMin = new Vector2(from, 0f); rect.anchorMax = new Vector2(to, 0f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 6f);
            rect.anchoredPosition = new Vector2(0f, -5f);
            track.sprite = SolidSprite();
            track.color = new Color(Ink.r, Ink.g, Ink.b, .18f);
            Image fill = Fill(name + " Fill", track.transform, team, origin);
            label = Part<Text>(name + " Label", track.transform);
            RectTransform words = label.rectTransform;
            words.anchorMin = Vector2.zero; words.anchorMax = new Vector2(1f, 0f);
            words.pivot = new Vector2(.5f, 1f);
            words.sizeDelta = new Vector2(0f, 16f);
            words.anchoredPosition = new Vector2(0f, -2f);
            label.font = font; label.fontSize = 11; label.color = team; label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return fill;
        }

        private static Text Number(string name, RectTransform parent, Font font, float at, TextAnchor align, Color color)
        {
            Text text = Part<Text>(name, parent);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(at, 0f);
            rect.pivot = new Vector2(at, 1f);
            rect.sizeDelta = new Vector2(140f, 22f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            text.font = font; text.fontSize = 16; text.color = color; text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Each pole stands on the edge of its team's cells, so the flags march toward each other as the teams paint
        /// and fall back when they lose cells. They sit at the inner ends of the frame before anything is painted.
        /// </summary>
        private void PlaceFlags()
        {
            float inner = Width - 2f * Inset;
            float blueAt = (Inset + blueShown * inner) / Width, redAt = 1f - (Inset + redShown * inner) / Width;
            blueFlag.anchorMin = blueFlag.anchorMax = new Vector2(blueAt, .5f);
            redFlag.anchorMin = redFlag.anchorMax = new Vector2(redAt, .5f);
        }

        /// <summary>A pole through the bar and a cloth above it, flying toward the team's own side.</summary>
        private RectTransform Flag(string name, string label, Color team, Font font, bool right)
        {
            var flag = new GameObject(name, typeof(RectTransform));
            flag.layer = gameObject.layer;
            var rect = (RectTransform)flag.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            const float clothHeight = 22f;
            float clothY = Height * .5f + 3f + clothHeight * .5f, top = clothY + clothHeight * .5f;
            Image pole = Part<Image>("Pole", rect);
            pole.color = new Color(.35f, .24f, .16f);
            pole.rectTransform.sizeDelta = new Vector2(3f, top + Height * .5f);
            pole.rectTransform.anchoredPosition = new Vector2(0f, (top - Height * .5f) * .5f);

            Image cloth = Part<Image>("Cloth", rect);
            cloth.color = Paper;
            cloth.rectTransform.sizeDelta = new Vector2(24f, clothHeight);
            cloth.rectTransform.anchoredPosition = new Vector2((right ? 1f : -1f) * 13.5f, clothY);
            Outline edge = cloth.gameObject.AddComponent<Outline>();
            edge.effectColor = team;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            Text text = Part<Text>("Label", cloth.transform);
            Stretch(text.rectTransform, 0f);
            text.font = font; text.text = label; text.fontSize = 16; text.color = team;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return rect;
        }

        private void SetRing(Image badge, LabTeam team)
        {
            if (badge == null || team == LabTeam.None)
            {
                if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }
            if (ring == null || ringOwner != badge)
            {
                if (ring != null) Destroy(ring.gameObject);
                ring = Part<Image>("Team Ring", badge.transform.parent);
                ringOwner = badge;
                ring.sprite = RingSprite();
                // Behind the badge, so only the part outside its own rim shows.
                ring.transform.SetSiblingIndex(badge.transform.GetSiblingIndex());
            }
            // Same rectangle as the badge, scaled about the badge's own pivot.
            RectTransform source = badge.rectTransform, target = ring.rectTransform;
            target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition; target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale * 1.24f;
            ring.color = team == LabTeam.Blue ? Blue : Red;
            bool shown = badge.enabled && badge.gameObject.activeInHierarchy;
            if (ring.gameObject.activeSelf != shown) ring.gameObject.SetActive(shown);
        }

        private static Image Fill(string name, Transform parent, Color color, Image.OriginHorizontal origin)
        {
            Image fill = Part<Image>(name, parent);
            Stretch(fill.rectTransform, 0f);
            // An Image without a sprite ignores Filled and draws its whole rectangle, so the fills need one.
            fill.sprite = SolidSprite();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)origin;
            fill.fillAmount = 0f;
            return fill;
        }

        private static T Part<T>(string name, Transform parent) where T : Graphic
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.layer = parent.gameObject.layer;
            item.transform.SetParent(parent, false);
            T graphic = item.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Sliced(Image image, Sprite sprite, float multiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        private static Sprite FrameSprite() => frameSprite != null ? frameSprite
            : frameSprite = Rounded("Lab team bar frame", 64, 30f, Paper, Ink, 4f, true);

        private static Sprite MaskSprite() => maskSprite != null ? maskSprite
            : maskSprite = Rounded("Lab team bar mask", 64, 30f, Color.white, Color.white, 0f, true);

        private static Sprite SolidSprite() => solidSprite != null ? solidSprite
            : solidSprite = Rounded("Lab team bar solid", 4, 0f, Color.white, Color.white, 0f, false);

        private static Sprite RingSprite() => ringSprite != null ? ringSprite
            : ringSprite = Rounded("Lab team badge ring", 128, 64f, Color.clear, Color.white, 11f, false);

        /// <summary>A rounded square of the given radius; sliced through its middle when border is true.</summary>
        private static Sprite Rounded(string label, int size, float radius, Color fill, Color edge, float edgeWidth, bool border)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float qx = Mathf.Abs(px - size * .5f) - (size * .5f - radius);
                    float qy = Mathf.Abs(py - size * .5f) - (size * .5f - radius);
                    float distance = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float coverage = Mathf.Clamp01(.5f - distance);
                    Color color = edgeWidth > 0f && distance > -edgeWidth ? edge : fill;
                    color.a *= coverage;
                    pixels[y * size + x] = color;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = label, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            float slice = border ? size * .5f - 1f : 0f;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            sprite.name = label;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
