using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281) 피버 연출. 우리 팀 피버가 시작되면 청·홍 상관없이 화면 가운데에 "피버타임!"과
    /// "마구 던져버리자!!"가 우리 팀 색으로 크게 튀어나왔다가 사라지고, 피버 동안 화면 가장자리가 팀 색으로 선명하게
    /// 숨 쉰다. 상대 팀 피버는 막대 아래 게이지로만 알린다. 구슬이 바뀌는 연출은 판(LabOrbBoard)이 맡는다.
    /// 그림은 실행 중에 만든다.
    /// </summary>
    public sealed class LabTeamFever : MonoBehaviour
    {
        private const float BannerSeconds = 1.6f, PopSeconds = .3f, FadeSeconds = .4f;
        /// <summary>A fever seen later than this after it began, for example after a reconnect, is not announced.</summary>
        private const double AnnounceWindowSeconds = 1.5d;
        private const float GlowHz = 1.6f, GlowFadeSeconds = .4f;
        /// <summary>Default faintest and strongest glow and how far in from each screen edge it reaches (#293 tunes them).</summary>
        private const float GlowLow = .45f, GlowHigh = .8f, GlowReach = .3f;
        public const string BannerTitle = "피버타임!", BannerLine = "마구 던져버리자!!";
        private static Sprite glowSprite;
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> reachSprites =
            new System.Collections.Generic.Dictionary<int, Sprite>();
        private float glowReach = GlowReach;

        private Image glow;
        private RectTransform banner;
        private Text title, subtitle;
        private readonly double[] announced = { -1d, -1d, -1d };
        private float bannerAge = float.PositiveInfinity, glowAlpha;
        private LabTeam bannerTeam;

        public bool BannerShown => banner != null && banner.gameObject.activeSelf;
        public string BannerText => title != null ? title.text : string.Empty;
        public string SubtitleText => subtitle != null && BannerShown ? subtitle.text : string.Empty;
        /// <summary>The edge glow's opacity now; above zero only during this player's team fever.</summary>
        public float GlowAlpha => glow != null && glow.gameObject.activeSelf ? glow.color.a : 0f;
        public Color GlowColor => glow != null ? glow.color : Color.clear;

        public static LabTeamFever Create(RectTransform battleUi, Font font)
        {
            var root = new GameObject("Team Fever", typeof(RectTransform));
            root.layer = battleUi.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(battleUi, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fever = root.AddComponent<LabTeamFever>();
            fever.Build(font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            return fever;
        }

        /// <summary>
        /// Announces each new fever once, and breathes the edge glow while this player's team is in one. The glow's
        /// strength and thickness come from config (#293); without it they keep their defaults.
        /// </summary>
        public void Present(LabSnapshot snapshot, LabTeam localTeam, LabConfig config = null)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (snapshot == null) { Hide(); return; }
            Announce(snapshot, LabTeam.Blue, snapshot.blueFeverStartedAt, localTeam);
            Announce(snapshot, LabTeam.Red, snapshot.redFeverStartedAt, localTeam);
            if (!string.IsNullOrEmpty(BannerText) && bannerTeam != localTeam) HideBanner();
            float delta = Time.unscaledDeltaTime;
            AdvanceBanner(delta);
            bool mine = localTeam != LabTeam.None && snapshot.FeverOn(localTeam);
            float low = config != null ? config.TeamFeverGlowMin : GlowLow, high = config != null ? config.TeamFeverGlowMax : GlowHigh;
            float reach = config != null ? config.TeamFeverGlowReach : GlowReach;
            if (!Mathf.Approximately(reach, glowReach))
            {
                glowReach = reach;
                glow.sprite = GlowSprite(reach);
            }
            float breathe = low + (high - low) * (.5f + .5f * Mathf.Sin(Time.unscaledTime * GlowHz * 2f * Mathf.PI));
            glowAlpha = mine ? breathe : Mathf.MoveTowards(glowAlpha, 0f, delta * Mathf.Max(.01f, high) / GlowFadeSeconds);
            Color team = localTeam == LabTeam.Red ? LabTeamBar.Red : LabTeamBar.Blue;
            glow.color = new Color(team.r, team.g, team.b, glowAlpha);
            if (glow.gameObject.activeSelf != glowAlpha > 0f) glow.gameObject.SetActive(glowAlpha > 0f);
        }

        public void Hide()
        {
            bannerAge = float.PositiveInfinity;
            glowAlpha = 0f;
            if (banner != null) banner.gameObject.SetActive(false);
            if (glow != null) glow.gameObject.SetActive(false);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // The glow is a sibling under the battle HUD, so it goes with this component.
        private void OnDestroy()
        {
            if (glow != null) Destroy(glow.gameObject);
        }

        /// <summary>Only this player's own team fever gets the banner, the same words for Blue and Red.</summary>
        private void Announce(LabSnapshot snapshot, LabTeam team, double startedAt, LabTeam localTeam)
        {
            int index = (int)team;
            if (startedAt < 0d || startedAt == announced[index]) return;
            announced[index] = startedAt;
            if (team != localTeam || snapshot.serverTime - startedAt > AnnounceWindowSeconds) return;
            bannerTeam = team;
            bannerAge = 0f;
            Color color = team == LabTeam.Blue ? LabTeamBar.Blue : LabTeamBar.Red;
            title.text = BannerTitle;
            title.color = color;
            subtitle.text = BannerLine;
            subtitle.color = color;
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();
        }

        private void HideBanner()
        {
            bannerAge = float.PositiveInfinity;
            if (banner != null) banner.gameObject.SetActive(false);
        }

        private void AdvanceBanner(float delta)
        {
            if (float.IsPositiveInfinity(bannerAge)) return;
            bannerAge += delta;
            if (bannerAge >= BannerSeconds)
            {
                bannerAge = float.PositiveInfinity;
                banner.gameObject.SetActive(false);
                return;
            }
            // Pops past full size, settles, then fades at the end.
            float pop = Mathf.Clamp01(bannerAge / PopSeconds);
            float scale = pop < 1f ? Mathf.Lerp(.6f, 1.15f, pop) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((bannerAge - PopSeconds) / .15f));
            banner.localScale = Vector3.one * scale;
            float alpha = Mathf.Clamp01((BannerSeconds - bannerAge) / FadeSeconds);
            title.color = new Color(title.color.r, title.color.g, title.color.b, alpha);
            subtitle.color = new Color(subtitle.color.r, subtitle.color.g, subtitle.color.b, alpha);
        }

        private void Build(Font font)
        {
            // The glow lies under every other battle HUD element; only the banner sits on top.
            glow = Part<Image>("Fever Edge Glow", transform.parent);
            Stretch(glow.rectTransform);
            glow.sprite = GlowSprite();
            glow.gameObject.SetActive(false);
            glow.transform.SetAsFirstSibling();

            var bannerObject = new GameObject("Fever Banner", typeof(RectTransform));
            bannerObject.layer = gameObject.layer;
            banner = (RectTransform)bannerObject.transform;
            banner.SetParent(transform, false);
            banner.anchorMin = banner.anchorMax = new Vector2(.5f, .62f);
            banner.sizeDelta = new Vector2(360f, 110f);
            title = Words("Title", font, 46, new Vector2(0f, 14f));
            subtitle = Words("Subtitle", font, 20, new Vector2(0f, -28f));
            banner.gameObject.SetActive(false);
        }

        private Text Words(string name, Font font, int size, Vector2 offset)
        {
            Text text = Part<Text>(name, banner);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(360f, size + 12f);
            rect.anchoredPosition = offset;
            text.font = font; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, .98f, .93f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>White at the screen's edges, fading to clear well before its middle. Fever time (#334) shares it.</summary>
        internal static Sprite GlowSprite() => glowSprite != null ? glowSprite : glowSprite = CreateGlowSprite(GlowReach);

        /// <summary>The same glow reaching another share of the screen inward (#293), made once per reach.</summary>
        private static Sprite GlowSprite(float reach)
        {
            if (Mathf.Approximately(reach, GlowReach)) return GlowSprite();
            int key = Mathf.RoundToInt(reach * 1000f);
            if (reachSprites.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            return reachSprites[key] = CreateGlowSprite(reach);
        }

        private static Sprite CreateGlowSprite(float reach)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size, v = (y + .5f) / size;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    // Solid at the very edge and fading inward slowly, so the colour reads clearly around the screen.
                    float alpha = Mathf.Pow(1f - Mathf.Clamp01(edge / Mathf.Max(.01f, reach)), 1.4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Lab team fever glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var created = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            created.name = "Lab team fever glow";
            created.hideFlags = HideFlags.DontSave;
            return created;
        }
    }
}
