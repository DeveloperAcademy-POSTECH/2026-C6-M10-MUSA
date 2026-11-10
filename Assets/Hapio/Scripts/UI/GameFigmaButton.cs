using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Presentation only: existing Button, hit bounds, colours and navigation remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFigmaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string kind;
        public Button button;
        public Graphic original;
        public GameFigmaSurface artwork;
        private bool pressed;

        public static void Attach(Button target, string kind)
        {
            if (target == null || GameFigmaStrokeLibrary.Find(kind == "Room" ? "RoomDark" : kind == "Seal" ? "SealWait" : kind) == null) return;
            var part = target.GetComponent<GameFigmaButton>() ?? target.gameObject.AddComponent<GameFigmaButton>();
            part.kind = kind; part.button = target; part.original = target.targetGraphic;
            if (part.artwork == null)
            {
                var go = new GameObject("Figma Brush Surface", typeof(RectTransform), typeof(GameFigmaSurface));
                go.layer = target.gameObject.layer; go.transform.SetParent(target.transform,false); go.transform.SetAsFirstSibling();
                part.artwork = go.GetComponent<GameFigmaSurface>();
                var r = part.artwork.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
            }
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

        public void Refresh()
        {
            if (button == null || original == null || artwork == null) return;
            string name = kind;
            if (kind == "Chip") name = original.color.a > .9f ? "ChipSelected" : "Chip";
            else if (kind == "Room") name = original.color.a < .6f ? "RoomDark" : "RoomLight";
            else if (kind == "Mode") name = button.interactable ? "Mode" : "ModeDisabled";
            else if (kind == "Seal") name = button.interactable ? "SealReady" : "SealWait";
            if (kind == "Room" && pressed && button.IsInteractable()) name += "Pressed";
            artwork.Show(name);
        }
        private void LateUpdate() => Refresh();
        private void OnDisable() { pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { pressed = true; Refresh(); } }
        public void OnPointerUp(PointerEventData e) { pressed = false; Refresh(); }
        public void OnPointerExit(PointerEventData e) { pressed = false; Refresh(); }
    }
}
