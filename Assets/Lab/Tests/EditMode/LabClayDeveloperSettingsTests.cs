using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabClayDeveloperSettingsTests
    {
        [Test]
        public void ClayFieldsAppendWithoutMovingExistingSlotsAndRoundTripIndependently()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] before = config.CaptureDeveloperValues();
                Assert.That(before.Length, Is.EqualTo(261));
                Assert.That(LabConfig.ClayThicknessField, Is.EqualTo(257));
                Assert.That(LabConfig.ClayGlossField, Is.EqualTo(258));
                Assert.That(config.ClayThickness, Is.EqualTo(.006f));
                Assert.That(config.ClayGloss, Is.EqualTo(.25f));
                var changed = (float[])before.Clone();
                changed[257] = .02f; changed[258] = 0;
                Assert.That(config.TryApplyDeveloperValues(changed, out string error), Is.True, error);
                Assert.That(config.ClayThickness, Is.EqualTo(.02f));
                Assert.That(config.ClayGloss, Is.EqualTo(0));
                Assert.That(config.CaptureDeveloperValues().Take(257), Is.EqualTo(before.Take(257)));
                changed[257] = 0; changed[258] = 1;
                Assert.That(config.TryApplyDeveloperValues(changed, out error), Is.True, error);
                Assert.That(config.ClayThickness, Is.EqualTo(0));
                Assert.That(config.ClayGloss, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(257, float.NaN)] [TestCase(257, float.PositiveInfinity)]
        [TestCase(257, -.001f)] [TestCase(257, .021f)]
        [TestCase(258, float.NaN)] [TestCase(258, -.01f)] [TestCase(258, 1.01f)]
        public void InvalidClaySettingsRejectTheWholeForm(int index, float invalid)
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] before = config.CaptureDeveloperValues(), changed = (float[])before.Clone();
                changed[index] = invalid;
                Assert.That(config.TryApplyDeveloperValues(changed, out _), Is.False);
                Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PresentationStateRejectsRepeatedOrInvalidValuesAndIsScopedToRoundAndConfig()
        {
            var state = new LabClayPresentationState();
            Assert.That(state.TryApply("room", 1, 2, "settings", 3, .01f, .8f), Is.True);
            Assert.That(state.TryApply("room", 1, 2, "settings", 3, .02f, 0), Is.False);
            Assert.That(state.TryApply("room", 1, 2, "settings", 2, .02f, 0), Is.False);
            Assert.That(state.TryApply("room", 1, 2, "settings", 4, float.NaN, 0), Is.False);
            Assert.That(state.Thickness, Is.EqualTo(.01f));
            Assert.That(state.Gloss, Is.EqualTo(.8f));
            Assert.That(state.Matches("room", 1, 2, "settings"), Is.True);
            Assert.That(state.Matches("room", 2, 2, "settings"), Is.False);
            Assert.That(state.Matches("other-room", 1, 2, "settings"), Is.False);
            Assert.That(state.Matches("room", 1, 3, "settings"), Is.False);
            Assert.That(state.Matches("room", 1, 2, "other-settings"), Is.False);
        }

        [Test]
        public void NewSlidersExposeBothEndpointsIndependently()
        {
            foreach (int i in new[] { LabConfig.ClayThicknessField, LabConfig.ClayGlossField })
            {
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                float baseline = i == LabConfig.ClayThicknessField ? .006f : .25f;
                Assert.That(field.WholeNumber, Is.False);
                Assert.That(LabDeveloperSliderScale.Denormalize(field, baseline, 0), Is.EqualTo(field.Min));
                Assert.That(LabDeveloperSliderScale.Denormalize(field, baseline, 1), Is.EqualTo(field.Max));
            }
        }
    }
}
