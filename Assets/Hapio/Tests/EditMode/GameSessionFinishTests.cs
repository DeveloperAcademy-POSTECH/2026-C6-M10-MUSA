using System;
using System.Linq;
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

        [Test]
        public void AGuestThatReceivedTheSameFinalStampsInOtherBatchesStillMatchesTheHost()
        {
            using (var fixture = new Fixture())
            {
                LabPaintSurface host = StampedSurface(Stamps(6)), guest = StampedSurface(Stamps(2), Stamps(4), Stamps(6));
                Assert.That(guest.VisualVersion, Is.Not.EqualTo(host.VisualVersion), "Each device counts its own changes.");

                fixture.UsePaint(host);
                Set(fixture.Session, "finishIdentity", fixture.Invoke("MakeFinishIdentity", fixture.Network.Snapshot));
                fixture.UsePaint(guest);
                Assert.That((bool)fixture.Invoke("FinalPaintMatches"), Is.True);
            }
        }

        [Test]
        public void AGuestStillMissingTheFinalStampDoesNotMatchTheHost()
        {
            using (var fixture = new Fixture())
            {
                LabPaintSurface host = StampedSurface(Stamps(6)), guest = StampedSurface(Stamps(5));

                fixture.UsePaint(host);
                Set(fixture.Session, "finishIdentity", fixture.Invoke("MakeFinishIdentity", fixture.Network.Snapshot));
                fixture.UsePaint(guest);
                Assert.That((bool)fixture.Invoke("FinalPaintMatches"), Is.False);
            }
        }

        private static LabPaintSurface StampedSurface(params LabPaintVisualStamp[][] batches)
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 2 }, 1);
            foreach (LabPaintVisualStamp[] batch in batches) Assert.That(surface.ReplaceVisualStamps(batch), Is.True);
            return surface;
        }

        private static LabPaintVisualStamp[] Stamps(int count) => Enumerable.Range(1, count).Select(i => new LabPaintVisualStamp
        {
            sequence = (uint)i, center = Vector3.forward, right = Vector3.right, radius = .1f,
            color = new Color32(100, 110, 120, 255), painterSlot = 1, pattern = LabPaintPattern.Splatter
        }).ToArray();

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

        [Test]
        public void TheReplayRecordIsAdoptedOnlyWhenItDecodes()
        {
            using (var fixture = new Fixture())
            {
                fixture.Invoke("AdoptFinishReplay", Record(LabFinishReplayShot.MaximumPoses).Encode());
                Assert.That(fixture.Session.FinishUsesReplay, Is.True);
                fixture.Invoke("AdoptFinishReplay", "not a record");
                Assert.That(fixture.Session.FinishUsesReplay, Is.False, "A malformed record keeps the fixed finish.");
                fixture.Invoke("AdoptFinishReplay", string.Empty);
                Assert.That(fixture.Session.FinishUsesReplay, Is.False, "The Host's empty record means the fixed finish.");
                fixture.Invoke("AdoptFinishReplay", Record(LabFinishReplayShot.MaximumPoses).Encode());
                fixture.Invoke("ResetPaintFinish");
                Assert.That(fixture.Session.FinishUsesReplay, Is.False, "The next battle starts without the old record.");
            }
        }

        [Test]
        public void TheLargestReplayRecordFitsTheFinishControlLimit()
        {
            Type type = typeof(GameSession).GetNestedType("PaintFinishControl", BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null);
            object control = Activator.CreateInstance(type, true);
            Set(control, "kind", "paint_finish_commit");
            Set(control, "runId", Guid.NewGuid().ToString("N"));
            Set(control, "sessionId", Guid.NewGuid().ToString("N"));
            Set(control, "stageIndex", 3); Set(control, "paintCount", 32768);
            Set(control, "columns", 256); Set(control, "rows", 128);
            Set(control, "revision", uint.MaxValue); Set(control, "round", uint.MaxValue);
            Set(control, "paintRevision", uint.MaxValue); Set(control, "visualSequence", uint.MaxValue);
            Set(control, "generation", uint.MaxValue);
            Set(control, "startsAt", 123456.789012345d); Set(control, "deadline", 123468.789012345d);
            Set(control, "replay", Record(LabFinishReplayShot.MaximumPoses).Encode());
            string json = JsonUtility.ToJson(control);
            // ReceivePaintFinishControl refuses anything longer than 4096 characters.
            Assert.That(json.Length, Is.LessThan(4096), json.Length.ToString());
        }

        [Test]
        public void MatchingFailureIsCopiedWithoutReplacingPreviousSuccessfulStageRecords()
        {
            using (var fixture = new Fixture())
            {
                fixture.CaptureMetadataResult();
                Assert.That(fixture.Session.StageResults.Count, Is.EqualTo(1));
                fixture.Session.State.stageIndex = 3;
                fixture.Session.State.battleRound++;
                fixture.Session.State.phase = GameFlowPhase.GameOver;
                fixture.Session.State.retryableDefeat = true;
                var failed = new LabSnapshot { phase = LabPhase.Defeat, paintCoverage = .78f,
                    sessionId = fixture.Session.State.battleSessionId, round = fixture.Session.State.battleRound,
                    players = Array.Empty<LabPlayerState>() };
                Set(fixture.Network, "latest", failed);
                fixture.Invoke("CacheResultSnapshot");
                Assert.That(fixture.Session.ResultSnapshot, Is.Not.SameAs(failed));
                Assert.That(fixture.Session.ResultSnapshot.phase, Is.EqualTo(LabPhase.Defeat));
                Assert.That(fixture.Session.ResultSnapshot.paintCoverage, Is.EqualTo(.78f));
                Assert.That(fixture.Session.StageResults.Count, Is.EqualTo(1), "Failed attempts are not completed stages.");
                failed.paintCoverage = 0;
                Assert.That(fixture.Session.ResultSnapshot.paintCoverage, Is.EqualTo(.78f));
                Assert.That(fixture.HasCapturedResult, Is.False, "Failure never fabricates a completed-gourd preview.");
                Assert.That(fixture.Session.CanRetryStage, Is.False, "A disconnected passive fixture cannot retry.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnUnrelatedOrAbandonedFailureSnapshotCannotSupplyResultData(bool normalDefeat)
        {
            using (var fixture = new Fixture())
            {
                fixture.Session.State.phase = GameFlowPhase.GameOver;
                fixture.Session.State.retryableDefeat = normalDefeat;
                Set(fixture.Network, "latest", new LabSnapshot { phase = LabPhase.Defeat,
                    sessionId = fixture.Session.State.battleSessionId,
                    round = normalDefeat ? fixture.Session.State.battleRound + 1 : fixture.Session.State.battleRound });
                fixture.Invoke("CacheResultSnapshot");
                Assert.That(fixture.Session.ResultSnapshot, Is.Null);
                Assert.That(fixture.Session.StageResults, Is.Empty);
            }
        }

        [Test]
        public void ResultPresentationBusyIsLocalAndClearedAtTheNextGeneration()
        {
            using (var fixture = new Fixture())
            {
                fixture.Session.State.phase = GameFlowPhase.StageResult;
                fixture.Session.SetResultPresentationBusy(true);
                Assert.That(fixture.Session.IsAnyResultPresentationBusy, Is.True);
                Assert.That(fixture.Session.IsViewerOpen, Is.False, "Reading a story must not open a gourd preview.");
                fixture.Session.SetResultPresentationBusy(false);
                Assert.That(fixture.Session.IsAnyResultPresentationBusy, Is.False);
                fixture.Session.SetResultPresentationBusy(true);
                fixture.Invoke("ResetResultAdvance");
                Assert.That(fixture.Session.IsAnyResultPresentationBusy, Is.False);
                fixture.Session.State.phase = GameFlowPhase.Playing;
                fixture.Session.SetResultPresentationBusy(true);
                Assert.That(fixture.Session.IsAnyResultPresentationBusy, Is.False);
            }
        }

        private static LabFinishReplayShot Record(int poses)
        {
            var shot = new LabFinishReplayShot
            {
                Owner = ulong.MaxValue, Slot = 5, Element = LabOrbElement.Earth, Radius = .165f, FlightSeconds = .8f,
                Gravity = 30f, LaunchPosition = new Vector3(-123.456f, 0.85f, 14.123f), LaunchVelocity = new Vector3(1.2345f, 7.89f, -6.54f),
                HitPoint = new Vector3(0.1234f, 1.5678f, 10.4321f), HitNormal = Vector3.forward, ImpactVelocity = new Vector3(1f, -3f, -6f),
                PoseStart = -LabFinishReplayShot.PoseBefore, Poses = new LabHangingTarget.RigState[poses]
            };
            for (int i = 0; i < poses; i++)
                shot.Poses[i] = new LabHangingTarget.RigState
                    { Swing = new Vector2(-12.3456f, 23.4567f), Yaw = -12345.678f, RopeOffset = .0123f, PivotHeight = 1.75f };
            return shot;
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

            public void UsePaint(LabPaintSurface surface)
            {
                Set(Network, "clientPaintSurface", surface);
                Set(Network, "latest", new LabSnapshot
                {
                    phase = LabPhase.Victory, sessionId = Session.State.battleSessionId, round = Session.State.battleRound,
                    paintRevision = 9, paintCount = 100, paintColumns = 256, paintRows = 128,
                    players = Array.Empty<LabPlayerState>()
                });
            }

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
