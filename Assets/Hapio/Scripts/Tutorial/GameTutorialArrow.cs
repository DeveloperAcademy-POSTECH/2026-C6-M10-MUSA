using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477)의 한 방향 화살표 (2단계: 음구슬 → 양구슬). 몸통 사각형과 머리 삼각형을 메시 한 장으로 그린다.
    /// 시작·끝 점은 이 그래픽의 local 좌표다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameTutorialArrow : MaskableGraphic
    {
        private Vector2 from, to;
        private float width = 7f, headLength = 18f, headWidth = 22f;

        private static Texture2D brushTexture;
        public override Texture mainTexture => BrushTexture();

        private static Texture2D BrushTexture()
        {
            if (brushTexture != null) return brushTexture;
            const int w = 128, h = 64;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w, v = (y + .5f) / h;
                    float edge = .06f + .025f * Mathf.Sin(x * .51f) + .02f * Mathf.Sin(x * 1.73f);
                    float coverage = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Min(v, 1f - v) - edge) / .07f));
                    float grain = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.547f, 1f);
                    float streak = Mathf.Abs(v - (.38f + .025f * Mathf.Sin(u * 18f)));
                    if (streak < .012f && u > .15f && u < .85f) coverage *= .25f;
                    coverage *= grain < .07f ? .35f : .9f + .1f * grain;
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            brushTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Tutorial dry ink brush", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            brushTexture.SetPixels32(pixels);
            brushTexture.Apply(false, true);
            return brushTexture;
        }

        public Vector2 From => from;
        public Vector2 To => to;

        public void Set(Vector2 start, Vector2 end, float shaftWidth, float tipLength, float tipWidth)
        {
            shaftWidth = Mathf.Max(0f, shaftWidth);
            tipLength = Mathf.Max(0f, tipLength);
            tipWidth = Mathf.Max(0f, tipWidth);
            if (start == from && end == to && Mathf.Approximately(shaftWidth, width)
                && Mathf.Approximately(tipLength, headLength) && Mathf.Approximately(tipWidth, headWidth)) return;
            from = start;
            to = end;
            width = shaftWidth;
            headLength = tipLength;
            headWidth = tipWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 line = to - from;
            float length = line.magnitude;
            if (length < 1e-3f) return;
            Vector2 along = line / length, across = new Vector2(-along.y, along.x);
            // A short arrow (two orbs side by side) keeps a shaft: the head takes at most 60% and shrinks in proportion.
            float head = Mathf.Min(headLength, length * .6f);
            float headScale = headLength > 0f ? head / headLength : 1f;
            Vector2 neck = to - along * head;
            Color32 tint = color;
            if (length > head)
            {
                Vector2 side = across * (width * .5f);
                mesh.AddVert(from - side, tint, new Vector2(0f, 0f));
                mesh.AddVert(from + side, tint, new Vector2(0f, 1f));
                mesh.AddVert(neck + side, tint, new Vector2(1f, 1f));
                mesh.AddVert(neck - side, tint, new Vector2(1f, 0f));
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(2, 3, 0);
            }
            int tip = mesh.currentVertCount;
            Vector2 wing = across * (headWidth * headScale * .5f);
            mesh.AddVert(neck - wing, tint, new Vector2(0f, 0f));
            mesh.AddVert(neck + wing, tint, new Vector2(0f, 1f));
            mesh.AddVert(to, tint, new Vector2(1f, .5f));
            mesh.AddTriangle(tip, tip + 1, tip + 2);
        }
    }
}
