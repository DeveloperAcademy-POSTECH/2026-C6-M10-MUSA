using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Draws the authored wooden board behind the moving orbs and fits it to their live workspace.
    /// The board has no collider and does not change input or physics bounds.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(1100)]
    public sealed class OrbWoodenPlateView : MonoBehaviour
    {
        private const int PlateSortingOrder = 0; // OrbView uses 40-45, or 90-95 while held.
        [SerializeField] private T09Hud hud;
        [SerializeField] private SpriteRenderer plateRenderer;

        public T09Hud Hud => hud;
        public SpriteRenderer PlateRenderer => plateRenderer;

        public void Configure(T09Hud targetHud, SpriteRenderer targetRenderer)
        {
            hud = targetHud;
            plateRenderer = targetRenderer;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (plateRenderer == null) plateRenderer = GetComponent<SpriteRenderer>();
            if (plateRenderer == null) return;
            plateRenderer.sortingOrder = PlateSortingOrder;
            int orbLayer = LayerMask.NameToLayer("C6Orbs");
            if (orbLayer >= 0 && gameObject.layer != orbLayer) gameObject.layer = orbLayer;

            Camera camera = hud != null && hud.Layout != null ? hud.Layout.OrbCamera : null;
            if (camera == null || plateRenderer.sprite == null) return;
            Rect screenRect = hud.OrbWorkspaceScreenRect;
            if (screenRect.width <= 0f || screenRect.height <= 0f) return;

            var orbPlane = new Plane(Vector3.forward, Vector3.zero);
            Ray lowerRay = camera.ScreenPointToRay(screenRect.min);
            Ray upperRay = camera.ScreenPointToRay(screenRect.max);
            if (!orbPlane.Raycast(lowerRay, out float lowerDistance) ||
                !orbPlane.Raycast(upperRay, out float upperDistance)) return;
            Vector3 lower = lowerRay.GetPoint(lowerDistance);
            Vector3 upper = upperRay.GetPoint(upperDistance);
            float worldWidth = upper.x - lower.x;
            float worldHeight = upper.y - lower.y;
            if (worldWidth <= 0f || worldHeight <= 0f) return;

            Bounds spriteBounds = plateRenderer.sprite.bounds;
            if (spriteBounds.size.x <= 0f || spriteBounds.size.y <= 0f) return;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            if (Mathf.Abs(parentScale.x) <= 0.0001f || Mathf.Abs(parentScale.y) <= 0.0001f) return;
            transform.localScale = new Vector3(worldWidth / (spriteBounds.size.x * Mathf.Abs(parentScale.x)),
                worldHeight / (spriteBounds.size.y * Mathf.Abs(parentScale.y)), 1f);

            Vector3 worldScale = transform.lossyScale;
            Vector3 center = (lower + upper) * 0.5f;
            transform.position = new Vector3(center.x - spriteBounds.center.x * worldScale.x,
                center.y - spriteBounds.center.y * worldScale.y, transform.position.z);
        }
    }
}
