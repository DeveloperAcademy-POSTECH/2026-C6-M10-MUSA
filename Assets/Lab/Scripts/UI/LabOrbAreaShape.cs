using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// An immutable XY band between two left-to-right polylines. The top and bottom are
    /// physical surfaces; its left and right limits are open ownership-transfer planes.
    /// A body's usable Y interval is the band eroded by its circular XY cross-section.
    /// </summary>
    public sealed class LabOrbAreaShape
    {
        private const float Epsilon = .00001f;
        private readonly Vector2[] lower;
        private readonly Vector2[] upper;
        private readonly ReadOnlyCollection<Vector2> lowerView;
        private readonly ReadOnlyCollection<Vector2> upperView;
        private readonly float gapSlopeBound;

        public Rect CenterBounds { get; }
        public float ReferenceRadius { get; }
        public bool IsCurved { get; }
        public IReadOnlyList<Vector2> LowerBoundary => lowerView;
        public IReadOnlyList<Vector2> UpperBoundary => upperView;
        public IReadOnlyList<Vector2> BottomBoundary => lowerView;
        public IReadOnlyList<Vector2> TopBoundary => upperView;
        public IReadOnlyList<Vector2> Bottom => lowerView;
        public IReadOnlyList<Vector2> Top => upperView;

        private LabOrbAreaShape(Vector2[] lowerPoints, Vector2[] upperPoints,
            float referenceRadius, bool curved, Rect centerBounds)
        {
            lower = lowerPoints;
            upper = upperPoints;
            lowerView = Array.AsReadOnly(lower);
            upperView = Array.AsReadOnly(upper);
            ReferenceRadius = referenceRadius;
            IsCurved = curved;
            CenterBounds = centerBounds;
            gapSlopeBound = MaximumSlope(lower) + MaximumSlope(upper);
        }

        /// <summary>
        /// Copies the supplied surface samples. X must increase strictly and both curves
        /// must have the same side limits. A circle of referenceRadius must fit everywhere.
        /// </summary>
        public static bool TryCreate(IReadOnlyList<Vector2> lowerPoints,
            IReadOnlyList<Vector2> upperPoints, float referenceRadius,
            out LabOrbAreaShape shape, out string error)
        {
            shape = null;
            error = "The board requires finite, left-to-right top and bottom surface samples.";
            if (!NonNegative(referenceRadius) || !ValidCurve(lowerPoints) || !ValidCurve(upperPoints))
                return false;

            float left = lowerPoints[0].x, right = lowerPoints[lowerPoints.Count - 1].x;
            float tolerance = Mathf.Max(Epsilon, (right - left) * Epsilon);
            if (Mathf.Abs(upperPoints[0].x - left) > tolerance
                || Mathf.Abs(upperPoints[upperPoints.Count - 1].x - right) > tolerance)
                return false;

            Vector2[] bottom = Copy(lowerPoints), top = Copy(upperPoints);
            top[0].x = left;
            top[top.Length - 1].x = right;
            // A piecewise linear gap can change sign only at a vertex of either curve.
            foreach (Vector2 point in bottom)
                if (point.y >= Evaluate(top, point.x) - Epsilon) return false;
            foreach (Vector2 point in top)
                if (Evaluate(bottom, point.x) >= point.y - Epsilon) return false;

            float minY = bottom[0].y, maxY = top[0].y;
            foreach (Vector2 point in bottom) minY = Mathf.Min(minY, point.y);
            foreach (Vector2 point in top) maxY = Mathf.Max(maxY, point.y);
            Rect bounds = Rect.MinMaxRect(left, minY + referenceRadius, right, maxY - referenceRadius);
            if (!LabOrbAreaGeometry.Valid(bounds))
            {
                error = "The reference orb is too large for the board surface.";
                return false;
            }
            var candidate = new LabOrbAreaShape(bottom, top, referenceRadius, true, bounds);
            if (!candidate.CanFitRadius(referenceRadius))
            {
                error = "The reference orb cannot fit through the full curved board, including both side openings.";
                return false;
            }
            shape = candidate;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Preserves the historical centre rectangle exactly. Only its top/bottom surfaces
        /// are offset by referenceRadius; the open side planes receive no additional inset.
        /// </summary>
        public static LabOrbAreaShape FromCenterBounds(Rect bounds, float referenceRadius)
        {
            if (!LabOrbAreaGeometry.Valid(bounds) || !NonNegative(referenceRadius))
                throw new ArgumentException("A finite positive centre rectangle and non-negative radius are required.");
            return new LabOrbAreaShape(new[] {
                new Vector2(bounds.xMin, bounds.yMin - referenceRadius),
                new Vector2(bounds.xMax, bounds.yMin - referenceRadius)
            }, new[] {
                new Vector2(bounds.xMin, bounds.yMax + referenceRadius),
                new Vector2(bounds.xMax, bounds.yMax + referenceRadius)
            }, referenceRadius, false, bounds);
        }

        /// <summary>
        /// The exact vertical interval for the centre of a circle at X. The closest point
        /// on every segment (including its endpoints) supplies the normal clearance.
        /// X is clamped only to the side transfer planes, never inset by the body's radius.
        /// An interval with x greater than y means that this radius cannot fit at this X.
        /// </summary>
        public Vector2 VerticalRange(float x, float radius)
        {
            if (!Finite(x) || !NonNegative(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), "Finite X and non-negative radius are required.");
            x = Mathf.Clamp(x, CenterBounds.xMin, CenterBounds.xMax);
            if (radius == 0f) return new Vector2(Evaluate(lower, x), Evaluate(upper, x));
            return new Vector2(CircleLimit(lower, x, radius, true), CircleLimit(upper, x, radius, false));
        }

        public bool Contains(Vector2 world, float radius = 0f)
        {
            if (!Finite(world) || !NonNegative(radius)
                || world.x < CenterBounds.xMin - Epsilon || world.x > CenterBounds.xMax + Epsilon)
                return false;
            Vector2 range = VerticalRange(world.x, radius);
            return range.x <= range.y + Epsilon
                && world.y >= range.x - Epsilon && world.y <= range.y + Epsilon;
        }

        /// <summary>
        /// Keeps a body on the visible paper. For an outside point, valid normal projections
        /// onto the inset line segments and corner arcs compete with the vertical fallback;
        /// this avoids a vertical-only correction on a sloping surface. No side wall is added.
        /// </summary>
        public Vector2 Clamp(Vector2 world, float radius)
        {
            if (!Finite(world) || !NonNegative(radius))
                throw new ArgumentException("A finite position and non-negative radius are required.");
            if (Contains(world, radius)) return world;
            Vector2 best = VerticalClamp(world, radius);
            float distance = (best - world).sqrMagnitude;
            if (!IsCurved) return best;
            ProjectBoundary(lower, true, world, radius, ref best, ref distance);
            ProjectBoundary(upper, false, world, radius, ref best, ref distance);
            return best;
        }

        public Vector2 Clamp(Vector2 world) => Clamp(world, ReferenceRadius);

        /// <summary>Y is relative to this X and this body's radius, not a surrounding rectangle.</summary>
        public Vector2 ToNormalized(Vector2 world, float radius)
        {
            float x = Mathf.Clamp(world.x, CenterBounds.xMin, CenterBounds.xMax);
            Vector2 range = VerticalRange(x, radius);
            return new Vector2(Mathf.InverseLerp(CenterBounds.xMin, CenterBounds.xMax, x),
                Mathf.InverseLerp(range.x, range.y, world.y));
        }

        public Vector2 ToWorld(Vector2 normalized, float radius)
        {
            float x = Mathf.Lerp(CenterBounds.xMin, CenterBounds.xMax, Mathf.Clamp01(normalized.x));
            Vector2 range = VerticalRange(x, radius);
            return new Vector2(x, Mathf.Lerp(range.x, range.y, Mathf.Clamp01(normalized.y)));
        }

        public Vector2 ToNormalized(Vector2 world) => ToNormalized(world, ReferenceRadius);
        public Vector2 ToWorld(Vector2 normalized) => ToWorld(normalized, ReferenceRadius);

        /// <summary>
        /// Tests both openings and the full width. Disk erosion preserves each graph's maximum
        /// slope, so endpoint gap values bound the minimum gap within an interval. Ambiguous
        /// intervals are subdivided instead of accepting a fixed set of samples as proof.
        /// </summary>
        public bool CanFitRadius(float radius)
        {
            if (!NonNegative(radius)) return false;
            if (!Finite(gapSlopeBound)) return false;
            float left = CenterBounds.xMin, right = CenterBounds.xMax;
            float leftGap = Gap(left, radius), rightGap = Gap(right, radius);
            if (!Finite(leftGap) || !Finite(rightGap) || leftGap <= Epsilon || rightGap <= Epsilon) return false;
            int remaining = 16384;
            return FitsSpan(left, right, leftGap, rightGap, radius, 0, ref remaining);
        }

        public bool CanFit(float radius) => CanFitRadius(radius);

        public bool ApproximatelyEquals(LabOrbAreaShape other, float tolerance = .0001f)
        {
            if (other == null || IsCurved != other.IsCurved || !NonNegative(tolerance)
                || Mathf.Abs(ReferenceRadius - other.ReferenceRadius) > tolerance
                || lower.Length != other.lower.Length || upper.Length != other.upper.Length) return false;
            float squared = tolerance * tolerance;
            for (int i = 0; i < lower.Length; i++)
                if ((lower[i] - other.lower[i]).sqrMagnitude > squared) return false;
            for (int i = 0; i < upper.Length; i++)
                if ((upper[i] - other.upper[i]).sqrMagnitude > squared) return false;
            return true;
        }

        private Vector2 VerticalClamp(Vector2 world, float radius)
        {
            float x = Mathf.Clamp(world.x, CenterBounds.xMin, CenterBounds.xMax);
            Vector2 range = VerticalRange(x, radius);
            if (range.x > range.y + Epsilon)
                throw new ArgumentException("The body's radius does not fit at this board position.", nameof(radius));
            return new Vector2(x, Mathf.Clamp(world.y, range.x, range.y));
        }

        private void ProjectBoundary(Vector2[] curve, bool bottom, Vector2 world, float radius,
            ref Vector2 best, ref float bestDistance)
        {
            for (int i = 0; i < curve.Length - 1; i++)
            {
                Vector2 direction = curve[i + 1] - curve[i];
                Vector2 inward = new Vector2(-direction.y, direction.x).normalized * (bottom ? 1f : -1f);
                Vector2 a = curve[i] + inward * radius, b = curve[i + 1] + inward * radius;
                float t = Mathf.Clamp01(Vector2.Dot(world - a, direction) / direction.sqrMagnitude);
                Consider(world, a + (b - a) * t, radius, ref best, ref bestDistance);
                // An inset vertex may be circular rather than the intersection of two lines.
                Vector2 fromVertex = world - curve[i];
                if (fromVertex.sqrMagnitude > Epsilon * Epsilon)
                    Consider(world, curve[i] + fromVertex.normalized * radius, radius, ref best, ref bestDistance);
                Consider(world, a, radius, ref best, ref bestDistance);
                Consider(world, b, radius, ref best, ref bestDistance);
                if (i == curve.Length - 2)
                {
                    Vector2 fromEnd = world - curve[i + 1];
                    if (fromEnd.sqrMagnitude > Epsilon * Epsilon)
                        Consider(world, curve[i + 1] + fromEnd.normalized * radius,
                            radius, ref best, ref bestDistance);
                }
                if (i == 0) continue;
                Vector2 previous = curve[i] - curve[i - 1];
                Vector2 previousInward = new Vector2(-previous.y, previous.x).normalized * (bottom ? 1f : -1f);
                Vector2 previousEnd = curve[i] + previousInward * radius;
                float denominator = Cross(previous, direction);
                if (Mathf.Abs(denominator) <= Epsilon) continue;
                // Inward offsets meet at a sharp join on a convex part of the original band.
                float alongPrevious = Cross(a - previousEnd, direction) / denominator;
                Consider(world, previousEnd + previous * alongPrevious, radius, ref best, ref bestDistance);
            }
        }

        private void Consider(Vector2 world, Vector2 candidate, float radius,
            ref Vector2 best, ref float distance)
        {
            if (!Contains(candidate, radius)) return;
            float squared = (candidate - world).sqrMagnitude;
            if (squared >= distance) return;
            best = candidate;
            distance = squared;
        }

        private float Gap(float x, float radius)
        {
            Vector2 range = VerticalRange(x, radius);
            return range.y - range.x;
        }

        private bool FitsSpan(float left, float right, float leftGap, float rightGap,
            float radius, int depth, ref int remaining)
        {
            // Every point is at least max(leftGap - L*dx, rightGap - L*(width-dx)).
            // Its minimum is the intersection of those two lines (or an endpoint).
            float minimumBound = Mathf.Min(Mathf.Min(leftGap, rightGap),
                (leftGap + rightGap - gapSlopeBound * (right - left)) * .5f);
            if (minimumBound > Epsilon) return true;
            // A pathological near-zero passage is rejected rather than freezing the frame or
            // accepting a body that might overlap a wall. This tolerance is world-space only.
            if (depth >= 24 || --remaining <= 0) return false;
            float middle = (left + right) * .5f;
            if (middle <= left || middle >= right) return false;
            float middleGap = Gap(middle, radius);
            return Finite(middleGap) && middleGap > Epsilon
                && FitsSpan(left, middle, leftGap, middleGap, radius, depth + 1, ref remaining)
                && FitsSpan(middle, right, middleGap, rightGap, radius, depth + 1, ref remaining);
        }

        private static float CircleLimit(Vector2[] curve, float x, float radius, bool bottom)
        {
            float limit = bottom ? float.NegativeInfinity : float.PositiveInfinity;
            for (int i = 0; i < curve.Length - 1; i++)
            {
                Vector2 a = curve[i], b = curve[i + 1];
                float left = Mathf.Max(a.x, x - radius), right = Mathf.Min(b.x, x + radius);
                if (left > right) continue;
                float slope = (b.y - a.y) / (b.x - a.x);
                // Extremum of y(q) +/- sqrt(r² - (q.x - x)²), clipped to this segment.
                float dx = slope * radius / Mathf.Sqrt(1f + slope * slope) * (bottom ? 1f : -1f);
                float qx = Mathf.Clamp(x + dx, left, right);
                float qy = Mathf.LerpUnclamped(a.y, b.y, (qx - a.x) / (b.x - a.x));
                float height = Mathf.Sqrt(Mathf.Max(0f, radius * radius - (qx - x) * (qx - x)));
                float candidate = qy + (bottom ? height : -height);
                limit = bottom ? Mathf.Max(limit, candidate) : Mathf.Min(limit, candidate);
            }
            return limit;
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

        private static bool ValidCurve(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 2) return false;
            for (int i = 0; i < points.Count; i++)
                if (!Finite(points[i]) || (i > 0 && points[i].x <= points[i - 1].x)) return false;
            return true;
        }

        private static Vector2[] Copy(IReadOnlyList<Vector2> points)
        {
            var result = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++) result[i] = points[i];
            return result;
        }

        private static float MaximumSlope(Vector2[] points)
        {
            float maximum = 0f;
            for (int i = 1; i < points.Length; i++)
                maximum = Mathf.Max(maximum, Mathf.Abs((points[i].y - points[i - 1].y)
                    / (points[i].x - points[i - 1].x)));
            return maximum;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool NonNegative(float value) => Finite(value) && value >= 0f;
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
