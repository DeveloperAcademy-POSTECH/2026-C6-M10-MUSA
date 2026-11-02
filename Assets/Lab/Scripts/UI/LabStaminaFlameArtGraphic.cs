using UnityEngine;

namespace C6Lab
{
    /// <summary>Bounded UI quads reuse the existing transparent 2x2 reward flame atlas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabStaminaFlameArtGraphic : UnityEngine.UI.MaskableGraphic
    {
        private Texture2D atlas;
        private float fraction, age, heightRatio = .85f, fps = 12f, opacity = .95f;
        public override Texture mainTexture => atlas != null ? atlas : Texture2D.whiteTexture;
        public int FlameQuadCount { get; private set; }
        public void Configure(Texture2D texture, float filledFraction, float clock, float relativeHeight,
            float framesPerSecond, float strength)
        {
            if (atlas != texture) { atlas = texture; SetMaterialDirty(); }
            fraction = Mathf.Clamp01(filledFraction); age = clock; heightRatio = relativeHeight;
            fps = framesPerSecond; opacity = Mathf.Clamp01(strength);
            raycastTarget = false; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); FlameQuadCount = 0;
            Rect rect = GetPixelAdjustedRect();
            if (atlas == null || rect.width <= 0f || rect.height <= 0f || fraction <= 0f) return;
            int columns = Mathf.Clamp(Mathf.CeilToInt(rect.width / Mathf.Max(1f, rect.height * .85f)), 2, 24);
            float spacing = rect.width / columns;
            float end = rect.xMin + rect.width * fraction;
            for (int i = 0; i < columns; i++)
            {
                float x = rect.xMin + (i + .5f) * spacing;
                if (x - spacing * .6f >= end) break;
                float flicker = .88f + .12f * Mathf.Sin(age * 10f + i * 2.39996f);
                float height = rect.height * heightRatio * flicker;
                float width = Mathf.Min(spacing * 1.35f, height * .9f);
                int frame = ((int)(age * Mathf.Max(1f, fps)) + i) % 4;
                Rect uv = new Rect(frame % 2 * .5f, frame < 2 ? .5f : 0f, .5f, .5f);
                Quad(mesh, new Rect(x - width * .5f, rect.yMax - rect.height * .1f, width, height),
                    uv, rect.xMin, end, false, opacity);
                // The lower edge stays narrow so the blue fill remains readable.
                Quad(mesh, new Rect(x - width * .38f, rect.yMin - height * .35f,
                    width * .76f, height * .42f), uv, rect.xMin, end, true, opacity * .72f);
            }
        }
        private void Quad(UnityEngine.UI.VertexHelper mesh, Rect area, Rect uv, float leftLimit,
            float rightLimit, bool inverted, float alpha)
        {
            float left = Mathf.Max(area.xMin, leftLimit), right = Mathf.Min(area.xMax, rightLimit);
            if (right <= left) return;
            float u0 = Mathf.Lerp(uv.xMin, uv.xMax, (left - area.xMin) / area.width);
            float u1 = Mathf.Lerp(uv.xMin, uv.xMax, (right - area.xMin) / area.width);
            float v0 = inverted ? uv.yMax : uv.yMin, v1 = inverted ? uv.yMin : uv.yMax;
            int first = mesh.currentVertCount; Color32 tint = new Color(1f, 1f, 1f, alpha) * color;
            mesh.AddVert(new Vector2(left, area.yMin), tint, new Vector2(u0, v0));
            mesh.AddVert(new Vector2(left, area.yMax), tint, new Vector2(u0, v1));
            mesh.AddVert(new Vector2(right, area.yMax), tint, new Vector2(u1, v1));
            mesh.AddVert(new Vector2(right, area.yMin), tint, new Vector2(u1, v0));
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
            FlameQuadCount++;
        }
    }
}
