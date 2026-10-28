using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Muted playback/state regression checks for #358. A passive Guest exposes snapshots without starting
    /// any socket, transport or NetworkManager loop. These checks do not verify audible sound or LAN timing.
    /// </summary>
    public sealed class LabBattleBgmStageTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Fixture fixture;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            fixture?.Dispose();
            fixture = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StageSongsSelectTheGumihoClipOnlyForTheGumihoGameStage()
        {
            AudioClip normal = Resources.Load<AudioClip>("Audio/BGM/Kkwaenggwari_13");
            AudioClip gumiho = Resources.Load<AudioClip>("Audio/GumihoBGM/Gumiho_Stage");
            Assert.That(normal, Is.Not.Null);
            Assert.That(gumiho, Is.Not.Null);
            var stages = new[] { LabStageYokai.Gumiho, LabStageYokai.Yaguang, LabStageYokai.Jigwi,
                LabStageYokai.None, LabStageYokai.Gumiho };
            for (int i = 0; i < stages.Length; i++)
            {
                bool gameFlow = i != 4; // The last case is the unchanged Lab single-round path.
                bool territory = i == 3;
                fixture = new Fixture(gameFlow, stages[i]);
                var snapshot = Snapshot(10d, feverAt: -1d);
                snapshot.teamTerritory = territory;
                fixture.SetSnapshot(snapshot);
                yield return null;
                yield return null;
                AudioClip expected = gameFlow && stages[i] == LabStageYokai.Gumiho ? gumiho : normal;
                Assert.That(fixture.Bgm.BattleClip, Is.SameAs(expected),
                    "Game=" + gameFlow + ", yokai=" + stages[i] + ", territory=" + territory);
                Assert.That(fixture.Bgm.IsPlaying, Is.True, "The selected song must reach a running AudioSource.");
                Assert.That(fixture.Bgm.FeverPlaying, Is.False);
                Assert.That(fixture.Bgm.FeverCueCount, Is.Zero);
                foreach (AudioSource source in fixture.Sources) Assert.That(source.mute, Is.True);
                fixture.Dispose();
                fixture = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator GumihoFeverWaitsThroughPauseResumesOnceAndClearsOnTheNextRoundAndResults()
        {
            fixture = new Fixture(true, LabStageYokai.Gumiho);
            fixture.Bgm.feverCrossfadeSeconds = 0f;
            fixture.Bgm.fadeOutSeconds = 0f;
            AudioClip battle = Resources.Load<AudioClip>("Audio/GumihoBGM/Gumiho_Stage");
            AudioClip fever = Resources.Load<AudioClip>("Audio/GumihoFever/Gumiho_Fever");
            Assert.That(battle, Is.Not.Null);
            Assert.That(fever, Is.Not.Null);
            fixture.SetSnapshot(Snapshot(90d, feverAt: 100d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.BattleClip, Is.SameAs(battle));
            Assert.That(fixture.Bgm.IsPlaying, Is.True);
            Assert.That(fixture.Bgm.FeverPlaying, Is.False);

            fixture.SetSnapshot(Snapshot(99.8d, paused: true, feverAt: 100d));
            yield return null;
            float heldBattleTime = fixture.Sources[0].time;
            // An extrapolated wall clock would cross 100 while the authoritative battle clock is paused.
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(fixture.Bgm.IsPlaying, Is.False);
            Assert.That(fixture.Bgm.FeverPlaying, Is.False);
            Assert.That(fixture.Bgm.FeverCueCount, Is.Zero, "A pause before fever must not start its cue.");
            Assert.That(fixture.Sources[0].time, Is.EqualTo(heldBattleTime).Within(.05f));

            fixture.SetSnapshot(Snapshot(100.05d, feverAt: 100d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.FeverClip, Is.SameAs(fever));
            Assert.That(fixture.Bgm.FeverPlaying, Is.True);
            Assert.That(fixture.Bgm.FeverCueCount, Is.EqualTo(1));
            // Make a restart-to-zero observable independently of the Editor's frame rate.
            yield return new WaitForSecondsRealtime(.2f);

            fixture.SetSnapshot(Snapshot(100.2d, paused: true, feverAt: 100d));
            yield return null;
            float heldFeverTime = fixture.Sources[1].time;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(fixture.Bgm.IsPlaying, Is.False);
            Assert.That(fixture.Bgm.FeverPlaying, Is.False);
            Assert.That(fixture.Sources[1].time, Is.EqualTo(heldFeverTime).Within(.05f));
            Assert.That(fixture.Bgm.FeverCueCount, Is.EqualTo(1));

            fixture.SetSnapshot(Snapshot(100.2d, feverAt: 100d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.FeverClip, Is.SameAs(fever));
            Assert.That(fixture.Bgm.FeverPlaying, Is.True);
            Assert.That(fixture.Bgm.FeverCueCount, Is.EqualTo(1), "UnPause must not replay the fever-start cue.");
            Assert.That(fixture.Sources[1].time, Is.GreaterThanOrEqualTo(heldFeverTime - .05f),
                "The fever song must continue, rather than restarting from zero on resume.");

            fixture.SetSnapshot(Snapshot(200d, round: 2, feverAt: 300d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.BattleClip, Is.SameAs(battle));
            Assert.That(fixture.Bgm.IsPlaying, Is.True);
            Assert.That(fixture.Bgm.FeverPlaying, Is.False, "A new round clears the previous fever song.");
            Assert.That(fixture.Bgm.FeverCueCount, Is.EqualTo(1));

            var result = Snapshot(200.1d, round: 2, feverAt: 300d);
            result.phase = LabPhase.Victory;
            fixture.SetSnapshot(result);
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.IsPlaying, Is.False);
            Assert.That(fixture.Bgm.FeverPlaying, Is.False);
            Assert.That(fixture.Bgm.FeverCueCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GuestYaguangKeepsTheExistingFeverSongAndDoesNotReplayALateCue()
        {
            fixture = new Fixture(true, LabStageYokai.Yaguang);
            fixture.Bgm.feverCrossfadeSeconds = 0f;
            fixture.SetSnapshot(Snapshot(105d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.BattleClip, Is.SameAs(Resources.LoadAll<AudioClip>(LabBattleBgm.ClipFolder)[0]));
            Assert.That(fixture.Bgm.FeverClip, Is.SameAs(Resources.LoadAll<AudioClip>(LabBattleBgm.FeverClipFolder)[0]));
            Assert.That(fixture.Bgm.FeverPlaying, Is.True);
            Assert.That(fixture.Sources[1].time, Is.GreaterThanOrEqualTo(4.9f), "A late observer seeks into the existing fever song.");
            Assert.That(fixture.Bgm.FeverCueCount, Is.Zero, "A late Guest does not replay the fever-start cue.");
            fixture.SetSnapshot(Snapshot(106d, paused: true));
            yield return null;
            Assert.That(fixture.Bgm.FeverPlaying, Is.False);
            fixture.SetSnapshot(Snapshot(106d));
            yield return null;
            yield return null;
            Assert.That(fixture.Bgm.FeverPlaying, Is.True);
            Assert.That(fixture.Bgm.FeverCueCount, Is.Zero);
        }

        private static LabSnapshot Snapshot(double now, bool paused = false, uint round = 1, double feverAt = -1d) =>
            new LabSnapshot { sessionId = "muted-bgm-stage-358", round = round, phase = LabPhase.Playing,
                serverTime = now, playedSeconds = now, paused = paused, feverTimeAt = feverAt };

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject passiveRoot;
            private readonly GameObject audioRoot;
            private readonly LabConfig config;
            private readonly object manager;
            private readonly Type managerType;
            private readonly object localClient;
            private bool disposed;
            public readonly LabNetwork Network;
            public readonly LabBattleBgm Bgm;
            public readonly AudioSource[] Sources;

            public Fixture(bool gameFlow, LabStageYokai stage)
            {
                passiveRoot = new GameObject("Passive BGM Guest #358");
                passiveRoot.SetActive(false);
                config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
                Assert.That(config, Is.Not.Null);
                managerType = typeof(LabNetwork).GetField("manager", Instance).FieldType;
                manager = passiveRoot.AddComponent(managerType);
                Network = passiveRoot.AddComponent<LabNetwork>();
                Network.enabled = false;
                typeof(LabNetwork).GetField("manager", Instance).SetValue(Network, manager);
                Network.config = config;
                Assert.That(Network.SetGameFlowEnabled(gameFlow), Is.True);
                Network.ObserveGameStage(stage);
                localClient = managerType.GetProperty("LocalClient", Instance).GetValue(manager);
                SetClient("IsServer", false);
                SetClient("IsClient", true);
                SetClient("IsConnected", true);
                managerType.GetProperty("IsListening", Instance).SetValue(manager, true);
                Assert.That(Network.IsHost, Is.False);
                Assert.That(Network.IsConnected, Is.True, "Passive connection flags only; no transport starts.");

                audioRoot = new GameObject("Muted Battle BGM #358");
                audioRoot.SetActive(false);
                Bgm = audioRoot.AddComponent<LabBattleBgm>();
                Bgm.Bind(Network);
                audioRoot.SetActive(true); // Awake loads the real clips and creates all three sources.
                Sources = audioRoot.GetComponents<AudioSource>();
                Assert.That(Sources, Has.Length.EqualTo(3));
                foreach (AudioSource source in Sources) source.mute = true; // Before the first Update/Play.
            }

            public void SetSnapshot(LabSnapshot snapshot) =>
                typeof(LabNetwork).GetField("latest", Instance).SetValue(Network, snapshot);

            private void SetClient(string name, bool value) =>
                localClient.GetType().GetProperty(name, Instance).SetValue(localClient, value);

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (audioRoot != null)
                {
                    foreach (AudioSource source in Sources) if (source != null) source.Stop();
                    audioRoot.SetActive(false);
                    Object.Destroy(audioRoot);
                }
                // Reset fake flags before destroying the passive manager; never ask an unstarted transport to shut down.
                managerType.GetProperty("IsListening", Instance).SetValue(manager, false);
                SetClient("IsConnected", false);
                SetClient("IsClient", false);
                Object.Destroy(passiveRoot);
                Object.Destroy(config);
            }
        }
    }
}
