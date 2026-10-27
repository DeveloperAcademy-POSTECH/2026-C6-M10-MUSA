using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Fever time (#334): with the set seconds left in a fox's battle the yokai stops, the gimmicks still going end, and
    /// stamina refills faster by itself. Other stages and the 2:2 battle have none.
    /// </summary>
    public sealed class LabFeverTimeTests
    {
        private LabConfig config;

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[2] = 120f;                  // the two-minute battle (#328)
            values[3] = values[4] = 120f;      // a full six-orb bar (#328)
            values[5] = 20f;                   // one orb's stamina
            values[6] = 20f; values[7] = 1f;   // one orb's worth a second
            values[74] = LabModel.MaximumParticipants; // a drum for everyone
            values[205] = values[206] = 0f;    // no gimmicks unless a test turns them on
            Apply(values);
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private void Apply(float[] values) =>
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);

        [Test]
        public void FeverTimeAppendsTwoDevRowsInItsOwnGroup()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(230));
            Assert.That(LabConfig.DeveloperFields.Skip(228).Select(field => field.Label), Is.EqualTo(new[]
            {
                "피버 타임 시작 (남은 초, 0 = 끔)", "피버 타임 충전 배율"
            }));
            Assert.That(LabConfig.DeveloperFields.Skip(228).Select(field => field.Min), Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(LabConfig.DeveloperFields.Skip(228).Select(field => field.Max), Is.EqualTo(new[] { 60f, 5f }));
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "피버 타임 (Hapio 구미호 전용)").FieldIndices,
                Is.EqualTo(new[] { 228, 229 }));
            LabConfig saved = Resources.Load<LabConfig>("LabConfig");
            Assert.That(saved.CaptureDeveloperValues().Skip(228), Is.EqualTo(new[] { 20f, 2f }), "Saved: the last 20 s, twice the refill.");
            float[] values = config.CaptureDeveloperValues();
            values[228] = 25f; values[229] = 3f;
            Apply(values);
            Assert.That(config.FeverTimeSeconds, Is.EqualTo(25f));
            Assert.That(config.FeverTimeRecoveryMultiplier, Is.EqualTo(3f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
        }
    }
}
