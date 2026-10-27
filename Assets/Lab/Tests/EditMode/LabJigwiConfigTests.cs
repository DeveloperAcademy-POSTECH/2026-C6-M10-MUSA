using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabJigwiConfigTests
    {
        [Test]
        public void JigwiFieldsAppendSixRowsWithoutMovingTheExistingSabotageSchema()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(228), "The yaguang flash (#268) follows at 184-190 and the counting toggle (#271) at 191.");
                Assert.That(LabConfig.DeveloperFields[177].Label, Is.EqualTo("Charm Rescue Taps per Neighbor (0 = none)"));
                Assert.That(LabConfig.DeveloperFields.Skip(178).Take(6).Select(field => field.Label), Is.EqualTo(new[]
                {
                    "Jigwi Fire Target Count", "Jigwi Fire Seconds", "Jigwi Overheat Seconds",
                    "Jigwi Generation Burn Seconds", "Jigwi Raw Radius Scale", "Jigwi Raw Life Scale"
                }));
                Assert.That(LabConfig.DeveloperCategories.Single(category => category.Title == "Jigwi").FieldIndices,
                    Is.EqualTo(Enumerable.Range(178, 6).Concat(Enumerable.Range(192, 4))));
                Assert.That(config.CaptureDeveloperValues().Skip(178).Take(6), Is.EqualTo(new[] { 1f, 15f, 4f, 10f, .65f, .5f }));
                Assert.That(config.GimmickChances().Length, Is.EqualTo(4),
                    "The manual Jigwi experiment must not silently enter the legacy sabotage lottery.");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void JigwiSettingsRoundTripThroughDeveloperFormJsonAndASeparateClientConfig()
        {
            var host = ScriptableObject.CreateInstance<LabConfig>();
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                var values = host.CaptureDeveloperValues();
                var tail = new[] { 5f, 30f, 7f, 20f, .8f, .25f };
                Array.Copy(tail, 0, values, 178, tail.Length);
                Assert.That(host.TryApplyDeveloperValues(values, out var error), Is.True, error);
                string json = JsonUtility.ToJson(new Settings { values = host.CaptureDeveloperValues() });
                var received = JsonUtility.FromJson<Settings>(json).values;
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.JigwiFireTargetCount, Is.EqualTo(5));
                Assert.That(new[] { client.JigwiFireSeconds, client.JigwiOverheatSeconds,
                    client.JigwiGenerationBurnSeconds, client.JigwiRawRadiusScale, client.JigwiRawLifeScale },
                    Is.EqualTo(tail.Skip(1)));
                Assert.That(LabSessionSettings.Fingerprint(values, 2, 5671),
                    Is.EqualTo(LabSessionSettings.Fingerprint(received, 2, 5671)));
                values[183] = .75f;
                Assert.That(LabSessionSettings.Fingerprint(values, 2, 5671),
                    Is.Not.EqualTo(LabSessionSettings.Fingerprint(received, 2, 5671)));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(client); }
        }

        [Test]
        public void JigwiAndAppendedFlashSettingsRoundTripIndependentlyInTheSameSharedSchema()
        {
            var host = ScriptableObject.CreateInstance<LabConfig>();
            var client = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                var values = host.CaptureDeveloperValues();
                var jigwi = new[] { 5f, 30f, 7f, 20f, .8f, .25f };
                var flash = new[] { 1f, 1f, 6f, 24f, .5f, 2f, 1f };
                Array.Copy(jigwi, 0, values, 178, jigwi.Length);
                Array.Copy(flash, 0, values, 184, flash.Length);
                Assert.That(host.TryApplyDeveloperValues(values, out var error), Is.True, error);
                var received = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(
                    new Settings { values = host.CaptureDeveloperValues() })).values;
                Assert.That(client.TryApplyDeveloperValues(received, out error), Is.True, error);
                Assert.That(client.CaptureDeveloperValues().Skip(178).Take(6), Is.EqualTo(jigwi));
                Assert.That(client.CaptureDeveloperValues().Skip(184).Take(7), Is.EqualTo(flash));
                Assert.That(client.JigwiFireTargetCount, Is.EqualTo(5));
                Assert.That(client.YaguangFlashEnabled && client.YaguangFlashTargetOne, Is.True);
                Assert.That(client.YaguangFlashFirstStartSeconds, Is.EqualTo(6f));
                Assert.That(client.ComboPenaltyDurationSeconds, Is.EqualTo(15f));
                Assert.That(client.ComboRewardDurationSeconds, Is.EqualTo(15f));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(client); }
        }

        [TestCase(178, 0f)]
        [TestCase(178, 6f)]
        [TestCase(178, 1.5f)]
        [TestCase(179, 0f)]
        [TestCase(180, 0f)]
        [TestCase(181, 0f)]
        [TestCase(182, .1f)]
        [TestCase(183, 0f)]
        [TestCase(183, float.NaN)]
        public void InvalidJigwiFieldRejectsAllChangesAtomically(int index, float invalid)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                var original = config.CaptureDeveloperValues();
                var values = (float[])original.Clone();
                values[0] = 777;
                values[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(original));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Serializable]
        private sealed class Settings { public float[] values; }
    }
}
