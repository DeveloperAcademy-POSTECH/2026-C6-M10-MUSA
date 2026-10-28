using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>콤보 성공 추임새 (#271): 새 성공에서만 한 번, 실패·지난 결과·새 라운드 첫 스냅샷에서는 내지 않는다.</summary>
    public sealed class LabComboClearSoundTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        private LabComboClearSound Create()
        {
            host = new GameObject("combo-clear-sound");
            return host.AddComponent<LabComboClearSound>();
        }

        private static LabSnapshot Result(uint count, LabComboResult result, string session = "s", uint round = 1) =>
            new LabSnapshot { phase = LabPhase.Playing, sessionId = session, round = round, comboResultCount = count, comboResult = result };

        [Test]
        public void TheJotaClipIsInResources()
        {
            Assert.That(Resources.LoadAll<AudioClip>(LabComboClearSound.ClipFolder), Has.Length.EqualTo(1),
                "Resources/Audio/ComboClear/Jota.wav (염경애 추임새 '좋다', 국립국악원)");
        }

        [Test]
        public void OnlyANewSuccessPlaysOnce()
        {
            LabComboClearSound sound = Create();
            Assert.That(sound.Observe(Result(1, LabComboResult.Success)), Is.False, "A result from before joining stays quiet.");
            Assert.That(sound.Observe(Result(1, LabComboResult.Success)), Is.False);
            Assert.That(sound.Observe(Result(2, LabComboResult.Timeout)), Is.False, "No 좋다 on a timeout.");
            Assert.That(sound.Observe(Result(3, LabComboResult.WrongColor)), Is.False, "Nor on a wrong colour.");
            Assert.That(sound.Observe(Result(4, LabComboResult.Success)), Is.True, "CLEAR! plays it.");
            Assert.That(sound.Observe(Result(4, LabComboResult.Success)), Is.False, "Only once per result.");
            Assert.That(sound.PlayCount, Is.EqualTo(1));
            Assert.That(sound.FailPlayCount, Is.EqualTo(2), "A timeout and a wrong colour each play the fail sound once (#338).");
        }

        [Test]
        public void TheComboFailClipIsInResources()
        {
            Assert.That(Resources.LoadAll<AudioClip>(LabComboClearSound.FailClipFolder), Has.Length.EqualTo(1),
                "Resources/Audio/ComboFail/ComboFail.wav (#338)");
        }

        [Test]
        public void ANewRoundStartsCountingAgain()
        {
            LabComboClearSound sound = Create();
            sound.Observe(Result(2, LabComboResult.Success));
            Assert.That(sound.Observe(Result(0, LabComboResult.None, round: 2)), Is.False);
            Assert.That(sound.Observe(Result(1, LabComboResult.Success, round: 2)), Is.True);
            Assert.That(sound.Observe(new LabSnapshot { phase = LabPhase.Defeat, sessionId = "s", round = 2, comboResultCount = 2,
                comboResult = LabComboResult.Success }), Is.False, "Not after the battle is over.");
        }
    }
}
