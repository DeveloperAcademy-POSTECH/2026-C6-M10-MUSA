using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #551 지귀 B "당겨!" 그림: 야광귀 흔들기 안내와 같은 먹색 폰이 윗부분을 나를 향해 젖힌다. lean 0 = 똑바로 선 폰,
    /// 1 = 윗변이 넓어지고(가까워짐) 키가 줄어든 폰(내 쪽으로 넘어옴). 사다리꼴 한 장으로 그린다.
    /// </summary>
    public sealed class LabJigwiTiltPhoneGraphic : MaskableGraphic
    {
        private static Texture2D phone;
        [Range(0f, 1f)] public float lean;

        public override Texture mainTexture => Phone();

        public void SetLean(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, lean)) return;
            lean = value; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float w = r.width, h = r.height * (1f - .42f * lean);
            float bottom = r.yMin, top = bottom + h;
            float topHalf = w * .5f * (1f + .38f * lean), bottomHalf = w * .5f;
            float cx = r.center.x;
            Color32 c = color;
            vh.AddVert(new Vector3(cx - bottomHalf, bottom), c, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(cx - topHalf, top), c, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(cx + topHalf, top), c, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(cx + bottomHalf, bottom), c, new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }

        /// <summary>먹색 테두리의 둥근 직사각형 폰, 흰 화면, 아래 버튼 점 (야광귀 흔들기 폰과 같은 그림).</summary>
        private static Texture2D Phone()
        {
            if (phone != null) return phone;
            const int w = 116, h = 200;
            var pixels = new Color32[w * h];
            Color32 ink = new Color32(43, 36, 32, 255), screen = new Color32(255, 252, 240, 255), clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float r = 24f;
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    Color32 c = outside > 0f ? clear : ink;
                    bool inScreen = x >= 14 && x < w - 14 && y >= 32 && y < h - 24;
                    if (outside <= 0f && inScreen) c = screen;
                    float bx = x - w * .5f, by = y - 16f;
                    if (bx * bx + by * by < 36f) c = screen;
                    pixels[y * w + x] = c;
                }
            phone = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "Jigwi tilt phone (#551 runtime)", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            phone.SetPixels32(pixels); phone.Apply(false, true);
            return phone;
        }
    }
}
