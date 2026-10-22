using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281) 전투 화면 위쪽 막대. 기존 도색 % 막대 자리에서 화면 가로 대부분을 쓰며, 박 전체 칸 대비
    /// 청 칸은 왼쪽부터 파랑으로, 홍 칸은 오른쪽부터 빨강으로 채우고 가운데 빈 칸은 한지색으로 남긴다. 양 끝에 청·홍 깃발을 세운다.
    /// 내 이름표(아래 P 동그라미)에는 우리 팀 색 고리를 두른다. 깃발 그림을 받기 전까지 깃발은 깃대·천·글자로 그린다.
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

        private Image blueFill, redFill, ring;
        private Image ringOwner;
        private float blueShown, redShown;

        /// <summary>Share of the whole gourd the bar shows for each team now (it eases toward the snapshot).</summary>
        public float BlueShare => blueShown;
        public float RedShare => redShown;
        public bool Visible => gameObject.activeSelf;
        /// <summary>The team-coloured ring around this player's own badge, or null before a team battle.</summary>
        public Image LocalRing => ring;

        public static LabTeamBar Create(RectTransform battleUi, Font font)
        {
            var root = new GameObject("Team Territory Bar", typeof(RectTransform));
            root.layer = battleUi.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(battleUi, false);
            rect.anchorMin = rect.anchorMax = Anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = Vector2.zero;
            var bar = root.AddComponent<LabTeamBar>();
            bar.Build(font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            return bar;
        }

        /// <summary>청 칸·홍 칸 비율을 채우고, 내 이름표에 우리 팀 고리를 두른다.</summary>
        public void Present(LabSnapshot snapshot, LabTeam localTeam, Image localBadge)
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
            SetRing(localBadge, localTeam);
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

            Flag("Blue Flag", "청", Blue, font, false);
            Flag("Red Flag", "홍", Red, font, true);
        }

        private void Flag(string name, string label, Color team, Font font, bool right)
        {
            float side = right ? -1f : 1f;
            var flag = new GameObject(name, typeof(RectTransform));
            flag.layer = gameObject.layer;
            var rect = (RectTransform)flag.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = new Vector2(side * -6f, 0f);

            Image pole = Part<Image>("Pole", rect);
            pole.color = new Color(.35f, .24f, .16f);
            pole.rectTransform.sizeDelta = new Vector2(3f, 44f);
            pole.rectTransform.anchoredPosition = new Vector2(0f, 6f);

            Image cloth = Part<Image>("Cloth", rect);
            cloth.color = Paper;
            cloth.rectTransform.sizeDelta = new Vector2(24f, 22f);
            cloth.rectTransform.anchoredPosition = new Vector2(side * 13.5f, 16f);
            Outline edge = cloth.gameObject.AddComponent<Outline>();
            edge.effectColor = team;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            Text text = Part<Text>("Label", cloth.transform);
            Stretch(text.rectTransform, 0f);
            text.font = font; text.text = label; text.fontSize = 16; text.color = team;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
