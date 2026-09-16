using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    /// <summary>Opt-in L2 receipts. Mobile execution is passive; Mac actions require explicit fresh-output arguments.</summary>
    [DisallowMultipleComponent]
    public sealed class L2ConnectionProbe : MonoBehaviour
    {
        private T10GameSession game;
        private T10LobbySession lobby;
        private DirectConnectionSession connection;
        private Receipt receipt;
        private readonly List<Observation> observations = new List<Observation>();
        private readonly HashSet<string> firstJoinNonces = new HashSet<string>(StringComparer.Ordinal);
        private string receiptPath, output, shared, role, scenario, port, markerTag, lastKey, runtimeError;
        private bool automation, ownsOutput, trackingFirstJoin, cancelAtBarrier, subscribed, writing;
        private bool sawTwo, returnedToOne;
        private double nextCapture;
        private static double Now => Time.realtimeSinceStartupAsDouble;

#if C6_L2_CHECKS && DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var session = FindFirstObjectByType<T10GameSession>();
            if (session != null && session.GetComponent<L2ConnectionProbe>() == null)
                session.gameObject.AddComponent<L2ConnectionProbe>();
        }
#endif

        private IEnumerator Start()
        {
            game = GetComponent<T10GameSession>(); lobby = game?.Lobby; connection = lobby?.Connection;
            if (game == null || lobby == null || connection == null)
            { Debug.LogWarning("C6_L2_PROBE unavailable=game_session"); yield break; }
            receipt = new Receipt { runId = Guid.NewGuid().ToString("N"), startedUtc = DateTime.UtcNow.ToString("O"),
                unity = Application.unityVersion, gameBuild = game.BuildIdentifier, platform = Application.platform.ToString(),
                deviceModel = SystemInfo.deviceModel, mode = "PASSIVE", status = "OBSERVING" };
            string directory = Path.Combine(Application.persistentDataPath, "L2ConnectionChecks");
            try { Directory.CreateDirectory(directory); receiptPath = Path.Combine(directory, receipt.runId + ".json"); }
            catch (Exception error) { Debug.LogWarning("C6_L2_PROBE receipt_unavailable=" + error.GetType().Name); }
            if (receiptPath == null) yield break;
            connection.Changed += ConnectionChanged; lobby.Changed += LobbyChanged; game.Changed += LobbyChanged;
            Application.logMessageReceived += ObserveLog; subscribed = true;
            Capture("started", true);
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
            var args = Environment.GetCommandLineArgs(); output = Arg(args, "-c6L2Output", null);
            if (output != null)
            {
                automation = true;
                string error = null;
                try
                {
                    role = Arg(args, "-c6L2Role", "client"); scenario = Arg(args, "-c6L2Scenario", "fallback");
                    port = Arg(args, "-c6L2Port", "25260");
                    Require(role == "host" || role == "client", "Unknown automation role");
                    Require(new[] { "fallback", "reject", "cancelpending", "cancelbetween" }.Contains(scenario), "Unknown scenario");
                    Require(Path.IsPathRooted(output) && !File.Exists(output) &&
                        (!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any()), "A fresh absolute output directory is required");
                    output = Path.GetFullPath(output); Directory.CreateDirectory(output); ownsOutput = true;
                    shared = Arg(args, "-c6L2Shared", Directory.GetParent(output)?.FullName);
                    Require(!string.IsNullOrWhiteSpace(shared) && Path.IsPathRooted(shared), "An absolute shared marker directory is required");
                    shared = Path.GetFullPath(shared); Directory.CreateDirectory(shared);
                    Require(DirectConnectionValidation.TryParsePort(port, out _), "Invalid automation port");
                    markerTag = scenario + "-" + receipt.runId.Substring(0, 8);
                    receipt.mode = "EXPLICIT_MAC_AUTOMATION"; receipt.role = role; receipt.scenario = scenario;
                    Require(game.BuildIdentifier == "24", "P4 game contract changed");
                    Application.runInBackground = true; Application.targetFrameRate = 60;
                }
                catch (Exception exception) { error = exception.Message; }
                var stack = new Stack<IEnumerator>();
                if (error == null) stack.Push(role == "host" ? RunHost() : RunClient());
                double deadline = Now + (role == "host" ? 125 : 105);
                while (stack.Count > 0 && error == null)
                {
                    object next = null; bool more = false;
                    try
                    {
                        Require(Now < deadline, "Overall automation deadline expired");
                        Require(runtimeError == null, runtimeError);
                        more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; else stack.Pop();
                    }
                    catch (Exception exception) { error = exception.Message; }
                    if (error != null) break;
                    if (more && next is IEnumerator nested) stack.Push(nested); else if (more) yield return next;
                }
                cancelAtBarrier = false; trackingFirstJoin = false;
                receipt.status = error == null ? "PASS" : "FAIL"; receipt.error = error;
                receipt.finishedUtc = DateTime.UtcNow.ToString("O");
                receipt.firstJoinNonceFingerprints = firstJoinNonces.ToArray();
                Capture("automation_complete", true);
                game.Leave();
                yield return new WaitForSecondsRealtime(.5f);
                Save();
                Debug.Log("C6_L2_PROBE_COMPLETE role=" + role + " scenario=" + scenario + " status=" + receipt.status);
                Application.Quit(error == null ? 0 : 1);
            }
#endif
        }

        private void Update()
        {
            if (receipt == null || Now < nextCapture) return;
            nextCapture = Now + .25;
            Capture("poll");
        }

        private void ConnectionChanged()
        {
            Capture("connection");
            if (automation && cancelAtBarrier && connection.State == DirectConnectionState.Stopping && connection.JoinInProgress)
            {
                cancelAtBarrier = false; receipt.cancelObserved = "BETWEEN_CANDIDATES";
                receipt.cancelledAttempt = connection.CandidateAttempt;
                connection.Stop(); Capture("cancel_between", true);
            }
        }
        private void LobbyChanged() => Capture("lobby");
        private void OnApplicationPause(bool paused) { if (receipt != null) Capture(paused ? "paused" : "resumed", true); }
        private void OnApplicationFocus(bool focused) { if (receipt != null) Capture(focused ? "focused" : "unfocused", true); }
        private void OnApplicationQuit() { if (receipt != null) Capture("application_quit", true); }
        private void OnDestroy()
        {
            if (subscribed)
            {
                if (connection != null) connection.Changed -= ConnectionChanged;
                if (lobby != null) lobby.Changed -= LobbyChanged;
                if (game != null) game.Changed -= LobbyChanged;
                Application.logMessageReceived -= ObserveLog; subscribed = false;
            }
            Save();
        }

        private void Capture(string trigger, bool force = false)
        {
            if (receipt == null || connection == null || lobby == null) return;
            var snapshot = lobby.Snapshot;
            var players = snapshot?.OrderedPlayers?.Where(x => x != null && x.connected).ToArray() ?? Array.Empty<LobbyPlayer>();
            string nonce = "";
            byte[] payload = connection.OwnedManager?.NetworkConfig?.ConnectionData;
            if (payload != null && LobbyWire.TryDecode(payload, out LobbyHello hello)) nonce = Fingerprint(hello.clientNonce);
            if (trackingFirstJoin && nonce.Length != 0) firstJoinNonces.Add(nonce);
            string key = string.Join("|", connection.State, connection.ConnectionStage, connection.FailureStage,
                connection.FailureCode, connection.AttemptId, connection.CandidateAttempt, connection.AttemptAddressFamily,
                connection.JoinInProgress, snapshot?.roomId, snapshot?.revision, snapshot?.phase,
                lobby.InitialStateReady, lobby.CanReady, game.InitialConfirmed, lobby.Error, players.Length);
            if (!force && key == lastKey) return;
            lastKey = key;
            var observation = new Observation { at = Now, utc = DateTime.UtcNow.ToString("O"), trigger = trigger,
                state = connection.State.ToString(), stage = connection.ConnectionStage, failureStage = connection.FailureStage,
                failureCode = connection.FailureCode, approvalReason = connection.LastApprovalReason, lobbyError = lobby.Error,
                attemptId = connection.AttemptId, candidateAttempt = connection.CandidateAttempt, candidateCount = connection.CandidateCount,
                family = connection.AttemptAddressFamily, joining = connection.JoinInProgress,
                connected = lobby.Connected, initialStateReady = lobby.InitialStateReady, canReady = lobby.CanReady,
                gameInitialConfirmed = game.InitialConfirmed, roomId = snapshot?.roomId, sessionId = snapshot?.sessionId,
                configFingerprint = snapshot?.configFingerprint, phase = snapshot?.phase, revision = snapshot?.revision ?? 0,
                roster = players.Select(x => x.clientId).ToArray(), initialAcknowledgements = players.Select(x => x.initialStateReceived).ToArray(),
                helloNonceFingerprint = nonce };
            if (observations.Count < 2048) observations.Add(observation); else receipt.truncated = true;
            receipt.last = observation;
            receipt.maximumParticipants = Math.Max(receipt.maximumParticipants, players.Length);
            if (players.Length != players.Select(x => x.clientId).Distinct().Count()) receipt.duplicateRosterObserved = true;
            if (players.Length == 2) sawTwo = true;
            if (sawTwo && players.Length == 1) returnedToOne = true;
            receipt.observedTwo = sawTwo; receipt.observedReturnToOne = returnedToOne;
            Save();
        }

        private void Save()
        {
            if (receipt == null || receiptPath == null || writing) return;
            writing = true;
            try
            {
                receipt.events = observations.ToArray(); receipt.updatedUtc = DateTime.UtcNow.ToString("O");
                AtomicWrite(receiptPath, receipt);
                if (ownsOutput) AtomicWrite(Path.Combine(output, "result.json"), receipt);
            }
            catch (Exception error)
            { runtimeError = "Receipt write failed: " + error.GetType().Name; }
            finally { writing = false; }
        }

        private void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // Stock UTP logs this exact error immediately before its Disconnect event. Only the
            // deliberately unreachable first candidate in the explicit Mac fixtures expects it.
            // Passive device observations, rejoin, other messages and a second error remain failures.
            if (type == LogType.Error && condition == "Failed to connect to server." && automation && trackingFirstJoin &&
                (scenario == "fallback" || scenario == "cancelbetween") && connection != null &&
                connection.CandidateAttempt == 1 && connection.State == DirectConnectionState.Connecting &&
                receipt != null && receipt.expectedFirstCandidateConnectErrorCount == 0)
            {
                receipt.expectedFirstCandidateConnectErrorCount++;
                Capture("expected_first_candidate_connect_error", true);
                return;
            }
            runtimeError = "Runtime " + type + ": " + (condition?.Length > 512 ? condition.Substring(0, 512) : condition);
            if (receipt != null) receipt.runtimeError = runtimeError;
        }

