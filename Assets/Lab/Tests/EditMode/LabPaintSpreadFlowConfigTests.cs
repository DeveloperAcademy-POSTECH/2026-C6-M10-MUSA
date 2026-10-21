using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Round tuning/schema checks, not rendered or real Client connection results.</summary>
    public sealed class LabPaintSpreadFlowConfigTests
    {
        private LabConfig config;
        private float[] baseline;
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        [Serializable] private sealed class SharedTuning { public float[] values; }

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[111] = 1f; values[112] = 1f; values[119] = .75f;
            values[129] = 1f; values[130] = 2f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> Fields()
        {
            yield return new TestCaseData(133, "PaintGrowthStyle", 0f, 1f);
            yield return new TestCaseData(134, "PaintEdgeRaggedness", 0f, 1f);
            yield return new TestCaseData(135, "PaintFlowRatio", 0f, 1f);
            yield return new TestCaseData(136, "PaintDripWidthDegrees", .5f, 12f);
            yield return new TestCaseData(137, "PaintDripLengthDegrees", 5f, 135f);
            yield return new TestCaseData(138, "PaintGravityWeight", 1f, 8f);
        }

        [Test]
        public void SixSettingsAppendToGourdWhileNewConfigsKeepOriginalGrowthAndGrainOff()
        {
            Assert.That(baseline.Length, Is.EqualTo(169));
            Assert.That(baseline.Skip(133).Take(6), Is.EqualTo(new[] { 0f, .55f, .35f, 3f, 60f, 3f }));
            Assert.That(LabConfig.DeveloperFields.Skip(133).Take(6).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Paint Growth Style (0 original, 1 spread + flow)", "Paint Edge Raggedness", "Paint Flow Ratio",
                "Paint Drip Width (surface degrees)", "Paint Drip Length (surface degrees)", "Paint Gravity Weight"
            }));
            LabDeveloperField style = LabConfig.DeveloperFields[133];
            Assert.That(style.IsToggle && style.WholeNumber, Is.True);
            Assert.That(style.OffText, Is.EqualTo("ORIGINAL"));
            Assert.That(style.OnText, Is.EqualTo("SPREAD + FLOW"));
            Assert.That(LabConfig.DeveloperFields.Skip(134).Take(5).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices.Where(index => index >= 133 && index <= 138),
                Is.EqualTo(new[] { 133, 134, 135, 136, 137, 138 }));
            var untouched = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(untouched.PaintGrowthStyle, Is.Zero);
                Assert.That(untouched.PaintGrainEnabled, Is.False);
                Assert.That(untouched.PaintVictoryPercent, Is.EqualTo(100));
                AssertSettingsMatch(untouched);
            }
            finally { Object.DestroyImmediate(untouched); }
        }

        [TestCaseSource(nameof(Fields))]
        public void EndpointsRoundTripThroughConfigJsonAndFingerprintPreservingAllPrevious133Values(
            int index, string propertyName, float minimum, float maximum)
        {
            foreach (float endpoint in new[] { minimum, maximum })
            {
                float[] values = (float[])baseline.Clone(); values[index] = endpoint;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                object property = typeof(LabConfig).GetProperty(propertyName)?.GetValue(config);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(Convert.ToSingle(property), Is.EqualTo(endpoint));
                Assert.That(config.CaptureDeveloperValues().Take(133), Is.EqualTo(baseline.Take(133)));
                AssertSettingsMatch(config);
                var client = ScriptableObject.CreateInstance<LabConfig>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), client);
                    Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values), "Inspector serialization keeps all six values.");
                    string wire = JsonUtility.ToJson(new SharedTuning { values = config.CaptureDeveloperValues() });
                    float[] received = JsonUtility.FromJson<SharedTuning>(wire).values;
                    Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                    Assert.That(LabSessionSettings.Fingerprint(received, 3, Protocol),
                        Is.EqualTo(LabSessionSettings.Fingerprint(values, 3, Protocol)));
                    float[] changed = (float[])values.Clone(); changed[index] = endpoint == minimum ? maximum : minimum;
                    Assert.That(LabSessionSettings.Fingerprint(changed, 3, Protocol),
                        Is.Not.EqualTo(LabSessionSettings.Fingerprint(values, 3, Protocol)));
                }
                finally { Object.DestroyImmediate(client); }
            }
        }

        [TestCaseSource(nameof(Fields))]
        public void InvalidLateValueAtomicallyRejectsAllPriorChanges(int index, string propertyName, float minimum, float maximum)
        {
            var invalids = new List<float> { minimum - .25f, maximum + .25f, float.NaN,
                float.NegativeInfinity, float.PositiveInfinity };
            if (index == 133) invalids.Add(.5f);
            foreach (float invalid in invalids)
            {
                float[] values = (float[])baseline.Clone(); values[0] = 777f; values[129] = 0f; values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, propertyName);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            }
        }

        [Test]
        public void Old133ValueSchemaAndIncompleteTailAreRejectedBeforeApplyingAnyValue()
        {
            foreach (int length in new[] { 133, 138, 139, 141 })
            {
                var values = new float[length]; Array.Copy(baseline, values, Math.Min(length, baseline.Length));
                values[0] = 777f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
                Assert.That(error, Does.Contain("incomplete"));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
                Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 3, Protocol));
            }
            Assert.That(Protocol, Is.EqualTo(5648));
        }

        [Test]
        public void InspectorBoundsAreEffectiveAndCapturedSettingsStayIndependentBetweenRounds()
        {
            LabPaintGrowthSettings previous = config.PaintGrowthSettings;
            JsonUtility.FromJsonOverwrite("{\"paintGrowthStyle\":7,\"paintEdgeRaggedness\":-2," +
                "\"paintFlowRatio\":3,\"paintDripWidthDegrees\":-9,\"paintDripLengthDegrees\":999," +
                "\"paintGravityWeight\":-5}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(133).Take(6), Is.EqualTo(new[] { 1f, 0f, 1f, .5f, 135f, 1f }));
            AssertSettingsMatch(config);
            Assert.That(previous.Style, Is.Zero);
            Assert.That(previous.Raggedness, Is.EqualTo(.55f));
            Assert.That(previous.FlowRatio, Is.EqualTo(.35f));
            Assert.That(previous.DripWidthDegrees, Is.EqualTo(3f));
            Assert.That(previous.DripLengthDegrees, Is.EqualTo(60f));
            Assert.That(previous.GravityWeight, Is.EqualTo(3f));
        }

        private static void AssertSettingsMatch(LabConfig source)
        {
            LabPaintGrowthSettings settings = source.PaintGrowthSettings;
            Assert.That(settings.Style, Is.EqualTo(source.PaintGrowthStyle));
            Assert.That(settings.Raggedness, Is.EqualTo(source.PaintEdgeRaggedness));
            Assert.That(settings.FlowRatio, Is.EqualTo(source.PaintFlowRatio));
            Assert.That(settings.DripWidthDegrees, Is.EqualTo(source.PaintDripWidthDegrees));
            Assert.That(settings.DripLengthDegrees, Is.EqualTo(source.PaintDripLengthDegrees));
            Assert.That(settings.GravityWeight, Is.EqualTo(source.PaintGravityWeight));
        }
    }
}
