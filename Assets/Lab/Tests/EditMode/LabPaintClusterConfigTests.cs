using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Shared round tuning checks; connected peers and rendering are verified separately.</summary>
    public sealed class LabPaintClusterConfigTests
    {
        private LabConfig config;
        private float[] baseline;
        [Serializable] private sealed class SharedTuning { public float[] values; }
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        [Test]
        public void TwoSlidersAppendToTheSchemaBesideRepeatGrowthWithPreservedDefaults()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(157));
            Assert.That(Protocol, Is.EqualTo(5643));
            Assert.That(LabConfig.DeveloperFields.Skip(143).Take(2).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Paint Cluster Compactness", "Paint Cluster Irregularity"
            }));
            foreach (LabDeveloperField field in LabConfig.DeveloperFields.Skip(143).Take(2))
            {
                Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 0f, 1f }));
                Assert.That(field.WholeNumber || field.IsToggle || field.IsChoice, Is.False);
            }
            int[] gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices;
            int repeat = Array.IndexOf(gourd, 107);
            Assert.That(gourd.Skip(repeat).Take(4), Is.EqualTo(new[] { 107, 143, 144, 108 }));
            Assert.That(gourd.Count(index => index == 143), Is.EqualTo(1));
            Assert.That(gourd.Count(index => index == 144), Is.EqualTo(1));

            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            foreach (LabConfig source in new[] { config, authored })
            {
                float[] values = source.CaptureDeveloperValues();
                Assert.That(values.Length, Is.EqualTo(157));
                Assert.That(values.Skip(143).Take(2), Is.EqualTo(new[] { .85f, .25f }));
                Assert.That(source.PaintRepeatPercent, Is.EqualTo(1.5f));
                Assert.That(source.PaintGrowthPercentPerSecond, Is.EqualTo(6f));
                Assert.That(source.CompletedGourdViewerEnabled && source.PlayerPaintPatternsEnabled, Is.True);
                Assert.That(source.PaintVictoryPercent, Is.EqualTo(100));
                AssertSettingsMatch(source);
            }
        }

        [TestCase(143)]
        [TestCase(144)]
        public void EachSliderAcceptsEndpointsAndFractionalValuesWithoutChangingAnotherField(int index)
        {
            foreach (float value in new[] { 0f, .375f, 1f })
            {
                float[] requested = (float[])baseline.Clone();
                requested[index] = value;
                Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.True, error);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(config.CaptureDeveloperValues().Take(143), Is.EqualTo(baseline.Take(143)),
                    "All previous gameplay, footprint, grain, presentation and sound values keep their indices and values.");
                AssertSettingsMatch(config);

                string fingerprint = LabSessionSettings.Fingerprint(requested, 3, Protocol);
                float[] otherValue = (float[])requested.Clone();
                otherValue[index] = value == 1f ? 0f : 1f;
                Assert.That(LabSessionSettings.Fingerprint(otherValue, 3, Protocol), Is.Not.EqualTo(fingerprint),
                    "Each cluster value must be acknowledged in the Host's shared room fingerprint.");
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, 1f)]
        public void BothValuesShareSerializeAndCloneIndependently(float compactness, float irregularity)
        {
            float[] requested = (float[])baseline.Clone();
            requested[143] = compactness;
            requested[144] = irregularity;
            Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.True, error);
            LabPaintGrowthSettings captured = config.PaintGrowthSettings;
            LabConfig clone = Object.Instantiate(config);
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                string json = JsonUtility.ToJson(new SharedTuning { values = config.CaptureDeveloperValues() });
                float[] received = JsonUtility.FromJson<SharedTuning>(json).values;
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(clone.CaptureDeveloperValues(), Is.EqualTo(requested));
                Assert.That(LabSessionSettings.Fingerprint(received, 3, Protocol),
                    Is.EqualTo(LabSessionSettings.Fingerprint(requested, 3, Protocol)));
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(config), client);
                Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(requested),
                    "Both private serialized fields survive config JSON as well as the shared float array.");
                AssertSettingsMatch(client);

                requested[143] = 1f - compactness;
                requested[144] = 1f - irregularity;
                Assert.That(config.TryApplyDeveloperValues(requested, out error), Is.True, error);
                Assert.That(captured.ClusterCompactness, Is.EqualTo(compactness));
                Assert.That(captured.ClusterIrregularity, Is.EqualTo(irregularity));
                Assert.That(clone.PaintClusterCompactness, Is.EqualTo(compactness));
                Assert.That(clone.PaintClusterIrregularity, Is.EqualTo(irregularity));
                captured.ClusterCompactness = .5f;
                captured.ClusterIrregularity = .5f;
                Assert.That(config.PaintClusterCompactness, Is.EqualTo(1f - compactness));
                Assert.That(config.PaintClusterIrregularity, Is.EqualTo(1f - irregularity));
            }
            finally { Object.DestroyImmediate(clone); Object.DestroyImmediate(client); }
        }

        [TestCase(143)]
        [TestCase(144)]
        public void InvalidLateValuesRejectAllChangesAtomically(int index)
        {
            foreach (float invalid in new[] { -.001f, 1.001f, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                float[] requested = (float[])baseline.Clone();
                requested[0] = 777f;
                requested[111] = 0f;
                requested[142] = 0f;
                requested[143] = .1f;
                requested[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.False);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
                if (float.IsNaN(invalid) || float.IsInfinity(invalid))
                    Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(requested, 3, Protocol));
            }
        }

        [TestCase(143)]
        [TestCase(144)]
        [TestCase(145)]
        [TestCase(147)]
        public void PreviousIncompleteAndUnknownSharedSchemasCannotApplyAnyField(int count)
        {
            var requested = new float[count];
            Array.Copy(baseline, requested, Math.Min(baseline.Length, count));
            requested[0] = 777f;
            requested[111] = 0f;
            Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(requested, 3, Protocol));
        }

        [Test]
        public void InspectorRangesClampAndNonfiniteValuesUseIndependentDefaults()
        {
            JsonUtility.FromJsonOverwrite("{\"paintClusterCompactness\":-2,\"paintClusterIrregularity\":4}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(143).Take(2), Is.EqualTo(new[] { 0f, 1f }));
            JsonUtility.FromJsonOverwrite("{\"paintClusterCompactness\":4,\"paintClusterIrregularity\":-2}", config);
            Assert.That(config.CaptureDeveloperValues().Skip(143).Take(2), Is.EqualTo(new[] { 1f, 0f }));
            FieldInfo compactness = typeof(LabConfig).GetField("paintClusterCompactness", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo irregularity = typeof(LabConfig).GetField("paintClusterIrregularity", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (float invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                compactness.SetValue(config, invalid);
                irregularity.SetValue(config, .75f);
                Assert.That(config.CaptureDeveloperValues().Skip(143).Take(2), Is.EqualTo(new[] { .85f, .75f }));
                compactness.SetValue(config, .125f);
                irregularity.SetValue(config, invalid);
                Assert.That(config.CaptureDeveloperValues().Skip(143).Take(2), Is.EqualTo(new[] { .125f, .25f }));
                AssertSettingsMatch(config);
            }
            Assert.That(config.CaptureDeveloperValues().Take(143), Is.EqualTo(baseline.Take(143)));
        }

        [Test]
        public void OldSerializedConfigWithoutTheTwoFieldsKeepsNewDefaultsAndPreviousValues()
        {
            JsonUtility.FromJsonOverwrite("{\"paintRepeatPercent\":8,\"paintGrowthPercentPerSecond\":12," +
                "\"completedGourdViewerEnabled\":false,\"playerPaintPatternsEnabled\":false}", config);
            Assert.That(config.PaintRepeatPercent, Is.EqualTo(8f));
            Assert.That(config.PaintGrowthPercentPerSecond, Is.EqualTo(12f));
            Assert.That(config.CompletedGourdViewerEnabled || config.PlayerPaintPatternsEnabled, Is.False);
            Assert.That(config.CaptureDeveloperValues().Skip(143).Take(2), Is.EqualTo(new[] { .85f, .25f }));
            AssertSettingsMatch(config);
        }

        private static void AssertSettingsMatch(LabConfig source)
        {
            LabPaintGrowthSettings settings = source.PaintGrowthSettings;
            Assert.That(settings.ClusterCompactness, Is.EqualTo(source.PaintClusterCompactness));
            Assert.That(settings.ClusterIrregularity, Is.EqualTo(source.PaintClusterIrregularity));
            Assert.That(settings.RepeatPercent, Is.EqualTo(source.PaintRepeatPercent));
            Assert.That(settings.GrowthPercentPerSecond, Is.EqualTo(source.PaintGrowthPercentPerSecond));
        }
    }
}