#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        private IEnumerator RunHost()
        {
            yield return Wait(() => connection.CanStart, 10, "Host not ready to start");
            Require(lobby.CreateRoom("C6 L2 connection check", port), "Host creation refused");
            yield return Wait(() => lobby.InitialStateReady && lobby.CanReady, 20, "Host initial state not ready");
            AtomicWrite(Path.Combine(shared, "host-ready.json"), MarkerFor(1));
            double deadline = Now + 120;
            while (!File.Exists(Path.Combine(shared, "stop-host")))
            {
                Require(Now < deadline, "Host stop marker was not received within120seconds");
                Require(!receipt.duplicateRosterObserved && receipt.maximumParticipants <= 2, "Host roster duplicated or exceededtwo participants");
                foreach (string path in Directory.EnumerateFiles(shared, "client-*.json"))
                {
                    string acknowledgment = Path.Combine(shared, "host-" + Path.GetFileName(path).Substring(7));
                    if (File.Exists(acknowledgment)) continue;
                    var marker = JsonUtility.FromJson<Marker>(File.ReadAllText(path));
                    if (marker == null || marker.expectedCount < 1 || marker.expectedCount > 2) continue;
                    if (lobby.Snapshot?.ParticipantCount != marker.expectedCount) continue;
                    var local = MarkerFor(marker.expectedCount);
                    if (marker.expectedCount == 2 && (!local.roster.OrderBy(x => x).SequenceEqual(marker.roster.OrderBy(x => x)) ||
                        local.roomId != marker.roomId || local.config != marker.config)) continue;
                    AtomicWrite(acknowledgment, local);
                }
                yield return new WaitForSecondsRealtime(.1f);
            }
            Require(sawTwo && returnedToOne && lobby.Snapshot?.ParticipantCount == 1, "Host did not observejoinandcleanleave");
        }

        private IEnumerator RunClient()
        {
            yield return Wait(() => File.Exists(Path.Combine(shared, "host-ready.json")), 25, "Host-ready marker missing");
            var host = JsonUtility.FromJson<Marker>(File.ReadAllText(Path.Combine(shared, "host-ready.json")));
            Require(host != null && host.gameBuild == game.BuildIdentifier && !string.IsNullOrEmpty(host.roomId), "Host marker mismatch");
            yield return Wait(() => connection.CanStart, 8, "Client not ready to join");
            trackingFirstJoin = true;
            if (scenario == "fallback")
            {
                Require(lobby.JoinCandidatesForValidation(new[] { "192.0.2.1", "::1" }, port, game.BuildIdentifier, host.roomId), "Fallback join refused");
                yield return Ready(50);
                Require(connection.CandidateAttempt == 2 && connection.AttemptAddressFamily == "IPv6", "Fallback did not reachsecondIPv6candidate");
                Require(firstJoinNonces.Count == 1, "Candidate retry changed hello nonce");
                trackingFirstJoin = false; receipt.initialScenario = "FALLBACK_CONNECTED";
                yield return Barrier("first-joined", 2);
                game.Leave(); yield return Idle(); yield return Barrier("first-left", 1);
            }
            else if (scenario == "reject")
            {
                Require(lobby.JoinCandidatesForValidation(new[] { "::1", "127.0.0.1" }, port, "L2-WRONG-BUILD", host.roomId), "Rejection fixture join refused locally");
                yield return Wait(() => connection.State == DirectConnectionState.Failed && connection.CanStart, 25, "Expected admission rejection missing");
                receipt.rejectionReason = connection.LastApprovalReason;
                Require(connection.LastApprovalReason == "BUILD_MISMATCH" && connection.CandidateAttempt == 1 && !connection.JoinInProgress,
                    "Admission rejection retried another address or lost its reason");
                trackingFirstJoin = false; receipt.initialScenario = "BUILD_REJECTED_WITHOUT_FALLBACK";
                yield return Barrier("rejected", 1);
            }
            else
            {
                cancelAtBarrier = scenario == "cancelbetween";
                Require(lobby.JoinCandidatesForValidation(new[] { "192.0.2.1", "::1" }, port, game.BuildIdentifier, host.roomId), "Cancellation fixture join refused");
                if (scenario == "cancelpending")
                {
                    receipt.cancelObserved = "PENDING"; receipt.cancelledAttempt = connection.CandidateAttempt; connection.Stop();
                }
                yield return Idle(30);
                Require(scenario != "cancelbetween" || receipt.cancelObserved == "BETWEEN_CANDIDATES", "Shutdown barrier was not observed");
                uint cancelledId = connection.AttemptId;
                yield return new WaitForSecondsRealtime(1.2f);
                Require(connection.State == DirectConnectionState.Idle && !connection.JoinInProgress && connection.AttemptId == cancelledId
                    && connection.CandidateAttempt == 1, "Cancelled join resumed or moved to thenextcandidate");
                trackingFirstJoin = false; receipt.initialScenario = "CANCELLED_WITHOUT_LATE_RETRY";
                yield return Barrier("cancelled", 1);
            }
            Require(lobby.JoinForValidation("127.0.0.1", port, game.BuildIdentifier, host.roomId), "ValidIPv4rejoin refused");
            yield return Ready(22);
            Require(connection.CandidateAttempt == 1 && connection.AttemptAddressFamily == "IPv4", "Rejoin did not usefreshIPv4attempt");
            receipt.rejoined = true; yield return Barrier("rejoined", 2);
            game.Leave(); yield return Idle(); yield return Barrier("final-left", 1);
            receipt.cleanFinalLeave = true;
        }

        private IEnumerator Ready(double seconds)
        {
            yield return Wait(() => lobby.InitialStateReady && lobby.CanReady && lobby.Snapshot?.ParticipantCount == 2, seconds, "Connected lobby initialacknotcomplete");
            var players = lobby.Snapshot.OrderedPlayers.Where(x => x.connected).ToArray();
            Require(players.Length == 2 && players.Select(x => x.clientId).Distinct().Count() == 2 && players.All(x => x.initialStateReceived), "Invalidorunconfirmedroster");
        }
        private IEnumerator Idle(double seconds = 10)
            => Wait(() => connection.State == DirectConnectionState.Idle && connection.CanStart && !connection.JoinInProgress, seconds, "Connection didnotreturnIdle");
        private IEnumerator Barrier(string phase, int count)
        {
            string name = markerTag + "-" + phase + ".json";
            AtomicWrite(Path.Combine(shared, "client-" + name), MarkerFor(count));
            yield return Wait(() => File.Exists(Path.Combine(shared, "host-" + name)), 12, "Host didnotconfirm " + phase);
            receipt.completedPhases = (receipt.completedPhases ?? Array.Empty<string>()).Concat(new[] { phase }).ToArray();
            Capture("barrier_" + phase, true);
        }
        private Marker MarkerFor(int count)
        {
            var snapshot = lobby.Snapshot;
            return new Marker { expectedCount = count, gameBuild = game.BuildIdentifier, roomId = snapshot?.roomId,
                config = snapshot?.configFingerprint, roster = snapshot?.OrderedPlayers.Where(x => x.connected).Select(x => x.clientId).ToArray() ?? Array.Empty<ulong>() };
        }
        private IEnumerator Wait(Func<bool> condition, double seconds, string message)
        { double deadline = Now + seconds; while (!condition()) { Require(Now < deadline, message); yield return null; } }
        private static string Arg(string[] args, string key, string fallback)
        { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
#endif

        private static string Fingerprint(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private static void AtomicWrite(string path, object value)
        {
            string temporary = path + ".tmp"; File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        [Serializable] private sealed class Receipt
        {
            public string task = "L2", appBuild = LocalNetworkBuildInfo.ApplicationBuild, connectionRevision = LocalNetworkBuildInfo.Revision;
            public string runId, startedUtc, updatedUtc, finishedUtc, unity, gameBuild, platform, deviceModel, mode, role, scenario, status, error, runtimeError;
            public string gameplay = "NOT_ASSERTED_BY_PROBE", initialScenario, rejectionReason, cancelObserved;
            public int maximumParticipants, cancelledAttempt, expectedFirstCandidateConnectErrorCount;
            public bool observedTwo, observedReturnToOne, duplicateRosterObserved, rejoined, cleanFinalLeave, truncated;
            public string[] firstJoinNonceFingerprints, completedPhases;
            public Observation last; public Observation[] events;
        }
        [Serializable] private sealed class Observation
        {
            public double at; public string utc, trigger, state, stage, failureStage, failureCode, approvalReason, lobbyError,
                family, roomId, sessionId, configFingerprint, phase, helloNonceFingerprint;
            public uint attemptId; public int candidateAttempt, candidateCount; public ulong revision; public ulong[] roster;
            public bool joining, connected, initialStateReady, canReady, gameInitialConfirmed; public bool[] initialAcknowledgements;
        }
        [Serializable] private sealed class Marker
        { public int expectedCount; public string gameBuild, roomId, config; public ulong[] roster = Array.Empty<ulong>(); }
    }
}
