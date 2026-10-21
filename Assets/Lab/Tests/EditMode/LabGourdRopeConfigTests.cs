using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// The rope/swing tail must preserve the previously shared viewer/throw profile and
    /// reject a bad late field before applying any earlier value. These are data tests,
    /// not evidence of a Host/Client connection or a physical projectile collision.
    /// </summary>
    public sealed class LabGourdRopeConfigTests
    {
        private LabConfig config;
        private float[] baseline;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[110] = 1f;
            values[111] = 1f;
            values[112] = -.75f;
            values[113] = .85f;
            values[114] = .25f;
            values[115] = -7.5f;
            values[116] = .6f;
            values[117] = 1.25f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> TuningFields()
        {
            yield return new TestCaseData(118, "GourdRopeCycleAmplitude", 0f, 1f);
            yield return new TestCaseData(119, "GourdRopeCyclePeriod", .5f, 10f);
            yield return new TestCaseData(120, "GourdRopeHitBounce", 0f, 2f);
            yield return new TestCaseData(121, "GourdRopeSpring", 0f, 100f);
            yield return new TestCaseData(122, "GourdRopeDamping", 0f, 20f);
            yield return new TestCaseData(123, "GourdRopeMaximumHitExtension", 0f, 3f);
            yield return new TestCaseData(124, "GourdSwingImpactStrength", 0f, 20f);
            yield return new TestCaseData(125, "GourdSwingMaximumAngle", 1f, 75f);
            yield return new TestCaseData(126, "GourdSwingReturnStrength", 0f, 100f);
            yield return new TestCaseData(127, "GourdSwingDamping", 0f, 20f);
        }

        [TestCaseSource(nameof(TuningFields))]
        public void EachMotionFieldAppliesBothEndpointsWithoutChangingTheExistingProfile(
            int index, string propertyName, float minimum, float maximum)
        {
            foreach (float endpoint in new[] { minimum, maximum })
            {
                float[] values = (float[])baseline.Clone();
                values[index] = endpoint;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var property = typeof(LabConfig).GetProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That((float)property.GetValue(config), Is.EqualTo(endpoint), propertyName);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values),
                    "Applying the motion tail must preserve every preceding shared value.");
                Assert.That(config.CompletedGourdViewerEnabled, Is.True);
                Assert.That(config.ThrowForm, Is.EqualTo(LabThrowForm.Straight));
                Assert.That(config.CaptureDeveloperValues().Take(118), Is.EqualTo(baseline.Take(118)));
            }
        }

        [TestCaseSource(nameof(TuningFields))]
        public void EachMotionFieldRejectsNonFiniteAndOutOfRangeValuesWithoutApplyingEarlierFields(
            int index, string propertyName, float minimum, float maximum)
        {
            foreach (float invalid in new[] { minimum - .25f, maximum + .25f,
                float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float[] values = (float[])baseline.Clone();
                values[0] += 1f;
                values[110] = 0f;
                values[111] = 0f;
                values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False,
                    propertyName + " accepted " + invalid);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline),
                    propertyName + " must not partially change HP, viewing permission, or throw form.");
            }
        }

        [Test]
        public void RopePhaseAndSpringStateSurviveJsonAndSnapshotFragmentRoundTrips()
        {
            var source = new LabSnapshot
            {
                sessionId = "rope-state-roundtrip", round = 3, phase = LabPhase.Playing,
                paused = true, targetRopeCycleElapsed = 2.4375f,
                targetRopeHitOffset = -.125f, targetRopeHitVelocity = -1.75f,
                orbs = Enumerable.Range(0, 40).Select(index => new LabOrbState
                {
                    id = "rope-state-orb-" + index, owner = 7, kind = LabOrbKind.Combined,
                    element = LabOrbElement.Water, x = .25f, y = .75f
                }).ToArray()
            };
            LabSnapshot direct = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(source));
            string[] fragments = LabSessionSettings.ChunkSnapshot(source);
            Assert.That(fragments.Length, Is.GreaterThan(1), "Exercise the actual fragmented snapshot path.");
            LabSnapshot fragmented = JsonUtility.FromJson<LabSnapshot>(string.Concat(fragments));
            foreach (LabSnapshot restored in new[] { direct, fragmented })
            {
                Assert.That(restored.targetRopeCycleElapsed, Is.EqualTo(source.targetRopeCycleElapsed));
                Assert.That(restored.targetRopeHitOffset, Is.EqualTo(source.targetRopeHitOffset));
                Assert.That(restored.targetRopeHitVelocity, Is.EqualTo(source.targetRopeHitVelocity));
                Assert.That(restored.paused, Is.True);
                Assert.That(restored.sessionId, Is.EqualTo(source.sessionId));
                Assert.That(restored.round, Is.EqualTo(source.round));
            }
        }
    }
}
