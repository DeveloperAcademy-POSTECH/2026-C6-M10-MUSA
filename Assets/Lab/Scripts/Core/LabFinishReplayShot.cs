using System;
using System.IO;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// The approved final hit as the Host saw it, for the presentation-only replay that plays before a story result.
    /// Times are relative to the impact (0). It carries the launch, the impact and the gourd's fixed poses around it,
    /// so every device redraws the same flight and swing. It never feeds hits, paint, stats or any network state.
    /// </summary>
    public sealed class LabFinishReplayShot
    {
        public const byte FormatVersion = 1;
        public const float PoseRate = 25f;
        /// <summary>Poses kept before and after the impact. They cover every replay pass, including the transition.</summary>
        public const float PoseBefore = .8f, PoseAfter = .5f;
        public const int MaximumPoses = 40;
        public const int MaximumEncodedLength = 1400;
        /// <summary>The projectile's fixed step (ProjectSettings TimeManager), used to redraw its flight.</summary>
        public const float StepSeconds = .02f;
        private static readonly byte[] Magic = { (byte)'F', (byte)'R' };

        public ulong Owner;
        /// <summary>The thrower's P number (1-based), or 0 when unknown.</summary>
        public int Slot;
        public LabOrbElement Element;
        public bool FiveColor, JigwiReward;
        /// <summary>#713: the five-colour shot was the Jigwi black orb (soot tones, black aura). Old builds read it as five-colour.</summary>
        public bool Soot;
        /// <summary>LabProjectile.Create's blast size, its radius scale, the independent B shrink and the final radius.</summary>
        public float BlastScale = 1f, RadiusScale = 1f, JigwiRadiusScale = 1f, Radius = .165f;
        /// <summary>The model's launch scale (SMALL/BIG sound), before any B reward size.</summary>
        public float LaunchScale = 1f;
        public float FlightSeconds;
        public Vector3 LaunchPosition, LaunchVelocity;
        public float Gravity, Damping;
        public Vector3 HitPoint, HitNormal, ImpactVelocity;
        /// <summary>Time of <see cref="Poses"/>[0] relative to the impact, then one pose every 1/<see cref="PoseRate"/> s.</summary>
        public float PoseStart;
        public LabHangingTarget.RigState[] Poses = Array.Empty<LabHangingTarget.RigState>();
        private Vector3[] path;

        /// <summary>Unit surface normal pointing out of the gourd toward the incoming orb.</summary>
        public Vector3 OutwardNormal
        {
            get
            {
                Vector3 incoming = ImpactVelocity.sqrMagnitude > 1e-8f ? ImpactVelocity.normalized : Vector3.forward;
                Vector3 normal = HitNormal.sqrMagnitude > 1e-8f ? HitNormal.normalized : -incoming;
                return Vector3.Dot(normal, incoming) > 0f ? -normal : normal;
            }
        }

        /// <summary>The orb's centre at the moment of contact.</summary>
        public Vector3 ImpactCenter => HitPoint + OutwardNormal * Radius;

        /// <summary>The fixed pose at replay time <paramref name="time"/>, clamped to the recorded window.</summary>
        public LabHangingTarget.RigState PoseAt(float time)
        {
            if (Poses == null || Poses.Length == 0) return default;
            float index = Mathf.Clamp((time - PoseStart) * PoseRate, 0f, Poses.Length - 1);
            int low = Mathf.FloorToInt(index), high = Mathf.Min(low + 1, Poses.Length - 1);
            return LabHangingTarget.RigState.Lerp(Poses[low], Poses[high], index - low);
        }

        /// <summary>
        /// Where the orb's centre was at replay time <paramref name="time"/> (−FlightSeconds = launch, 0 = impact), using the
        /// projectile's own step: gravity as acceleration, then linear damping. The remaining step error is spread so the
        /// path ends exactly at <see cref="ImpactCenter"/>.
        /// </summary>
        public Vector3 BallAt(float time)
        {
            EnsurePath();
            float elapsed = Mathf.Clamp(time + FlightSeconds, 0f, FlightSeconds);
            Vector3 raw = RawAt(elapsed);
            Vector3 error = ImpactCenter - RawAt(FlightSeconds);
            float share = FlightSeconds > 0f ? elapsed / FlightSeconds : 1f;
            return raw + error * (share * share * (3f - 2f * share));
        }

        public bool Launched(float time) => time >= -FlightSeconds;

        private Vector3 RawAt(float elapsed)
        {
            float index = Mathf.Clamp(elapsed / StepSeconds, 0f, path.Length - 1);
            int low = Mathf.FloorToInt(index), high = Mathf.Min(low + 1, path.Length - 1);
            return Vector3.Lerp(path[low], path[high], index - low);
        }

        private void EnsurePath()
        {
            if (path != null) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(FlightSeconds / StepSeconds));
            path = new Vector3[steps + 2];
            Vector3 position = LaunchPosition, velocity = LaunchVelocity;
            path[0] = position;
            for (int i = 1; i < path.Length; i++)
            {
                velocity += Vector3.down * (Gravity * StepSeconds);
                if (Damping > 0f) velocity *= 1f / (1f + StepSeconds * Damping);
                position += velocity * StepSeconds;
                path[i] = position;
            }
        }

        public string Encode()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write(FormatVersion);
                writer.Write(Owner);
                writer.Write((byte)Mathf.Clamp(Slot, 0, 255));
                writer.Write((byte)Element);
                writer.Write((byte)((FiveColor ? 1 : 0) | (JigwiReward ? 2 : 0) | (Soot ? 4 : 0)));
                foreach (float value in new[] { BlastScale, RadiusScale, JigwiRadiusScale, Radius, LaunchScale,
                    FlightSeconds, Gravity, Damping, PoseStart }) writer.Write(value);
                foreach (Vector3 value in new[] { LaunchPosition, LaunchVelocity, HitPoint, HitNormal, ImpactVelocity })
                { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
                int count = Poses != null ? Mathf.Min(Poses.Length, MaximumPoses) : 0;
                writer.Write((byte)count);
                for (int i = 0; i < count; i++)
                {
                    writer.Write(Poses[i].Swing.x); writer.Write(Poses[i].Swing.y); writer.Write(Poses[i].Yaw);
                    writer.Write(Poses[i].RopeOffset); writer.Write(Poses[i].PivotHeight);
                }
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>Reads a Host record. Anything malformed, out of range or non-finite is refused, never repaired.</summary>
        public static bool TryDecode(string encoded, out LabFinishReplayShot shot)
        {
            shot = null;
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedLength) return false;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { return false; }
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (reader.ReadByte() != Magic[0] || reader.ReadByte() != Magic[1] || reader.ReadByte() != FormatVersion) return false;
                    var value = new LabFinishReplayShot { Owner = reader.ReadUInt64(), Slot = reader.ReadByte() };
                    byte element = reader.ReadByte();
                    if (!Enum.IsDefined(typeof(LabOrbElement), (int)element)) return false;
                    value.Element = (LabOrbElement)element;
                    byte flags = reader.ReadByte();
                    value.FiveColor = (flags & 1) != 0; value.JigwiReward = (flags & 2) != 0; value.Soot = value.FiveColor && (flags & 4) != 0;
                    value.BlastScale = reader.ReadSingle(); value.RadiusScale = reader.ReadSingle();
                    value.JigwiRadiusScale = reader.ReadSingle(); value.Radius = reader.ReadSingle();
                    value.LaunchScale = reader.ReadSingle(); value.FlightSeconds = reader.ReadSingle();
                    value.Gravity = reader.ReadSingle(); value.Damping = reader.ReadSingle(); value.PoseStart = reader.ReadSingle();
                    value.LaunchPosition = ReadVector(reader); value.LaunchVelocity = ReadVector(reader);
                    value.HitPoint = ReadVector(reader); value.HitNormal = ReadVector(reader); value.ImpactVelocity = ReadVector(reader);
                    int count = reader.ReadByte();
                    if (count < 2 || count > MaximumPoses) return false;
                    value.Poses = new LabHangingTarget.RigState[count];
                    for (int i = 0; i < count; i++)
                        value.Poses[i] = new LabHangingTarget.RigState
                        {
                            Swing = new Vector2(reader.ReadSingle(), reader.ReadSingle()), Yaw = reader.ReadSingle(),
                            RopeOffset = reader.ReadSingle(), PivotHeight = reader.ReadSingle()
                        };
                    if (reader.BaseStream.Position != bytes.Length || !value.IsValid()) return false;
                    shot = value;
                    return true;
                }
            }
            catch (EndOfStreamException) { return false; }
        }

        public bool IsValid()
        {
            if (!InRange(BlastScale, .01f, 5f) || !InRange(RadiusScale, .01f, 5f) || !InRange(JigwiRadiusScale, .01f, 5f)
                || !InRange(Radius, .001f, 3f) || !InRange(LaunchScale, 0f, 5f) || !InRange(FlightSeconds, .001f, 5f)
                || !InRange(Gravity, 0f, 200f) || !InRange(Damping, 0f, 10f) || !InRange(PoseStart, -3f, 0f)) return false;
            if (!Bounded(LaunchPosition, 500f) || !Bounded(LaunchVelocity, 200f) || !Bounded(HitPoint, 500f)
                || !Bounded(HitNormal, 10f) || !Bounded(ImpactVelocity, 200f)) return false;
            if (Poses == null || Poses.Length < 2 || Poses.Length > MaximumPoses) return false;
            foreach (LabHangingTarget.RigState pose in Poses)
                if (!InRange(pose.Swing.x, -360f, 360f) || !InRange(pose.Swing.y, -360f, 360f)
                    || !InRange(pose.Yaw, -1e6f, 1e6f) || !InRange(pose.RopeOffset, -50f, 50f)
                    || !InRange(pose.PivotHeight, -50f, 50f)) return false;
            return true;
        }

        private static Vector3 ReadVector(BinaryReader reader) =>
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static bool InRange(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        private static bool Bounded(Vector3 value, float limit) =>
            InRange(value.x, -limit, limit) && InRange(value.y, -limit, limit) && InRange(value.z, -limit, limit);
    }

    /// <summary>
    /// The replay's three passes. Each plays a window of replay time (0 = impact) at its own speed, slower each time;
    /// the third keeps flowing through the closing transition. Every device evaluates the same shared elapsed time.
    /// </summary>
    public static class LabFinishReplayTimeline
    {
        public readonly struct Pass
        {
            public readonly float From, To, Speed;
            public Pass(float from, float to, float speed) { From = from; To = to; Speed = speed; }
            public float RealSeconds => (To - From) / Speed;

            /// <summary>
            /// This pass for a flight of <paramref name="flightSeconds"/>: a window that would open long before the launch
            /// starts just before it instead, played slower so the pass keeps its real length (and every device its timing).
            /// </summary>
            public Pass Fitted(float flightSeconds)
            {
                float flight = float.IsNaN(flightSeconds) || flightSeconds <= 0f ? 0f : flightSeconds;
                float from = Mathf.Min(To - .05f, Mathf.Max(From, -(flight + LaunchLeadIn)));
                return new Pass(from, To, (To - from) / RealSeconds);
            }
        }

        /// <summary>How much of the empty sky before the launch a pass keeps.</summary>
        public const float LaunchLeadIn = .12f;

        public static readonly Pass[] Passes =
        {
            new Pass(-.75f, .30f, .8f),   // the thrower's view, following the orb in
            new Pass(-.30f, .30f, .4f),   // from the side, travelling with the orb
            new Pass(-.12f, .33f, .25f)   // close on the impact, slowly circling in
        };
        public const float TransitionSeconds = .5f;
        public const float CutFlashSeconds = .1f;

        public static float Duration
        {
            get
            {
                float total = TransitionSeconds;
                foreach (Pass pass in Passes) total += pass.RealSeconds;
                return total;
            }
        }

        /// <summary>Real seconds from the start to the start of <paramref name="pass"/>.</summary>
        public static float PassStart(int pass)
        {
            float total = 0f;
            for (int i = 0; i < pass && i < Passes.Length; i++) total += Passes[i].RealSeconds;
            return total;
        }

        /// <summary>
        /// Which pass shows at <paramref name="elapsed"/>, its replay time and how long it has run, for a flight of
        /// <paramref name="flightSeconds"/> (see <see cref="Pass.Fitted"/>). Real timings never depend on the flight.
        /// </summary>
        public static int Evaluate(double elapsed, float flightSeconds, out float replayTime, out float passElapsed)
        {
            float time = Mathf.Clamp((float)elapsed, 0f, Duration);
            int last = Passes.Length - 1;
            int index = last;
            for (int i = 0; i < last; i++)
                if (time < PassStart(i) + Passes[i].RealSeconds) { index = i; break; }
            Pass pass = Passes[index].Fitted(flightSeconds);
            passElapsed = time - PassStart(index);
            replayTime = pass.From + pass.Speed * passElapsed;
            return index;
        }
    }
}
