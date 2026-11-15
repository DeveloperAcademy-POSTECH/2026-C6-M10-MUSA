using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Presentation only: existing Button, hit bounds, colours and navigation remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFigmaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public string kind;
        public Button button;
        public Graphic original;
        public GameFigmaSurface artwork;
        // PrimaryBrown/700 and SecondaryLightIvory/100.
        private static readonly Color ChipInk = new Color32(49, 43, 35, 255);
        private static readonly Color LightInk = new Color32(250, 246, 241, 255);
        private Text[] labels;
        private Color[] normalLabelColors;
        private int? heldPointer;
        private bool pointerInside;
        private GameFigmaStrokeLibrary.Surface artworkSource;
        private Texture artworkTexture;
        private C6Lab.LabRoundedGraphic textFill;
        private Text textCaption;
        private Graphic textArrow;
        private GameObject refreshFallbackGlyph;
        private Color textBaseColor, arrowBaseColor;
        private static readonly Color TextPressedFill = new Color32(239, 218, 215, 255); // DarkRed/100

        public static void AttachClose(Button target)
        {
            if (target == null || !(target.targetGraphic is Image image) || image.sprite == null) return;
            Sprite pressed = GameFigmaStrokeLibrary.Find("ClosePressed")?.sprite;
            if (pressed == null) return;
            target.spriteState = new SpriteState { pressedSprite = pressed };
            target.transition = Selectable.Transition.SpriteSwap;
        }

        internal static void AttachResultArtwork(Button target, Graphic background)
        {
            if (target == null || background == null) return;
            var part = target.GetComponent<GameFigmaButton>() ?? target.gameObject.AddComponent<GameFigmaButton>();
            part.kind = "ResultAction";
            part.button = target;
            part.original = background;
            part.EnsureArtwork(background.transform);
            part.artwork.raycastTarget = false;
            part.Refresh();
        }

        internal static void AttachResultText(Button target, Text caption = null, Graphic arrow = null)
        {
            if (target == null) return;
            if (caption == null) caption = target.GetComponentInChildren<Text>(true);
            if (caption == null) return;
            var part = target.GetComponent<GameFigmaButton>() ?? target.gameObject.AddComponent<GameFigmaButton>();
            if (part.textFill != null) return;
            part.button = target;
            part.kind = "ResultText";
            part.textCaption = caption;
            part.textBaseColor = caption.color;
            part.textArrow = arrow;
            part.arrowBaseColor = arrow != null ? arrow.color : Color.white;
            var rect = GameViewerHelpUi.Rect(target.transform, "Pressed Text Surface", 0, 0, 0, 0);
            if (!caption.transform.IsChildOf(target.transform))
            {
                // Authored story captions are sometimes sibling layers of the invisible hit area.
                Transform layer = caption.transform.parent;
                rect.SetParent(layer.parent, false);
                rect.pivot = Vector2.one * .5f;
                rect.SetSiblingIndex(layer.GetSiblingIndex());
            }
            else
            {
                GameViewerHelpUi.Stretch(rect);
                rect.SetAsFirstSibling();
            }
            part.textFill = rect.gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
            part.textFill.radius = 14;
            part.textFill.color = TextPressedFill;
            part.textFill.raycastTarget = false;
            part.Refresh();
        }

        internal void SetResultTextBaseColor(Color color)
        {
            textBaseColor = color;
            Refresh();
        }

        private void RefreshResultText(bool pressed)
        {
            if (textFill == null || textCaption == null) return;
            if (pressed && textFill.transform.parent != button.transform)
            {
                var hit = (RectTransform)button.transform;
                var parent = textFill.transform.parent;
                var min = parent.InverseTransformPoint(hit.TransformPoint(hit.rect.min));
                var max = parent.InverseTransformPoint(hit.TransformPoint(hit.rect.max));
                textFill.rectTransform.localPosition = (min + max) * .5f;
                textFill.rectTransform.sizeDelta = (Vector2)(max - min);
            }

            textFill.enabled = pressed;
            textCaption.color = pressed ? GameResultStrokes.ActionNormal : textBaseColor;
            if (textArrow != null) textArrow.color = pressed ? GameResultStrokes.ActionNormal : arrowBaseColor;
        }

        private void EnsureArtwork(Transform parent)
        {
            if (artwork != null) return;
            var surface = new GameObject("Figma Brush Surface", typeof(RectTransform), typeof(GameFigmaSurface));
            surface.layer = parent.gameObject.layer;
            surface.transform.SetParent(parent, false);
            surface.transform.SetAsFirstSibling();
            artwork = surface.GetComponent<GameFigmaSurface>();
            var rect = artwork.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static void Attach(Button target, string kind)
        {
            if (target == null || target.targetGraphic == null) return;
            string defaultKey = kind == "Room" ? "RoomDark" : kind == "Seal" ? "SealWait" : kind;
            if (kind != "ResultActionSecondary" && GameFigmaStrokeLibrary.Find(defaultKey) == null) return;
            var part = target.GetComponent<GameFigmaButton>() ?? target.gameObject.AddComponent<GameFigmaButton>();
            part.kind = kind; part.button = target; part.original = target.targetGraphic;
            part.EnsureArtwork(target.transform);
            // The child's logical rect receives the same clicks, while its mesh includes the original outside stroke.
            if (part.artwork.GetComponent<CanvasRenderer>() == null) part.artwork.gameObject.AddComponent<CanvasRenderer>();
            part.artwork.raycastTarget = true; part.original.enabled = false; part.Refresh();
        }

        public static void Apply(GameLobbyView view)
        {
            Attach(view.enterButton,"Start"); Attach(view.createButton,"Create");
            foreach (var button in view.countButtons) Attach(button,"Chip");
            Attach(view.roomTemplate,"Room"); Attach(view.basicModeButton,"Mode"); Attach(view.teamModeButton,"Mode");
            Attach(view.sealButton,"Seal");
            AttachRefresh(view.refreshButton);
            var scroll = view.roomTemplate.GetComponentInParent<ScrollRect>(true);
            if (scroll != null && scroll.viewport != null)
            {
                var stencil = scroll.viewport.GetComponent<Mask>();
                if (stencil != null) stencil.enabled = false;
                var background = scroll.viewport.GetComponent<Graphic>();
                if (background != null) background.color = Color.clear;
                var clip = scroll.viewport.GetComponent<RectMask2D>() ?? scroll.viewport.gameObject.AddComponent<RectMask2D>();
                clip.padding = new Vector4(-4,-4,-4,-4);
            }
            // Keep scroll and click bounds, allow centred strokes to extend beyond the first/last item.
            foreach (var mask in view.GetComponentsInChildren<RectMask2D>(true))
                if (view.roomTemplate.transform.IsChildOf(mask.transform) ||
                    view.countButtons.Length > 0 && view.countButtons[0].transform.IsChildOf(mask.transform))
                    mask.padding = new Vector4(-4,-4,-4,-4);
        }

        private static void AttachRefresh(Button target)
        {
            if (target == null || GameFigmaStrokeLibrary.Find("Refresh") == null) return;
            C6Lab.LabLobbyView.Place((RectTransform)target.transform, 336, 437, 39, 38);
            var label = target.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            var glyph = target.transform.Find("RefreshGlyph");
            if (glyph != null) GameViewerHelpUi.Stretch((RectTransform)glyph);
            Attach(target, "Refresh");
            var part = target.GetComponent<GameFigmaButton>();
            part.refreshFallbackGlyph = glyph != null ? glyph.gameObject : null;
            part.Refresh();
        }

        public void Refresh()
        {
            if (button == null) return;
            bool enabled = button.IsActive() && button.IsInteractable();
            if (!enabled) heldPointer = null;
            bool pressed = heldPointer.HasValue && pointerInside;
            if (kind == "ResultText")
            {
                RefreshResultText(pressed);
                return;
            }
            if (original == null || artwork == null) return;

            string name = kind;
            switch (kind)
            {
                case "Chip":
                    bool selected = original.color.a > .9f;
                    name = selected || pressed ? "ChipPressed" : "Chip";
                    SetLabelColors(selected || pressed, ChipInk);
                    break;
                case "Room":
                    name = original.color.a < .6f ? "RoomDark" : "RoomLight";
                    if (pressed) name += "Pressed";
                    SetLabelColors(pressed);
                    break;
                case "Mode":
                    name = !button.IsInteractable() ? "ModeDisabled" : pressed ? "ModePressed" : "Mode";
                    break;
                case "Seal":
                    name = !button.IsInteractable() ? "SealWait" : pressed ? "SealReadyPressed" : "SealReady";
                    break;
                case "Start":
                case "Create":
                case "Refresh":
                case "SealReady":
                    if (pressed) name += "Pressed";
                    break;
                case "ResultAction":
                    ShowResultArtwork(pressed, secondary: false);
                    return;
                case "ResultActionSecondary":
                    ShowResultArtwork(pressed, secondary: true);
                    return;
            }
            bool hasArtwork = ShowDrawableArtwork(name);
            original.enabled = !hasArtwork;
            if (refreshFallbackGlyph != null) refreshFallbackGlyph.SetActive(!hasArtwork);
            if (!hasArtwork && (kind == "Chip" || kind == "Room"))
                SetLabelColors(false, kind == "Chip" ? ChipInk : (Color?)null);
        }

        private static bool HasDrawableSurface(string key)
        {
            var sprite = GameFigmaStrokeLibrary.Find(key)?.sprite;
            return sprite != null && sprite.texture != null;
        }

        private void ShowResultArtwork(bool pressed, bool secondary)
        {
            // The secondary default is the plain capsule. Its pressed stroke matches the primary default.
            string key = secondary ? (pressed ? "ResultAction" : null)
                : pressed && HasDrawableSurface("ResultActionPressed") ? "ResultActionPressed" : "ResultAction";
            bool hasArtwork = ShowDrawableArtwork(key);
            if (original != button.targetGraphic) original.raycastTarget = false;
            original.enabled = !hasArtwork;
            if (!hasArtwork)
            {
                original.color = secondary
                    ? (pressed ? GameResultStrokes.ActionNormal : GameResultStrokes.SecondaryActionNormal)
                    : (pressed ? GameResultStrokes.ActionPressed : GameResultStrokes.ActionNormal);
            }
        }

        private bool ShowDrawableArtwork(string key)
        {
            var source = key != null ? GameFigmaStrokeLibrary.Find(key) : null;
            var sprite = source?.sprite;
            Texture texture = sprite != null ? sprite.texture : null;
            if (key != null)
            {
                if (artwork.SurfaceName != key) artwork.Show(key);
                else if (!ReferenceEquals(artworkSource, source) || !ReferenceEquals(artworkTexture, texture))
                    artwork.ReloadSource();
            }
            artworkSource = source;
            artworkTexture = texture;
            artwork.enabled = texture != null;
            return artwork.enabled;
        }

        private void SetLabelColors(bool light, Color? normal = null)
        {
            if (labels == null)
            {
                labels = button.GetComponentsInChildren<Text>(true);
                normalLabelColors = System.Array.ConvertAll(labels, label => label.color);
            }
            for (int i = 0; i < labels.Length; i++)
                labels[i].color = light ? LightInk : normal ?? normalLabelColors[i];
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void OnDisable()
        {
            heldPointer = null;
            pointerInside = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || heldPointer.HasValue
                || button == null || !button.IsActive() || !button.IsInteractable()) return;
            heldPointer = e.pointerId;
            pointerInside = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (heldPointer != e.pointerId || e.button != PointerEventData.InputButton.Left) return;
            heldPointer = null;
            pointerInside = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (heldPointer != e.pointerId) return;
            pointerInside = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (heldPointer != e.pointerId) return;
            pointerInside = false;
            Refresh();
        }
    }
}
