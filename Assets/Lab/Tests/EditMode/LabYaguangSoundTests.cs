using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>
    /// 야광귀 섬광 예고와 번쩍 효과음 (#395): 예고 시작은 모든 기기, 눈 빛남/3·2·1/번쩍은 맞는 사람 기기에서 한 번씩.
    /// 늦게 알아챈 순간과 일시정지 중에는 내지 않는다. 소리 파일이 없어도 알아챈 횟수는 센다.
    /// </summary>
    public sealed class LabYaguangSoundTests
    {
        private const ulong Me = 7ul, Other = 9ul;
        private readonly System.Collections.Generic.List<GameObject> hosts = new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts) if (host != null) Object.DestroyImmediate(host);
            hosts.Clear();
        }

        private LabYaguangSound Sound()
        {
            var host = new GameObject("yaguang-sound");
            hosts.Add(host);
            return host.AddComponent<LabYaguangSound>();
        }

        // 10초에 예고가 시작해 13초에 번쩍이는 섬광 (예고 3초). 눈 빛은 30% 지점인 10.9초부터.
        private static LabSnapshot Flash(ulong target)
        {
            return new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing,
                flashWarningAt = 10d, flashAt = 13d, flashTarget = target, flashTargetAll = false
            };
        }

        [Test]
        public void TheTargetHearsTheWholeWarningOnce()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            sound.ObserveFlash(snapshot, 9.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero, "Nothing before the warning.");

            sound.ObserveFlash(snapshot, 10d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(1), "3");
            Assert.That(sound.LastCount, Is.EqualTo(3));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero, "The eyes are not glowing yet.");

            sound.ObserveFlash(snapshot, 10.95d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.EqualTo(1));
            sound.ObserveFlash(snapshot, 11d, Me);
            sound.ObserveFlash(snapshot, 11.5d, Me);
            sound.ObserveFlash(snapshot, 12d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(3), "3, 2, 1 once each.");
            Assert.That(sound.LastCount, Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.EqualTo(1));

            // 13초에 번쩍: 섬광 수가 늘고 내가 맞았다.
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Me;
            snapshot.flashWarningAt = snapshot.flashAt = 73d;
            sound.ObserveFlash(snapshot, 13.1d, Me);
            sound.ObserveFlash(snapshot, 13.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashBurst), Is.EqualTo(1), "Once per flash.");
        }

        [Test]
        public void OthersHearOnlyTheWarningStart()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Other);
            for (double now = 10d; now < 13d; now += .25d) sound.ObserveFlash(snapshot, now, Me);
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Other;
            sound.ObserveFlash(snapshot, 13.1d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.EqualTo(1), "Everyone sees the eyes.");
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashBurst), Is.Zero);
        }

        [Test]
        public void LateOrPausedMomentsStayQuiet()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            // 11.6초에 처음 봄: 예고 시작(10초), 눈 빛(10.9초), 숫자 2(11초)는 모두 0.5초 넘게 지났다.
            sound.ObserveFlash(snapshot, 11.6d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashGlow), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);
            sound.ObserveFlash(snapshot, 12d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCount), Is.EqualTo(1), "The next number is on time.");

            // 일시정지 중에는 새 숫자를 내지 않는다.
            LabYaguangSound paused = Sound();
            LabSnapshot hold = Flash(Me);
            hold.paused = true;
            paused.ObserveFlash(hold, 10d, Me);
            Assert.That(paused.PlayCount(LabYaguangSfx.FlashWarn), Is.Zero);
            Assert.That(paused.PlayCount(LabYaguangSfx.FlashCount), Is.Zero);

            // 들어오자마자 이미 지난 섬광은 번쩍 소리를 내지 않는다.
            LabYaguangSound joined = Sound();
            LabSnapshot after = Flash(Me);
            after.flashCount = 1; after.flashLastAt = 13d; after.flashLastTarget = Me;
            joined.ObserveFlash(after, 13.1d, Me);
            Assert.That(joined.PlayCount(LabYaguangSfx.FlashBurst), Is.Zero);
        }

        [Test]
        public void TheCountGoesUpInPitchAndTheFoldersAreUnderResourcesAudio()
        {
            Assert.That(LabYaguangSound.CountPitch(3, 2f), Is.EqualTo(1f).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(2, 2f), Is.EqualTo(Mathf.Pow(2f, 2f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(1, 2f), Is.EqualTo(Mathf.Pow(2f, 4f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.CountPitch(5, 2f), Is.EqualTo(1f).Within(.0001f), "A longer warning starts at the base pitch.");
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashWarn), Is.EqualTo("Audio/YaguangFlashWarn"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashGlow), Is.EqualTo("Audio/YaguangFlashGlow"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashCount), Is.EqualTo("Audio/YaguangFlashCount"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashBurst), Is.EqualTo("Audio/YaguangFlashBurst"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashTaunt), Is.EqualTo("Audio/YaguangFlashTaunt"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ShakeBell), Is.EqualTo("Audio/YaguangShakeBell"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ShakeBellFinal), Is.EqualTo("Audio/YaguangShakeBellFinal"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.FlashCaught), Is.EqualTo("Audio/YaguangFlashCaught"));
        }

        // #396 섬광 반응
        [Test]
        public void TheTauntFollowsTheGongAndTheDizzyLineFollowsTheRelease()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            snapshot.players = new[] { new LabPlayerState { id = Me } };
            sound.ObserveFlash(snapshot, 10d, Me);

            // 13초 번쩍: 징은 바로, "안보이지롱~"은 0.35초 뒤에 한 번.
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Me;
            snapshot.flashWarningAt = snapshot.flashAt = 73d;
            sound.ObserveFlash(snapshot, 13.05d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashBurst), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashTaunt), Is.Zero, "Not on top of the gong.");
            sound.ObserveFlash(snapshot, 13.4d, Me);
            sound.ObserveFlash(snapshot, 13.6d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashTaunt), Is.EqualTo(1));

            // 20초에 Host가 내 눈부심을 풀었다: 0.2초 뒤 "으악 어지러워~" 한 번.
            snapshot.players[0].flashReleasedAt = 20d;
            sound.ObserveFlash(snapshot, 20.1d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCaught), Is.Zero);
            sound.ObserveFlash(snapshot, 20.25d, Me);
            sound.ObserveFlash(snapshot, 21d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCaught), Is.EqualTo(1));
        }

        [Test]
        public void LateJoinersHearNoOldLines()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = Flash(Me);
            snapshot.flashCount = 1; snapshot.flashLastAt = 13d; snapshot.flashLastTarget = Me;
            snapshot.flashWarningAt = snapshot.flashAt = 73d;
            snapshot.players = new[] { new LabPlayerState { id = Me, flashReleasedAt = 20d } };
            sound.ObserveFlash(snapshot, 20.3d, Me);
            sound.ObserveFlash(snapshot, 21d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashTaunt), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.FlashCaught), Is.Zero);
        }

        [Test]
        public void EachShakeRingsHigherAndTheLastOneRingsBig()
        {
            LabYaguangSound sound = Sound();
            Assert.That(sound.ObserveShakes(0, 10), Is.Null);
            Assert.That(sound.ObserveShakes(1, 10), Is.EqualTo(LabYaguangSfx.ShakeBell));
            Assert.That(sound.ObserveShakes(1, 10), Is.Null, "Once per shake.");
            Assert.That(sound.ObserveShakes(3, 10), Is.EqualTo(LabYaguangSfx.ShakeBell), "Two at once ring once.");
            Assert.That(sound.ObserveShakes(10, 10), Is.EqualTo(LabYaguangSfx.ShakeBellFinal));
            Assert.That(sound.PlayCount(LabYaguangSfx.ShakeBell), Is.EqualTo(2));
            Assert.That(sound.PlayCount(LabYaguangSfx.ShakeBellFinal), Is.EqualTo(1));
            // 다음 섬광: 수가 0으로 돌아가면 다시 센다.
            Assert.That(sound.ObserveShakes(0, 10), Is.Null);
            Assert.That(sound.ObserveShakes(1, 10), Is.EqualTo(LabYaguangSfx.ShakeBell));

            Assert.That(LabYaguangSound.BellPitch(1, 1f), Is.EqualTo(1f).Within(.0001f));
            Assert.That(LabYaguangSound.BellPitch(2, 1f), Is.EqualTo(Mathf.Pow(2f, 1f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.BellPitch(9, 1f), Is.EqualTo(Mathf.Pow(2f, 8f / 12f)).Within(.0001f));
            Assert.That(LabYaguangSound.BellPitch(20, 4f), Is.EqualTo(2f).Within(.0001f), "At most one octave up.");
        }

        // #397 콤보 예고와 진행
        private static LabSnapshot ComboWarning()
        {
            // 30초에 예고가 시작해 34초에 4칸이 나오는 콤보 (예고 4초).
            return new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing,
                comboWarningAt = 30d, comboNextAt = 34d
            };
        }

        [Test]
        public void TheGoblinCountsFourThreeTwoOneThenTheGongStartsTheCombo()
        {
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = ComboWarning();
            sound.ObserveFlash(snapshot, 29.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountFour), Is.Zero);
            foreach (double now in new[] { 30d, 30.5d, 31d, 32d, 32.7d, 33d })
                sound.ObserveFlash(snapshot, now, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountFour), Is.EqualTo(1), "넷");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountThree), Is.EqualTo(1), "셋");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountTwo), Is.EqualTo(1), "둘");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountOne), Is.EqualTo(1), "하나");

            // 34초에 4칸이 나온다: 깽! 한 번.
            snapshot.comboWarningAt = snapshot.comboNextAt = -1d;
            snapshot.comboCount = 1; snapshot.comboActive = true;
            snapshot.comboStartedAt = 34d; snapshot.comboEndsAt = 44d;
            sound.ObserveFlash(snapshot, 34.05d, Me);
            sound.ObserveFlash(snapshot, 34.2d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboStart), Is.EqualTo(1));

            Assert.That(LabYaguangSound.CountSfx(5), Is.EqualTo(LabYaguangSfx.ComboCountOne), "Longer warnings count from 하나 again.");
        }

        [Test]
        public void GeumokSaysTheRuleAfterTheGoblinsFourOnlyBeforeTheRoundsFirstCombo()
        {
            // #695: 30초 예고 (아직 나온 콤보 0개): 야광귀 "넷" → 0.9초 뒤 금옥 규칙. 넷/셋/둘/하나는 모두 센다.
            LabYaguangSound sound = Sound();
            LabSnapshot snapshot = ComboWarning();
            sound.ObserveFlash(snapshot, 29.5d, Me);
            sound.ObserveFlash(snapshot, 30d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountFour), Is.EqualTo(1), "넷 first.");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideRule), Is.Zero, "Geumok waits for the four.");
            foreach (double now in new[] { 30.5d, 30.95d, 31d, 32d, 33d })
                sound.ObserveFlash(snapshot, now, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideRule), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountThree), Is.EqualTo(1), "The count goes on under her.");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboCountOne), Is.EqualTo(1));

            // 두 번째 콤보 예고 (이미 1개가 나옴): 규칙은 다시 말하지 않는다 (기본 1 = 첫 콤보만).
            snapshot.comboCount = 1;
            snapshot.comboWarningAt = 60d; snapshot.comboNextAt = 64d;
            foreach (double now in new[] { 60d, 61d, 62d, 63d })
                sound.ObserveFlash(snapshot, now, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideRule), Is.EqualTo(1));
        }

        [Test]
        public void TheCountFadesFourThreeTwoOneUnderTheRule()
        {
            Assert.That(LabYaguangSound.GuideCountGain(4, .6f), Is.EqualTo(1f).Within(1e-5f), "넷 at full size");
            Assert.That(LabYaguangSound.GuideCountGain(3, .6f), Is.EqualTo(.6f).Within(1e-5f));
            Assert.That(LabYaguangSound.GuideCountGain(2, .6f), Is.EqualTo(.36f).Within(1e-5f));
            Assert.That(LabYaguangSound.GuideCountGain(1, .6f), Is.EqualTo(.216f).Within(1e-5f), "하나 the quietest");
            Assert.That(LabYaguangSound.GuideCountGain(2, 1f), Is.EqualTo(1f), "1 keeps them all the same.");
        }

        [Test]
        public void GeumokPraisesEachHitAndCheersTheClear()
        {
            LabYaguangSound sound = Sound();
            var snapshot = new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing };
            sound.ObserveFlash(snapshot, 33.9d, Me);
            snapshot.comboCount = 1; snapshot.comboActive = true;
            snapshot.comboStartedAt = 34d; snapshot.comboEndsAt = 44d;
            Watch(sound, snapshot, 34d, 35.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit1), Is.Zero, "Nothing until a slot is hit.");

            // #695: 칸을 맞힐 때마다 0.3초 뒤에 좋아! / 잘했어! / 하나만 더..!
            snapshot.comboProgress = 1;
            sound.ObserveFlash(snapshot, 36d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit1), Is.Zero, "It waits for the hit sound.");
            Watch(sound, snapshot, 36.05d, 36.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit1), Is.EqualTo(1), "좋아!");
            snapshot.comboProgress = 2;
            Watch(sound, snapshot, 37d, 37.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit2), Is.EqualTo(1), "잘했어!");
            snapshot.comboProgress = 3;
            Watch(sound, snapshot, 38d, 38.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit3), Is.EqualTo(1), "하나만 더..!");

            // 넷째 칸 = 성공: 콤보는 끝났지만 그렇지!!
            snapshot.comboProgress = 4; snapshot.comboActive = false;
            snapshot.comboResultCount = 1; snapshot.comboResult = LabComboResult.Success; snapshot.comboResultAt = 39d;
            Watch(sound, snapshot, 39d, 40d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideClear), Is.EqualTo(1), "그렇지!!");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit1), Is.EqualTo(1), "Each line once.");
        }

        [Test]
        public void AFailedComboGetsNoPraiseOnlyTheGoblinsMelong()
        {
            LabYaguangSound sound = Sound();
            var snapshot = new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing };
            sound.ObserveFlash(snapshot, 33.9d, Me);
            snapshot.comboCount = 1; snapshot.comboActive = true;
            snapshot.comboStartedAt = 34d; snapshot.comboEndsAt = 44d;
            snapshot.comboProgress = 2;
            sound.ObserveFlash(snapshot, 36d, Me);
            // 0.3초 안에 틀린 색으로 끝남: 잘했어!는 내지 않는다.
            snapshot.comboActive = false;
            snapshot.comboResultCount = 1; snapshot.comboResult = LabComboResult.WrongColor; snapshot.comboResultAt = 36.1d;
            Watch(sound, snapshot, 36.1d, 38d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideHit2), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideClear), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboGuideFail), Is.EqualTo(1), "아이고 first");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.EqualTo(1), "then 메롱~");
        }

        [Test]
        public void TheGuideFoldersFollowTheHitCount()
        {
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboGuideRule), Is.EqualTo("Audio/YaguangComboGuideRule"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboGuideHit1), Is.EqualTo("Audio/YaguangComboGuideHit1"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboGuideHit3), Is.EqualTo("Audio/YaguangComboGuideHit3"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboGuideClear), Is.EqualTo("Audio/YaguangComboGuideClear"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboGuideFail), Is.EqualTo("Audio/YaguangComboGuideFail"));
            Assert.That(LabYaguangSound.GuideFeedbackSfx(1), Is.EqualTo(LabYaguangSfx.ComboGuideHit1));
            Assert.That(LabYaguangSound.GuideFeedbackSfx(2), Is.EqualTo(LabYaguangSfx.ComboGuideHit2));
            Assert.That(LabYaguangSound.GuideFeedbackSfx(3), Is.EqualTo(LabYaguangSfx.ComboGuideHit3));
            Assert.That(LabYaguangSound.GuideFeedbackSfx(4), Is.EqualTo(LabYaguangSfx.ComboGuideClear));
        }

        [Test]
        public void TheClockTicksEveryHalfSecondThenFasterAndStopsWithTheCombo()
        {
            LabYaguangSound sound = Sound();
            var snapshot = new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing
            };
            sound.ObserveFlash(snapshot, 33.9d, Me);
            snapshot.comboCount = 1; snapshot.comboActive = true;
            snapshot.comboStartedAt = 34d; snapshot.comboEndsAt = 44d;
            // 34~44초를 0.05초 간격으로 지켜본다: 34.5~39.5초 0.5초마다 11번, 남은 4초(40~43.75초)는 0.25초마다 16번.
            for (int i = 0; i < 200; i++) sound.ObserveFlash(snapshot, 34d + i * .05d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTick), Is.EqualTo(11));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTickFast), Is.EqualTo(16));

            // 콤보가 끝나면 더 울리지 않는다.
            snapshot.comboActive = false;
            int before = sound.PlayCount(LabYaguangSfx.ComboTickFast) + sound.PlayCount(LabYaguangSfx.ComboTick);
            sound.ObserveFlash(snapshot, 44.5d, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTickFast) + sound.PlayCount(LabYaguangSfx.ComboTick), Is.EqualTo(before));

            Assert.That(LabYaguangSound.LatestTickAt(34d, 44d, 34.4d, 3d, .5d, .25d, out _), Is.EqualTo(double.NegativeInfinity));
            Assert.That(LabYaguangSound.LatestTickAt(34d, 44d, 36.2d, 3d, .5d, .25d, out bool fast), Is.EqualTo(36d));
            Assert.That(fast, Is.False);
            Assert.That(LabYaguangSound.LatestTickAt(34d, 44d, 42.7d, 3d, .5d, .25d, out fast), Is.EqualTo(42.5d));
            Assert.That(fast, Is.True);
            Assert.That(LabYaguangSound.LatestTickAt(34d, 44d, 36.2d, 3d, 1d, .5d, out _), Is.EqualTo(36d));
        }

        [Test]
        public void TheComboFoldersAreUnderResourcesAudio()
        {
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboCountOne), Is.EqualTo("Audio/YaguangComboCount1"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboCountFour), Is.EqualTo("Audio/YaguangComboCount4"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboStart), Is.EqualTo("Audio/YaguangComboStart"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboTick), Is.EqualTo("Audio/YaguangComboTick"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboTickFast), Is.EqualTo("Audio/YaguangComboTickFast"));
        }

        // #398: 50초에 콤보 결과. 효과는 팀 전체가 받아 65초에 끝난다 (Other는 64초).
        private static LabSnapshot ComboResult(LabComboResult result)
        {
            bool failed = result != LabComboResult.Success;
            return new LabSnapshot
            {
                sessionId = "yaguang", round = 1, phase = LabPhase.Playing,
                comboResultCount = 1, comboResult = result, comboResultAt = 50d,
                players = new[]
                {
                    new LabPlayerState { id = Me, comboPenaltyUntil = failed ? 65d : 0d, comboRewardUntil = failed ? 0d : 65d },
                    new LabPlayerState { id = Other, comboPenaltyUntil = failed ? 64d : 0d, comboRewardUntil = failed ? 0d : 64d }
                }
            };
        }

        private static void Watch(LabYaguangSound sound, LabSnapshot snapshot, double from, double to)
        {
            for (double t = from; t <= to + 1e-9; t += .05d) sound.ObserveFlash(snapshot, t, Me);
        }

        [Test]
        public void AFailedComboGetsAMelongThenSmallStartsAndEnds()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing }, 49.9d, Me);
            LabSnapshot snapshot = ComboResult(LabComboResult.Timeout);
            Watch(sound, snapshot, 50d, 50.25d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.Zero, "메롱 waits for the fail sound.");
            Watch(sound, snapshot, 50.3d, 51.5d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallStart), Is.Zero, "SMALL waits for its badge.");
            Watch(sound, snapshot, 51.6d, 64.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallStart), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.Zero, "It ends when the last one gets back to size.");
            Watch(sound, snapshot, 65d, 70d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigStart) + sound.PlayCount(LabYaguangSfx.ComboBigEnd), Is.Zero);
        }

        [Test]
        public void ASuccessfulComboStartsAndEndsBigWithoutAMelong()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing }, 49.9d, Me);
            LabSnapshot snapshot = ComboResult(LabComboResult.Success);
            Watch(sound, snapshot, 50d, 70d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigStart), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigEnd), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallStart) + sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.Zero);
        }

        [Test]
        public void ANewResultDropsTheLastOnesEndAndALateJoinerHearsNothingOld()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing }, 49.9d, Me);
            LabSnapshot snapshot = ComboResult(LabComboResult.WrongColor);
            Watch(sound, snapshot, 50d, 55d);
            // 다음 콤보 성공: SMALL은 소리 없이 끝나고 BIG이 시작된다.
            snapshot.comboResultCount = 2; snapshot.comboResult = LabComboResult.Success; snapshot.comboResultAt = 56d;
            foreach (LabPlayerState player in snapshot.players) { player.comboPenaltyUntil = 56d; player.comboRewardUntil = 71d; }
            Watch(sound, snapshot, 56d, 72d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.Zero);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigStart), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigEnd), Is.EqualTo(1));

            LabYaguangSound late = Sound();
            LabSnapshot old = ComboResult(LabComboResult.Timeout);
            for (double t = 50.5d; t <= 70d; t += .05d) late.ObserveFlash(old, t, Me);
            Assert.That(late.PlayCount(LabYaguangSfx.ComboTaunt) + late.PlayCount(LabYaguangSfx.ComboSmallStart)
                + late.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.Zero, "A screen that joins after the result stays quiet.");
        }

        [Test]
        public void TheComboResultFoldersAreUnderResourcesAudio()
        {
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboTaunt), Is.EqualTo("Audio/YaguangComboTaunt"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboSmallStart), Is.EqualTo("Audio/YaguangComboSmallStart"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboSmallEnd), Is.EqualTo("Audio/YaguangComboSmallEnd"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboBigStart), Is.EqualTo("Audio/YaguangComboBigStart"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboBigEnd), Is.EqualTo("Audio/YaguangComboBigEnd"));
        }

        [Test]
        public void TheFirstSuccessSaysIreonThenLaterOnesSayIreolSuga()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing }, 49.9d, Me);
            LabSnapshot snapshot = ComboResult(LabComboResult.Success);
            Watch(sound, snapshot, 50d, 50.75d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst), Is.Zero, "The line waits 0.8 s.");
            Watch(sound, snapshot, 50.8d, 70d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst), Is.EqualTo(1), "이런! on the first success.");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedSecond), Is.Zero);

            // 두 번째, 세 번째 성공은 "이럴 수가!"
            for (uint n = 2; n <= 3; n++)
            {
                double at = 60d * n;
                snapshot.comboResultCount = n; snapshot.comboResultAt = at;
                foreach (LabPlayerState player in snapshot.players) player.comboRewardUntil = at + 15d;
                Watch(sound, snapshot, at, at + 2d);
            }
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedSecond), Is.EqualTo(2));

            // 새 라운드는 다시 "이런!"부터
            LabSnapshot next = ComboResult(LabComboResult.Success);
            next.round = 2;
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 2, phase = LabPhase.Playing }, 49.9d, Me);
            Watch(sound, next, 50d, 52d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst), Is.EqualTo(2));
        }

        [Test]
        public void FailuresAndLateJoinersHearNoSuccessLine()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "yaguang", round = 1, phase = LabPhase.Playing }, 49.9d, Me);
            Watch(sound, ComboResult(LabComboResult.WrongColor), 50d, 70d);
            LabYaguangSound late = Sound();
            for (double t = 50.5d; t <= 70d; t += .05d) late.ObserveFlash(ComboResult(LabComboResult.Success), t, Me);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst) + sound.PlayCount(LabYaguangSfx.ComboClearedSecond)
                + late.PlayCount(LabYaguangSfx.ComboClearedFirst) + late.PlayCount(LabYaguangSfx.ComboClearedSecond), Is.Zero);
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboClearedFirst), Is.EqualTo("Audio/YaguangComboClearedFirst"));
            Assert.That(LabYaguangSound.Folder(LabYaguangSfx.ComboClearedSecond), Is.EqualTo("Audio/YaguangComboClearedSecond"));
        }

        // #664 구미호 여우구슬 판정: 5초에 떨어져 20초에 끝난다.
        private static LabSnapshot FoxRound()
        {
            return new LabSnapshot
            {
                sessionId = "gumiho", round = 1, phase = LabPhase.Playing,
                foxRoundStartedAt = 5d, foxRoundEndsAt = 20d, foxRoundRunning = true, foxRoundTotal = 6, foxRoundRemaining = 6,
                players = new[] { new LabPlayerState { id = Me }, new LabPlayerState { id = Other } }
            };
        }

        private static void Settle(LabSnapshot snapshot, int result, double at, double until)
        {
            snapshot.foxRoundRunning = false;
            snapshot.foxRoundRemaining = 0;
            snapshot.foxRoundResult = result;
            snapshot.foxRoundResultAt = at;
            snapshot.foxRoundCount++;
            foreach (LabPlayerState player in snapshot.players)
                if (result < 0) player.comboPenaltyUntil = until;
                else player.comboRewardUntil = until;
        }

        [Test]
        public void TheFoxRoundTicksLikeTheComboThenTimesUpIntoSmallWithoutTheGoblinsVoice()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "gumiho", round = 1, phase = LabPhase.Playing }, 4.9d, Me);
            LabSnapshot snapshot = FoxRound();
            // 5~20초: 5.5~15.5초 0.5초마다 21번, 남은 4초(16~19.75초)는 0.25초마다 16번. 끝나는 순간에는 울리지 않는다.
            Watch(sound, snapshot, 5d, 19.99d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTick), Is.EqualTo(21));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTickFast), Is.EqualTo(16));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboStart), Is.Zero, "No gong: the fox orbs falling have their own sound.");

            Settle(snapshot, -1, 20d, 35d);
            Watch(sound, snapshot, 20d, 21.5d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTick) + sound.PlayCount(LabYaguangSfx.ComboTickFast), Is.EqualTo(37),
                "The clock stops with the round.");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallStart), Is.Zero, "SMALL waits for its mark, like the combo's.");
            Watch(sound, snapshot, 21.6d, 34.9d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallStart), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.Zero);
            Watch(sound, snapshot, 35d, 37d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboSmallEnd), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTaunt), Is.Zero, "No 메롱~: that is the 야광귀's voice.");
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigStart) + sound.PlayCount(LabYaguangSfx.ComboBigEnd), Is.Zero);
        }

        [Test]
        public void ClearingTheFoxOrbsStopsTheClockAndGoesBigWithoutTheGoblinsVoice()
        {
            LabYaguangSound sound = Sound();
            sound.ObserveFlash(new LabSnapshot { sessionId = "gumiho", round = 1, phase = LabPhase.Playing }, 4.9d, Me);
            LabSnapshot snapshot = FoxRound();
            Watch(sound, snapshot, 5d, 9.2d);
            int ticks = sound.PlayCount(LabYaguangSfx.ComboTick);
            Assert.That(ticks, Is.EqualTo(8), "5.5 s to 9 s.");
            Settle(snapshot, 1, 9.2d, 24.2d);
            Watch(sound, snapshot, 9.25d, 30d);
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboTick) + sound.PlayCount(LabYaguangSfx.ComboTickFast), Is.EqualTo(ticks));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigStart), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboBigEnd), Is.EqualTo(1));
            Assert.That(sound.PlayCount(LabYaguangSfx.ComboClearedFirst) + sound.PlayCount(LabYaguangSfx.ComboClearedSecond),
                Is.Zero, "No 이런!: that is the 야광귀's voice.");

            LabYaguangSound late = Sound();
            for (double t = 12d; t <= 30d; t += .05d) late.ObserveFlash(snapshot, t, Me);
            Assert.That(late.PlayCount(LabYaguangSfx.ComboBigStart) + late.PlayCount(LabYaguangSfx.ComboBigEnd), Is.Zero,
                "Someone joining after the result hears nothing old.");
            Assert.That(LabYaguangSound.FoxRoundClipFolders, Is.EquivalentTo(new[] { "Audio/YaguangComboTick",
                "Audio/YaguangComboTickFast", "Audio/YaguangComboSmallStart", "Audio/YaguangComboSmallEnd",
                "Audio/YaguangComboBigStart", "Audio/YaguangComboBigEnd" }));
        }
    }
}
