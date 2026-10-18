using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabStraightThrowConfigTests
    {
        private LabConfig config;
        [SetUp] public void SetUp() => config = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void ArcRemainsDefaultAndTheSevenNewFieldsUseTheirOwnCategory()
        {
            Assert.That(config.ThrowForm, Is.EqualTo(LabThrowForm.Arc));
            Assert.That(config.EffectiveThrowGravity, Is.EqualTo(config.ThrowGravity));
            Assert.That(config.StraightOriginOffset, Is.EqualTo(new Vector3(.35f, .4f, 0f)));
            Assert.That(config.CaptureDeveloperValues().Skip(112).ToArray(),
                Is.EqualTo(new[] { 0f, .35f, .4f, 0f, 3f, .1f, .1f }));
            string[] names = {
                "Throw Form (0 arc, 1 straight)", "Straight Origin Offset X", "Straight Origin Offset Y",
                "Straight Origin Offset Z", "Straight Pitch Offset (degrees)", "Straight Loft Scale", "Straight Gravity Scale"
            };
            Assert.That(LabConfig.DeveloperFields.Skip(112).Select(field => field.Label).ToArray(), Is.EqualTo(names));
            Assert.That(LabConfig.DeveloperFields[112].IsToggle, Is.True);
            Assert.That(new[] { LabConfig.DeveloperFields[112].OffText, LabConfig.DeveloperFields[112].OnText },
                Is.EqualTo(new[] { "ARC", "STRAIGHT" }));
            Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Throw form").FieldIndices,
                Is.EqualTo(Enumerable.Range(112, 7).ToArray()));
        }

        [Test]
        public void StraightFieldsRoundTripThroughJsonAndFingerprintWithoutChangingThePrevious112Values()
        {
            float[] initial = config.CaptureDeveloperValues();
            initial[111] = 1f;
            Assert.That(config.TryApplyDeveloperValues(initial, out string enabledError), Is.True, enabledError);
            float[] changed = (float[])initial.Clone();
            float[] profile = { 1f, -.75f, .2f, .5f, -2f, .25f, .05f };
            Array.Copy(profile, 0, changed, 112, profile.Length);
            string initialHash = LabSessionSettings.Fingerprint(initial, 2, 1);
            string changedHash = LabSessionSettings.Fingerprint(changed, 2, 1);
            Assert.That(changedHash, Is.Not.EqualTo(initialHash));
            var received = JsonUtility.FromJson<WireValues>(JsonUtility.ToJson(new WireValues { values = changed }));
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(client.TryApplyDeveloperValues(received.values, out string error), Is.True, error);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(changed));
                Assert.That(client.CaptureDeveloperValues().Take(112), Is.EqualTo(initial.Take(112)));
                Assert.That(client.CompletedGourdViewerEnabled, Is.True, "The preserved prefix includes the viewer permission at 111.");
                Assert.That(client.ThrowForm, Is.EqualTo(LabThrowForm.Straight));
                Assert.That(client.StraightOriginOffset, Is.EqualTo(new Vector3(-.75f, .2f, .5f)));
                Assert.That(client.StraightPitchOffsetDegrees, Is.EqualTo(-2f));
                Assert.That(client.StraightLoftScale, Is.EqualTo(.25f));
                Assert.That(client.StraightGravityScale, Is.EqualTo(.05f));
                Assert.That(client.EffectiveThrowGravity, Is.EqualTo(client.ThrowGravity * .05f).Within(.00001f));
                Assert.That(LabSessionSettings.Fingerprint(received.values, 2, 1), Is.EqualTo(changedHash));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(initial), "Applying the session copy does not edit the source.");
            }
            finally { UnityEngine.Object.DestroyImmediate(client); }
            for (int i = 112; i <= 118; i++)
            {
                float[] oneField = (float[])initial.Clone();
                oneField[i] = oneField[i] == LabConfig.DeveloperFields[i].Max
                    ? LabConfig.DeveloperFields[i].Min : LabConfig.DeveloperFields[i].Max;
                Assert.That(LabSessionSettings.Fingerprint(oneField, 2, 1), Is.Not.EqualTo(initialHash), "Shared field " + i);
            }
        }

        [TestCase(112, 0f, 1f)]
        [TestCase(113, -5f, 5f)]
        [TestCase(114, -5f, 5f)]
        [TestCase(115, -5f, 5f)]
        [TestCase(116, -30f, 30f)]
        [TestCase(117, 0f, 1f)]
        [TestCase(118, 0f, 2f)]
        public void EveryStraightFieldValidatesBeforeAnyEarlierValueIsApplied(int index, float min, float max)
        {
            foreach (float boundary in new[] { min, max })
            {
                float[] valid = config.CaptureDeveloperValues();
                valid[index] = boundary;
                Assert.That(config.TryApplyDeveloperValues(valid, out string error), Is.True, error);
                Assert.That(config.CaptureDeveloperValues()[index], Is.EqualTo(boundary));
            }
            float[] before = config.CaptureDeveloperValues();
            foreach (float invalid in new[] { min - 1f, max + 1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float[] values = (float[])before.Clone();
                values[0] = 9999f;
                values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
                if (float.IsNaN(invalid) || float.IsInfinity(invalid))
                    Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 2, 1));
            }
        }

        [Test]
        public void FractionalModeAndPrevious112ValueSchemaAreRejected()
        {
            float[] before = config.CaptureDeveloperValues();
            float[] fractional = (float[])before.Clone();
            fractional[0] = 500f;
            fractional[112] = .5f;
            Assert.That(config.TryApplyDeveloperValues(fractional, out string error), Is.False);
            Assert.That(error, Does.Contain("whole number"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            float[] oldSchema = before.Take(112).ToArray();
            Assert.That(config.TryApplyDeveloperValues(oldSchema, out error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(oldSchema, 2, 1));
        }

        [Test]
        public void InspectorValuesClampAndNonFiniteCoordinatesFallBackPerAxis()
        {
            JsonUtility.FromJsonOverwrite("{\"throwForm\":99,\"straightOriginOffset\":{\"x\":-99,\"y\":99,\"z\":10},\"straightPitchOffsetDegrees\":99,\"straightLoftScale\":-1,\"straightGravityScale\":9}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(112).ToArray(),
                Is.EqualTo(new[] { 0f, -5f, 5f, 5f, 30f, 0f, 2f }));
            SetPrivate("straightOriginOffset", new Vector3(float.NaN, float.PositiveInfinity, float.NegativeInfinity));
            SetPrivate("straightPitchOffsetDegrees", float.NaN);
            SetPrivate("straightLoftScale", float.PositiveInfinity);
            SetPrivate("straightGravityScale", float.NegativeInfinity);
            Assert.That(config.CaptureDeveloperValues().Skip(112).ToArray(),
                Is.EqualTo(new[] { 0f, .35f, .4f, 0f, 3f, .1f, .1f }));
        }

        private void SetPrivate(string field, object value) =>
            typeof(LabConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, value);
        [Serializable] private sealed class WireValues { public float[] values; }
    }
}
