using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Host record of recent approved paint hits for the final-hit replay. It only observes launches and approved
    /// hits; nothing here changes the model, the projectile, the gourd or any packet the game already sends.
    /// </summary>
    public sealed partial class LabNetwork
    {
        private const int ReplayLaunchLimit = 64, ReplayHitLimit = 8;
        private const double ReplayLaunchSeconds = 6d, ReplayHitSeconds = 4d;

        private struct ReplayLaunch
        {
            public string SessionId;
            public uint Round;
            public double At;
            public ulong Owner;
            public int Slot;
            public LabOrbElement Element;
            public bool FiveColor, JigwiReward;
            public float BlastScale, RadiusScale, JigwiRadiusScale, Radius, LaunchScale, Gravity, Damping;
            public Vector3 Position, Velocity;
        }

        private struct ReplayHit
        {
            public ReplayLaunch Launch;
            public double At;
            public float FlightSeconds;
            public Vector3 Point, Normal, Velocity;
            /// <summary>The gourd's poses around this impact, kept once they are recorded so an older hit stays replayable.</summary>
            public LabHangingTarget.RigState[] Poses;
        }

        private readonly Dictionary<string, ReplayLaunch> replayLaunches = new Dictionary<string, ReplayLaunch>();
        private readonly List<ReplayHit> replayHits = new List<ReplayHit>();

        private void RememberReplayLaunch(LabOrbState orb, LabProjectile projectile, float launchScale)
        {
            if (orb == null || projectile == null || config == null || string.IsNullOrEmpty(orb.id)) return;
            double now = Time.timeAsDouble;
            if (replayLaunches.Count >= ReplayLaunchLimit)
            {
                var stale = new List<string>();
                foreach (var pair in replayLaunches) if (now - pair.Value.At > ReplayLaunchSeconds) stale.Add(pair.Key);
                foreach (string id in stale) replayLaunches.Remove(id);
                if (replayLaunches.Count >= ReplayLaunchLimit) return;
            }
            float blast = projectile.SizeScale, radius = projectile.RadiusScale, jigwi = projectile.JigwiABRadiusScale;
            replayLaunches[orb.id] = new ReplayLaunch
            {
                SessionId = sessionId, Round = model != null ? model.Round : 0u, At = now,
                Owner = orb.owner, Slot = playerSlots != null ? playerSlots.GetSlot(orb.owner) : 0,
                Element = orb.element, FiveColor = orb.fiveColor, JigwiReward = projectile.JigwiABRewardActive,
                BlastScale = blast, RadiusScale = radius, JigwiRadiusScale = jigwi,
                Radius = config.ThrowRadius * (jigwi < 1f ? Mathf.Min(blast * radius, jigwi) : blast * radius),
                LaunchScale = launchScale, Gravity = projectile.GravityAcceleration, Damping = config.ThrowAirDamping,
                Position = projectile.LaunchPosition, Velocity = projectile.InitialVelocity
            };
        }

        /// <summary>An approved hit that painted: the replay's candidates. Blocked, element-less and fire hits never enter.</summary>
        private void RememberReplayHit(LabProjectileResult result)
        {
            if (string.IsNullOrEmpty(result.ProjectileId) || !replayLaunches.TryGetValue(result.ProjectileId, out ReplayLaunch launch))
                return;
            replayLaunches.Remove(result.ProjectileId);
            if (replayHits.Count >= ReplayHitLimit) replayHits.RemoveAt(0);
            replayHits.Add(new ReplayHit
            {
                Launch = launch, At = Time.fixedTimeAsDouble, FlightSeconds = result.FlightSeconds,
                Point = result.HitPoint, Normal = result.HitNormal, Velocity = result.ImpactVelocity
            });
        }

        /// <summary>
        /// Keep each recent hit's poses once the gourd has recorded them. The live history holds about five seconds, so a
        /// gourd finished later by paint without an orb (the Jigwi common-talisman waterfall and its growth) would
        /// otherwise have no hit left to replay.
        /// </summary>
        private void KeepReplayHitPoses()
        {
            if (!IsHost || target == null || replayHits.TrueForAll(hit => hit.Poses != null)) return;
            var hanging = target.GetComponent<LabHangingTarget>();
            if (hanging == null || hanging.PoseHistoryCount < 2) return;
            for (int i = replayHits.Count - 1; i >= 0; i--)
            {
                ReplayHit hit = replayHits[i];
                if (hit.Poses != null) continue;
                // Its window already left the kept history (an earlier round, or poses never caught up): never replayable.
                if (hit.At - LabFinishReplayShot.PoseBefore < hanging.OldestPoseTime) { replayHits.RemoveAt(i); continue; }
                // Every sample must be recorded first, the last one too (it sits one step past +PoseAfter).
                if (hanging.LatestPoseTime < hit.At - LabFinishReplayShot.PoseBefore + (ReplayPoseCount - 1) / LabFinishReplayShot.PoseRate)
                    continue;
                hit.Poses = SampleReplayPoses(hanging, hit.At);
                replayHits[i] = hit;
            }
        }

        private static readonly int ReplayPoseCount = Mathf.Min(LabFinishReplayShot.MaximumPoses,
            Mathf.CeilToInt((LabFinishReplayShot.PoseBefore + LabFinishReplayShot.PoseAfter) * LabFinishReplayShot.PoseRate) + 1);

        private static LabHangingTarget.RigState[] SampleReplayPoses(LabHangingTarget hanging, double at)
        {
            var poses = new LabHangingTarget.RigState[ReplayPoseCount];
            for (int i = 0; i < poses.Length; i++)
                hanging.TrySamplePose(at - LabFinishReplayShot.PoseBefore + i / LabFinishReplayShot.PoseRate, out poses[i]);
            return poses;
        }

        /// <summary>Why the last <see cref="TryCaptureFinishReplay"/> had no record (for the fallback log), or null.</summary>
        public string FinishReplayMissReason { get; private set; }

        /// <summary>
        /// Host only, after Victory: the replay record of the final hit — the newest recent hit (last
        /// <see cref="ReplayHitSeconds"/>) by the player credited with the last paint (the result's "last hit"), else the newest
        /// recent hit. When the gourd was finished by paint that is not an orb, so no hit is recent, the round's newest kept
        /// hit stands in (the last painter's first). <paramref name="waiting"/> is true while the gourd's poses after that
        /// impact are still being recorded; pass <paramref name="force"/> to take what exists. False with a null record when
        /// there is no hit to replay.
        /// </summary>
        public bool TryCaptureFinishReplay(bool force, out string encoded, out bool waiting)
        {
            encoded = null;
            waiting = false;
            FinishReplayMissReason = null;
            if (!IsHost || model == null || target == null) { FinishReplayMissReason = "no_host_battle"; return false; }
            var hanging = target.GetComponent<LabHangingTarget>();
            if (hanging == null || hanging.PoseHistoryCount < 2) { FinishReplayMissReason = "no_pose_history"; return false; }
            KeepReplayHitPoses();
            double now = Time.fixedTimeAsDouble;
            ulong lastPainter = model.PaintSurface != null ? model.PaintSurface.LastPainter : 0ul;
            int chosen = -1;
            // Passes: recent by the last painter, recent by anyone, then (no recent hit) older kept hits the same way.
            for (int pass = 0; pass < 4 && chosen < 0; pass++)
                for (int i = replayHits.Count - 1; i >= 0; i--)
                {
                    ReplayHit hit = replayHits[i];
                    if (hit.Launch.SessionId != sessionId || hit.Launch.Round != model.Round) continue;
                    if (pass < 2 && now - hit.At > ReplayHitSeconds) continue;
                    if (pass % 2 == 0 && hit.Launch.Owner != lastPainter) continue;
                    // Its whole replay window must still be in the gourd's kept poses, or the swing would freeze.
                    if (hit.Poses == null && hit.At - LabFinishReplayShot.PoseBefore < hanging.OldestPoseTime) continue;
                    chosen = i;
                    break;
                }
            if (chosen < 0) { FinishReplayMissReason = "no_hit"; return false; }
            ReplayHit final = replayHits[chosen];
            if (final.Poses == null && !force && hanging.LatestPoseTime < final.At + LabFinishReplayShot.PoseAfter)
            {
                waiting = true;
                return false;
            }
            var poses = final.Poses ?? SampleReplayPoses(hanging, final.At);
            ReplayLaunch launch = final.Launch;
            var shot = new LabFinishReplayShot
            {
                Owner = launch.Owner, Slot = launch.Slot, Element = launch.Element, FiveColor = launch.FiveColor,
                JigwiReward = launch.JigwiReward, BlastScale = launch.BlastScale, RadiusScale = launch.RadiusScale,
                JigwiRadiusScale = launch.JigwiRadiusScale, Radius = launch.Radius, LaunchScale = launch.LaunchScale,
                FlightSeconds = Mathf.Max(LabFinishReplayShot.StepSeconds, final.FlightSeconds),
                LaunchPosition = launch.Position, LaunchVelocity = launch.Velocity,
                Gravity = launch.Gravity, Damping = launch.Damping,
                HitPoint = final.Point, HitNormal = final.Normal, ImpactVelocity = final.Velocity,
                PoseStart = -LabFinishReplayShot.PoseBefore, Poses = poses
            };
            if (!shot.IsValid()) { FinishReplayMissReason = "invalid_shot"; return false; }
            string record = shot.Encode();
            if (record.Length > LabFinishReplayShot.MaximumEncodedLength) { FinishReplayMissReason = "record_too_long"; return false; }
            encoded = record;
            return true;
        }
    }
}
