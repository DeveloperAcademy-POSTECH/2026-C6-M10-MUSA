using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Samples only explicit pointer input. It never decides combine, transfer, or attack.
    /// Release consumes the gesture, including a rejected/too-short gesture; cancellation
    /// and focus/geometry changes must call Clear. Time uses one monotonic clock.
    /// </summary>
    public sealed class ThrowGestureSampler
    {
        private readonly struct Sample
        {
            public readonly Vector2 Position;
            public readonly double Time;
            public Sample(Vector2 position, double time) { Position = position; Time = time; }
        }

        private const int MaxSamples = 256;
        private readonly List<Sample> samples = new List<Sample>();
        public bool Active => samples.Count > 0;

        public bool Begin(Vector2 normalizedPosition, double now)
        {
            Clear();
            if (!Valid(normalizedPosition, now)) return false;
            samples.Add(new Sample(normalizedPosition, now));
            return true;
        }

        public bool Add(Vector2 normalizedPosition, double now)
        {
            if (!Active) return false;
            if (!Valid(normalizedPosition, now) || now < samples[samples.Count - 1].Time)
            { Clear(); return false; }
            var sample = new Sample(normalizedPosition, now);
            if (now == samples[samples.Count - 1].Time) samples[samples.Count - 1] = sample;
            else samples.Add(sample);
            if (samples.Count > MaxSamples) samples.RemoveAt(0);
            return true;
        }

        public bool TryRelease(Vector2 normalizedPosition, double now, float sampleWindow,
            float minDuration, out OrbThrowInput input)
        {
            input = default;
            try
            {
                if (!Finite(sampleWindow) || !Finite(minDuration) || sampleWindow <= 0f ||
                    minDuration <= 0f || minDuration > sampleWindow || !Add(normalizedPosition, now)) return false;
                double startTime = Math.Max(samples[0].Time, now - sampleWindow);
                double duration = now - startTime;
                if (duration + 0.0000001 < minDuration || samples.Count < 2) return false;
                Vector2 startPosition = samples[0].Position;
                // Interpolate the cutoff rather than quantizing it to render-frame times.
                for (int i = 1; i < samples.Count; i++)
                {
                    if (samples[i].Time < startTime) continue;
                    var before = samples[i - 1];
                    var after = samples[i];
                    float fraction = (float)((startTime - before.Time) / (after.Time - before.Time));
                    startPosition = Vector2.LerpUnclamped(before.Position, after.Position, fraction);
                    break;
                }
                Vector2 delta = normalizedPosition - startPosition;
                if (!Finite(delta.x) || !Finite(delta.y)) return false;
                input = new OrbThrowInput(delta, (float)duration);
                return true;
            }
            finally { Clear(); }
        }

        public void Clear() => samples.Clear();
        private static bool Valid(Vector2 position, double now) => Finite(position.x) && Finite(position.y) &&
            !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
