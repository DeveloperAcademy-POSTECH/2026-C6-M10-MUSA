using System;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #509 지귀 B 화면(각 기기, 스냅샷만 본다). 불기둥 없이 전투 영역 위쪽에 지목된 사람 수만큼 물방울 칸을 보여 준다(지목된 사람과
    /// 도우미 모두). 지목된 사람 중 누가 먼저 박을 맞혀 1초 창이 열리면 아직 안 맞힌 지목자 화면에 "지금!"과 줄어드는
    /// 1초 막대가 뜨고 폰이 한 번 울린다. 창이 어긋나 0/3으로 돌아가면 물방울이 빨갛게 흔들린다. 글자는 띄우지 않는다.
    /// 그림은 코드로 만든 임시 그림이고(최종 그림은 VFX 이슈), 규칙/충돌은 바꾸지 않는다. 모든 Graphic은 터치를 막지 않는다.
    /// (클래스 이름은 처음 불기둥으로 만들 때의 것을 그대로 둔다.)
    /// </summary>
    public sealed class LabJigwiSyncPillarView : MonoBehaviour
    {
        private const float FlashSeconds = .45f, ResetSeconds = .6f;
        /// <summary>#509: one drop per picked seat (at most three are picked).</summary>
        public const int MaxDrops = 3;
        private RectTransform root, dropsRect, barBack;
        private RawImage[] drops;
        private Image bar;
        private Text cue;
        private Texture2D dropTexture, ringTexture;
        private string roundId;
        private int shownCount = -1;
        private double lastWindowAt = -1d, lastResetAt = -1d, filledAt = -1d, resetAt = -1d;
        private bool vibrateForWindow;
        public bool DropsVisible => dropsRect != null && dropsRect.gameObject.activeSelf;
        public bool NowCueVisible { get; private set; }
        public bool ResetCueVisible { get; private set; }
        public int FilledDrops { get; private set; }
        /// <summary>#509: how many drops are shown — the number of picked seats.</summary>
        public int DropCount { get; private set; }
        public int VibrationCount { get; private set; }
        public RectTransform DropsRect => dropsRect;

        public void Build(RectTransform layer, Font font)
        {
            if (root != null) return;
            root = new GameObject("Jigwi Sync Drops (#509)", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(layer, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            dropTexture = Drop("Jigwi Sync Drop (runtime)", false); ringTexture = Drop("Jigwi Sync Drop Empty (runtime)", true);
            dropsRect = new GameObject("Sync Drops", typeof(RectTransform)).GetComponent<RectTransform>();
            dropsRect.SetParent(root, false); dropsRect.anchorMin = dropsRect.anchorMax = new Vector2(.5f, .5f);
            drops = new RawImage[MaxDrops];
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
        /// <paramref name="clip"/> is the battle area in the layer's space. <paramref name="picked"/> is false for a helper,
        /// who sees the same drops but never the "now" cue.
        /// </summary>
        public void Present(LabJigwiABRound round, LabJigwiABPlayerFire mine, bool picked, Rect clip,
            double now, bool held, bool hapticsEnabled)
        {
            if (root == null) return;
            if (round == null || !round.Active(now)) { Hide(); return; }
            if (roundId != round.id)
            {
                roundId = round.id; shownCount = Hits(round); lastWindowAt = round.syncWindowAt;
                lastResetAt = round.syncResetAt; filledAt = resetAt = -1d; vibrateForWindow = false;
            }
            int hits = Hits(round);
            if (hits > shownCount) filledAt = now;
            shownCount = hits;
            if (round.syncResetAt != lastResetAt && round.syncResetAt >= 0d) { resetAt = now; lastResetAt = round.syncResetAt; }
            bool mineHit = mine != null && mine.targets != null && mine.targets.Length > 0 && mine.targets[0].extinguishedAt >= 0d;
            bool windowOpen = round.syncWindowAt >= 0d && now < round.syncWindowAt + LabJigwiABTimeline.GroupSyncWindowSeconds;
            if (round.syncWindowAt != lastWindowAt) { lastWindowAt = round.syncWindowAt; vibrateForWindow = windowOpen && picked && !mineHit; }
            NowCueVisible = picked && windowOpen && !mineHit;
            if (NowCueVisible && vibrateForWindow && !held)
            {
                vibrateForWindow = false; VibrationCount++;
#if UNITY_IOS && !UNITY_EDITOR
                if (hapticsEnabled) Handheld.Vibrate();
#endif
            }

            dropsRect.gameObject.SetActive(true);
            float size = Mathf.Clamp(clip.height * .085f, 26f, 64f);
            float shake = ResetVisible(now) ? Mathf.Sin((float)(now - resetAt) * 60f) * size * .18f : 0f;
            dropsRect.anchoredPosition = new Vector2(clip.center.x + shake, clip.yMax - size * 1.6f);
            // #509: one drop per picked seat; it fills when that seat's special Water hits inside the window.
            DropCount = Mathf.Clamp(round.players?.Length ?? 0, 1, drops.Length);
            FilledDrops = Mathf.Clamp(hits, 0, DropCount);
            ResetCueVisible = ResetVisible(now);
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].gameObject.SetActive(i < DropCount);
                if (i >= DropCount) continue;
                RectTransform rect = drops[i].rectTransform;
                bool filled = i < FilledDrops;
                float pop = filled && i == FilledDrops - 1 && filledAt >= 0d && now - filledAt < FlashSeconds
                    ? 1f + .35f * (1f - (float)((now - filledAt) / FlashSeconds)) : 1f;
                rect.sizeDelta = Vector2.one * size * pop;
                rect.anchoredPosition = new Vector2((i - (DropCount - 1) * .5f) * size * 1.25f, 0f);
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
            cue.rectTransform.anchoredPosition = new Vector2(0f, -size * 1.6f);
            cue.rectTransform.sizeDelta = new Vector2(size * 6f, size);
            // #509: no words beside the drops; the red shake, the one-second bar and the vibration remain.
            cue.text = string.Empty;
            cue.gameObject.SetActive(false);
        }

        private bool ResetVisible(double now) => resetAt >= 0d && now - resetAt < ResetSeconds;

        /// <summary>#509: picked seats whose special Water already hit inside the open window.</summary>
        private static int Hits(LabJigwiABRound round)
        {
            if (round.resolution == LabJigwiABResolution.Completed) return round.players?.Length ?? 0;
            int hits = 0;
            foreach (var player in round.players ?? Array.Empty<LabJigwiABPlayerFire>())
                if (player?.targets != null && player.targets.Length > 0 && player.targets[0].extinguishedAt >= 0d) hits++;
            return hits;
        }

        public void Hide()
        {
            NowCueVisible = ResetCueVisible = false; FilledDrops = 0; DropCount = 0;
            if (dropsRect != null) dropsRect.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var texture in new[] { dropTexture, ringTexture })
                if (texture != null) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
        }

        private static RawImage Raw(string name, Transform parent, Texture texture)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(parent, false); image.texture = texture; image.raycastTarget = false;
            return image;
        }

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
