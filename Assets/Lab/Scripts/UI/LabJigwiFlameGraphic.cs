using UnityEngine;

namespace C6Lab
{
    /// <summary>Passive ignition rings and an invisible stencil shape; the fire itself always comes from artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabJigwiFlameGraphic : UnityEngine.UI.MaskableGraphic
    {
        public enum Shape { CurtainArtMask = 0, IgnitionMarker = 1 }
        [SerializeField] private Shape shape;
        [SerializeField] private Color ink = new Color(.045f, .012f, .006f, .98f);
        [SerializeField] private Color marker = new Color(1f, .96f, .68f, 1f);
        private const int Segments = 48;

        public Shape GraphicShape => shape;
        public void Configure(Shape value)
        {
            shape = value;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (shape == Shape.IgnitionMarker)
            {
                float radius = Mathf.Min(rect.width, rect.height) * .5f;
                Ring(mesh, rect.center, radius, .34f, .5f, ink);
                Ring(mesh, rect.center, radius, .365f, .435f, marker);
                Disc(mesh, rect.center, radius * .11f, marker);
                return;
            }
            // Used only with Mask.showMaskGraphic=false. There is no red procedural body.
            Vector2 centre = rect.center;
            mesh.AddVert(centre, Color.white, Vector2.one * .5f);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments);
                Vector2 point = centre + new Vector2(Mathf.Cos(angle) * rect.width * .5f, Mathf.Sin(angle) * rect.height * .5f);
                mesh.AddVert(point, Color.white, Vector2.zero);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
        }

        private static void Ring(UnityEngine.UI.VertexHelper mesh, Vector2 centre, float radius,
            float inner, float outer, Color tint)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * (Mathf.PI * 2f / Segments);
                Vector2 arm = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius
                    * (1f + .018f * Mathf.Sin(a * 7f) + .01f * Mathf.Cos(a * 11f));
                mesh.AddVert(centre + arm * (outer * 2f), tint, Vector2.zero);
                mesh.AddVert(centre + arm * (inner * 2f), tint, Vector2.zero);
                if (i == 0) continue;
                int at = first + i * 2;
                mesh.AddTriangle(at - 2, at, at - 1); mesh.AddTriangle(at - 1, at, at + 1);
            }
        }
        private static void Disc(UnityEngine.UI.VertexHelper mesh, Vector2 centre, float radius, Color tint)
        {
            int first = mesh.currentVertCount; mesh.AddVert(centre, tint, Vector2.zero);
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * (Mathf.PI * 2f / Segments);
                mesh.AddVert(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
