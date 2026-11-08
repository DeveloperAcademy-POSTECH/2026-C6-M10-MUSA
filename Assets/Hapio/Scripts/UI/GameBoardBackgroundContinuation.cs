using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Continues the same stage view below its saved viewport without moving gameplay cameras.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class GameBoardBackgroundContinuation : MonoBehaviour
    {
        private GameBattleBindings bindings;
        private Camera continuation;
        public Camera ContinuationCamera => continuation;
        public void Bind(GameBattleBindings value) => bindings = value;

        private void LateUpdate()
        {
            var composition = bindings != null && bindings.hud != null ? bindings.hud.GetComponent<LabOrbHudComposition>() : null;
            bool active = composition != null && composition.IsCompositing && composition.presentationBackdrop != null;
            if (!active) { if (continuation != null) continuation.enabled = false; return; }
            Camera source = bindings.battleCamera;
            Rect view = source.rect;
            if (view.yMin <= 0 || view.height <= 0) return;
            if (continuation == null)
            {
                var root = new GameObject("Stage background continuation");
                root.transform.SetParent(transform,false);
                continuation = root.AddComponent<Camera>();
            }
            continuation.CopyFrom(source);
            continuation.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            continuation.rect = new Rect(view.x,0,view.width,view.yMin);
            continuation.depth = source.depth - .5f;
            continuation.cullingMask = source.cullingMask & ~(1 << LayerMask.NameToLayer(LabOrbHudComposition.PaperLayerName));
            // Remap the original projection into the lower screen region. At the shared boundary both
            // cameras use exactly the same ray; the original viewport, FOV and input projection stay intact.
            float scale = view.height / view.yMin;
            Matrix4x4 projection = source.projectionMatrix;
            projection.SetRow(1,scale*projection.GetRow(1)+(scale+1)*projection.GetRow(3));
            continuation.projectionMatrix = projection;
            continuation.enabled = true;
        }
        private void OnDisable() { if (continuation != null) continuation.enabled = false; }
        private void OnDestroy() { if (continuation != null) Destroy(continuation.gameObject); }
    }
}
