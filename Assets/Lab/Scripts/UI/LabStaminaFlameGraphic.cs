using UnityEngine;

namespace C6Lab
{
    /// <summary>Thin irregular warm outline. The separate artwork layer supplies actual flames.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabStaminaFlameGraphic : UnityEngine.UI.MaskableGraphic
    {
        private const int Segments = 64;
        private float fraction, age, thickness = .11f, opacity = 1f;
        public float Fraction => fraction;
        public void Configure(float filledFraction, float clock, float widthRatio, float strength)
        {
            fraction = Mathf.Clamp01(filledFraction); age = clock;
            thickness = Mathf.Clamp(widthRatio, .025f, .2f); opacity = Mathf.Clamp01(strength);
            raycastTarget = false; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f || fraction <= 0f) return;
            rect.width *= fraction;
            float width = rect.height * thickness;
            Ring(mesh, rect, width, new Color(.12f, .025f, .008f, opacity));
            Ring(mesh, rect, width * .68f, new Color(1f, .25f, .025f, opacity));
            Ring(mesh, rect, width * .28f, new Color(1f, .87f, .28f, opacity));
        }
        private void Ring(UnityEngine.UI.VertexHelper mesh, Rect rect, float width, Color tint)
        {
            int first = mesh.currentVertCount;
            float halfWidth = rect.width * .5f, halfHeight = rect.height * .5f;
            float radius = Mathf.Min(halfWidth, halfHeight);
            Color32 ink = tint * color;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / Segments);
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float side = normal.x >= 0f ? 1f : -1f;
                Vector2 point = rect.center + new Vector2(side * (halfWidth - radius), 0f) + normal * radius;
                float wave = rect.height * .012f * Mathf.Sin(angle * 9f + age * 7f);
                point += normal * wave;
                mesh.AddVert(point + normal * width, ink, Vector2.zero);
                mesh.AddVert(point - normal * width, ink, Vector2.zero);
                if (i == 0) continue;
                int current = first + i * 2;
                mesh.AddTriangle(current - 2, current, current - 1);
                mesh.AddTriangle(current - 1, current, current + 1);
            }
        }
    }
}
