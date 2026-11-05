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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// #507: on the Stage 2 Yagwanggwi gourd, a real thrown orb that the Host approves paints the cell in the direction it
    /// hit. The 100% right after it replays that throw on the live gourd (body and lid drawn, never hidden behind a copy),
    /// and the result preview copies the baked body and lid. Saved Home → DEV solo → Stage 2, real board gestures and
    /// physics; solo production binding only. The fixed copy finish without a recent hit is in GameFinishReplayTests.
    /// </summary>
    [Category("Slow")]
    public sealed class GameStage2GourdHitFinishTests
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
            Scene cleanup = SceneManager.CreateScene("Stage 2 hit finish cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARealThrowPaintsTheStageTwoGourdAndTheFinishCopiesItsBodyAndLid()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(2);

            // Deterministic orbs: every generation is a Secret Box that opens into two Combined orbs with one tap.
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
            string reloadedFrom = session.State.battleSessionId;
            Assert.That(session.ApplyDeveloperSettings(values, out string error), Is.True, error);
            // Applying reloads the current stage; wait for the fresh battle, not the one still on screen.
            yield return Until(() => session.State.battleSessionId != reloadedFrom && session.State.phase == GameFlowPhase.Playing
                && session.State.stageIndex == 2 && !session.IsLoadingScene && network.HasBattleReferences
                && network.Snapshot != null && !network.Snapshot.paused, "The reloaded Stage 2 battle");

            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.Rig, Is.Not.Null, "Stage 2 uses the baked Yagwanggwi gourd.");
            LabTarget target = network.target;
            var approved = new List<Vector3>();
            Action<Vector3, LabOrbElement> onHit = (local, element) => approved.Add(local);
            target.ApprovedHit += onHit;
            try
            {
                int paintsBefore = network.PaintSurface.PaintCount;
                bool hit = false;
                int throws = 0;
                // The release speed is the last 0.12 s of travel (LabOrbBoard.ReleaseSampleWindow), so the strength is the
                // final flick's length in board widths. Try a few; any approved hit counts.
                float[] flicks = { .6f, .5f, .7f, .55f, .65f, .45f };
                for (int attempt = 0; attempt < 3 && !hit; attempt++)
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
                        yield return GameFinishReplayTests.WaitReplayPoseWindow(target.GetComponent<LabHangingTarget>());
                        target.GetComponent<LabHangingTarget>().ResetSwing();
                        yield return Until(() => network.board.GetComponentsInChildren<LabOrbView>().Any(v => v.OrbId == shot), "Orb on board");
                        int hits = approved.Count;
                        Launch(network.board, shot, Time.timeAsDouble, flicks[throws++ % flicks.Length]);
                        yield return Until(() => network.Snapshot.orbs.All(orb => orb.id != shot), "Shot resolves", 8d);
                        for (int i = 0; i < 5; i++) yield return null;
                        TestContext.WriteLine("shot " + shot + ": " + network.LastShotReport);
                        if (approved.Count > hits) { hit = true; break; }
                    }
                }
                Assert.That(hit, Is.True, "A real throw must hit the Stage 2 gourd (last: " + network.LastShotReport + ").");
                Assert.That(network.PaintSurface.PaintCount, Is.GreaterThan(paintsBefore), "The approved hit paints.");

                // The Host paints in the hitbox-local direction; that is the paint shell's direction too.
                Vector3 local = approved[approved.Count - 1];
                Vector3 world = target.Hitbox.transform.TransformPoint(local);
                Vector3 direction = local.normalized;
                Assert.That(Vector3.Distance(direction, paint.Shell.InverseTransformPoint(world).normalized), Is.LessThan(1e-4f));
                Assert.That(Painted(network.PaintSurface, direction), Is.True, "The cell where the orb hit is painted.");
                yield return null;
                paint.RefreshCommittedMap();
                foreach (Renderer renderer in new Renderer[] { paint.Rig.paintShell, paint.PaintExtras[0] })
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(paint.VisualPaintTexture), renderer.name);
                }
            }
            finally { target.ApprovedHit -= onHit; }

            // 100%: the final-hit replay draws the live body and lid; the result preview copies them.
            LabModel model = (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);
            // Real Host paint hits spread over the sphere until every cell is covered (no forged victory snapshot).
            ulong painter = model.CopyPlayers().Single().id;
            for (int i = 0; i < 4000 && !model.PaintSurface.IsComplete; i++)
            {
                float y = 1f - 2f * ((i % 997) + .5f) / 997f, radius = Mathf.Sqrt(1f - y * y), angle = i * 2.39996323f;
                model.PaintSurface.RegisterHit(new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle)),
                    model.PaintColorFor(LabOrbElement.Fire), painter, 1);
            }
            // Paint grows from each hit over the Host's ticks; wait for it to cover the gourd.
            yield return Until(() => model.PaintSurface.IsComplete || session.IsFinishingBattle,
                "Real paint hits cover the gourd", 60d);
            LabFinishReplay replay = null;
            yield return Until(() => (replay = session.FinishReplay) != null && replay.Running
                || session.State.phase == GameFlowPhase.StageResult, "The final-hit replay starts");
            Assert.That(session.FinishUsesReplay && replay.Running, Is.True, "The real hit just before 100% is replayed.");
            Assert.That(GameObject.Find("Completed paint · presentation only"), Is.Null);
            foreach (Renderer renderer in new Renderer[] { paint.Rig.paintShell, paint.PaintExtras[0] })
                Assert.That(renderer.forceRenderingOff || !renderer.enabled, Is.False, "The live " + renderer.name + " draws.");
            Assert.That(target.GetComponentsInChildren<Renderer>().Any(r => r.name.StartsWith("Rear_Rope")), Is.False);

            yield return Until(() => session.IsFinishingBattle && session.PreviewTexture != null, "Completed preview capture");
            var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview", Private).GetValue(session);
            Assert.That(preview, Is.Not.Null);
            string[] previewParts = preview.ModelRoot.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name).ToArray();
            Assert.That(previewParts, Does.Contain("Paint Shell"), "The result preview shows the Yagwanggwi body.");
            Assert.That(previewParts, Does.Contain("Lid Paint"));
            Assert.That(previewParts.Any(n => n.StartsWith("Rear_Rope")), Is.False);
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult, "Stage 2 result loads", 30d);
            Assert.That(session.PreviewTexture, Is.Not.Null, "The Stage 2 preview survives the Battle unload.");
        }

        /// <summary>Whether the cell in this direction (or one next to it) holds paint. Same mapping as the paint shader.</summary>
        private static bool Painted(LabPaintSurface surface, Vector3 direction)
        {
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            float u = Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI) + .5f, v = .5f - direction.y * .5f;
            int column = Mathf.Clamp((int)(u * surface.Columns), 0, surface.Columns - 1);
            int row = Mathf.Clamp((int)(v * surface.Rows), 0, surface.Rows - 1);
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = Mathf.Clamp(row + dr, 0, surface.Rows - 1);
                    int c = ((column + dc) % surface.Columns + surface.Columns) % surface.Columns;
                    if (pixels[r * surface.Columns + c].a > 0) return true;
                }
            return false;
        }

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
            // The real Host binds UDP in both address families. A TCP loopback probe
            // does not establish that the same number is free for either UDP listener.
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
                        // Only the disposable probes retry. Production starts once with
                        // both probe sockets released, and real bind errors stay visible.
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }

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
