using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Native dashed rectangle for the Figma team-area primitive (2pt stroke, 16pt corners).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameDashedRoundedGraphic : MaskableGraphic
    {
        public float radius = 16, strokeWidth = 2, dashLength = 4, gapLength = 4;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float half = strokeWidth * .5f;
            rect = Rect.MinMaxRect(rect.xMin + half, rect.yMin + half, rect.xMax - half, rect.yMax - half);
            float corner = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
            var points = new List<Vector2>(52);
            for (int c = 0; c < 4; c++)
            {
                Vector2 center = new Vector2(c == 0 || c == 3 ? rect.xMax - corner : rect.xMin + corner,
                    c < 2 ? rect.yMax - corner : rect.yMin + corner);
                for (int s = 0; s <= 12; s++)
                {
                    float angle = (c * 90 + s * 7.5f) * Mathf.Deg2Rad;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * corner);
                }
            }
            float travelled = 0, period = Mathf.Max(1, dashLength + gapLength);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count], delta = b - a;
                float length = delta.magnitude, at = 0;
                if (length <= .0001f) continue;
                Vector2 direction = delta / length, normal = new Vector2(-direction.y, direction.x) * half;
                while (at < length - .0001f)
                {
                    float phase = travelled % period;
                    bool draw = phase < dashLength;
                    float step = Mathf.Min(length - at, draw ? dashLength - phase : period - phase);
                    if (step < .0001f) { travelled += .001f; continue; }
                    if (draw)
                    {
                        Vector2 from = a + direction * at, to = from + direction * step;
                        int first = vh.currentVertCount;
                        vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
                        vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
                        vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
                    }
                    at += step; travelled += step;
                }
            }
        }
    }
}
