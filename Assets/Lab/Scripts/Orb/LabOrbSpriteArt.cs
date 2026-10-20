using UnityEngine;

namespace C6Lab
{
    /// <summary>Presentation only: the root sphere continues to own collision and gameplay size.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LabOrbSpriteArt : MonoBehaviour
    {
        public static float SizeScaleFor(LabOrbKind kind) => kind == LabOrbKind.Combined ? 1.2f : 1f;

        public static LabOrbSpriteArt Set(Transform parent, LabOrbSpriteArt existing,
            Sprite sprite, float radius, float depth = 0f)
        {
            if (sprite == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return existing;
            }
            if (existing == null)
            {
                var art = new GameObject("Orb Sprite Art", typeof(SpriteRenderer));
                art.transform.SetParent(parent, false);
                existing = art.AddComponent<LabOrbSpriteArt>();
            }
            existing.gameObject.layer = parent.gameObject.layer;
            existing.gameObject.SetActive(true);
            existing.GetComponent<SpriteRenderer>().sprite = sprite;
            existing.transform.localPosition = new Vector3(0f, 0f, depth);
            existing.transform.localRotation = Quaternion.identity;
            existing.transform.localScale = Vector3.one * (2f * radius / Mathf.Max(.0001f,
                Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
            return existing;
        }

        private void OnWillRenderObject()
        {
            // Each device views the same physical projectile from its own battle camera.
            if (Camera.current != null) transform.rotation = Camera.current.transform.rotation;
        }
    }
}
