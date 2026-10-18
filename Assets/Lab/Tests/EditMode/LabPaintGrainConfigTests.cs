using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Shared visual tuning only: these tests do not claim rendered or connected results.</summary>
    public sealed class LabPaintGrainConfigTests
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
            values[111] = 1f;
            values[112] = 1f;
            values[119] = .75f;
            values[128] = 3f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> Fields()
        {
            yield return new TestCaseData(129, "PaintGrainEnabled", 0f, 1f);
            yield return new TestCaseData(130, "PaintGrainSize", .25f, 4f);
            yield return new TestCaseData(131, "PaintGrainStrength", 0f, 1f);
            yield return new TestCaseData(132, "PaintMottleStrength", 0f, 1f);
        }

        [Test]
        public void FourVisualSettingsAppendDisabledByDefaultWithoutReplacingPreviousFields()
        {
            Assert.That(baseline.Length, Is.EqualTo(141));
            Assert.That(baseline.Skip(129).Take(4), Is.EqualTo(new[] { 0f, 1f, .3f, .18f }));
            Assert.That(config.PaintGrainEnabled, Is.False);
            Assert.That(LabConfig.DeveloperFields.Skip(129).Take(4).Select(field => field.Label), Is.EqualTo(new[]
            {
                "Paint Grain (0 off, 1 on)", "Paint Grain Size", "Paint Grain Strength", "Paint Mottle Strength"
            }));
            Assert.That(LabConfig.DeveloperFields[129].IsToggle, Is.True);
            Assert.That(LabConfig.DeveloperFields.Skip(130).Take(3).All(field => !field.IsToggle && !field.WholeNumber), Is.True);
            var category = LabConfig.DeveloperCategories.Single(item => item.Title == "Gourd");
            Assert.That(category.FieldIndices.Where(index => index >= 129 && index <= 132), Is.EqualTo(new[] { 129, 130, 131, 132 }));
        }

        [TestCaseSource(nameof(Fields))]
        public void EndpointsRoundTripThroughConfigJsonAndSharedFingerprintPreservingAll129PreviousValues(
            int index, string propertyName, float minimum, float maximum)
        {
            foreach (float endpoint in new[] { minimum, maximum })
            {
                float[] values = (float[])baseline.Clone();
                values[index] = endpoint;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                object property = typeof(LabConfig).GetProperty(propertyName)?.GetValue(config);
                Assert.That(property, Is.Not.Null, propertyName);
                if (index == 129) Assert.That((bool)property, Is.EqualTo(endpoint == 1f));
                else Assert.That((float)property, Is.EqualTo(endpoint));
                Assert.That(config.CaptureDeveloperValues().Take(129), Is.EqualTo(baseline.Take(129)));
                string json = JsonUtility.ToJson(new SharedTuning { values = config.CaptureDeveloperValues() });
                float[] received = JsonUtility.FromJson<SharedTuning>(json).values;
                var client = ScriptableObject.CreateInstance<LabConfig>();
                try
                {
                    Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                    Assert.That(client.CaptureDeveloperValues(), Is.EqualTo(values));
                    Assert.That(LabSessionSettings.Fingerprint(received, 3, Protocol),
                        Is.EqualTo(LabSessionSettings.Fingerprint(values, 3, Protocol)));
                    float[] different = (float[])values.Clone();
                    different[index] = endpoint == minimum ? maximum : minimum;
                    Assert.That(LabSessionSettings.Fingerprint(different, 3, Protocol),
                        Is.Not.EqualTo(LabSessionSettings.Fingerprint(values, 3, Protocol)));
                }
                finally { Object.DestroyImmediate(client); }
            }
        }

        [TestCaseSource(nameof(Fields))]
        public void InvalidLateFieldRejectsTheEntireFormWithoutChangingAnyApprovedValue(
            int index, string propertyName, float minimum, float maximum)
        {
            var invalids = new List<float> { minimum - .25f, maximum + .25f, float.NaN,
                float.PositiveInfinity, float.NegativeInfinity };
            if (index == 129) invalids.Add(.5f);
            foreach (float invalid in invalids)
            {
                float[] values = (float[])baseline.Clone();
                values[0] = 777f;
                values[111] = 0f;
                values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False, propertyName);
                Assert.That(error, Does.Contain(LabConfig.DeveloperFields[index].Label));
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            }
        }

        [TestCase(129)]
        [TestCase(132)]
        [TestCase(134)]
        public void OldOrIncompleteSharedSchemaIsRejectedBeforeApplyingAnyValue(int count)
        {
            var values = new float[count];
            Array.Copy(baseline, values, Math.Min(count, baseline.Length));
            values[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(values, 3, Protocol));
            Assert.That(Protocol, Is.EqualTo(5635));
        }

        [Test]
        public void MalformedInspectorValuesHaveFiniteBoundedEffectiveValues()
        {
            JsonUtility.FromJsonOverwrite("{\"paintGrainEnabled\":true,\"paintGrainSize\":-4," +
                "\"paintGrainStrength\":7,\"paintMottleStrength\":-2}", config);
            Assert.That(config.PaintGrainEnabled, Is.True);
            Assert.That(config.PaintGrainSize, Is.EqualTo(.25f));
            Assert.That(config.PaintGrainStrength, Is.EqualTo(1f));
            Assert.That(config.PaintMottleStrength, Is.Zero);
            JsonUtility.FromJsonOverwrite("{\"paintGrainSize\":8}", config);
            Assert.That(config.PaintGrainSize, Is.EqualTo(4f));
        }
    }
}
