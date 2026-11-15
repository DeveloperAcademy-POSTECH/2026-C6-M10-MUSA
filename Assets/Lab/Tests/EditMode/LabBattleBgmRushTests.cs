using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#745: 남은 30초부터 라운드 곡을 음높이 그대로 빠르게.</summary>
    public sealed class LabBattleBgmRushTests
    {
        [Test]
        public void ItSpeedsUpFromTheThirtySecondsLeftAlarmOnlyWhereTheAlarmIs()
        {
            Assert.That(LabBattleBgm.RushOn(true, 30.5d, 30f), Is.False);
            Assert.That(LabBattleBgm.RushOn(true, 30d, 30f), Is.True);
            Assert.That(LabBattleBgm.RushOn(true, 12d, 30f), Is.True);
            Assert.That(LabBattleBgm.RushOn(true, 0d, 30f), Is.False, "Time is up: the song fades out instead.");
            Assert.That(LabBattleBgm.RushOn(false, 12d, 30f), Is.False, "No alarm (tutorial / 2:2 / Lab): no rush either.");
        }

        [Test]
        public void TheFastSongPicksUpWhereTheSongIs()
        {
            Assert.That(LabBattleBgm.RushTimeFor(69f, 1.15f, 100f), Is.EqualTo(60f).Within(1e-4f));
            Assert.That(LabBattleBgm.RushTimeFor(0f, 1.15f, 100f), Is.EqualTo(0f));
            Assert.That(LabBattleBgm.RushTimeFor(172.5f, 1.15f, 100f), Is.EqualTo(50f).Within(1e-3f), "Past the end it wraps (the song loops).");
            Assert.That(LabBattleBgm.RushTimeFor(10f, 1.15f, 0f), Is.EqualTo(0f));
        }

        [TestCase(LabBattleBgm.GumihoClipFolder, LabBattleBgm.GumihoRushClipFolder)]
        [TestCase(LabBattleBgm.YaguangClipFolder, LabBattleBgm.YaguangRushClipFolder)]
        [TestCase(LabBattleBgm.JigwiClipFolder, LabBattleBgm.JigwiRushClipFolder)]
        public void EachStageSongHasAFasterCopyThatStartsMidWayWithoutStreaming(string songFolder, string rushFolder)
        {
            AudioClip[] songs = Resources.LoadAll<AudioClip>(songFolder), rushes = Resources.LoadAll<AudioClip>(rushFolder);
            Assert.That(songs, Has.Length.EqualTo(1), songFolder);
            Assert.That(rushes, Has.Length.EqualTo(1), rushFolder);
            Assert.That(rushes[0].length, Is.EqualTo(songs[0].length / LabBattleBgm.RushSpeed).Within(.5f),
                "The fast copy is the same song played " + LabBattleBgm.RushSpeed + "x as fast.");
            Assert.That(rushes[0].loadType, Is.Not.EqualTo(AudioClipLoadType.Streaming),
                "It starts at an offset mid-battle; streamed clips stall on that frame (#489).");
        }

        [Test]
        public void TheVfxLabCanSkipTheBattleClockAheadEvenWhilePaused()
        {
            var clock = new LabBattleClock();
            Assert.That(clock.Now(10d), Is.EqualTo(10d));
            Assert.That(clock.Skip(80d), Is.True);
            Assert.That(clock.Now(10d), Is.EqualTo(90d));
            Assert.That(clock.Skip(0d), Is.False);
            Assert.That(clock.Skip(-5d), Is.False);
            clock.Pause(12d);
            Assert.That(clock.Skip(3d), Is.True);
            Assert.That(clock.Now(20d), Is.EqualTo(95d), "Paused: it stays at the skipped time.");
            clock.Resume(20d);
            Assert.That(clock.Now(21d), Is.EqualTo(96d));
        }

        [Test]
        public void EachStagePreloadsItsFastSong()
        {
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Gumiho), Does.Contain(LabBattleBgm.GumihoRushClipFolder));
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Yaguang), Does.Contain(LabBattleBgm.YaguangRushClipFolder));
            Assert.That(LabBattleAudioWarmup.StageFolders(LabStageYokai.Jigwi), Does.Contain(LabBattleBgm.JigwiRushClipFolder));
        }
    }
}
