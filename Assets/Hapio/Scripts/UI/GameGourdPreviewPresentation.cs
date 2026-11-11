using C6Lab;
using UnityEngine;

namespace Hapio
{
    internal static class GameGourdPreviewPresentation
    {
        // The square sits in the upper half of the 402×874 design (centre y≈335): 2.8× its height reaches the bottom of
        // the design with margin for taller phones, 1.7× its width covers an iPad's wider screen.
        internal const float FrameHorizontal = 1.7f, FrameVertical = 2.8f;
        internal static void Apply(LabGourdResultPreview preview, Transform session)
        {
            Transform rig = preview.ModelRoot.parent;
            // Keep the rig in the session's scene so editor Play sessions tear it down
            // together with its owner, including when domain and scene reload are disabled.
            foreach (Transform part in rig.GetComponentsInChildren<Transform>(true))
                part.gameObject.hideFlags = HideFlags.HideInHierarchy;
            rig.SetParent(session, true);

            preview.PreviewCamera.backgroundColor = Color.clear;
            preview.ModelRoot.localScale = Vector3.one * 1.55f;
            // Zooming in used to cut the gourd at the 402×402 square; draw it past the square at the same size.
            preview.ExtendFrame(FrameHorizontal, FrameVertical);
            preview.ModelRoot.gameObject.AddComponent<GameGourdFloorShadow>();
        }
    }
}
