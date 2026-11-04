using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477)의 "화면 어둡게 하고 한 곳만 밝히기". 화면 전체를 메시 한 장으로 덮고 구멍을 하나 낸다.
    /// 구멍은 둥근 모서리 사각형이라 동그라미(박·북)와 알약 모양(기운 막대)을 다 그린다. 구멍 둘레는 안쪽부터 바깥쪽으로
    /// 점점 어두워진다. 반투명 조각 여러 장을 이어 붙이면 겹친 줄이 두 번 어두워져 네모난 테두리로 보이므로,
    /// 겹치는 곳이 없는 한 장으로 그린다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameTutorialSpotlight : MaskableGraphic
    {
        private const int CornerSegments = 18;
        private const float EdgeStep = 10f;
        /// <summary>A straight side shorter than this (UI units) is treated as none: the two arcs meet.</summary>
        private const float MinimumSide = .01f;
        private Rect hole;
        private float corner;
        private float soft;
        private float angle;
        private readonly List<Vector2> outline = new List<Vector2>();
        private readonly List<Vector2> normals = new List<Vector2>();

        public Rect Hole => hole;
        public float Corner => corner;
        public float Angle => angle;

        /// <summary>
        /// A hole in this graphic's local space with rounded corners (a circle when <paramref name="cornerRadius"/> is
        /// half its side), turned <paramref name="degrees"/> counter-clockwise about its center (a tilted capsule).
        /// <paramref name="softWidth"/> is the fading rim inside the hole's edge.
        /// </summary>
        public void SetHole(Rect area, float cornerRadius, float softWidth, float degrees = 0f)
        {
            area.width = Mathf.Max(0f, area.width);
            area.height = Mathf.Max(0f, area.height);
            cornerRadius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(area.width, area.height) * .5f);
            softWidth = Mathf.Clamp(softWidth, 0f, cornerRadius);
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) degrees = 0f;
            if (area == hole && Mathf.Approximately(cornerRadius, corner) && Mathf.Approximately(softWidth, soft)
                && Mathf.Approximately(degrees, angle)) return;
            hole = area;
            corner = cornerRadius;
            soft = softWidth;
            angle = degrees;
            SetVerticesDirty();
        }

        /// <summary>Turn outline points and normals <paramref name="degrees"/> counter-clockwise about <paramref name="pivot"/>.</summary>
        public static void Rotate(List<Vector2> points, List<Vector2> outward, Vector2 pivot, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f)) return;
            Quaternion turn = Quaternion.Euler(0f, 0f, degrees);
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = pivot + (Vector2)(turn * (points[i] - pivot));
                outward[i] = turn * outward[i];
            }
        }

        /// <summary>A round hole: <see cref="SetHole(Rect, float, float)"/> with a square and half its side as the corner.</summary>
        public void SetCircle(Vector2 center, float radius, float softWidth) =>
            SetHole(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), radius, softWidth);

        /// <summary>
        /// Points around a rounded rectangle, counter-clockwise from the right, each with its outward normal.
        /// Straight sides are split every <see cref="EdgeStep"/> so no two neighbours are far apart seen from the center.
        /// </summary>
        public static void Outline(Rect area, float cornerRadius, List<Vector2> points, List<Vector2> outward)
        {
            points.Clear();
            outward.Clear();
            float c = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(area.width, area.height) * .5f);
            Vector2[] centers =
            {
                new Vector2(area.xMax - c, area.yMax - c), new Vector2(area.xMin + c, area.yMax - c),
                new Vector2(area.xMin + c, area.yMin + c), new Vector2(area.xMax - c, area.yMin + c)
            };
            // Each point appears once. A corner's arc stops one step short of its end, and the straight side that
            // follows starts exactly there; for a round hole the sides have no length, so the next arc carries on.
            // Repeating the shared end instead left a near-zero edge (360° vs 0° differ in the last digits) whose
            // direction is noise, a sliver in the mesh.
            for (int k = 0; k < 4; k++)
            {
                for (int s = 0; s < CornerSegments; s++)
                {
                    float angle = (k + (float)s / CornerSegments) * Mathf.PI * .5f;
                    var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    points.Add(centers[k] + normal * c);
                    outward.Add(normal);
                }
                // The straight side toward the next corner, from the arc's end up to (not including) the next arc.
                float endAngle = (k + 1) * Mathf.PI * .5f;
                var sideNormal = new Vector2(Mathf.Round(Mathf.Cos(endAngle)), Mathf.Round(Mathf.Sin(endAngle)));
                Vector2 from = centers[k] + sideNormal * c, to = centers[(k + 1) % 4] + sideNormal * c;
                float length = Vector2.Distance(from, to);
                int pieces = length > MinimumSide ? Mathf.Max(1, Mathf.FloorToInt(length / EdgeStep)) : 0;
                for (int i = 0; i < pieces; i++)
                {
                    points.Add(Vector2.Lerp(from, to, (float)i / pieces));
                    outward.Add(sideNormal);
                }
            }
        }

        /// <summary>
        /// How far out the dark ring must reach along each outline direction so that its polygon still covers every
        /// corner of <paramref name="area"/>, given the widest angle between two neighbouring directions.
        /// </summary>
        public static float CoveringRadius(Rect area, Vector2 center, IReadOnlyList<Vector2> points)
        {
            float farthest = 0f;
            foreach (Vector2 cornerPoint in new[]
                { area.min, area.max, new Vector2(area.xMin, area.yMax), new Vector2(area.xMax, area.yMin) })
                farthest = Mathf.Max(farthest, Vector2.Distance(center, cornerPoint));
            float widest = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i] - center, b = points[(i + 1) % points.Count] - center;
                widest = Mathf.Max(widest, Vector2.Angle(a, b));
                farthest = Mathf.Max(farthest, a.magnitude);
            }
            float half = Mathf.Min(widest, 120f) * .5f * Mathf.Deg2Rad;
            return farthest / Mathf.Cos(half) + 1f;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            Color32 dark = color;
            if (hole.width <= 0f || hole.height <= 0f)
            {
                mesh.AddVert(new Vector3(area.xMin, area.yMin), dark, Vector4.zero);
                mesh.AddVert(new Vector3(area.xMin, area.yMax), dark, Vector4.zero);
                mesh.AddVert(new Vector3(area.xMax, area.yMax), dark, Vector4.zero);
                mesh.AddVert(new Vector3(area.xMax, area.yMin), dark, Vector4.zero);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(2, 3, 0);
                return;
            }
            Color32 clear = dark;
            clear.a = 0;
            Outline(hole, corner, outline, normals);
            Vector2 center = hole.center;
            Rotate(outline, normals, center, angle);
            float far = CoveringRadius(area, center, outline);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 point = outline[i];
                Vector2 away = point - center;
                away = away.sqrMagnitude > 1e-8f ? away.normalized : normals[i];
                mesh.AddVert(point - normals[i] * soft, clear, Vector4.zero);
                mesh.AddVert(point, dark, Vector4.zero);
                mesh.AddVert(center + away * far, dark, Vector4.zero);
            }
            for (int i = 0; i < outline.Count; i++)
            {
                int a = 3 * i, b = 3 * ((i + 1) % outline.Count);
                // Soft rim: clear inside, fully dark at the hole's edge.
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
                // Everything beyond, out past the screen corners.
                mesh.AddTriangle(a + 1, a + 2, b + 2);
                mesh.AddTriangle(a + 1, b + 2, b + 1);
            }
        }
    }
}
