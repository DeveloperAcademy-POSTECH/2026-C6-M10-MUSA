using System;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>Passive session gates only: no transport, Scene activation, effect or GPU preview is started.</summary>
    public sealed class GameSessionFinishTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ResultCaptureReusesTheFirstVictoryBudgetWithoutStartingAnotherWait()
        {
            using (var fixture = new Fixture())
            {
                fixture.Invoke("ObserveFinishDeadline", 0d, 10d);
                Assert.That(fixture.CaptureDeadline(20d), Is.EqualTo(22d));
                Assert.That(fixture.CaptureDeadline(21d), Is.EqualTo(22d));
                fixture.Invoke("ObserveFinishDeadline", 0d, 21d);
                Assert.That(fixture.CaptureDeadline(21d), Is.EqualTo(22d));
                Assert.That(fixture.CaptureDeadline(40d), Is.EqualTo(22d),
                    "An expired finish budget must not become a fresh result-capture budget.");
            }
        }

        [Test]
        public void ResetRetiresThePreviousFinishBudgetBeforeTheNextBattle()
        {
            using (var fixture = new Fixture())
            {
                fixture.Invoke("ObserveFinishDeadline", 0d, 10d);
                Assert.That(fixture.CaptureDeadline(21d), Is.EqualTo(22d));
                fixture.Invoke("ResetPaintFinish");
                fixture.Invoke("ObserveFinishDeadline", 0d, 50d);
                Assert.That(fixture.CaptureDeadline(60d), Is.EqualTo(62d));
                Assert.That(fixture.CaptureDeadline(61d), Is.EqualTo(62d));
            }
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1d)]
        public void StateControlsRejectMalformedSharedFinishDeadlines(double deadline)
        {
            using (var fixture = new Fixture())
            {
                object control = fixture.Invoke("MakeControl", "state", fixture.Session.State.Copy());
                Set(control, "finishDeadline", 22d);
                Assert.That((bool)fixture.Invoke("ValidStateControl", control), Is.True,
                    "The same battle/control identity must be accepted before changing only its deadline.");
                Set(control, "finishDeadline", deadline);
                Assert.That((bool)fixture.Invoke("ValidStateControl", control), Is.False);
            }
        }

        [Test]
        public void CapturedResultSurvivesTheFlowRevisionChangeAndNoLongerNeedsTheLiveSnapshot()
        {
            using (var fixture = new Fixture())
            {
                fixture.CaptureMetadataResult();
                Assert.That(fixture.HasCapturedResult, Is.True);
                Assert.That(fixture.Session.ResultSnapshot, Is.Not.SameAs(fixture.Network.Snapshot));

                fixture.Session.State.revision++;
                fixture.Session.State.phase = GameFlowPhase.StageResult;
                Set(fixture.Network, "latest", null);

                Assert.That(fixture.HasCapturedResult, Is.True,
                    "Playing-to-result revisions do not change the captured battle identity.");
                Assert.That((bool)fixture.Invoke("CanCaptureResult"), Is.True,
                    "A preserved result must not wait for the live battle snapshot again.");
            }
        }

        [TestCase("run")]
        [TestCase("stage")]
        [TestCase("session")]
        [TestCase("round")]
        [TestCase("paintRevision")]
        public void CapturedResultCannotBeReusedForAMismatchedIdentity(string changedPart)
        {
            using (var fixture = new Fixture())
            {
                fixture.CaptureMetadataResult();
                Assert.That(fixture.HasCapturedResult, Is.True);
                switch (changedPart)
                {
                    case "run": fixture.Session.State.runId = "another-run"; break;
                    case "stage": fixture.Session.State.stageIndex++; break;
                    case "session": fixture.Session.ResultSnapshot.sessionId = "another-battle"; break;
                    case "round": fixture.Session.ResultSnapshot.round++; break;
                    case "paintRevision": fixture.Session.ResultSnapshot.paintRevision++; break;
                    default: Assert.Fail("Unknown identity dimension."); break;
                }
                Assert.That(fixture.HasCapturedResult, Is.False, changedPart);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly LabConfig config;
            public readonly GameSession Session;
            public readonly LabNetwork Network;

            public Fixture()
            {
                root = new GameObject("Inactive Hapio finish gate fixture");
                root.SetActive(false);
                var prefab = Resources.Load<GameObject>(GameSession.ResourcePath);
                Assert.That(prefab, Is.Not.Null);
                GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform);
                Session = instance.GetComponent<GameSession>();
                Assert.That(Session, Is.Not.Null);
                Network = Session.Network;
                Assert.That(Network, Is.Not.Null);
                Assert.That(Network.IsConnected, Is.False);

                config = ScriptableObject.CreateInstance<LabConfig>();
                // This fixture verifies immutable result metadata without allocating a render preview.
                Set(config, "completedGourdViewerEnabled", false);
                Network.config = config;
                Set(Session, "stageCatalog", GameStageCatalog.RequireDefault());
                Set(Session, "expectedSettingsRevision", 1);
                Set(Session, "expectedSettingsFingerprint", "finish-gate-config");
                Set(Session, "state", new GameFlowState
                {
                    runId = "finish-gate-run", revision = 4, stageIndex = 2,
                    mode = GameMode.Story, phase = GameFlowPhase.Playing,
                    battleSessionId = "finish-gate-battle", battleRound = 7
                });
            }

            public object Invoke(string name, params object[] arguments)
            {
                MethodInfo method = typeof(GameSession).GetMethod(name, Private);
                Assert.That(method, Is.Not.Null, name);
                return method.Invoke(Session, arguments);
            }

            public double CaptureDeadline(double now) => (double)Invoke("ResultCaptureDeadline", now);
            public bool HasCapturedResult => (bool)Invoke("HasCapturedResult");

            public void CaptureMetadataResult()
            {
                Set(Network, "latest", new LabSnapshot
                {
                    phase = LabPhase.Victory,
                    sessionId = Session.State.battleSessionId,
                    round = Session.State.battleRound,
                    paintRevision = Network.PaintSurface.Revision,
                    players = Array.Empty<LabPlayerState>()
                });
                Invoke("CaptureResult");
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
