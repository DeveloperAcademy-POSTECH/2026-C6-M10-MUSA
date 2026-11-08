using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Native rounded outline; leaves the centre transparent instead of colouring the board behind it.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameUiFrame : MaskableGraphic
    {
        public float radius = 8, stroke = 3;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            float outer = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            float width = Mathf.Clamp(stroke, 0, outer), inner = outer - width;
            for (int c = 0; c < 4; c++)
            {
                Vector2 centre = new Vector2(c == 0 || c == 3 ? r.xMax - outer : r.xMin + outer,
                    c < 2 ? r.yMax - outer : r.yMin + outer);
                for (int i = 0; i <= 12; i++)
                {
                    float angle = (c * 90 + i * 7.5f) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    mesh.AddVert(centre + direction * outer, color, Vector2.zero);
                    mesh.AddVert(centre + direction * inner, color, Vector2.zero);
                }
            }
            const int count = 52;
            for (int i = 0; i < count; i++)
            {
                int a = i * 2, b = ((i + 1) % count) * 2;
                mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a + 1, b, b + 1);
            }
        }
    }
}
