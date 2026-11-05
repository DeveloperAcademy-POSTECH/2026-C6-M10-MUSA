using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>The final-hit replay record and its three-pass timeline: pure data, no Scene, camera or network.</summary>
    public sealed class LabFinishReplayShotTests
    {
        private static LabFinishReplayShot Sample(int poses = 33)
        {
            var shot = new LabFinishReplayShot
            {
                Owner = 7, Slot = 3, Element = LabOrbElement.Water, FiveColor = true, JigwiReward = true,
                BlastScale = .5f, RadiusScale = 1.25f, JigwiRadiusScale = .8f, Radius = .12f, LaunchScale = 1.25f,
                FlightSeconds = .62f, Gravity = 30f, Damping = 0f,
                LaunchPosition = new Vector3(.3f, .85f, 14f), LaunchVelocity = new Vector3(-.4f, 7.5f, -6.4f),
                HitPoint = new Vector3(.05f, 1.6f, 10.6f), HitNormal = new Vector3(.1f, .2f, .97f),
                ImpactVelocity = new Vector3(-.4f, -3f, -6.4f), PoseStart = -LabFinishReplayShot.PoseBefore,
                Poses = Enumerable.Range(0, poses).Select(i => new LabHangingTarget.RigState
                {
                    Swing = new Vector2(i * .5f, -i * .25f), Yaw = 360f + i * 3f, RopeOffset = i * .001f, PivotHeight = 1.75f
                }).ToArray()
            };
            Assert.That(shot.IsValid(), Is.True);
            return shot;
        }

        [Test]
        public void TheRecordRoundTripsWithinThePacketBudget()
        {
            LabFinishReplayShot shot = Sample();
            string encoded = shot.Encode();
            Assert.That(encoded.Length, Is.LessThanOrEqualTo(LabFinishReplayShot.MaximumEncodedLength));
            Assert.That(LabFinishReplayShot.TryDecode(encoded, out LabFinishReplayShot copy), Is.True);
            Assert.That(copy.Owner, Is.EqualTo(7ul));
            Assert.That(copy.Slot, Is.EqualTo(3));
            Assert.That(copy.Element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(copy.FiveColor && copy.JigwiReward, Is.True);
            Assert.That(copy.BlastScale, Is.EqualTo(.5f));
            Assert.That(copy.RadiusScale, Is.EqualTo(1.25f));
            Assert.That(copy.JigwiRadiusScale, Is.EqualTo(.8f));
            Assert.That(copy.Radius, Is.EqualTo(.12f));
            Assert.That(copy.FlightSeconds, Is.EqualTo(.62f));
            Assert.That(copy.LaunchPosition, Is.EqualTo(shot.LaunchPosition));
            Assert.That(copy.LaunchVelocity, Is.EqualTo(shot.LaunchVelocity));
            Assert.That(copy.HitPoint, Is.EqualTo(shot.HitPoint));
            Assert.That(copy.HitNormal, Is.EqualTo(shot.HitNormal));
            Assert.That(copy.ImpactVelocity, Is.EqualTo(shot.ImpactVelocity));
            Assert.That(copy.PoseStart, Is.EqualTo(shot.PoseStart));
            Assert.That(copy.Poses.Length, Is.EqualTo(33));
            for (int i = 0; i < copy.Poses.Length; i++)
            {
                Assert.That(copy.Poses[i].Swing, Is.EqualTo(shot.Poses[i].Swing));
                Assert.That(copy.Poses[i].Yaw, Is.EqualTo(shot.Poses[i].Yaw));
                Assert.That(copy.Poses[i].RopeOffset, Is.EqualTo(shot.Poses[i].RopeOffset));
                Assert.That(copy.Poses[i].PivotHeight, Is.EqualTo(shot.Poses[i].PivotHeight));
            }
            Assert.That(copy.Encode(), Is.EqualTo(encoded));
        }

        [Test]
        public void TheLargestRecordStillFitsThePacketBudget()
        {
            Assert.That(Sample(LabFinishReplayShot.MaximumPoses).Encode().Length,
                Is.LessThanOrEqualTo(LabFinishReplayShot.MaximumEncodedLength));
        }

        [Test]
        public void MalformedRecordsAreRefusedNotRepaired()
        {
            string encoded = Sample().Encode();
            byte[] bytes = Convert.FromBase64String(encoded);
            Assert.That(LabFinishReplayShot.TryDecode(null, out _), Is.False);
            Assert.That(LabFinishReplayShot.TryDecode(string.Empty, out _), Is.False);
            Assert.That(LabFinishReplayShot.TryDecode("not base64!", out _), Is.False);
            Assert.That(LabFinishReplayShot.TryDecode(new string('A', LabFinishReplayShot.MaximumEncodedLength + 4), out _), Is.False);
            Assert.That(LabFinishReplayShot.TryDecode(Convert.ToBase64String(bytes.Take(bytes.Length - 3).ToArray()), out _), Is.False,
                "A truncated record");
            Assert.That(LabFinishReplayShot.TryDecode(Convert.ToBase64String(bytes.Concat(new byte[] { 0 }).ToArray()), out _), Is.False,
                "Trailing bytes");
            byte[] magic = (byte[])bytes.Clone(); magic[0] = (byte)'X';
            Assert.That(LabFinishReplayShot.TryDecode(Convert.ToBase64String(magic), out _), Is.False, "Another format");
            byte[] version = (byte[])bytes.Clone(); version[2] = 99;
            Assert.That(LabFinishReplayShot.TryDecode(Convert.ToBase64String(version), out _), Is.False, "Another version");
            byte[] element = (byte[])bytes.Clone(); element[12] = 42;
            Assert.That(LabFinishReplayShot.TryDecode(Convert.ToBase64String(element), out _), Is.False, "Unknown element");

            LabFinishReplayShot nan = Sample(); nan.FlightSeconds = float.NaN;
            Assert.That(LabFinishReplayShot.TryDecode(nan.Encode(), out _), Is.False);
            LabFinishReplayShot far = Sample(); far.HitPoint = Vector3.one * 1e5f;
            Assert.That(LabFinishReplayShot.TryDecode(far.Encode(), out _), Is.False);
            LabFinishReplayShot onePose = Sample(); onePose.Poses = onePose.Poses.Take(1).ToArray();
            Assert.That(LabFinishReplayShot.TryDecode(onePose.Encode(), out _), Is.False);
        }

        [Test]
        public void TheFlightStartsAtTheLaunchAndEndsAtTheOrbCentreOnContact()
        {
            LabFinishReplayShot shot = Sample();
            Assert.That(Vector3.Distance(shot.BallAt(-shot.FlightSeconds), shot.LaunchPosition), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(shot.BallAt(-5f), shot.LaunchPosition), Is.LessThan(1e-5f), "Before launch it waits there.");
            Assert.That(Vector3.Distance(shot.BallAt(0f), shot.ImpactCenter), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(shot.BallAt(1f), shot.ImpactCenter), Is.LessThan(1e-4f), "It never passes the contact.");
            Assert.That(Vector3.Dot(shot.OutwardNormal, shot.ImpactVelocity), Is.LessThan(0f), "The centre sits outside the surface.");
            Assert.That(Vector3.Distance(shot.ImpactCenter, shot.HitPoint), Is.EqualTo(shot.Radius).Within(1e-5f));
            Assert.That(shot.Launched(-shot.FlightSeconds), Is.True);
            Assert.That(shot.Launched(-shot.FlightSeconds - .01f), Is.False);
        }

        [Test]
        public void TheFlightFollowsTheProjectileStepUntilTheCorrection()
        {
            // Without step error the end correction is zero and the path is exactly the projectile's semi-implicit Euler.
            LabFinishReplayShot shot = Sample();
            Vector3 position = shot.LaunchPosition, velocity = shot.LaunchVelocity;
            var steps = new Vector3[32];
            steps[0] = position;
            for (int i = 0; i < 31; i++)
            {
                velocity += Vector3.down * (shot.Gravity * LabFinishReplayShot.StepSeconds);
                position += velocity * LabFinishReplayShot.StepSeconds;
                steps[i + 1] = position;
            }
            float elapsed = 31 * LabFinishReplayShot.StepSeconds;
            var exact = new LabFinishReplayShot
            {
                Element = LabOrbElement.Fire, Radius = .1f, FlightSeconds = elapsed, Gravity = shot.Gravity,
                LaunchPosition = shot.LaunchPosition, LaunchVelocity = shot.LaunchVelocity,
                HitPoint = position - Vector3.forward * .1f, HitNormal = -Vector3.forward, ImpactVelocity = velocity,
                PoseStart = -LabFinishReplayShot.PoseBefore, Poses = shot.Poses
            };
            Assert.That(Vector3.Distance(exact.ImpactCenter, position), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(exact.BallAt(0f), position), Is.LessThan(1e-4f));
            for (int i = 1; i < 31; i += 5)
                Assert.That(Vector3.Distance(exact.BallAt(-elapsed + i * LabFinishReplayShot.StepSeconds), steps[i]),
                    Is.LessThan(2e-3f), "Step " + i);
        }

        [Test]
        public void PosesInterpolateAndClampToTheRecordedWindow()
        {
            LabFinishReplayShot shot = Sample();
            float step = 1f / LabFinishReplayShot.PoseRate;
            LabHangingTarget.RigState first = shot.PoseAt(shot.PoseStart - 1f);
            Assert.That(first.Swing, Is.EqualTo(shot.Poses[0].Swing));
            LabHangingTarget.RigState last = shot.PoseAt(5f);
            Assert.That(last.Yaw, Is.EqualTo(shot.Poses[shot.Poses.Length - 1].Yaw));
            LabHangingTarget.RigState between = shot.PoseAt(shot.PoseStart + 2.5f * step);
            Assert.That(between.Yaw, Is.EqualTo((shot.Poses[2].Yaw + shot.Poses[3].Yaw) * .5f).Within(1e-4f));
            Assert.That(between.Swing.x, Is.EqualTo((shot.Poses[2].Swing.x + shot.Poses[3].Swing.x) * .5f).Within(1e-4f));
            // The recorded window covers every pass, including the closing transition.
            LabFinishReplayTimeline.Evaluate(LabFinishReplayTimeline.Duration, shot.FlightSeconds, out float lastTime, out _);
            Assert.That(shot.PoseStart, Is.LessThanOrEqualTo(LabFinishReplayTimeline.Passes[0].From));
            Assert.That(shot.PoseStart + (shot.Poses.Length - 1) * step, Is.GreaterThanOrEqualTo(lastTime - 1e-4f));
        }

        [Test]
        public void ThreePassesEachSlowerThenATransitionLastAboutFiveSeconds()
        {
            var passes = LabFinishReplayTimeline.Passes;
            Assert.That(passes.Length, Is.EqualTo(3));
            for (int i = 0; i < passes.Length; i++)
            {
                Assert.That(passes[i].From, Is.LessThan(0f), "Every pass shows the impact.");
                Assert.That(passes[i].To, Is.GreaterThan(0f));
                Assert.That(passes[i].RealSeconds, Is.InRange(1.3f - 1e-4f, 1.8f + 1e-4f));
                if (i > 0)
                {
                    Assert.That(passes[i].Speed, Is.LessThan(passes[i - 1].Speed), "Each pass is slower.");
                    Assert.That(passes[i].RealSeconds, Is.GreaterThan(passes[i - 1].RealSeconds));
                }
            }
            Assert.That(LabFinishReplayTimeline.Duration, Is.EqualTo(5.1125f).Within(1e-4f));
            Assert.That(LabFinishReplayTimeline.Evaluate(0d, 5f, out float t0, out _), Is.EqualTo(0));
            Assert.That(t0, Is.EqualTo(passes[0].From));
            Assert.That(LabFinishReplayTimeline.Evaluate(passes[0].RealSeconds + .001d, 5f, out float t1, out _), Is.EqualTo(1));
            Assert.That(t1, Is.EqualTo(passes[1].From).Within(1e-3f));
            double third = passes[0].RealSeconds + passes[1].RealSeconds + .001d;
            Assert.That(LabFinishReplayTimeline.Evaluate(third, 5f, out float t2, out _), Is.EqualTo(2));
            Assert.That(t2, Is.EqualTo(passes[2].From).Within(1e-3f));
            Assert.That(LabFinishReplayTimeline.Evaluate(99d, 5f, out float end, out float endElapsed), Is.EqualTo(2));
            Assert.That(endElapsed, Is.EqualTo(passes[2].RealSeconds + LabFinishReplayTimeline.TransitionSeconds).Within(1e-4f));
            Assert.That(end, Is.LessThanOrEqualTo(LabFinishReplayShot.PoseAfter));
        }

        [Test]
        public void AShortFlightStartsThePassJustBeforeTheLaunchWithoutChangingItsRealLength()
        {
            const float flight = .3f;
            LabFinishReplayTimeline.Pass first = LabFinishReplayTimeline.Passes[0], fitted = first.Fitted(flight);
            Assert.That(fitted.From, Is.EqualTo(-(flight + LabFinishReplayTimeline.LaunchLeadIn)).Within(1e-5f));
            Assert.That(fitted.RealSeconds, Is.EqualTo(first.RealSeconds).Within(1e-4f));
            Assert.That(fitted.Speed, Is.LessThan(first.Speed));
            Assert.That(LabFinishReplayTimeline.Evaluate(0d, flight, out float start, out _), Is.EqualTo(0));
            Assert.That(start, Is.EqualTo(fitted.From).Within(1e-5f));
            Assert.That(LabFinishReplayTimeline.Passes[1].Fitted(flight).From, Is.EqualTo(LabFinishReplayTimeline.Passes[1].From),
                "A window that already starts after the launch is unchanged.");
            Assert.That(first.Fitted(float.NaN).RealSeconds, Is.EqualTo(first.RealSeconds).Within(1e-4f));
        }
    }
}
