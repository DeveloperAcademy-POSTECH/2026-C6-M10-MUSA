using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabPlayerPaintPatternConfigTests
    {
        private LabConfig config;
        [Serializable] private sealed class Shared { public float[] values; }
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        [SetUp] public void Create() => config = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        [Test]
        public void FreshAndAuthoredDefaultsEnableBothIndependentGourdPresentationSwitches()
        {
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                Assert.That(source.PlayerPaintPatternsEnabled, Is.True);
                Assert.That(source.CompletedGourdViewerEnabled, Is.True);
                float[] values = source.CaptureDeveloperValues();
                Assert.That(values.Length, Is.EqualTo(171));
                Assert.That(values[111], Is.EqualTo(1f));
                Assert.That(values[142], Is.EqualTo(1f));
            }
            Assert.That(LabConfig.DeveloperFields[111].DisplayLabel, Is.EqualTo("Completed Gourd Viewer"));
            Assert.That(LabConfig.DeveloperFields[139].Label, Is.EqualTo("Paint Pattern"));
            Assert.That(LabConfig.DeveloperFields[140].Label, Is.EqualTo("Chuimsae Chance (%)"));
            Assert.That(LabConfig.DeveloperFields[141].Label, Is.EqualTo("Chuimsae Min Gap (generates)"));
            LabDeveloperField field = LabConfig.DeveloperFields[142];
            Assert.That(field.Label, Is.EqualTo("Player Paint Patterns (0 off, 1 on)"));
            Assert.That(field.DisplayLabel, Is.EqualTo("Player Paint Patterns"));
            Assert.That(field.IsToggle && field.WholeNumber, Is.True);
            Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 0f, 1f }));
            Assert.That(new[] { field.OffText, field.OnText }, Is.EqualTo(new[] { "OFF", "ON" }));
            int[] gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices;
            Assert.That(gourd, Does.Contain(111));
            Assert.That(gourd, Does.Contain(142));
            Assert.That(gourd.Count(index => index == 142), Is.EqualTo(1));
            Assert.That(Protocol, Is.EqualTo(5651));
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, 1f)]
        public void SwitchesRoundTripIndependentlyThroughSharedSettingsAndClones(float patterns, float viewer)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] values = (float[])before.Clone();
            values[111] = viewer;
            values[142] = patterns;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.PlayerPaintPatternsEnabled, Is.EqualTo(patterns == 1f));
            Assert.That(config.CompletedGourdViewerEnabled, Is.EqualTo(viewer == 1f));
            float[] actual = config.CaptureDeveloperValues();
            Assert.That(actual, Is.EqualTo(values));
            Assert.That(actual.Take(111), Is.EqualTo(before.Take(111)));
            Assert.That(actual.Skip(112).Take(30), Is.EqualTo(before.Skip(112).Take(30)),
                "Both switches affect presentation only; all earlier gameplay and tuning values remain unchanged.");

            string fingerprint = LabSessionSettings.Fingerprint(values, 3, Protocol);
            string json = JsonUtility.ToJson(new Shared { values = actual });
            float[] received = JsonUtility.FromJson<Shared>(json).values;
            var client = ScriptableObject.CreateInstance<LabConfig>();
            LabConfig clone = Object.Instantiate(config);
            try
            {
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(clone.CaptureDeveloperValues(), Is.EqualTo(values),
                    "Runtime and completed-gourd preview copies must retain both switches.");
                Assert.That(LabSessionSettings.Fingerprint(client.CaptureDeveloperValues(), 3, Protocol), Is.EqualTo(fingerprint));
                float[] oppositePatterns = (float[])values.Clone();
                oppositePatterns[142] = 1f - patterns;
                Assert.That(LabSessionSettings.Fingerprint(oppositePatterns, 3, Protocol), Is.Not.EqualTo(fingerprint),
                    "Clients must acknowledge the Host's player-pattern setting before the round starts.");
                values[142] = 1f - patterns;
                Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
                Assert.That(clone.PlayerPaintPatternsEnabled, Is.EqualTo(patterns == 1f),
                    "Changing the source config cannot alter an already captured round or preview copy.");
            }
            finally { Object.DestroyImmediate(client); Object.DestroyImmediate(clone); }
        }

        [TestCase(-1f)]
        [TestCase(2f)]
        [TestCase(.5f)]
        [TestCase(.9999f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidPatternSwitchRejectsTheEntireSettingsFormAtomically(float invalid)
        {
            float[] before = config.CaptureDeveloperValues();
            float[] invalidValues = (float[])before.Clone();
            invalidValues[0] = 777f;
            invalidValues[111] = 0f;
            invalidValues[139] = (float)LabPaintPattern.Flow;
            invalidValues[142] = invalid;
            Assert.That(config.TryApplyDeveloperValues(invalidValues, out string error), Is.False);
            Assert.That(error, Does.Contain("Player Paint Patterns"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
        }

        [TestCase(142)]
        [TestCase(143)]
        [TestCase(145)]
        [TestCase(147)]
        public void OldOrUnknownSharedSettingsSchemasAreRejectedBeforeApplyingAnyField(int count)
        {
            float[] before = config.CaptureDeveloperValues();
            var values = new float[count];
            Array.Copy(before, values, Math.Min(before.Length, count));
            values[0] = 777f;
            values[111] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 3, Protocol));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InspectorSwitchUsesTheSameCapturedBooleanWithoutChangingViewerPermission(bool enabled)
        {
            JsonUtility.FromJsonOverwrite("{\"playerPaintPatternsEnabled\":" + (enabled ? "true" : "false") + "}", config);
            Assert.That(config.PlayerPaintPatternsEnabled, Is.EqualTo(enabled));
            Assert.That(config.CaptureDeveloperValues()[142], Is.EqualTo(enabled ? 1f : 0f));
            Assert.That(config.CompletedGourdViewerEnabled, Is.True);
        }
    }
}
