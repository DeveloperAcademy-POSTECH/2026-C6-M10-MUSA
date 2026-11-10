using C6Lab;
using UnityEngine;

namespace Hapio
{
    internal static class GameGourdPreviewPresentation
    {
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
            preview.ModelRoot.gameObject.AddComponent<GameGourdFloorShadow>();
        }
    }
}
