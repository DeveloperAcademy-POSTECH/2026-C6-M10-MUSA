using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>The paper image's baked silhouette. Runtime never reads texture pixels.</summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbAreaProfile : MonoBehaviour
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField, Range(0f, 1f)] private float alphaThreshold = .5f;
        [SerializeField] private Vector2[] lowerBoundary = new Vector2[0];
        [SerializeField] private Vector2[] upperBoundary = new Vector2[0];

        private bool hasCachedShape;
        private Rect cachedRect;
        private float cachedOrbRadius, cachedDrumRadius;
        private LabOrbAreaShape cachedShape;
        private string cachedError;
        private bool cachedValid;

        public Texture2D SourceTexture => sourceTexture;
        public float AlphaThreshold => alphaThreshold;
        public IReadOnlyList<Vector2> LowerBoundary => lowerBoundary;
        public IReadOnlyList<Vector2> UpperBoundary => upperBoundary;

        // Called by the Editor bake, and kept explicit so hand-edited Scene points are never
        // overwritten at runtime. Points are UVs on the existing world-space Quad.
        public void SetBakedBoundary(Texture2D source, float threshold, Vector2[] lower, Vector2[] upper)
        {
            sourceTexture = source;
            alphaThreshold = threshold;
            lowerBoundary = lower != null ? (Vector2[])lower.Clone() : new Vector2[0];
            upperBoundary = upper != null ? (Vector2[])upper.Clone() : new Vector2[0];
            InvalidateCache();
        }

        public bool TryBuildShape(Rect plateWorldRect, float orbRadius, float drumRadius,
            out LabOrbAreaShape shape, out string error)
        {
            if (hasCachedShape && cachedRect == plateWorldRect
                && cachedOrbRadius == orbRadius && cachedDrumRadius == drumRadius)
            {
                shape = cachedShape;
                error = cachedError;
                return cachedValid;
            }
            cachedRect = plateWorldRect;
            cachedOrbRadius = orbRadius;
            cachedDrumRadius = drumRadius;
            hasCachedShape = true;
            cachedValid = BuildShape(plateWorldRect, orbRadius, drumRadius, out cachedShape, out cachedError);
            shape = cachedShape;
            error = cachedError;
            return cachedValid;
        }

        private bool BuildShape(Rect plateWorldRect, float orbRadius, float drumRadius,
            out LabOrbAreaShape shape, out string error)
        {
            shape = null;
            error = "The paper curve profile has not been baked in the saved Scene.";
            if (!ValidPoints(lowerBoundary) || !ValidPoints(upperBoundary)) return false;
            var lower = new Vector2[lowerBoundary.Length];
            var upper = new Vector2[upperBoundary.Length];
            for (int i = 0; i < lower.Length; i++) lower[i] = Map(plateWorldRect, lowerBoundary[i]);
            for (int i = 0; i < upper.Length; i++) upper[i] = Map(plateWorldRect, upperBoundary[i]);
            if (!LabOrbAreaShape.TryCreate(lower, upper, orbRadius, out shape, out error)) return false;
            if (!shape.CanFitRadius(Mathf.Max(orbRadius, drumRadius)))
            {
                shape = null;
                error = "The selected orb or drum is too large for the paper's curved combination area. "
                    + "Reduce its radius or increase Orb Board Height.";
                return false;
            }
            return true;
        }

        public void SetControlPoint(bool upper, int index, Vector2 point)
        {
            Vector2[] points = upper ? upperBoundary : lowerBoundary;
            if (points == null || index < 0 || index >= points.Length) return;
            // The horizontal sampling stays ordered; a Scene handle edits the vertical contour.
            points[index].y = Mathf.Clamp01(point.y);
            InvalidateCache();
        }

        private void InvalidateCache()
        {
            hasCachedShape = false;
            cachedShape = null;
        }

        // The default Inspector edits serialized arrays without calling SetControlPoint.
        private void OnValidate() => InvalidateCache();

        public Vector3 LocalPoint(Vector2 uv) => new Vector3(uv.x - .5f, uv.y - .5f, 0f);

        private static Vector2 Map(Rect rect, Vector2 uv) => new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y));

        private static bool ValidPoints(Vector2[] points)
        {
            if (points == null || points.Length < 2 || !Mathf.Approximately(points[0].x, 0f)
                || !Mathf.Approximately(points[points.Length - 1].x, 1f)) return false;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i];
                if (float.IsNaN(p.x) || float.IsInfinity(p.x) || float.IsNaN(p.y)
                    || float.IsInfinity(p.y) || p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f
                    || i > 0 && p.x <= points[i - 1].x) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            DrawCurve(lowerBoundary, new Color(1f, .7f, .15f));
            DrawCurve(upperBoundary, new Color(.2f, 1f, .8f));
        }

        private void DrawCurve(Vector2[] points, Color colour)
        {
            if (points == null) return;
            Gizmos.color = colour;
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(transform.TransformPoint(LocalPoint(points[i - 1])),
                    transform.TransformPoint(LocalPoint(points[i])));
        }
    }
}
