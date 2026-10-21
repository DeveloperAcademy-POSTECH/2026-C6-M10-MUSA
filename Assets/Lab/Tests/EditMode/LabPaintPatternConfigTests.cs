using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabPaintPatternConfigTests
    {
        private LabConfig source;
        [SetUp] public void Create() => source = ScriptableObject.CreateInstance<LabConfig>();
        [TearDown] public void Dispose() => Object.DestroyImmediate(source);

        [Serializable] private sealed class Shared { public float[] values; }
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        [Test]
        public void BothAuthoredAndFreshConfigDefaultToSplatterWithFourNamedChoices()
        {
            Assert.That(source.PaintPattern, Is.EqualTo(LabPaintPattern.Splatter));
            Assert.That(source.PaintGrowthSettings.Pattern, Is.EqualTo(LabPaintPattern.Splatter));
            Assert.That(source.CaptureDeveloperValues()[139], Is.EqualTo(2f));
            LabConfig authored = Resources.Load<LabConfig>("LabConfig");
            Assert.That(authored, Is.Not.Null);
            Assert.That(authored.PaintPattern, Is.EqualTo(LabPaintPattern.Splatter));
            LabDeveloperField field = LabConfig.DeveloperFields[139];
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(169));
            Assert.That(field.Label, Is.EqualTo("Paint Pattern"));
            Assert.That(field.IsChoice, Is.True);
            Assert.That(field.IsToggle, Is.False);
            Assert.That(field.WholeNumber, Is.True);
            Assert.That(field.Choices, Is.EqualTo(new[] { "LEGACY", "BROAD", "SPLATTER", "FLOW" }));
            Assert.That(new[] { field.Min, field.Max }, Is.EqualTo(new[] { 0f, 3f }));
            int[] gourd = LabConfig.DeveloperCategories.Single(category => category.Title == "Gourd").FieldIndices;
            Assert.That(gourd, Does.Contain(139));
            Assert.That(gourd.Distinct().Count(), Is.EqualTo(gourd.Length));
            Assert.That(Protocol, Is.EqualTo(5647));
        }

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void AllFourChoicesRoundTripAndAcknowledgeWithoutRenumberingPreviousFields(LabPaintPattern pattern)
        {
            float[] before = source.CaptureDeveloperValues();
            float[] requested = (float[])before.Clone(); requested[139] = (int)pattern;
            Assert.That(source.TryApplyDeveloperValues(requested, out string error), Is.True, error);
            Assert.That(source.PaintPattern, Is.EqualTo(pattern));
            Assert.That(source.PaintGrowthSettings.Pattern, Is.EqualTo(pattern));
            Assert.That(source.CaptureDeveloperValues().Take(139), Is.EqualTo(before.Take(139)));
            string wire = JsonUtility.ToJson(new Shared { values = source.CaptureDeveloperValues() });
            float[] received = JsonUtility.FromJson<Shared>(wire).values;
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.PaintPattern, Is.EqualTo(pattern));
                Assert.That(LabSessionSettings.Fingerprint(received, 3, Protocol),
                    Is.EqualTo(LabSessionSettings.Fingerprint(requested, 3, Protocol)));
                float[] changed = (float[])received.Clone(); changed[139] = ((int)pattern + 1) % 4;
                Assert.That(LabSessionSettings.Fingerprint(changed, 3, Protocol),
                    Is.Not.EqualTo(LabSessionSettings.Fingerprint(received, 3, Protocol)));
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), client);
                Assert.That(client.PaintPattern, Is.EqualTo(pattern));
            }
            finally { Object.DestroyImmediate(client); }
        }

        [TestCase(-1f)]
        [TestCase(.5f)]
        [TestCase(4f)]
        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPatternRejectsTheEntireFormBeforeApplyingAnyEarlierValue(float invalid)
        {
            float[] before = source.CaptureDeveloperValues();
            float[] requested = (float[])before.Clone(); requested[0] = 777f; requested[129] = 1f; requested[139] = invalid;
            Assert.That(source.TryApplyDeveloperValues(requested, out string error), Is.False);
            Assert.That(error, Does.Contain("Paint Pattern"));
            Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(before));
        }

        [TestCase(139)]
        [TestCase(141)]
        public void IncompatibleSchemaCannotPartiallyApplyOldOrUnknownPatternData(int count)
        {
            float[] before = source.CaptureDeveloperValues(), invalid = new float[count];
            Array.Copy(before, invalid, Math.Min(before.Length, count)); invalid[0] = 777f;
            Assert.That(source.TryApplyDeveloperValues(invalid, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(source.CaptureDeveloperValues(), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(invalid, 3, Protocol));
        }

        [Test]
        public void InvalidInspectorPatternFallsBackToSplatterWithoutMutatingCapturedRoundSettings()
        {
            float[] values = source.CaptureDeveloperValues(); values[139] = 1f;
            source.TryApplyDeveloperValues(values, out _);
            LabPaintGrowthSettings previous = source.PaintGrowthSettings;
            JsonUtility.FromJsonOverwrite("{\"paintPattern\":99}", source);
            Assert.That(source.PaintPattern, Is.EqualTo(LabPaintPattern.Splatter));
            Assert.That(source.CaptureDeveloperValues()[139], Is.EqualTo(2f));
            Assert.That(previous.Pattern, Is.EqualTo(LabPaintPattern.Broad));
        }

        [TestCase(LabOrbElement.Fire, 216, 87, 54)]
        [TestCase(LabOrbElement.Water, 20, 113, 167)]
        [TestCase(LabOrbElement.Wood, 61, 130, 88)]
        [TestCase(LabOrbElement.Metal, 248, 183, 42)]
        [TestCase(LabOrbElement.Earth, 111, 74, 50)]
        public void ApprovedPaintPalettePreservesElementIdentityAndConvertsSrgbToLinear(
            LabOrbElement element, int red, int green, int blue)
        {
            var expected = new Color32((byte)red, (byte)green, (byte)blue, 255);
            Assert.That(LabPaintPalette.SrgbColorFor(element), Is.EqualTo(expected));
            Assert.That(Vector4.Distance(LabPaintPalette.ColorFor(element), ((Color)expected).linear), Is.LessThan(.000001f));
            Assert.That(LabPaintPalette.ColorFor(LabOrbElement.None), Is.EqualTo(Color.clear));
        }
    }
}
