using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>New stage-only values append after the retained 228-value contract.</summary>
    public sealed class LabJigwiStageConfigTests
    {
        private LabConfig config;
        [Serializable] private sealed class Settings { public float[] values; }
        [SetUp] public void Create() => config = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        [Test]
        public void DefaultsAppendEightFieldsWithoutChangingRawOrRetiredPositions()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(240));
            Assert.That(LabConfig.JigwiStageFirstField, Is.EqualTo(228));
            Assert.That(LabConfig.JigwiStageLastField, Is.EqualTo(235));
            Assert.That(config.CaptureDeveloperValues().Skip(228).Take(8), Is.EqualTo(new[] { 1f, 1f, 7f, 18f, 30f, 1f, 3f, 2f }));
            Assert.That(config.CaptureDeveloperValues().Skip(225).Take(3), Is.EqualTo(new[] { 1f, 75f, 3f }));
            Assert.That(config.CaptureDeveloperValues()[79], Is.Zero);
            Assert.That(LabConfig.DeveloperFields[228].IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperFields[234].WholeNumber, Is.True);
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "지귀 120초 (Hapio 전용)")
                .FieldIndices, Is.EqualTo(Enumerable.Range(228, 8)));
        }

        [Test]
        public void AcceptedFormRoundTripsAllEightValuesAndDoesNotMutateSourceOrEarlierFields()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] requested = (float[])before.Clone();
            float[] tail = { 0f, 2f, 12f, 25f, 40f, 2f, 2f, 3f };
            Array.Copy(tail, 0, requested, 228, tail.Length);
            Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.True, error);
            var json = JsonUtility.ToJson(new Settings { values = config.CaptureDeveloperValues() });
            var received = JsonUtility.FromJson<Settings>(json).values;
            var peer = ScriptableObject.CreateInstance<LabConfig>();
            LabConfig runtime = Object.Instantiate(config);
            try
            {
                Assert.That(peer.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(peer.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(runtime.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(peer.CaptureDeveloperValues().Take(228), Is.EqualTo(before.Take(228)));
                Assert.That(LabSessionSettings.Fingerprint(received, 3, 5674),
                    Is.EqualTo(LabSessionSettings.Fingerprint(requested, 3, 5674)));
                float[] runtimeValues = runtime.CaptureDeveloperValues(); runtimeValues[230] = 15f;
                Assert.That(runtime.TryApplyDeveloperValues(runtimeValues, out error), Is.True, error);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(requested));
            }
            finally { Object.DestroyImmediate(peer); Object.DestroyImmediate(runtime); }
        }

        [TestCase(228, .5f)] [TestCase(228, 2f)] [TestCase(228, float.NaN)]
        [TestCase(229, -1f)] [TestCase(229, 3.1f)]
        [TestCase(230, .9f)] [TestCase(230, 20.1f)]
        [TestCase(231, 4.9f)] [TestCase(231, 30.1f)]
        [TestCase(232, 9.9f)] [TestCase(232, 45.1f)]
        [TestCase(233, .09f)] [TestCase(233, 3.1f)]
        [TestCase(234, 0f)] [TestCase(234, 1.5f)] [TestCase(234, 4f)]
        [TestCase(235, .9f)] [TestCase(235, 4.1f)] [TestCase(235, float.PositiveInfinity)]
        public void BadTailRejectsTheWholeFormBeforeAnyLeadingValueChanges(int index, float value)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] requested = (float[])before.Clone(); requested[0] = 777f; requested[index] = value;
            Assert.That(config.TryApplyDeveloperValues(requested, out _), Is.False);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
        }

        [TestCase(228, 0f)] [TestCase(229, 2f)] [TestCase(230, 10f)] [TestCase(231, 20f)]
        [TestCase(232, 35f)] [TestCase(233, 2f)] [TestCase(234, 2f)] [TestCase(235, 3f)]
        public void EachFieldChangesTheSharedFingerprintAndRoundTripsSliderEndpoints(int index, float changed)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] requested = (float[])before.Clone(); requested[index] = changed;
            Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.True, error);
            Assert.That(LabSessionSettings.Fingerprint(requested, 3, 5674),
                Is.Not.EqualTo(LabSessionSettings.Fingerprint(before, 3, 5674)));
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            foreach (float endpoint in new[] { field.Min, field.Max, before[index] })
            {
                float normalized = LabDeveloperSliderScale.Normalize(field, before[index], endpoint);
                Assert.That(LabDeveloperSliderScale.Denormalize(field, before[index], normalized),
                    Is.EqualTo(endpoint).Within(.0001f));
            }
        }

        [Test]
        public void Old228PayloadIsRejectedAtomicallyRatherThanSilentlyUsingLocalDefaults()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] old = before.Take(228).ToArray(); old[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(old, out _), Is.False);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(old, 3, 5674));
        }

        [Test]
        public void MissingSerializedValuesKeepDefaultsAndInspectorValuesClampAtTheirOwnBounds()
        {
            JsonUtility.FromJsonOverwrite("{\"yinChance\":40,\"yangChance\":60}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(228).Take(8), Is.EqualTo(new[] { 1f, 1f, 7f, 18f, 30f, 1f, 3f, 2f }));
            JsonUtility.FromJsonOverwrite("{\"jigwiStageTimelineEnabled\":false,\"jigwiStageWarningSeconds\":-1,\"jigwiStageActiveSeconds\":30,\"jigwiStageRootAngleDegrees\":0,\"jigwiStageFireAngleDegrees\":70,\"jigwiStageSpreadSeconds\":0,\"jigwiStageSpreadPatches\":8,\"jigwiStageFeverMultiplier\":0}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(228).Take(8), Is.EqualTo(new[] { 0f, 0f, 20f, 5f, 45f, .1f, 3f, 1f }));
        }
    }
}
