using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Original Figma surfaces only; result values, winner flags and navigation remain owned by their views.</summary>
    public static class GameResultStrokes
    {
        public static void Apply(GameOverCharacterView view) => GameFigmaButton.Attach(view.homeButton, "SealReady");

        public static void Apply(GameTerritoryResultView view)
        {
            GameFigmaButton.Attach(view.table.viewerButton, "ResultAction");
            GameFigmaButton.Attach(view.table.nextButton, "ResultAction");
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
