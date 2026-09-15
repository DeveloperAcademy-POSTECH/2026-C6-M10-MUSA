using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Combination;
using C6.Prototype.Lobby;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace C6.Prototype.GameSync
{
    /// <summary>
    /// Explicit build-18 device diagnostics. Commands exercise existing gameplay requests;
    /// they are automated input, never physical-touch acceptance evidence.
    /// Ordinary launches install no component, message handler, polling or game automation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class T12DeviceDiagnostics : MonoBehaviour
    {
        public string DirectoryPath { get; private set; }
        public string RunId { get; private set; }
        public bool IsCommandActive => active != null;
        public string ActiveCommandId => active?.id ?? "";
        public string LastStatus { get; private set; } = "IDLE";
        public string LastError { get; private set; } = "";
        private T10GameSession game;
        private T09BattleController controller;
        private T12DiagnosticCommand active;
        private CommandReport report;
        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private T12ValidationAck outgoingAck;
        private T12ValidationAck previousAck;
        private readonly Dictionary<ulong, T12ValidationAck> peerAcks = new Dictionary<ulong, T12ValidationAck>();
        private readonly Dictionary<ulong, Observation> observations = new Dictionary<ulong, Observation>();
        private readonly List<AttackRequestReply> attackReplies = new List<AttackRequestReply>();
        private readonly List<ResourceRequestReply> generationReplies = new List<ResourceRequestReply>();
        private readonly List<CombinationReply> combinationReplies = new List<CombinationReply>();
        private readonly List<TransferProof> transfers = new List<TransferProof>();
        private readonly List<ClockSample> clocks = new List<ClockSample>();
        private readonly Dictionary<string, ulong> localSequences = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly List<BehaviourState> blockedInputs = new List<BehaviourState>();
        private readonly HashSet<string> rejectedPayloads = new HashSet<string>(StringComparer.Ordinal);
        private StreamWriter eventsWriter, clockWriter;
        private double nextPoll, nextClock, nextAck;
        private string commandError, seriesSession, evidenceError;
        private uint seriesRound;
        private ulong seriesPeer, seriesP1, seriesP2;
        private bool closing;
        private static double Now => Time.realtimeSinceStartupAsDouble;

        public static bool TryResolveRunId(string[] args, string environmentValue, out string run)
        {
            run = environmentValue;
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "-c6T12Run")
                    { if (i + 1 >= args.Length || Array.IndexOf(args, "-c6T12Run", i + 1) >= 0) { run = null; return false; } run = args[i + 1]; break; }
            if (T12DiagnosticCommand.SafeId(run)) return true; run = null; return false;
        }
#if DEVELOPMENT_BUILD && (UNITY_IOS || UNITY_STANDALONE) && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachOptIn()
        {
            if (!TryResolveRunId(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("C6_T12_RUN"), out string run)) return;
            var target = FindAnyObjectByType<T10GameSession>();
            if (target == null || target.BuildIdentifier != "19" || target.GetComponent<T12DeviceDiagnostics>() != null) return;
            var component = target.gameObject.AddComponent<T12DeviceDiagnostics>(); component.RunId = run;
        }
#endif
        private void Start()
        {
#if DEVELOPMENT_BUILD && (UNITY_IOS || UNITY_STANDALONE) && !UNITY_EDITOR
            if (!T12DiagnosticCommand.SafeId(RunId)) { enabled = false; return; }
            try
            {
                game = GetComponent<T10GameSession>(); controller = game?.Controller;
                Require(game != null && game.BuildIdentifier == "19" && game.TransfersEnabled && controller != null, "T12_SCENE_REQUIRED");
                DirectoryPath = Path.Combine(Application.persistentDataPath, "c6-t12", RunId);
#if UNITY_STANDALONE
                var arguments = Environment.GetCommandLineArgs(); int directoryIndex = Array.IndexOf(arguments, "-c6T12Directory");
                if (directoryIndex >= 0)
                {
                    Require(directoryIndex + 1 < arguments.Length && Array.IndexOf(arguments, "-c6T12Directory", directoryIndex + 1) < 0, "INVALID_STANDALONE_DIRECTORY_ARGUMENT");
                    string directory = arguments[directoryIndex + 1];
                    Require(Path.IsPathRooted(directory) && (!Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any()), "STANDALONE_DIRECTORY_MUST_BE_ABSOLUTE_AND_EMPTY");
                    DirectoryPath = Path.GetFullPath(directory);
                    // Explicit two-process Mac precheck only. iOS background lifecycle is unchanged.
                    Application.runInBackground = true;
                }
#endif
                Directory.CreateDirectory(DirectoryPath);
                eventsWriter = Append("events.jsonl"); clockWriter = Append("clock.jsonl");
                controller.Attack.RequestResolved += OnAttack;
                controller.Resource.GenerationResolved += OnGeneration;
                controller.Resource.RecoveryResolved += OnRecovery;
                controller.Combination.RequestResolved += OnCombination;
                controller.Attack.ValidHit += OnHit;
                game.Changed += OnCommitted;
                Event("diagnostics_started", new StartEvent { build = game.BuildIdentifier, platform = Application.platform.ToString(),
                    device = SystemInfo.deviceModel, persistentDirectory = DirectoryPath, automatedInput = true, physicalTouch = false });
                WriteStatus(); Debug.Log("C6_T12_DIAGNOSTICS run=" + RunId + " directory=" + DirectoryPath + " automatedInput=true physicalTouch=false");
            }
            catch (Exception e) { LastError = e.Message; Debug.LogError("C6_T12_DIAGNOSTICS_START_FAILED " + e); enabled = false; }
#else
            enabled = false;
#endif
        }
        private StreamWriter Append(string name) => new StreamWriter(new FileStream(Path.Combine(DirectoryPath, name), FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        // Observe the real operating-system callbacks; never change pause, focus, or game state.
        private void OnApplicationFocus(bool focused) => RecordLifecycle("application_focus", focused);
        private void OnApplicationPause(bool paused) => RecordLifecycle("application_pause", paused);
        private void RecordLifecycle(string kind, bool value)
        {
            if (eventsWriter == null || closing) return;
            var snapshot = game == null ? null : game.Snapshot;
            Event(kind, new LifecycleEvent { value = value, sessionId = snapshot?.sessionId ?? "",
                phase = snapshot?.battle?.phase ?? "", remaining = snapshot?.battle?.remaining ?? 0 });
        }
        private void Update()
        {
            if (DirectoryPath == null || closing) return;
            try
            {
                RefreshMessaging();
                if (Now >= nextClock) { nextClock = Now + .5; SampleClock(); }
                if (active?.action == "transferSeries" && outgoingAck != null && Now >= nextAck)
                { nextAck = Now + .25; SendAck(); }
                if (Now < nextPoll) return;
                nextPoll = Now + .25;
                if (active == null) PollCommand();
                WriteStatus();
            }
            catch (Exception e)
            {
                LastError = e.Message;
                if (active != null) commandError = "DIAGNOSTIC_IO_OR_PROTOCOL_ERROR / " + e.Message;
                else { Debug.LogError("C6_T12_DIAGNOSTICS_FAILED " + e); enabled = false; }
            }
        }
        private void PollCommand()
        {
            if (evidenceError != null) { LastStatus = "FAIL"; LastError = evidenceError; return; }
            string path = Path.Combine(DirectoryPath, "command.json"); if (!File.Exists(path)) return;
            var info = new FileInfo(path); if (info.Length == 0 || info.Length > 8192) { LastError = "WAITING_FOR_VALID_COMMAND_FILE"; return; }
            string json;
            try { json = File.ReadAllText(path); } catch (IOException) { return; }
            if (!T12DiagnosticCommand.TryRead(json, RunId, out var command, out string reason))
            { LastError = "WAITING_FOR_VALID_COMMAND_FILE / " + reason; return; }
            string resultPath = Path.Combine(DirectoryPath, command.id + ".json");
            if (File.Exists(resultPath))
            {
                var previous = JsonUtility.FromJson<CommandReport>(File.ReadAllText(resultPath));
                if (previous == null || previous.command?.id != command.id || previous.planHash != command.PlanHash)
                {
                    LastError = "COMMAND_ID_ALREADY_USED_WITH_DIFFERENT_PAYLOAD";
                    if (rejectedPayloads.Add(command.PlanHash)) Event("command_id_conflict", command);
                }
                else if (previous.status == "RUNNING")
                {
                    previous.status = "FAIL"; previous.error = "PROCESS_ENDED_BEFORE_COMMAND_COMPLETED / NOT_REPLAYED";
                    previous.finishedUtc = Utc(); AtomicJson(resultPath, previous); LastStatus = previous.status; LastError = previous.error;
                }
                return;
            }
            active = command; commandError = null; LastError = ""; LastStatus = "RUNNING";
            transfers.Clear(); peerAcks.Clear(); observations.Clear(); outgoingAck = null; previousAck = null;
            report = new CommandReport { schema = 1, runId = RunId, commandId = command.id, command = command, planHash = command.PlanHash, status = "RUNNING", startedUtc = Utc(),
                automatedInput = command.action != "capture", physicalTouch = false, actualDevice = Application.platform == RuntimePlatform.IPhonePlayer };
            // Persist the reservation before the first gameplay action. A restarted process never replays it.
            AtomicJson(resultPath, report); Event("command_started", command); StartCoroutine(ExecuteGuarded());
        }
        private IEnumerator ExecuteGuarded()
        {
            string failure = null;
            var stack = new Stack<IEnumerator>();
            try { if (active.action != "capture") BlockManualInput(); report.before = Capture(); stack.Push(Execute()); }
            catch (Exception e) { failure = e.Message; }
            while (stack.Count > 0 && failure == null)
            {
                object current = null; bool more = false;
                try
                {
                    if (commandError != null) throw new InvalidOperationException(commandError);
                    more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; else stack.Pop();
                }
                catch (Exception e) { failure = e.Message; }
                if (failure != null) break;
                if (more && current is IEnumerator nested) stack.Push(nested); else if (more) yield return current;
            }
            // End-of-frame capture is outside child receipt callbacks. Game.Snapshot is the committed proof;
            // raw child DTOs are labelled separately and never used as aggregate agreement evidence.
            yield return new WaitForEndOfFrame();
            try
            {
                report.after = Capture();
                if (active.screenshot)
                {
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    try { report.screenshot = active.id + ".png"; AtomicBytes(Path.Combine(DirectoryPath, report.screenshot), texture.EncodeToPNG()); }
                    finally { if (texture != null) Destroy(texture); }
                }
            }
            catch (Exception e) { failure = failure ?? "CAPTURE_FAILED / " + e.Message; }
            RestoreManualInput();
            failure = failure ?? commandError ?? evidenceError;
            report.status = failure == null ? "PASS" : "FAIL"; report.error = failure ?? ""; report.finishedUtc = Utc();
            report.transfers = transfers.ToArray(); report.attackReplies = attackReplies.ToArray(); report.generationReplies = generationReplies.ToArray();
            report.combinationReplies = combinationReplies.ToArray(); report.proofs = game.Proofs.ToArray(); report.clockSamples = clocks.ToArray();
            LastStatus = report.status; LastError = report.error;
            try
            {
                Event("command_completed", new Completion { id = active.id, status = report.status, error = report.error });
                if (evidenceError != null) { report.status = LastStatus = "FAIL"; report.error = LastError = evidenceError; }
                AtomicJson(Path.Combine(DirectoryPath, active.id + ".json"), report);
            }
            catch (Exception e) { LastStatus = "FAIL"; LastError = "REPORT_WRITE_FAILED / " + e.Message; Debug.LogError("C6_T12_REPORT_FAILED " + e); }
            Debug.Log("C6_T12_COMMAND_COMPLETE run=" + RunId + " id=" + active.id + " action=" + active.action + " status=" + LastStatus + " error=" + LastError);
            active = null; outgoingAck = null; previousAck = null; seriesSession = null; peerAcks.Clear(); observations.Clear();
            try { WriteStatus(); } catch (Exception e) { Debug.LogError("C6_T12_STATUS_FAILED " + e.Message); }
        }
        private IEnumerator Execute()
        {
            if (active.action == "capture") yield break;
            if (active.action == "lobbyCreate" || active.action == "joinDirect" || active.action == "ready")
            {
#if UNITY_STANDALONE && !UNITY_EDITOR
                Require(!game.Attached, "MAC_PRECHECK_LOBBY_ACTION_REQUIRES_LOBBY");
                if (active.action == "lobbyCreate")
                { Require(game.Lobby.CreateRoom(active.roomName, active.port), "NORMAL_LOBBY_CREATE_REFUSED"); yield return Await(() => game.Lobby.Connected, 15, "LOBBY_CREATE_TIMEOUT"); }
                else if (active.action == "joinDirect")
                { Require(game.Lobby.JoinDirect(active.ip, active.port), "NORMAL_DIRECT_JOIN_REFUSED"); yield return Await(() => game.Lobby.CanReady, 15, "DIRECT_JOIN_CONFIG_TIMEOUT"); }
                else
                { Require(game.Lobby.CanReady && !game.Lobby.LocalReady && game.Lobby.ToggleReady(), "NORMAL_READY_REFUSED"); yield return Await(() => game.Lobby.LocalReady && !game.Lobby.HasPending, 8, "READY_TIMEOUT"); }
                Event("mac_precheck_lobby_action", active); yield break;
#else
                throw new InvalidOperationException("MAC_PRECHECK_ONLY / IPHONE_IPAD_LOBBY_REQUIRES_HUMAN_BONJOUR");
#endif
            }
            if (active.action == "hostStart")
            {
                Require(game.Lobby.IsHost, "HOST_REQUIRED");
                Require(game.Attached ? controller.RequestHostStart() : game.Lobby.StartMatch(), "APPROVED_HOST_START_REFUSED");
                yield return Await(() => game.InitialConfirmed && game.Snapshot?.battle.phase == "Playing", 15, "PLAYING_AGGREGATE_NOT_CONFIRMED"); yield break;
            }
            if (active.action == "retry")
            {
                Require(game.Attached && game.Lobby.IsHost && game.Snapshot != null && game.Snapshot.battle.phase != "Playing", "HOST_RESULT_REQUIRED");
                uint old = game.Snapshot.roundId; controller.RetryBattle();
                yield return Await(() => game.InitialConfirmed && game.Snapshot?.roundId == old + 1 && game.Snapshot.battle.phase == "Ready", 15, "RETRY_READY_AGGREGATE_NOT_CONFIRMED");
                Require(game.Snapshot.attack.orbs.Length == 0 && game.Snapshot.resources.players.All(p => p.stamina == game.Snapshot.resources.maximum)
                    && game.Snapshot.attack.hp == game.Snapshot.attack.maxHp && game.Snapshot.battle.remaining == game.Snapshot.battle.duration, "RETRY_INITIAL_VALUES_MISMATCH"); yield break;
            }
            Playing();
            switch (active.action)
            {
                case "generate": for (int i = 0; i < active.count; i++) yield return Generate(active.expectRejected); break;
                case "combine": yield return Combine(); break;
                case "launch":
                    if (active.spendBeforeLaunch) { yield return Generate(false); Require(controller.Resource.LocalPlayer.stamina <= controller.Resource.Snapshot.maximum - 5, "HIT_RECOVERY_HEADROOM_NOT_AVAILABLE"); }
                    yield return Launch(); break;
                case "transfer": yield return SingleTransfer(); break;
                case "transferSeries": yield return TransferSeries(); break;
                default: throw new InvalidOperationException("UNSUPPORTED_ACTION");
            }
        }
        private void Playing()
        {
            Require(game.Attached && game.InitialConfirmed && game.Snapshot?.battle.phase == "Playing"
                && game.Lobby.Connected && controller.CanInteract && !controller.Resource.HasPending && !controller.Combination.HasPending, "CONFIRMED_PLAYING_WITHOUT_PENDING_REQUIRED");
            Require(game.Error.Length == 0, "GAME_ERROR / " + game.Error);
        }
        private IEnumerator Generate(bool expectRejected)
        {
            Playing(); var before = Copy(game.Snapshot); int index = generationReplies.Count;
            if (!expectRejected)
                yield return Await(() => controller.Resource.LocalPlayer.stamina >= controller.Resource.Snapshot.generateCost, 4, "GENERATION_STAMINA_UNAVAILABLE");
            Require(controller.RequestGenerate(), "NORMAL_GENERATION_REQUEST_NOT_SENT");
            yield return Await(() => generationReplies.Count > index && generationReplies.Skip(index).Any(r => r.known), 8, "GENERATION_RECEIPT_TIMEOUT", () => controller.Resource.QueryPending());
            var reply = generationReplies.Skip(index).Last(r => r.known);
            Require(reply.accepted != expectRejected && reply.operation == (int)ResourceRequestKind.Generate, "GENERATION_RECEIPT_UNEXPECTED / " + reply.reason);
            if (!expectRejected)
            {
                Require(reply.confirmedOrb != null, "GENERATION_CONFIRMED_ID_MISSING");
                yield return Await(() => SameRound(before) && game.Snapshot.attack.revision >= reply.inventoryRevision
                    && game.Snapshot.resources.revision >= reply.resourceRevision && Orb(game.Snapshot, reply.confirmedOrb.id) != null, 8, "GENERATION_AGGREGATE_TIMEOUT");
                var after = game.Snapshot; Require(LiveIds(after).Except(LiveIds(before)).SequenceEqual(new[] { reply.confirmedOrb.id })
                    && LiveIds(before).Except(LiveIds(after)).Count() == 0, "GENERATION_LIVE_ID_DELTA_NOT_ONE");
                Require(Math.Abs(reply.staminaBefore - reply.staminaAfter - after.resources.generateCost) < .0001, "GENERATION_COST_MISMATCH");
            }
            Event("generation_before_after", new StateDelta { requestId = reply.requestId, before = before, after = Copy(game.Snapshot), source = "NORMAL_GENERATE_COST" });
        }
        private IEnumerator Combine()
        {
            Playing(); var before = Copy(game.Snapshot); var source = RequireOrb(before, active.orbId); var target = RequireOrb(before, active.otherOrbId);
            ulong seq = NextSequence(source, target); string id = RequestId();
            // A real request describes the release positions after moving the source onto the target.
            var request = new CombinationRequest(before.sessionId, before.roundId, id, source.id, target.id, seq, target.pos, target.pos);
            Require(controller.Combination.Submit(request), "NORMAL_COMBINATION_REQUEST_NOT_SENT");
            yield return Await(() => combinationReplies.Any(r => r.requestId == id && r.known), 8, "COMBINATION_RECEIPT_TIMEOUT", () => controller.Combination.QueryPending(request));
            var reply = combinationReplies.Last(r => r.requestId == id && r.known);
            Require(reply.accepted != active.expectRejected, "COMBINATION_RECEIPT_UNEXPECTED / " + reply.reason);
            if (active.expectRejected)
            {
                yield return null; Require(SameRound(before) && T12DiagnosticProtocol.RecordHash(RequireOrb(game.Snapshot, source.id)) == T12DiagnosticProtocol.RecordHash(source)
                    && T12DiagnosticProtocol.RecordHash(RequireOrb(game.Snapshot, target.id)) == T12DiagnosticProtocol.RecordHash(target), "REJECTED_COMBINATION_CHANGED_INPUTS");
            }
            else
            {
                Require(reply.originalCombined != null, "COMBINED_ID_MISSING");
                yield return Await(() => SameRound(before) && game.Snapshot.attack.revision >= reply.inventoryRevision && Orb(game.Snapshot, reply.originalCombined.id) != null
                    && Orb(game.Snapshot, source.id) == null && Orb(game.Snapshot, target.id) == null, 8, "COMBINATION_AGGREGATE_TIMEOUT");
                var combined = RequireOrb(game.Snapshot, reply.originalCombined.id);
                Require(combined.kind == (int)OrbKind.Combined && combined.transferCount == 0 && combined.lastTransferSequence == 0
                    && combined.owner == controller.Attack.LocalPlayerId && combined.id != source.id && combined.id != target.id, "COMBINATION_ID_OR_OWNER_MISMATCH");
            }
            Event("combination_before_after", new StateDelta { requestId = id, before = before, after = Copy(game.Snapshot), source = "NORMAL_COMBINATION_REQUEST" });
        }
        private IEnumerator Launch()
        {
            Playing(); var before = Copy(game.Snapshot); var orb = RequireOrb(before, active.orbId);
            var request = new OrbActionRequest(before.sessionId, before.roundId, RequestId(), orb.id, "", OrbActionKind.Launch, NextSequence(orb), new Vector2(active.x, active.y));
            yield return Attack(request, active.expectRejected);
            if (active.expectRejected)
            {
                Require(SameRound(before) && T12DiagnosticProtocol.RecordHash(RequireOrb(game.Snapshot, orb.id)) == T12DiagnosticProtocol.RecordHash(orb)
                    && game.Snapshot.attack.hp == before.attack.hp, "REJECTED_LAUNCH_CHANGED_ORB_OR_HP");
            }
            else
            {
                int hp = Math.Max(0, before.attack.hp - controller.Layout.Config.BaseDamage);
                yield return Await(() => SameRound(before) && game.Snapshot.attack.hp == hp && game.Snapshot.attack.roundHits == before.attack.roundHits + 1
                    && Orb(game.Snapshot, orb.id) == null, 8, "REAL_HIT_AND_CONSUMPTION_NOT_CONFIRMED");
                Require(!controller.Views.ContainsKey(orb.id), "CONSUMED_LOCAL_VIEW_REMAINS");
            }
            Event("launch_before_after", new StateDelta { requestId = request.RequestId, before = before, after = Copy(game.Snapshot), source = "NORMAL_LAUNCH_AND_REAL_PHYSICS" });
        }
        private IEnumerator SingleTransfer()
        {
            Playing(); var before = Copy(game.Snapshot); var orb = RequireOrb(before, active.orbId);
            if (active.expectedOwner != ulong.MaxValue) Require(orb.owner == active.expectedOwner, "TRANSFER_EXPECTED_OWNER_MISMATCH");
            var request = TransferRequest(before, orb, active.direction);
            yield return Attack(request, active.expectRejected);
            if (active.expectRejected)
                Require(SameRound(before) && T12DiagnosticProtocol.RecordHash(RequireOrb(game.Snapshot, orb.id)) == T12DiagnosticProtocol.RecordHash(orb), "REJECTED_TRANSFER_CHANGED_ORB");
            else
            {
                yield return Await(() => SameRound(before) && Orb(game.Snapshot, orb.id)?.transferCount == orb.transferCount + 1, 8, "TRANSFER_AGGREGATE_TIMEOUT");
                transfers.Add(ValidateTransfer(before, Copy(game.Snapshot), active.direction, request.RequestId, true));
            }
        }
        private IEnumerator Attack(OrbActionRequest request, bool rejected)
        {
            Event("attack_request", new RequestEvent { sessionId = request.SessionId, roundId = request.RoundId, requestId = request.RequestId,
                orbId = request.OrbId, action = request.Kind.ToString(), sequence = request.SequenceNumber, position = request.NormalizedPosition });
            Require(controller.Attack.Submit(request), "NORMAL_ATTACK_REQUEST_NOT_SENT");
            yield return Await(() => attackReplies.Any(r => r.requestId == request.RequestId && r.known && !r.pending), 8,
                "ATTACK_RECEIPT_TIMEOUT", () => { Event("attack_query", new Completion { id = request.RequestId }); controller.Attack.QueryPending(request); });
            var receipt = attackReplies.Last(r => r.requestId == request.RequestId && r.known && !r.pending);
            Require(receipt.accepted != rejected, "ATTACK_RECEIPT_UNEXPECTED / " + receipt.reason);
        }
        private IEnumerator TransferSeries()
        {
            var initial = Copy(game.Snapshot); var orb = RequireOrb(initial, active.orbId);
            Require(orb.transferCount == active.initialTransferCount && orb.owner == active.expectedOwner && orb.state == (int)OrbAuthorityState.Idle, "SERIES_INITIAL_ORB_MISMATCH");
            seriesSession = initial.sessionId; seriesRound = initial.roundId; seriesP1 = initial.p1.clientId; seriesP2 = initial.p2.clientId;
            Require(orb.owner == seriesP1 || orb.owner == seriesP2, "SERIES_OWNER_NOT_PARTICIPANT");
            ulong local = controller.Attack.LocalPlayerId; Require(local == seriesP1 || local == seriesP2, "SERIES_LOCAL_NOT_PARTICIPANT");
            seriesPeer = local == seriesP1 ? seriesP2 : seriesP1;
            Observe(initial); SetAck(orb);
            yield return AckBarrier(orb.transferCount, 30);
            for (int i = 0; i < active.count; i++)
            {
                ulong previousCount = active.initialTransferCount + (ulong)i; ulong expectedCount = previousCount + 1;
                Require(observations.TryGetValue(previousCount, out var beforeObservation), "SERIES_PREVIOUS_OBSERVATION_MISSING");
                var before = beforeObservation.game; var previous = RequireOrb(before, active.orbId);
                bool sending = previous.owner == local; string requestId = "";
                Require(CurrentSeries(), "SERIES_CONTEXT_CHANGED");
                ulong observedCount = RequireOrb(game.Snapshot, active.orbId).transferCount;
                // The receiver can observe the next commit in the same frame that the prior
                // ACK arrives. It must verify that stored observation, not resend or reject it.
                Require(observedCount == previousCount || !sending && observedCount == expectedCount,
                    "SERIES_EXTERNAL_OR_SKIPPED_ACTION");
                if (sending)
                {
                    Playing(); var request = TransferRequest(before, previous, active.directions[i]); requestId = request.RequestId;
                    yield return Attack(request, false);
                }
                yield return Await(() => observations.ContainsKey(expectedCount), 8, "SERIES_COMMITTED_TRANSFER_NOT_OBSERVED");
                var after = observations[expectedCount].game;
                transfers.Add(ValidateTransfer(before, after, active.directions[i], requestId, sending));
                SetAck(RequireOrb(after, active.orbId));
                yield return AckBarrier(expectedCount, 8);
                // Pacing is for readability; the authenticated same-record ACK above is the proof barrier.
                yield return new WaitForSecondsRealtime(.1f);
            }
            Require(transfers.Count == active.count && RequireOrb(game.Snapshot, active.orbId).transferCount == active.initialTransferCount + (ulong)active.count,
                "SERIES_FINAL_COUNT_MISMATCH");
        }
        private OrbActionRequest TransferRequest(GameSnapshot snapshot, OrbWire orb, int direction)
            => new OrbActionRequest(snapshot.sessionId, snapshot.roundId, RequestId(), orb.id, "", (OrbActionKind)direction,
                NextSequence(orb), new Vector2(direction == (int)OrbActionKind.TransferLeft ? 0 : 1, active.height));
        private TransferProof ValidateTransfer(GameSnapshot before, GameSnapshot after, int direction, string requestId, bool sending)
        {
            var old = RequireOrb(before, active.orbId); var current = RequireOrb(after, active.orbId);
            ulong peer = old.owner == before.p1.clientId ? before.p2.clientId : before.p1.clientId;
            float inset = controller.Layout.Config.OrbRadiusScreenFraction;
            Require(after.sessionId == before.sessionId && after.roundId == before.roundId && after.revision > before.revision
                && LiveIds(before).SequenceEqual(LiveIds(after)), "TRANSFER_CONTEXT_OR_LIVE_IDS_CHANGED");
            Require(current.id == old.id && current.kind == old.kind && current.polarity == old.polarity && current.owner == peer
                && current.state == (int)OrbAuthorityState.Idle && current.transferCount == old.transferCount + 1
                && current.sequence > old.sequence && current.lastTransferSequence == current.sequence, "TRANSFER_IDENTITY_COUNTER_OR_OWNER_MISMATCH");
            int side = direction == (int)OrbActionKind.TransferLeft ? (int)EntrySide.Right : (int)EntrySide.Left;
            float edge = side == (int)EntrySide.Left ? inset : 1 - inset;
            Require(current.entrySide == side && Math.Abs(current.pos.x - edge) < .00001 && Math.Abs(current.pos.y - active.height) < .00001, "TRANSFER_ENTRY_EDGE_OR_HEIGHT_MISMATCH");
            if (sending) Require(!controller.Views.ContainsKey(current.id), "TRANSFER_SENDER_VIEW_REMAINS");
            else Require(controller.Views.ContainsKey(current.id), "TRANSFER_RECEIVER_VIEW_MISSING");
            return new TransferProof { index = transfers.Count + 1, requestId = requestId, sending = sending, direction = direction,
                localPlayer = controller.Attack.LocalPlayerId, height = active.height, before = Copy(old), after = Copy(current), beforeIds = LiveIds(before), afterIds = LiveIds(after),
                beforeRevision = before.revision, afterRevision = after.revision, beforeHash = GameWire.CanonicalHash(before), afterHash = GameWire.CanonicalHash(after),
                actualViewIds = controller.Views.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(), committedObservation = true, physicalTouch = false };
        }
        private IEnumerator AckBarrier(ulong count, double seconds)
        {
            yield return Await(() => peerAcks.TryGetValue(count, out var ack) && observations.TryGetValue(count, out var seen)
                && T12DiagnosticProtocol.SameRecord(ack, RequireOrb(seen.game, active.orbId)), seconds, "PEER_SAME_RECORD_ACK_TIMEOUT / " + count);
            Event("both_observed_transfer", new AckEvent { localPlayer = controller.Attack.LocalPlayerId, peer = seriesPeer, ack = peerAcks[count] });
        }
        private void OnCommitted()
        {
            try { if (active?.action == "transferSeries" && seriesSession != null && game.Snapshot != null) Observe(game.Snapshot); }
            catch (Exception e) { commandError = e.Message; }
        }
        private void Observe(GameSnapshot snapshot)
        {
            Require(snapshot.sessionId == seriesSession && snapshot.roundId == seriesRound, "SERIES_CONTEXT_CHANGED");
            var orb = RequireOrb(snapshot, active.orbId);
            Require(orb.transferCount >= active.initialTransferCount && orb.transferCount - active.initialTransferCount <= (ulong)active.count, "SERIES_UNEXPECTED_TRANSFER_COUNT");
            if (observations.TryGetValue(orb.transferCount, out var previous))
                Require(T12DiagnosticProtocol.RecordHash(RequireOrb(previous.game, orb.id)) == T12DiagnosticProtocol.RecordHash(orb), "SERIES_SAME_COUNT_RECORD_CHANGED");
            else observations.Add(orb.transferCount, new Observation { game = Copy(snapshot) });
            if (peerAcks.TryGetValue(orb.transferCount, out var ack)) Require(T12DiagnosticProtocol.SameRecord(ack, orb), "PEER_ORB_RECORD_HASH_MISMATCH");
        }
        private void SetAck(OrbWire orb)
        {
            if (outgoingAck != null && outgoingAck.transferCount != orb.transferCount) previousAck = outgoingAck;
            outgoingAck = new T12ValidationAck { runId = RunId, commandId = active.id, planHash = active.PlanHash, sessionId = seriesSession,
                roundId = seriesRound, orbId = active.orbId, transferCount = orb.transferCount, recordHash = T12DiagnosticProtocol.RecordHash(orb) };
            SendAck(); nextAck = Now + .25;
        }
        private bool CurrentSeries() => game.Snapshot != null && game.Snapshot.sessionId == seriesSession && game.Snapshot.roundId == seriesRound;
        private void RefreshMessaging()
        {
            var current = game.Lobby.Connection?.OwnedManager; var currentMessaging = current != null && current.IsListening ? current.CustomMessagingManager : null;
            if (ReferenceEquals(currentMessaging, messaging)) return;
            if (messaging != null) messaging.UnregisterNamedMessageHandler(T12DiagnosticProtocol.MessageName);
            manager = current; messaging = currentMessaging;
            if (messaging != null) messaging.RegisterNamedMessageHandler(T12DiagnosticProtocol.MessageName, ReceiveAck);
        }
        private void SendAck()
        {
            if (outgoingAck == null || messaging == null || manager == null || !manager.IsListening || !game.Lobby.Connected) return;
            // A peer may still be waiting at the previous barrier while our newest observation is
            // ready. Repeat that ACK too; a packet received before its local command was active
            // was correctly ignored and must never strand the later starter.
            if (previousAck != null)
                using (var previous = T12DiagnosticProtocol.Write(previousAck)) messaging.SendNamedMessage(T12DiagnosticProtocol.MessageName, seriesPeer, previous, NetworkDelivery.ReliableFragmentedSequenced);
            using (var writer = T12DiagnosticProtocol.Write(outgoingAck)) messaging.SendNamedMessage(T12DiagnosticProtocol.MessageName, seriesPeer, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        private void ReceiveAck(ulong sender, FastBufferReader reader)
        {
            // An unsolicited packet cannot start a command. Its sender must be the already approved other participant.
            if (active?.action != "transferSeries" || seriesSession == null || !CurrentSeries() || !game.Lobby.Connected
                || game.Lobby.Snapshot?.Find(sender)?.connected != true || !T12DiagnosticProtocol.TryRead(reader, out var ack)
                || !T12DiagnosticProtocol.Matches(ack, active, seriesSession, seriesRound, sender, seriesPeer)) return;
            try
            {
                if (peerAcks.TryGetValue(ack.transferCount, out var old)) Require(old.recordHash == ack.recordHash, "PEER_CONFLICTING_ACK");
                else { peerAcks.Add(ack.transferCount, ack); Event("peer_observed_ack", new AckEvent { localPlayer = controller.Attack.LocalPlayerId, peer = sender, ack = ack }); }
                if (observations.TryGetValue(ack.transferCount, out var observation)) Require(T12DiagnosticProtocol.SameRecord(ack, RequireOrb(observation.game, active.orbId)), "PEER_ORB_RECORD_HASH_MISMATCH");
            }
            catch (Exception e) { commandError = e.Message; }
        }
        private IEnumerator Await(Func<bool> predicate, double seconds, string failure, Action query = null)
        {
            double started = Now; bool queried = false;
            while (!predicate())
            {
                if (commandError != null) throw new InvalidOperationException(commandError);
                if (game.Error.Length > 0) throw new InvalidOperationException("GAME_ERROR / " + game.Error);
                if (!queried && query != null && Now - started >= 3) { queried = true; query(); }
                Require(Now - started < seconds, failure); yield return null;
            }
        }
        private ulong NextSequence(params OrbWire[] orbs)
        {
            ulong current = 0;
            foreach (var orb in orbs)
            {
                current = Math.Max(current, orb.sequence);
                if (localSequences.TryGetValue(orb.id, out var local)) current = Math.Max(current, local);
            }
            Require(current < ulong.MaxValue, "SEQUENCE_EXHAUSTED"); current++;
            foreach (var orb in orbs) localSequences[orb.id] = current;
            return current;
        }
        private static string RequestId() => Guid.NewGuid().ToString("N");
        private bool SameRound(GameSnapshot old) => game.Snapshot != null && game.Snapshot.sessionId == old.sessionId && game.Snapshot.roundId == old.roundId;
        private static OrbWire Orb(GameSnapshot snapshot, string id) => snapshot?.attack?.orbs?.FirstOrDefault(o => o.id == id && o.state != (int)OrbAuthorityState.Consumed);
        private static OrbWire RequireOrb(GameSnapshot snapshot, string id) { var orb = Orb(snapshot, id); Require(orb != null, "LIVE_ORB_MISSING / " + id); return orb; }
        private static string[] LiveIds(GameSnapshot s) => s.attack.orbs.Where(o => o.state != (int)OrbAuthorityState.Consumed).Select(o => o.id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        private static T Copy<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Utc() => DateTime.UtcNow.ToString("O");

        private void BlockManualInput()
        {
            Require(controller.Gestures?.ActivePointerId == null && controller.Gestures?.HasPending != true && !controller.HasCombinationPending && !controller.Resource.HasPending, "EXISTING_MANUAL_INPUT_PENDING");
            // Keep the gameplay controller enabled: its normal permission checks remain in force.
            foreach (var input in FindObjectsByType<OrbPointerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Block(input);
            foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Block(raycaster);
        }
        private void Block(Behaviour input) { blockedInputs.Add(new BehaviourState { target = input, wasEnabled = input.enabled }); input.enabled = false; }
        private void RestoreManualInput()
        { foreach (var state in blockedInputs) if (state.target != null) state.target.enabled = state.wasEnabled; blockedInputs.Clear(); }
        private void OnAttack(AttackRequestReply value) { Keep(attackReplies, Copy(value)); Event("attack_receipt", value); }
        private void OnGeneration(ResourceRequestReply value) { Keep(generationReplies, Copy(value)); Event("generation_receipt", value); }
        private void OnCombination(CombinationReply value) { Keep(combinationReplies, Copy(value)); Event("combination_receipt", value); }
        private void OnRecovery(ResourceRecoveryResult value) => Event("hit_stamina_recovery", new RecoveryEvent { playerId = value.PlayerId,
            accepted = value.Accepted, duplicate = value.IsDuplicate, applied = value.Applied, reason = value.Reason,
            before = value.StaminaBefore, after = value.StaminaAfter, added = value.Added, source = "VALID_HIT_RECOVERY_EVENT" });
        private void OnHit(AttackHitResult value) => Event("real_valid_hit", new HitEvent { sessionId = value.SessionId, roundId = value.RoundId,
            orbId = value.OrbId, attacker = value.AttackerPlayerId, applied = value.Applied, reason = value.Reason, damage = value.Damage,
            hpBefore = value.HpBefore, hpAfter = value.HpAfter, source = "HOST_RIGIDBODY_COLLIDER_HIT_EVENT" });
        private static void Keep<T>(List<T> list, T item) { list.Add(item); if (list.Count > 2048) list.RemoveAt(0); }
        private void Event<T>(string kind, T value)
        {
            if (eventsWriter == null) return;
            try
            {
                eventsWriter.WriteLine(JsonUtility.ToJson(new EventLine { utc = Utc(), realtime = Now, runId = RunId, commandId = active?.id ?? "",
                    kind = kind, localPlayer = game?.Lobby.Connection?.LocalClientId ?? ulong.MaxValue, round = game?.Snapshot?.roundId ?? 0,
                    revision = game?.Snapshot?.revision ?? 0, payloadJson = JsonUtility.ToJson(value), committedProof = false }));
            }
            catch (Exception e)
            {
                // Diagnostic storage cannot throw through a gameplay receipt/hit callback. Once
                // evidence is lost this run cannot execute another command or report success.
                bool first = evidenceError == null;
                evidenceError = "DIAGNOSTIC_EVENT_EVIDENCE_WRITE_FAILED / " + e.Message;
                commandError = evidenceError; LastStatus = "FAIL"; LastError = evidenceError;
                if (first) Debug.LogError("C6_T12_EVIDENCE_FAILED " + evidenceError);
            }
        }
        private void SampleClock()
        {
            var s = game.Snapshot; if (s == null) return;
            var sample = new ClockSample { utc = Utc(), realtime = Now, localPlayer = controller.Attack.LocalPlayerId, sessionId = s.sessionId, round = s.roundId,
                revision = s.revision, phase = s.battle.phase, hostNow = s.hostNow, serverTime = s.serverTime, deadline = s.battle.deadline,
                remaining = s.battle.remaining, displayedRemaining = game.DisplayRemaining, hp = s.attack.hp,
                p1Stamina = s.resources.players.FirstOrDefault(p => p.playerId == s.p1.clientId)?.stamina ?? -1,
                p2Stamina = s.resources.players.FirstOrDefault(p => p.playerId == s.p2.clientId)?.stamina ?? -1, source = "COMMITTED_AGGREGATE_AND_LOCAL_CLOCK_DISPLAY" };
            clocks.Add(sample); if (clocks.Count > 4096) clocks.RemoveAt(0); clockWriter?.WriteLine(JsonUtility.ToJson(sample));
        }
        private CaptureState Capture()
        {
            var snapshot = Copy(game.Snapshot); var lobby = game.Lobby; var discovery = lobby.Discovery;
            return new CaptureState { utc = Utc(), realtime = Now, build = game.BuildIdentifier, buildGuid = Application.buildGUID, unity = Application.unityVersion,
                platform = Application.platform.ToString(), device = SystemInfo.deviceModel, width = Screen.width, height = Screen.height, safeArea = Screen.safeArea,
                localPlayer = lobby.Connection?.LocalClientId ?? ulong.MaxValue, attached = game.Attached, initialConfirmed = game.InitialConfirmed,
                gameError = game.Error, gameStatus = game.Status, lobbyStatus = lobby.Status, lobbyError = lobby.Error, joinRoute = lobby.JoinRoute,
                gameEntries = snapshot == null ? Array.Empty<GameSnapshot>() : new[] { snapshot }, aggregateHash = snapshot == null ? "" : GameWire.CanonicalHash(snapshot),
                lobbyEntries = lobby.Snapshot == null ? Array.Empty<LobbySnapshot>() : new[] { Copy(lobby.Snapshot) }, hostConfigJson = lobby.HostConfig == null ? "null" : JsonUtility.ToJson(lobby.HostConfig),
                runtimeConfigJson = JsonUtility.ToJson(controller.Layout.Config), rawAttackJson = controller.Attack.Snapshot == null ? "null" : JsonUtility.ToJson(controller.Attack.Snapshot),
                rawResourceJson = controller.Resource.Snapshot == null ? "null" : JsonUtility.ToJson(controller.Resource.Snapshot),
                rawBattleJson = controller.Battle.Snapshot == null ? "null" : JsonUtility.ToJson(controller.Battle.Snapshot),
                rawChildrenAreSeparateDiagnostics = true, displayedRemaining = game.DisplayRemaining,
                ownViewIds = controller.Views.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray(), touchBegins = controller.TouchBegins, touchLaunches = controller.TouchLaunches,
                attackProjectiles = controller.Attack.ActiveProjectileCount, attackPending = controller.Gestures?.HasPending ?? false,
                generationPending = controller.Resource.HasPending, combinationPending = controller.Combination.HasPending,
                canInteract = controller.CanInteract, advertising = discovery?.IsAdvertising ?? false, advertisingConfirmed = discovery?.AdvertisingConfirmed ?? false,
                browsing = discovery?.IsBrowsing ?? false, registeredServiceName = discovery?.RegisteredServiceName, discoveryError = discovery?.LastError,
                discoveredRooms = discovery == null ? Array.Empty<RoomState>() : discovery.Rooms.Select(r => new RoomState { roomId = r.RoomId, name = r.Name,
                    address = r.Address, port = r.Port, participants = r.Participants, build = r.Build, configHash = r.ConfigHash, protocol = r.ProtocolVersion,
                    status = r.Status.ToString(), discoveryKey = r.DiscoveryKey, lastSeen = r.LastSeenSeconds, expiresAt = r.ExpiresAtSeconds }).ToArray(),
                hud = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled && t.gameObject.activeInHierarchy)
                    .Select(t => new HudText { name = t.name, text = t.text }).OrderBy(t => t.name, StringComparer.Ordinal).ToArray(),
                pointerInputs = FindObjectsByType<OrbPointerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(p => new PointerState { enabled = p.enabled,
                    touches = p.TouchSampleCount, mouse = p.MouseSampleCount, cancels = p.TouchCancelCount, lastTouchId = p.LastTouchId, lastDevice = p.LastTouchDeviceId }).ToArray() };
        }
        private void WriteStatus() => AtomicJson(Path.Combine(DirectoryPath, "status.json"), new StatusReport { schema = 1, runId = RunId, directory = DirectoryPath,
            utc = Utc(), realtime = Now, commandId = ActiveCommandId, action = active?.action ?? "", status = LastStatus, error = LastError,
            build = game.BuildIdentifier, localPlayer = game.Lobby.Connection?.LocalClientId ?? ulong.MaxValue, attached = game.Attached,
            phase = game.Snapshot?.battle.phase ?? game.Lobby.Snapshot?.phase ?? "NoRoom", round = game.Snapshot?.roundId ?? 0,
            revision = game.Snapshot?.revision ?? 0, transfersConfirmed = transfers.Count, localAck = outgoingAck?.transferCount ?? 0,
            peerAck = peerAcks.Count == 0 ? 0 : peerAcks.Keys.Max(), inputTemporarilyBlocked = blockedInputs.Count != 0, physicalTouch = false });
        private static void AtomicJson(string path, object value) => AtomicBytes(path, new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value, true)));
        private static void AtomicBytes(string path, byte[] bytes)
        {
            string temporary = path + ".tmp"; using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        private void OnDestroy()
        {
            closing = true; RestoreManualInput();
            if (game != null) game.Changed -= OnCommitted;
            if (controller != null)
            {
                controller.Attack.RequestResolved -= OnAttack; controller.Attack.ValidHit -= OnHit;
                controller.Resource.GenerationResolved -= OnGeneration; controller.Resource.RecoveryResolved -= OnRecovery;
                controller.Combination.RequestResolved -= OnCombination;
            }
            if (messaging != null) messaging.UnregisterNamedMessageHandler(T12DiagnosticProtocol.MessageName);
            eventsWriter?.Dispose(); clockWriter?.Dispose();
        }

        private sealed class Observation { public GameSnapshot game; }
        private sealed class BehaviourState { public Behaviour target; public bool wasEnabled; }
        [Serializable] public sealed class CommandReport
        { public int schema; public T12DiagnosticCommand command; public string runId, commandId, planHash, status, error, startedUtc, finishedUtc, screenshot;
          public bool automatedInput, physicalTouch, actualDevice; public CaptureState before, after; public TransferProof[] transfers;
          public AttackRequestReply[] attackReplies; public ResourceRequestReply[] generationReplies; public CombinationReply[] combinationReplies;
          public GameStateProof[] proofs; public ClockSample[] clockSamples; }
        [Serializable] public sealed class CaptureState
        { public string utc, build, buildGuid, unity, platform, device, aggregateHash, gameError, gameStatus, lobbyStatus, lobbyError, joinRoute,
            hostConfigJson, runtimeConfigJson, rawAttackJson, rawResourceJson, rawBattleJson, registeredServiceName, discoveryError;
          public double realtime, displayedRemaining; public int width, height, touchBegins, touchLaunches, attackProjectiles; public Rect safeArea;
          public ulong localPlayer; public bool attached, initialConfirmed, rawChildrenAreSeparateDiagnostics, attackPending, generationPending, combinationPending,
            canInteract, advertising, advertisingConfirmed, browsing; public GameSnapshot[] gameEntries; public LobbySnapshot[] lobbyEntries;
          public string[] ownViewIds; public HudText[] hud; public RoomState[] discoveredRooms; public PointerState[] pointerInputs; }
        [Serializable] public sealed class TransferProof
        { public int index, direction; public string requestId, beforeHash, afterHash; public bool sending, committedObservation, physicalTouch;
          public ulong localPlayer, beforeRevision, afterRevision; public float height; public OrbWire before, after; public string[] beforeIds, afterIds, actualViewIds; }
        [Serializable] public sealed class ClockSample
        { public string utc, sessionId, phase, source; public uint round; public ulong localPlayer, revision; public int hp;
          public double realtime, hostNow, serverTime, deadline, remaining, displayedRemaining, p1Stamina, p2Stamina; }
        [Serializable] public sealed class HudText { public string name, text; }
        [Serializable] public sealed class PointerState { public bool enabled; public int touches, mouse, cancels, lastTouchId, lastDevice; }
        [Serializable] public sealed class RoomState
        { public string roomId, name, address, build, configHash, status, discoveryKey; public int port, participants, protocol; public double lastSeen, expiresAt; }
        [Serializable] private sealed class StatusReport
        { public int schema, transfersConfirmed; public string runId, directory, utc, commandId, action, status, error, build, phase;
          public double realtime; public ulong localPlayer, revision, localAck, peerAck; public uint round; public bool attached, inputTemporarilyBlocked, physicalTouch; }
        [Serializable] private sealed class EventLine
        { public string utc, runId, commandId, kind, payloadJson; public double realtime; public ulong localPlayer, revision; public uint round; public bool committedProof; }
        [Serializable] private sealed class StartEvent { public string build, platform, device, persistentDirectory; public bool automatedInput, physicalTouch; }
        [Serializable] private sealed class Completion { public string id, status, error; }
        [Serializable] private sealed class LifecycleEvent { public bool value; public string sessionId, phase; public double remaining; }
        [Serializable] private sealed class StateDelta { public string requestId, source; public GameSnapshot before, after; }
        [Serializable] private sealed class RequestEvent { public string sessionId, requestId, orbId, action; public uint roundId; public ulong sequence; public Vector2 position; }
        [Serializable] private sealed class AckEvent { public ulong localPlayer, peer; public T12ValidationAck ack; }
        [Serializable] private sealed class RecoveryEvent { public ulong playerId; public bool accepted, duplicate, applied; public string reason, source; public double before, after, added; }
        [Serializable] private sealed class HitEvent { public string sessionId, orbId, reason, source; public uint roundId; public ulong attacker; public bool applied; public int damage, hpBefore, hpAfter; }
    }
}
