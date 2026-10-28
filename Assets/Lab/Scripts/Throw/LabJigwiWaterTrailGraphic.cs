using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Passive Canvas ink on top of B's opaque curtain. Every centre point is projected from a
    /// position the real projectile visited; it cannot aim, move, collide or confirm a hit.
    /// </summary>
    [DefaultExecutionOrder(80)]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabJigwiWaterTrailGraphic : UnityEngine.UI.MaskableGraphic
    {
        private const int MaximumPoints = 2048, MaximumTraces = 16;
        [SerializeField, Range(2f, 32f)] private float minimumWidth = 7f;
        [SerializeField, Range(4f, 48f)] private float maximumWidth = 20f;
        [SerializeField] private Color ink = new Color(.015f, .105f, .2f, .96f);
        [SerializeField] private Color water = new Color(.12f, .64f, .98f, .98f);
        [SerializeField] private Color highlight = new Color(.72f, .97f, 1f, .94f);
        private readonly Point[] points = new Point[MaximumPoints];
        private readonly Trace[] traces = new Trace[MaximumTraces];
        private Canvas ownerCanvas;
        private Camera worldCamera;
        private ulong owner;
        private string eventId;
        private bool showing, held;
        private int pointCount, traceCount;
        private struct Point { public Vector2 Position; public float Alpha; }
        private struct Trace { public int Start, Count; public float Width; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        public int VisibleTraceCount => traceCount;
        public int ProjectedPointCount => pointCount;
        public Vector2 GetProjectedPoint(int index) => points[index].Position;

        public void Present(Canvas targetCanvas, Camera camera, ulong localOwner, string fireEventId, double battleNow, bool pauseHeld)
        {
            bool changed = owner != localOwner || eventId != fireEventId || ownerCanvas != targetCanvas || worldCamera != camera;
            ownerCanvas = targetCanvas; worldCamera = camera; owner = localOwner; eventId = fireEventId;
            showing = targetCanvas != null && camera != null && !string.IsNullOrEmpty(eventId);
            raycastTarget = false;
            bool rebuild = changed || !held || !pauseHeld;
            held = pauseHeld;
            if (rebuild) RebuildProjection();
        }

        /// <summary>Keep a just-resolved B shot fading after the last ignition completes the round.</summary>
        public void ContinueFading(bool pauseHeld)
        {
            bool rebuild = !held || !pauseHeld;
            held = pauseHeld;
            if (rebuild) RebuildProjection();
        }

        public void Hide()
        {
            showing = held = false; eventId = null; ownerCanvas = null; worldCamera = null;
            pointCount = traceCount = 0; SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (showing && !held) RebuildProjection();
        }

        private void RebuildProjection()
        {
            pointCount = traceCount = 0;
            if (!showing || ownerCanvas == null || worldCamera == null) { SetVerticesDirty(); return; }
            Camera uiCamera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
            var histories = LabJigwiWaterFlightPath.Live;
            for (int h = histories.Count - 1; h >= 0 && traceCount < MaximumTraces && pointCount < MaximumPoints; h--)
            {
                var history = histories[h];
                if (history == null || history.Owner != owner || history.FireEventId != eventId || history.Count < 1) continue;
                int first = pointCount;
                float width = minimumWidth;
                for (int i = 0; i < history.Count && pointCount < MaximumPoints; i++)
                {
                    Vector3 position = history.GetPosition(i);
                    if (worldCamera.WorldToViewportPoint(position).z <= 0f) continue;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                        worldCamera.WorldToScreenPoint(position), uiCamera, out Vector2 projected)) continue;
                    float alpha = Mathf.Clamp01(1f - (Time.time - history.GetTime(i)) / history.Lifetime);
                    if (alpha <= 0f) continue;
                    points[pointCount++] = new Point { Position = projected, Alpha = alpha };
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                        worldCamera.WorldToScreenPoint(position + worldCamera.transform.right * history.Width), uiCamera, out Vector2 rim))
                        width = Mathf.Clamp(Vector2.Distance(projected, rim), minimumWidth, Mathf.Max(minimumWidth, maximumWidth));
                }
                int count = pointCount - first;
                if (count > 0) traces[traceCount++] = new Trace { Start = first, Count = count, Width = width };
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            for (int t = 0; t < traceCount; t++)
            {
                Trace trace = traces[t];
                for (int pass = 0; pass < 3; pass++)
                {
                    float width = trace.Width * (pass == 0 ? 1f : pass == 1 ? .72f : .2f);
                    Color tint = pass == 0 ? ink : pass == 1 ? water : highlight;
                    for (int i = 1; i < trace.Count; i++)
                    {
                        Point a = points[trace.Start + i - 1], b = points[trace.Start + i];
                        Vector2 delta = b.Position - a.Position;
                        if (delta.sqrMagnitude < .0001f) continue;
                        Vector2 side = new Vector2(-delta.y, delta.x).normalized;
                        float from = width * .5f * Mathf.Lerp(.22f, 1f, (i - 1f) / Mathf.Max(1, trace.Count - 1));
                        float to = width * .5f * Mathf.Lerp(.22f, 1f, i / (float)Mathf.Max(1, trace.Count - 1));
                        Color ca = tint; ca.a *= a.Alpha;
                        Color cb = tint; cb.a *= b.Alpha;
                        Quad(mesh, a.Position - side * from, a.Position + side * from,
                            b.Position + side * to, b.Position - side * to, ca, cb);
                    }
                }
                Point head = points[trace.Start + trace.Count - 1];
                Color outline = ink; outline.a *= head.Alpha;
                Color blue = water; blue.a *= head.Alpha;
                Color light = highlight; light.a *= head.Alpha;
                Disc(mesh, head.Position, trace.Width * .64f, outline);
                Disc(mesh, head.Position, trace.Width * .46f, blue);
                Disc(mesh, head.Position + new Vector2(-.13f, .15f) * trace.Width, trace.Width * .14f, light);
                // Separated droplets stay on older real passed points instead of an invented aim line.
                for (int i = 1; i < trace.Count - 2; i += 3)
                {
                    Point point = points[trace.Start + i];
                    Color drop = water; drop.a *= point.Alpha;
                    Disc(mesh, point.Position, trace.Width * .24f, drop);
                }
            }
        }

        private static void Quad(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color start, Color end)
        {
            int at = mesh.currentVertCount;
            mesh.AddVert(a, start, Vector2.zero); mesh.AddVert(b, start, Vector2.zero);
            mesh.AddVert(c, end, Vector2.zero); mesh.AddVert(d, end, Vector2.zero);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }
        private static void Disc(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int sides = 12;
            int at = mesh.currentVertCount; mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * (Mathf.PI * 2f / sides);
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(at, at + i, at + i + 1);
            }
        }
        protected override void OnDisable() { Hide(); base.OnDisable(); }
    }
}
