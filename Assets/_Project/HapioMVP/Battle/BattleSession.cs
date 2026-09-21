using System;
using C6.Prototype.Attack;
using C6.Prototype.Combination;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>T09 Host clock on the existing authenticated two-participant connection.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleSession : MonoBehaviour
    {
        private const string SnapshotMessage = "C6.T09.Battle.v1";
        private const string SyncMessage = "C6.T09.Sync.v1";
        private AttackSession attack;
        private ResourceSession resources;
        private CombinationSession combinations;
        private ScreenLayoutConfig config;
        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private string sessionId, localNonce;
        private uint roundId;
        private ulong revision;
        private bool aggregateMode;
        public void SetAggregateMode(bool enabled)
        {
            if (attack != null && attack.Connected) throw new InvalidOperationException("Choose snapshot mode before gameplay attaches.");
            aggregateMode = enabled;
        }
        public bool ApplyAggregateSnapshot(BattleSnapshot incoming, bool notify = false)
        {
            if (!aggregateMode || attack == null || !attack.Connected || attack.IsHost || incoming == null
                || !BattleWire.ValidSnapshot(incoming, attack.ParticipantCapacity) || incoming.nonce != attack.Snapshot.nonce
                || incoming.sessionId != attack.Snapshot.sessionId || incoming.roundId != attack.Snapshot.roundId) return false;
            RefreshBinding();
            if (!Connected) return false;
            if (Snapshot != null && incoming.revision == Snapshot.revision)
                return JsonUtility.ToJson(incoming) == JsonUtility.ToJson(Snapshot);
            if (!BattleWire.AcceptsSnapshot(Snapshot, incoming, localNonce, sessionId, roundId, attack.ParticipantCapacity)) return false;
            Snapshot = incoming;
            Status = "Approved aggregate battle state synchronized: " + incoming.phase;
            if (notify) NotifyAggregateChanged();
            return true;
        }
        public void NotifyAggregateChanged()
        {
            if (!aggregateMode) return;
            if (BattleWire.IsTerminal(Phase))
            {
                resources.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
                combinations.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
            }
            Changed?.Invoke();
        }
        // T10-B already established the fresh round and verified both initial snapshots.
        public bool HostStartPreparedRound()
        {
            RefreshBinding();
            if (!aggregateMode || !CanStart) return false;
            if (!Authority.Start(Now)) return false;
            monsterAttack?.Begin(Authority.StartedAt);
            terminalPublished = false;
            PublishSnapshot("approved-playing");
            attack.PublishInventoryChange("t10b-started");
            return true;
        }

        private bool developmentSolo;
        private double? developmentDuration;
        private bool refreshing, changingRound, publishing, endingGameplay;
        private bool hadSession, intentionalEnd, terminalPublished;
        private bool processingHit;
        private double processingTimestamp;
        private string processingSession;
        private uint processingRound;
        private double nextPublishAt, nextSyncAt;
        private string resourceTimeSession;
        private uint resourceTimeRound;
        private double lastResourceNow;
        private HostMonsterAttack monsterAttack; // #28 Host only; recreated with each round
        private static double Now => Time.realtimeSinceStartupAsDouble;

        public HostBattleClock Authority { get; private set; }
        public BattleSnapshot Snapshot { get; private set; }
        public bool Connected => isActiveAndEnabled && attack != null && attack.Connected && resources != null && resources.Connected
            && combinations != null && combinations.Connected && manager != null && manager.IsListening && !manager.ShutdownInProgress
            && sessionId == attack.Snapshot.sessionId && roundId == attack.Snapshot.roundId;
        public bool IsHost => Connected && manager.IsHost && Authority != null;
        public BattlePhase Phase => Snapshot != null && Enum.TryParse<BattlePhase>(Snapshot.phase, out var phase) ? phase : BattlePhase.Boot;
        public bool CanStart => IsHost && !changingRound && Authority.CanStart && !resources.HasPending && !combinations.HasPending;
        public string Status { get; private set; } = "Connect both participants, then the Host starts one battle.";
        public event Action Changed;

        public bool CanAct
        {
            get
            {
                if (!Connected || changingRound) return false;
                if (!manager.IsHost) return Snapshot != null && Snapshot.phase == BattlePhase.Playing.ToString();
                AdvanceClock(processingHit ? processingTimestamp : Now);
                return Authority != null && Authority.Phase == BattlePhase.Playing;
            }
        }

        // Resource time follows the same accepted collision timestamp/deadline as battle decisions.
        // Per-round monotonic clamping also keeps teardown callbacks from moving its clock backwards.
        public double ResourceGameplayNow
        {
            get
            {
                double now = Now;
                var context = attack?.Snapshot;
                if (context == null) return now;
                double value = now;
                if (Authority != null && Authority.Matches(context.sessionId, context.roundId) && Authority.StartedAt > 0)
                {
                    if (processingHit && processingSession == context.sessionId && processingRound == context.roundId) value = processingTimestamp;
                    else if (Authority.Phase == BattlePhase.Playing) value = Math.Min(now, Authority.Deadline);
                    // #28: failed defenses end the round before the deadline; exclude that removed time.
                    else if (Authority.Phase == BattlePhase.Defeat) value = Authority.Deadline - Authority.PenaltySeconds;
                    else if (Authority.Phase == BattlePhase.Victory || Authority.Phase == BattlePhase.NetworkError)
                        value = Authority.StartedAt + Authority.DurationSeconds - Authority.Remaining - Authority.PenaltySeconds;
                }
                if (resourceTimeSession != context.sessionId || resourceTimeRound != context.roundId)
                { resourceTimeSession = context.sessionId; resourceTimeRound = context.roundId; lastResourceNow = value; }
                else lastResourceNow = Math.Max(lastResourceNow, value);
                return lastResourceNow;
            }
        }

        public void Configure(AttackSession attackSession, ResourceSession resourceSession,
            CombinationSession combinationSession, ScreenLayoutConfig sharedConfig)
        {
            if (Connected) throw new InvalidOperationException("End this battle connection before reconfiguring it.");
            Unsubscribe(); ResetNetworkBinding(); Snapshot = null;
            attack = attackSession; resources = resourceSession; combinations = combinationSession; config = sharedConfig;
            if (attack == null || resources == null || combinations == null || config == null)
                throw new ArgumentNullException("T09 requires the existing attack, resource, combination, and shared configuration.");
            attack.ConfigureBattleHooks(() => CanAct, BeforeHostHit, AfterRewardedHit);
            resources.ConfigureGameplayGate(() => CanAct, () => ResourceGameplayNow);
            combinations.ConfigureGameplayGate(() => CanAct);
            if (isActiveAndEnabled) Subscribe();
            RefreshBinding();
        }

        public void ConfigureDevelopmentSolo(bool enabled)
        {
            if (attack != null && (attack.Connected || attack.Connection != null && !attack.Connection.CanStart))
                throw new InvalidOperationException("Choose explicit solo development mode before connecting.");
            if (enabled && !DevelopmentAvailable) throw new InvalidOperationException("Solo mode is available only in development builds.");
            developmentSolo = enabled;
            Status = enabled ? "Explicit DEV SOLO selected; still press Host and Start." : "Normal start requires two authenticated participants.";
            Changed?.Invoke();
        }

        public void ConfigureDevelopmentDuration(double? seconds)
        {
            if (seconds.HasValue && (!DevelopmentAvailable || !BattleWire.Finite(seconds.Value) || seconds.Value <= 0
                || config == null || seconds.Value > config.BattleDurationSeconds))
                throw new ArgumentOutOfRangeException(nameof(seconds), "A development duration must be positive and no longer than the configured battle.");
            if (Connected && (!IsHost || Authority.Phase == BattlePhase.Playing))
                throw new InvalidOperationException("The Host can change development duration only outside Playing.");
            developmentDuration = seconds;
            if (IsHost) ResetToLobby("development-duration-changed");
            Changed?.Invoke();
        }

        private bool DevelopmentAvailable => Application.isEditor || Debug.isDebugBuild;
        private double Duration => developmentDuration ?? config.BattleDurationSeconds;
        private void OnEnable() { Subscribe(); RefreshBinding(); }
        private void Subscribe()
        {
            if (attack != null) { attack.Changed += OnDependencyChanged; if (attack.Connection != null) attack.Connection.Changed += OnDependencyChanged; }
            if (resources != null) resources.Changed += OnDependencyChanged;
            if (combinations != null) combinations.Changed += OnDependencyChanged;
        }
        private void Unsubscribe()
        {
            if (attack != null) { attack.Changed -= OnDependencyChanged; if (attack.Connection != null) attack.Connection.Changed -= OnDependencyChanged; }
            if (resources != null) resources.Changed -= OnDependencyChanged;
            if (combinations != null) combinations.Changed -= OnDependencyChanged;
        }
        private void OnDependencyChanged() { RefreshBinding(); Changed?.Invoke(); }

        private void RefreshBinding()
        {
            if (refreshing || changingRound) return;
            refreshing = true;
            try
            {
                var candidate = attack?.Connection?.OwnedManager;
                bool transportReady = isActiveAndEnabled && config != null && attack != null && attack.Connected
                    && candidate != null && candidate.IsListening && !candidate.ShutdownInProgress;
                if (!transportReady)
                {
                    if (hadSession && !intentionalEnd) RecordDisconnect();
                    ResetNetworkBinding(); return;
                }
                // Resource and combination services may observe the new round one callback later.
                // This transient binding order is not a disconnected network and must not create an error.
                if (resources == null || !resources.Connected || combinations == null || !combinations.Connected) return;
                if (manager != candidate || !ReferenceEquals(messaging, candidate.CustomMessagingManager))
                {
                    ResetNetworkBinding(); manager = candidate; messaging = candidate.CustomMessagingManager;
                    if (messaging == null) return;
                    messaging.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
                    messaging.RegisterNamedMessageHandler(SyncMessage, ReceiveSync);
                }
                var context = attack.Snapshot;
                if (sessionId != context.sessionId || roundId != context.roundId)
                {
                    bool newSession = sessionId != context.sessionId;
                    sessionId = context.sessionId; roundId = context.roundId; localNonce = context.nonce;
                    intentionalEnd = false; hadSession = true; processingHit = false; terminalPublished = false;
                    if (newSession) revision = 0;
                    Snapshot = null; nextPublishAt = nextSyncAt = 0;
                    if (manager.IsHost)
                    {
                        Authority = new HostBattleClock(Duration, config.TeamHpDecayPerSecond, attack.ParticipantCapacity);
                        Authority.BeginLobby(sessionId, roundId, attack.AuthenticatedPlayerIds.Count, developmentSolo, config.MonsterMaxHpFor(attack.OrderedParticipantIds.Count));
                        monsterAttack = new HostMonsterAttack(config.MonsterAttackFirstDelaySeconds, config.MonsterAttackIntervalSeconds,
                            config.MonsterAttackWarningSeconds, Guid.NewGuid().GetHashCode());
                        PublishSnapshot("lobby-ready");
                    }
                    else { Authority = null; monsterAttack = null; Status = "Waiting for the Host battle state."; }
                }
                if (manager.IsHost && Authority != null)
                {
                    int participants = attack.AuthenticatedPlayerIds.Count;
                    if (participants != Authority.Participants && Authority.SetParticipants(participants)) PublishSnapshot("participants");
                    AdvanceClock(processingHit ? processingTimestamp : Now);
                }
            }
            finally { refreshing = false; }
        }

        private void Update()
        {
            RefreshBinding();
            if (!Connected) return;
            double now = Now;
            if (IsHost)
            {
                AdvanceClock(processingHit ? processingTimestamp : now);
                TickMonsterAttack(now);
                if (now >= nextPublishAt)
                { nextPublishAt = now + 1d / config.AttackSnapshotRateHz; PublishSnapshot(null); }
            }
            else if (!aggregateMode && Snapshot == null && now >= nextSyncAt)
            {
                nextSyncAt = now + 1;
                Send(SyncMessage, NetworkManager.ServerClientId, new BattleSyncPacket { nonce = localNonce, sessionId = sessionId, roundId = roundId });
            }
        }

        public bool HostStart()
        {
            RefreshBinding();
            if (!CanStart) return false;
            if (!ResetToLobby("host-start-fresh-round")) return false;
            double now = Now;
            if (!Authority.Start(now)) return false;
            monsterAttack?.Begin(Authority.StartedAt);
            terminalPublished = false;
            PublishSnapshot("playing");
            // Re-evaluate the shared resource/gesture services after their gate becomes Playing.
            attack.PublishInventoryChange("t09-started");
            return true;
        }

        public bool RetryHost() => IsHost && ResetToLobby("retry-ready");
        public bool ReturnLobby() => IsHost && ResetToLobby("return-lobby");
        private bool ResetToLobby(string stage)
        {
            if (!IsHost) return false;
            changingRound = true;
            bool reset;
            try { reset = attack.ResetDevelopmentRound(); }
            finally { changingRound = false; }
            if (!reset) return false;
            RefreshBinding();
            if (!IsHost) return false;
            Status = "Fresh empty inventory and full Stamina; Host Start is required.";
            PublishSnapshot(stage);
            return true;
        }

        public void EndSession()
        {
            intentionalEnd = true; hadSession = false; processingHit = false;
            if (attack != null) attack.EndDevelopmentTest();
            ResetNetworkBinding(); Snapshot = null;
            Status = "Connection closed. Choose Host or Join to return to the lobby.";
            Changed?.Invoke();
        }

        private void AdvanceClock(double now)
        {
            if (Authority == null || changingRound) return;
            Authority.Advance(now);
            if (Authority.IsTerminal && !terminalPublished) CommitTerminal();
        }
        /// <summary>
        /// #28 Host-only attack progress with the frozen roster (the legacy two-player scene has none, so it never attacks).
        /// A Hit removes team time; start and result are published at once so every screen sees the same attack.
        /// </summary>
        private void TickMonsterAttack(double now)
        {
            if (monsterAttack == null || Authority == null || changingRound || processingHit || Authority.Phase != BattlePhase.Playing) return;
            int started = monsterAttack.Sequence;
            ulong target = monsterAttack.Target;
            var result = monsterAttack.Tick(now, attack.OrderedParticipantIds);
            if (result == MonsterAttackResult.Hit) Authority.ApplyTimePenalty(now, config.DefenseFailPenaltySeconds);
            if (result != MonsterAttackResult.None)
                Debug.Log($"C6_MONSTER_ATTACK stage=result round={roundId} attack={monsterAttack.ResolvedSequence} target={target} result={result} penaltySeconds={Authority.PenaltySeconds:R} phase={Authority.Phase}");
            if (monsterAttack.Sequence != started)
                Debug.Log($"C6_MONSTER_ATTACK stage=warning round={roundId} attack={monsterAttack.Sequence} target={monsterAttack.Target} warningSeconds={monsterAttack.WarningSeconds:R}");
            if (result == MonsterAttackResult.None && monsterAttack.Sequence == started) return;
            if (Authority.IsTerminal && !terminalPublished) CommitTerminal();
            else PublishSnapshot(null);
        }
        private bool BeforeHostHit(double now)
        {
            if (!IsHost || processingHit || Authority == null) return false;
            bool allowed = Authority.CanApplyHit(sessionId, roundId, now);
            if (!allowed)
            { if (Authority.IsTerminal && !terminalPublished) CommitTerminal(); return false; }
            processingHit = true; processingTimestamp = now; processingSession = sessionId; processingRound = roundId;
            return true;
        }
        private void AfterRewardedHit(double now, AttackHitResult hit)
        {
            if (!processingHit) return;
            try
            {
                if (Authority == null || !Authority.Matches(processingSession, processingRound)) return;
                if (hit != null && hit.Applied && hit.SessionId == processingSession && hit.RoundId == processingRound)
                    Authority.ObserveAppliedHit(processingSession, processingRound, processingTimestamp, hit.HpAfter);
                else Authority.Advance(processingSession, processingRound, processingTimestamp);
                if (Authority.IsTerminal && !terminalPublished) CommitTerminal();
                else PublishSnapshot(hit != null && hit.Applied ? "hit-after-recovery" : null);
            }
            finally { processingHit = false; }
        }
        private void CommitTerminal()
        {
            if (Authority == null || !Authority.IsTerminal || terminalPublished || endingGameplay) return;
            terminalPublished = true; endingGameplay = true;
            try
            {
                resources.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
                combinations.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
                PublishSnapshot("result-after-resource-recovery");
                if (attack != null && attack.IsHost) attack.EndGameplayRound();
            }
            finally { endingGameplay = false; }
        }

        private BattleSnapshot BuildSnapshot(string nonce)
        {
            return new BattleSnapshot
            {
                nonce = nonce, sessionId = Authority.SessionId, roundId = Authority.RoundId, revision = revision,
                phase = Authority.Phase.ToString(), startedAt = Authority.StartedAt, deadline = Authority.Deadline,
                remaining = Authority.Remaining, teamHp = Authority.TeamHp, duration = Authority.DurationSeconds,
                teamHpDecayPerSecond = Authority.TeamHpDecayPerSecond, penaltySeconds = Authority.PenaltySeconds,
                observedMonsterHp = Authority.ObservedMonsterHp,
                monsterMaxHp = config.MonsterMaxHpFor(attack.OrderedParticipantIds.Count), developmentSolo = Authority.DevelopmentSolo,
                shortDuration = Authority.DurationSeconds < config.BattleDurationSeconds, participants = Authority.Participants,
                attackSequence = monsterAttack?.Sequence ?? 0, attackTarget = monsterAttack?.Target ?? 0UL,
                attackWarningStartsAt = monsterAttack?.WarningStartsAt ?? 0, attackWarningEndsAt = monsterAttack?.WarningEndsAt ?? 0,
                // A round that ended mid-warning keeps the attack for the record but no longer warns anyone.
                attackActive = monsterAttack != null && monsterAttack.Active && Authority.Phase == BattlePhase.Playing,
                attackResolvedSequence = monsterAttack?.ResolvedSequence ?? 0,
                attackResult = (int)(monsterAttack?.LastResult ?? MonsterAttackResult.None)
            };
        }
        private void PublishSnapshot(string stage)
        {
            if (publishing || Authority == null || !Connected || !manager.IsHost) return;
            publishing = true;
            try
            {
                revision++;
                Snapshot = BuildSnapshot(localNonce);
                if (!aggregateMode) foreach (ulong participant in attack.AuthenticatedPlayerIds)
                    if (participant != manager.LocalClientId) Send(SnapshotMessage, participant, BuildSnapshot(attack.NonceForPlayer(participant)));
                Status = Snapshot.phase == BattlePhase.Playing.ToString() ? "Host clock is running." : "Battle state: " + Snapshot.phase;
                if (!string.IsNullOrEmpty(stage))
                    Debug.Log($"C6_T09_STATE stage={stage} role=Host session={sessionId} round={roundId} revision={revision} phase={Snapshot.phase} remaining={Snapshot.remaining:R} teamHp={Snapshot.teamHp:R} monsterHp={Snapshot.observedMonsterHp} duration={Snapshot.duration:R} solo={Snapshot.developmentSolo} short={Snapshot.shortDuration} participants={Snapshot.participants}");
                Changed?.Invoke();
            }
            finally { publishing = false; }
        }
        private void ReceiveSync(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || sender == manager.LocalClientId || !BattleWire.TryRead<BattleSyncPacket>(reader, out var packet)
                || !BattleWire.ValidSync(packet) || packet.sessionId != sessionId || packet.roundId != roundId
                || !attack.IsAuthenticatedPlayer(sender, packet.nonce)) return;
            AdvanceClock(processingHit ? processingTimestamp : Now);
            PublishSnapshot("authenticated-sync");
        }
        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (aggregateMode || manager == null || manager.IsHost || sender != NetworkManager.ServerClientId || !Connected
                || !BattleWire.TryRead<BattleSnapshot>(reader, out var incoming)
                || !BattleWire.AcceptsSnapshot(Snapshot, incoming, localNonce, sessionId, roundId, attack.ParticipantCapacity)) return;
            bool changedPhase = Snapshot == null || incoming.phase != Snapshot.phase;
            Snapshot = incoming;
            if (BattleWire.IsTerminal(Phase))
            {
                resources.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
                combinations.ClearPendingForConfirmedRoundEnd(sessionId, roundId);
            }
            Status = "Host battle state synchronized: " + incoming.phase;
            if (changedPhase) Debug.Log($"C6_T09_STATE stage=snapshot role=Client session={sessionId} round={roundId} revision={incoming.revision} phase={incoming.phase} remaining={incoming.remaining:R} teamHp={incoming.teamHp:R} monsterHp={incoming.observedMonsterHp} duration={incoming.duration:R} solo={incoming.developmentSolo} short={incoming.shortDuration} participants={incoming.participants}");
            Changed?.Invoke();
        }

        private void RecordDisconnect()
        {
            if (Authority != null)
            {
                Authority.NetworkError(processingHit ? processingTimestamp : Now);
                revision++;
                Snapshot = BuildSnapshot(localNonce);
            }
            else if (Snapshot != null && Snapshot.phase != BattlePhase.Victory.ToString() && Snapshot.phase != BattlePhase.Defeat.ToString())
            {
                // Preserve confirmed time/HP. A disconnected Client never extrapolates a game result.
                Snapshot = JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(Snapshot));
                Snapshot.phase = BattlePhase.NetworkError.ToString();
                Snapshot.locallyDetectedNetworkError = true;
            }
            Status = Snapshot != null && (Snapshot.phase == BattlePhase.Victory.ToString() || Snapshot.phase == BattlePhase.Defeat.ToString())
                ? "The connection ended after its confirmed result. Close to reconnect manually."
                : "NETWORK_ERROR: connection ended; no automatic replay or reconnection.";
            Debug.Log("C6_T09_NETWORK_ERROR retainedConfirmedTimeAndHp=true automaticReconnect=false");
        }
        private bool Send<T>(string message, ulong receiver, T packet)
        {
            if (!Connected || messaging == null) return false;
            try
            {
                using (var writer = BattleWire.Write(packet))
                    messaging.SendNamedMessage(message, receiver, writer, NetworkDelivery.ReliableFragmentedSequenced);
                return true;
            }
            catch (Exception exception) { Debug.LogWarning("C6_T09_SEND_FAILED type=" + exception.GetType().Name); return false; }
        }
        private void ResetNetworkBinding()
        {
            if (messaging != null)
            { messaging.UnregisterNamedMessageHandler(SnapshotMessage); messaging.UnregisterNamedMessageHandler(SyncMessage); }
            messaging = null; manager = null; Authority = null; monsterAttack = null; sessionId = localNonce = null; roundId = 0;
            hadSession = false; terminalPublished = false; processingHit = false; nextSyncAt = nextPublishAt = 0;
        }
        private void OnApplicationPause(bool paused)
        {
            // The round clock uses elapsed Host time. Resume cannot grant additional battle time.
            if (!paused && IsHost) { AdvanceClock(Now); PublishSnapshot("resume-host-clock"); }
        }
        private void OnDisable() { Unsubscribe(); EndSession(); }
        private void OnDestroy() { Unsubscribe(); ResetNetworkBinding(); Changed = null; }
    }
}
