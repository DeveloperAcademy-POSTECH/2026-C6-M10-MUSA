using UnityEngine;

namespace C6Lab
{
    /// <summary>Art only. The item marker supplies the existing handoff label, burn share and rolling pose.</summary>
    [DisallowMultipleComponent]
    public sealed class LabBombVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body, cap, fuse, flame, count;
        [SerializeField] private Transform rollPivot, authoredParts;
        [SerializeField] private Sprite[] counts;
        private float flameSize;
        private MaterialPropertyBlock fuseProperties;
        private static readonly int BurnEnd = Shader.PropertyToID("_BurnEnd");
        public string CountText { get; private set; } = string.Empty;
        public bool CountVisible => count.gameObject.activeInHierarchy;
        public bool FlameVisible => flame.gameObject.activeInHierarchy;
        public Color Tint => body.color;
        public Vector2 TipPosition { get; private set; }
        public float FlameSize => flame.transform.localScale.x;
        public Sprite CountSprite => count.sprite;
        public Transform CountTransform => count.transform;
        public int CapSortingOrder => cap.sortingOrder;

        // Exact curve from Parts/BombFuse.svg, in its 48 x 32 canvas.
        public static Vector2 FusePoint(float t)
        {
            float u = 1f - t;
            return u*u*u * new Vector2(11f, 24.6749f) + 3f*u*u*t * new Vector2(18f, -.325121f)
                + 3f*u*t*t * new Vector2(24f, 2.67488f) + t*t*t * new Vector2(34f, 6.67488f);
        }

        public void ShowCount(string handoffs)
        {
            int.TryParse(handoffs, out int value);
            bool visible = value >= 1 && value <= 4;
            CountText = visible ? value.ToString() : string.Empty;
            count.gameObject.SetActive(visible);
            count.sprite = visible ? counts[value - 1] : null;
            count.transform.localRotation = Quaternion.identity;
        }

        public void SetTint(Color tint) { body.color = tint; cap.color = tint; }
        public void SetLit(bool lit) => flame.gameObject.SetActive(lit);
        public void AnimateFlame(float flicker, float turn)
        {
            // Reuse the marker's original noise clock and rotation; this is the new painted star.
            flame.transform.localScale = Vector3.one * (flameSize * Mathf.Lerp(.48f, .62f, flicker) / .55f);
            flame.transform.localRotation = Quaternion.Euler(0f, 0f, turn);
            flame.color = new Color(1f, 1f, 1f, Mathf.Lerp(.75f, 1f, flicker));
        }

        public void Pose(float facingDegrees, float reach, float authoredDegrees, float share)
        {
            rollPivot.localRotation = Quaternion.Euler(0f, 0f, facingDegrees);
            rollPivot.localScale = new Vector3(reach, 1f, 1f);
            authoredParts.localRotation = Quaternion.Euler(0f, 0f, -authoredDegrees);
            share = Mathf.Clamp01(share);
            Vector2 p = FusePoint(share);
            Vector3 tip = new Vector3(73f + p.x - 52f, 78f - 16f - p.y, 0f) / 104f;
            Vector3 worldTip = authoredParts.TransformPoint(tip);
            Vector3 localTip = transform.InverseTransformPoint(worldTip);
            TipPosition = localTip * transform.localScale.x;
            // Initial flame slot is (91,7,38,38); its centre covers the curve's end.
            float flameScale = Mathf.Lerp(.2f, 1f, share);
            Vector3 flameAt = tip + new Vector3(3f, -3.32512f, 0f) / 104f * flameScale;
            flameAt = transform.InverseTransformPoint(authoredParts.TransformPoint(flameAt));
            flameAt.z = -.012f;
            flame.transform.localPosition = flameAt;
            flameSize = 38f / 104f * flameScale;
            flame.transform.localScale = Vector3.one * flameSize;
            ClipFuse(share, p.x);
        }

        private void ClipFuse(float share, float tipX)
        {
            if (fuseProperties == null) fuseProperties = new MaterialPropertyBlock();
            fuse.GetPropertyBlock(fuseProperties);
            fuseProperties.SetFloat(BurnEnd, share >= .99999f ? 1f : tipX / 48f);
            fuse.SetPropertyBlock(fuseProperties);
            fuse.gameObject.SetActive(share > 0f);
        }
    }
}
