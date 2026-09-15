using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Resources;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    // Explicit iOS development diagnostics. No ordinary-launch behavior, synthetic lifecycle, or automatic game start.
    [DisallowMultipleComponent]
    public sealed class T13DeviceDiagnostics : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && UNITY_IOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachOptIn()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-c6T13DeviceRun");
            string selectedRun = Environment.GetEnvironmentVariable("C6_T13_DEVICE_RUN");
            if (i >= 0)
            {
                if (i + 1 >= args.Length || Array.IndexOf(args, "-c6T13DeviceRun", i + 1) >= 0) return;
                selectedRun = args[i + 1];
            }
            if (!SafeToken(selectedRun)) return;
            var owner = FindAnyObjectByType<T10GameSession>();
            if (owner == null || owner.BuildIdentifier != "20" || !owner.InterruptionHandlingEnabled
                || owner.GetComponent<T13DeviceDiagnostics>() != null) return;
            owner.gameObject.AddComponent<T13DeviceDiagnostics>().run = selectedRun;
        }
#endif
        private string run, directory, boot, lastCommandId = "", lastCommandHash = "", lastStatus = "IDLE", lastError = "";
        private string[] addresses;
        private T10GameSession game;
        private T09BattleController controller;
        private T13Command active;
        private T13Report report;
        private StreamWriter events;
        private double nextPoll, nextStatus;
        private bool closing;
        private readonly HashSet<string> rejected = new HashSet<string>();
        private static double Now => Time.realtimeSinceStartupAsDouble;
        private static string Utc() => DateTime.UtcNow.ToString("O");
        private void Start()
        {
#if DEVELOPMENT_BUILD && UNITY_IOS && !UNITY_EDITOR
            if (!SafeToken(run)) { enabled = false; return; }
            try
            {
                game = GetComponent<T10GameSession>(); controller = game?.Controller;
                Require(game != null && controller != null && game.BuildIdentifier == "20" && game.InterruptionHandlingEnabled, "T13_SCENE_REQUIRED");
                boot = Guid.NewGuid().ToString("N");
                directory = Path.Combine(Application.persistentDataPath, "T13", run);
                Directory.CreateDirectory(directory);
                events = new StreamWriter(new FileStream(Path.Combine(directory, "events.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false)) { AutoFlush = true };
                addresses = DirectConnectionSession.GetLocalIPv4Addresses();
                Event("diagnostics_started", false, "iOS development opt-in; all game actions require an explicit command.");
                WriteStatus();
                Debug.Log("C6_T13_DEVICE_DIAGNOSTICS run=" + run + " boot=" + boot + " directory=" + directory);
            }
            catch (Exception error) { lastError = error.Message; enabled = false; Debug.LogError("C6_T13_DIAGNOSTIC_START_FAILED " + error); }
#else
            enabled = false;
#endif
        }
        private void Update()
        {
            if (directory == null || closing) return;
            try
            {
                if (Now >= nextPoll) { nextPoll = Now + .25; if (active == null) Poll(); }
                if (Now >= nextStatus) { nextStatus = Now + .5; WriteStatus(); }
            }
            catch (Exception error)
            {
                lastStatus = "FAIL"; lastError = "DIAGNOSTIC_IO_ERROR / " + error.Message;
                // An uncertain diagnostic write is never converted to success or automatically resent.
                Debug.LogError("C6_T13_DIAGNOSTIC_IO_ERROR " + error); enabled = false;
            }
        }
        private void Poll()
        {
            string path = Path.Combine(directory, "command.json");
            if (!File.Exists(path)) return;
            long length = new FileInfo(path).Length;
            if (length < 2 || length > 8192) { lastError = "INVALID_COMMAND_FILE_SIZE"; return; }
            string raw;
            try { raw = File.ReadAllText(path); } catch (IOException) { return; }
            string hash = Sha(raw);
            T13Command command;
            try { command = JsonUtility.FromJson<T13Command>(raw); }
            catch (ArgumentException) { if (rejected.Add(hash)) Event("invalid_command_json", false, hash); return; }
            if (command == null || !SafeToken(command.id))
            { if (rejected.Add(hash)) Event("invalid_command_id", false, hash); return; }
            string resultPath = Path.Combine(directory, "result-" + command.id + ".json");
            if (File.Exists(resultPath))
            {
                T13Report previous;
                try { previous = JsonUtility.FromJson<T13Report>(File.ReadAllText(resultPath)); }
                catch (Exception) { lastError = "EXISTING_RESERVATION_UNREADABLE / NOT_REPLAYED"; return; }
                if (previous == null || previous.commandHash != hash)
                { lastError = "COMMAND_ID_ALREADY_USED_WITH_DIFFERENT_PAYLOAD"; if (rejected.Add(hash)) Event("command_id_conflict", false, command.id); return; }
                if (previous.status == "RUNNING")
                {
                    previous.status = "FAIL"; previous.error = "PROCESS_ENDED_BEFORE_COMPLETION / NOT_REPLAYED"; previous.finishedUtc = Utc();
                    AtomicJson(resultPath, previous); lastCommandId = command.id; lastCommandHash = hash;
                    lastStatus = previous.status; lastError = previous.error;
                }
                return;
            }
            report = new T13Report { schema = 1, run = run, boot = boot, id = command.id, command = command,
                commandHash = hash, status = "RUNNING", startedUtc = Utc(), before = Observe(),
                automatedInput = command.action != "capture", explicitResponseFault = command.action == "holdPeerResponses" };
            // CreateNew is the durable once-only reservation, before context checks or any action. Never reuse an id.
            using (var reservation = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(report, true));
                reservation.Write(bytes, 0, bytes.Length); reservation.Flush(true);
            }
            active = command; lastCommandId = command.id; lastCommandHash = hash; lastStatus = "RUNNING"; lastError = "";
            Event("command_reserved", false, command.id + " / " + command.action);
            StartCoroutine(ExecuteGuarded());
        }
        private IEnumerator ExecuteGuarded()
        {
            string error = null;
            var stack = new Stack<IEnumerator>(); stack.Push(Execute());
            while (stack.Count > 0 && error == null)
            {
                object item = null; bool more = false;
                try { more = stack.Peek().MoveNext(); if (more) item = stack.Peek().Current; else stack.Pop(); }
                catch (Exception failure) { error = failure.Message; }
                if (error != null) break;
                if (more && item is IEnumerator nested) stack.Push(nested); else if (more) yield return item;
            }
            yield return new WaitForEndOfFrame();
            try
            {
                report.after = Observe();
                if (active.screenshot)
                {
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    try { report.screenshot = active.id + ".png"; AtomicBytes(Path.Combine(directory, report.screenshot), texture.EncodeToPNG()); }
                    finally { if (texture != null) Destroy(texture); }
                }
            }
            catch (Exception failure) { error = error ?? "CAPTURE_FAILED / " + failure.Message; }
            report.status = error == null ? "PASS" : "FAIL"; report.error = error ?? ""; report.finishedUtc = Utc();
            lastStatus = report.status; lastError = report.error;
            try
            {
                Event("command_completed", false, active.id + " / " + lastStatus + " / " + lastError);
                AtomicJson(Path.Combine(directory, "result-" + active.id + ".json"), report);
            }
            catch (Exception failure) { lastStatus = "FAIL"; lastError = "RESULT_WRITE_FAILED / " + failure.Message; }
            Debug.Log("C6_T13_DEVICE_COMMAND run=" + run + " id=" + active.id + " status=" + lastStatus + " error=" + lastError);
            active = null; WriteStatus();
        }
        private IEnumerator Execute()
        {
            Require(active.schema == 1 && active.run == run && active.build == "20", "COMMAND_RUN_SCHEMA_BUILD_MISMATCH");
            Require(active.expectedBoot == boot, "COMMAND_PROCESS_BOOT_MISMATCH / NOT_REPLAYED");
            Require(active.action != null, "ACTION_REQUIRED");
            string session = CurrentSession(), room = CurrentRoom(); uint round = CurrentRound();
            bool opens = active.action == "createRoom" || active.action == "joinDirect";
            bool captures = active.action == "capture";
            if (opens)
                Require(game.Lobby.Connection.CanStart && !game.Attached && session.Length == 0
                    && string.IsNullOrEmpty(active.expectedSession) && string.IsNullOrEmpty(active.expectedRoom) && active.expectedRound == 0,
                    "NEW_CONNECTION_REQUIRES_EMPTY_CURRENT_CONTEXT");
            else if (!captures || !string.IsNullOrEmpty(active.expectedSession))
                Require(active.expectedSession == session && active.expectedRoom == room && active.expectedRound == round
                    && (session.Length != 0 || active.action == "end"), "EXPECTED_SESSION_ROOM_ROUND_MISMATCH");
            switch (active.action)
            {
                case "createRoom":
                    Require(game.Lobby.CreateRoom(active.name, active.port), "CREATE_ROOM_REJECTED");
                    yield return WaitFor(() => game.Lobby.Connected, 12, "HOST_CONNECTION_TIMEOUT"); break;
                case "joinDirect":
                    Require(game.Lobby.JoinDirect(active.address, active.port), "JOIN_DIRECT_REJECTED");
                    yield return WaitFor(() => game.Lobby.Connected && game.Lobby.Snapshot != null, 12, "DIRECT_JOIN_TIMEOUT"); break;
                case "ready":
                    Require(!game.Lobby.LocalReady, "ALREADY_READY / NOT_TOGGLED");
                    Require(game.Lobby.ToggleReady(), "READY_REJECTED");
                    yield return WaitFor(() => !game.Lobby.HasPending, 8, "READY_REPLY_TIMEOUT");
                    Require(game.Lobby.LocalReady, "READY_NOT_TRUE"); break;
                case "start":
                    if (game.Attached) Require(game.StartPreparedRound(), "PREPARED_START_REJECTED");
                    else Require(game.Lobby.StartMatch(), "LOBBY_START_REJECTED");
                    yield return WaitFor(() => game.InitialConfirmed && game.Snapshot?.battle.phase == "Playing", 15, "START_CONFIRMATION_TIMEOUT"); break;
                case "end":
                    game.Leave();
                    yield return WaitFor(() => !game.Attached && !game.Lobby.Connected && game.Lobby.Connection.CanStart,
                        12, "END_CLEANUP_TIMEOUT"); break;
                case "holdPeerResponses":
                    Require(game.Attached && game.InitialConfirmed && game.Error.Length == 0, "CONFIRMED_GAME_REQUIRED");
                    game.DevelopmentHoldPeerResponses = active.value;
                    Require(game.DevelopmentHoldPeerResponses == active.value, "FAULT_FLAG_NOT_APPLIED"); break;
                case "generateOnce":
                    Require(controller.RequestGenerate(), "GENERATE_REJECTED");
                    yield return WaitFor(() => !controller.Resource.HasPending, 8, "GENERATION_REPLY_TIMEOUT");
                    var reply = controller.Resource.LastResult;
                    Require(reply != null && reply.accepted && reply.known && reply.sessionId == session && reply.roundId == round
                        && reply.operation == (int)ResourceRequestKind.Generate
                        && Math.Abs(reply.staminaBefore - reply.staminaAfter - 20) < .00001, "NORMAL_GENERATION_COST_OR_CONTEXT_MISMATCH");
                    report.generationReply = JsonUtility.FromJson<ResourceRequestReply>(JsonUtility.ToJson(reply)); break;
                case "capture": break;
                default: throw new InvalidOperationException("UNKNOWN_ACTION");
            }
        }
        private IEnumerator WaitFor(Func<bool> ready, double seconds, string reason)
        { double deadline = Now + seconds; while (!ready() && Now < deadline) yield return null; Require(ready(), reason); }
        private string CurrentSession() => game.Snapshot?.sessionId ?? game.Lobby.Snapshot?.sessionId ?? "";
        private string CurrentRoom() => game.Snapshot?.roomId ?? game.Lobby.Snapshot?.roomId ?? "";
        private uint CurrentRound() => game.Snapshot?.roundId ?? game.Lobby.Snapshot?.roundId ?? 0;
        private T13Observation Observe()
        {
            var lobbyHud = GetComponent<T10LobbyHud>();
            return new T13Observation { schema = 1, run = run, boot = boot, utc = Utc(), realtime = Now,
                build = game.BuildIdentifier, buildGuid = Application.buildGUID, unity = Application.unityVersion,
                deviceModel = SystemInfo.deviceModel, operatingSystem = SystemInfo.operatingSystem, ipv4Addresses = addresses,
                actualDevice = Application.platform == RuntimePlatform.IPhonePlayer, physicalTouch = false,
                sessionId = CurrentSession(), roomId = CurrentRoom(), roundId = CurrentRound(),
                configHash = game.Snapshot?.configHash ?? game.Lobby.Snapshot?.configFingerprint ?? "",
                localId = game.Lobby.Connection.LocalClientId?.ToString() ?? "", localNonce = controller.Attack.LocalNonce,
                connected = game.Lobby.Connected, connectionState = game.Lobby.Connection.State.ToString(),
                connectionCanStart = game.Lobby.Connection.CanStart, joinRoute = game.Lobby.JoinRoute,
                attached = game.Attached, initialConfirmed = game.InitialConfirmed, canReady = game.Lobby.CanReady,
                localReady = game.Lobby.LocalReady, lobbyCanStart = game.Lobby.CanStart, gameCanStart = game.CanStart,
                lobby = game.Lobby.Snapshot, game = game.Snapshot, config = game.Lobby.HostConfig,
                gameError = game.Error, lobbyError = game.Lobby.Error, interruption = game.LastInterruptionReason,
                displayRemaining = game.DisplayRemaining, canonicalHash = game.Snapshot == null ? "" : GameWire.CanonicalHash(game.Snapshot),
                battleHudVisible = controller.Hud.Canvas.gameObject.activeSelf, lobbyHudVisible = lobbyHud.Canvas.gameObject.activeSelf,
                actionLabel = controller.Hud.ActionLabel.text, detailLabel = controller.Hud.DetailLabel.text,
                views = controller.Views.Count, viewIds = controller.Views.Keys.OrderBy(id => id).ToArray(),
                projectiles = controller.Attack.ActiveProjectileCount, resourcePending = controller.Resource.HasPending,
                combinationPending = controller.Combination.HasPending || controller.HasCombinationPending, lobbyPending = game.Lobby.HasPending,
                canInteract = controller.CanInteract, holdPeerResponses = game.DevelopmentHoldPeerResponses,
                rejectedControls = game.RejectedControls, rejectedStates = game.RejectedStates,
                activeCommandId = active?.id ?? "", lastCommandId = lastCommandId, lastCommandHash = lastCommandHash,
                commandStatus = lastStatus, commandError = lastError };
        }
        private void WriteStatus() => AtomicJson(Path.Combine(directory, "status.json"), Observe());
        // Observation only: these are real Unity callbacks from iOS; there is no command to invoke them.
        private void OnApplicationPause(bool value) { Event("application_pause", value, "OS callback observed; diagnostics made no lifecycle change."); }
        private void OnApplicationFocus(bool value) { Event("application_focus", value, "OS callback observed; diagnostics made no lifecycle change."); }
        private void OnApplicationQuit() { Event("application_quit", true, "OS callback observed; forced termination may omit this callback."); }
        private void Event(string kind, bool value, string detail)
        {
            if (events == null || closing) return;
            events.WriteLine(JsonUtility.ToJson(new T13Event { kind = kind, value = value, detail = detail, run = run, boot = boot,
                utc = Utc(), realtime = Now, sessionId = CurrentSession(), roomId = CurrentRoom(), roundId = CurrentRound(),
                build = game.BuildIdentifier, buildGuid = Application.buildGUID, activeCommandId = active?.id ?? "" }));
        }
        private void OnDestroy() { closing = true; events?.Dispose(); events = null; }
        private static bool SafeToken(string value) => !string.IsNullOrEmpty(value) && value.Length <= 80
            && value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_');
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static string Sha(string text)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
        private static void AtomicJson(string path, object value) => AtomicBytes(path, new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value, true)));
        private static void AtomicBytes(string path, byte[] bytes)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N"); File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        [Serializable] public sealed class T13Command
        { public int schema; public string id, run, build, action, expectedBoot, expectedSession, expectedRoom, name, address, port; public uint expectedRound; public bool value, screenshot; }
        [Serializable] public sealed class T13Report
        {
            public int schema; public string run, boot, id, commandHash, status, error, startedUtc, finishedUtc, screenshot;
            public bool actualDevice = true, physicalTouch = false, automatedInput, explicitResponseFault;
            public string acceptanceScope = "Command execution only; read resulting observations for network/gameplay acceptance.";
            public T13Command command; public T13Observation before, after; public ResourceRequestReply generationReply;
        }
        [Serializable] public sealed class T13Observation
        {
            public int schema, views, projectiles, rejectedControls, rejectedStates;
            public uint roundId; public double realtime, displayRemaining;
            public string run, boot, utc, build, buildGuid, unity, deviceModel, operatingSystem, sessionId, roomId, configHash,
                localId, localNonce, connectionState, joinRoute, gameError, lobbyError, interruption, canonicalHash,
                actionLabel, detailLabel, activeCommandId, lastCommandId, lastCommandHash, commandStatus, commandError;
            public string[] ipv4Addresses, viewIds;
            public bool actualDevice, physicalTouch, connected, connectionCanStart, attached, initialConfirmed, canReady,
                localReady, lobbyCanStart, gameCanStart, battleHudVisible, lobbyHudVisible, resourcePending, combinationPending,
                lobbyPending, canInteract, holdPeerResponses;
            public LobbySnapshot lobby; public GameSnapshot game; public LobbyHostConfig config;
        }
        [Serializable] private sealed class T13Event
        { public string kind, detail, run, boot, utc, sessionId, roomId, build, buildGuid, activeCommandId; public bool value; public double realtime; public uint roundId; }
    }
}
