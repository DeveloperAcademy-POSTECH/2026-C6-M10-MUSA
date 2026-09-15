using System;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>
    /// Direct pointer displacement over its recent sampling interval. Both axes are
    /// divided by the full display width (not board width, display height, or DPI).
    /// This is untrusted input; the Host validates it before calculating a launch.
    /// </summary>
    public readonly struct OrbThrowInput : IEquatable<OrbThrowInput>
    {
        public Vector2 Delta { get; }
        public float Duration { get; }

        public OrbThrowInput(Vector2 delta, float duration)
        {
            Delta = delta;
            Duration = duration;
        }

        // Receipt identity is separate from launch validity. float.Equals keeps a
        // rejected NaN sample equal to the same sample on duplicate/query requests;
        // Unity Vector2.Equals does not. ThrowMapping still rejects every NaN.
        public bool Equals(OrbThrowInput other) => Delta.x.Equals(other.Delta.x) &&
            Delta.y.Equals(other.Delta.y) && Duration.Equals(other.Duration);
        public override bool Equals(object obj) => obj is OrbThrowInput other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Delta.x.GetHashCode();
                hash = hash * 397 ^ Delta.y.GetHashCode();
                return hash * 397 ^ Duration.GetHashCode();
            }
        }
    }
}
