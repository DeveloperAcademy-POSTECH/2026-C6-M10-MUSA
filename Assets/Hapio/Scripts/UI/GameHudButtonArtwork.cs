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
        public static void Attach(Button button,string kind)
        {
            var a=button.GetComponent<GameHudButtonArtwork>()??button.gameObject.AddComponent<GameHudButtonArtwork>();
            a.button=button;a.kind=kind;
            if(a.artwork==null)a.artwork=GameViewerHelpUi.Surface(button.transform,kind+"Normal",6,5,33,34);
            a.artwork.Show(kind+"Normal");
        }
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button.IsInteractable())artwork.Show(kind+"Pressed");}
        public void OnPointerUp(PointerEventData e)=>Normal();
        public void OnPointerExit(PointerEventData e)=>Normal();
        private void OnDisable()=>Normal();
        private void Normal(){if(artwork!=null)artwork.Show(kind+"Normal");}
    }
}
