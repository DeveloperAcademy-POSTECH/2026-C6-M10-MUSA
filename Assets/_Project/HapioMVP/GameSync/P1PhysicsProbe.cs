using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Explicit build21 Mac pair diagnostic. Never runs on an ordinary launch, Editor, or iOS.</summary>
    public sealed class P1PhysicsProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6P1ProbeDirectory") < 0) return;
            var game = FindAnyObjectByType<T10GameSession>();
            if (game != null) game.gameObject.AddComponent<P1PhysicsProbe>();
        }
        private T10GameSession game;
        private T09BattleController controller;
        private string output, shared, role, port, runtimeError;
        private bool ownsOutput;
        private int pointer = -31000;
        private Report report;
        private readonly List<Proof> proofs = new List<Proof>();
        private readonly HashSet<string> proofKeys = new HashSet<string>();
        private readonly List<TransferProof> transfers = new List<TransferProof>();
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private string Peer => role == "host" ? "client" : "host";

        private void Update()
        {
            var snapshot = game?.Snapshot;
            if (snapshot == null) return;
            string key = snapshot.sessionId + "/" + snapshot.roundId + "/" + snapshot.revision;
            if (proofKeys.Add(key)) proofs.Add(new Proof { session = snapshot.sessionId, round = snapshot.roundId,
                revision = snapshot.revision, hash = GameWire.CanonicalHash(snapshot) });
        }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = Arg(args, "-c6P1ProbeDirectory", null); role = Arg(args, "-c6P1Role", "host");
            port = Arg(args, "-c6P1Port", "25141");
            report = new Report { role = role, startedAtUtc = DateTime.UtcNow.ToString("O"), buildGuid = Application.buildGUID,
                unity = Application.unityVersion };
            string error = null;
            double deadline = Now + 70;
            try
            {
                Require(Path.IsPathRooted(output) && Path.GetFileName(output) == role, "Absolute role-named output required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output must be empty.");
                Require(role == "host" || role == "client", "Invalid role.");
                Directory.CreateDirectory(output); ownsOutput = true; shared = Directory.GetParent(output).FullName;
                game = GetComponent<T10GameSession>(); controller = game.Controller;
                Require(game.BuildIdentifier == "21" && game.TransfersEnabled && controller.OrbPhysicsEnabled
                    && game.InterruptionHandlingEnabled, "Build21 P1 scene wiring required.");
                Application.runInBackground = true; Application.logMessageReceived += Observe;
            }
            catch (Exception exception) { error = exception.ToString(); }
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object next = null; bool more = false;
                try
                {
                    Require(Now < deadline, "P1 probe exceeded its 70-second overall deadline.");
                    if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                    more = stack.Peek().MoveNext();
                    if (more) next = stack.Peek().Current; else stack.Pop();
                }
                catch (Exception exception) { error = exception.ToString(); }
                if (error != null) break;
                if (more && next is IEnumerator nested) stack.Push(nested);
                else if (more) yield return next;
            }
            report.status = error == null ? "PASS" : "FAIL"; report.error = error;
            report.finishedAtUtc = DateTime.UtcNow.ToString("O"); report.proofs = proofs.ToArray(); report.transfers = transfers.ToArray();
            Application.logMessageReceived -= Observe;
            if (game != null) game.Leave();
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_P1_PROBE_COMPLETE role=" + role + " status=" + report.status + " error=" + error);
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6P1Quit") >= 0) Application.Quit(error == null ? 0 : 1);
        }
        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            report.width = Screen.width; report.height = Screen.height;
            Require(!game.Attached && controller.Views.Count == 0 && !controller.DevelopmentSolo
                && controller.Layout.Config.BattleDurationSeconds == 180, "Probe requires normal empty two-player setup.");
            Require(role == "host" ? game.Lobby.CreateRoom("C6 P1 physics", port) : game.Lobby.JoinDirect("127.0.0.1", port), "Lobby request failed.");
            if (role == "host")
            {
                yield return Wait(() => game.Lobby.Connected, 12, "Host lobby missing.");
                Write("host-open.json", new Marker { value = "ready" });
            }
            yield return Wait(() => game.Lobby.CanReady, 18, "Lobby config acknowledgement missing.");
            Require(game.Lobby.ToggleReady(), "Ready request failed.");
            if (role == "host")
            {
                yield return Wait(() => game.Lobby.CanStart, 15, "Both Ready missing.");
                Require(game.Lobby.StartMatch(), "Host Start refused.");
            }
            yield return Wait(() => game.InitialConfirmed && controller.CanInteract, 15, "Playing confirmation missing.");
            Require(controller.Views.Count == 0 && controller.Resource.LocalPlayer.stamina == 100, "Start is not empty/full.");
            report.session = game.Snapshot.sessionId; report.room = game.Snapshot.roomId; report.seed = game.Snapshot.seed;
            report.localPlayer = controller.Attack.LocalPlayerId;
            Require(controller.RequestGenerate(), "Normal Generate refused.");
            yield return Wait(() => !controller.Resource.HasPending && controller.Views.Count == 1, 6, "Generation confirmation missing.");
            var generation = controller.Resource.LastResult;
            Require(generation != null && generation.known && generation.accepted
                && Math.Abs(generation.staminaBefore - generation.staminaAfter - 20) < .00001, "Normal generation must cost 20.");
            report.normalGenerated = 1; report.normalGenerateCost = generation.staminaBefore - generation.staminaAfter;
            string local = controller.Views.Keys.Single(); report.normalOrbId = local;
            yield return Barrier("generated");
            yield return Wait(() => game.Snapshot.attack.orbs.Length == 2, 6, "Two paid orbs not synchronized.");
            yield return Coast(local);
            yield return Capture("01-normal-coasting");
            yield return Barrier("coast-complete");
            if (role == "host") Write("transfer-id.json", new Marker { value = local });
            yield return Wait(() => Exists("transfer-id.json"), 5, "Transfer identity missing.");
            string raw = Read<Marker>("transfer-id.json").value;
            yield return Pass(raw, 1, "host", OrbActionKind.TransferRight);
            yield return Pass(raw, 2, "client", OrbActionKind.TransferLeft);
            if (role == "host")
            {
                // Explicit geometry fixtures are separate from the two paid, normal-Seed generations above.
                var yin = controller.Attack.Registry.RegisterDevelopmentOrb(report.localPlayer, OrbKind.Raw, OrbPolarity.Yin, new Vector2(.3f, .8f));
                var yang = controller.Attack.Registry.RegisterDevelopmentOrb(report.localPlayer, OrbKind.Raw, OrbPolarity.Yang, new Vector2(.6f, .8f));
                report.explicitFixtureRawCount = 2;
                controller.Attack.PublishInventoryChange("p1-explicit-yin-yang-geometry-fixture");
                yield return Wait(() => controller.Views.ContainsKey(yin.OrbId) && controller.Views.ContainsKey(yang.OrbId), 5, "Fixture views missing.");
                Physics2D.SyncTransforms();
                Vector2 start = controller.GetViewScreenPosition(yin.OrbId), end = controller.GetViewScreenPosition(yang.OrbId);
                int p = --pointer; Require(controller.BeginPointer(p, start, false), "Fixture combination grab failed.");
                controller.MovePointer(p, end); yield return new WaitForSecondsRealtime(.15f);
                Require(controller.Combination.LastResult == null && controller.Views.ContainsKey(yin.OrbId), "Held contact combined automatically.");
                controller.EndPointer(p, end);
                yield return Wait(() => !controller.HasCombinationPending && controller.Combination.LastResult != null, 6, "Fixture combination unresolved.");
                Require(controller.Combination.LastResult.accepted, "Fixture combination rejected: " + controller.Combination.LastResult.reason);
                string combined = controller.Combination.LastResult.originalCombined.id;
                Require(controller.OrbPhysics.TryGetVelocity(combined, out var initial) && initial.sqrMagnitude < .0001f, "New Combined inherited motion.");
                report.combinedId = combined; Write("combined-id.json", new Marker { value = combined });
                Physics2D.SyncTransforms(); start = controller.GetViewScreenPosition(combined);
                end = new Vector2(start.x, controller.Layout.BottomPixelRect.yMax + 15f);
                p = --pointer; Require(controller.BeginPointer(p, start, false), "Combined launch grab failed.");
                controller.MovePointer(p, end);
                Require(!controller.Views.ContainsKey(combined) && !controller.OrbPhysics.TryGetVelocity(combined, out _), "P1 old boundary launch kept local body.");
                var projectile = FindObjectsByType<HostProjectile3D>().Single(body => body.OrbId == combined);
                Require(!projectile.Body.isKinematic && projectile.Body.linearVelocity.magnitude > 0, "No actual Host projectile.");
                report.actualHostProjectileObserved = true; controller.EndPointer(p, end);
            }
            yield return Wait(() => Exists("combined-id.json"), 12, "Combined identity missing.");
            report.combinedId = Read<Marker>("combined-id.json").value;
            yield return Wait(() => game.Snapshot?.attack.hp == 80 && game.Snapshot.attack.roundHits == 1
                && !game.Snapshot.attack.orbs.Any(o => o.id == report.combinedId), 10, "Shared actual hit state missing.");
            Require(game.Error.Length == 0 && controller.TouchBegins == 0, "Runtime error or automated pointer mislabeled physical Touch.");
            report.sharedHp80 = true; report.finalGame = Copy(game.Snapshot);
            yield return Capture("02-shared-real-hit");
            yield return Barrier("final-proof");
            game.Leave();
            yield return Wait(() => !game.Attached && !game.Lobby.Connected && game.Snapshot == null
                && controller.Views.Count == 0 && controller.OrbPhysics.Count == 0 && controller.Attack.ActiveProjectileCount == 0
                && !controller.HasCombinationPending && !controller.Gestures.HasPending && game.Lobby.Connection.CanStart, 6, "END did not clear local bodies and connection.");
            report.finalClean = true;
        }
        private IEnumerator Coast(string id)
        {
            Physics2D.SyncTransforms();
            var view = controller.Views[id]; Vector2 start = controller.GetViewScreenPosition(id);
            int p = --pointer; Require(controller.BeginPointer(p, start, false), "Coast grab failed.");
            float travel = Mathf.Min(controller.OrbGridScreenRect.width * .12f, 55f);
            for (int i = 1; i <= 4; i++)
            {
                yield return new WaitForSecondsRealtime(.02f);
                controller.MovePointer(p, start + Vector2.right * (travel * i / 4f));
                Require(!controller.Gestures.HasPending && view.HeldFeedbackActive, "Ordinary drag became an action or lost held feedback.");
            }
            controller.EndPointer(p, start + Vector2.right * travel);
            Require(controller.OrbPhysics.TryGetVelocity(id, out var velocity) && velocity.x > .01f, "Controller Up produced no inertia.");
            report.releaseVelocity = velocity; Vector2 from = view.transform.position;
            double until = Now + .12d;
            while (Now < until) yield return null;
            Require(ReferenceEquals(view, controller.Views[id]) && view.transform.position.x > from.x + .001f, "Ordinary snapshots stopped/replaced the coasting view.");
            Require(controller.Attack.Snapshot.orbs.Single(o => o.id == id).state == (int)OrbAuthorityState.Idle
                && controller.Combination.LastResult == null && controller.SentTransfers == 0 && controller.Attack.Snapshot.hp == 100,
                "Passive movement changed gameplay state.");
            report.coastDistance = Vector2.Distance(from, view.transform.position); report.coastPreserved = true;
            Physics2D.SyncTransforms(); p = --pointer;
            Require(controller.BeginPointer(p, controller.GetViewScreenPosition(id), false), "Moving orb regrab failed.");
            controller.CancelPointer(p);
            Require(controller.OrbPhysics.TryGetVelocity(id, out velocity) && velocity.sqrMagnitude < .0001f, "Cancel retained inertia.");
            report.cancelStopped = true;
        }
        private IEnumerator Pass(string id, ulong count, string sender, OrbActionKind direction)
        {
            string key = "pass-" + count;
            yield return Barrier(key + "-ready");
            if (role == sender)
            {
                yield return Wait(() => controller.CanInteract && controller.Views.ContainsKey(id), 5, "Sender view missing.");
                Vector2 center = ClearCenter(id);
                Physics2D.SyncTransforms(); int p = --pointer;
                Require(controller.BeginPointer(p, controller.GetViewScreenPosition(id), false), "Transfer reposition grab failed.");
                controller.MovePointer(p, center); controller.EndPointer(p, center);
                yield return null;
                Vector2 start = controller.GetViewScreenPosition(id);
                Rect lower = controller.Layout.BottomPixelRect;
                Vector2 edge = new Vector2(direction == OrbActionKind.TransferLeft ? lower.xMin + lower.width * .015f : lower.xMax - lower.width * .015f, start.y);
                p = --pointer; Require(controller.BeginPointer(p, start, false), "Transfer grab failed.");
                for (int i = 1; i <= 6; i++)
                { controller.MovePointer(p, Vector2.Lerp(start, edge, i / 6f)); Require(!controller.Gestures.HasPending, "Transfer occurred before Up."); yield return null; }
                controller.EndPointer(p, edge);
            }
            yield return Wait(() => controller.Attack.Snapshot.orbs.Any(o => o.id == id && o.transferCount == count), 7, "Transfer count missing.");
            var wire = controller.Attack.Snapshot.orbs.Single(o => o.id == id);
            bool receiving = role != sender;
            var proof = new TransferProof { orbId = id, count = count, direction = direction.ToString(), receiver = receiving,
                owner = wire.owner, entrySide = wire.entrySide };
            if (receiving)
            {
                yield return Wait(() => controller.Views.ContainsKey(id), 5, "Receiving body missing.");
                Require(wire.owner == report.localPlayer && controller.OrbPhysics.TryGetVelocity(id, out var velocity)
                    && velocity.sqrMagnitude < .0001f, "Arrival inherited sender momentum.");
                proof.receivedStopped = true;
                Vector2 position = controller.Views[id].transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                Require(Vector2.Distance(position, controller.Views[id].transform.position) < .01f
                    && controller.Attack.Snapshot.orbs.Single(o => o.id == id).transferCount == count,
                    "Arrival moved or automatically returned.");
            }
            else Require(!controller.Views.ContainsKey(id) && !controller.OrbPhysics.TryGetVelocity(id, out _), "Sender kept a duplicate local body.");
            Require(wire.entrySide == (int)(direction == OrbActionKind.TransferLeft ? EntrySide.Right : EntrySide.Left), "Opposite entry side not preserved.");
            transfers.Add(proof); yield return Barrier(key + "-complete");
        }
        private Vector2 ClearCenter(string id)
        {
            Rect rect = controller.OrbGridScreenRect;
            foreach (float y in new[] { .45f, .7f, .25f, .85f })
            {
                Vector2 point = rect.min + Vector2.Scale(new Vector2(.5f, y), rect.size);
                if (controller.Views.Keys.Where(other => other != id).All(other => Vector2.Distance(point, controller.GetViewScreenPosition(other))
                    > Screen.width * controller.Layout.Config.CombinationRadiusFraction * 1.4f)) return point;
            }
            throw new InvalidOperationException("No clear interior point.");
        }
        private IEnumerator Capture(string label)
        {
            yield return null; yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(output, label + ".json"), JsonUtility.ToJson(game.Snapshot, true));
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, label + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        private IEnumerator Barrier(string key)
        { Write(key + "-" + role + ".json", new Marker { value = "ready" }); yield return Wait(() => Exists(key + "-" + Peer + ".json"), 10, "Peer barrier missing: " + key); }
        private bool Exists(string path) => File.Exists(Path.Combine(shared, path));
        private T Read<T>(string path) => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(shared, path)));
        private void Write<T>(string name, T value)
        {
            string destination = Path.Combine(shared, name), temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true)); File.Move(temporary, destination);
        }
        private void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) runtimeError = text; }
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static IEnumerator Wait(Func<bool> condition, float seconds, string error)
        { double deadline = Now + seconds; while (!condition() && Now < deadline) yield return null; Require(condition(), error); }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        private static string Arg(string[] args, string key, string fallback)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        [Serializable] private sealed class Marker { public string value; }
        [Serializable] private sealed class Proof { public string session, hash; public uint round; public ulong revision; }
        [Serializable] private sealed class TransferProof
        { public string orbId, direction; public ulong count, owner; public int entrySide; public bool receiver, receivedStopped; }
        [Serializable] private sealed class Report
        {
            public string role, status, error, startedAtUtc, finishedAtUtc, buildGuid, unity, session, room, normalOrbId, combinedId;
            public string scope = "MAC_TWO_PROCESS_DIRECT_IP_NORMAL_GENERATION_LOCAL_PHYSICS_EXPLICIT_COMBINATION_FIXTURE";
            public string pointerScope = "Synthetic controller Begin/Move/Up samples; no physical Touch or P2 release-based 3D throw.";
            public bool physicalDevice, bonjourValidated, syntheticFocusMaintained;
            public bool coastPreserved, cancelStopped, actualHostProjectileObserved, sharedHp80, finalClean;
            public int width, height, normalGenerated, explicitFixtureRawCount;
            public double normalGenerateCost; public float coastDistance; public Vector2 releaseVelocity;
            public uint seed; public ulong localPlayer; public Proof[] proofs; public TransferProof[] transfers; public GameSnapshot finalGame;
        }
#endif
    }
}
