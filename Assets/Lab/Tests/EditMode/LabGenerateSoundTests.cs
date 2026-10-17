using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabGenerateSoundTests
    {
        [Test]
        public void EveryThirdGenerateIsTheEolssuVoice()
        {
            var turns = new bool[9];
            for (int i = 0; i < turns.Length; i++) turns[i] = LabGenerateSound.IsVoiceTurn(i, 3);
            Assert.That(turns, Is.EqualTo(new[] { false, false, true, false, false, true, false, false, true }),
                "Janggu, janggu, eolssu, and again.");
            Assert.That(LabGenerateSound.IsVoiceTurn(0, 1), Is.True, "Every 1 means always the voice.");
            Assert.That(LabGenerateSound.IsVoiceTurn(2, 0), Is.False, "Zero turns the voice off.");
        }
    }
}
