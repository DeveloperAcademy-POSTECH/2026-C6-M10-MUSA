using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #509 지귀 B 화면(각 기기, 스냅샷만 본다). 지목된 사람 화면에는 박 앞에 임시 불기둥 하나와 그 위 물방울 3칸,
    /// 도우미 화면에는 물방울 3칸만 보여 준다. 누가 먼저 맞혀 1초 창이 열리면 아직 안 맞힌 지목자의 불기둥이
    /// 번쩍이고 남은 시간 막대가 줄어들며 폰이 한 번 울린다. 창이 어긋나 0/3으로 돌아가면 물방울이 흔들린다.
    /// 그림은 코드로 만든 임시 그림이고(최종 그림은 VFX 이슈), 규칙/충돌은 바꾸지 않는다. 모든 Graphic은 터치를 막지 않는다.
    /// </summary>
    public sealed class LabJigwiSyncPillarView : MonoBehaviour
    {
        private const float FlashSeconds = .45f, ResetSeconds = .6f;
        private RectTransform root, pillarRect, dropsRect, barBack;
        private RawImage pillar, pillarGlow;
        private RawImage[] drops;
        private Image bar;
        private Text cue;
        private Texture2D pillarTexture, dropTexture, ringTexture;
        private string roundId;
        private int shownCount = -1;
        private double lastWindowAt = -1d, lastResetAt = -1d, filledAt = -1d, resetAt = -1d;
        private bool vibrateForWindow;
        public bool PillarVisible => pillar != null && pillar.gameObject.activeSelf;
        public bool DropsVisible => dropsRect != null && dropsRect.gameObject.activeSelf;
        public bool NowCueVisible { get; private set; }
        public bool ResetCueVisible { get; private set; }
        public int FilledDrops { get; private set; }
        public int VibrationCount { get; private set; }
        public RectTransform PillarRect => pillarRect;

        public void Build(RectTransform layer, Font font)
        {
            if (root != null) return;
            root = new GameObject("Jigwi Sync Pillar (#509)", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(layer, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            pillarTexture = PillarTexture(); dropTexture = DropTexture(); ringTexture = RingTexture();
            pillar = Raw("Pillar", root, pillarTexture);
            pillarRect = pillar.rectTransform; pillarRect.anchorMin = pillarRect.anchorMax = new Vector2(.5f, .5f);
            pillarGlow = Raw("Pillar Glow", pillarRect, pillarTexture);
            Stretch(pillarGlow.rectTransform, new Vector2(-.12f, -.04f), new Vector2(1.12f, 1.04f));
            dropsRect = new GameObject("Sync Drops", typeof(RectTransform)).GetComponent<RectTransform>();
            dropsRect.SetParent(root, false); dropsRect.anchorMin = dropsRect.anchorMax = new Vector2(.5f, .5f);
            drops = new RawImage[LabJigwiABTimeline.GroupSyncRequired];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = Raw("Drop " + (i + 1), dropsRect, dropTexture);
                RectTransform rect = drops[i].rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            }
            barBack = new GameObject("Window Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            barBack.SetParent(dropsRect, false); barBack.anchorMin = barBack.anchorMax = new Vector2(.5f, .5f);
            Image back = barBack.GetComponent<Image>(); back.color = new Color(0f, 0f, 0f, .35f); back.raycastTarget = false;
            bar = new GameObject("Window Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.rectTransform.SetParent(barBack, false); bar.raycastTarget = false;
            bar.color = new Color(.45f, .85f, 1f, .95f);
            bar.rectTransform.anchorMin = Vector2.zero; bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.pivot = new Vector2(0f, .5f); bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            cue = new GameObject("Sync Cue", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            cue.rectTransform.SetParent(dropsRect, false); cue.raycastTarget = false;
            cue.alignment = TextAnchor.MiddleCenter; cue.font = font; cue.fontStyle = FontStyle.Bold;
            cue.horizontalOverflow = HorizontalWrapMode.Overflow; cue.verticalOverflow = VerticalWrapMode.Overflow;
            cue.rectTransform.anchorMin = cue.rectTransform.anchorMax = new Vector2(.5f, .5f);
            Outline outline = cue.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0f, 0f, 0f, .8f);
            Hide();
        }

        public void SetFont(Font font) { if (cue != null && font != null) cue.font = font; }

        /// <summary>
        /// <paramref name="area"/> is the projected curtain frame (covers the boss) in the layer's space, or null when
        /// it cannot be projected. A helper passes picked = false and sees only the drops.
        /// </summary>
        public void Present(LabJigwiABRound round, LabJigwiABPlayerFire mine, bool picked, Rect? area, Rect clip,
            double now, bool held, bool hapticsEnabled)
        {
            if (root == null) return;
            if (round == null || !round.Active(now) && !round.Warning(now)) { Hide(); return; }
            if (roundId != round.id)
            {
                roundId = round.id; shownCount = round.syncCount; lastWindowAt = round.syncWindowAt;
                lastResetAt = round.syncResetAt; filledAt = resetAt = -1d; vibrateForWindow = false;
            }
            bool active = round.Active(now);
            if (round.syncCount > shownCount) filledAt = now;
            shownCount = round.syncCount;
            if (round.syncResetAt != lastResetAt && round.syncResetAt >= 0d) { resetAt = now; lastResetAt = round.syncResetAt; }
            bool mineHit = mine != null && mine.targets != null && mine.targets.Length > 0 && mine.targets[0].extinguishedAt >= 0d;
            bool windowOpen = active && round.syncWindowAt >= 0d && now < round.syncWindowAt + LabJigwiABTimeline.GroupSyncWindowSeconds;
            if (round.syncWindowAt != lastWindowAt) { lastWindowAt = round.syncWindowAt; vibrateForWindow = windowOpen && picked && !mineHit; }
            float t = (float)Math.Max(0d, now - round.activeAt);

            // Pillar (picked only). Sits on the fire target's column and covers the boss height.
            Rect frame = area ?? new Rect(clip.center.x - clip.width * .2f, clip.yMin + clip.height * .15f, clip.width * .4f, clip.height * .6f);
            bool showPillar = picked && area.HasValue;
            pillar.gameObject.SetActive(showPillar);
            NowCueVisible = picked && windowOpen && !mineHit;
            if (showPillar)
            {
                float width = Mathf.Min(frame.width * .62f, frame.height * .5f), height = frame.height * 1.05f;
                float sway = held ? 0f : Mathf.Sin(t * 5.3f) * .015f;
                pillarRect.sizeDelta = new Vector2(width * (1f + sway), height);
                pillarRect.anchoredPosition = new Vector2(frame.center.x, frame.center.y);
                pillar.uvRect = new Rect(0f, -(held ? 0f : t * .35f) % 1f, 1f, 1f);
                float pulse = NowCueVisible ? .55f + .45f * Mathf.Abs(Mathf.Sin(t * 14f)) : 0f;
                pillar.color = new Color(1f, 1f, 1f, round.Warning(now) ? .45f : 1f);
                pillarGlow.gameObject.SetActive(pulse > 0f);
                pillarGlow.color = new Color(1f, .95f, .6f, .65f * pulse);
                pillarGlow.uvRect = pillar.uvRect;
            }
            else pillarGlow.gameObject.SetActive(false);
            if (NowCueVisible && vibrateForWindow && !held)
            {
                vibrateForWindow = false; VibrationCount++;
#if UNITY_IOS && !UNITY_EDITOR
                if (hapticsEnabled) Handheld.Vibrate();
#endif
            }

            // Drops above the pillar (or the battle centre for a helper).
            dropsRect.gameObject.SetActive(active);
            float size = Mathf.Clamp(clip.height * .085f, 26f, 64f);
            float top = showPillar ? Mathf.Min(frame.yMax + size * .9f, clip.yMax - size * .7f) : clip.yMax - size * 1.6f;
            float shake = ResetVisible(now) ? Mathf.Sin((float)(now - resetAt) * 60f) * size * .18f : 0f;
            dropsRect.anchoredPosition = new Vector2(frame.center.x + shake, top);
            FilledDrops = Mathf.Clamp(round.syncCount, 0, drops.Length);
            ResetCueVisible = ResetVisible(now);
            for (int i = 0; i < drops.Length; i++)
            {
                RectTransform rect = drops[i].rectTransform;
                bool filled = i < FilledDrops;
                float pop = filled && i == FilledDrops - 1 && filledAt >= 0d && now - filledAt < FlashSeconds
                    ? 1f + .35f * (1f - (float)((now - filledAt) / FlashSeconds)) : 1f;
                rect.sizeDelta = Vector2.one * size * pop;
                rect.anchoredPosition = new Vector2((i - (drops.Length - 1) * .5f) * size * 1.25f, 0f);
                drops[i].texture = filled ? dropTexture : ringTexture;
                drops[i].color = ResetCueVisible ? new Color(1f, .45f, .4f, .95f)
                    : filled ? new Color(.35f, .75f, 1f, 1f) : new Color(1f, 1f, 1f, .8f);
            }
            barBack.gameObject.SetActive(windowOpen);
            barBack.sizeDelta = new Vector2(size * 3.6f, Mathf.Max(5f, size * .14f));
            barBack.anchoredPosition = new Vector2(0f, -size * .85f);
            if (windowOpen)
            {
                float left = Mathf.Clamp01((float)((round.syncWindowAt + LabJigwiABTimeline.GroupSyncWindowSeconds - now)
                    / LabJigwiABTimeline.GroupSyncWindowSeconds));
                bar.rectTransform.anchorMax = new Vector2(left, 1f);
            }
            cue.fontSize = Mathf.RoundToInt(size * .62f);
            cue.rectTransform.anchoredPosition = new Vector2(0f, size * .95f);
            cue.rectTransform.sizeDelta = new Vector2(size * 6f, size);
            if (ResetCueVisible) { cue.text = "어긋났어요!"; cue.color = new Color(1f, .55f, .5f); }
            else if (NowCueVisible) { cue.text = "지금!"; cue.color = new Color(1f, .93f, .45f); }
            else if (windowOpen && picked) { cue.text = "기다려요…"; cue.color = new Color(.8f, .9f, 1f); }
            else cue.text = string.Empty;
            cue.gameObject.SetActive(cue.text.Length > 0);
        }

        private bool ResetVisible(double now) => resetAt >= 0d && now - resetAt < ResetSeconds;

        public void Hide()
        {
            NowCueVisible = ResetCueVisible = false; FilledDrops = 0;
            if (pillar != null) pillar.gameObject.SetActive(false);
            if (dropsRect != null) dropsRect.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var texture in new[] { pillarTexture, dropTexture, ringTexture })
                if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }

        private static RawImage Raw(string name, Transform parent, Texture texture)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(parent, false); image.texture = texture; image.raycastTarget = false;
            return image;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        /// <summary>Temporary flame column: hot yellow core, orange body, ragged red edges, tongues at the top. Tiles vertically.</summary>
        private static Texture2D PillarTexture()
        {
            const int w = 64, h = 256;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi Sync Pillar (runtime)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                float wave = .06f * Mathf.Sin(v * 6.283f * 3f) + .035f * Mathf.Sin(v * 6.283f * 7f + 1.3f);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w;
                    float d = Mathf.Abs(u - .5f + wave) / .46f; // 0 centre, 1 edge
                    float tongue = .08f * Mathf.Sin(v * 6.283f * 5f + u * 9f);
                    float a = Mathf.Clamp01((1f - d + tongue) * 3f);
                    if (a <= 0f) { pixels[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    Color edge = new Color(.78f, .12f, .05f), body = new Color(1f, .45f, .08f), core = new Color(1f, .9f, .45f);
                    Color c = d > .55f ? Color.Lerp(body, edge, (d - .55f) / .45f) : Color.Lerp(core, body, d / .55f);
                    c.a = a * .95f;
                    pixels[y * w + x] = c;
                }
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        private static Texture2D DropTexture() => Drop("Jigwi Sync Drop (runtime)", false);
        private static Texture2D RingTexture() => Drop("Jigwi Sync Drop Empty (runtime)", true);
        /// <summary>A water drop (round bottom, pointed top); <paramref name="outline"/> draws only its rim.</summary>
        private static Texture2D Drop(string name, bool outline)
        {
            const int s = 64;
            var texture = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + .5f) / s - .5f, v = (y + .5f) / s;
                    // Signed distance-ish: circle at the bottom, narrowing cone to the tip.
                    float radius = v < .42f ? .3f : Mathf.Lerp(.3f, 0f, (v - .42f) / .52f);
                    float dx = Mathf.Abs(u), dy = v < .42f ? .42f - v : 0f;
                    float inside = v < .42f ? .3f - Mathf.Sqrt(dx * dx + dy * dy) : radius - dx;
                    float a = Mathf.Clamp01(inside * s * .5f);
                    if (outline) a *= Mathf.Clamp01((.06f - inside) * s * .5f);
                    float shine = !outline && u < -.05f && v > .25f && v < .45f ? .35f : 0f;
                    pixels[y * s + x] = new Color(1f, 1f, 1f, a) + new Color(shine, shine, shine, 0f);
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }
    }
}
