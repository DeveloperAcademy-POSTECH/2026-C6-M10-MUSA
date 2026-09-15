using System;
using UnityEngine;

namespace C6.Prototype.Presentation
{
    /// <summary>Splits the complete render surface. HUD safe-area padding is independent.</summary>
    [DisallowMultipleComponent]
    public sealed class SplitScreenLayout : MonoBehaviour
    {
        [SerializeField] private ScreenLayoutConfig config;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private Camera orbCamera;

        private ScreenLayoutConfig appliedConfig;
        private Camera appliedBattleCamera;
        private Camera appliedOrbCamera;
        private float appliedFraction = float.NaN;
        private int appliedWidth = -1;
        private int appliedHeight = -1;

        public ScreenLayoutConfig Config => config;
        public Camera BattleCamera => battleCamera;
        public Camera OrbCamera => orbCamera;
        public Rect TopViewport => battleCamera != null ? battleCamera.rect : Rect.zero;
        public Rect BottomViewport => orbCamera != null ? orbCamera.rect : Rect.zero;
        public Rect TopPixelRect => battleCamera != null ? battleCamera.pixelRect : Rect.zero;
        public Rect BottomPixelRect => orbCamera != null ? orbCamera.pixelRect : Rect.zero;
        public event Action Changed;

        public void Configure(ScreenLayoutConfig layoutConfig, Camera upperCamera, Camera lowerCamera)
        {
            if (layoutConfig == null) throw new ArgumentNullException(nameof(layoutConfig));
            if (upperCamera == null) throw new ArgumentNullException(nameof(upperCamera));
            if (lowerCamera == null) throw new ArgumentNullException(nameof(lowerCamera));
            if (upperCamera == lowerCamera)
                throw new ArgumentException("The upper and lower views require distinct cameras.");

            config = layoutConfig;
            battleCamera = upperCamera;
            orbCamera = lowerCamera;
            ApplyLayout();
        }

        private void OnEnable() => ApplyLayout();

        private void LateUpdate()
        {
            if (config == null || battleCamera == null || orbCamera == null) return;
            if (appliedConfig != config || appliedBattleCamera != battleCamera || appliedOrbCamera != orbCamera ||
                appliedWidth != Screen.width || appliedHeight != Screen.height ||
                !Mathf.Approximately(appliedFraction, config.UpperFraction) ||
                battleCamera.rect != GetTopViewport() || orbCamera.rect != GetBottomViewport())
                ApplyLayout();
        }

        public void ApplyLayout()
        {
            // AddComponent runs OnEnable before Editor setup can assign the saved references.
            if (config == null || battleCamera == null || orbCamera == null || battleCamera == orbCamera) return;

            battleCamera.rect = GetTopViewport();
            orbCamera.rect = GetBottomViewport();
            appliedConfig = config;
            appliedBattleCamera = battleCamera;
            appliedOrbCamera = orbCamera;
            appliedFraction = config.UpperFraction;
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;
            Changed?.Invoke();
        }

        public bool ContainsBottomScreenPoint(Vector2 screenPoint)
        {
            if (orbCamera == null || !IsFinite(screenPoint.x) || !IsFinite(screenPoint.y)) return false;
            // The actual camera region is authoritative, including resizing and the exclusive upper edge.
            return orbCamera.pixelRect.Contains(screenPoint);
        }

        public bool TryScreenToOrbPlane(Vector2 screenPoint, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!ContainsBottomScreenPoint(screenPoint)) return false;

            // The saved T04 lower view uses the world XY plane (z = 0); this does not handle gestures.
            var ray = orbCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return false;
            worldPoint = ray.GetPoint(distance);
            return true;
        }

        private Rect GetTopViewport() => new Rect(0f, config.LowerFraction, 1f, config.UpperFraction);
        private Rect GetBottomViewport() => new Rect(0f, 0f, 1f, config.LowerFraction);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
