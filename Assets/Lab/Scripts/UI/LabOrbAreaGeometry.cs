using UnityEngine;

namespace C6Lab
{
    /// <summary>Converts the saved Canvas play area into the existing XY physics plane.</summary>
    public static class LabOrbAreaGeometry
    {
        public const float SurfaceClearance = .05f;

        public static Rect ScreenRect(RectTransform target, Camera uiCamera, Vector3[] corners)
        {
            target.GetWorldCorners(corners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 min = first, max = first;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static bool TryCenterBounds(Camera camera, Rect screenArea, float boardHeight,
            float orbRadius, float drumRadius, out Rect bounds, out string error)
        {
            bounds = default;
            error = "The orb camera and a positive play area are required.";
            if (camera == null || !camera.orthographic || !Positive(boardHeight)
                || !Positive(orbRadius) || !Positive(drumRadius)
                || !Positive(camera.orthographicSize) || !Valid(screenArea)) return false;

            Rect viewport = camera.pixelRect;
            Rect clipped = Rect.MinMaxRect(Mathf.Max(screenArea.xMin, viewport.xMin),
                Mathf.Max(screenArea.yMin, viewport.yMin), Mathf.Min(screenArea.xMax, viewport.xMax),
                Mathf.Min(screenArea.yMax, viewport.yMax));
            if (!Valid(clipped)) return false;
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!Project(camera, plane, clipped.min, out Vector2 lower)
                || !Project(camera, plane, clipped.max, out Vector2 upper)
                || !Project(camera, plane, viewport.center, out Vector2 centre)) return false;

            // DEV validates a candidate height without changing the actual camera or saved Config.
            float ratio = boardHeight / (camera.orthographicSize * 2f);
            lower = centre + (lower - centre) * ratio;
            upper = centre + (upper - centre) * ratio;
            Vector2 min = Vector2.Min(lower, upper), max = Vector2.Max(lower, upper);
            float largest = Mathf.Max(orbRadius, drumRadius);
            if (max.x - min.x <= 2f * (largest + SurfaceClearance)
                || max.y - min.y <= 2f * (largest + SurfaceClearance))
            {
                error = "The selected orb or drum is too large for the visible combination area. "
                    + "Reduce its radius or increase Orb Board Height.";
                return false;
            }
            float inset = orbRadius + SurfaceClearance;
            bounds = Rect.MinMaxRect(min.x + inset, min.y + inset, max.x - inset, max.y - inset);
            error = string.Empty;
            return true;
        }

        private static bool Project(Camera camera, Plane plane, Vector2 screen, out Vector2 world)
        {
            world = default;
            Ray ray = camera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance) || distance < 0f) return false;
            Vector3 hit = ray.GetPoint(distance);
            world = new Vector2(hit.x, hit.y);
            return Finite(world.x) && Finite(world.y);
        }

        public static bool Valid(Rect rect) => Finite(rect.xMin) && Finite(rect.yMin)
            && Finite(rect.xMax) && Finite(rect.yMax) && rect.width > 0f && rect.height > 0f;
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
