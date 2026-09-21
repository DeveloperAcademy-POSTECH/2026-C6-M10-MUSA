using System;
using C6.Prototype.Attack;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>Fits the fixed target inside the real HUD-free upper viewport without changing either viewport.</summary>
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class ThrowBattleFraming : MonoBehaviour
    {
        [SerializeField] SplitScreenLayout layout;
        [SerializeField] T09Hud hud;
        [SerializeField] BenchmarkMonster monster;
        [SerializeField] Vector3 baselinePosition;
        [SerializeField] Quaternion baselineRotation = Quaternion.identity;
        [SerializeField] float baselineFieldOfView;
        [SerializeField] bool hasBaseline;
        float participantYaw;

        readonly Vector3[] corners = new Vector3[4];
        Renderer[] visualRenderers;
        Bounds lastBounds;
        Vector3 lastCameraPosition;
        Rect lastViewport;
        public Rect EffectiveScreenRect { get; private set; }
        public Rect ProjectedMonsterRect { get; private set; }
        public bool FitSucceeded { get; private set; }
        public string FitStatus { get; private set; } = "WAITING_FOR_HUD";
        public Camera BattleCamera => layout != null ? layout.BattleCamera : null;
        public float BaselineFieldOfView => baselineFieldOfView;
        public Vector3 BaselinePosition => baselinePosition;
        public Quaternion BaselineRotation => baselineRotation;
        public float ParticipantYaw => participantYaw;
        /// <summary>#28: while true, the last solved camera stays put unless the view or HUD area changes.</summary>
        public bool HoldFraming { get; set; }

        public Quaternion ParticipantRotation =>
            Quaternion.AngleAxis(participantYaw, Vector3.up) * baselineRotation;

        public void Configure(SplitScreenLayout source, T09Hud view, BenchmarkMonster target)
        {
            if (source == null || source.BattleCamera == null || view == null || target == null)
                throw new ArgumentNullException(nameof(source));
            layout = source; hud = view; monster = target;
            visualRenderers = target.Visual.GetComponentsInChildren<Renderer>(true);
            baselinePosition = source.BattleCamera.transform.position;
            baselineRotation = source.BattleCamera.transform.rotation;
            baselineFieldOfView = source.BattleCamera.fieldOfView;
            hasBaseline = true;
        }
        public void ConfigureParticipantView(int playerNumber, int participantCount)
        {
            participantYaw = ParticipantViewAngle.CalculateYaw(
                playerNumber,
                participantCount
            );

            ApplyFraming();
        }

        void LateUpdate() => ApplyFraming();
        void OnDisable() => RestoreBaseline();
        public void RestoreBaseline()
        {
            var camera = BattleCamera;
            if (hasBaseline && camera != null)
            {
                camera.transform.SetPositionAndRotation(baselinePosition, baselineRotation);
                camera.fieldOfView = baselineFieldOfView;
            }
            FitSucceeded = false;
        }
        public bool ApplyFraming()
        {
            bool previousFit = FitSucceeded;
            FitSucceeded = false;
            var camera = BattleCamera;
            Quaternion participantRotation = ParticipantRotation;
            if (!hasBaseline || camera == null || camera.orthographic || hud == null || hud.Canvas == null
                || !hud.Canvas.gameObject.activeInHierarchy || monster == null || monster.Visual == null)
            { FitStatus = "WAITING_FOR_HUD"; return false; }
            if (!TryEffectiveRect(camera.pixelRect, Screen.safeArea, out var available))
            { FitStatus = "HUD_LEAVES_NO_TARGET_AREA"; return false; }
            Bounds bounds = monster.Hitbox.bounds;
            if (visualRenderers == null) visualRenderers = monster.Visual.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in visualRenderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
            var viewport = camera.pixelRect;
            if (HoldFraming && previousFit && available == EffectiveScreenRect && viewport == lastViewport
                && camera.transform.position == lastCameraPosition && camera.transform.rotation == participantRotation
                && Mathf.Approximately(camera.fieldOfView, baselineFieldOfView))
            { FitSucceeded = true; return true; }
            if (previousFit && available == EffectiveScreenRect && bounds == lastBounds && viewport == lastViewport
                && camera.transform.position == lastCameraPosition && camera.transform.rotation == participantRotation
                && Mathf.Approximately(camera.fieldOfView, baselineFieldOfView))
            { FitSucceeded = true; return true; }
            var normalized = new Rect((available.xMin - viewport.xMin) / viewport.width,
                (available.yMin - viewport.yMin) / viewport.height, available.width / viewport.width, available.height / viewport.height);
            if (!TrySolvePosition(bounds, participantRotation, baselineFieldOfView, viewport.width / viewport.height,
                normalized, baselinePosition, out var position))
            { FitStatus = "TARGET_FIT_FAILED"; return false; }
            camera.fieldOfView = baselineFieldOfView;
            camera.transform.SetPositionAndRotation(position, participantRotation);
            EffectiveScreenRect = available;
            lastBounds = bounds; lastCameraPosition = position; lastViewport = viewport;
            ProjectedMonsterRect = ProjectBounds(camera, bounds);
            FitSucceeded = ProjectedMonsterRect.xMin >= available.xMin - .5f
                && ProjectedMonsterRect.xMax <= available.xMax + .5f
                && ProjectedMonsterRect.yMin >= available.yMin - .5f
                && ProjectedMonsterRect.yMax <= available.yMax + .5f;
            FitStatus = FitSucceeded ? "TARGET_CLEAR_OF_HUD" : "PROJECTION_OUTSIDE_TARGET_AREA";
            return FitSucceeded;
        }
        bool TryEffectiveRect(Rect viewport, Rect safe, out Rect area)
        {
            area = Rect.MinMaxRect(Mathf.Max(viewport.xMin, safe.xMin), Mathf.Max(viewport.yMin, safe.yMin),
                Mathf.Min(viewport.xMax, safe.xMax), Mathf.Min(viewport.yMax, safe.yMax));
            if (hud.TeamHpLabel == null || hud.ClockLabel == null || hud.StorageLabel == null
                || hud.ResourceModeLabel == null || hud.RoundLabel == null) return false;
            float top = Mathf.Min(ScreenRect(hud.TeamHpLabel.rectTransform).yMin,
                ScreenRect(hud.ClockLabel.rectTransform).yMin, ScreenRect(hud.StorageLabel.rectTransform).yMin);
            float bottom = Mathf.Max(ScreenRect(hud.ResourceModeLabel.rectTransform).yMax,
                ScreenRect(hud.RoundLabel.rectTransform).yMax);
            area.yMin = Mathf.Max(area.yMin, bottom);
            area.yMax = Mathf.Min(area.yMax, top);
            if (area.width <= 1f || area.height <= 1f) return false;
            float padding = Mathf.Min(area.width, area.height) * layout.Config.BattleFramingPaddingFraction;
            area = Rect.MinMaxRect(area.xMin + padding, area.yMin + padding, area.xMax - padding, area.yMax - padding);
            return area.width > 1f && area.height > 1f;
        }
        Rect ScreenRect(RectTransform transform)
        {
            transform.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        public static Rect ProjectBounds(Camera camera, Bounds bounds)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = camera.WorldToScreenPoint(Corner(bounds, i));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Each participant rotates around world Y while keeping the saved pitch and field of view.
        public static bool TrySolvePosition(Bounds bounds, Quaternion rotation, float verticalFov, float aspect,
            Rect allowedViewport, Vector3 baseline, out Vector3 position)
        {
            position = baseline;
            if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.sqrMagnitude <= 0
                || !Finite(baseline) || !Finite(verticalFov) || verticalFov <= 1f || verticalFov >= 170f
                || !Finite(aspect) || aspect <= 0 || !Finite(allowedViewport.xMin) || !Finite(allowedViewport.yMin)
                || !Finite(allowedViewport.xMax) || !Finite(allowedViewport.yMax)
                || allowedViewport.width <= 0 || allowedViewport.height <= 0
                || allowedViewport.xMin < 0 || allowedViewport.yMin < 0 || allowedViewport.xMax > 1 || allowedViewport.yMax > 1)
                return false;
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            if (!Finite(right) || !Finite(up) || !Finite(forward) || forward.sqrMagnitude < .5f) return false;
            var points = new Vector3[8];
            float distance = Mathf.Max(.5f, Vector3.Dot(bounds.center - baseline, forward));
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 delta = Corner(bounds, i) - bounds.center;
                points[i] = new Vector3(Vector3.Dot(delta, right), Vector3.Dot(delta, up), Vector3.Dot(delta, forward));
                distance = Mathf.Max(distance, .5f - points[i].z);
            }
            float tan = Mathf.Tan(verticalFov * Mathf.Deg2Rad * .5f);
            var slopes = new Vector4((allowedViewport.xMin * 2f - 1f) * tan * aspect,
                (allowedViewport.xMax * 2f - 1f) * tan * aspect,
                (allowedViewport.yMin * 2f - 1f) * tan, (allowedViewport.yMax * 2f - 1f) * tan);
            float minimum = distance;
            Vector2 offset;
            int attempts = 0;
            while (!TryOffset(points, slopes, distance, out offset))
            {
                distance *= 1.2f;
                if (++attempts >= 48 || !Finite(distance)) return false;
            }
            float upper = distance;
            for (int i = 0; i < 30; i++)
            {
                float middle = (minimum + upper) * .5f;
                if (TryOffset(points, slopes, middle, out _)) upper = middle; else minimum = middle;
            }
            // A small numerical margin prevents a near-exact solution flickering at a pixel edge.
            distance = upper * 1.001f;
            if (!TryOffset(points, slopes, distance, out offset)) return false;
            position = bounds.center - forward * distance + right * offset.x + up * offset.y;
            return Finite(position);
        }
        static bool TryOffset(Vector3[] points, Vector4 slope, float distance, out Vector2 offset)
        {
            float minX = float.NegativeInfinity, maxX = float.PositiveInfinity;
            float minY = float.NegativeInfinity, maxY = float.PositiveInfinity;
            foreach (var p in points)
            {
                float depth = distance + p.z;
                if (depth <= 0) { offset = default; return false; }
                minX = Mathf.Max(minX, p.x - slope.y * depth); maxX = Mathf.Min(maxX, p.x - slope.x * depth);
                minY = Mathf.Max(minY, p.y - slope.w * depth); maxY = Mathf.Min(maxY, p.y - slope.z * depth);
            }
            offset = new Vector2((minX + maxX) * .5f, (minY + maxY) * .5f);
            return minX <= maxX && minY <= maxY;
        }
        static Vector3 Corner(Bounds b, int i) => b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
