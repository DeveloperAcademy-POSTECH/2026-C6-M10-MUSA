using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// The story finish replays the final real throw three times from different angles, on the live completed gourd,
    /// with its own camera over a hidden HUD, then opens the result under the cover. A new tap skips it on this device;
    /// a 100% with no recent throw keeps the fixed 1.2 s finish. Saved Home → DEV solo, real board gestures and physics.
    /// </summary>
    [Category("Slow")]
    public sealed class GameFinishReplayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home");
            session = GameSession.Instance; network = session.Network;
            yield return null;
            network.port = AvailableDualFamilyPort();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                network.Stop(); Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Final hit replay cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFinalThrowIsReplayedThreeTimesThenTheResultOpensUnderTheCover([Values(1, 3)] int stage)
        {
            yield return ReachStage(stage);
            yield return DeterministicOrbs(stage);
            yield return ThrowUntilHit();
            LabModel model = Model();
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            var hanging = network.target.GetComponent<LabHangingTarget>();
            CompletePaint(model);

            LabFinishReplay replay = null;
            yield return Until(() => (replay = session.FinishReplay) != null && replay.Running
                || session.State.phase != GameFlowPhase.Playing, "The final-hit replay starts", 60d);
            Assert.That(session.FinishUsesReplay && replay.Running, Is.True, "A recent real hit is replayed.");
            LabPlayerState before = Copy(model.CopyPlayers().Single());
            uint paintRevision = network.Snapshot.paintRevision;
            int paintCount = network.PaintSurface.PaintCount;

            // Its own full-screen camera; the battle cameras and every battle HUD canvas are off; the live gourd draws.
            Assert.That(replay.ReplayCamera != null && replay.ReplayCamera.enabled, Is.True);
            Assert.That(replay.ReplayCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(network.BattleCamera.enabled, Is.False);
            Canvas[] visibleBattleCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.enabled && c.gameObject.scene == network.target.gameObject.scene && c != replay.Overlay)
                .ToArray();
            Assert.That(visibleBattleCanvases.Select(c => c.name), Is.Empty, "The battle HUD is hidden.");
            Assert.That(GameObject.Find("Completed paint · presentation only"), Is.Null, "No copy hides the live gourd.");
            foreach (Renderer renderer in new[] { paint.Shell.GetComponent<Renderer>() }.Concat(paint.PaintExtras))
                Assert.That(renderer.forceRenderingOff, Is.False, renderer.name);
            Assert.That(hanging.PresentationPoseActive, Is.True, "The recorded pose is drawn.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Global time is never slowed.");

            var passes = new HashSet<int>();
            bool ghostChecked = false;
            float cover = 0f;
            int impacts = 0;
            double startedAt = Time.realtimeSinceStartupAsDouble, coveredAt = -1d;
            while (SceneManager.GetActiveScene().path == GameScenePaths.Battle)
            {
                if (Time.realtimeSinceStartupAsDouble - startedAt > 20d) Assert.Fail("The replay never handed over to the result.");
                if (replay != null)
                {
                    passes.Add(replay.CurrentPass);
                    impacts = replay.ImpactsPresented;
                    LabProjectile ghost = replay.Ghost;
                    if (ghost != null)
                    {
                        var body = ghost.GetComponent<Rigidbody>();
                        Assert.That(body.isKinematic && !body.detectCollisions, Is.True, "The ghost orb has no physics.");
                        Assert.That(ghost.GetComponent<SphereCollider>().enabled, Is.False);
                        Assert.That(ghost.IsResolved, Is.False);
                        Assert.That(ghost.ClayFlightActive, Is.True, "The real story replay uses the battle's current clay flight look.");
                        Assert.That(replay.ClayVfx.ManualClock, Is.True, "Replay clay follows the shared presentation clock.");
                        ghostChecked = true;
                    }
                    if (replay.Running)
                    {
                        // Presentation only: the model's round record and the paint map never move while it plays.
                        LabPlayerState now = model.CopyPlayers().Single();
                        Assert.That(now.hitTotal, Is.EqualTo(before.hitTotal));
                        Assert.That(now.throwTotal, Is.EqualTo(before.throwTotal));
                        Assert.That(now.damageTotal, Is.EqualTo(before.damageTotal));
                        Assert.That(now.lastHit, Is.EqualTo(before.lastHit));
                        Assert.That(network.Snapshot.paintRevision, Is.EqualTo(paintRevision));
                        Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(paintCount));
                    }
                }
                cover = Mathf.Max(cover, session.SceneFade.Alpha);
                if (coveredAt < 0d && session.SceneFade.Alpha >= .999f) coveredAt = Time.realtimeSinceStartupAsDouble;
                yield return null;
            }
            Assert.That(passes.Where(p => p >= 0).OrderBy(p => p), Is.EqualTo(new[] { 0, 1, 2 }), "Three angles.");
            Assert.That(impacts, Is.EqualTo(3), "The impact is shown once per pass.");
            Assert.That(ghostChecked, Is.True, "The flying orb was redrawn.");
            Assert.That(cover, Is.GreaterThanOrEqualTo(.999f), "The result opens under the cover.");
            Assert.That(coveredAt - startedAt, Is.GreaterThan(LabFinishReplay.Duration - .6d),
                "Unskipped, the whole replay plays before the cover closes.");
            Assert.That(session.SceneFade.CoverColor.grayscale, Is.GreaterThan(.5f), "A paper-coloured cover, not black.");

            // Stage 3 is the last story stage: its result is the completed run, on the same result Scene.
            yield return Until(() => (session.State.phase == GameFlowPhase.StageResult || session.State.phase == GameFlowPhase.Completed)
                && !session.IsLoadingScene && SceneManager.GetActiveScene().path == GameScenePaths.StageResult,
                "The stage result loads (phase " + session.State.phase + ")", 30d);
            yield return Until(() => session.SceneFade.Alpha <= 0f, "The cover lifts on the result", 3d);
            Assert.That(session.SceneFade.Status, Is.Null);
            Assert.That(session.ResultSnapshot.paintRevision, Is.EqualTo(paintRevision));
        }

        [UnityTest]
        public IEnumerator ANewTapSkipsTheReplayOnThisDeviceAndTheSoloResultFollows()
        {
            yield return ReachStage(1);
            yield return DeterministicOrbs(1);
            yield return ThrowUntilHit();
            CompletePaint(Model());
            LabFinishReplay replay = null;
            yield return Until(() => (replay = session.FinishReplay) != null && replay.Running && replay.Elapsed > .4f
                || session.State.phase != GameFlowPhase.Playing, "The final-hit replay runs", 60d);
            Assert.That(replay != null && replay.Running, Is.True);
            // A batch-mode Editor has no focused Game view; let the test mouse's events through like a device.
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            double skippedAt = 0d;
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
                yield return Until(() => replay == null || replay.Skipped, "A new tap skips the replay", 2d);
                skippedAt = Time.realtimeSinceStartupAsDouble;
                Assert.That(replay.Skipped && replay.Complete, Is.True);
                Assert.That(replay.Elapsed, Is.LessThan(LabFinishReplay.Duration - 1f));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            }
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult, "The solo result follows the skip", 10d);
            Assert.That(Time.realtimeSinceStartupAsDouble - skippedAt, Is.LessThan(3d), "Solo does not wait out the replay.");
        }

        [UnityTest]
        public IEnumerator WithoutARecentFinalHitTheFixedFinishCopiesTheGourd()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            CompletePaint(Model());
            GameObject copy = null;
            yield return Until(() => (copy = GameObject.Find("Completed paint · presentation only")) != null
                || session.State.phase == GameFlowPhase.StageResult, "The fixed finish starts", 60d);
            Assert.That(copy, Is.Not.Null, "No thrown hit to replay: the fixed finish copies the gourd.");
            Assert.That(session.FinishUsesReplay, Is.False);
            string[] parts = copy.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name).ToArray();
            Assert.That(parts.Any(n => n.StartsWith("Paint Shell")), Is.True);
            Assert.That(parts.Any(n => n.StartsWith("Lid Paint")), Is.True);
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult, "The stage result loads", 30d);
            GameStageStoryPresentation story = null;
            yield return Until(() => (story = Object.FindAnyObjectByType<GameStageStoryPresentation>()) != null, "The result story", 10d);
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                Assert.That(story.statusText.gameObject.activeInHierarchy, Is.False, "The approved result design has no status line.");
            }
        }

        private IEnumerator ReachStage(int stage)
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            for (int next = 2; next <= stage; next++)
            {
                Assert.That(network.PauseBattle(), Is.True);
                Assert.That(session.DeveloperNextStage(), Is.True);
                yield return WaitPlaying(next);
            }
        }

        private IEnumerator DeterministicOrbs(int stage)
        {
            // Every generation is a Secret Box that opens into two Combined orbs with one tap.
            session.Pause();
            float[] values = network.config.CaptureDeveloperValues();
            Set(values, "Orb Generation Cost", 1f);
            Set(values, "Secret Box Chance (%)", 100f);
            Set(values, "Yin Chance (%)", 0f);
            Set(values, "Yang Chance (%)", 0f);
            Set(values, "Relay Orb Chance (%)", 0f);
            Set(values, "Potion Chance (%)", 0f);
            Set(values, "Bomb Chance (%)", 0f);
            Set(values, "Item Unlock Swipes Min", 0f);
            Set(values, "Item Unlock Swipes Max", 0f);
            Set(values, "Spawn Pop Duration (seconds)", 0f);
            Set(values, "Spawn Minimum Speed (board widths/s)", 0f);
            Set(values, "Spawn Maximum Speed (board widths/s)", 0f);
            Set(values, "Drum Drift Speed (board widths/s)", 0f);
            // Stage 3: no talisman or fire curtain over the board while the test throws.
            if (stage == 3) values[228] = 0f;
            string reloadedFrom = session.State.battleSessionId;
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            yield return Until(() => session.State.battleSessionId != reloadedFrom && session.State.phase == GameFlowPhase.Playing
                && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
                && network.Snapshot != null && !network.Snapshot.paused, "The reloaded stage " + stage + " battle");
            Assert.That(network.target.GetComponent<LabGourdPaint>().Rig, Is.Not.Null, "The stage's baked gourd.");
        }

        private IEnumerator ThrowUntilHit()
        {
            LabTarget target = network.target;
            int approved = 0;
            Action<Vector3, LabOrbElement> onHit = (local, element) => approved++;
            target.ApprovedHit += onHit;
            try
            {
                int throws = 0;
                float[] flicks = { .6f, .5f, .7f, .55f, .65f, .45f };
                for (int attempt = 0; attempt < 4 && approved == 0; attempt++)
                {
                    int generated = network.Snapshot.players.Single().generatedTotal;
                    network.Generate();
                    yield return Until(() => network.Snapshot.players.Single().generatedTotal > generated, "Generate");
                    LabOrbState box = network.Snapshot.orbs.First(orb => orb.kind == LabOrbKind.SecretBox);
                    yield return Until(() => network.board.GetComponentsInChildren<LabOrbView>().Any(v => v.OrbId == box.id), "Box on board");
                    double now = Time.timeAsDouble;
                    TapOrb(network.board, box.id, now);
                    TapOrb(network.board, box.id, now + .2d);
                    yield return Until(() => network.Snapshot.orbs.Count(orb => orb.kind == LabOrbKind.Combined) >= 2, "Box opens");
                    foreach (string shot in network.Snapshot.orbs.Where(orb => orb.kind == LabOrbKind.Combined).Select(orb => orb.id).ToArray())
                    {
                        yield return WaitReplayPoseWindow(target.GetComponent<LabHangingTarget>());
                        target.GetComponent<LabHangingTarget>().ResetSwing();
                        yield return Until(() => network.board.GetComponentsInChildren<LabOrbView>().Any(v => v.OrbId == shot), "Orb on board");
                        Launch(network.board, shot, Time.timeAsDouble, flicks[throws++ % flicks.Length]);
                        yield return Until(() => network.Snapshot.orbs.All(orb => orb.id != shot), "Shot resolves", 8d);
                        for (int i = 0; i < 5; i++) yield return null;
                        TestContext.WriteLine("shot " + shot + ": " + network.LastShotReport);
                        if (approved > 0) break;
                    }
                }
            }
            finally { target.ApprovedHit -= onHit; }
            Assert.That(approved, Is.GreaterThan(0), "A real throw must hit the gourd (last: " + network.LastShotReport + ").");
        }

        /// <summary>Real Host paint hits spread over the sphere until every cell is covered (no forged victory snapshot).</summary>
        private static void CompletePaint(LabModel model)
        {
            ulong painter = model.CopyPlayers().Single().id;
            for (int i = 0; i < 4000 && !model.PaintSurface.IsComplete; i++)
            {
                float y = 1f - 2f * ((i % 997) + .5f) / 997f, radius = Mathf.Sqrt(1f - y * y), angle = i * 2.39996323f;
                model.PaintSurface.RegisterHit(new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle)),
                    model.PaintColorFor(LabOrbElement.Fire), painter, 1);
            }
        }

        private LabModel Model() => (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);

        private static LabPlayerState Copy(LabPlayerState value) => JsonUtility.FromJson<LabPlayerState>(JsonUtility.ToJson(value));

        private static void TapOrb(LabOrbBoard board, string id, double now)
        {
            Vector3 position = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id).Body.position;
            var point = new Vector2(position.x, position.y);
            Assert.That(board.TryBeginDragAtWorld(point, now), Is.True);
            Assert.That(board.EndDragAtWorld(point, now + .05d), Is.True);
        }

        private static void Launch(LabOrbBoard board, string id, double now, float flick)
        {
            LabOrbView orb = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == id);
            var from = new Vector2(orb.Body.position.x, orb.Body.position.y);
            Vector2 aim = board.AreaShape.ToWorld(new Vector2(.5f, .25f), orb.HitSphere.radius);
            Assert.That(board.TryBeginDragAtWorld(from, now), Is.True);
            Assert.That(board.DragToWorld(aim, now + 1d), Is.True);
            Assert.That(board.EndDragAtWorld(aim + Vector2.up * (board.CenterBounds.width * flick), now + 1.04d), Is.True);
        }

        private static void Set(float[] values, string label, float value)
        {
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count).Single(i => LabConfig.DeveloperFields[i].Label == label);
            values[index] = value;
        }

        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }

        /// <summary>
        /// The replay only keeps a hit whose pose window (PoseBefore before impact) is still recorded. The gourd starts
        /// its pose history around the start of play, so with the short test start (GameTestStartTiming) a throw right
        /// after Playing can land before that window exists. A real final hit cannot come that early.
        /// </summary>
        internal static IEnumerator WaitReplayPoseWindow(LabHangingTarget hanging) =>
            Until(() => hanging.PoseHistoryCount > 1
                && Time.fixedTimeAsDouble - hanging.OldestPoseTime >= LabFinishReplayShot.PoseBefore + .1d,
                "The gourd keeps the replay's pre-impact pose window", 5d);

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Stage " + stage + " playing");

        private static IEnumerator Until(Func<bool> condition, string reason, double seconds = 60d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(reason);
                yield return null;
            }
        }
    }
}
