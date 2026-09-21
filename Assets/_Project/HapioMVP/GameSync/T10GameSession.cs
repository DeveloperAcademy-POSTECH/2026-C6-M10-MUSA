using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Resources;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync
{
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class T10GameSession : MonoBehaviour
    {
        public const string Build = "15";
        [SerializeField] private bool transfersEnabled;
        [SerializeField] private bool continuousTransfers;
        public bool ContinuousTransfersEnabled => continuousTransfers;
        [SerializeField] private bool reachableEdgeTransferDistance;
        public bool ReachableEdgeTransferDistance => reachableEdgeTransferDistance;
        [SerializeField] private string buildIdentifierOverride = "";
        public bool TransfersEnabled => transfersEnabled;
        [SerializeField] private bool interruptionHandlingEnabled;
        public bool InterruptionHandlingEnabled => interruptionHandlingEnabled;
        public string LastInterruptionReason { get; private set; } = "";
        private readonly ParticipantGameBarrier peerResponses = new ParticipantGameBarrier();
        [SerializeField] private int maximumParticipants = 2;
        public int MaximumParticipants => maximumParticipants;
        public IReadOnlyList<LobbyPlayer> Participants => participants;
        private bool returnToLobbyAfterPause, developmentHoldPeerResponses;
        // Development-only fault injection, enabled explicitly by an external diagnostic command.
        public bool DevelopmentHoldPeerResponses
        {
            get => developmentHoldPeerResponses;
            set
            {
                if (value && !Application.isEditor && !Debug.isDebugBuild)
                    throw new InvalidOperationException("Peer response injection requires a development build.");
                developmentHoldPeerResponses = value;
            }
        }
        public void ConfigureInterruptions(bool enabled)
        {
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose interruption handling before opening a room.");
            interruptionHandlingEnabled = enabled;
        }
        public string BuildIdentifier => string.IsNullOrEmpty(buildIdentifierOverride) ? (transfersEnabled ? "16" : Build)
            : ValidBuildIdentifier(buildIdentifierOverride) ? buildIdentifierOverride
            : throw new InvalidOperationException("The saved build identifier must be a positive number of at most 32 digits.");
        private string StateMessage => continuousTransfers ? "C6.P4.State.v1" : maximumParticipants == 5 ? "C6.P3.State.v1" : "C6.T10B.State.v1";
        private string ControlMessage => continuousTransfers ? "C6.P4.Control.v1" : maximumParticipants == 5 ? "C6.P3.Control.v1" : "C6.T10B.Control.v1";
        private T10LobbySession lobby;
        private T09BattleController controller;
        private ThrowBattleFraming framing;
        private GameRuntimeConfig runtimeConfig;
        private NetworkManager manager;
        private CustomMessagingManager messaging;
        private LobbyStartContract contract;
        private LobbyPlayer p1,p2;
        private LobbyPlayer[] participants = Array.Empty<LobbyPlayer>();
        private double attachedAt,nextPublish,nextSync,initialWaitAt,lastGoodPublish;
        private bool attached,initialConfirmed,autoStart,ending;
        private uint preparedRound;
        private ulong revision;
        private string frozenSignature;
        private readonly Dictionary<ulong,string> initialProofs = new Dictionary<ulong,string>();
        private readonly List<GameStateProof> proofs = new List<GameStateProof>();
        public GameSnapshot Snapshot { get; private set; }
        public string Error { get; private set; } = "";
        public string Status { get; private set; } = "Create or join a room";
        public IReadOnlyList<GameStateProof> Proofs => proofs;
        public T09BattleController Controller => controller;
        public T10LobbySession Lobby => lobby;
        public bool Attached => attached;
        public bool InitialConfirmed => initialConfirmed;
        public bool CanStart => attached && Error.Length==0 && lobby.IsHost && initialConfirmed && controller.Battle.CanStart;
        public int InitialAcks { get; private set; }
        public int RejectedStates { get; private set; }
        public int RejectedControls { get; private set; }
        public event Action Changed;
        // Explicit diagnostic fault injection. It is not enabled by ordinary builds or UI.
        public bool DevelopmentHoldInitialAck { get; set; }
        public double DisplayRemaining
        {
            get
            {
                var s=Snapshot; if(s==null)return 0;
                if(s.battle.phase!="Playing"||manager==null||!manager.IsListening)return s.battle.remaining;
                double elapsed=Math.Max(0,manager.ServerTime.Time-s.serverTime);
                return Math.Max(0,Math.Min(s.battle.duration,s.battle.deadline-s.battle.penaltySeconds-s.hostNow-elapsed));
            }
        }
        /// <summary>#28: this screen's estimate of the Host clock (the Host reads its own), used only for presentation timing.</summary>
        public double? EstimatedHostNow
        {
            get
            {
                if(lobby!=null&&lobby.IsHost)return Now;
                var s=Snapshot; if(s==null||manager==null||!manager.IsListening)return null;
                return s.hostNow+Math.Max(0,manager.ServerTime.Time-s.serverTime);
            }
        }
        private static double Now=>Time.realtimeSinceStartupAsDouble;
        public void ConfigureBuildIdentifier(string value)
        {
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose the build identifier before opening a room.");
            if (!ValidBuildIdentifier(value))
                throw new ArgumentException("Build identifier must be a positive number of at most 32 digits.", nameof(value));
            buildIdentifierOverride = value;
            // Editor setup stores the scene override; an initialized runtime also updates its lobby contract.
            if (lobby != null) lobby.ConfigureBuild(BuildIdentifier);
        }
        private static bool ValidBuildIdentifier(string value) => !string.IsNullOrEmpty(value) && value.Length <= 32
            && value.All(c => c >= '0' && c <= '9') && value.Any(c => c != '0');

        public void ConfigureMaximumParticipants(int value)
        {
            if (value != 2 && value != 5) throw new ArgumentOutOfRangeException(nameof(value));
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose participant capacity before opening a room.");
            if (continuousTransfers && value != 5) throw new InvalidOperationException("Disable continuous transfers before reducing capacity.");
            if (lobby != null) lobby.ConfigureCapacity(value);
            if (controller != null) controller.ConfigureMaximumParticipants(value);
            maximumParticipants = value;
        }

        public void ConfigureContinuousTransfers(bool enabled)
        {
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose continuous transfers before opening a room.");
            if (enabled && (maximumParticipants != 5 || !transfersEnabled))
                throw new InvalidOperationException("Continuous transfers require five-player capacity and transfers.");
            continuousTransfers = enabled;
            if (lobby != null) lobby.ConfigureContinuousTransfers(enabled);
            if (controller != null)
            { controller.ConfigureContinuousTransfers(enabled); controller.Attack.ConfigureContinuousTransfers(enabled); }
        }

        public void ConfigureTransfers(bool enabled)
        {
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose transfer support before opening a room.");
            if (!enabled && continuousTransfers) throw new InvalidOperationException("Disable continuous transfers before transfers.");
            transfersEnabled = enabled;
            // Editor setup saves only the scene flag; Awake applies it after the existing services initialize.
            if (lobby != null && controller != null && runtimeConfig?.Value != null) ApplyTransferConfiguration();
        }
        private void ApplyTransferConfiguration()
        {
            lobby.ConfigureBuild(BuildIdentifier);
            controller.ConfigureTransfers(transfersEnabled);
            controller.ConfigureReachableEdgeTransferDistance(reachableEdgeTransferDistance);
            controller.Attack.ConfigureTransfers(transfersEnabled, runtimeConfig.Value.OrbStorageLimit,
                runtimeConfig.Value.OrbRadiusScreenFraction);
        }
        public void ConfigureReachableEdgeTransferDistance(bool enabled)
        {
            if (attached || lobby != null && lobby.Connection != null && !lobby.Connection.CanStart)
                throw new InvalidOperationException("Choose edge input before opening a room.");
            reachableEdgeTransferDistance = enabled;
            if (controller != null) controller.ConfigureReachableEdgeTransferDistance(enabled);
        }
        private void Awake()
        {
            lobby = GetComponent<T10LobbySession>();
            controller = GetComponent<T09BattleController>();
            framing = GetComponent<ThrowBattleFraming>();
            runtimeConfig = GetComponent<GameRuntimeConfig>();
            if (framing == null)
            {
                throw new InvalidOperationException("ThrowBattleFraming component is required.");
            }
            lobby.Configure(runtimeConfig.Value); ConfigureMaximumParticipants(maximumParticipants); ApplyTransferConfiguration();
            ConfigureContinuousTransfers(continuousTransfers);
            controller.ConfigureApprovedLifecycle(StartPreparedRound,Retry,Leave);
            controller.ConfigureHostClock(()=>EstimatedHostNow);
            controller.Hud.CoordinatedGame=true;
            lobby.StartConfirmed+=Attach;
            lobby.Changed+=OnLobbyChanged;
            lobby.ApplicationPauseChanged+=HandleApplicationPause;
        }
        private void Start()
        {
            SetGameVisible(false);
            Debug.Log("C6_T10B_READY build="+BuildIdentifier+" transfers="+transfersEnabled+" initialOrbs=0 stamina=100 cost=20 recovery=20/3 hitRecovery=5");
        }
        private void Attach(LobbyStartContract value)
        {
            if(attached||ending||value==null)return;
            try
            {
                if(lobby.Snapshot?.p2==null || value.roomId!=lobby.Snapshot.roomId || value.sessionId!=lobby.Snapshot.sessionId
                    || value.roundId!=1 || value.configFingerprint!=lobby.Snapshot.configFingerprint
                    || value.continuousTransfers!=continuousTransfers)
                    throw new InvalidOperationException("START_CONTRACT_MISMATCH");
                var ordered = lobby.Snapshot.OrderedPlayers;
                if (ordered == null || ordered.Length < 2 || ordered.Length > maximumParticipants
                    || ordered.Any(player => player == null || !player.connected || !player.ready || !player.initialStateReceived)
                    || ordered.Select(player => player.clientId).Distinct().Count() != ordered.Length)
                    throw new InvalidOperationException("START_ROSTER_MISMATCH");
                if (maximumParticipants == 5 && (value.participantIds == null
                    || !value.participantIds.SequenceEqual(ordered.Select(player => player.clientId))))
                    throw new InvalidOperationException("START_FROZEN_ROSTER_MISMATCH");
                runtimeConfig.Apply(lobby.HostConfig);
                // Apply the approved Host capacity after Config adoption, before any registry binds.
                controller.Attack.ConfigureTransfers(transfersEnabled, runtimeConfig.Value.OrbStorageLimit,
                    runtimeConfig.Value.OrbRadiusScreenFraction);
                contract=Copy(value); participants=ordered.Select(Copy).ToArray(); p1=participants[0]; p2=participants[1];
                if (maximumParticipants == 5) controller.Attack.ConfigureRoster(contract.participantIds);
                controller.Attack.ConfigureContinuousTransfers(continuousTransfers);
                int localPlayerNumber = lobby.Snapshot.LocalPlayerNumber(
                    lobby.Connection.OwnedManager.LocalClientId);

                controller.ConfigurePlayerNumber(localPlayerNumber);
                framing.ConfigureParticipantView(localPlayerNumber, ordered.Length);

                manager=lobby.Connection.OwnedManager; messaging=manager.CustomMessagingManager;
                messaging.RegisterNamedMessageHandler(StateMessage,ReceiveState);
                messaging.RegisterNamedMessageHandler(ControlMessage,ReceiveControl);
                controller.Attack.SetAggregateMode(true);
                controller.Resource.SetAggregateMode(true);
                controller.Battle.SetAggregateMode(true);
                controller.Resource.ConfigureApprovedSeed(contract.seed);
                attached=true; attachedAt=initialWaitAt=lastGoodPublish=Now; preparedRound=1; autoStart=lobby.IsHost;
                initialConfirmed=false; revision=0; Snapshot=null; Error="";
                peerResponses.Configure(lobby.IsHost ? participants.Skip(1).Select(player => player.clientId)
                    : new[] { NetworkManager.ServerClientId });
                initialProofs.Clear(); proofs.Clear(); InitialAcks=0;
                developmentHoldPeerResponses=false; LastInterruptionReason=""; returnToLobbyAfterPause=false;
                if (!controller.Attack.BeginApprovedConnection(contract.sessionId,contract.roundId))
                    throw new InvalidOperationException("APPROVED_GAME_BIND_FAILED");
                Status="Checking all initial game states";
                SetGameVisible(true); Changed?.Invoke();
                Debug.Log($"C6_T10B_ATTACHED role={(lobby.IsHost?"HOST":"CLIENT")} session={contract.sessionId} round=1 seed={contract.seed} config={contract.configFingerprint}");
            }
            catch(Exception e){Fail("GAME_ATTACH_FAILED / "+e.Message);}
        }
        private void OnLobbyChanged()
        {
            if(attached&&!lobby.Connected&&!ending)Fail("PARTICIPANT_DISCONNECTED");
        }
        private void HandleApplicationPause(bool paused)
        {
            if (!interruptionHandlingEnabled) return;
            if (paused)
            {
                var state = lobby.Connection.State;
                if (!attached && state != DirectConnectionState.Connected && state != DirectConnectionState.Connecting
                    && state != DirectConnectionState.StartingHost) return;
                returnToLobbyAfterPause = true;
                LastInterruptionReason = "APPLICATION_BACKGROUNDED";
                if (attached) Fail(LastInterruptionReason);
                // Lobby owns connection shutdown, including a room that has not started a game.
            }
            else if (returnToLobbyAfterPause)
            {
                returnToLobbyAfterPause = false;
                string reason = LastInterruptionReason;
                Leave();
                LastInterruptionReason = reason;
                lobby.ReportInterruption(reason);
                Debug.Log("C6_T13_RETURNED_TO_LOBBY reason=" + reason);
            }
        }
        private void ObservePeerResponse(ulong sender)
        {
            if (!interruptionHandlingEnabled || !initialConfirmed) return;
            peerResponses.Observe(sender, Now);
        }
        private void LateUpdate()
        {
            if(!attached){if(controller!=null&&controller.Hud.Canvas!=null)SetGameVisible(false);return;}
            if(Error.Length!=0){UpdateHud();return;}
            if(manager==null||!manager.IsListening||manager.ShutdownInProgress||!lobby.Connected){Fail("CONNECTION_ENDED");return;}
            if (interruptionHandlingEnabled && initialConfirmed)
            {
                peerResponses.Arm(Now);
                if (peerResponses.TryExpired(Now, DirectConnectionSession.ConnectionTimeoutSeconds, out ulong expired))
                { Debug.Log("C6_P3_EXPIRED_PARTICIPANT id=" + expired); Fail("PEER_GAME_RESPONSE_TIMEOUT"); return; }
            }
            if(lobby.IsHost)
            {
                var a=controller.Attack.Snapshot;
                if(a!=null && a.roundId!=preparedRound)
                { preparedRound=a.roundId; initialConfirmed=false; initialWaitAt=Now; initialProofs.Clear(); frozenSignature=null; peerResponses.Reset(); }
                if(Now>=nextPublish)
                {
                    nextPublish=Now+1.0/runtimeConfig.Value.AttackSnapshotRateHz;
                    try { Publish(); } catch (Exception e) { Fail("SNAPSHOT_CAPTURE_FAILED / "+e.Message); }
                    if (initialConfirmed && Now-lastGoodPublish>2) Fail("CONSISTENT_STATE_TIMEOUT");
                }
                if(autoStart&&CanStart){autoStart=false;StartPreparedRound();}
            }
            else if(Now>=nextSync)
            {
                nextSync=Now+.5;
                if(Snapshot?.battle.phase=="Ready")Acknowledge(Snapshot);
                SendControl(new GameControl{kind="QUERY",nonce=controller.Attack.LocalNonce,roomId=contract.roomId,
                    sessionId=contract.sessionId,roundId=Snapshot?.roundId??contract.roundId,configHash=contract.configFingerprint});
            }
            if(!initialConfirmed&&Now-initialWaitAt>12)Fail("INITIAL_STATE_CONFIRMATION_TIMEOUT");
            UpdateHud();
        }
        private void Publish()
        {
            var a=controller.Attack.Snapshot; var r=controller.Resource.Snapshot; var b=controller.Battle.Snapshot;
            if(a==null||r==null||b==null||r.players.Length!=participants.Length
                ||participants.Any(player=>!controller.Attack.IsAuthenticatedPlayer(player.clientId,controller.Attack.NonceForPlayer(player.clientId))))return;
            if(a.roundId!=r.roundId||a.roundId!=b.roundId)return;
            var candidate=new GameSnapshot{nonce=controller.Attack.LocalNonce,roomId=contract.roomId,sessionId=contract.sessionId,
                roundId=a.roundId,revision=revision+1,configHash=contract.configFingerprint,seed=contract.seed,continuousTransfers=continuousTransfers,
                p1=Copy(p1),p2=Copy(p2),players=maximumParticipants==5?participants.Select(Copy).ToArray():null,initialStateConfirmed=initialConfirmed,hostNow=Now,serverTime=manager.ServerTime.Time,
                attack=Copy(a),resources=Copy(r),battle=Copy(b)};
            SetNonce(candidate,controller.Attack.LocalNonce);
            string signature=JsonUtility.ToJson(candidate.attack)+JsonUtility.ToJson(candidate.resources)+JsonUtility.ToJson(candidate.battle)+initialConfirmed;
            if(candidate.battle.phase!="Playing"&&Snapshot!=null&&frozenSignature==signature){lastGoodPublish=Now;return;}
            if(!GameWire.Validate(candidate,Context(candidate.nonce),Snapshot,out string reason))
            {
                // A service can bind a frame later during initial setup. Persistent failures time out.
                Status="Waiting for consistent Host state / "+reason;
                if(Now-attachedAt>2)Debug.LogWarning("C6_T10B_CAPTURE_REJECT reason="+reason);
                return;
            }
            lastGoodPublish=Now; frozenSignature=signature; Snapshot=candidate; revision=candidate.revision;
            RecordProof(candidate);
            if(candidate.battle.phase=="Ready"&&!initialConfirmed)
            {
                initialProofs[candidate.revision]=GameWire.CanonicalHash(candidate);
                if(initialProofs.Count>256)initialProofs.Remove(initialProofs.Keys.Min());
            }
            foreach (var player in participants.Skip(1)) SendSnapshot(player.clientId,candidate);
            Status=initialConfirmed?candidate.battle.phase:$"Confirming initial state {peerResponses.AcknowledgedCount + 1} / {participants.Length}";
            Changed?.Invoke();
        }
        private GameSnapshotContext Context(string nonce)=>new GameSnapshotContext{roomId=contract.roomId,sessionId=contract.sessionId,
            configHash=contract.configFingerprint,config=lobby.HostConfig,seed=contract.seed,p1=p1.clientId,p2=p2.clientId,participantIds=maximumParticipants==5?contract.participantIds:null,nonce=nonce,
            allowTransfers=transfersEnabled,continuousTransfers=continuousTransfers,
            transferMaximumSpeed=runtimeConfig.Value.OrbMaxReleaseSpeed*C6.Prototype.Orbs.OrbTransferMotion.MaximumReleaseSpeedMultiplier,transferEdgeInset=runtimeConfig.Value.OrbRadiusScreenFraction};
        private void ReceiveState(ulong sender,FastBufferReader reader)
        {
            if(!attached||Error.Length!=0||!GameWire.TryRead(reader,out var incoming))return;
            if(IsDuplicate(sender,incoming))
            { ObservePeerResponse(sender); if(incoming.battle.phase=="Ready")Acknowledge(incoming);return; }
            if(!GameWire.AcceptFromSender(sender,lobby.IsHost,incoming,Context(controller.Attack.LocalNonce),Snapshot,out string reason))
            { RejectedStates++;Debug.Log("C6_T10B_REJECT_STATE reason="+reason);return; }
            if(!controller.Attack.ApplyAggregateSnapshot(Copy(incoming.attack),false)
                ||!controller.Resource.ApplyAggregateSnapshot(Copy(incoming.resources),false)) {Fail("AGGREGATE_APPLY_FAILED");return;}
            controller.Combination.RefreshApprovedBinding();
            if(!controller.Battle.ApplyAggregateSnapshot(Copy(incoming.battle),false)){Fail("BATTLE_APPLY_FAILED");return;}
            bool newRound=Snapshot==null||Snapshot.roundId!=incoming.roundId;
            Snapshot=incoming; initialConfirmed=incoming.initialStateConfirmed;
            if(newRound){initialWaitAt=Now;peerResponses.Reset();}
            ObservePeerResponse(sender);
            controller.Attack.NotifyAggregateChanged(); controller.Resource.NotifyAggregateChanged();
            controller.Combination.NotifyAggregateChanged(); controller.Battle.NotifyAggregateChanged();
            RecordProof(incoming); Status=initialConfirmed?incoming.battle.phase:"Initial game state received / confirming";
            if(incoming.battle.phase=="Ready")Acknowledge(incoming);
            Changed?.Invoke();
        }
        private bool IsDuplicate(ulong sender, GameSnapshot incoming)
        {
            if (Snapshot == null || incoming == null || incoming.roundId != Snapshot.roundId || incoming.revision != Snapshot.revision) return false;
            var previous = Copy(Snapshot); previous.revision--;
            return GameWire.AcceptFromSender(sender, lobby.IsHost, incoming, Context(controller.Attack.LocalNonce), previous, out _)
                && GameWire.CanonicalHash(incoming) == GameWire.CanonicalHash(Snapshot);
        }
        private void Acknowledge(GameSnapshot value)
        {
            if(DevelopmentHoldInitialAck)return;
            SendControl(new GameControl{kind="INITIAL_ACK",nonce=controller.Attack.LocalNonce,roomId=value.roomId,sessionId=value.sessionId,
                roundId=value.roundId,revision=value.revision,configHash=value.configHash,stateHash=GameWire.CanonicalHash(value)});
        }
        private void ReceiveControl(ulong sender,FastBufferReader reader)
        {
            if(!attached||!lobby.IsHost||Error.Length!=0||!GameControl.TryRead(reader,out var c))return;
            if(!peerResponses.Contains(sender)||!controller.Attack.IsAuthenticatedPlayer(sender,c.nonce)||c.roomId!=contract.roomId
                ||c.sessionId!=contract.sessionId||c.configHash!=contract.configFingerprint||c.roundId!=preparedRound)
            {RejectedControls++;return;}
            if(c.kind=="QUERY"){ObservePeerResponse(sender);if(Snapshot!=null)SendSnapshot(sender,Snapshot);return;}
            if(c.kind!="INITIAL_ACK"||Snapshot?.battle.phase!="Ready"||initialConfirmed
                ||!initialProofs.TryGetValue(c.revision,out string expected)||c.stateHash!=expected)
            {RejectedControls++;return;}
            if (!peerResponses.Acknowledge(sender)) { ObservePeerResponse(sender); return; }
            InitialAcks++; initialConfirmed=peerResponses.AllAcknowledged; frozenSignature=null;nextPublish=0;
            if (initialConfirmed) peerResponses.Arm(Now);
            ObservePeerResponse(sender);
            Debug.Log($"C6_T10B_INITIAL_ACK player={sender} count={peerResponses.AcknowledgedCount}/{peerResponses.ExpectedCount} round={preparedRound} revision={c.revision} hash={c.stateHash}");Changed?.Invoke();
        }
        public bool StartPreparedRound()
        {
            if(!CanStart)return false;
            bool started=controller.Battle.HostStartPreparedRound();
            if(started){nextPublish=0;Debug.Log($"C6_T10B_START round={preparedRound} initialConfirmed=true");}
            return started;
        }
        public void Retry()
        {
            if(!attached||!lobby.IsHost||Error.Length!=0||controller.Battle.Phase==BattlePhase.Playing)return;
            autoStart=false;initialConfirmed=false;initialWaitAt=Now;initialProofs.Clear();peerResponses.Reset();
            controller.Battle.RetryHost(); preparedRound=controller.Attack.Snapshot.roundId;
            frozenSignature=null;nextPublish=0;Status="Checking empty next round";
        }
        public void Leave()
        {
            if(ending)return;ending=true;
            controller.CancelInteractions("SESSION_ENDED"); peerResponses.Clear(); developmentHoldPeerResponses=false;
            Unbind();lobby.Leave();controller.Battle.EndSession(); attached=false;Snapshot=null;contract=null;
            initialConfirmed=false;participants=Array.Empty<LobbyPlayer>();initialProofs.Clear();Error="";Status="Create or join a room";ending=false;SetGameVisible(false);Changed?.Invoke();
        }
        private void Fail(string reason)
        {
            if(Error.Length!=0)return;Error=reason;
            Status=interruptionHandlingEnabled?InterruptionMessage(reason):"NETWORK ERROR / "+reason;
            Debug.Log("C6_T10B_NETWORK_ERROR reason="+reason);
            ending=true; peerResponses.Reset();
            if (interruptionHandlingEnabled)
            {
                LastInterruptionReason=reason;
                controller.CancelInteractions(reason); Unbind();
                if(controller?.Attack!=null)controller.Attack.FailNetwork(reason);
                lobby.Leave();
            }
            else
            {
                Unbind(); lobby.Leave();
                if(controller?.Attack!=null)controller.Attack.FailUnconfirmedRequest();
            }
            ending=false; attached=true; SetGameVisible(true); UpdateHud(); Changed?.Invoke();
        }
        private static string InterruptionMessage(string reason)
        {
            switch (reason)
            {
                case "APPLICATION_BACKGROUNDED": return "The app left the foreground. Find or create a new room to continue.";
                case "PEER_GAME_RESPONSE_TIMEOUT": return "The other player stopped responding. Tap END, then connect again.";
                case "INITIAL_STATE_CONFIRMATION_TIMEOUT": return "All game states could not be confirmed. Tap END and reconnect.";
                case "PARTICIPANT_DISCONNECTED":
                case "CONNECTION_ENDED": return "The room connection ended. Tap END, then find or create a new room.";
                default: return "The game state could not be confirmed. Tap END and reconnect.";
            }
        }
        private void SendSnapshot(ulong player,GameSnapshot value)
        {
            if(developmentHoldPeerResponses||messaging==null||manager==null||!manager.IsListening)return;
            string nonce=controller.Attack.NonceForPlayer(player);if(string.IsNullOrEmpty(nonce))return;
            var outgoing=Copy(value);SetNonce(outgoing,nonce);
            using(var writer=GameWire.Write(outgoing))messaging.SendNamedMessage(StateMessage,player,writer,NetworkDelivery.ReliableFragmentedSequenced);
        }
        private void SendControl(GameControl value)
        {if(developmentHoldPeerResponses||messaging==null||manager==null||!manager.IsListening)return;using(var writer=GameControl.Write(value))messaging.SendNamedMessage(ControlMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.ReliableFragmentedSequenced);}
        private static void SetNonce(GameSnapshot value,string nonce){value.nonce=value.attack.nonce=value.resources.nonce=value.battle.nonce=nonce;}
        private static T Copy<T>(T value) where T:class=>value==null?null:JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private void RecordProof(GameSnapshot s)
        {
            proofs.Add(new GameStateProof{round=s.roundId,revision=s.revision,hash=GameWire.CanonicalHash(s),phase=s.battle.phase,
                deadline=s.battle.deadline,hostNow=s.hostNow,remaining=s.battle.remaining,p1Stamina=s.resources.players.Single(p=>p.playerId==p1.clientId).stamina,
                p2Stamina=s.resources.players.Single(p=>p.playerId==p2.clientId).stamina,
                players=participants.Select(player=>new GamePlayerProof{playerNumber=player.playerNumber,playerId=player.clientId,
                    stamina=s.resources.players.Single(resource=>resource.playerId==player.clientId).stamina}).ToArray()});
            if(proofs.Count>8192)proofs.RemoveAt(0);
        }
        private void SetGameVisible(bool visible)
        {
            var l=GetComponent<T10LobbyHud>();if(l?.Canvas!=null)l.Canvas.gameObject.SetActive(!visible);
            if(controller.Hud.Canvas!=null)controller.Hud.Canvas.gameObject.SetActive(visible);
            // Keep services and scene cameras alive; the opaque lobby canvas covers the unused game view.
        }
        private void UpdateHud()
        {
            var h=controller.Hud;if(h?.Canvas==null)return;
            h.SetNetworkFieldsVisible(false);h.HostButton.gameObject.SetActive(false);h.JoinButton.gameObject.SetActive(false);
            h.SoloModeButton.gameObject.SetActive(false);h.DebugFixtureButton.gameObject.SetActive(false);
            h.StartButton.interactable=CanStart;h.EndButton.interactable=true;
            h.LobbyButton.gameObject.SetActive(false);
            if(Error.Length!=0)
            {
                h.GenerateButton.interactable=false;h.StartButton.interactable=false;h.ActionLabel.text="NETWORK ERROR";
                h.DetailLabel.text=interruptionHandlingEnabled?InterruptionMessage(Error):Error;
                if(interruptionHandlingEnabled)h.PhaseLabel.text="C6 / NETWORK ERROR / SESSION ENDED";
                return;
            }
            if(Snapshot==null){h.ActionLabel.text="PREPARING";h.DetailLabel.text=Status;return;}
            var s=Snapshot;double remaining=DisplayRemaining;
            h.ClockLabel.text="TIME "+remaining.ToString("0.0",CultureInfo.InvariantCulture)+"s";
            h.TeamHpLabel.text="TEAM HP "+(s.battle.phase=="Playing"?remaining*s.battle.teamHpDecayPerSecond:s.battle.teamHp).ToString("0.0",CultureInfo.InvariantCulture);
            h.RecoveryLabel.text=maximumParticipants==5
                ? string.Join(" ", participants.Select(player=>$"P{player.playerNumber}:{s.resources.players.Single(resource=>resource.playerId==player.clientId).stamina:0}"))
                : string.Join(" / ", participants.Select(player=>$"P{player.playerNumber} {s.resources.players.Single(resource=>resource.playerId==player.clientId).stamina:0}"));
            h.PhaseLabel.text="C6 / "+s.battle.phase.ToUpperInvariant()+" / "+(initialConfirmed?$"{participants.Length} / {participants.Length}":"CHECKING INITIAL STATE");
            h.ResourceModeLabel.text="+20 / 3s    HIT +5    NORMAL";
            if(!initialConfirmed)h.DetailLabel.text=Status;
        }
        private void Unbind()
        {if(messaging!=null){messaging.UnregisterNamedMessageHandler(StateMessage);messaging.UnregisterNamedMessageHandler(ControlMessage);}messaging=null;manager=null;}
        private void OnDestroy(){lobby.StartConfirmed-=Attach;lobby.Changed-=OnLobbyChanged;lobby.ApplicationPauseChanged-=HandleApplicationPause;Unbind();}
    }
    [Serializable] public sealed class GameStateProof
    {public uint round;public ulong revision;public string hash,phase;public double deadline,hostNow,remaining,p1Stamina,p2Stamina;public GamePlayerProof[] players;}
    [Serializable] public sealed class GamePlayerProof {public int playerNumber;public ulong playerId;public double stamina;}
    [Serializable] internal sealed class GameControl
    {
        public string kind,nonce,roomId,sessionId,configHash,stateHash;
        public uint roundId; public ulong revision;
        internal static FastBufferWriter Write(GameControl c)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(c));
            if(bytes.Length>2048)throw new ArgumentException("Control too large.");
            var writer=new FastBufferWriter(bytes.Length+5,Allocator.Temp);writer.WriteValueSafe((byte)1);writer.WriteValueSafe(bytes.Length);writer.WriteBytesSafe(bytes);return writer;
        }
        internal static bool TryRead(FastBufferReader reader,out GameControl c)
        {
            c=null;int size=reader.Length-reader.Position;if(size<6||size>2053)return false;
            try
            {
                reader.ReadValueSafe(out byte version);reader.ReadValueSafe(out int length);
                if(version!=1||length!=reader.Length-reader.Position||length<1)return false;
                byte[] bytes=new byte[length];reader.ReadBytesSafe(ref bytes,length);
                c=JsonUtility.FromJson<GameControl>(new UTF8Encoding(false,true).GetString(bytes));
                return c!=null&&Guid.TryParse(c.nonce,out _)&&Guid.TryParse(c.roomId,out _)&&Guid.TryParse(c.sessionId,out _)&&c.roundId>0&&c.configHash?.Length==64;
            }
            catch(Exception e)when(e is ArgumentException||e is InvalidOperationException||e is OverflowException){return false;}
        }
    }
}
