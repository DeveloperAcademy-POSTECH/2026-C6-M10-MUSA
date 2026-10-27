using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>야광귀 셈법 콤보 (#271): 넷까지만 세는 수, DEV 토글, 콤보 슬라이더 가운데 기본값.</summary>
    public sealed class LabYaguangCountTests
    {
        [Test]
        public void TheYaguangCountsOnlyToFourThenStartsAgain()
        {
            Assert.That(Enumerable.Range(1, 6).Select(LabGourdComboHud.CountWord),
                Is.EqualTo(new[] { "하나", "둘", "셋", "넷", "하나", "둘" }), "Past four it starts again from one.");
            Assert.That(LabGourdComboHud.CountWord(0), Is.EqualTo("하나"));
            Assert.That(Enumerable.Range(1, 4).Reverse().Select(LabGourdComboHud.CountWord),
                Is.EqualTo(new[] { "넷", "셋", "둘", "하나" }),
                "The warning reads the remaining seconds in reverse, while the four slot labels stay ascending.");
        }

        [Test]
        public void TheCountingLookIsAnOnToggleUnderTheComboSwitch()
        {
            Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(228));
            LabDeveloperField field = LabConfig.DeveloperFields[191];
            Assert.That(field.Label, Is.EqualTo("야광귀 셈법 (0 off, 1 on)"));
            Assert.That(field.DisplayLabel, Is.EqualTo("야광귀 셈법"));
            Assert.That(new[] { field.OffText, field.OnText }, Is.EqualTo(new[] { "끔", "켬" }));
            int[] group = LabConfig.DeveloperCategories.Single(category => category.Title == "야광귀").FieldIndices;
            Assert.That(group.Take(2), Is.EqualTo(new[] { 155, 191 }));

            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                LabConfig authored = Resources.Load<LabConfig>("LabConfig");
                Assert.That(authored, Is.Not.Null);
                foreach (LabConfig source in new[] { config, authored })
                {
                    Assert.That(source.ComboYaguangCountEnabled, Is.True, "On by default (it only shows when the combo is on).");
                    Assert.That(source.CaptureDeveloperValues()[191], Is.EqualTo(1f));
                    Assert.That(source.ComboWarningSeconds, Is.EqualTo(4f), "One count a second: 하나, 둘, 셋, 넷.");
                }
                float[] values = config.CaptureDeveloperValues();
                var original = (float[])values.Clone();
                values[191] = 0f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.ComboYaguangCountEnabled, Is.False);
                Assert.That(config.CaptureDeveloperValues().Take(191), Is.EqualTo(original.Take(191)),
                    "The appended count-look toggle cannot change Jigwi, flash or the underlying combo rules.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EveryComboSliderStartsInTheMiddle()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                foreach (int index in new[] { 156, 157, 158, 159, 160, 162, 163, 164 })
                {
                    LabDeveloperField field = LabConfig.DeveloperFields[index];
                    Assert.That((field.Min + field.Max) * .5f, Is.EqualTo(values[index]).Within(1e-5f),
                        field.Label + " starts in the middle of its slider.");
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
