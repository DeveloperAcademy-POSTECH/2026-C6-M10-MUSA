using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Original Figma surfaces only; result values, winner flags and navigation remain owned by their views.</summary>
    public static class GameResultStrokes
    {
        // Figma 4784:3260, TextColor/DarkRed/600 and /700, SecondaryLightIvory/100.
        internal static readonly Color ActionNormal = new Color32(140, 57, 45, 255);
        internal static readonly Color ActionPressed = new Color32(105, 43, 33, 255);
        // Figma 4784:3256, TextColor/DarkRed/400.
        internal static readonly Color SecondaryActionNormal = new Color32(191, 108, 96, 255);
        private static readonly Color ActionText = new Color32(250, 246, 241, 255);

        public static void Apply(GameOverCharacterView view) => GameFigmaButton.Attach(view.homeButton, "SealReady");

        public static void Apply(GameTerritoryResultView view)
        {
            ApplyAction(view.table.viewerButton, secondary: false);
            ApplyAction(view.table.nextButton, secondary: true);
            GameFigmaButton.AttachResultText(view.table.homeButton);
            if (GameFigmaStrokeLibrary.Find("ResultFill") == null) return;
            var frame = (RectTransform)view.table.design.Find("TerritoryBarFrame");
            var track = (RectTransform)view.blueFill.parent;
            if (track.name == "Brush Fill Clip") track = (RectTransform)track.parent;
            var oldTrack = track.GetComponent<Image>();
            var clip = frame.Find("Brush Fill Clip") as RectTransform ?? track.Find("Brush Fill Clip") as RectTransform;
            if (clip == null)
            {
                clip = Child(frame,"Brush Fill Clip");
                var shape = clip.gameObject.AddComponent<LabRoundedGraphic>();
                shape.radius = 8; shape.color = oldTrack.color; shape.raycastTarget = false;
                clip.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            }
            clip.SetParent(frame,false);
            clip.anchorMin=Vector2.zero; clip.anchorMax=Vector2.one; clip.offsetMin=clip.offsetMax=Vector2.zero;
            clip.GetComponent<LabRoundedGraphic>().radius=8;
            view.blueFill.SetParent(track,false); view.redFill.SetParent(track,false);
            track.SetParent(clip,false); track.anchoredPosition=Vector2.zero;
            // Keep the existing 336-unit share calculation, but fill the logical frame underneath its centred outline.
            track.localScale=new Vector3(342f/336f,27f/21f,1);
            clip.SetAsFirstSibling();
            oldTrack.enabled = false;
            Fill(view.blueFill); Fill(view.redFill);
            var original = frame.GetComponent<Image>();
            var outline = Surface(frame,"Brush Outline","TeamPaint",original.color);
            outline.transform.SetSiblingIndex(clip.GetSiblingIndex()+1);
            original.enabled = false;
        }

        private static void ApplyAction(Button button, bool secondary)
        {
            if (button == null || button.targetGraphic == null) return;
            // The Figma artwork owns the transition. Keep the original Image untinted for fallback rendering.
            button.transition = Selectable.Transition.None;
            button.targetGraphic.color = secondary ? SecondaryActionNormal : ActionNormal;
            button.targetGraphic.canvasRenderer.SetColor(Color.white);
            GameFigmaButton.Attach(button, secondary ? "ResultActionSecondary" : "ResultAction");

            var caption = button.GetComponentInChildren<Text>(true);
            if (caption == null) return;
            caption.color = ActionText;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.resizeTextForBestFit = false;
            caption.fontSize = 40;
            // Render the 20-unit label at twice its nominal resolution.
            var rect = caption.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(468, 90);
            rect.localScale = new Vector3(.5f, .5f, 1);
        }

        private static void Fill(RectTransform rect)
        {
            var original = rect.GetComponent<Image>();
            Surface(rect,"Brush Fill","ResultFill",original.color);
            original.enabled = false;
        }

        private static GameFigmaSurface Surface(RectTransform parent, string name, string key, Color color)
        {
            var rect = parent.Find(name) as RectTransform ?? Child(parent,name);
            var surface = rect.GetComponent<GameFigmaSurface>() ?? rect.gameObject.AddComponent<GameFigmaSurface>();
            surface.Show(key); surface.color = color; surface.raycastTarget = false;
            return surface;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer; rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            return rect;
        }
    }
}
