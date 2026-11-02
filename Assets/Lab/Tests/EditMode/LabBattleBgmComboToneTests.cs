using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 야광귀 콤보 BIG/SMALL 동안 전투 BGM 높이: BIG 배지 동안 +2반음, SMALL 배지 동안 -2반음, 0.3초에 걸쳐 바뀐다.
    /// </summary>
    public sealed class LabBattleBgmComboToneTests
    {
        // 50초에 콤보 결과, 효과는 65초에 끝난다. 배지는 51.6초(결과 문구 1.6초 뒤)부터.
        private static LabSnapshot Result(LabComboResult result)
        {
            bool failed = result != LabComboResult.Success;
            return new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing,
                comboResultCount = 1, comboResult = result, comboResultAt = 50d,
                players = new[]
                {
                    new LabPlayerState { id = 1ul, comboPenaltyUntil = failed ? 65d : 0d, comboRewardUntil = failed ? 0d : 65d },
                    new LabPlayerState { id = 2ul, comboPenaltyUntil = failed ? 63d : 0d, comboRewardUntil = failed ? 0d : 63d }
                }
            };
        }

        [Test]
        public void BigRaisesAndSmallLowersTheBgmWhileTheBadgeIsUp()
        {
            LabSnapshot big = Result(LabComboResult.Success), small = Result(LabComboResult.Timeout);
            Assert.AreEqual(0f, LabBattleBgm.ComboToneSemitones(big, 51d, 2f, 2f), "Not before the badge.");
            Assert.AreEqual(2f, LabBattleBgm.ComboToneSemitones(big, 52d, 2f, 2f));
            Assert.AreEqual(2f, LabBattleBgm.ComboToneSemitones(big, 64.9d, 2f, 2f), "Until the last one ends.");
            Assert.AreEqual(0f, LabBattleBgm.ComboToneSemitones(big, 65d, 2f, 2f));
            Assert.AreEqual(-2f, LabBattleBgm.ComboToneSemitones(small, 52d, 2f, 2f));
            Assert.AreEqual(-2f, LabBattleBgm.ComboToneSemitones(Result(LabComboResult.WrongColor), 52d, 2f, 2f));
            Assert.AreEqual(0f, LabBattleBgm.ComboToneSemitones(new LabSnapshot { phase = LabPhase.Playing }, 52d, 2f, 2f),
                "No combo result yet.");
            big.phase = LabPhase.Victory;
            Assert.AreEqual(0f, LabBattleBgm.ComboToneSemitones(big, 52d, 2f, 2f), "Only during the battle.");
        }

        [Test]
        public void TheToneGlidesOverAThirdOfASecond()
        {
            var host = new GameObject("bgm-tone");
            try
            {
                LabBattleBgm bgm = host.AddComponent<LabBattleBgm>();
                LabSnapshot big = Result(LabComboResult.Success);
                bgm.DriveTone(big, 52d, .15f);
                Assert.AreEqual(1f, bgm.ToneSemitones, 1e-3f, "Halfway after 0.15 s.");
                bgm.DriveTone(big, 52d, .2f);
                Assert.AreEqual(2f, bgm.ToneSemitones, 1e-3f);
                bgm.DriveTone(Result(LabComboResult.Timeout), 52d, .15f);
                Assert.AreEqual(0f, bgm.ToneSemitones, 1e-3f, "BIG to SMALL also takes 0.3 s.");
                bgm.DriveTone(big, 70d, 1f);
                Assert.AreEqual(0f, bgm.ToneSemitones, 1e-3f, "Back to normal after the effect.");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
