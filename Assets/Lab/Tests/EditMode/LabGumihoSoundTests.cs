using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// 구미호 스테이지 소리 (#351): 효과음 6종은 폴더가 비어 있으면 조용히 넘어가고, 매혹 소리는 예고가 끝나 걸린 순간에
    /// 한 번만 난다. 전투 BGM·피버 곡은 각자 폴더에 있고, 소리별 크기는 DEV 칸 없는 Inspector 값이다. Guest 기기도
    /// 스테이지 요괴를 알아 스테이지 BGM과 야광귀 피버 (#343)가 모든 기기에서 같다.
    /// </summary>
    public sealed class LabGumihoSoundTests
    {
        private GameObject host;
        private LabConfig config;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (config != null) Object.DestroyImmediate(config);
        }

        private LabGumihoSound Sound()
        {
            host = new GameObject("gumiho-sound");
            LabGumihoSound sound = host.AddComponent<LabGumihoSound>();
            // 에디터 테스트에서는 Awake가 저절로 불리지 않을 수 있어 직접 불러 소리 파일을 읽힌다.
            if (sound.ClipCount(LabGumihoSfx.CharmCast) == 0)
                typeof(LabGumihoSound).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sound, null);
            return sound;
        }

        private static LabSnapshot Charm(double warnedAt, double startedAt, double until) => new LabSnapshot
        {
            sessionId = "gumiho-sound", round = 1, phase = LabPhase.Playing, charmedPlayer = 20,
            charmWarnedAt = warnedAt, charmStartedAt = startedAt, charmUntil = until
        };

        [Test]
        public void TheTeamsClipsAreInTheirOwnFolders()
        {
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.CharmCast), Is.EqualTo("Audio/CharmCast"));
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.CharmCheck), Is.EqualTo("Audio/CharmCheck"));
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.CharmRelease), Is.EqualTo("Audio/CharmRelease"));
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.FoxFall), Is.EqualTo("Audio/FoxFall"));
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.FoxClear), Is.EqualTo("Audio/FoxClear"));
            Assert.That(LabGumihoSound.Folder(LabGumihoSfx.BombOpen), Is.EqualTo("Audio/BombOpen"));
            Assert.That(Resources.LoadAll<AudioClip>(LabGumihoSound.CharmCastFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "CharmCast" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabGumihoSound.CharmReleaseFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "CharmRelease" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabGumihoSound.FoxFallFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "FoxFall" }));
            Assert.That(Resources.LoadAll<AudioClip>(LabGumihoSound.FoxClearFolder).Select(clip => clip.name),
                Is.EqualTo(new[] { "FoxClear" }));
        }

        [Test]
        public void EachSoundCountsAndStaysQuietWithoutAClip()
        {
            LabGumihoSound sound = Sound();
            Assert.That(sound.ClipCount(LabGumihoSfx.CharmCast), Is.EqualTo(1));
            foreach (LabGumihoSfx sfx in System.Enum.GetValues(typeof(LabGumihoSfx)))
            {
                // 체크·폭탄 열림처럼 파일이 아직 없으면 아무것도 내지 않고 넘어간다. 넣으면 낸다.
                Assert.That(sound.Play(sfx), Is.EqualTo(sound.ClipCount(sfx) > 0), sfx.ToString());
                Assert.That(sound.PlayCount(sfx), Is.EqualTo(1), sfx.ToString());
            }
        }

        [Test]
        public void TheCharmSoundWaitsForTheCharmToTakeHoldAndPlaysOnce()
        {
            LabGumihoSound sound = Sound();
            LabSnapshot charm = Charm(10d, 13d, 21d);
            Assert.That(sound.ObserveCharm(charm, 11d), Is.False, "Not during the 3 s warning.");
            Assert.That(sound.ObserveCharm(charm, 13d), Is.True, "The moment the hearts show.");
            Assert.That(sound.ObserveCharm(charm, 13.5d), Is.False, "Once per charm.");
            Assert.That(sound.ObserveCharm(Charm(-1d, -1d, -1d), 22d), Is.False, "Not when it is over.");
            Assert.That(sound.ObserveCharm(Charm(37d, 40d, 48d), 40.2d), Is.True, "The next charm plays again.");
            Assert.That(sound.ObserveCharm(Charm(57d, 60d, 68d), 62d), Is.False,
                "A device that first sees a charm long after it took hold (a rejoin) stays quiet.");
            LabSnapshot ended = Charm(77d, 80d, 88d);
            ended.phase = LabPhase.Victory;
            Assert.That(sound.ObserveCharm(ended, 80d), Is.False, "Not after the battle.");
        }

        [Test]
        public void EverySoundHasItsOwnInspectorVolumeWithoutADeveloperField()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            Assert.That(config.GumihoBgmVolume, Is.EqualTo(.2f));
            Assert.That(config.GumihoFeverBgmVolume, Is.EqualTo(.3f));
            Assert.That(config.CharmCastSoundVolume, Is.EqualTo(.7f));
            Assert.That(config.CharmCheckSoundVolume, Is.EqualTo(.8f));
            Assert.That(config.CharmReleaseSoundVolume, Is.EqualTo(.8f));
            Assert.That(config.FoxFallSoundVolume, Is.EqualTo(.8f));
            Assert.That(config.FoxClearSoundVolume, Is.EqualTo(.8f));
            Assert.That(config.BombOpenSoundVolume, Is.EqualTo(.8f));
            float[] shared = config.CaptureDeveloperValues();
            JsonUtility.FromJsonOverwrite("{\"charmCastSoundVolume\":3,\"foxClearSoundVolume\":-1,\"gumihoFeverBgmVolume\":0.5}", config);
            Assert.That(config.CharmCastSoundVolume, Is.EqualTo(1f));
            Assert.That(config.FoxClearSoundVolume, Is.Zero);
            Assert.That(config.GumihoFeverBgmVolume, Is.EqualTo(.5f));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(shared),
                "Inspector only: no DEV value moves, so the shared settings and the connection version stay as they were.");
        }

        [Test]
        public void TheGumihoSongsAreInTheirOwnFolders()
        {
            AudioClip[] stage = Resources.LoadAll<AudioClip>(LabBattleBgm.GumihoClipFolder);
            Assert.That(stage.Select(clip => clip.name), Is.EqualTo(new[] { "Gumiho_Stage" }));
            Assert.That(stage[0].loadType, Is.EqualTo(AudioClipLoadType.Streaming),
                "The long stage song streams instead of sitting decompressed in memory.");
            AudioClip[] fever = Resources.LoadAll<AudioClip>(LabBattleBgm.GumihoFeverClipFolder);
            Assert.That(fever.Select(clip => clip.name), Is.EqualTo(new[] { "Gumiho_Fever" }));
            Assert.That(fever[0].length, Is.EqualTo(20f).Within(.1f), "Made for the 20 s fever time.");
            Assert.That(Resources.LoadAll<AudioClip>(LabBattleBgm.ClipFolder), Has.Length.EqualTo(1),
                "The other stages keep the one battle song.");
        }

        [Test]
        public void AGuestLearnsTheStagesYokaiSoTheYaguangFeverShowsThereToo()
        {
            host = new GameObject("gumiho-stage-guest");
            var network = host.AddComponent<LabNetwork>();
            config = ScriptableObject.CreateInstance<LabConfig>();
            network.config = config;
            network.ObserveGameStage(LabStageYokai.Gumiho);
            Assert.That(network.GameStageYokai, Is.EqualTo(LabStageYokai.None), "The Lab has no stages.");

            Assert.That(network.SetGameFlowEnabled(true), Is.True);
            Assert.That(network.IsHost, Is.False, "Not hosting: a Guest.");
            network.ObserveGameStage(LabStageYokai.Gumiho);
            Assert.That(network.GameStageYokai, Is.EqualTo(LabStageYokai.Gumiho));
            var snapshot = new LabSnapshot { phase = LabPhase.Playing, playedSeconds = 105d };
            Assert.That(network.YaguangFeverElapsed(snapshot), Is.EqualTo(-1d), "No yaguang fever in the fox's stage.");
            network.ObserveGameStage(LabStageYokai.Yaguang);
            Assert.That(network.YaguangFeverElapsed(snapshot), Is.EqualTo(5d).Within(1e-9),
                "The yaguang's fever (#343) runs on a Guest, not only on the Host.");
            network.ObserveGameStage((LabStageYokai)99);
            Assert.That(network.GameStageYokai, Is.EqualTo(LabStageYokai.Yaguang), "An unknown yokai is ignored.");
        }
    }
}
