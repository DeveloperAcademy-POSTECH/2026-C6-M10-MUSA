using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// Paper clouds drift one way across the title and come back in from the other side (#467). By default they go left
    /// with the wind that blows the flag and the leaves; "반대 방향" sends one right. A cloud wraps the moment it has fully
    /// left the title width, so it starts entering the other edge at once. Positions are a function of time from the
    /// authored start (no per-frame accumulation), the clock runs with a paused game, and disabling restores the start.
    /// </summary>
    public sealed class TitleCloudDrift : MonoBehaviour
    {
        [Serializable] public sealed class Cloud
        {
            public RectTransform target;
            [InspectorName("초당 이동"), Min(0)] public float speed = 12;
            [InspectorName("반대 방향 (오른쪽으로)")] public bool reverse;
        }
        [InspectorName("구름이 지나가는 폭")] public float width = 402;
        public Cloud[] clouds;
        private Vector2[] origins;
        private float elapsed;
        private void OnEnable() { Capture(); elapsed = 0; Sample(0); }
        private void Update() { elapsed += Time.unscaledDeltaTime; Sample(elapsed); }
        private void Capture()
        {
            origins = new Vector2[clouds == null ? 0 : clouds.Length];
            for (int i = 0; i < origins.Length; i++)
                if (clouds[i] != null && clouds[i].target != null) origins[i] = clouds[i].target.anchoredPosition;
        }

        /// <summary>
        /// Left edge of a cloud (top-left pivot) after moving <paramref name="travelled"/> from <paramref name="start"/>.
        /// It stays in [-cloudWidth, areaWidth): fully gone past one edge is the same as just touching the other.
        /// </summary>
        public static float Wrap(float start, float travelled, float cloudWidth, float areaWidth)
        {
            float span = Mathf.Max(1f, areaWidth + cloudWidth);
            return -cloudWidth + Mathf.Repeat(start + cloudWidth + travelled, span);
        }

        public void Sample(float seconds)
        {
            if (clouds == null) return;
            if (origins == null || origins.Length != clouds.Length) Capture();
            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = clouds[i];
                if (cloud == null || cloud.target == null) continue;
                float travelled = seconds * Mathf.Max(0, cloud.speed) * (cloud.reverse ? 1 : -1);
                cloud.target.anchoredPosition = new Vector2(
                    Wrap(origins[i].x, travelled, cloud.target.rect.width, width), origins[i].y);
            }
        }
        private void OnDisable()
        {
            if (origins == null || clouds == null) return;
            for (int i = 0; i < Mathf.Min(origins.Length, clouds.Length); i++)
                if (clouds[i] != null && clouds[i].target != null) clouds[i].target.anchoredPosition = origins[i];
            origins = null;
        }
    }
}
