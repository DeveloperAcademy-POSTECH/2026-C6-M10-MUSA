using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 콤보 히트 효과음 (#338): 같은 콤보에서 맞힌 칸이 늘 때마다 한 번, 칸이 오를수록 높게.
    /// 접속 직후 이미 맞힌 칸·새 라운드 첫 스냅샷·전투가 끝난 뒤에는 내지 않는다.
    /// </summary>
    public sealed class LabComboHitSoundTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        private LabComboHitSound Create()
        {
            host = new GameObject("combo-hit-sound");
            return host.AddComponent<LabComboHitSound>();
        }

        private static LabSnapshot Combo(uint count, int progress, string session = "s", uint round = 1,
            LabPhase phase = LabPhase.Playing) =>
            new LabSnapshot { phase = phase, sessionId = session, round = round, comboCount = count, comboProgress = progress };

        [Test]
        public void TheComboHitClipIsInResources()
        {
            Assert.That(Resources.LoadAll<AudioClip>(LabComboHitSound.ClipFolder), Has.Length.EqualTo(1),
                "Resources/Audio/ComboHit/ComboHit.wav, one note the code raises step by step");
        }

        [Test]
        public void EachNewStepPlaysOnceAndClimbs()
        {
            LabComboHitSound sound = Create();
            Assert.That(sound.Observe(Combo(1, 1)), Is.Zero, "A step from before joining stays quiet.");
            Assert.That(sound.Observe(Combo(1, 1)), Is.Zero);
            Assert.That(sound.Observe(Combo(1, 2)), Is.EqualTo(2));
            Assert.That(sound.Observe(Combo(1, 2)), Is.Zero, "Only once per step.");
            Assert.That(sound.Observe(Combo(1, 4)), Is.EqualTo(4), "Two steps between snapshots play the last one.");
            Assert.That(sound.PlayCount, Is.EqualTo(2));

            Assert.That(sound.Observe(Combo(2, 0)), Is.Zero, "A new combo starts again.");
            Assert.That(sound.Observe(Combo(2, 1)), Is.EqualTo(1));
            Assert.That(sound.Observe(Combo(3, 1)), Is.EqualTo(1), "A new combo first seen after its first hit plays it.");
        }

        [Test]
        public void ANewRoundOrTheEndOfBattleStaysQuiet()
        {
            LabComboHitSound sound = Create();
            sound.Observe(Combo(2, 0));
            Assert.That(sound.Observe(Combo(1, 1, round: 2)), Is.Zero, "The first snapshot of a new round only sets the count.");
            Assert.That(sound.Observe(Combo(1, 2, round: 2)), Is.EqualTo(2));
            Assert.That(sound.Observe(Combo(1, 3, round: 2, phase: LabPhase.Victory)), Is.Zero, "Not after the battle is over.");
            Assert.That(sound.Observe(Combo(1, 3, session: "t")), Is.Zero, "A new session starts again.");
        }

        [Test]
        public void HigherStepsPlayHigherByTheInspectorSemitones()
        {
            LabComboHitSound sound = Create();
            Assert.That(sound.PitchFor(1), Is.EqualTo(1f).Within(1e-5f), "The first step is the clip as recorded (do).");
            Assert.That(sound.PitchFor(2), Is.EqualTo(Mathf.Pow(2f, 2f / 12f)).Within(1e-5f), "re");
            Assert.That(sound.PitchFor(3), Is.EqualTo(Mathf.Pow(2f, 4f / 12f)).Within(1e-5f), "mi");
            Assert.That(sound.PitchFor(4), Is.EqualTo(Mathf.Pow(2f, 7f / 12f)).Within(1e-5f), "sol");
            for (int step = 2; step <= LabGourdCombo.Length; step++)
                Assert.That(sound.PitchFor(step), Is.GreaterThan(sound.PitchFor(step - 1)));
            sound.stepSemitones = new[] { 0f, 12f };
            Assert.That(sound.PitchFor(2), Is.EqualTo(2f).Within(1e-5f), "An octave doubles the pitch.");
            Assert.That(sound.PitchFor(4), Is.EqualTo(1f).Within(1e-5f), "A missing step stays at the recorded pitch.");
        }
    }
}
