using System.Collections.Generic;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Stage identity decorates only the existing animated yokai slot. The paint shell and target stay owned by Lab.</summary>
    [DisallowMultipleComponent]
    public sealed class GameYokaiPresentation : MonoBehaviour
    {
        public const string SlotPath = "GourdYokai_Details/YokaiRoot";
        private LabTarget target;
        private GameStageDefinition definition;
        private GameObject seenVisual, seenPrefab;
        private GameStageDefinition seenDefinition;
        private readonly List<(GameObject child, bool active)> original = new List<(GameObject, bool)>();
        public GameObject DecorativeInstance { get; private set; }
        public Transform DecorativeSlot { get; private set; }
        public bool UsingExistingVisual => DecorativeInstance == null;
        public string Status { get; private set; } = "Waiting for the paint presentation.";

        public void Bind(LabTarget stageTarget, GameStageDefinition stage)
        {
            target = stageTarget;
            definition = stage;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            GameObject visual = target != null ? target.MountedVisual : null;
            GameObject prefab = definition != null ? definition.yokaiVisual : null;
            if (visual == seenVisual && definition == seenDefinition && prefab == seenPrefab) return;
            Release();
            seenVisual = visual; seenDefinition = definition; seenPrefab = prefab;
            if (visual == null || definition == null)
            { Status = "Waiting for the paint presentation."; return; }
            DecorativeSlot = visual.transform.Find(SlotPath);
            if (DecorativeSlot == null)
            {
                // Crack/custom presentations need not have the paint prefab's animated slot.
                // Keep their real shell visible and collidable instead of replacing the whole target.
                Status = "No decorative yokai slot in this presentation; the existing target is preserved.";
                return;
            }
            if (prefab == null)
            {
                Status = definition.displayName + ": existing yokai art (no stage model supplied).";
                return;
            }
            if (!TryBounds(DecorativeSlot, out Bounds available))
            { Status = "The existing decorative slot has no renderable body; original art is preserved."; return; }
            foreach (Transform child in DecorativeSlot) original.Add((child.gameObject, child.gameObject.activeSelf));
            DecorativeInstance = Instantiate(prefab, DecorativeSlot);
            DecorativeInstance.name = definition.stageId + " · decorative yokai";
            DecorativeInstance.transform.localPosition = Vector3.zero;
            DecorativeInstance.transform.localRotation = Quaternion.identity;
            StripGameplayComponents(DecorativeInstance);
            Renderer[] modelRenderers = DecorativeInstance.GetComponentsInChildren<Renderer>(true);
            if (!TryBounds(DecorativeSlot, modelRenderers, out Bounds modelBounds))
            {
                Release(); seenVisual = visual; seenDefinition = definition; seenPrefab = prefab;
                Status = "The supplied decorative model has no renderer; original art is preserved.";
                return;
            }
            float size = Mathf.Max(modelBounds.size.x, Mathf.Max(modelBounds.size.y, modelBounds.size.z));
            float fit = Mathf.Max(available.size.x, Mathf.Max(available.size.y, available.size.z));
            DecorativeInstance.transform.localScale *= Mathf.Max(.00001f, fit) / Mathf.Max(.00001f, size);
            TryBounds(DecorativeSlot, modelRenderers, out Bounds fitted);
            DecorativeInstance.transform.localPosition += available.center - fitted.center;
            SetLayer(DecorativeInstance.transform, DecorativeSlot.gameObject.layer);
            foreach (var entry in original) if (entry.child != null) entry.child.SetActive(false);
            Status = definition.displayName + ": decorative stage model applied.";
        }

        private static void StripGameplayComponents(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            { collider.enabled = false; Remove(collider); }
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            { body.useGravity = false; body.isKinematic = true; Remove(body); }
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Animation animation in root.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }

        private static bool TryBounds(Transform root, out Bounds result) =>
            TryBounds(root, root.GetComponentsInChildren<Renderer>(true), out result);

        private static bool TryBounds(Transform root, Renderer[] renderers, out Bounds result)
        {
            result = default;
            bool any = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                            if (!any) { result = new Bounds(point, Vector3.zero); any = true; }
                            else result.Encapsulate(point);
                        }
            }
            return any;
        }

        private void Release()
        {
            foreach (var entry in original) if (entry.child != null) entry.child.SetActive(entry.active);
            original.Clear();
            if (DecorativeInstance != null)
            {
                DecorativeInstance.SetActive(false);
                DecorativeInstance.transform.SetParent(null, false);
                Remove(DecorativeInstance);
            }
            DecorativeInstance = null; DecorativeSlot = null;
            seenVisual = seenPrefab = null; seenDefinition = null;
        }

        private static void Remove(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }
}
