using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Bounded, allocation-free world-space history. Both age and arc length clip the oldest
    /// segment by interpolation, rather than extending a decorative tail behind the source.
    /// </summary>
    public sealed class LabFlightTrailPath
    {
        public const int Capacity = 512;
        private readonly Vector3[] positions = new Vector3[Capacity];
        private readonly float[] times = new float[Capacity];
        private int start, count;
        private float length;

        public int Count => count;
        public float Length => length;

        public Vector3 GetPosition(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return positions[Slot(index)];
        }

        public float GetTime(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return times[Slot(index)];
        }

        public void Clear()
        {
            start = count = 0;
            length = 0f;
        }

        /// <returns>True only when a new, later point was added.</returns>
        public bool Record(Vector3 position, float time)
        {
            if (!Finite(time) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return false;
            if (count == 0)
            {
                positions[start] = position;
                times[start] = time;
                count = 1;
                return true;
            }
            int previous = Slot(count - 1);
            // A paused frame or duplicate callback must not grow the path or emit more stamps.
            if (time <= times[previous] || (position - positions[previous]).sqrMagnitude < .00000001f)
                return false;
            if (count == Capacity) RemoveFirst();
            int next = Slot(count);
            positions[next] = position;
            times[next] = time;
            length += Vector3.Distance(positions[Slot(count - 1)], position);
            count++;
            return true;
        }

        public void Trim(float now, float lifetime, float maximumLength)
        {
            if (count == 0 || !Finite(now)) return;
            float oldestTime = now - Mathf.Max(.001f, lifetime);
            while (count > 1 && GetTime(1) <= oldestTime) RemoveFirst();
            if (count == 1)
            {
                if (GetTime(0) < oldestTime) Clear();
                return;
            }
            if (GetTime(0) < oldestTime)
            {
                float fraction = Mathf.InverseLerp(GetTime(0), GetTime(1), oldestTime);
                CropFirst(fraction, oldestTime);
            }
            float limit = Mathf.Max(0f, maximumLength);
            while (count > 1 && length > limit)
            {
                float segment = Vector3.Distance(GetPosition(0), GetPosition(1));
                float excess = length - limit;
                if (segment <= excess + .000001f)
                {
                    RemoveFirst();
                    continue;
                }
                float fraction = excess / segment;
                CropFirst(fraction, Mathf.Lerp(GetTime(0), GetTime(1), fraction));
                break;
            }
        }

        private void CropFirst(float fraction, float time)
        {
            Vector3 previous = positions[start];
            positions[start] = Vector3.Lerp(previous, positions[Slot(1)], Mathf.Clamp01(fraction));
            times[start] = time;
            length = Mathf.Max(0f, length - Vector3.Distance(previous, positions[start]));
        }

        private void RemoveFirst()
        {
            if (count > 1)
                length = Mathf.Max(0f, length - Vector3.Distance(positions[start], positions[Slot(1)]));
            start = (start + 1) % Capacity;
            count--;
            if (count < 2) length = 0f;
        }

        private int Slot(int index) => (start + index) % Capacity;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
