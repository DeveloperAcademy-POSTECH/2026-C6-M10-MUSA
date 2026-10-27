using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabGenerateSoundTests
    {
        [Test]
        public void ChuimsaeNeverPlaysInsideTheMinimumGap()
        {
            for (int since = 0; since < 5; since++)
                Assert.That(LabGenerateSound.ShouldPlayVoice(since, 5, 100, 0f), Is.False,
                    "Within the gap only the janggu plays, even at 100%.");
            Assert.That(LabGenerateSound.ShouldPlayVoice(5, 5, 100, 0f), Is.True, "Right after the gap it may play.");
        }

        [Test]
        public void ChuimsaeFollowsTheChanceAfterTheGap()
        {
            Assert.That(LabGenerateSound.ShouldPlayVoice(10, 5, 15, .14f), Is.True, "Roll under 15% plays the voice.");
            Assert.That(LabGenerateSound.ShouldPlayVoice(10, 5, 15, .15f), Is.False, "Roll at or above 15% stays janggu.");
            Assert.That(LabGenerateSound.ShouldPlayVoice(10, 5, 0, 0f), Is.False, "Zero percent turns the voice off.");
            Assert.That(LabGenerateSound.ShouldPlayVoice(0, 0, 100, .99f), Is.True, "No gap and 100% means always.");
        }

        [Test]
        public void ChuimsaeDefaultsAreSharedThroughDeveloperSettings()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = config.CaptureDeveloperValues();
                Assert.That(values.Length, Is.EqualTo(205));
                Assert.That(new[] { values[135], values[136] }, Is.EqualTo(new[] { 15f, 5f }));
                Assert.That(LabConfig.DeveloperFields[135].Label, Is.EqualTo("Chuimsae Chance (%)"));
                Assert.That(LabConfig.DeveloperFields[136].Label, Is.EqualTo("Chuimsae Min Gap (generates)"));
                values[135] = 30f;
                values[136] = 2f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                Assert.That(config.ChuimsaeChancePercent, Is.EqualTo(30));
                Assert.That(config.ChuimsaeMinGap, Is.EqualTo(2));
                values[136] = 2.5f;
                Assert.That(config.TryApplyDeveloperValues(values, out _), Is.False, "Gap must be a whole number.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
