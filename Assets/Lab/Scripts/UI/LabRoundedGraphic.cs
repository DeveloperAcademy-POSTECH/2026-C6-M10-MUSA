using UnityEngine;
using UnityEngine.UI;
namespace C6Lab
{
    /// <summary>Resolution-independent solid rounded controls, with no generated texture dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabRoundedGraphic : MaskableGraphic
    {
        public float radius = 27f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect;
            float corner = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            vh.AddVert(r.center, color, Vector2.zero);
            const int steps = 12;
            for (int c = 0; c < 4; c++)
            {
                Vector2 center = new Vector2(c == 0 || c == 3 ? r.xMax - corner : r.xMin + corner,
                    c < 2 ? r.yMax - corner : r.yMin + corner);
                for (int s = 0; s <= steps; s++)
                {
                    float angle = (c * 90f + s * 90f / steps) * Mathf.Deg2Rad;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner, color, Vector2.zero);
                }
            }
            int n = 4 * (steps + 1);
            for (int i = 1; i <= n; i++) vh.AddTriangle(0, i, i == n ? 1 : i + 1);
        }
    }
}
