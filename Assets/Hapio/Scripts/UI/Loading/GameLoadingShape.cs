using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 로딩 화면 (#478)의 둥근 모양 하나: 둥근 네모(모서리 반지름 &lt; 0이면 양 끝이 반원인 알약)와, 원하면 아래 가운데로
    /// 뾰족한 꼬리. 그림 파일 없이 그려 크기가 바뀌어도 모서리가 깨지지 않는다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameLoadingShape : MaskableGraphic
    {
        private const int CornerSegments = 8;
        [SerializeField] private float corner = -1f;
        [SerializeField] private float tailHeight;
        [SerializeField] private float tailWidth;
        private readonly List<Vector2> ring = new List<Vector2>(4 * (CornerSegments + 1));

        /// <summary>Rounded corners of <paramref name="cornerRadius"/> (below 0: a pill), and a tail under the middle.</summary>
        public void SetShape(float cornerRadius, float tail = 0f, float tailBase = 0f)
        {
            corner = cornerRadius;
            tailHeight = Mathf.Max(0f, tail);
            tailWidth = Mathf.Max(0f, tailBase);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            float tail = Mathf.Min(tailHeight, area.height);
            var body = new Rect(area.xMin, area.yMin + tail, area.width, area.height - tail);
            if (body.width <= 0f || body.height <= 0f) return;
            Outline(body, corner, ring);
            Color32 tint = color;
            mesh.AddVert(body.center, tint, Vector4.zero);
            foreach (Vector2 point in ring) mesh.AddVert(point, tint, Vector4.zero);
            for (int i = 0; i < ring.Count; i++) mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % ring.Count);
            if (tail <= 0f || tailWidth <= 0f) return;
            int start = mesh.currentVertCount;
            float half = Mathf.Min(tailWidth, body.width) * .5f;
            mesh.AddVert(new Vector2(body.center.x - half, body.yMin + .5f), tint, Vector4.zero);
            mesh.AddVert(new Vector2(body.center.x, area.yMin), tint, Vector4.zero);
            mesh.AddVert(new Vector2(body.center.x + half, body.yMin + .5f), tint, Vector4.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        /// <summary>The rounded rectangle's edge, counter-clockwise from the right end of its top. A radius below 0 is a pill.</summary>
        public static void Outline(Rect body, float cornerRadius, List<Vector2> points)
        {
            points.Clear();
            float limit = Mathf.Min(body.width, body.height) * .5f;
            float c = cornerRadius < 0f ? limit : Mathf.Min(cornerRadius, limit);
            Vector2[] centers =
            {
                new Vector2(body.xMax - c, body.yMax - c), new Vector2(body.xMin + c, body.yMax - c),
                new Vector2(body.xMin + c, body.yMin + c), new Vector2(body.xMax - c, body.yMin + c)
            };
            int segments = c > 0f ? CornerSegments : 0;
            for (int k = 0; k < 4; k++)
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (k + (segments > 0 ? (float)s / segments : 0f)) * Mathf.PI * .5f;
                    points.Add(centers[k] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * c);
                }
        }
    }
}
