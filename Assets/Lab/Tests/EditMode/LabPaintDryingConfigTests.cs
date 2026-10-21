using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Shared schema and runtime-copy boundaries, not a claim of remote or visual execution.</summary>
    public sealed class LabPaintDryingConfigTests
    {
        private LabConfig config;
        private float[] baseline;
        [Serializable] private sealed class SharedTuning { public float[] values; }
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        [SetUp] public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            baseline = config.CaptureDeveloperValues();
        }
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        [Test]
        public void FreshAndAuthoredGameDefaultsEnableDryingAndExposeFourAppendedGourdControls()
        {
            Assert.That(new LabPaintGrowthSettings().DryingEnabled, Is.False, "Bare surface fixtures remain opt-in, independently of the game config default.");
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(169));
            Assert.That(Protocol, Is.EqualTo(5648));
            Assert.That(LabConfig.DeveloperFields.Skip(153).Take(4).Select(field => field.DisplayLabel), Is.EqualTo(new[]
            {
                "Paint Drying", "Drying Delay (seconds)", "Drying Rate (%/s)", "Nearby Hit Radius (surface degrees)"
            }));
            Assert.That(LabConfig.DeveloperFields[153].IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperFields.Skip(154).Take(3).Select(field => field.Min), Is.EqualTo(new[] { 0f, 0f, 1f }));
            Assert.That(LabConfig.DeveloperFields.Skip(154).Take(3).Select(field => field.Max), Is.EqualTo(new[] { 60f, 5f, 90f }));
            Assert.That(LabConfig.DeveloperFields.Skip(154).Take(3).All(field => !field.IsToggle && !field.IsChoice && !field.WholeNumber), Is.True);
            int[] gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices;
            Assert.That(gourd.Skip(gourd.Length - 4), Is.EqualTo(new[] { 153, 154, 155, 156 }));
            foreach (int field in Enumerable.Range(153, 4))
                Assert.That(LabConfig.DeveloperCategories.SelectMany(category => category.FieldIndices).Count(index => index == field), Is.EqualTo(1));
            Assert.That(LabConfig.DeveloperFields[152].Label, Is.EqualTo("Small Orb Paint Share (x paint)"));
            Assert.That(LabConfig.DeveloperFields[145].Label, Is.EqualTo("Gourd Visual Size (x)"));
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                Assert.That(source.CaptureDeveloperValues().Skip(153).Take(4), Is.EqualTo(new[] { 1f, 5f, .2f, 20f }));
                Assert.That(source.PaintDryingEnabled, Is.True);
                Assert.That(source.PaintRepeatPercent, Is.EqualTo(1.5f));
                Assert.That(source.CompletedGourdViewerEnabled && source.PlayerPaintPatternsEnabled, Is.True);
                AssertSettingsMatch(source);
            }
        }

        [TestCase(0f, 0f, 0f, 1f)]
        [TestCase(1f, 60f, 5f, 90f)]
        [TestCase(1f, 3.75f, .125f, 17.5f)]
        public void SharedSettingsRoundTripAndRoundSnapshotsRemainIndependent(float enabled, float delay, float rate, float radius)
        {
            float[] values = (float[])baseline.Clone();
            values[153] = enabled; values[154] = delay; values[155] = rate; values[156] = radius;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(values));
            Assert.That(config.CaptureDeveloperValues().Take(153), Is.EqualTo(baseline.Take(153)), "Every older setting keeps its index and value.");
            AssertSettingsMatch(config);
            LabPaintGrowthSettings captured = config.PaintGrowthSettings;
            LabConfig clone = Object.Instantiate(config);
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                string json = JsonUtility.ToJson(new SharedTuning { values = config.CaptureDeveloperValues() });
                float[] received = JsonUtility.FromJson<SharedTuning>(json).values;
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(clone.CaptureDeveloperValues(), Is.EqualTo(values));
                Assert.That(LabSessionSettings.Fingerprint(received, 2, Protocol),
                    Is.EqualTo(LabSessionSettings.Fingerprint(values, 2, Protocol)));
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), client);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                AssertSettingsMatch(client);
                values[153] = 1f - enabled; values[154] = delay == 60f ? 0f : 60f;
                values[155] = rate == 5f ? 0f : 5f; values[156] = radius == 90f ? 1f : 90f;
                Assert.That(config.TryApplyDeveloperValues(values, out error), Is.True, error);
                Assert.That(captured.DryingEnabled, Is.EqualTo(enabled == 1f));
                Assert.That(captured.DryingDelaySeconds, Is.EqualTo(delay));
                Assert.That(captured.DryingPercentPerSecond, Is.EqualTo(rate));
                Assert.That(captured.NearbyHitRadiusDegrees, Is.EqualTo(radius));
                Assert.That(clone.PaintGrowthSettings.DryingDelaySeconds, Is.EqualTo(delay));
            }
            finally { Object.DestroyImmediate(clone); Object.DestroyImmediate(client); }
        }

        [TestCase(153, 0f)]
        [TestCase(154, 0f)]
        [TestCase(155, 0f)]
        [TestCase(156, 90f)]
        public void EachDryingValueIsIncludedInTheAcknowledgedRoomFingerprint(int field, float value)
        {
            string original = LabSessionSettings.Fingerprint(baseline, 2, Protocol);
            float[] values = (float[])baseline.Clone(); values[field] = value;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            Assert.That(LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), 2, Protocol), Is.Not.EqualTo(original));
        }

        [TestCase(153)]
        [TestCase(154)]
        [TestCase(155)]
        [TestCase(156)]
        public void InvalidLateInputRejectsAllFieldsEvenWhenDryingIsOff(int field)
        {
            LabDeveloperField schema = LabConfig.DeveloperFields[field];
            float[] invalids = field == 153
                ? new[] { -.01f, 1.01f, .5f, float.NaN, float.NegativeInfinity, float.PositiveInfinity }
                : new[] { schema.Min - .01f, schema.Max + .01f, float.NaN, float.NegativeInfinity, float.PositiveInfinity };
            foreach (float invalid in invalids)
            {
                float[] values = (float[])baseline.Clone(); values[0] = 777f; values[111] = 0f; values[153] = 0f; values[field] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain(schema.Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            }
        }

        [TestCase(153)]
        [TestCase(156)]
        [TestCase(161)]
        public void PreviousOrUnknownSchemaCannotInstallAnySetting(int count)
        {
            var values = new float[count]; Array.Copy(baseline, values, Math.Min(count, baseline.Length)); values[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 2, Protocol));
        }

        [Test]
        public void InspectorValuesClampAndNonfiniteValuesFallBackWithoutAlteringOtherSettings()
        {
            JsonUtility.FromJsonOverwrite("{\"paintDryingDelaySeconds\":-1,\"paintDryingPercentPerSecond\":10,\"paintNearbyHitRadiusDegrees\":0}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(154).Take(3), Is.EqualTo(new[] { 0f, 5f, 1f }));
            JsonUtility.FromJsonOverwrite("{\"paintDryingDelaySeconds\":100,\"paintDryingPercentPerSecond\":-1,\"paintNearbyHitRadiusDegrees\":100}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(154).Take(3), Is.EqualTo(new[] { 60f, 0f, 90f }));
            foreach (float invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                foreach (string field in new[] { "paintDryingDelaySeconds", "paintDryingPercentPerSecond", "paintNearbyHitRadiusDegrees" })
                    typeof(LabConfig).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, invalid);
                Assert.That(config.CaptureDeveloperValues().Skip(153).Take(4), Is.EqualTo(new[] { 1f, 5f, .2f, 20f }));
                AssertSettingsMatch(config);
            }
            Assert.That(config.CaptureDeveloperValues().Take(153), Is.EqualTo(baseline.Take(153)));
        }

        private static void AssertSettingsMatch(LabConfig source)
        {
            LabPaintGrowthSettings settings = source.PaintGrowthSettings;
            Assert.That(settings.DryingEnabled, Is.EqualTo(source.PaintDryingEnabled));
            Assert.That(settings.DryingDelaySeconds, Is.EqualTo(source.PaintDryingDelaySeconds));
            Assert.That(settings.DryingPercentPerSecond, Is.EqualTo(source.PaintDryingPercentPerSecond));
            Assert.That(settings.NearbyHitRadiusDegrees, Is.EqualTo(source.PaintNearbyHitRadiusDegrees));
        }
    }
}
