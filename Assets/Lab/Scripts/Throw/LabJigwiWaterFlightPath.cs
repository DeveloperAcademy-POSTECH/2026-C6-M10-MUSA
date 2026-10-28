using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Bounded, presentation-only history of a real B Water projectile, shared with its Canvas trace.</summary>
    [DefaultExecutionOrder(40)]
    public sealed class LabJigwiWaterFlightPath : MonoBehaviour
    {
        private const int MaximumActivePaths = 64;
        private static readonly List<LabJigwiWaterFlightPath> live = new List<LabJigwiWaterFlightPath>(MaximumActivePaths);
        private readonly LabFlightTrailPath path = new LabFlightTrailPath();
        private Transform source;
        private bool emitting, fading;
        private float fadeDeadline;
        public ulong Owner { get; private set; }
        public string FireEventId { get; private set; }
        public float Lifetime { get; private set; } = .45f;
        public float Width { get; private set; } = .4f;
        public float MaximumLength { get; private set; } = 2.1f;
        public bool IsEmitting => emitting;
        public int Count => path.Count;
        public Vector3 GetPosition(int index) => path.GetPosition(index);
        public float GetTime(int index) => path.GetTime(index);
        internal static IReadOnlyList<LabJigwiWaterFlightPath> Live => live;
        public static int ActivePathCount => live.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => live.Clear();

        public void Begin(Transform movingSource, ulong owner, string fireEventId, LabElementTrailProfile profile)
        {
            Clear();
            if (movingSource == null || movingSource == transform || movingSource.IsChildOf(transform)
                || string.IsNullOrEmpty(fireEventId)) return;
            source = movingSource; Owner = owner; FireEventId = fireEventId;
            Lifetime = profile != null ? profile.Lifetime : .45f;
            Width = profile != null ? profile.Width : .4f;
            MaximumLength = profile != null ? profile.MaxLength : 2.1f;
            transform.SetParent(source, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            path.Record(source.position, Time.time);
            emitting = true;
            // Exceptional projectile floods discard old visual histories; no gameplay object is touched.
            if (live.Count >= MaximumActivePaths)
            {
                var oldest = live[0]; bool detached = oldest.fading;
                oldest.Clear();
                if (detached) Destroy(oldest.gameObject);
            }
            live.Add(this);
        }

        public void StopAndFade(Transform parent)
        {
            if (!emitting) return;
            Record(); emitting = false; source = null; fading = true;
            fadeDeadline = Time.time + Lifetime;
            transform.SetParent(parent, true);
        }

        public void Clear()
        {
            live.Remove(this);
            source = null; emitting = fading = false; path.Clear();
        }

        private void Record()
        {
            if (source != null) path.Record(source.position, Time.time);
            path.Trim(Time.time, Lifetime, MaximumLength);
        }

        private void LateUpdate()
        {
            if (emitting)
            {
                if (source == null) { Clear(); return; }
                Record();
            }
            else if (fading)
            {
                path.Trim(Time.time, Lifetime, MaximumLength);
                if (Time.time >= fadeDeadline) { Clear(); Destroy(gameObject); }
            }
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => live.Remove(this);
    }
}
