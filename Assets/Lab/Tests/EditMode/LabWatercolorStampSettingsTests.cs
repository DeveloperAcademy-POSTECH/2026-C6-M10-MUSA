using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabWatercolorStampSettingsTests
    {
        static readonly string[] AddedFields = {
            "pigmentContrast", "pigmentSaturation", "granulationStrength", "granulationScale", "rimBreakup"
        };
        static FieldInfo Pin => typeof(LabWatercolorStampSettings).GetField("pinnedBrushVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        static IEnumerable<FieldInfo> NumericFields => typeof(LabWatercolorStampSettings).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(float));

        [Test]
        public void V2DefaultsRepresentTheApprovedAppearanceWithoutAddingSharedGameParameters()
        {
            var value = new LabWatercolorStampSettings();
            Assert.That(LabWatercolorStampSettings.StyleVersion, Is.EqualTo("watercolor-stamps-v2"));
            Assert.That(value.enabled, Is.True);
            Assert.That(value.density, Is.EqualTo(.9f));
            Assert.That(value.overlap, Is.EqualTo(.78f));
            Assert.That(value.edgeStrength, Is.EqualTo(.55f));
            Assert.That(value.edgeWidth, Is.EqualTo(.025f));
            Assert.That(value.pigmentContrast, Is.EqualTo(1.6f));
            Assert.That(value.pigmentSaturation, Is.EqualTo(1.15f));
            Assert.That(value.granulationStrength, Is.EqualTo(.25f));
            Assert.That(value.granulationScale, Is.EqualTo(120f));
            Assert.That(value.rimBreakup, Is.EqualTo(.45f));
            Assert.That(value.revealSeconds, Is.EqualTo(.35f));
            Assert.DoesNotThrow(value.Validate);
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(265), "V2 settings remain in their presentation profile; only the new clay controls append to the current shared schema.");
        }

        [Test]
        public void EveryAppearanceValueChangesTheRoundFingerprintAndSurvivesCopiesAndSerialization()
        {
            var original = new LabWatercolorStampSettings();
            foreach (FieldInfo field in NumericFields)
            {
                var changed = new LabWatercolorStampSettings();
                UnityEngine.RangeAttribute range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
                Assert.That(range, Is.Not.Null, field.Name);
                field.SetValue(changed, range.max);
                Assert.That(changed.Fingerprint, Is.Not.EqualTo(original.Fingerprint), field.Name + " must participate in the version handshake.");
                var copy = changed.Clone();
                var serialized = JsonUtility.FromJson<LabWatercolorStampSettings>(JsonUtility.ToJson(changed));
                Assert.That((float)field.GetValue(copy), Is.EqualTo(range.max), field.Name);
                Assert.That((float)field.GetValue(serialized), Is.EqualTo(range.max), field.Name);
                Assert.That(copy.Fingerprint, Is.EqualTo(changed.Fingerprint), field.Name);
                Assert.That(serialized.Fingerprint, Is.EqualTo(changed.Fingerprint), field.Name);
            }
            original.enabled = false;
            Assert.That(original.Fingerprint, Is.Not.EqualTo(new LabWatercolorStampSettings().Fingerprint));
        }

        [Test]
        public void NonFiniteValuesUseDefaultsAndFiniteOutOfRangeValuesClampWithoutMutatingTheSource()
        {
            var defaults = new LabWatercolorStampSettings();
            foreach (FieldInfo field in NumericFields)
            {
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    var value = new LabWatercolorStampSettings(); field.SetValue(value, invalid);
                    Assert.Throws<ArgumentOutOfRangeException>(value.Validate, field.Name);
                    var copy = value.SanitizedCopy();
                    Assert.That((float)field.GetValue(copy), Is.EqualTo((float)field.GetValue(defaults)), field.Name);
                    Assert.DoesNotThrow(copy.Validate);
                    Assert.That((float)field.GetValue(value), Is.EqualTo(invalid), "Sanitization cannot edit authoring input.");
                }
                UnityEngine.RangeAttribute range = field.GetCustomAttribute<UnityEngine.RangeAttribute>();
                var low = new LabWatercolorStampSettings(); field.SetValue(low, range.min - 1f);
                var high = new LabWatercolorStampSettings(); field.SetValue(high, range.max + 1f);
                Assert.That((float)field.GetValue(low.SanitizedCopy()), Is.EqualTo(range.min), field.Name);
                Assert.That((float)field.GetValue(high.SanitizedCopy()), Is.EqualTo(range.max), field.Name);
            }
        }

        [Test]
        public void CapturingARoundPinsOnlyItsCopyAndLaterAuthoringChangesCannotChangeTheCapturedIdentity()
        {
            var authoring = ScriptableObject.CreateInstance<LabWatercolorStampProfile>();
            try
            {
                Assert.That(Pin.GetValue(authoring.settings), Is.Null);
                var round = authoring.settings.Clone();
                string identity = round.Version;
                Assert.That(Pin.GetValue(authoring.settings), Is.Null, "Reading a round cannot pin or dirty the saved authoring asset.");
                Assert.That(round.PinnedBrushVersion, Is.EqualTo(LabWatercolorStampSettings.BakedBrushVersion));
                foreach (string name in AddedFields)
                {
                    FieldInfo field = typeof(LabWatercolorStampSettings).GetField(name);
                    field.SetValue(authoring.settings, field.GetCustomAttribute<UnityEngine.RangeAttribute>().max);
                }
                Assert.That(round.Version, Is.EqualTo(identity));
                Assert.That(round.Clone().Version, Is.EqualTo(identity));
                Assert.That(round.SanitizedCopy().Version, Is.EqualTo(identity));
                Assert.That(authoring.settings.Fingerprint, Is.Not.EqualTo(round.Fingerprint));
                Pin.SetValue(round, new string('b', 64));
                Assert.That(round.Clone().PinnedBrushVersion, Is.EqualTo(new string('b', 64)));
                Assert.That(round.SanitizedCopy().PinnedBrushVersion, Is.EqualTo(new string('b', 64)));
            }
            finally { Object.DestroyImmediate(authoring); }
        }
    }
}
