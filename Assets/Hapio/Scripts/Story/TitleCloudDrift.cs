using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>Slow, bounded paper-cloud motion around editable authored positions.</summary>
    public sealed class TitleCloudDrift : MonoBehaviour
    {
        [Serializable] public sealed class Cloud
        {
            public RectTransform target;
            [InspectorName("좌우 이동 폭"), Min(0)] public float distance = 20;
            [InspectorName("왕복 시간(초)"), Min(1)] public float seconds = 36;
            [InspectorName("반대 방향")] public bool reverse;
        }
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
        public void Sample(float seconds)
        {
            if (clouds == null) return;
            if (origins == null || origins.Length != clouds.Length) Capture();
            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = clouds[i];
                if (cloud == null || cloud.target == null) continue;
                float offset = Mathf.Sin(seconds * 2 * Mathf.PI / Mathf.Max(1, cloud.seconds)) *
                    Mathf.Max(0, cloud.distance) * (cloud.reverse ? -1 : 1);
                cloud.target.anchoredPosition = origins[i] + new Vector2(offset, 0);
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
