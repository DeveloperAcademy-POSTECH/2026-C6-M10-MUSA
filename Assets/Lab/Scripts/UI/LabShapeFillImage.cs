using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace C6Lab
{
    // Two adjacent regions sample one alpha silhouette, without a backing image.
    public sealed class LabShapeFillImage : Image
    {
        public Color filledColor = Color.white;
        public Color emptyColor = new Color32(0xE0, 0xD0, 0xBD, 255);
        private Color filledFlashColor = Color.clear;
        private float filledFlashAmount;

        /// <summary>#788: how far the filled cells are blended toward the flash colour now (0 = their own colour).</summary>
        public float FilledFlashAmount => filledFlashAmount;
        /// <summary>The colour the filled cells are drawn with now.</summary>
        public Color ShownFilledColor => filledFlashAmount <= 0f ? filledColor
            : Color.Lerp(filledColor, new Color(filledFlashColor.r, filledFlashColor.g, filledFlashColor.b, filledColor.a), filledFlashAmount);

        /// <summary>
        /// #788: blends the filled cells toward <paramref name="color"/> by <paramref name="amount"/> (0~1) for a moment, e.g. red
        /// when stamina is short. filledColor itself (the character's colour, set by its owner) is never changed.
        /// </summary>
        public void SetFilledFlash(Color color, float amount)
        {
            amount = Mathf.Clamp01(float.IsNaN(amount) ? 0f : amount);
            if (amount == filledFlashAmount && (amount <= 0f || color == filledFlashColor)) return;
            filledFlashColor = color;
            filledFlashAmount = amount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (sprite == null) return;
            Rect rect = GetPixelAdjustedRect();
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            bool fromRight = fillOrigin == (int)OriginHorizontal.Right;
            float split = fromRight ? 1f - fillAmount : fillAmount;
            Color filled = ShownFilledColor;
            AddRegion(mesh, rect, uv, 0f, split, fromRight ? emptyColor : filled);
            AddRegion(mesh, rect, uv, split, 1f, fromRight ? filled : emptyColor);
        }

        private void AddRegion(VertexHelper mesh, Rect rect, Vector4 uv, float start, float end, Color tint)
        {
            if (end <= start) return;
            int index = mesh.currentVertCount;
            float left = Mathf.Lerp(rect.xMin, rect.xMax, start);
            float right = Mathf.Lerp(rect.xMin, rect.xMax, end);
            float u0 = Mathf.Lerp(uv.x, uv.z, start);
            float u1 = Mathf.Lerp(uv.x, uv.z, end);
            Color32 vertexColor = tint * color;
            mesh.AddVert(new Vector3(left, rect.yMin), vertexColor, new Vector2(u0, uv.y));
            mesh.AddVert(new Vector3(left, rect.yMax), vertexColor, new Vector2(u0, uv.w));
            mesh.AddVert(new Vector3(right, rect.yMax), vertexColor, new Vector2(u1, uv.w));
            mesh.AddVert(new Vector3(right, rect.yMin), vertexColor, new Vector2(u1, uv.y));
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
