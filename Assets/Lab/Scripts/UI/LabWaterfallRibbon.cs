using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// #741 지귀 부적 물 폭포의 물줄기 한 겹. 위가 좁고 아래로 갈수록 넓어지며 살짝 흔들리는 띠를 그리고, 텍스처를 아래로 흘린다.
    /// RectTransform의 위쪽 가운데(pivot .5, 1)에서 아래로 rect 높이만큼, 가장 넓은 곳이 rect 너비다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabWaterfallRibbon : MaskableGraphic
    {
        public Texture texture;
        /// <summary>텍스처가 흘러간 양 (늘수록 아래로 흐른다).</summary>
        public float scroll;
        /// <summary>너비 하나당 텍스처가 몇 번 반복되는지 (세로).</summary>
        public float tiles = .7f;
        /// <summary>맨 위 너비 (가장 넓은 곳에 대한 비율).</summary>
        public float topWidth = .5f;
        /// <summary>좌우 흔들림 세기와 위상.</summary>
        public float sway, phase;
        /// <summary>위에서부터 보이는 길이 비율 (0이면 안 보이고 1이면 끝까지).</summary>
        public float grow = 1f;
        public int segments = 28;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        /// <summary>값을 바꾼 뒤 다시 그리게 한다.</summary>
        public void Refresh() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float length = r.height * Mathf.Clamp01(grow);
            if (length <= 0f || r.width <= 0f) return;
            Color32 c = color;
            int count = Mathf.Max(2, segments);
            float aspect = length / r.width;
            for (int k = 0; k <= count; k++)
            {
                float v = k / (float)count;
                float y = r.yMax - v * length;
                // Narrow where it pours out, opening toward the bottom; the lower part swings a little.
                float half = r.width * .5f * Mathf.Lerp(topWidth, 1f, Mathf.Pow(v, .75f));
                float x = r.center.x + Mathf.Sin(v * Mathf.PI * 2.2f + phase) * sway * r.width * .05f * v;
                float tv = scroll - v * tiles * aspect;
                vh.AddVert(new Vector3(x - half, y), c, new Vector2(0f, tv));
                vh.AddVert(new Vector3(x + half, y), c, new Vector2(1f, tv));
                if (k == 0) continue;
                int i = k * 2;
                vh.AddTriangle(i - 2, i - 1, i);
                vh.AddTriangle(i - 1, i + 1, i);
            }
        }
    }
}
