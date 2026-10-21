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
            values[110] = 1f; values[111] = 1f; values[118] = .75f;
            values[128] = 1f; values[129] = 2f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> Fields()
        {
            yield return new TestCaseData(132, "PaintGrowthStyle", 0f, 1f);
            yield return new TestCaseData(133, "PaintEdgeRaggedness", 0f, 1f);
        }

        [Test]
        public void GrowthPresentationSettingsKeepOriginalGrowthAndGrainOffByDefault()
        {
            Assert.That(baseline.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(baseline.Skip(132).Take(2), Is.EqualTo(new[] { 0f, .55f }));
            Assert.That(LabConfig.DeveloperFields.Skip(132).Take(2).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Paint Growth Style (0 original, 1 spread + flow)", "Paint Edge Raggedness"
            }));
            LabDeveloperField style = LabConfig.DeveloperFields[132];
            Assert.That(style.IsToggle && style.WholeNumber, Is.True);
            Assert.That(style.OffText, Is.EqualTo("ORIGINAL"));
            Assert.That(style.OnText, Is.EqualTo("SPREAD + FLOW"));
            Assert.That(LabConfig.DeveloperFields.Skip(133).Take(1).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            var gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd");
            Assert.That(gourd.FieldIndices.Where(index => index >= 132 && index <= 133),
                Is.EqualTo(new[] { 132, 133 }));
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
        public void EndpointsRoundTripThroughConfigJsonAndFingerprintPreservingAllPrevious132Values(
            int index, string propertyName, float minimum, float maximum)
        {
            foreach (float endpoint in new[] { minimum, maximum })
            {
                float[] values = (float[])baseline.Clone(); values[index] = endpoint;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                object property = typeof(LabConfig).GetProperty(propertyName)?.GetValue(config);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(Convert.ToSingle(property), Is.EqualTo(endpoint));
                Assert.That(config.CaptureDeveloperValues().Take(132), Is.EqualTo(baseline.Take(132)));
                AssertSettingsMatch(config);
                var client = ScriptableObject.CreateInstance<LabConfig>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), client);
                    Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values), "Inspector serialization keeps both presentation values.");
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
            if (index == 132) invalids.Add(.5f);
            foreach (float invalid in invalids)
            {
                float[] values = (float[])baseline.Clone(); values[0] = 777f; values[128] = 0f; values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, propertyName);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            }
        }

        [Test]
        public void InspectorBoundsAreEffectiveAndCapturedSettingsStayIndependentBetweenRounds()
        {
            LabPaintGrowthSettings previous = config.PaintGrowthSettings;
            JsonUtility.FromJsonOverwrite("{\"paintGrowthStyle\":7,\"paintEdgeRaggedness\":-2}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(132).Take(2), Is.EqualTo(new[] { 1f, 0f }));
            AssertSettingsMatch(config);
            Assert.That(previous.Style, Is.Zero);
            Assert.That(previous.Raggedness, Is.EqualTo(.55f));
        }

        private static void AssertSettingsMatch(LabConfig source)
        {
            LabPaintGrowthSettings settings = source.PaintGrowthSettings;
            Assert.That(settings.Style, Is.EqualTo(source.PaintGrowthStyle));
            Assert.That(settings.Raggedness, Is.EqualTo(source.PaintEdgeRaggedness));
        }
    }
}
