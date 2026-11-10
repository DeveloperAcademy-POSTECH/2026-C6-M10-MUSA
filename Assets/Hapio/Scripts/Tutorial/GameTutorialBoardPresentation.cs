using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Shows the existing tutorial plate with its alpha, over the continued stage background.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GameTutorialBoardPresentation : MonoBehaviour
    {
        private GameBattleBindings bindings;
        private Renderer plate;
        private bool originalPlateEnabled;
        private UnityEngine.UI.RawImage artwork;
        private LabOrbHudComposition composition;
        public UnityEngine.UI.RawImage Artwork => artwork;

        public void Bind(GameBattleBindings target)
        {
            bindings = target;
            plate = target.board.transform.Find("OrbBoardPlate").GetComponent<Renderer>();
            originalPlateEnabled = plate.enabled;
            var root = new GameObject("Tutorial Board Artwork", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            root.layer = target.hud.battleUi.layer;
            root.transform.SetParent(target.hud.battleUi.transform, false);
            artwork = root.GetComponent<UnityEngine.UI.RawImage>();
            artwork.raycastTarget = false;
            artwork.texture = plate.sharedMaterial.mainTexture;
            artwork.color = plate.sharedMaterial.color;
            artwork.uvRect = new Rect(plate.sharedMaterial.mainTextureOffset, plate.sharedMaterial.mainTextureScale);
            artwork.enabled = false;
            gameObject.AddComponent<GameBoardBackgroundContinuation>().Bind(target);
        }

        private void LateUpdate()
        {
            if (bindings == null || artwork == null) return;
            composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            if (composition == null) return;
            composition.presentationBackdrop = artwork.rectTransform;
            bool active = composition.IsCompositing;
            artwork.enabled = active;
            plate.enabled = !active && originalPlateEnabled;
            if (!active) return;

            // The existing orthographic input camera and board profile own the tutorial's placement.
            // Project their current bounds each frame: aspect changes must not move the visual off its hit area.
            Camera camera = bindings.sceneController.orbCamera;
            Bounds bounds = plate.bounds;
            var parent = (RectTransform)artwork.transform.parent;
            Vector2 min, max;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, camera.WorldToScreenPoint(bounds.min), null, out min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, camera.WorldToScreenPoint(bounds.max), null, out max);
            var rect = artwork.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = min - parent.rect.min;
            rect.sizeDelta = max - min;
        }

        private void OnDisable()
        {
            if (composition != null && artwork != null && composition.presentationBackdrop == artwork.rectTransform)
                composition.presentationBackdrop = null;
            if (artwork != null) artwork.enabled = false;
            if (plate != null) plate.enabled = originalPlateEnabled;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (artwork != null) Destroy(artwork.gameObject);
        }
    }
}
