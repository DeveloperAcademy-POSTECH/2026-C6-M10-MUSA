using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Baked image silhouettes. Runtime uses the saved samples, never texture pixels.</summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbAreaProfile : MonoBehaviour
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField, Range(0f, 1f)] private float alphaThreshold = .5f;
        [SerializeField] private Vector2[] lowerBoundary = new Vector2[0];
        [SerializeField] private Vector2[] upperBoundary = new Vector2[0];
        [SerializeField] private Texture2D lowerOccluderTexture;
        [SerializeField, Range(0f, 1f)] private float lowerOccluderAlphaThreshold = .5f;
        [SerializeField] private Vector2[] lowerOccluderBoundary = new Vector2[0];

        private bool hasCachedShape;
        private bool cachedUsesOccluder;
        private Rect cachedRect, cachedHudRect;
        private float cachedOrbRadius, cachedDrumRadius;
        private float cachedAlphaThreshold, cachedOccluderAlphaThreshold;
        private Texture2D cachedSourceTexture, cachedOccluderTexture;
        private Vector2[] cachedLower, cachedUpper, cachedOccluder;
        private LabOrbAreaShape cachedShape;
        private string cachedError;
        private bool cachedValid;

        public Texture2D SourceTexture => sourceTexture;
        public float AlphaThreshold => alphaThreshold;
        public IReadOnlyList<Vector2> LowerBoundary => lowerBoundary;
        public IReadOnlyList<Vector2> UpperBoundary => upperBoundary;
        public Texture2D LowerOccluderTexture => lowerOccluderTexture;
        public float LowerOccluderAlphaThreshold => lowerOccluderAlphaThreshold;
        public IReadOnlyList<Vector2> LowerOccluderBoundary => lowerOccluderBoundary;

        // Called by the Editor bake. Scene handles and the Inspector may also edit these
        // normalized points; runtime must never replace the artist's layout or control points.
        public void SetBakedBoundary(Texture2D source, float threshold, Vector2[] lower, Vector2[] upper)
        {
            sourceTexture = source;
            alphaThreshold = threshold;
            lowerBoundary = Copy(lower);
            upperBoundary = Copy(upper);
            InvalidateCache();
        }

        /// <summary>The opaque upper contour of the existing bottom HUD, in that image's UVs.</summary>
        public void SetBakedOccluder(Texture2D source, float threshold, Vector2[] top)
        {
            lowerOccluderTexture = source;
            lowerOccluderAlphaThreshold = threshold;
            lowerOccluderBoundary = Copy(top);
            InvalidateCache();
        }

        /// <summary>Historical paper-only adapter for fixtures and callers without a HUD.</summary>
        public bool TryBuildShape(Rect plateWorldRect, float orbRadius, float drumRadius,
            out LabOrbAreaShape shape, out string error) =>
            TryBuildCached(plateWorldRect, default, false, orbRadius, drumRadius, out shape, out error);

        /// <summary>
        /// The plate stays at its saved transform. Only its visible playable silhouette is
        /// computed: bottom = max(paper bottom, opaque HUD top), top = paper top.
        /// </summary>
        public bool TryBuildShape(Rect plateWorldRect, Rect hudWorldRect, float orbRadius, float drumRadius,
            out LabOrbAreaShape shape, out string error) =>
            TryBuildCached(plateWorldRect, hudWorldRect, true, orbRadius, drumRadius, out shape, out error);

        private bool TryBuildCached(Rect plateWorldRect, Rect hudWorldRect, bool useOccluder,
            float orbRadius, float drumRadius, out LabOrbAreaShape shape, out string error)
        {
            if (hasCachedShape && cachedUsesOccluder == useOccluder && cachedRect == plateWorldRect
                && (!useOccluder || cachedHudRect == hudWorldRect)
                && cachedOrbRadius == orbRadius && cachedDrumRadius == drumRadius
                && cachedSourceTexture == sourceTexture && cachedOccluderTexture == lowerOccluderTexture
                && cachedAlphaThreshold == alphaThreshold && cachedOccluderAlphaThreshold == lowerOccluderAlphaThreshold
                && SamePoints(cachedLower, lowerBoundary) && SamePoints(cachedUpper, upperBoundary)
                && SamePoints(cachedOccluder, lowerOccluderBoundary))
            {
                shape = cachedShape;
                error = cachedError;
                return cachedValid;
            }
            cachedRect = plateWorldRect;
            cachedHudRect = hudWorldRect;
            cachedUsesOccluder = useOccluder;
            cachedOrbRadius = orbRadius;
            cachedDrumRadius = drumRadius;
            cachedSourceTexture = sourceTexture;
            cachedOccluderTexture = lowerOccluderTexture;
            cachedAlphaThreshold = alphaThreshold;
            cachedOccluderAlphaThreshold = lowerOccluderAlphaThreshold;
            cachedLower = Copy(lowerBoundary);
            cachedUpper = Copy(upperBoundary);
            cachedOccluder = Copy(lowerOccluderBoundary);
            hasCachedShape = true;
            cachedValid = BuildShape(plateWorldRect, hudWorldRect, useOccluder, orbRadius, drumRadius,
                out cachedShape, out cachedError);
            shape = cachedShape;
            error = cachedError;
            return cachedValid;
        }

        private bool BuildShape(Rect plateWorldRect, Rect hudWorldRect, bool useOccluder,
            float orbRadius, float drumRadius, out LabOrbAreaShape shape, out string error)
        {
            shape = null;
            error = "The paper curve profile has not been baked in the saved Scene.";
            if (!ValidPoints(lowerBoundary) || !ValidPoints(upperBoundary)) return false;
            if (!LabOrbAreaGeometry.Valid(plateWorldRect))
            {
                error = "The existing paper image requires a finite, positive world rectangle.";
                return false;
            }
            Vector2[] lower = Map(plateWorldRect, lowerBoundary), upper = Map(plateWorldRect, upperBoundary);
            if (useOccluder)
            {
                error = "The bottom HUD's opaque upper contour has not been baked in the saved Scene.";
                if (!ValidPoints(lowerOccluderBoundary)) return false;
                if (!LabOrbAreaGeometry.Valid(hudWorldRect))
                {
                    error = "The existing bottom HUD requires a finite, positive world rectangle.";
                    return false;
                }
                // This saved HUD spans the entire plate and extends slightly beyond its sides.
                // Partial-width overlays need a different silhouette representation, not a
                // rectangle or a fabricated continuation through the uncovered part of the paper.
                float tolerance = Mathf.Max(.00001f, plateWorldRect.width * .00001f);
                if (hudWorldRect.xMin > plateWorldRect.xMin + tolerance
                    || hudWorldRect.xMax < plateWorldRect.xMax - tolerance)
                {
                    error = "The bottom HUD must cover the paper's full width to form its lower visible boundary.";
                    return false;
                }
                lower = MergeMaximum(lower, Map(hudWorldRect, lowerOccluderBoundary));
            }
            if (!LabOrbAreaShape.TryCreate(lower, upper, orbRadius, out shape, out error)) return false;
            if (!shape.CanFitRadius(Mathf.Max(orbRadius, drumRadius)))
            {
                shape = null;
                error = useOccluder
                    ? "The selected orb or drum is too large for the visible paper area above the bottom HUD. "
                        + "Reduce its radius or increase Orb Board Height."
                    : "The selected orb or drum is too large for the paper's curved combination area. "
                        + "Reduce its radius or increase Orb Board Height.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// The maximum of two polylines is piecewise linear. Their combined vertices split
        /// the domain into linear intervals; a sign change adds the exact intersection inside
        /// each interval. Simply resampling a fixed grid would round off those transitions.
        /// </summary>
        private static Vector2[] MergeMaximum(Vector2[] paper, Vector2[] hud)
        {
            float left = paper[0].x, right = paper[paper.Length - 1].x;
            var union = new SortedSet<float>();
            foreach (Vector2 point in paper) union.Add(point.x);
            foreach (Vector2 point in hud)
                if (point.x > left && point.x < right) union.Add(point.x);
            var knots = new List<float>(union);
            for (int i = 1; i < knots.Count; i++)
            {
                float a = knots[i - 1], b = knots[i];
                float da = Evaluate(paper, a) - Evaluate(hud, a);
                float db = Evaluate(paper, b) - Evaluate(hud, b);
                if ((da < 0f && db > 0f) || (da > 0f && db < 0f))
                {
                    float intersection = a + (b - a) * da / (da - db);
                    if (intersection > a && intersection < b) union.Add(intersection);
                }
            }
            var result = new Vector2[union.Count];
            int index = 0;
            foreach (float x in union)
                result[index++] = new Vector2(x, Mathf.Max(Evaluate(paper, x), Evaluate(hud, x)));
            return result;
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

        private static Vector2[] Map(Rect rect, Vector2[] points)
        {
            var result = new Vector2[points.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Map(rect, points[i]);
            return result;
        }

        private static float Evaluate(Vector2[] points, float x)
        {
            if (x <= points[0].x) return points[0].y;
            for (int i = 1; i < points.Length; i++)
                if (x <= points[i].x)
                    return Mathf.LerpUnclamped(points[i - 1].y, points[i].y,
                        (x - points[i - 1].x) / (points[i].x - points[i - 1].x));
            return points[points.Length - 1].y;
        }

        private static Vector2[] Copy(Vector2[] points) => points != null ? (Vector2[])points.Clone() : new Vector2[0];

        private static bool SamePoints(Vector2[] first, Vector2[] second)
        {
            if (first == null || second == null) return first == second;
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++) if (!first[i].Equals(second[i])) return false;
            return true;
        }

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
