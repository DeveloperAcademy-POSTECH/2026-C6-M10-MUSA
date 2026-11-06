using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 금옥이 화면 미니 창 (#477 4단계): 북을 오른쪽으로 넘기면, 금옥이 말풍선 자리(꼬리가 금옥이 얼굴)에 창이 커지며 뜨고
    /// 그 안에 금옥이 판이 보인다. 판 그림은 실제 판(OrbBoardPlate)의 그림에서 북이 들어오는 쪽(왼쪽 끝 ~ 멈출 자리 조금 너머,
    /// 넘긴 높이)을 잘라 쓴다. 북은 실제 판의 북 그림으로, 실제 판이 넘겨받은 북을 멈추는 것과 같은 빠르기·감속으로 왼쪽 끝에서
    /// 미끄러져 들어와 금옥이 판의 멈출 자리(모델의 stopAt)에서 멈춘다. 잠깐 머문 뒤 창이 사라지고 "고마워!"가 같은 자리에 뜬다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private RectTransform mirror;
        private CanvasGroup mirrorGroup;
        private RectTransform mirrorView;
        private RawImage mirrorBoard;
        private Image mirrorDrum;
        private float mirrorAt = -1f;
        private float mirrorDuration;
        private float mirrorGlide;
        // The part of her board in the window, the drum's path and its size: all in board (world) units.
        private Rect mirrorRegion;
        private Vector2 mirrorFrom, mirrorTo;
        private float mirrorDrumSize;

        /// <summary>How long the mini window stays up for the current pass (0 before any).</summary>
        public float MirrorDuration => mirrorDuration;

        private void BuildMirror()
        {
            mirror = new GameObject("Geumok Mirror", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            mirror.SetParent(overlay, false);
            Center(mirror);
            mirror.sizeDelta = art.bubbleSize;
            // 금옥이's bubble: the art mirrored, its tail at the bottom right touching her face.
            mirror.pivot = new Vector2(1f - art.tailTip.x, art.tailTip.y);
            mirrorGroup = mirror.GetComponent<CanvasGroup>();
            mirrorGroup.blocksRaycasts = mirrorGroup.interactable = false;
            Image frame = AddImage("Bubble Art", mirror, Color.white, art.bubble, false);
            Stretch(frame.rectTransform);
            frame.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            mirrorView = new GameObject("Her Board", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            mirrorView.SetParent(mirror, false);
            Vector4 inset = art.textInset;
            mirrorView.anchorMin = new Vector2(inset.z, inset.y);
            mirrorView.anchorMax = new Vector2(1f - inset.x, 1f - inset.w);
            mirrorView.offsetMin = mirrorView.offsetMax = Vector2.zero;
            mirrorBoard = new GameObject("Board Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mirrorBoard.rectTransform.SetParent(mirrorView, false);
            Stretch(mirrorBoard.rectTransform);
            mirrorBoard.raycastTarget = false;
            mirrorDrum = AddImage("Arriving Drum", mirrorView, Color.white, null, false);
            mirrorDrum.preserveAspect = true;
            mirror.gameObject.SetActive(false);
        }

        /// <summary>The drum has just gone to 금옥이: show it arriving on her board.</summary>
        private void StartMirror(LabOrbState drum)
        {
            LabConfig config = session.Network.config;
            Rect area = board.CenterBounds;
            float width = Mathf.Max(.0001f, area.width);
            float orbRadius = config != null ? config.OrbRadius : width * .05f;
            float drumRadius = config != null ? config.DrumRadius : width * .08f;
            // Her board takes it at its left edge and brakes it to rest stopAt of the width in, as a real board does.
            float stop = drum.stopAt >= 0f ? Mathf.Clamp01(drum.stopAt) : .5f;
            float speed = LabOrbBoard.StopArrivalSpeed(drum.vx, stop);
            mirrorGlide = MirrorGlideSeconds(stop, speed);
            mirrorDuration = MirrorTotal(art.popSeconds, mirrorGlide, art.mirrorHoldSeconds, art.mirrorFadeSeconds);
            float y = area.yMin + Mathf.Clamp01(drum.y) * area.height;
            mirrorFrom = new Vector2(area.xMin, y);
            mirrorTo = new Vector2(area.xMin + stop * width, y);
            mirrorDrumSize = 2f * drumRadius;

            // From her screen's left edge to a little past where it will rest, at the height it was passed.
            Vector2 view = MirrorViewSize();
            float shown = Mathf.Max(art.mirrorViewMinWidth, stop + art.mirrorViewPastStop) * width;
            float tall = shown * view.y / Mathf.Max(1f, view.x);
            float left = area.xMin - orbRadius;
            // The drum passes low in the window, so more of her board shows above it, toward its rim.
            float bottom = y - tall * art.mirrorDrumHeightInView;
            Texture paper = BoardPaper(out Rect plate);
            if (plate.height > tall) bottom = Mathf.Clamp(bottom, plate.yMin, plate.yMax - tall);
            mirrorRegion = new Rect(left, bottom, shown, tall);
            mirrorBoard.texture = paper;
            mirrorBoard.color = paper != null ? Color.white : art.mirrorColor;
            mirrorBoard.uvRect = paper != null && plate.width > 0f && plate.height > 0f
                ? new Rect((left - plate.xMin) / plate.width, (bottom - plate.yMin) / plate.height,
                    shown / plate.width, tall / plate.height)
                : new Rect(0f, 0f, 1f, 1f);
            mirrorDrum.sprite = MirrorDrumSprite(config);

            mirrorAt = Now;
            mirror.gameObject.SetActive(true);
            PlaceMirror();
        }

        /// <summary>The board's own paper art and where it lies (world): the plate that fills the board camera.</summary>
        private Texture BoardPaper(out Rect plate)
        {
            plate = default;
            LabOrbAreaProfile profile = board.GetComponentInChildren<LabOrbAreaProfile>(true);
            Transform holder = profile != null ? profile.transform : board.transform.Find("OrbBoardPlate");
            Renderer renderer = holder != null ? holder.GetComponent<Renderer>() : null;
            if (renderer == null) return null;
            Bounds bounds = renderer.bounds;
            plate = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            Texture texture = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
            return texture != null ? texture : profile != null ? profile.SourceTexture : null;
        }

        private Sprite MirrorDrumSprite(LabConfig config)
        {
            if (art.mirrorDrum != null) return art.mirrorDrum;
            LabOrbAppearance look = config != null && config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            if (look != null && look.DrumSprite != null) return look.DrumSprite;
            LabDrumMinimap minimap = FindInBattle<LabDrumMinimap>();
            return minimap != null ? minimap.drumIconSprite : null;
        }

        private Vector2 MirrorViewSize()
        {
            Vector4 inset = art.textInset;
            return new Vector2(art.bubbleSize.x * (1f - inset.x - inset.z), art.bubbleSize.y * (1f - inset.y - inset.w));
        }

        private void PlaceMirror()
        {
            if (mirrorAt < 0f) return;
            float elapsed = Now - mirrorAt;
            if (elapsed >= mirrorDuration)
            {
                mirrorAt = -1f;
                mirror.gameObject.SetActive(false);
                return;
            }
            MirrorWindow(elapsed, art.popSeconds, mirrorGlide, art.mirrorHoldSeconds, art.mirrorFadeSeconds, art.popOvershoot,
                out float scale, out float alpha, out float share);
            mirror.anchoredPosition = KeepOnScreen(TailTip(GameTutorialSpeaker.Geumok), mirror.pivot, art.bubbleSize);
            mirror.localScale = Vector3.one * scale;
            mirrorGroup.alpha = alpha;
            Vector2 view = MirrorViewSize();
            Vector2 world = Vector2.Lerp(mirrorFrom, mirrorTo, share);
            RectTransform drum = mirrorDrum.rectTransform;
            drum.anchoredPosition = new Vector2(
                ((world.x - mirrorRegion.xMin) / mirrorRegion.width - .5f) * view.x,
                ((world.y - mirrorRegion.yMin) / mirrorRegion.height - .5f) * view.y);
            drum.sizeDelta = Vector2.one * (mirrorDrumSize / mirrorRegion.width * view.x);
        }

        /// <summary>Seconds a drum entering at <paramref name="speed"/> takes to brake evenly to rest <paramref name="distance"/> in.</summary>
        public static float MirrorGlideSeconds(float distance, float speed) =>
            speed <= 0f ? 0f : 2f * Mathf.Max(0f, distance) / speed;

        public static float MirrorTotal(float pop, float glide, float hold, float fade) =>
            Mathf.Max(0f, pop) + Mathf.Max(0f, glide) + Mathf.Max(0f, hold) + Mathf.Max(0f, fade);

        /// <summary>
        /// The mini window <paramref name="elapsed"/> seconds in: it pops up like a bubble, then the drum slides in braking
        /// evenly (share of its way, 1 - (1 - u)²), it holds, and the window fades.
        /// </summary>
        public static void MirrorWindow(float elapsed, float pop, float glide, float hold, float fade, float overshoot,
            out float scale, out float alpha, out float drumShare)
        {
            scale = pop > 0f && elapsed < pop ? PopScale(elapsed / pop, overshoot) : 1f;
            float slid = elapsed - pop;
            float u = glide > 0f ? Mathf.Clamp01(slid / glide) : 1f;
            drumShare = slid <= 0f ? 0f : 1f - (1f - u) * (1f - u);
            float fadeFrom = pop + glide + hold;
            alpha = elapsed <= fadeFrom ? 1f : fade <= 0f ? 0f : 1f - Mathf.Clamp01((elapsed - fadeFrom) / fade);
        }
    }
}
