using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Original Figma normal/pressed exports, including GRINDHOUSE strokes.</summary>
    public sealed class GameHudButtonArtwork : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public string kind;
        public GameFigmaSurface artwork;
        private Button button;
        private string normalKey, pressedKey;
        public static void Attach(Button button,string kind)
        {
            var current = button.GetComponent<GameHudButtonArtwork>();
            var surface = current != null && current.artwork != null ? current.artwork
                : GameViewerHelpUi.Surface(button.transform, kind + "Normal", 6, 5, 33, 34);
            Attach(button, surface, kind + "Normal", kind + "Pressed");
            button.GetComponent<GameHudButtonArtwork>().kind = kind;
        }

        internal static void Attach(Button button, GameFigmaSurface surface, string normal, string pressed)
        {
            var state = button.GetComponent<GameHudButtonArtwork>() ?? button.gameObject.AddComponent<GameHudButtonArtwork>();
            state.button = button;
            state.artwork = surface;
            state.normalKey = normal;
            state.pressedKey = pressed;
            state.Normal();
        }
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button.IsInteractable())artwork.Show(pressedKey);}
        public void OnPointerUp(PointerEventData e)=>Normal();
        public void OnPointerExit(PointerEventData e)=>Normal();
        private void OnDisable()=>Normal();
        private void Normal(){if(artwork!=null)artwork.Show(normalKey);}
    }
}
