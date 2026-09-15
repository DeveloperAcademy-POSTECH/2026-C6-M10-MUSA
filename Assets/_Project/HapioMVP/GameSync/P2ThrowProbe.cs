using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Explicit build22 Mac diagnostic. Saved wiring is inert without its development launch arguments.</summary>
    [DisallowMultipleComponent]
    public sealed class P2ThrowProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6P2ProbeDirectory") < 0) return;
            var game = FindAnyObjectByType<T10GameSession>();
            if (game != null && game.GetComponent<P2ThrowProbe>() == null) game.gameObject.AddComponent<P2ThrowProbe>();
        }
        private T10GameSession game;
        private T09BattleController controller;
        private ThrowBattleFraming framing;
        private string output, shared, role, port, runtimeError;
        private bool ownsOutput, active;
        private int pointer = -32000;
        private Report report;
        private readonly List<Proof> proofs = new List<Proof>();
        private readonly HashSet<string> proofKeys = new HashSet<string>();
        private readonly Dictionary<string, MotionProof> motions = new Dictionary<string, MotionProof>();
        private readonly List<RecoveryProof> recoveries = new List<RecoveryProof>();
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private string Peer => role == "host" ? "client" : "host";

        private void Update()
        {
            if (!active) return;
            var snapshot = game?.Snapshot;
            if (snapshot == null) return;
            string key = snapshot.sessionId + "/" + snapshot.roundId + "/" + snapshot.revision;
            if (proofKeys.Add(key)) proofs.Add(new Proof { session = snapshot.sessionId, round = snapshot.roundId,
                revision = snapshot.revision, hash = GameWire.CanonicalHash(snapshot) });
            if (role == "host")
            {
                foreach (var body in FindObjectsByType<HostProjectile3D>())
                {
                    if (body.HasCompleted || body.Body == null) continue;
                    if (!motions.TryGetValue(body.OrbId, out var item))
                    {
                        item = new MotionProof { orbId = body.OrbId, attacker = body.AttackerPlayerId,
                            ballistic = body.BallisticActive, dynamicBody = !body.Body.isKinematic,
                            initialVelocity = body.InitialVelocity, gravity = body.GravityVector, lifetime = body.Lifetime };
                        motions.Add(body.OrbId, item);
                    }
                    item.samples++; item.maximumCollisionCallbacks = Math.Max(item.maximumCollisionCallbacks, body.CollisionCallbackCount);
                    if (body.Body.linearVelocity.y < -.05f) item.descendingObserved = true;
                    // This scene has only the target and the non-target floor as static 3D colliders.
                    // A target hit completes immediately; a still-live collision near the floor is ground contact.
                    if (body.CollisionCallbackCount > 0 && body.Body.position.y < controller.Layout.Config.ThrowFloorY + body.Radius + .25f)
                    {
                        item.groundContactObserved = true;
                        if (item.descendingObserved && body.Body.linearVelocity.y > .05f) item.bounceObserved = true;
                    }
                }
            }
            else
            {
                var displays = FindObjectsByType<BallisticProjectileDisplay>();
                if (displays.Length > 0)
                {
                    report.clientDisplayFrames++;
                    // CreatePrimitive's collider is disabled immediately and destroyed at frame end.
                    // Its deferred-destruction presence is not participation in gameplay physics.
                    if (displays.Any(value => value.GetComponentsInChildren<Collider>().Any(collider => collider.enabled && collider.gameObject.activeInHierarchy)
                        || value.GetComponentInChildren<Rigidbody>() != null))
                        runtimeError = "A Client ballistic presentation acquired gameplay physics.";
                }
                if (FindObjectsByType<HostProjectile3D>().Length > 0) runtimeError = "Client created an authoritative projectile.";
            }
        }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = Arg(args, "-c6P2ProbeDirectory", null);
            if (output == null) yield break;
            role = Arg(args, "-c6P2Role", "host"); port = Arg(args, "-c6P2Port", "25142");
            report = new Report { role = role, startedAtUtc = DateTime.UtcNow.ToString("O"), buildGuid = Application.buildGUID,
                unity = Application.unityVersion };
            string error = null; double deadline = Now + 100;
            try
            {
                Require(role == "host" || role == "client", "Invalid role.");
                Require(Path.IsPathRooted(output) && Path.GetFileName(output) == role, "Absolute role-named output required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output must be empty.");
                Directory.CreateDirectory(output); ownsOutput = true; shared = Directory.GetParent(output).FullName;
                game = GetComponent<T10GameSession>(); controller = game.Controller;
                framing = GetComponent<ThrowBattleFraming>();
                Require(game.BuildIdentifier == "22" && game.TransfersEnabled && controller.OrbPhysicsEnabled
                    && controller.ReleaseThrowsEnabled && game.InterruptionHandlingEnabled && framing != null,
                    "Build22 P2 scene wiring required.");
                Application.runInBackground = true; Application.logMessageReceived += Observe; active = true;
            }
            catch (Exception exception) { error = exception.ToString(); }
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object next = null; bool more = false;
                try
                {
                    Require(Now < deadline, "P2 probe exceeded its 100-second overall deadline.");
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
            report.finishedAtUtc = DateTime.UtcNow.ToString("O"); report.proofs = proofs.ToArray();
            report.motions = motions.Values.ToArray(); report.recoveries = recoveries.ToArray();
            Application.logMessageReceived -= Observe; active = false;
            if (game != null) game.Leave();
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_P2_PROBE_COMPLETE role=" + role + " status=" + report.status + " error=" + error);
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6P2Quit") >= 0) Application.Quit(error == null ? 0 : 1);
        }
        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            var args = Environment.GetCommandLineArgs();
            report.requestedWindowPosition = new Vector2Int(int.Parse(Arg(args, "-c6P2WindowX", role == "host" ? "20" : "450")),
                int.Parse(Arg(args, "-c6P2WindowY", "60")));
            var windowDisplay = Screen.mainWindowDisplayInfo;
            var move = Screen.MoveMainWindowTo(windowDisplay, report.requestedWindowPosition);
            yield return Wait(() => move == null || move.isDone, 5, "Window placement did not complete.");
            report.actualWindowPosition = Screen.mainWindowPosition;
            report.width = Screen.width; report.height = Screen.height;
            Require(!game.Attached && controller.Views.Count == 0 && !controller.DevelopmentSolo
                && controller.Layout.Config.BattleDurationSeconds == 180, "Normal empty two-player setup required.");
            Require(role == "host" ? game.Lobby.CreateRoom("C6 P2 throws", port) : game.Lobby.JoinDirect("127.0.0.1", port), "Lobby request failed.");
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
            yield return Generate();
            report.normalOrbId = controller.Resource.LastResult.confirmedOrb.id;
            yield return Barrier("generated");
            yield return Wait(() => game.Snapshot.attack.orbs.Length == 2, 6, "Two paid orbs not synchronized.");
            yield return Wait(() => framing.FitSucceeded, 5, "Monster never fit the HUD-free area: " + framing.FitStatus);
            report.framingFits = true; report.framingStatus = framing.FitStatus;
            report.effectiveScreenRect = framing.EffectiveScreenRect; report.projectedMonsterRect = framing.ProjectedMonsterRect;
            yield return Capture("01-normal-framing");
            if (role == "host")
            {
                ulong other = game.Snapshot.resources.players.Single(player => player.playerId != report.localPlayer).playerId;
                var hostOrb = controller.Attack.Registry.RegisterDevelopmentOrb(report.localPlayer, OrbKind.Combined, OrbPolarity.None, new Vector2(.5f, .55f));
                var clientOrb = controller.Attack.Registry.RegisterDevelopmentOrb(other, OrbKind.Combined, OrbPolarity.None, new Vector2(.5f, .55f));
                report.explicitFixtureCombinedCount = 2;
                controller.Attack.PublishInventoryChange("p2-explicit-two-owner-combined-throw-fixture");
                Write("fixtures.json", new Fixtures { host = hostOrb.OrbId, client = clientOrb.OrbId });
            }
            yield return Wait(() => Exists("fixtures.json"), 8, "Combined fixture identities missing.");
            var fixtures = Read<Fixtures>("fixtures.json");
            report.hitOrbId = fixtures.host; report.missOrbId = fixtures.client;
            string local = role == "host" ? fixtures.host : fixtures.client;
            yield return Wait(() => controller.Views.ContainsKey(local) && controller.CanInteract, 6, "Owned fixture view missing.");
            yield return HoldAndCancel(local);
            yield return Barrier("held-cancelled");
            if (role == "host")
            {
                // A further paid Generate creates headroom for the real +5 reward; no stamina fixture is injected.
                yield return Generate();
                yield return SwipeRelease(fixtures.host, 0f);
                Require(!controller.Views.ContainsKey(fixtures.host) && !controller.OrbPhysics.TryGetVelocity(fixtures.host, out _),
                    "Host kept a duplicate local body after approved launch.");
                Require(FindObjectsByType<HostProjectile3D>().Any(body => body.OrbId == fixtures.host && body.BallisticActive),
                    "The Host did not create an actual ballistic Rigidbody.");
                Write("center-released.json", new Marker { value = fixtures.host });
            }
            else yield return Wait(() => Exists("center-released.json"), 8, "Host center release missing.");
            yield return Wait(() => controller.Attack.Snapshot.projectiles.Any(body => body.id == fixtures.host && body.ballistic),
                4, "Approved center ballistic pose missing.");
            yield return Capture("03-center-projectile");
            yield return Wait(() => game.Snapshot.attack.hp == 80 && game.Snapshot.attack.roundHits == 1
                && !game.Snapshot.attack.orbs.Any(orb => orb.id == fixtures.host), 8, "Actual center throw did not produce shared HP80.");
            if (role == "host")
            {
                Require(recoveries.Count == 1 && recoveries[0].orbId == fixtures.host && recoveries[0].attacker == report.localPlayer
                    && recoveries[0].accepted && !recoveries[0].duplicate && Math.Abs(recoveries[0].added - 5) < .00001,
                    "The real hit did not produce exactly one full attacker +5 reward receipt.");
                Require(controller.Attack.Registry.TryGet(fixtures.host, out var consumed) && consumed.AuthorityState == OrbAuthorityState.Consumed,
                    "Hit did not finish as Consumed in the Host registry.");
            }
            report.sharedHp80 = true;
            yield return Capture("04-shared-real-hit");
            yield return Barrier("center-hit-complete");
            if (role == "client")
            {
                yield return SwipeRelease(fixtures.client, 1.4f);
                Write("lateral-released.json", new Marker { value = fixtures.client });
            }
            else yield return Wait(() => Exists("lateral-released.json"), 8, "Client lateral release missing.");
            yield return Wait(() => controller.Attack.Snapshot.projectiles.Any(body => body.id == fixtures.client && body.ballistic),
                5, "The valid lateral swipe was not approved as a ballistic throw.");
            yield return Wait(() => !controller.Views.ContainsKey(fixtures.client), 4, "Client kept a duplicate local view after launch approval.");
            yield return Capture("05-lateral-projectile");
            yield return Wait(() => !game.Snapshot.attack.orbs.Any(orb => orb.id == fixtures.client)
                && game.Snapshot.attack.projectiles.Length == 0, controller.Layout.Config.ThrowLifetime + 4, "Missed throw did not expire and disappear.");
            Require(game.Snapshot.attack.hp == 80 && game.Snapshot.attack.roundHits == 1, "Ground/miss changed HP or counted as a hit.");
            if (role == "host")
            {
                Require(controller.Attack.Registry.TryGet(fixtures.client, out var expired) && expired.AuthorityState == OrbAuthorityState.Consumed,
                    "Miss did not finish as Consumed in the Host registry.");
                Require(motions.TryGetValue(fixtures.client, out var miss) && miss.groundContactObserved && miss.bounceObserved,
                    "Actual missed throw did not show live floor contact and an upward rebound.");
                Require(recoveries.Count == 1, "The missed throw gave an additional hit recovery.");
            }
            report.sharedMissConsumed = true;
            Require(game.Error.Length == 0 && controller.TouchBegins == 0, "Runtime error or synthetic pointer mislabeled physical Touch.");
            if (role == "client") Require(report.clientDisplayFrames > 1, "No sustained Client ballistic presentation observed.");
            report.finalGame = Copy(game.Snapshot);
            yield return Capture("06-expired-and-clean-inventory");
            yield return Barrier("final-proof");
            game.Leave();
            yield return Wait(() => !game.Attached && !game.Lobby.Connected && game.Snapshot == null
                && controller.Views.Count == 0 && controller.OrbPhysics.Count == 0 && controller.Attack.ActiveProjectileCount == 0
                && !controller.HasCombinationPending && !controller.Gestures.HasPending && game.Lobby.Connection.CanStart, 6,
                "END did not clear local bodies and connection.");
            report.finalClean = true;
        }
        private IEnumerator Generate()
        {
            int count = controller.Resource.LocalPlayer.generatedTotal;
            Require(controller.RequestGenerate(), "Normal Generate refused.");
            yield return Wait(() => !controller.Resource.HasPending && controller.Resource.LocalPlayer.generatedTotal == count + 1, 6,
                "Normal generation confirmation missing.");
            var receipt = controller.Resource.LastResult;
            Require(receipt != null && receipt.known && receipt.accepted
                && Math.Abs(receipt.staminaBefore - receipt.staminaAfter - 20) < .00001, "Normal generation must cost 20.");
            report.normalGenerated++; report.normalGenerateCost = receipt.staminaBefore - receipt.staminaAfter;
        }
        private IEnumerator HoldAndCancel(string id)
        {
            Physics2D.SyncTransforms(); int p = --pointer;
            Require(controller.BeginPointer(p, controller.GetViewScreenPosition(id), false), "Combined grab failed.");
            Vector2 upper = new Vector2(Screen.width * .5f, controller.Layout.BottomPixelRect.yMax + Screen.width * .05f);
            controller.MovePointer(p, upper);
            yield return new WaitForSecondsRealtime(.25f);
            Require(controller.ThrowArmed && controller.Views.ContainsKey(id) && !controller.Gestures.HasPending
                && controller.Attack.Snapshot.projectiles.Length == 0 && controller.Attack.Snapshot.hp == 100,
                "Held upper state reserved, consumed, or spawned an orb before release.");
            Require(FindObjectsByType<ThrowHeldPreview>().Any(value => value.Visible), "Held upper preview missing.");
            report.heldUpperPreserved = true;
            yield return Capture("02-held-upper-preview");
            controller.CancelPointer(p);
            yield return null;
            Require(controller.Views.ContainsKey(id) && !controller.ThrowArmed && !controller.Gestures.HasActivePointer
                && !controller.Gestures.HasPending && controller.OrbPhysics.TryGetVelocity(id, out var velocity)
                && velocity.sqrMagnitude < .0001f && !FindObjectsByType<ThrowHeldPreview>().Any(value => value.Visible),
                "Cancel did not preserve the owned, stopped orb and hide the preview.");
            Require(controller.Hud.ActionLabel.text != "THROW READY", "Cancelled preview left the armed HUD status behind.");
            report.cancelPreserved = true;
        }
        private IEnumerator SwipeRelease(string id, float horizontalWidthsPerSecond)
        {
            Physics2D.SyncTransforms(); int p = --pointer;
            Require(controller.BeginPointer(p, controller.GetViewScreenPosition(id), false), "Throw grab failed.");
            Vector2 start = new Vector2(Screen.width * .5f, controller.Layout.BottomPixelRect.yMax - Screen.width * .025f);
            controller.MovePointer(p, start);
            yield return new WaitForSecondsRealtime(.16f);
            double began = Time.unscaledTimeAsDouble;
            Vector2 end = start;
            do
            {
                yield return null;
                float elapsed = (float)(Time.unscaledTimeAsDouble - began);
                end = start + new Vector2(horizontalWidthsPerSecond, 1.25f) * (Screen.width * elapsed);
                Require(end.x > 0 && end.x < Screen.width && end.y < Screen.height, "Synthetic release left the display.");
                controller.MovePointer(p, end);
                Require(controller.Views.ContainsKey(id) && !controller.Gestures.HasPending,
                    "The throw was committed during Move rather than Up.");
                report.syntheticMovingSamples++;
            } while (Time.unscaledTimeAsDouble - began < .1);
            Require(controller.ThrowArmed, "The synthetic release never entered the upper area.");
            controller.EndPointer(p, end);
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
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) runtimeError = text;
            if (!text.StartsWith("C6_T07_HIT_RECOVERY ", StringComparison.Ordinal)) return;
            var values = text.Split(' ').Skip(1).Select(part => part.Split(new[] { '=' }, 2))
                .Where(pair => pair.Length == 2).ToDictionary(pair => pair[0], pair => pair[1]);
            if (!values.TryGetValue("orb", out var id) || !values.TryGetValue("attacker", out var player)
                || !values.TryGetValue("added", out var amount)) { runtimeError = "Unparseable actual hit recovery receipt."; return; }
            recoveries.Add(new RecoveryProof { orbId = id, attacker = ulong.Parse(player, CultureInfo.InvariantCulture),
                added = double.Parse(amount, CultureInfo.InvariantCulture), accepted = values["accepted"] == "True",
                duplicate = values["duplicate"] == "True" });
        }
        private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static IEnumerator Wait(Func<bool> condition, float seconds, string error)
        { double deadline = Now + seconds; while (!condition() && Now < deadline) yield return null; Require(condition(), error); }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        private static string Arg(string[] args, string key, string fallback)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        [Serializable] private sealed class Marker { public string value; }
        [Serializable] private sealed class Fixtures { public string host, client; }
        [Serializable] private sealed class Proof { public string session, hash; public uint round; public ulong revision; }
        [Serializable] private sealed class RecoveryProof { public string orbId; public ulong attacker; public double added; public bool accepted, duplicate; }
        [Serializable] private sealed class MotionProof
        {
            public string orbId; public ulong attacker; public bool ballistic, dynamicBody, descendingObserved, groundContactObserved, bounceObserved;
            public int samples, maximumCollisionCallbacks; public Vector3 initialVelocity, gravity; public float lifetime;
        }
        [Serializable] private sealed class Report
        {
            public string role, status, error, startedAtUtc, finishedAtUtc, buildGuid, unity, session, room, normalOrbId, hitOrbId, missOrbId, framingStatus;
            public string scope = "BUILD22_MAC_TWO_PROCESS_NORMAL_2PLAYER_WITH_EXPLICIT_COMBINED_FIXTURES";
            public string pointerScope = "DEBUG_POINTER: synthetic controller Begin/Move/Up over recent real frame times; not physical Touch.";
            public bool physicalDevice, bonjourValidated, framingFits, heldUpperPreserved, cancelPreserved, sharedHp80, sharedMissConsumed, finalClean;
            public int width, height, normalGenerated, explicitFixtureCombinedCount, syntheticMovingSamples, clientDisplayFrames;
            public double normalGenerateCost; public Rect effectiveScreenRect, projectedMonsterRect;
            public Vector2Int requestedWindowPosition, actualWindowPosition;
            public uint seed; public ulong localPlayer; public Proof[] proofs; public MotionProof[] motions; public RecoveryProof[] recoveries; public GameSnapshot finalGame;
        }
#endif
    }
}
