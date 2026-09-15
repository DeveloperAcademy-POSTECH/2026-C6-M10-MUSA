using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Resources;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    // Explicit local two-process evidence only; no ordinary, Editor, or iOS launch automation.
    public sealed class T13InterruptionProbe : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T13ProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T10GameSession>();
            if (owner != null && owner.GetComponent<T13InterruptionProbe>() == null)
                owner.gameObject.AddComponent<T13InterruptionProbe>();
        }

        private T10GameSession game;
        private T09BattleController controller;
        private string output, peerOutput, role, port, runtimeError;
        private bool ownsOutput;
        private Report report;
        private ResourceRequestPacket previousGeneration;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<ObservedProof> proofs = new List<ObservedProof>();
        private readonly HashSet<string> proofKeys = new HashSet<string>();
        private double nextInvalidControl;
        private bool invalidControlFault;
        private string invalidNonce;
        private int invalidControlsSent, maximumRejectedControls;
        private static double Now => Time.realtimeSinceStartupAsDouble;

        private void Update()
        {
            if (game == null) return;
            maximumRejectedControls = Math.Max(maximumRejectedControls, game.RejectedControls);
            var value = game.Snapshot;
            if (value != null)
            {
                string key = value.sessionId + "/" + value.roundId + "/" + value.revision;
                if (proofKeys.Add(key)) proofs.Add(new ObservedProof { session = value.sessionId, round = value.roundId,
                    revision = value.revision, phase = value.battle.phase, hash = GameWire.CanonicalHash(value) });
            }
            if (invalidControlFault && game.Attached && game.Lobby.Connected && value != null && Now >= nextInvalidControl)
            {
                nextInvalidControl = Now + .2;
                var control = new GameControl { kind = "QUERY", nonce = invalidNonce, roomId = value.roomId,
                    sessionId = value.sessionId, roundId = value.roundId, configHash = value.configHash };
                using (var writer = GameControl.Write(control)) controller.Attack.Connection.OwnedManager.CustomMessagingManager
                    .SendNamedMessage("C6.T10B.Control.v1", NetworkManager.ServerClientId, writer,
                        NetworkDelivery.ReliableFragmentedSequenced);
                invalidControlsSent++;
            }
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = Arg(args, "-c6T13ProbeDirectory", null);
            role = Arg(args, "-c6T13Role", "host");
            port = Arg(args, "-c6T13Port", "25133");
            report = new Report { role = role, startedAtUtc = DateTime.UtcNow.ToString("O"),
                buildGuid = Application.buildGUID, unity = Application.unityVersion };
            string error = null;
            try
            {
                Require(Path.IsPathRooted(output), "Absolute output directory required.");
                Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Output must be empty.");
                Require(role == "host" || role == "client", "Explicit host/client role required.");
                Require(Path.GetFileName(output) == role, "Output basename must equal role for the local barrier.");
                peerOutput = Path.Combine(Directory.GetParent(output).FullName, role == "host" ? "client" : "host");
                Directory.CreateDirectory(output); ownsOutput = true;
                game = GetComponent<T10GameSession>(); controller = GetComponent<T09BattleController>();
                Require(game != null && controller != null && game.InterruptionHandlingEnabled,
                    "T13 interruption-enabled scene is required.");
                Application.runInBackground = true;
                Application.logMessageReceived += Observe;
            }
            catch (Exception exception) { error = exception.ToString(); }
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object next = null; bool more = false;
                try
                {
                    if (runtimeError != null) throw new InvalidOperationException(runtimeError);
                    more = stack.Peek().MoveNext();
                    if (more) next = stack.Peek().Current; else stack.Pop();
                }
                catch (Exception exception) { error = exception.ToString(); }
                if (error != null) break;
                if (more && next is IEnumerator nested) stack.Push(nested);
                else if (more) yield return next;
            }
            report.status = error == null ? "PASS" : "FAIL";
            report.error = error; report.finishedAtUtc = DateTime.UtcNow.ToString("O");
            report.checkpoints = checkpoints.ToArray(); report.proofs = proofs.ToArray();
            report.invalidControlsSent = invalidControlsSent;
            report.maximumRejectedControls = maximumRejectedControls;
            if (ownsOutput) File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Observe;
            if (game != null) { game.DevelopmentHoldPeerResponses = false; game.Leave(); }
            Debug.Log("C6_T13_PROBE_COMPLETE role=" + role + " status=" + report.status + " error=" + error);
            yield return new WaitForSecondsRealtime(.5f);
            if (Array.IndexOf(args, "-c6T13ProbeQuit") >= 0) Application.Quit(error == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            Require(!game.Attached && !game.Lobby.Connected && controller.Views.Count == 0, "Ordinary scene auto-started.");
            Require(controller.Layout.Config.BattleDurationSeconds == 180 && !controller.DevelopmentSolo,
                "Probe requires normal two-player 180-second configuration.");
            yield return ConnectAndStart(1);
            report.firstSession = game.Snapshot.sessionId; report.firstRoom = game.Snapshot.roomId;
            report.firstNonce = controller.Attack.LocalNonce;
            yield return GenerateOne();
            yield return Barrier("first-generated", 12);
            yield return WaitFor(() => game.Snapshot.attack.orbs.Length == 2, 8, "First paid inventory was not synchronized.");
            Record("first-paid-inventory");

            // A synthetic focus callback is a regression check, not an iOS status-bar observation.
            double before = game.DisplayRemaining; string sessionBefore = game.Snapshot.sessionId;
            game.gameObject.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSecondsRealtime(1.2f);
            game.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            Require(game.Attached && game.Snapshot.sessionId == sessionBefore && game.DisplayRemaining < before - .5,
                "Synthetic focus loss ended the room or stopped its clock.");
            report.syntheticFocusMaintained = true; Record("synthetic-focus-kept-playing");
            // Capture before the shared barrier releases the Client's fault. Initial-ACK rejections are not fault evidence.
            report.rejectedControlsBeforeFault = game.RejectedControls;
            yield return Barrier("focus-complete", 10);
            if (role == "client")
            {
                invalidNonce = Guid.NewGuid().ToString("N");
                game.DevelopmentHoldPeerResponses = true;
                invalidControlFault = true;
                WriteMarker("fault-started");
            }
            yield return WaitFor(() => File.Exists(Path.Combine(role == "client" ? output : peerOutput, "fault-started.json")),
                10, "Fault-start marker missing.");
            double faultObservedAt = Now;
            yield return WaitFor(() => game.Error.Length != 0 && !game.Lobby.Connected, 16,
                "Peer response fault did not end the connection with an explicit network error.");
            report.faultToErrorSeconds = Now - faultObservedAt;
            report.watchdogInterruption = game.LastInterruptionReason;
            report.rejectedControlsAtWatchdog = Math.Max(maximumRejectedControls, game.RejectedControls);
            invalidControlFault = false; game.DevelopmentHoldPeerResponses = false;
            if (role == "host")
            {
                Require(report.faultToErrorSeconds >= 7 && report.faultToErrorSeconds <= 13,
                    "Host cleanup did not occur near the configured 8-second valid-response watchdog.");
                Require(report.rejectedControlsAtWatchdog > report.rejectedControlsBeforeFault,
                    "Client fault produced no new rejected controls beyond the pre-fault baseline.");
                Require(report.watchdogInterruption == "PEER_GAME_RESPONSE_TIMEOUT",
                    "Host ended for a different reason than the valid-response watchdog: " + report.watchdogInterruption);
            }
            else Require(invalidControlsSent > 0, "Client fault sent no explicitly invalid controls.");
            Require(!controller.CanInteract && !controller.RequestGenerate()
                && controller.Hud.Canvas.gameObject.activeSelf, "Network error did not stop gameplay on the error screen.");
            Record("watchdog-network-error");
            yield return Barrier("watchdog-error", 10);
            game.Leave(); // Explicit END-equivalent action; connection errors are not asserted to auto-return.
            yield return WaitFor(Clean, 8, "Explicit END did not clear the failed session and show Lobby.");
            Record("watchdog-clean-lobby");
            yield return Barrier("watchdog-clean", 10);
            yield return new WaitForSecondsRealtime(1.2f);
            Require(Clean(), "Session reconnected or restored itself without a new request.");
            report.noAutomaticReconnect = true;

            // Explicit new Create/Join calls model a user's fresh room; never resume or replay the old session.
            yield return ConnectAndStart(2);
            report.secondSession = game.Snapshot.sessionId; report.secondRoom = game.Snapshot.roomId;
            report.secondNonce = controller.Attack.LocalNonce;
            Require(report.secondSession != report.firstSession && report.secondRoom != report.firstRoom
                && report.secondNonce != report.firstNonce, "Fresh session retained an old room/session/nonce.");
            Require(EmptyFull(), "Fresh session restored old inventory or resource state.");
            Record("fresh-session-empty-full");
            if (role == "client")
            {
                Require(previousGeneration != null && previousGeneration.sessionId == report.firstSession
                    && previousGeneration.nonce == report.firstNonce, "Missing original request context for stale replay.");
                using (var writer = ResourceWire.Write(previousGeneration)) controller.Attack.Connection.OwnedManager.CustomMessagingManager
                    .SendNamedMessage("C6.T07.Generate.v1", NetworkManager.ServerClientId, writer,
                        NetworkDelivery.ReliableFragmentedSequenced);
                report.previousSessionRequestsSent = 1;
                WriteMarker("stale-request-sent");
            }
            yield return WaitFor(() => File.Exists(Path.Combine(role == "client" ? output : peerOutput, "stale-request-sent.json")),
                10, "Stale request marker missing.");
            yield return new WaitForSecondsRealtime(1.2f);
            Require(EmptyFull() && game.Attached, "Previous-session request revived an orb, charged stamina, or ended fresh session.");
            report.previousSessionRequestHadNoEffect = true; Record("previous-session-request-rejected");
            yield return Barrier("stale-request-checked", 10);
            yield return GenerateOne();
            yield return Barrier("second-generated", 10);
            yield return WaitFor(() => game.Snapshot.attack.orbs.Length == 2, 8, "First new request did not create exactly one orb per player.");
            Require(controller.Views.Count == 1 && controller.Resource.LocalPlayer.generatedTotal == 1,
                "Fresh session duplicated the first new generation.");
            Record("fresh-first-generate-one");
            yield return Barrier("before-synthetic-pause", 10);
            if (role == "client")
            {
                game.Lobby.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
                yield return null;
                game.Lobby.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
                report.syntheticLobbyPauseInvoked = true;
            }
            else
            {
                yield return WaitFor(() => game.Error.Length != 0 && !game.Lobby.Connected, 12,
                    "Peer synthetic background did not produce the Host's explicit network error.");
                report.pauseInterruption = game.LastInterruptionReason;
                Record("peer-synthetic-pause-network-error");
                game.Leave(); // Peer remains on error until explicit END, matching the connection-error policy.
            }
            yield return WaitFor(Clean, 12, "Synthetic pause handling did not leave a clean Lobby after resume/explicit END.");
            if (role == "client") report.pauseInterruption = game.LastInterruptionReason;
            Record("synthetic-pause-clean-lobby");
            yield return Barrier("final-clean", 10);
            yield return new WaitForSecondsRealtime(1.2f);
            Require(Clean(), "Synthetic resume restored old gameplay or reconnected automatically.");
            report.finalClean = true;
        }

        private IEnumerator ConnectAndStart(int epoch)
        {
            yield return WaitFor(() => game.Lobby.Connection.CanStart, 10, "Previous transport has not released.");
            if (role == "host")
            {
                Require(game.Lobby.CreateRoom("C6 T13 " + epoch, port), "Explicit CreateRoom failed.");
                yield return WaitFor(() => game.Lobby.Connected, 15, "Host Lobby did not open.");
                WriteMarker("host-open-" + epoch);
            }
            else
            {
                yield return WaitFor(() => File.Exists(Path.Combine(peerOutput, "host-open-" + epoch + ".json")),
                    20, "Host did not explicitly open its new room.");
                Require(game.Lobby.JoinDirect("127.0.0.1", port), "Explicit direct-IP Join failed.");
            }
            yield return WaitFor(() => game.Lobby.CanReady, 20, "Two-player Config ACK did not permit Ready.");
            Require(game.Lobby.ToggleReady(), "Ready request failed.");
            if (role == "host")
            {
                yield return WaitFor(() => game.Lobby.CanStart, 20, "Both Ready acknowledgements missing.");
                Require(game.Lobby.StartMatch(), "Host Start failed.");
            }
            yield return WaitFor(() => game.Attached && game.InitialConfirmed && game.Snapshot?.battle.phase == "Playing",
                20, "Confirmed game did not start.");
            Require(EmptyFull() && game.Snapshot.roundId == 1 && game.Snapshot.battle.duration == 180,
                "Fresh start is not empty/full round 1 with normal duration.");
            Require(game.Error.Length == 0 && (role != "client" || game.Lobby.JoinRoute == "DIRECT_IP"),
                "Unexpected coordinator error or changed join route.");
            Record("epoch-" + epoch + "-playing-empty-full");
            yield return Barrier("epoch-" + epoch + "-started", 10);
        }

        private IEnumerator GenerateOne()
        {
            Require(controller.RequestGenerate(), "Normal Generate request failed.");
            if (role == "client" && previousGeneration == null)
            {
                var field = typeof(ResourceSession).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(field != null, "Unable to capture original pending packet.");
                previousGeneration = Copy((ResourceRequestPacket)field.GetValue(controller.Resource));
            }
            yield return WaitFor(() => !controller.Resource.HasPending, 8, "Normal generation reply timed out.");
            var reply = controller.Resource.LastResult;
            Require(reply != null && reply.accepted && reply.known && reply.operation == (int)ResourceRequestKind.Generate
                && Math.Abs(reply.staminaBefore - reply.staminaAfter - 20) < .00001, "Normal generation did not cost exactly 20.");
            yield return WaitFor(() => controller.Views.Count == 1, 5, "Local first orb view missing.");
        }
        private bool EmptyFull() => game.Snapshot != null && game.Snapshot.attack.orbs.Length == 0
            && game.Snapshot.attack.hp == 100 && game.Snapshot.attack.totalHits == 0
            && game.Snapshot.resources.players.All(p => p.stamina == 100 && p.generatedTotal == 0)
            && controller.Views.Count == 0 && !controller.Resource.HasPending;
        private bool Clean() => !game.Attached && !game.Lobby.Connected && game.Snapshot == null
            && game.Lobby.Snapshot == null && controller.Views.Count == 0 && controller.Attack.ActiveProjectileCount == 0
            && !controller.Resource.HasPending && !controller.Combination.HasPending && !controller.HasCombinationPending
            && !game.Lobby.HasPending && GetComponent<T10LobbyHud>().Canvas.gameObject.activeSelf
            && !controller.Hud.Canvas.gameObject.activeSelf;
        private IEnumerator Barrier(string name, float seconds)
        { WriteMarker(name); yield return WaitFor(() => File.Exists(Path.Combine(peerOutput, name + ".json")), seconds, "Peer barrier missing: " + name); }
        private void WriteMarker(string name)
        { File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(new Marker { name = name, role = role, utc = DateTime.UtcNow.ToString("O"), at = Now })); }
        private void Record(string name)
        {
            var point = new Checkpoint { name = name, utc = DateTime.UtcNow.ToString("O"), snapshot = Copy(game.Snapshot),
                attached = game.Attached, connected = game.Lobby.Connected, localNonce = controller.Attack.LocalNonce,
                localViews = controller.Views.Count, interruption = game.LastInterruptionReason, error = game.Error,
                clean = Clean(), rejectedControls = game.RejectedControls };
            checkpoints.Add(point); File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(point, true));
            File.WriteAllText(Path.Combine(output, "progress.json"), JsonUtility.ToJson(point, true));
            Debug.Log("C6_T13_PROBE_CHECK role=" + role + " name=" + name);
        }
        private IEnumerator WaitFor(Func<bool> okay, float seconds, string error)
        {
            double until = Now + seconds;
            while (!okay() && Now < until)
            {
                string peerResult = Path.Combine(peerOutput, "result.json");
                if (File.Exists(peerResult))
                { var other = JsonUtility.FromJson<Report>(File.ReadAllText(peerResult)); Require(other.status != "FAIL", "Peer failed: " + other.error); }
                yield return null;
            }
            Require(okay(), error);
        }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        private static T Copy<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static string Arg(string[] args, string key, string fallback)
        { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        private void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
        [Serializable] private sealed class Marker { public string name, role, utc; public double at; }
        [Serializable] private sealed class ObservedProof { public string session, phase, hash; public uint round; public ulong revision; }
        [Serializable] private sealed class Checkpoint
        { public string name, utc, localNonce, interruption, error; public GameSnapshot snapshot; public bool attached, connected, clean; public int localViews, rejectedControls; }
        [Serializable] private sealed class Report
        {
            public string status = "NOT_RUN", error, role, startedAtUtc, finishedAtUtc, buildGuid, unity;
            public string scope = "MAC_TWO_PROCESS_DIRECT_IP_VALID_RESPONSE_TIMEOUT_FRESH_SESSION_AND_SYNTHETIC_CALLBACKS";
            public string pauseScope = "SendMessage OnApplicationPause(true/false) on Lobby component/game object; synthetic Unity callbacks, not actual OS/iOS pause.";
            public string focusScope = "Synthetic OnApplicationFocus(false/true) on game object; not actual iOS status bar.";
            public bool physicalDevice = false, bonjourValidated = false, gameplayStateInjected = false;
            public string firstSession, secondSession, firstRoom, secondRoom, firstNonce, secondNonce;
            public string watchdogInterruption, pauseInterruption;
            public bool syntheticFocusMaintained, noAutomaticReconnect, previousSessionRequestHadNoEffect,
                syntheticLobbyPauseInvoked, finalClean;
            public int invalidControlsSent, maximumRejectedControls, previousSessionRequestsSent;
            public int rejectedControlsBeforeFault, rejectedControlsAtWatchdog;
            public double faultToErrorSeconds;
            public Checkpoint[] checkpoints; public ObservedProof[] proofs;
        }
#endif
    }
}
