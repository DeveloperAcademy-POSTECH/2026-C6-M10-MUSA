using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace C6Lab
{
    [Serializable]
    internal sealed class LabNetworkPacket
    {
        public int protocol = 3;
        public string type;
        public string sessionId;
        // Connected Netcode client IDs, independent of their chosen P numbers.
        public ulong[] seats;
        public LabPlayerSlotAssignment[] slotAssignments;
        public int slotNumber;
        public bool accepted;
        public bool restarting;
        public long lobbyRevision;
        public string roomName;
        public LabSnapshot snapshot;
        public LabPaintFrame paintFrame;
        public LabActionReceipt actionReceipt;
        public LabBoardFrame boardFrame;
        public ulong ownershipVersion, secondOwnershipVersion;
        public string provisionalId;
        public long requestId;
        public string firstId;
        public string secondId;
        /// <summary>True only for owner-local passive contact combination, not a player release.</summary>
        public bool passiveCombine;
        /// <summary>The orbs a blast flung, in a holder's blastHit report (#213).</summary>
        public string[] ids;
        /// <summary>A projectile thrown from a blast-shrunk orb (#213).</summary>
        public bool small;
        /// <summary>Why a transferred body crossed its edge (LabEdgeCrossingKind).</summary>
        public int crossing;
        public uint round;
        /// <summary>Exact flash generation for a shake-release request; zero is not a valid request.</summary>
        public uint flashGeneration;
        public int damage;
        public int impactValue;
        public float paintX, paintY, paintZ;
        public float paintR, paintG, paintB, paintA;
        public bool right;
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float vz;
        public float z;
        public float wx;
        public float wy;
        public float wz;
        public float spinImpulse;
        /// <summary>던지는 공 크기 배율 (#222 콤보 페널티/보상). 0(옛 패킷)이면 1로 본다.</summary>
        public float sizeScale;
        public string reason;
        public ulong owner;
        public int element;
        public int targetParticipants;
        public int configRevision;
        public string configFingerprint;
        public float[] tuning;
        public long snapshotSequence;
        public int chunkIndex;
        public int chunkCount;
        public string chunkData;
    }

    /// <summary>
    /// Client-side gate for authoritative impact presentation. A shot can display once in the
    /// current Host round; stale, malformed, and repeated messages never replay the effect.
    /// </summary>
    public sealed class LabImpactReceiptGate
    {
        private readonly HashSet<string> seenProjectileIds = new HashSet<string>(StringComparer.Ordinal);
        private string activeSession;
        private uint activeRound;

        public void SetActive(string session, uint round)
        {
            if (activeSession == session && activeRound == round) return;
            activeSession = session;
            activeRound = round;
            seenProjectileIds.Clear();
        }

        public void Reset() => SetActive(null, 0);

        public bool TryAccept(string session, uint round, string projectileId, LabOrbElement element,
            int damage, Vector3 targetLocalPoint, Vector3 impactVelocity, float signedYawImpulse = 0f)
        {
            if (string.IsNullOrEmpty(activeSession) || activeRound == 0 || session != activeSession
                || round != activeRound || string.IsNullOrEmpty(projectileId)
                // Relay and bomb projectiles legitimately carry None; the Host already
                // confirmed their damage before sending this presentation event.
                || element < LabOrbElement.None || element > LabOrbElement.Earth || damage <= 0
                || !Finite(targetLocalPoint) || !Finite(impactVelocity)
                || float.IsNaN(signedYawImpulse) || float.IsInfinity(signedYawImpulse)
                || Mathf.Abs(signedYawImpulse) > 1440f) return false;
            return seenProjectileIds.Add(projectileId);
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    /// <summary>
    /// Maps unscaled transport time to a Host-owned battle time. Connection timeouts and room
    /// discovery still use real time; only gameplay expiry, recovery and damage use this clock.
    /// </summary>
    public sealed class LabBattleClock
    {
        private double pausedAt;
        private double pausedDuration;
        public bool IsPaused { get; private set; }

        public double Now(double realTime) =>
            (IsPaused ? pausedAt : realTime) - pausedDuration;

        public bool Pause(double realTime)
        {
            if (IsPaused || !Valid(realTime)) return false;
            pausedAt = realTime;
            IsPaused = true;
            return true;
        }

        public bool Resume(double realTime)
        {
            if (!IsPaused || !Valid(realTime) || realTime < pausedAt) return false;
            pausedDuration += realTime - pausedAt;
            IsPaused = false;
            return true;
        }

        public void Reset()
        {
            pausedAt = 0d;
            pausedDuration = 0d;
            IsPaused = false;
        }

        private static bool Valid(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    /// <summary>
    /// Local Host/Join experiment. The host alone owns LabModel and session tuning.
    /// Only owner motion is simulated locally; requests and damage are accepted by the host.
    /// </summary>
    public sealed partial class LabNetwork : MonoBehaviour
    {
        private const string MessageName = "C6Lab.Packet.3";
        // P-slot assignments join lobby packets, generate drums join the snapshot with the generate
        // request naming one, Gourd Mode joins the shared config, and a bomb blast carries where it
        // happened so the holder's board can push orbs away, combine sound/haptic settings join the
        // shared config and the Host tells the combiner when a combine is approved; older clients
        // cannot share this room. Version 5621 adds the Paint goal setting and independent
        // approved impact values/directions/colors. Version 5625 adds the minimap on/off, size and position settings (#181).
        // Version 5626 adds the bomb sound volume setting (#183).
        // Version 5627 shares the confirmed room name in lobby packets.
        // #187: six growth settings, fixed 100% target, authoritative cell transactions.
        // #190: Arc/Straight profile adds seven settings; preserve viewer field 111; reject older 112-field peers.
        // 5632: Host-shared side-hit swing tuning for the exposed gourd underside.
        // 5633: Host-shared experimental paint grain presentation (four appended values).
        // 5635: Host-shared four-pattern impact footprints and independent paint palette.
        // 5636: #201 Host-shared chuimsae chance and minimum gap (two appended values).
        // 5637: #204 manual lobby P selection; results name the authoritative lobby revision.
        // 5638: #206 paint transactions commit RGBA and the actual thrower's P number together.
        // 5639: #206 Host-shared player paint pattern presentation toggle (one appended value).
        // 5640: #208 Host-shared clustered repeat growth tuning (two appended values).
        // 5641: shared gourd visual size appended at index 145.
        // 5642: #212 bombs come locked again (an unlocked bomb is a throwable attack orb); heavy bomb DEV 146-149.
        // 5643: #213 blast-burnt, shrunk orbs (orb burnedAt/small, blastHit report); DEV 150-152.
        // 5644: #225 nearby-hit paint drying adds shared DEV 153-156.
        // 5645: #215 swipe-block snapshots and pushed crossings integrate at DEV 157-159; 160 shared values.
        // 5646: #222 gourd color combo experiment (DEV 160-168, combo snapshot, projectile size scale); 169 shared values.
        // 5647: #235 holder expiry notices before board removal.
        // 5648: #241 combo warning (DEV 169); 170 shared values.
        // 5649: #240 owner-local transactions/board deltas and independent Bulk, integrated with #235/#241.
        // 5650: #236 AUTO drum count integrated with #240; 170 shared values.
        // 5651: #246 removes unused tuning and legacy payloads, retaining AUTO; 165 shared values.
        // 5652: #254 board orb trail toggle (DEV 165); 166 shared values.
        // 5653: #258 visible gourd mesh collision and mesh-local approved paint coordinates.
        // 5654: #251 the yokai's sabotage mix announces each sabotage it casts, and fox fire adds the FoxFire orb
        //       kind with its foxFire drop and fox touch messages, and the charm travels in the snapshot and board
        //       header with charmTap wake-ups from the players beside and a charmWoken notice; DEV 166-177, 178 shared values.
        // 5655: #272 per-player Jigwi fire targets and independent Raw burn metadata, DEV 178-183.
        // 5657: #268 flash settings appended after Jigwi (DEV 184-190), explicit ALL target flags;
        //       shared schema 191. 5656 is reserved by the parallel #271 branch.
        // 5658: #271 counting presentation appended at DEV 191 (192 values); fast combo control metadata.
        // 5659: #276 stage-selected scheduler, four Jigwi policy values and game stage settings barrier.
        // 5660: #263 bounded live room presence and creator-owned pending-room registrations.
        // 5661: #278 immutable Lab/Game Bonjour identity before pending-room activation.
        // 5662: #281 2:2 team territory (DEV 196-199, 200 shared values): team cells, winner and fever in the
        //       snapshot, fever in the board header, team-edge handoffs and territory overwrite paint.
        // 5663: #299 bounded paint visual descriptors in the same paint commit; DEV200-201 (202 values).
        // 5664: standalone #301/#305 used different shared layouts with the same version.
        // 5665: combined flash shake DEV202-203 + charm warning DEV204 (205 values),
        //       flash release state and each neighbor's charm rescue progress.
        // 5666: #315 paint-only finish (DEV 79 a fixed-zero slot, 205 values), shared paint finish barrier.
        // 5668: #311/#314 unified gimmick scheduler and Yaguang timetable (DEV 205-224, 225 values), percent chances,
        //       retired bomb/block/sabotage timers.
        // 5669: #324 per-player Yin/Yang streak control (DEV 225-227, 228 shared values).
        // 5670: #326 Blast Burn Seconds range 3-5 -> 3-9 (default 4 -> 6); an older peer would reject the new value.
        // 5671: #328 an unlocked bomb glides in and opens into two attack orbs through "use" (never thrown), a double
        //       tap clears a fox orb through "use" and the side edges stop it; DEV 55 and 211 retired (228 values kept).
        internal const ushort LabProtocolVersion = 5671;
        private const string BombBlastNotice = "BOMB! The blast pushed the orbs around it away.";
        private const string GourdBombNotice = "The gourd dropped a bomb on you! Pass it on before it blows.";
        private const string FoxFireNotice = "Fox fire! Double-tap a blue fox orb to clear it, and keep your Yin and Yang away.";
        /// <summary>More fox orbs than one fox fire ever drops; a longer list is ignored (#251).</summary>
        private const int MaximumFoxFireOrbs = 10;
        private static readonly byte[] LabConnectionToken = Encoding.ASCII.GetBytes("C6PhysicsLab/32");
        private const int MaximumPacketBytes = 32768;
        private const double JoinAttemptSeconds = 12d;
        private const double JoinBudgetSeconds = 60d;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public LabConfig config;
        public LabOrbBoard board;
        public LabTarget target;
        public LabImpactFeedback impactFeedback;
        public Collider battleFloor;
        public Transform projectileRoot;
        public NetworkManager manager;
        public UnityTransport transport;
        public ushort port = 7777;
        public string hostAddress = "127.0.0.1";

        private readonly List<ulong> order = new List<ulong>(5);
        private readonly Dictionary<ulong, long> slotRequestIds = new Dictionary<ulong, long>();
        private readonly HashSet<ulong> configAcknowledgements = new HashSet<ulong>();
        private readonly Dictionary<ulong, long> pauseRequestIds = new Dictionary<ulong, long>();
        private readonly Dictionary<ulong, double> pendingApprovals = new Dictionary<ulong, double>();
        private readonly HashSet<string> visibleOrbIds = new HashSet<string>();
        private readonly LabImpactReceiptGate impactReceipts = new LabImpactReceiptGate();
        private readonly LabPaintSurface clientPaintSurface = new LabPaintSurface();
        private readonly LabPaintSurface receivedPaintSurface = new LabPaintSurface();
        private LabPaintReceiver paintReceiver;
        private LabPaintReceiver PaintReceiver => paintReceiver ?? (paintReceiver = new LabPaintReceiver(receivedPaintSurface));
        private readonly Dictionary<ulong, double> paintCheckpointRequests = new Dictionary<ulong, double>();
        private LabSnapshot pendingPaintSnapshot;
        private string awaitingPaintSession;
        private uint awaitingPaintRound;
        private double nextPaintRequestAt;

        /// <summary>The map committed with the displayed snapshot; never a client simulation.</summary>
        public LabPaintSurface PaintSurface => clientPaintSurface;
        private readonly LabBattleClock battleClock = new LabBattleClock();
        private readonly List<string> joinCandidates = new List<string>();
        private LabModel model;
        private LabPlayerSlots playerSlots;
        private double nextSnapshotAt;

        private long requestSequence;
        private bool handlerRegistered;
        private bool autoStart;
        private bool autoGenerate;
        private bool autoTransfer;
        private bool developerSolo;
        private string autoGeneratedSession;
        private string autoTransferredSession;
        private bool stopping;
        private bool stopAfterDisconnect;
        private bool pendingDeveloperRestart;
        private bool pauseRequestPending;
        private double pauseRequestSentAt;
        private bool slotSelectionPending;
        private long pendingSlotRequestId;
        private int pendingSlotNumber;
        private double slotRequestSentAt;
        private bool pendingSlotApproved;
        private long pendingSlotLobbyRevision;
        private bool locallyPaused;
        private float timeScaleBeforePause = 1f;
        private long lobbySnapshotSequenceFloor;
        private long lobbyRevision;
        private long receivedLobbyRevision;
        private string sessionId;
        private LabSnapshot latest;
        private LabConfig sourceConfig;
        private LabConfig sessionConfig;
        private LabBonjourDiscovery discovery;
        private string advertisedName;
        private IReadOnlyList<LabDiscoveredRoom> discoveredRooms = Array.Empty<LabDiscoveredRoom>();
        private bool roomSearchActive;
        private bool resumeRoomSearchAfterStop;
        private double roomSearchStartedAt;
        private bool roomCacheGraceActive;
        private string roomSearchMessage = string.Empty;
        private bool roomSearchFailed;
        private string connectedRoomName = string.Empty;
        private bool joinFinishPending;
        private bool joinCanceling;
        private bool clientSessionConnected;
        private int targetParticipantCount = 3;
        private int configRevision;
        private string configFingerprint;
        private int joinCandidateIndex;
        private double joinStartedAt;
        private double joinAttemptAt;
        private bool joinInProgress;
        private bool joinRetryPending;
        private bool discoveryJoinActive;
        private string discoveryRoomKey;
        private bool clientConfigReady;
        private long snapshotSequence;
        private long incomingSequence;
        private int incomingChunkCount;
        private string[] incomingChunks;
        private int incomingChunkReceived;

        public event Action Changed;
        public event Action<LabConfig> ConfigApplied;
        /// <summary>내가 던진 구슬이 요괴에 맞았다고 Host가 확정했을 때, 던진 사람 기기에서만 한 번 알린다. (승인 명중값: 페인트 명목 공격 점수)</summary>
        public event Action<int> LocalThrowHit;
        /// <summary>
        /// 마지막으로 내 구슬이 맞은 자리가 박 중심에서 얼마나 옆으로 벗어났는지 (0 = 정중앙, 1 = 가장자리를 스침).
        /// LocalThrowHit을 부르기 바로 전에 정해지므로, 그 이벤트 안에서 읽으면 이번 타격 값이다.
        /// </summary>
        public float LastLocalHitOffset { get; private set; }
        /// <summary>내 판에서 잠긴 폭탄이 터졌을 때 한 번 알린다. (폭탄 ID, 터진 위치 0..1)</summary>
        public event Action<string, Vector2> BombBlasted;
        /// <summary>An orb on this player's board reached the end of its lifetime and went (#235).</summary>
        public event Action<string> OrbExpired;
        /// <summary>
        /// The gourd just blocked this player's own passes one way (#215): to the right or not, and the
        /// seconds it lasts. Raised once per block, on the blocked player's screen only.
        /// </summary>
        public event Action<bool, float> SwipeBlockStarted;
        /// <summary>A block first seen later than this after it began (a reconnect) is not announced again.</summary>
        private const double SwipeBlockAnnounceWindowSeconds = 1d;
        private uint announcedBlockRound;
        private double announcedBlockStart = -1d;
        /// <summary>박이 누군가의 판에 폭탄을 떨어뜨렸을 때 모든 기기에서 한 번 알린다. (받는 사람, 폭탄 ID, 떨어질 위치 0..1)</summary>
        public event Action<ulong, string, Vector2> GourdBombDropped;
        /// <summary>요괴가 통합 추첨으로 방해공작을 걸었을 때 모든 기기에서 한 번 알린다 (#251). (종류, 받는 사람)</summary>
        public event Action<LabSabotageKind, ulong> SabotageCast;
        /// <summary>옆 사람들이 깨워 매혹이 풀렸을 때 모든 기기에서 한 번 알린다 (#251). (깨어난 사람) 시간이 다 되어 풀리면 알리지 않는다.</summary>
        public event Action<ulong> CharmWoken;
        /// <summary>내가 던진 구슬이 요괴를 빗나갔다고 Host가 확정했을 때, 던진 사람 기기에서만 한 번 알린다.</summary>
        public event Action LocalThrowMissed;
        /// <summary>내가 요청한 결합을 Host가 승인했을 때, 결합한 사람 기기에서만 한 번 알린다. (거절되면 알리지 않음)</summary>
        public event Action LocalCombined;
        private string lastMissNoticeId;
        public string Status { get; private set; } = "Choose HOST or JOIN.";
        public string Notice { get; private set; } = string.Empty;
        public string LastShotReport { get; private set; } = string.Empty;
        public int ParticipantCount => order.Count;
        // Retained solely so older saved Scenes/Editor utilities deserialize without losing references.
        // Host authority and the chosen P number are independent in every current room.
        [HideInInspector] public bool fixedHostPosition;
        public int SelectedParticipantCount => playerSlots?.SelectedCount ?? 0;
        public int TargetParticipantCount => targetParticipantCount;
        public int LocalSlotNumber => IsConnected && playerSlots != null ? playerSlots.GetSlot(LocalPlayerId) : 0;
        public bool SlotSelectionPending => slotSelectionPending;
        public bool IsDeveloperRestartPending => pendingDeveloperRestart;
        public bool CanSelectPlayerSlot => IsConnected && !developerSolo && model == null && latest == null
            && (!gameFlowEnabled || !gameRosterLocked)
            && playerSlots != null && order.Contains(LocalPlayerId) && !slotSelectionPending && !pendingDeveloperRestart;
        public ulong GetSlotOwner(int slotNumber) => playerSlots?.GetOwner(slotNumber) ?? ulong.MaxValue;
        public bool IsSearchingRooms => roomSearchActive && discovery != null && discovery.IsBrowsing;
        public IReadOnlyList<LabDiscoveredRoom> DiscoveredRooms => discoveredRooms;
        /// <summary>Includes the NGO shutdown wait after cancellation or a failed attempt.</summary>
        public bool IsJoiningRoom => joinInProgress || joinRetryPending || joinFinishPending;
        public bool IsCancelingJoin => joinCanceling;
        public string JoiningRoomKey => IsJoiningRoom ? discoveryRoomKey ?? string.Empty : string.Empty;
        public float JoinElapsedSeconds => IsJoiningRoom
            ? (float)Math.Max(0d, Time.realtimeSinceStartupAsDouble - joinStartedAt) : 0f;
        public bool RoomSearchFailed => roomSearchFailed;
        public string ConnectedRoomName => IsConnected ? connectedRoomName : string.Empty;
        public string RoomSearchMessage
        {
            get
            {
                if (joinCanceling) return "Canceling connection…";
                if (IsJoiningRoom) return joinFinishPending ? "Closing connection…" : "Connecting to room…";
                // A resumed browse must not erase the reason a join or search failed.
                if (!string.IsNullOrEmpty(roomSearchMessage)) return roomSearchMessage;
                if (!IsSearchingRooms) return "Choose a room or refresh the list.";
                if (discoveredRooms.Count > 0) return "Select a room to join.";
                return Time.realtimeSinceStartupAsDouble - roomSearchStartedAt >= 5d
                    ? "No rooms found yet. Keep the Host app open on the same network. Search continues…"
                    : "Searching for rooms…";
            }
        }
        // Bonjour may rename a duplicate service. Only its confirmed name is shared.
        public string HostRoomName => IsHost ? connectedRoomName : string.Empty;
        public bool HostRoomNamePending => IsHost && discovery != null && discovery.IsAdvertising
            && !discovery.AdvertisingConfirmed;
        public bool SessionConfigReady => IsHost
            ? order.Count > 0 && order.All(id => configAcknowledgements.Contains(id))
            : clientConfigReady;
        public bool IsHost => manager != null && manager.IsHost;
        public bool IsDeveloperSolo => developerSolo;
        /// <summary>Only a player with a drum on their board can generate orbs.</summary>
        public bool HasLocalDrum => LocalDrum() != null;
        public bool DeveloperBuildAvailable => Application.isEditor || Debug.isDebugBuild;
        public bool IsConnected => !joinFinishPending && manager != null && manager.IsListening
            && (manager.IsHost || manager.IsConnectedClient);
        private bool CanBeginConnection => !IsConnected && !IsJoiningRoom
            && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress));
        // Browsing does not occupy a connection: creating a room or using DEV closes it first.
        public bool CanChooseConnection => CanBeginConnection;
        public ulong LocalPlayerId => IsConnected ? manager.LocalClientId : ulong.MaxValue;
        public LabSnapshot Snapshot => latest;
        public bool CanStartBattle => IsHost && !developerSolo && order.Count == targetParticipantCount
            && playerSlots != null && playerSlots.IsComplete(order) && SessionConfigReady
            && (latest == null || latest.phase != LabPhase.Playing);
        public bool IsBattlePaused => latest != null && latest.phase == LabPhase.Playing && latest.paused;
        public bool CanPauseBattle => !GameStartHeld && IsConnected && latest != null && latest.phase == LabPhase.Playing
            && !latest.paused && !pauseRequestPending
            && (IsHost ? model != null && !battleClock.IsPaused : clientConfigReady);
        public bool CanResumeBattle => !GameStartHeld && IsHost && model != null && model.Phase == LabPhase.Playing
            && battleClock.IsPaused;
        public bool CanReturnAllToLobby => IsHost && !developerSolo && model != null;
        public bool CanRestartSharedBattle => !GameStartHeld && DeveloperBuildAvailable && CanReturnAllToLobby
            && model.Phase == LabPhase.Playing && battleClock.IsPaused;

        private void Awake()
        {
            LabMobilePresentationPolicy.Attach(this);
            // GameSession (-1000) can prepare its initial runtime profile before this Awake.
            // Retain the original saved asset if that early installation already captured it.
            if (sourceConfig == null) sourceConfig = config;
            if (manager == null) manager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
            if (impactFeedback == null) impactFeedback = FindFirstObjectByType<LabImpactFeedback>();
            SubscribeBoard(board);
            if (manager != null)
            {
                manager.OnClientConnectedCallback += OnConnected;
                manager.OnClientDisconnectCallback += OnDisconnected;
                manager.OnTransportFailure += OnTransportFailure;
            }
        }

        private void Start()
        {
            ConfigureTargetSpin();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--lab-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out var chosenPort)) port = chosenPort;
                else if (args[i] == "--lab-join" && i + 1 < args.Length) hostAddress = args[i + 1];
                else if (args[i] == "--lab-auto-start") autoStart = true;
                else if (args[i] == "--lab-auto-generate") autoGenerate = true;
                else if (args[i] == "--lab-auto-transfer") autoTransfer = true;
            }
            if (Array.IndexOf(args, "--lab-host") >= 0) Host();
            else if (Array.IndexOf(args, "--lab-join") >= 0) Join(hostAddress);
        }

        /// <summary>The Host chooses 2–5 network participants before creating the room.</summary>
        public bool SetTargetParticipantCount(int count)
        {
            if (!CanChooseConnection || count < 2 || count > 5) return false;
            targetParticipantCount = count;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Install a validated, session-only copy before Host. Never edit the saved asset.</summary>
        public bool ConfigureHostSession(LabConfig candidate, int targetParticipants)
        {
            if (!CanChooseConnection || targetParticipants < 2 || targetParticipants > 5)
            {
                SetNotice("Choose 2–5 players before creating a room.");
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            targetParticipantCount = targetParticipants;
            SetNotice("Room settings prepared for " + targetParticipants + " players.");
            return true;
        }

        /// <summary>
        /// Select P1..PN in a connected room lobby, or 0 to clear the choice. The Host alone
        /// accepts a claim; the requesting client waits for its result and the new lobby state.
        /// </summary>
        public bool RequestPlayerSlot(int slotNumber)
        {
            if (!CanSelectPlayerSlot || slotNumber < 0 || slotNumber > targetParticipantCount)
            {
                SetNotice("Choose an available P number in the room lobby.");
                return false;
            }
            if (IsHost) return TrySelectSlot(LocalPlayerId, slotNumber);
            long id = ++requestSequence;
            if (!Send(NetworkManager.ServerClientId, new LabNetworkPacket
                { type = "slot_select", requestId = id, slotNumber = slotNumber }))
            {
                SetNotice("Could not send the P number selection. Try again.");
                return false;
            }
            slotSelectionPending = true;
            pendingSlotRequestId = id;
            pendingSlotNumber = slotNumber;
            slotRequestSentAt = Time.realtimeSinceStartupAsDouble;
            pendingSlotApproved = false;
            pendingSlotLobbyRevision = 0;
            SetNotice(string.Empty);
            return true;
        }

        private bool TrySelectSlot(ulong playerId, int slotNumber)
        {
            if (!IsHost || developerSolo || model != null || !order.Contains(playerId)
                || playerSlots == null || !playerSlots.TrySelect(playerId, slotNumber, out string reason))
            {
                SetNotice("That P number is unavailable. Choose another.");
                return false;
            }
            SetNotice(string.Empty);
            BroadcastLobby();
            Debug.Log("C6_LAB_SLOT player=" + playerId + " p=" + slotNumber + " result=" + reason);
            if (autoStart && CanStartBattle) StartBattle();
            else TryStartPendingDeveloperRestart();
            return true;
        }

        private void AssignAutomaticTestSlot(ulong playerId)
        {
            // Only explicit scripted smoke tests opt into automatic claims. Normal joins
            // start unselected and never replace another participant's chosen P number.
            if (!autoStart || playerSlots == null || playerSlots.GetSlot(playerId) != 0) return;
            int slot = FirstAvailableSlot();
            if (slot > 0) playerSlots.TrySelect(playerId, slot, out _);
        }

        private int FirstAvailableSlot()
        {
            if (playerSlots == null) return 0;
            for (int slot = 1; slot <= targetParticipantCount; slot++)
                if (playerSlots.GetOwner(slot) == ulong.MaxValue) return slot;
            return 0;
        }

        /// <summary>Host lobby edits invalidate every old ACK and send a new revision.</summary>
        public bool ApplyHostLobbyConfig(LabConfig candidate)
        {
            if (!IsHost || developerSolo || model != null)
            {
                SetNotice("Settings can change only in the Host lobby.");
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            pendingDeveloperRestart = false;
            PublishHostConfigRevision();
            SetNotice("Settings updated; waiting for all players to confirm.");
            return true;
        }

        private void PublishHostConfigRevision()
        {
            configRevision++;
            configFingerprint = LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), targetParticipantCount,
                LabProtocolVersion);
            configAcknowledgements.Clear();
            configAcknowledgements.Add(LocalPlayerId);
            BroadcastLobby();
            foreach (ulong id in order) if (id != LocalPlayerId) SendSessionConfig(id);
        }

        private bool TryInstallSessionConfig(LabConfig candidate, out string error)
        {
            error = "A valid session config is required.";
            if (candidate == null) return false;
            return TryInstallValues(candidate.CaptureDeveloperValues(), out error);
        }

        private bool TryInstallValues(float[] values, out string error)
        {
            error = "A valid session config is required.";
            if (sourceConfig == null && gameFlowEnabled) sourceConfig = config;
            if (sourceConfig == null) return false;
            LabConfig replacement = Instantiate(sourceConfig);
            replacement.name = sourceConfig.name + " (Session Runtime)";
            replacement.hideFlags = HideFlags.DontSave;
            if (!replacement.TryApplyDeveloperValues(values, out error))
            {
                Destroy(replacement);
                return false;
            }
            if (sessionConfig != null) Destroy(sessionConfig);
            sessionConfig = replacement;
            config = replacement;
            ConfigureTargetSpin();
            ConfigApplied?.Invoke(config);
            error = string.Empty;
            return true;
        }

        private void RestoreSourceConfig()
        {
            if (sourceConfig != null && config != sourceConfig)
            {
                config = sourceConfig;
                ConfigureTargetSpin();
                ConfigApplied?.Invoke(config);
            }
            if (sessionConfig != null)
            {
                Destroy(sessionConfig);
                sessionConfig = null;
            }
        }

        private void ConfigureTargetSpin()
        {
            if (config == null || target == null) return;
            target.GetComponent<LabHangingTarget>()?.ConfigureSpin(
                config.TargetSpinStrength, config.TargetSpinDamping, config.TargetSpinMaximumSpeed);
            target.GetComponent<LabHangingTarget>()?.ConfigureRopeMotion(
                config.GourdRopeCycleAmplitude, config.GourdRopeCyclePeriod,
                config.GourdRopeHitBounce, config.GourdRopeSpring,
                config.GourdRopeDamping, config.GourdRopeMaximumHitExtension);
            target.GetComponent<LabHangingTarget>()?.ConfigureSwing(
                config.GourdSwingImpactStrength, config.GourdSwingMaximumAngle,
                config.GourdSwingReturnStrength, config.GourdSwingDamping);
        }

        public void Host() => HostInternal(null);

        internal bool HostRegisteredRoom(LabBonjourDiscovery registeredRoom, ushort roomPort, int capacity)
        {
            if (!CanChooseConnection || registeredRoom == null || registeredRoom.Family != RoomFamily || !registeredRoom.IsAdvertising
                || !registeredRoom.AdvertisingConfirmed || !ValidRoomName(registeredRoom.RegisteredServiceName)
                || roomPort == 0 || capacity < 2 || capacity > 5) return false;
            port = roomPort;
            targetParticipantCount = capacity;
            return HostInternal(registeredRoom);
        }

        private bool HostInternal(LabBonjourDiscovery registeredRoom)
        {
            if (!CanChooseConnection || manager == null || transport == null || config == null) return false;
            resumeRoomSearchAfterStop = false;
            ClearRoomSearchMessage();
            DisposeDiscovery();
            ResetLocal();
            playerSlots = new LabPlayerSlots(targetParticipantCount);
            configRevision = 1;
            configFingerprint = LabSessionSettings.Fingerprint(config.CaptureDeveloperValues(), targetParticipantCount,
                LabProtocolVersion);
            ConfigureManager();
            try
            {
                if (transport is LabDualStackTransport dual) dual.ConfigureHost(port);
                else transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            }
            catch (Exception e)
            {
                SetRoomSearchMessage("Host endpoint failed: " + e.Message, true);
                return false;
            }
            if (!manager.StartHost())
            {
                string code = transport is LabDualStackTransport failed ? failed.LastFailureCode : string.Empty;
                SetRoomSearchMessage("Host failed: "
                    + (string.IsNullOrEmpty(code) ? "check port and network" : code) + ".", true);
                return false;
            }
            RegisterHandler();
            if (!order.Contains(manager.LocalClientId)) order.Add(manager.LocalClientId);
            AssignAutomaticTestSlot(manager.LocalClientId);
            configAcknowledgements.Add(manager.LocalClientId);
            StartAdvertising(registeredRoom);
            SetStatus("HOST listening on port " + port + "; waiting for " + (targetParticipantCount - 1) + " peers.");
            Debug.Log("C6_LAB_HOST port=" + port + " protocol=" + LabProtocolVersion);
            BroadcastLobby();
            return true;
        }

        public void Join(string address)
        {
            if (!CanChooseConnection || manager == null || transport == null || config == null) return;
            if (string.IsNullOrWhiteSpace(address))
            {
                SetRoomSearchMessage("Enter the Host IP address.", true);
                return;
            }
            resumeRoomSearchAfterStop = false;
            ClearRoomSearchMessage();
            DisposeDiscovery(true);
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            hostAddress = address.Trim();
            var addresses = new List<string>();
            try
            {
                if (IPAddress.TryParse(hostAddress, out var literal)) addresses.Add(literal.ToString());
                else addresses.AddRange(Dns.GetHostAddresses(hostAddress).Select(ip => ip.ToString()));
            }
            catch (Exception e)
            {
                SetRoomSearchMessage("Host address lookup failed: " + e.GetType().Name, true);
                StartRoomSearchInternal(true);
                return;
            }
            BeginJoinCandidates(addresses);
        }

        /// <summary>Browse without joining. Refresh preserves cached rows briefly, never stale routes.</summary>
        public bool StartRoomSearch() => StartRoomSearchInternal(false);

        private bool StartRoomSearchInternal(bool preserveMessage)
        {
            if (!CanBeginConnection || manager == null || transport == null || config == null) return false;
            // A UI-initiated browse consumes a pending one-shot resume as well.
            resumeRoomSearchAfterStop = false;
            if (!preserveMessage) ClearRoomSearchMessage();
            DisposeDiscovery(true);
            roomSearchStartedAt = Time.realtimeSinceStartupAsDouble;
            roomCacheGraceActive = discoveredRooms.Count > 0;
            if (!LabBonjourDiscovery.IsSupported)
            {
                ReportRoomSearchFailure("Room discovery is available on macOS and iOS. Use direct IP here.", preserveMessage);
                return false;
            }
            discovery = new LabBonjourDiscovery(RoomFamily);
            discovery.Diagnostic += message => Debug.Log("C6_LAB_DISCOVERY " + message);
            discovery.Changed += OnDiscoveryChanged;
            roomSearchActive = true;
            if (!discovery.StartBrowse())
            {
                string error = discovery.LastError ?? "Room search could not start. Use direct IP.";
                DisposeDiscovery(true);
                ReportRoomSearchFailure(error, preserveMessage);
                return false;
            }
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            if (!preserveMessage) SetStatus("Searching local rooms; keep the Host app open...");
            else Changed?.Invoke();
            return true;
        }

        public void CancelRoomSearch()
        {
            if (!roomSearchActive || IsJoiningRoom) return;
            DisposeDiscovery();
            SetRoomSearchMessage("Room search canceled. Create a room or refresh to search again.", false);
        }

        public bool CanJoinDiscoveredRoom(string roomKey)
        {
            return IsSearchingRooms && CanBeginConnection && !string.IsNullOrEmpty(roomKey)
                && discoveredRooms.Any(item => string.Equals(item.Key, roomKey, StringComparison.OrdinalIgnoreCase) && !item.Playing && !item.IsFull && !item.WaitingForHost)
                && LabRoomCatalog.RoutesForRoom(discovery.Targets, roomKey).Length > 0;
        }

        /// <summary>Use only fresh routes for the selected room; cached rows are never connection targets.</summary>
        public bool JoinDiscoveredRoom(string roomKey)
        {
            if (!IsSearchingRooms || !CanBeginConnection) return false;
            LabDiscoveredRoom room = discoveredRooms.FirstOrDefault(item =>
                string.Equals(item.Key, roomKey, StringComparison.OrdinalIgnoreCase));
            LabBonjourTarget[] targets = LabRoomCatalog.RoutesForRoom(discovery.Targets, roomKey);
            if (room == null || room.Playing || room.IsFull || room.WaitingForHost || targets.Length == 0)
            {
                RefreshDiscoveredRooms();
                SetRoomSearchMessage("That room is no longer available. Choose another room or refresh.", true);
                return false;
            }
            ClearRoomSearchMessage();
            port = room.Port;
            // Keep browsing and showing the current list while one selected room connects.
            discoveryJoinActive = true;
            discoveryRoomKey = room.Key;
            BeginJoinCandidates(targets.Select(item => item.Address));
            return IsJoiningRoom;
        }

        /// <summary>Cancel only the current join. Re-open browsing once NGO has fully shut down.</summary>
        public bool CancelJoin()
        {
            if (!IsJoiningRoom || joinFinishPending) return false;
            FinishJoin("Connection canceled. Choose a room to try again.", false, true);
            return true;
        }

        private void BeginJoinCandidates(IEnumerable<string> addresses)
        {
            ResetLocal();
            joinCandidates.Clear();
            joinCandidates.AddRange(LabConnectionCandidates.Initial(addresses, port));
            joinCandidateIndex = 0;
            joinStartedAt = Time.realtimeSinceStartupAsDouble;
            joinInProgress = true;
            joinFinishPending = false;
            joinCanceling = false;
            if (joinCandidates.Count == 0)
            {
                FinishJoin("No usable numeric IPv4/IPv6 address. Use direct IP.", true);
                return;
            }
            StartJoinAttempt();
        }

        private void StartJoinAttempt()
        {
            if (!joinInProgress || joinFinishPending || joinCandidateIndex >= joinCandidates.Count
                || manager == null || transport == null) return;
            string address = joinCandidates[joinCandidateIndex];
            ConfigureManager();
            try
            {
                if (transport is LabDualStackTransport dual) dual.ConfigureClient(address, port);
                else transport.SetConnectionData(address, port);
                if (!manager.StartClient()) throw new InvalidOperationException("Client start failed.");
            }
            catch (Exception e)
            {
                string code = transport is LabDualStackTransport failed ? failed.LastFailureCode : string.Empty;
                SetStatus("Cannot connect via " + address + ": "
                    + (string.IsNullOrEmpty(code) ? e.GetType().Name : code));
                AdvanceJoinCandidate();
                return;
            }
            RegisterHandler();
            joinAttemptAt = Time.realtimeSinceStartupAsDouble;
            SetStatus("Connecting to " + address + ":" + port + " (" + (joinCandidateIndex + 1)
                + "/" + joinCandidates.Count + ")...");
            Debug.Log("C6_LAB_JOIN address=" + address + " port=" + port);
        }

        private void AdvanceJoinCandidate()
        {
            if (!joinInProgress || joinRetryPending || joinFinishPending) return;
            string rejection = manager == null ? string.Empty : manager.DisconnectReason;
            if (IsTerminalJoinRejection(rejection))
            {
                FinishJoin("Join rejected: " + rejection, true);
                return;
            }
            if (++joinCandidateIndex >= joinCandidates.Count
                || Time.realtimeSinceStartupAsDouble - joinStartedAt > JoinBudgetSeconds)
            {
                if (discoveryJoinActive && Time.realtimeSinceStartupAsDouble - joinStartedAt <= JoinBudgetSeconds)
                {
                    joinRetryPending = true;
                    UnregisterHandler();
                    RequestJoinShutdown();
                    SetStatus("Waiting for another local route to the discovered Host...");
                    return;
                }
                FinishJoin("Connection failed on all local routes. Check Host, port, and network.", true);
                return;
            }
            joinRetryPending = true;
            UnregisterHandler();
            RequestJoinShutdown();
        }

        /// <summary>
        /// NGO returns a server-supplied reason verbatim. Its generated transport diagnostic uses
        /// this prefix instead; such a timeout/disconnect does not reject the other IP routes.
        /// Unknown explicit Host reasons remain terminal rather than being retried blindly.
        /// </summary>
        public static bool IsTerminalJoinRejection(string reason) => !string.IsNullOrEmpty(reason)
            && !reason.StartsWith("[Disconnect Event]", StringComparison.Ordinal);

        private void FinishJoin(string message, bool failed, bool canceled = false)
        {
            // Keep the operation locked through shutdown. Late connect/disconnect callbacks cannot
            // turn a canceled connection into a room or advance it to a second address.
            joinFinishPending = true;
            joinCanceling = canceled;
            joinRetryPending = false;
            discoveryJoinActive = false;
            joinAttemptAt = 0d;
            SetRoomSearchMessage(message, failed);
            UnregisterHandler();
            RequestJoinShutdown();
        }

        private void RequestJoinShutdown()
        {
            if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
        }

        private void CompleteJoinShutdown()
        {
            RequestJoinShutdown();
            if (manager != null && (manager.IsListening || manager.ShutdownInProgress)) return;
            joinInProgress = false;
            joinRetryPending = false;
            joinFinishPending = false;
            joinCanceling = false;
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            stopAfterDisconnect = false;
            ResetLocal();
            // Exactly one automatic resume for this failure/cancel. A Bonjour failure stays stopped.
            StartRoomSearchInternal(true);
        }

        public void Stop()
        {
            if (stopping) return;
            stopping = true;
            joinFinishPending = false;
            joinCanceling = false;
            joinInProgress = false;
            joinRetryPending = false;
            discoveryJoinActive = false;
            discoveryRoomKey = null;
            DisposeDiscovery();
            UnregisterHandler();
            if (manager != null && manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            ResetLocal();
            RestoreSourceConfig();
            SetStatus("Disconnected. Choose HOST or JOIN.");
            ClearRoomSearchMessage();
            resumeRoomSearchAfterStop = true;
            stopping = false;
        }

        public void StartBattle()
        {
            if (gameFlowEnabled && (!gameStageStarting || !HasBattleReferences || model != null || latest != null)) return;
            if (!CanStartBattle)
            {
                SetNotice("Wait for " + targetParticipantCount + " selected P numbers and matching settings confirmations.");
                return;
            }
            pendingDeveloperRestart = false;
            ResetActionTransport();
            pauseRequestIds.Clear();
            pauseRequestPending = false;
            battleClock.Reset();
            ApplyLocalPause(false);
            ClearProjectiles();
            sessionId = Guid.NewGuid().ToString("N");
            impactFeedback?.Clear();
            ConfigureTargetSpin();
            target?.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = new LabModel();
            ulong[] playersInPOrder = playerSlots.PlayersInSlotOrder();
            double beginAt = Time.realtimeSinceStartupAsDouble;
            model.Begin(sessionId, playersInPOrder, beginAt, config,
                stageYokai: gameFlowEnabled ? gameStageYokai : LabStageYokai.None);
            if (GameStartHeld) battleClock.Pause(beginAt);
            SampleHostPaintFlow(battleClock.Now(Time.realtimeSinceStartupAsDouble));
            impactReceipts.SetActive(sessionId, model.Round);
            SetNotice(string.Empty);
            PublishSnapshot(true);
            SetStatus("PLAYING: " + order.Count + " / " + targetParticipantCount);
            Debug.Log("C6_LAB_START session=" + sessionId + " seats=" + string.Join(",", playersInPOrder));
        }

        /// <summary>Start a one-player Host session only in the Editor or a Development Build.</summary>
        public bool StartDeveloperSolo()
        {
            if (!DeveloperBuildAvailable)
            {
                SetNotice("Developer Solo requires a Development Build.");
                return false;
            }
            if (IsConnected)
            {
                SetNotice("Leave the current session before starting Developer Solo.");
                return false;
            }
            Host();
            if (!IsHost || order.Count != 1) return false;
            developerSolo = true;
            DisposeDiscovery();
            BeginDeveloperSoloRound();
            return true;
        }

        /// <summary>Apply new runtime tuning by starting a fresh one-player round.</summary>
        public bool RestartDeveloperSolo()
        {
            if (!DeveloperBuildAvailable || !developerSolo || !IsHost || order.Count != 1 || config == null)
            {
                SetNotice("Start Developer Solo before applying a new round.");
                return false;
            }
            if (gameFlowEnabled)
            {
                if (!ValidateGameDeveloperConfig(config, out string validationError))
                {
                    config = sessionConfig != null ? sessionConfig : sourceConfig;
                    SetNotice(validationError);
                    return false;
                }
                // LabDeveloperMode installs its Scene-owned runtime object directly. Own a
                // validated copy before that Scene unloads and destroys its DEV panel copy.
                if (!TryInstallSessionConfig(config, out string error))
                {
                    SetNotice(error);
                    return false;
                }
                RequestGameSoloRestart();
                return true;
            }
            ClearProjectiles();
            battleClock.Reset();
            ApplyLocalPause(false);
            if (board != null) board.Clear();
            visibleOrbIds.Clear();
            latest = null;
            BeginDeveloperSoloRound();
            return true;
        }

        private void BeginDeveloperSoloRound()
        {
            // DEV Solo installs its runtime copy directly; notify the same presentation
            // subscribers that receive accepted Host room settings before the next round.
            ConfigApplied?.Invoke(config);
            LastShotReport = string.Empty;
            battleClock.Reset();
            ApplyLocalPause(false);
            sessionId = Guid.NewGuid().ToString("N");
            impactFeedback?.Clear();
            ConfigureTargetSpin();
            target?.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = new LabModel();
            double beginAt = Time.realtimeSinceStartupAsDouble;
            model.BeginSolo(sessionId, LocalPlayerId, beginAt, config,
                stageYokai: gameFlowEnabled ? gameStageYokai : LabStageYokai.None);
            if (GameStartHeld) battleClock.Pause(beginAt);
            SampleHostPaintFlow(battleClock.Now(Time.realtimeSinceStartupAsDouble));
            impactReceipts.SetActive(sessionId, model.Round);
            SetNotice(string.Empty);
            PublishSnapshot(true);
            SetStatus("DEVELOPER SOLO: 1 / 1");
            Debug.Log("C6_LAB_SOLO_START session=" + sessionId + " player=" + LocalPlayerId);
        }

        /// <summary>Generate from the first drum on this player's board.</summary>
        public void Generate()
        {
            LabOrbState drum = LocalDrum();
            if (drum != null) Generate(drum.id);
        }

        /// <summary>Ask the Host to generate from this drum at the position this screen shows now.</summary>
        public void Generate(string drumId)
        {
            if (!CanProcessBattleActions || !IsConnected || latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            LabOrbState drum = Array.Find(latest.drums ?? Array.Empty<LabOrbState>(),
                candidate => candidate.id == drumId && candidate.owner == LocalPlayerId);
            if (drum == null) return;
            Vector2 position = board != null && board.TryGetMotion(drum.id, out var local, out _)
                ? local : new Vector2(drum.x, drum.y);
            // The drum travels in secondId: a refused request restores its firstId orb, and a
            // refused generate (no stamina, full board) must never stop the drum.
            Request(new LabNetworkPacket { type = "generate", requestId = ++requestSequence, secondId = drum.id,
                x = Mathf.Clamp01(position.x), y = Mathf.Clamp01(position.y) });
        }

        private LabOrbState LocalDrum() => latest == null ? null
            : Array.Find(latest.drums ?? Array.Empty<LabOrbState>(), drum => drum.owner == LocalPlayerId);

        /// <summary>Any current participant may ask the Host to pause this shared round.</summary>
        public bool PauseBattle()
        {
            if (!CanPauseBattle) return false;
            if (IsHost) return PauseAsHost();
            bool sent = Send(NetworkManager.ServerClientId, new LabNetworkPacket
            {
                type = "pause", sessionId = sessionId, round = latest.round, requestId = ++requestSequence
            });
            if (sent)
            {
                pauseRequestPending = true;
                pauseRequestSentAt = Time.realtimeSinceStartupAsDouble;
            }
            return sent;
        }

        private bool PauseAsHost()
        {
            if (!IsHost || model == null || model.Phase != LabPhase.Playing || battleClock.IsPaused)
                return false;
            double realTime = Time.realtimeSinceStartupAsDouble;
            model.Tick(battleClock.Now(realTime));
            if (model.Phase != LabPhase.Playing)
            {
                PublishSnapshot(true);
                return false;
            }
            if (!battleClock.Pause(realTime)) return false;
            PublishSnapshot(true);
            Debug.Log("C6_LAB_PAUSED session=" + sessionId);
            return true;
        }

        /// <summary>Only the Host resumes; the same battle time and physical motion continue.</summary>
        public bool ResumeBattle()
        {
            if (!CanResumeBattle) return false;
            if (!battleClock.Resume(Time.realtimeSinceStartupAsDouble)) return false;
            PublishSnapshot(true);
            Debug.Log("C6_LAB_RESUMED session=" + sessionId);
            return true;
        }

        /// <summary>Keep the transport, participants, advertised room and approved settings.</summary>
        public bool ReturnAllToLobby()
        {
            if (!CanReturnAllToLobby) return false;
            EndBattleToLobby();
            BroadcastLobby();
            SetNotice("All players returned to the same room lobby.");
            Debug.Log("C6_LAB_RETURN_TO_LOBBY participants=" + order.Count);
            return true;
        }

        /// <summary>Validate Host DEV settings, then start a fresh shared round after every ACK.</summary>
        public bool RestartBattleWithDeveloperConfig(LabConfig candidate)
        {
            if (!CanRestartSharedBattle)
            {
                SetNotice("Pause a Host battle before restarting with DEV settings.");
                return false;
            }
            if (!ValidateGameDeveloperConfig(candidate, out string validationError))
            {
                SetNotice(validationError);
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            if (gameFlowEnabled)
            {
                EndBattleToLobby();
                PublishHostConfigRevision();
                GameFlowDeveloperRestartRequested?.Invoke();
                return true;
            }
            EndBattleToLobby();
            pendingDeveloperRestart = true;
            PublishHostConfigRevision();
            SetNotice("New settings sent; restarting after all players confirm.");
            TryStartPendingDeveloperRestart();
            return true;
        }

        private void TryStartPendingDeveloperRestart()
        {
            if (pendingDeveloperRestart && CanStartBattle) StartBattle();
        }

        private void EndBattleToLobby()
        {
            ResetGameStartHold();
            pendingDeveloperRestart = false;
            ResetActionTransport();
            pauseRequestIds.Clear();
            pauseRequestPending = false;
            ClearProjectiles();
            board?.Clear();
            visibleOrbIds.Clear();
            impactReceipts.Reset();
            impactFeedback?.Clear();
            // Unity's destroyed components compare equal to null even though C# ?. still
            // invokes GetComponent on the stale managed wrapper during scene teardown.
            if (target != null) target.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = null;
            latest = null;
            sessionId = null;
            ResetPaintTransport();
            ResetActionTransport();
            battleClock.Reset();
            ApplyLocalPause(false);
            LastShotReport = string.Empty;
        }

        private void ApplyLocalPause(bool pause)
        {
            if (locallyPaused == pause) return;
            if (pause)
            {
                timeScaleBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                locallyPaused = true;
            }
            else
            {
                Time.timeScale = timeScaleBeforePause;
                locallyPaused = false;
            }
        }

        private void Update()
        {
            if (stopAfterDisconnect)
            {
                stopAfterDisconnect = false;
                string explanation = Status;
                Stop();
                SetRoomSearchMessage(explanation, true);
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (resumeRoomSearchAfterStop && CanBeginConnection)
            {
                resumeRoomSearchAfterStop = false;
                if (!IsSearchingRooms) StartRoomSearchInternal(true);
            }
            if (joinFinishPending)
            {
                CompleteJoinShutdown();
                return;
            }
            if (pauseRequestPending && now - pauseRequestSentAt >= 5d)
                pauseRequestPending = false;
            if (slotSelectionPending && now - slotRequestSentAt >= 5d)
            {
                ClearPendingSlotSelection();
                SetNotice("P number selection timed out. Try again.");
            }
            foreach (ulong stale in pendingApprovals.Where(item => now - item.Value > 15d)
                         .Select(item => item.Key).ToArray()) pendingApprovals.Remove(stale);
            LabBonjourDiscovery activeDiscovery = discovery;
            if (activeDiscovery != null)
            {
                // A resumed browse may have been created later in this Update.
                // Sample native discovery time now so its monotonic clock never moves backwards.
                if (IsHost && !developerSolo)
                {
                    // Retry the actual listener families too if activation's native TXT update failed.
                    bool ipv4 = !(transport is LabDualStackTransport dual) || dual.HasIpv4Listener;
                    bool ipv6 = transport is LabDualStackTransport second && second.HasIpv6Listener;
                    if (ipv4 || ipv6)
                        activeDiscovery.UpdatePresence(ipv4, ipv6, order.Count, targetParticipantCount, RoomPresenceBusy);
                }
                activeDiscovery.Tick();
                if (discovery == activeDiscovery && roomSearchActive && !activeDiscovery.IsBrowsing)
                {
                    string error = activeDiscovery.LastError ?? "Room search ended. Use direct IP or search again.";
                    DisposeDiscovery(true);
                    roomSearchStartedAt = now;
                    roomCacheGraceActive = discoveredRooms.Count > 0;
                    ReportRoomSearchFailure(error, true);
                }
                if (discovery == activeDiscovery && discoveryJoinActive)
                {
                    foreach (LabBonjourTarget target in activeDiscovery.Targets)
                    {
                        if (!string.Equals(LabRoomCatalog.Key(target.ServiceName, target.Port),
                                discoveryRoomKey, StringComparison.OrdinalIgnoreCase)) continue;
                        if (LabConnectionCandidates.AppendDiscovered(joinCandidates, target.Address, port))
                            Debug.Log("C6_LAB_DISCOVERY_ROUTE_ADDED family=" + LabBonjourAddressCodec.Family(target.Address));
                    }
                }
            }
            if (roomCacheGraceActive && !IsJoiningRoom && now - roomSearchStartedAt >= 5d)
            {
                roomCacheGraceActive = false;
                RefreshDiscoveredRooms();
                Changed?.Invoke();
            }
            if (joinRetryPending && manager != null && !manager.IsListening && !manager.ShutdownInProgress)
            {
                if (joinCandidateIndex < joinCandidates.Count)
                {
                    joinRetryPending = false;
                    StartJoinAttempt();
                }
                else if (now - joinStartedAt > JoinBudgetSeconds)
                {
                    FinishJoin("No reachable route found. Use direct IP or check the Host network.", true);
                }
            }
            if (joinInProgress && !joinRetryPending && !joinFinishPending && joinAttemptAt > 0
                && now - joinAttemptAt >= JoinAttemptSeconds) AdvanceJoinCandidate();
            ApplyGameStartSimulationHold();
            if (IsHost && model != null && !gameBattleFrozen)
            {
                SampleHostPaintFlow(battleClock.Now(now));
                if (now >= nextSnapshotAt) PublishSnapshot(false);
            }
            SyncJigwiFireSurfaces();
            if (!gameBattleFrozen)
            {
                FlushPendingBlastReports(now);
                FlushBoardActions(now);
            }
            FlushBulkPackets();
            if (!IsHost && (pendingPaintSnapshot != null || PaintReceiver.HasPending)
                && now >= nextPaintRequestAt) RequestPaintCheckpoint();
            // Command-line smoke mode is intentionally separate from ordinary UI play.
            if (CanProcessBattleActions && IsConnected && latest != null && latest.phase == LabPhase.Playing && !latest.paused)
            {
                if (autoGenerate && autoGeneratedSession != latest.sessionId)
                {
                    autoGeneratedSession = latest.sessionId;
                    Generate();
                }
                if (IsHost && autoTransfer && autoTransferredSession != latest.sessionId)
                {
                    LabOrbState candidate = null;
                    foreach (var orb in latest.orbs ?? Array.Empty<LabOrbState>())
                        if (orb.owner == LocalPlayerId && !orb.inFlight) { candidate = orb; break; }
                    if (candidate != null)
                    {
                        autoTransferredSession = latest.sessionId;
                        Request(new LabNetworkPacket { type = "transfer", firstId = candidate.id, requestId = ++requestSequence,
                            right = true, x = 1f, y = .5f, vx = 3f, vy = 0f });
                        Debug.Log("C6_LAB_SMOKE_EDGE orb=" + candidate.id);
                    }
                }
            }
        }

        private void ConfigureManager()
        {
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ProtocolVersion = LabProtocolVersion,
                ConnectionData = (byte[])ActiveConnectionToken.Clone(),
                EnableSceneManagement = false,
                ConnectionApproval = true,
                PlayerPrefab = null,
                ClientConnectionBufferTimeout = 10
            };
            manager.ConnectionApprovalCallback = Approve;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool alreadyPending = pendingApprovals.ContainsKey(request.ClientNetworkId);
            string rejection = !manager.IsHost ? "The room is no longer hosted."
                : request.Payload == null || !request.Payload.SequenceEqual(ActiveConnectionToken)
                    ? "Lab version mismatch. Use the same game version."
                    : model != null || (gameFlowEnabled && gameRosterLocked)
                        ? "Battle already started. Wait for the Host to return to the lobby."
                        : order.Count + pendingApprovals.Count - (alreadyPending ? 1 : 0) >= targetParticipantCount
                            ? "Room is full. Choose another room." : string.Empty;
            bool allowed = string.IsNullOrEmpty(rejection);
            if (allowed) pendingApprovals[request.ClientNetworkId] = Time.realtimeSinceStartupAsDouble;
            response.Approved = allowed;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = rejection;
        }

        private void RegisterHandler()
        {
            if (handlerRegistered || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, Receive);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(BulkMessageName, ReceiveBulk);
            handlerRegistered = true;
        }

        private void UnregisterHandler()
        {
            if (handlerRegistered && manager != null && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(BulkMessageName);
            }
            handlerRegistered = false;
        }

        private void OnConnected(ulong id)
        {
            if (stopping || manager == null) return;
            if (joinFinishPending || (!manager.IsHost && (!joinInProgress || manager.ShutdownInProgress)))
            {
                RequestJoinShutdown();
                return;
            }
            RegisterHandler();
            if (manager.IsHost)
            {
                pendingApprovals.Remove(id);
                if (!order.Contains(id) && order.Count < targetParticipantCount) order.Add(id);
                else if (!order.Contains(id))
                {
                    manager.DisconnectClient(id);
                    return;
                }
                AssignAutomaticTestSlot(id);
                Debug.Log("C6_LAB_PEER_JOIN id=" + id + " participants=" + order.Count);
                BroadcastLobby();
                if (id != LocalPlayerId) SendSessionConfig(id);
                if (autoStart && CanStartBattle) StartBattle();
            }
            else if (id == manager.LocalClientId)
            {
                joinInProgress = false;
                joinRetryPending = false;
                clientSessionConnected = true;
                discoveryJoinActive = false;
                DisposeDiscovery();
                ClearRoomSearchMessage();
                SetStatus("Connected; waiting for Host settings.");
                Debug.Log("C6_LAB_CONNECTED local=" + id);
            }
        }

        private void OnDisconnected(ulong id)
        {
            if (stopping || joinFinishPending) return;
            pendingApprovals.Remove(id);
            if (manager != null && manager.IsHost)
            {
                if (gameFlowEnabled && order.Contains(id))
                {
                    GameFlowDisconnected?.Invoke(id, false);
                    // GameSession owns the GameOver transition; never clear the result or
                    // publish a legacy lobby that would erase it on the remaining peers.
                    if (stopping || manager == null || !manager.IsHost) return;
                    order.Remove(id);
                    bulkSenders.Remove(id); peerPaintPublishers.Remove(id); peerSharedRounds.Remove(id);
                    boardSequences.Remove(id);
                    playerSlots?.Release(id);
                    slotRequestIds.Remove(id);
                    configAcknowledgements.Remove(id);
                    pendingDeveloperRestart = false;
                    if (model == null && !gameBattleFrozen) BroadcastLobby();
                    return;
                }
                if (!order.Remove(id))
                {
                    Debug.Log("C6_LAB_JOIN_REJECTED id=" + id);
                    return;
                }
                bulkSenders.Remove(id); peerPaintPublishers.Remove(id); peerSharedRounds.Remove(id);
                boardSequences.Remove(id);
                playerSlots?.Release(id);
                slotRequestIds.Remove(id);
                Debug.LogWarning("C6_LAB_PEER_LEFT id=" + id);
                if (model != null) EndBattleToLobby();
                pendingDeveloperRestart = false;
                configAcknowledgements.Remove(id);
                BroadcastLobby();
                SetStatus("A player left. Connect " + targetParticipantCount + " before starting.");
            }
            else
            {
                if (joinInProgress) { AdvanceJoinCandidate(); return; }
                if (!clientSessionConnected) return; // Ignore a retired attempt's late shutdown callback.
                if (gameFlowEnabled)
                {
                    GameFlowDisconnected?.Invoke(id, true);
                    clientSessionConnected = false;
                    // Keep the last snapshot until GameSession captures the terminal screen.
                    // Its Home action performs the eventual transport shutdown.
                    SetStatus("Connection ended. Return home from Game Over.");
                    return;
                }
                clientSessionConnected = false;
                stopAfterDisconnect = true;
                string reason = manager == null ? string.Empty : manager.DisconnectReason;
                SetStatus(string.IsNullOrEmpty(reason) ? "Connection ended. Rejoin the Host." : "Join failed: " + reason);
                Debug.LogWarning("C6_LAB_DISCONNECTED reason=" + reason);
            }
        }

        private void OnTransportFailure()
        {
            if (joinFinishPending) return;
            if (joinInProgress) { AdvanceJoinCandidate(); return; }
            if (gameFlowEnabled && (IsConnected || clientSessionConnected))
                GameFlowDisconnected?.Invoke(manager == null ? ulong.MaxValue : manager.LocalClientId, !IsHost);
            SetRoomSearchMessage("Network transport failed. Check IP, port, and same network.", true);
            Debug.LogWarning("C6_LAB_TRANSPORT_FAILURE");
        }

        private void BroadcastLobby()
        {
            if (!IsHost) return;
            var packet = new LabNetworkPacket { type = "lobby", seats = order.ToArray(),
                slotAssignments = playerSlots?.Snapshot() ?? Array.Empty<LabPlayerSlotAssignment>(),
                targetParticipants = targetParticipantCount, configRevision = configRevision,
                configFingerprint = configFingerprint, snapshotSequence = snapshotSequence,
                lobbyRevision = ++lobbyRevision, roomName = connectedRoomName,
                restarting = pendingDeveloperRestart };
            foreach (ulong id in order) if (id != manager.LocalClientId) Send(id, packet);
            SetStatus("LOBBY: " + order.Count + " / " + targetParticipantCount
                + " · P " + (playerSlots?.SelectedCount ?? 0) + "/" + targetParticipantCount
                + " · settings " + configAcknowledgements.Count + "/" + order.Count);
            Changed?.Invoke();
        }

        private void HandleSlotSelectionRequest(ulong sender, LabNetworkPacket packet)
        {
            bool accepted = false;
            bool changed = false;
            string reason;
            if (!CanSelectPlayerSlot || !order.Contains(sender))
                reason = "SLOT_SELECTION_CLOSED";
            else if (packet.requestId <= 0)
                reason = "INVALID_SLOT_REQUEST";
            else if (slotRequestIds.TryGetValue(sender, out long previous) && packet.requestId <= previous)
            {
                accepted = packet.requestId == previous && playerSlots != null
                    && playerSlots.GetSlot(sender) == packet.slotNumber;
                reason = accepted ? "SLOT_UNCHANGED" : "STALE_SLOT_REQUEST";
            }
            else
            {
                slotRequestIds[sender] = packet.requestId;
                int previousSlot = playerSlots.GetSlot(sender);
                accepted = playerSlots.TrySelect(sender, packet.slotNumber, out reason);
                changed = accepted && previousSlot != packet.slotNumber;
            }
            Send(sender, new LabNetworkPacket { type = "slot_result", requestId = packet.requestId,
                slotNumber = packet.slotNumber, accepted = accepted, reason = reason,
                lobbyRevision = accepted ? lobbyRevision + 1 : 0 });
            if (!accepted) return;
            // Also republish an unchanged accepted selection. A retried request must be
            // able to recover both its approval and the matching authoritative mapping.
            BroadcastLobby();
            if (!changed) return;
            Debug.Log("C6_LAB_SLOT player=" + sender + " p=" + packet.slotNumber + " result=" + reason);
            if (autoStart && CanStartBattle) StartBattle();
            else TryStartPendingDeveloperRestart();
        }

        private void SendSessionConfig(ulong id)
        {
            if (!IsHost || config == null || id == LocalPlayerId) return;
            Send(id, new LabNetworkPacket { type = "config", targetParticipants = targetParticipantCount,
                configRevision = configRevision, configFingerprint = configFingerprint,
                tuning = config.CaptureDeveloperValues() });
        }

        /// <summary>UT용 기믹 기록 파일 이름 (#311). 기기의 Application.persistentDataPath 아래에 판마다 한 줄씩 붙는다.</summary>
        public const string GimmickStatsFileName = "gimmick-stats.jsonl";
        private string gimmickStatsPath;
        private string gimmickStatsReportedRound;

        /// <summary>
        /// 기믹 기록 (#311, UT용): <see cref="LabConfig.RecordGimmickStats"/>가 켜져 있으면 판이 끝난 뒤 Host가 처음 내보내는
        /// 스냅샷에서 그 판의 기록을 한 번 남긴다. 기본은 꺼져 있어 아무것도 쓰지 않는다. 파일을 못 써도 게임은 그대로 간다.
        /// 끝까지 가지 않고 다시 시작하거나 나간 판은 남기지 않는다.
        /// </summary>
        private void ReportGimmickStats()
        {
            if (config == null || !config.RecordGimmickStats || latest == null
                || latest.phase == LabPhase.Playing || latest.phase == LabPhase.Idle) return;
            string roundKey = latest.sessionId + "/" + latest.round;
            if (gimmickStatsReportedRound == roundKey) return;
            gimmickStatsReportedRound = roundKey;
            string line = JsonUtility.ToJson(model.GimmickStats());
            Debug.Log("C6_LAB_GIMMICK_STATS " + line);
            try
            {
                string path = gimmickStatsPath ?? System.IO.Path.Combine(Application.persistentDataPath, GimmickStatsFileName);
                System.IO.File.AppendAllText(path, line + "\n");
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("C6_LAB_GIMMICK_STATS_NOT_SAVED " + exception.Message);
            }
        }

        private void PublishSnapshot(bool immediate)
        {
            if (!IsHost || model == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            latest = model.Snapshot(battleClock.Now(now));
            EnsureActionRound();
            LabHangingTarget hanging = target == null ? null : target.GetComponent<LabHangingTarget>();
            if (hanging != null)
            {
                latest.targetYawDegrees = hanging.YawAngleDegrees;
                latest.targetYawSpeedDegreesPerSecond = hanging.YawSpeedDegreesPerSecond;
                latest.targetRopeCycleElapsed = hanging.CycleElapsed;
                latest.targetRopeHitOffset = hanging.VerticalOffset;
                latest.targetRopeHitVelocity = hanging.VerticalVelocity;
            }
            latest.paused = battleClock.IsPaused && !GameStartHeld && latest.phase == LabPhase.Playing;
            ReportGimmickStats();
            AnnounceBombExplosions();
            AnnounceOrbExpiries();
            AnnounceGourdBombs();
            PublishOwnerBoards(latest);
            // Large experimental states are deliberately updated less often instead of queuing
            // many reliable fragments per peer until the network becomes unusable.
            nextSnapshotAt = now + ((latest.orbs?.Length ?? 0) > 100 ? .5d : .1d);
            // Commit the Host's view and counts from the very same authoritative map version.
            CopyPaintToDisplay(model.PaintSurface, latest);
            if (target != null) target.GetComponent<LabGourdPaint>()?.RefreshCommittedMap();
            ApplyLocalPause(latest.paused);
            SyncBoard();
            long sequence = ++snapshotSequence;
            foreach (ulong id in order)
            {
                if (id == manager.LocalClientId || HasPendingBulk(id)) continue;
                // A slow peer coalesces to the newest shared state, without holding up board actions or other peers.
                var shared = LabBoardReplica.CopySnapshot(latest);
                string roundKey = latest.sessionId + "/" + latest.round;
                if (peerSharedRounds.TryGetValue(id, out string sentRound) && sentRound == roundKey)
                {
                    shared.orbs = Array.Empty<LabOrbState>();
                    shared.drums = Array.Empty<LabOrbState>();
                }
                string[] pieces;
                try { pieces = LabSessionSettings.ChunkSnapshot(shared); }
                catch (ArgumentException exception) { AbortOversizedSnapshot(exception.Message); return; }
                LabPaintFrame[] paintFrames = PeerPaintPublisher(id).Publish(model.PaintSurface, sessionId, latest.round, sequence);
                if (!SendPaintFrames(id, paintFrames))
                { AbortOversizedSnapshot("Could not queue the complete paint transaction."); return; }
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (!QueueBulk(id, new LabNetworkPacket { type = "snapshot_chunk", sessionId = sessionId,
                        round = latest.round, configRevision = configRevision, configFingerprint = configFingerprint,
                        snapshotSequence = sequence, chunkIndex = i, chunkCount = pieces.Length, chunkData = pieces[i] }))
                    { AbortOversizedSnapshot("Could not queue the complete battle state."); return; }
                }
                peerSharedRounds[id] = roundKey;
            }
            if (immediate) Debug.Log("C6_LAB_STATE phase=" + latest.phase + " hp=" + latest.hp + " remaining=" + latest.remaining.ToString("F1"));
            Changed?.Invoke();
        }

        private readonly List<string> blastFlung = new List<string>();

        private void AnnounceOrbExpiries()
        {
            foreach (LabOrbExpiry expiry in model.DrainOrbExpiries())
            {
                // Ordered before receipts/owner-board frames remove the body. Bulk snapshots use a separate pipeline;
                // the owner ledger prevents them from resurrecting a consumed/expired orb.
                if (expiry.Owner == LocalPlayerId) OnOrbExpired(expiry.OrbId);
                else Send(expiry.Owner, new LabNetworkPacket { type = "orbExpired", sessionId = sessionId, round = model.Round,
                    firstId = expiry.OrbId });
            }
        }

        private void OnOrbExpired(string orbId)
        {
            if (board != null) board.Vanish(orbId);
            OrbExpired?.Invoke(orbId);
        }

        private void AnnounceBombExplosions()
        {
            foreach (LabBombBlast blast in model.DrainBombExplosions())
            {
                Debug.Log("C6_LAB_BOMB_EXPLODED owner=" + blast.Owner + " bomb=" + blast.BombId);
                if (blast.Owner == LocalPlayerId) OnBombBlast(blast.BombId, blast.Position);
                // Sent before this snapshot's chunks on the same ordered channel, so the holder still
                // shows the bomb where it blew when the blast arrives.
                else Send(blast.Owner, new LabNetworkPacket { type = "bombBlast", sessionId = sessionId,
                    firstId = blast.BombId, x = blast.Position.x, y = blast.Position.y });
            }
        }

        private void AnnounceGourdBombs()
        {
            foreach (LabGourdBomb drop in model.DrainGourdBombs())
            {
                Debug.Log("C6_LAB_GOURD_BOMB target=" + drop.Target + " bomb=" + drop.BombId);
                // Everyone sees the gourd warn; it reaches each player before the snapshot that
                // first holds the bomb, so the drop can play before the bomb appears.
                foreach (ulong id in order)
                {
                    if (id == manager.LocalClientId) continue;
                    Send(id, new LabNetworkPacket { type = "gourdBomb", sessionId = sessionId, owner = drop.Target,
                        firstId = drop.BombId, x = drop.Position.x, y = drop.Position.y });
                }
                OnGourdBomb(drop.Target, drop.BombId, drop.Position);
            }
            AnnounceSabotages();
        }

        /// <summary>The sabotage mix's casts go out with the gourd's bombs, before the snapshot that shows them (#251).</summary>
        private void AnnounceSabotages()
        {
            foreach (LabFoxFireDrop drop in model.DrainFoxFires())
            {
                Debug.Log("C6_LAB_FOX_FIRE target=" + drop.Target + " orbs=" + drop.OrbIds.Length);
                // Only its holder's board drops the fox orbs in; it reaches them before the snapshot holding them.
                if (drop.Target == LocalPlayerId) OnFoxFire(drop.OrbIds);
                else Send(drop.Target, new LabNetworkPacket { type = "foxFire", sessionId = sessionId, round = model.Round,
                    owner = drop.Target, ids = drop.OrbIds });
            }
            foreach (LabSabotage cast in model.DrainSabotages())
            {
                Debug.Log("C6_LAB_SABOTAGE kind=" + cast.Kind + " target=" + cast.Target);
                foreach (ulong id in order)
                {
                    if (id == manager.LocalClientId) continue;
                    Send(id, new LabNetworkPacket { type = "sabotage", sessionId = sessionId, round = model.Round,
                        owner = cast.Target, element = (int)cast.Kind });
                }
                SabotageCast?.Invoke(cast.Kind, cast.Target);
            }
        }

        private void AnnounceCharmWoken(ulong player)
        {
            Debug.Log("C6_LAB_CHARM_WOKEN player=" + player);
            foreach (ulong id in order)
            {
                if (id == manager.LocalClientId) continue;
                Send(id, new LabNetworkPacket { type = "charmWoken", sessionId = sessionId, round = model.Round, owner = player });
            }
            CharmWoken?.Invoke(player);
        }

        private void OnFoxFire(string[] orbIds)
        {
            SetNotice(FoxFireNotice);
            if (board == null) return;
            foreach (string id in orbIds) board.ExpectDrop(id);
        }

        private void OnGourdBomb(ulong target, string bombId, Vector2 position)
        {
            if (target == LocalPlayerId)
            {
                SetNotice(GourdBombNotice);
                // Arrives before the snapshot holding the bomb, so the board can drop it in from above.
                if (board != null) board.ExpectDrop(bombId);
            }
            GourdBombDropped?.Invoke(target, bombId, position);
        }

        private void OnBombBlast(string bombId, Vector2 position)
        {
            SetNotice(BombBlastNotice);
            if (board != null)
            {
                blastFlung.Clear();
                board.Blast(bombId, position, blastFlung);
                if (blastFlung.Count > 0) ReportBlast(bombId, blastFlung.ToArray());
            }
            BombBlasted?.Invoke(bombId, position);
        }

        /// <summary>
        /// Tells the Host which orbs this board's blast flung, so they burn and shrink (#213). It goes
        /// out before any of them crosses an edge, on the same ordered channel, so the Host still finds
        /// them here. The Host's own blast arrives while it publishes a snapshot: it marks them at once
        /// and the next regular snapshot carries them, rather than publishing again from inside.
        /// </summary>
        private void ReportBlast(string bombId, string[] flung)
        {
            if (!IsHost)
            {
                Request(new LabNetworkPacket { type = "blastHit", firstId = bombId, ids = flung });
                return;
            }
            int burned = model?.MarkBlasted(LocalPlayerId, bombId, flung,
                battleClock.Now(Time.realtimeSinceStartupAsDouble)) ?? 0;
            Debug.Log("C6_LAB_BLAST_BURN owner=" + LocalPlayerId + " bomb=" + bombId + " burned=" + burned);
        }

        private void AbortOversizedSnapshot(string reason)
        {
            Debug.LogError("C6_LAB_SNAPSHOT_ABORT " + reason);
            Stop();
            SetStatus("Room ended: state exceeded the safe network size.");
        }

        private void Request(LabNetworkPacket packet)
        {
            if (!CanProcessBattleActions)
            {
                // Release callbacks can have primed one or two owner-local previews already.
                // Freezing a game must refuse the command without leaving those bodies hidden.
                if (IsBoardAction(packet.type)) RejectBoardActionLocally(packet, "BATTLE_NOT_PLAYING");
                return;
            }
            packet.sessionId = sessionId;
            packet.round = latest?.round ?? model?.Round ?? 0;
            if (IsBoardAction(packet.type)) { SubmitBoardAction(packet); return; }
            if (packet.type == "blastHit" && DeferBlastReport(packet)) return;
            if (IsHost) HandleRequest(LocalPlayerId, packet);
            else Send(NetworkManager.ServerClientId, packet);
        }

        private void HandleRequest(ulong sender, LabNetworkPacket packet)
        {
            if (!IsHost || model == null || packet == null || !order.Contains(sender)) return;
            // Recovery remains legal during start synchronization; game actions do not.
            if (!CanProcessBattleActions && !(GameStartHeld && packet.type == "board_checkpoint")) return;
            if (IsBoardAction(packet.type)) { HandleBoardAction(sender, packet); return; }
            if (packet.sessionId != sessionId || packet.round != model.Round) return;
            double now = battleClock.Now(Time.realtimeSinceStartupAsDouble);
            if (packet.type == "action_query")
            {
                if (actionResults.TryGet(new LabActionIdentity(sessionId, model.Round, sender, packet.requestId), out var receipt))
                    DeliverReceipt(sender, receipt);
                else Send(sender, new LabNetworkPacket { type = "action_retry", sessionId = sessionId,
                    round = model.Round, requestId = packet.requestId });
                return;
            }
            if (packet.type == "board_checkpoint")
            {
                LabSnapshot checkpoint = model.Snapshot(now);
                AnnounceBombExplosions();
                AnnounceOrbExpiries();
                AnnounceGourdBombs();
                PublishOwnerBoards(checkpoint, true, sender);
                return;
            }
            if (packet.type == "pause")
            {
                if (packet.requestId < 1 || (pauseRequestIds.TryGetValue(sender, out long previous)
                    && packet.requestId <= previous)) return;
                pauseRequestIds[sender] = packet.requestId;
                PauseAsHost();
                return;
            }
            if (packet.type == FlashShakePacket)
            {
                // 야광귀 섬광 (#301): 폰을 다 흔든 사람의 눈부심만 푼다. 눈부시지 않으면 무시한다.
                if (battleClock.IsPaused || packet.requestId < 1) return;
                if (model.TryFlashShakeRelease(sender, packet.flashGeneration, now)) PublishSnapshot(true);
                return;
            }
            if (packet.type == "charmTap")
            {
                // Only a player beside the charmed one counts; enough taps wake them at once (#251).
                if (!model.TryCharmTap(sender, now, out var woken)) return;
                // Sent before the snapshot that no longer holds the charm, so every screen knows it was woken.
                if (woken.HasValue) AnnounceCharmWoken(woken.Value);
                PublishSnapshot(true);
                return;
            }
            if (packet.type == "blastHit")
            {
                int burned = model.MarkBlasted(sender, packet.firstId, packet.ids, now);
                Debug.Log("C6_LAB_BLAST_BURN owner=" + sender + " bomb=" + packet.firstId + " burned=" + burned);
                if (burned > 0) PublishSnapshot(true);
            }
        }

        private void SpawnHostProjectile(LabOrbState orb, Vector2 releaseVelocity, float releaseX01, long actionId = 0)
        {
            if (orb == null || target == null || config == null) return;
            int selectedSeat = (playerSlots?.GetSlot(orb.owner) ?? 0) - 1;
            int seat = latest != null && latest.players != null
                ? Array.Find(latest.players, p => p.id == orb.owner)?.seat ?? selectedSeat : selectedSeat;
            int seatCount = latest?.players?.Length ?? targetParticipantCount;
            if (seat < 0 || seat >= seatCount) return;
            // 2:2 (#281): the throw starts from the same alternating spot as the thrower's camera.
            seat = LabTeams.RingSeatFor(latest, seat);
            Vector3 targetCenter = target.transform.position;
            Vector3 seatAnchor = LabThrowMath.SeatPosition(seat, seatCount, targetCenter,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset, !LabTeams.FourPlayerTeams(latest));
            Vector3 launchPosition = LabThrowMath.LaunchPosition(seat, seatCount, targetCenter, releaseX01, config);
            Vector3 velocity = LabThrowMath.LaunchVelocity(releaseVelocity, seatAnchor, targetCenter, config);
            // A blast-shrunk orb flies small and so has less reach on the gourd (#213).
            var projectile = LabProjectile.Create(launchPosition, config, projectileRoot, orb.element,
                orb.small ? config.BlastShrinkScale : 1f);
            projectile.name = "Projectile " + orb.id;
            float sizeScale = model != null ? model.LaunchScaleOf(orb.id) : 1f;
            projectile.SetRadiusScale(sizeScale);
            projectile.SetMissSurface(battleFloor);
            SyncJigwiFireSurfaces();
            projectile.SetFireSurfaces(JigwiFireSurfacesFor(orb.owner));
            projectile.Launch(orb.id, orb.owner, velocity, config, target, OnProjectileResolved);
            var notice = new LabNetworkPacket { type = "projectile", sessionId = sessionId,
                round = model.Round, requestId = actionId,
                firstId = orb.id, owner = orb.owner, element = (int)orb.element, small = orb.small,
                x = launchPosition.x, y = launchPosition.y, z = launchPosition.z,
                wx = velocity.x, wy = velocity.y, wz = velocity.z, sizeScale = sizeScale };
            foreach (ulong id in order) if (id != LocalPlayerId) Send(id, notice);
            Debug.Log("C6_LAB_THROW orb=" + orb.id + " seat=" + seat
                + " size=" + sizeScale.ToString("F2", CultureInfo.InvariantCulture)
                + " form=" + config.ThrowForm + " origin=" + launchPosition
                + " swipe=" + releaseVelocity + " launch=" + velocity
                + " gravity=" + config.EffectiveThrowGravity.ToString("F2", CultureInfo.InvariantCulture)
                + " exponent=" + config.ThrowPowerExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " loft=" + config.ThrowLoftOffsetDegrees.ToString("F1", CultureInfo.InvariantCulture)
                + " damping=" + config.ThrowAirDamping.ToString("F2", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Only the Host frame boundary samples world-down. Model requests use its last sample;
        /// advancing before replacing it prevents the new pose from changing elapsed history.
        /// </summary>
        private void SampleHostPaintFlow(double now)
        {
            if (!IsHost || model == null) return;
            Vector3 localDown = target != null && target.VisualMount != null
                ? target.VisualMount.InverseTransformDirection(Vector3.down) : Vector3.down;
            if (!model.SetPaintFlowDirection(localDown, now)) model.Tick(now);
        }

        private void OnProjectileResolved(LabProjectileResult result)
        {
            if (!IsHost || model == null) return;
            double now = battleClock.Now(Time.realtimeSinceStartupAsDouble);
            if (result.Kind == LabProjectileHitKind.Fire)
            {
                if (!model.ConfirmJigwiFireHit(result.ProjectileId, result.FireId, result.FireTargetId, now, out bool extinguished))
                    model.ConfirmMiss(result.ProjectileId, now, countThrow: false); // 불을 노린 투척은 명중률에서 뺀다
                SetNotice(extinguished ? "Water extinguished a flame." : "Only a Water Combined orb extinguishes fire.");
            }
            else if (result.Hit)
            {
                // The paint shader samples normalized mesh-local positions, including the shell's
                // size and offset. The bound hitbox shares that exact mesh and transform.
                Vector3 paintDirection = target != null && target.Hitbox != null
                    ? target.Hitbox.transform.InverseTransformPoint(result.HitPoint).normalized : Vector3.zero;
                if (model.ConfirmHit(result.ProjectileId, now, paintDirection,
                    out var element, out int actualDamage, out int impactValue) && target != null)
                {
                    Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
                    Vector3 targetLocalPoint = hitbox.InverseTransformPoint(result.HitPoint);
                    float signedYawImpulse = target.GetComponent<LabHangingTarget>()
                        ?.ApplyHostImpact(result.HitPoint, result.ImpactVelocity) ?? 0f;
                    target.NotifyApprovedHit(targetLocalPoint, element);
                    impactFeedback?.PlayHit(target, targetLocalPoint, element, actualDamage,
                        result.ImpactVelocity, false);
                    if (result.OwnerPlayerId == LocalPlayerId)
                    {
                        LastLocalHitOffset = LabThrowSound.HitOffset(targetLocalPoint,
                            hitbox.InverseTransformDirection(result.ImpactVelocity));
                        LocalThrowHit?.Invoke(impactValue);
                    }
                    Color paintColor = model.PaintColorFor(element);
                    var impact = new LabNetworkPacket { type = "targetImpact", sessionId = sessionId,
                        round = model.Round, firstId = result.ProjectileId, owner = result.OwnerPlayerId,
                        element = (int)element, damage = actualDamage, impactValue = impactValue,
                        paintX = paintDirection.x, paintY = paintDirection.y, paintZ = paintDirection.z,
                        paintR = Mathf.Clamp01(paintColor.r), paintG = Mathf.Clamp01(paintColor.g),
                        paintB = Mathf.Clamp01(paintColor.b), paintA = Mathf.Clamp01(paintColor.a),
                        x = targetLocalPoint.x, y = targetLocalPoint.y, z = targetLocalPoint.z,
                        wx = result.ImpactVelocity.x, wy = result.ImpactVelocity.y, wz = result.ImpactVelocity.z,
                        spinImpulse = signedYawImpulse };
                    foreach (ulong id in order) if (id != LocalPlayerId) Send(id, impact);
                }
            }
            else if (model.ConfirmMiss(result.ProjectileId, now))
            {
                // 빗나감은 던진 사람에게만 알린다. 같은 구슬은 Host가 한 번만 확정한다.
                if (result.OwnerPlayerId == LocalPlayerId) LocalThrowMissed?.Invoke();
                else if (order.Contains(result.OwnerPlayerId))
                    Send(result.OwnerPlayerId, new LabNetworkPacket { type = "targetMiss", sessionId = sessionId,
                        round = model.Round, firstId = result.ProjectileId, owner = result.OwnerPlayerId });
            }
            LastShotReport = string.Format(CultureInfo.InvariantCulture,
                "SHOT {0} | {1:F2}s | peak +{2:F2}m | range {3:F2}m",
                result.Hit ? "HIT" : "MISS", result.FlightSeconds,
                result.PeakRise, result.HorizontalDistance);
            Debug.Log("C6_LAB_PROJECTILE id=" + result.ProjectileId + " hit=" + result.Hit
                + " flight=" + result.FlightSeconds.ToString("F2", CultureInfo.InvariantCulture)
                + " peak=" + result.PeakRise.ToString("F2", CultureInfo.InvariantCulture)
                + " range=" + result.HorizontalDistance.ToString("F2", CultureInfo.InvariantCulture)
                + " contact=" + result.HitPoint);
            if (developerSolo) SetNotice(LastShotReport);
            PublishSnapshot(true);
        }

        private void SyncBoard()
        {
            ApplyGameStartSimulationHold();
            SyncJigwiFireSurfaces();
            if (board == null || latest == null) return;
            var current = new HashSet<string>();
            foreach (var orb in latest.orbs ?? Array.Empty<LabOrbState>())
            {
                if (orb.owner != LocalPlayerId || orb.inFlight) continue;
                current.Add(orb.id);
                if (board.IsSnapshotSuppressed(orb.id)) continue;
                // A fever (#281) generates team orbs that are already combined; they still pop out of the drum.
                bool showSpawn = orb.origin == LabOrbOrigin.Generated && orb.originOwner == LocalPlayerId
                    && (orb.kind != LabOrbKind.Combined || orb.fever);
                // Every screen derives a locked bomb's fuse from the shared Host clock in the snapshot.
                float fuse = orb.kind == LabOrbKind.Bomb && orb.lockSwipes > 0 && config != null
                    ? Mathf.Max(0f, (float)(orb.createdAt + config.ItemLifetimeSeconds - latest.serverTime)) : -1f;
                board.UpsertOrb(orb.id, orb.kind, new Vector2(orb.x, orb.y),
                    new Vector2(orb.vx, orb.vy), true, showSpawn, orb.element, orb.lockSwipes, orb.relayStage, fuse,
                    orb.JustUnlocked(latest.serverTime), small: orb.small,
                    // Every screen counts the blast fire down from the same Host clock (#213).
                    burnSeconds: config != null ? Mathf.Max(orb.BurnSecondsLeft(latest.serverTime, config.BlastBurnSeconds),
                        orb.jigwiGenerationBurn && orb.kind.IsRaw()
                            ? orb.LifeSecondsLeft(latest.serverTime, config.OrbLifetimeSeconds, config.ItemLifetimeSeconds) : 0f) : 0f,
                    // The white flash before an orb runs out (#235); a locked bomb shows its own red one.
                    lifeSeconds: config == null || (orb.kind == LabOrbKind.Bomb && orb.lockSwipes > 0) ? -1f
                        : orb.LifeSecondsLeft(latest.serverTime, config.OrbLifetimeSeconds, config.ItemLifetimeSeconds),
                    jigwiScale: orb.jigwiRadiusScale, fever: orb.fever,
                    // An unlocked bomb (#328) glides in to the Host's spot like a drum, then opens there.
                    arrival: orb.kind == LabOrbKind.Bomb && orb.JustArrived(latest.serverTime) ? orb.arrival : LabDrumArrival.None,
                    stopAt: orb.stopAt);
            }
            foreach (var drum in latest.drums ?? Array.Empty<LabOrbState>())
            {
                if (drum.owner != LocalPlayerId) continue;
                current.Add(drum.id);
                // A drum just handed to this player arrives the way the Host drew it (#178); an older
                // handoff, seen late after a reconnect, is not replayed.
                board.UpsertOrb(drum.id, drum.kind, new Vector2(drum.x, drum.y), new Vector2(drum.vx, drum.vy),
                    arrival: drum.JustArrived(latest.serverTime) ? drum.arrival : LabDrumArrival.None, stopAt: drum.stopAt);
            }
            foreach (string id in board.OrbIds)
                if (!current.Contains(id) && !board.ShouldPreserveFromSnapshot(id)) board.RemoveOrb(id);
            visibleOrbIds.Clear();
            foreach (string id in current) visibleOrbIds.Add(id);
            board.SetInteractionEnabled(CanProcessBattleActions && latest.phase == LabPhase.Playing && !latest.paused
                && (IsHost || !boardReplica.NeedsCheckpoint
                    && (boardReplica.LatestSequence == 0 || boardReplica.InteractionAllowed)));
            // The gourd's swipe block (#215) shows and bounces only on the blocked player's own board.
            bool blockedHere = latest.SwipeBlockOn(LocalPlayerId);
            board.SetSwipeBlock(blockedHere ? (latest.swipeBlockedRight ? 1 : -1) : 0,
                blockedHere ? (float)(latest.swipeBlockedUntil - latest.serverTime) : 0f);
            // 2:2 (#281): the outer edge of this player's board is closed; only the teammate's side passes.
            board.SetTeamWall(LabTeams.ClosedSide(latest, LocalPlayerId));
            // The fox's charm (#251) stops throws and shows its haze only on the charmed player's own board.
            bool charmedHere = latest.CharmOn(LocalPlayerId);
            board.SetCharm(charmedHere ? (float)(latest.charmUntil - latest.serverTime) : 0f);
            board.SetJigwiOverheat(latest.phase == LabPhase.Playing
                && latest.JigwiFor(LocalPlayerId)?.Overheated(latest.serverTime) == true);
            if (blockedHere && (latest.round != announcedBlockRound || latest.swipeBlockStartedAt != announcedBlockStart))
            {
                announcedBlockRound = latest.round;
                announcedBlockStart = latest.swipeBlockStartedAt;
                if (latest.serverTime - latest.swipeBlockStartedAt <= SwipeBlockAnnounceWindowSeconds)
                    SwipeBlockStarted?.Invoke(latest.swipeBlockedRight, (float)(latest.swipeBlockedUntil - latest.serverTime));
            }
        }

        private void OnEdgeCrossed(LabOrbEdgeCrossing edge)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            if (developerSolo)
            {
                // The single player has no neighbor. Restore the stopped edge orb immediately.
                board.RejectEdge(edge.OrbId);
                return;
            }
            board.SetOrbLocked(edge.OrbId, true);
            Request(new LabNetworkPacket { type = "transfer", requestId = ++requestSequence, firstId = edge.OrbId, right = edge.ToRight, x = edge.ToRight ? 1f : 0f, y = edge.Height01, vx = edge.VelocityInBoardWidthsPerSecond.x, vy = edge.VelocityInBoardWidthsPerSecond.y,
                crossing = (int)edge.Kind });
        }

        private void OnCombineRequested(string firstId, string secondId, Vector2 center, Vector2 velocity)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            Request(new LabNetworkPacket { type = "combine", requestId = ++requestSequence, firstId = firstId, secondId = secondId, x = center.x, y = center.y, vx = velocity.x, vy = velocity.y,
                passiveCombine = board != null && board.IsContactCombineRequest });
        }

        /// <summary>매혹 (#251): 하트 말풍선을 눌러 옆 사람을 깨우는 한 번을 Host에 보낸다. 셈은 Host가 한다.</summary>
        public void TapCharm()
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            Request(new LabNetworkPacket { type = "charmTap", requestId = ++requestSequence });
        }

        private void OnFoxFireTouched(string foxId, string orbId)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused)
            {
                board.RejectPendingLaunch(foxId);
                board.RejectPendingLaunch(orbId);
                return;
            }
            Request(new LabNetworkPacket { type = "fox", requestId = ++requestSequence, firstId = foxId, secondId = orbId });
        }

        private void OnUseRequested(string orbId)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            Vector2 position = board != null && board.TryGetMotion(orbId, out var point, out _) ? point : Vector2.one * .5f;
            Request(new LabNetworkPacket { type = "use", requestId = ++requestSequence, firstId = orbId,
                x = position.x, y = position.y });
        }

        private void OnThrowRequested(LabOrbThrowGesture gesture)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused) return;
            board.SetOrbLocked(gesture.OrbId, true);
            Request(new LabNetworkPacket { type = "launch", requestId = ++requestSequence,
                firstId = gesture.OrbId, x = gesture.ReleaseX01,
                y = gesture.DistanceInBoardWidths, vx = gesture.Velocity.x, vy = gesture.Velocity.y });
        }

        private void Receive(ulong sender, FastBufferReader reader)
        {
            if (joinFinishPending || !TryRead(reader, out var packet)) return;
            if (packet.type == "game_flow")
            {
                ReceiveGameFlow(sender, packet.reason);
                return;
            }
            if (IsHost)
            {
                if (sender == LocalPlayerId || !order.Contains(sender)) return;
                if (packet.type == "slot_select")
                {
                    HandleSlotSelectionRequest(sender, packet);
                    return;
                }
                if (packet.type == "config_ack")
                {
                    if (model == null && packet.configRevision == configRevision
                        && packet.configFingerprint == configFingerprint)
                    {
                        configAcknowledgements.Add(sender);
                        BroadcastLobby();
                        if (autoStart && CanStartBattle) StartBattle();
                        else TryStartPendingDeveloperRestart();
                    }
                    return;
                }
                if (packet.type == "paint_resync")
                {
                    if (model != null && packet.sessionId == sessionId && packet.round == model.Round
                        && packet.configRevision == configRevision && packet.configFingerprint == configFingerprint)
                    {
                        double now = Time.realtimeSinceStartupAsDouble;
                        if (!paintCheckpointRequests.TryGetValue(sender, out double last) || now - last >= .5d)
                        {
                            paintCheckpointRequests[sender] = now;
                            if (!HasPendingBulk(sender)) SendPaintFrames(sender, PeerPaintPublisher(sender).Checkpoint());
                        }
                    }
                    return;
                }
                HandleRequest(sender, packet);
                return;
            }
            if (sender != NetworkManager.ServerClientId) return;
            switch (packet.type)
            {
                case "action_retry":
                    ReceiveActionRetry(packet);
                    break;
                case "action_result":
                    ReceiveActionReceipt(packet.actionReceipt);
                    break;
                case "board_delta":
                    ReceiveBoardChunk(packet);
                    break;
                case "lobby":
                {
                    if (packet.targetParticipants < 2 || packet.targetParticipants > 5
                        || packet.seats == null || packet.seats.Length > packet.targetParticipants
                        || packet.seats.Distinct().Count() != packet.seats.Length
                        || packet.slotAssignments == null || !ValidRoomName(packet.roomName)
                        || packet.lobbyRevision <= receivedLobbyRevision
                        || packet.snapshotSequence < incomingSequence
                        || packet.snapshotSequence < lobbySnapshotSequenceFloor) return;
                    var newSlots = new LabPlayerSlots(packet.targetParticipants);
                    if (!newSlots.TryReplace(packet.slotAssignments, packet.seats)) return;
                    receivedLobbyRevision = packet.lobbyRevision;
                    connectedRoomName = packet.roomName ?? string.Empty;
                    lobbySnapshotSequenceFloor = packet.snapshotSequence;
                    incomingSequence = Math.Max(incomingSequence, packet.snapshotSequence);
                    incomingChunks = null;
                    incomingChunkCount = 0;
                    incomingChunkReceived = 0;
                    order.Clear();
                    latest = null;
                    sessionId = null;
                    ResetPaintTransport();
                    ResetActionTransport();
                    pauseRequestPending = false;
                    impactReceipts.Reset();
                    impactFeedback?.Clear();
                    ClearProjectiles();
                    target?.GetComponent<LabHangingTarget>()?.ResetSwing();
                    visibleOrbIds.Clear();
                    if (board != null) board.Clear();
                    ApplyLocalPause(false);
                    order.AddRange(packet.seats);
                    playerSlots = newSlots;
                    targetParticipantCount = packet.targetParticipants;
                    pendingDeveloperRestart = packet.restarting;
                    TryCompleteSlotSelection();
                    if (packet.configRevision != configRevision || packet.configFingerprint != configFingerprint)
                        clientConfigReady = false;
                    SetStatus("LOBBY: " + order.Count + " / " + targetParticipantCount
                        + " · P " + playerSlots.SelectedCount + "/" + targetParticipantCount
                        + (clientConfigReady ? " · settings confirmed" : " · waiting for settings"));
                    Debug.Log("C6_LAB_LOBBY participants=" + order.Count + " slots="
                        + string.Join(",", playerSlots.Snapshot().Select(item => "P" + item.slotNumber + ":" + item.playerId)));
                    break;
                }
                case "slot_result":
                    ReceiveSlotResult(packet);
                    break;
                case "config":
                    ReceiveSessionConfig(packet);
                    break;
                case "paint_cells":
                    ReceivePaintFrame(packet);
                    break;
                case "snapshot_chunk":
                    ReceiveSnapshotChunk(packet);
                    break;
                case "reject":
                    if (packet.sessionId == sessionId) RejectLocally(packet.firstId, packet.reason);
                    break;
                case "combined":
                    if (packet.sessionId == sessionId) LocalCombined?.Invoke();
                    break;
                case "notice":
                    if (packet.sessionId == sessionId) SetNotice(packet.reason);
                    break;
                case "orbExpired":
                    if (packet.sessionId == sessionId && packet.round == actionRound
                        && !string.IsNullOrEmpty(packet.firstId)) OnOrbExpired(packet.firstId);
                    break;
                case "bombBlast":
                    if (packet.sessionId == sessionId && Finite(packet.x) && Finite(packet.y))
                        OnBombBlast(packet.firstId, new Vector2(Mathf.Clamp01(packet.x), Mathf.Clamp01(packet.y)));
                    break;
                case "foxFire":
                    if (packet.sessionId == sessionId && packet.owner == LocalPlayerId && packet.ids != null
                        && packet.ids.Length > 0 && packet.ids.Length <= MaximumFoxFireOrbs
                        && packet.ids.All(id => !string.IsNullOrEmpty(id)))
                        OnFoxFire(packet.ids);
                    break;
                case "charmWoken":
                    if (packet.sessionId == sessionId) CharmWoken?.Invoke(packet.owner);
                    break;
                case "sabotage":
                    if (packet.sessionId == sessionId && packet.element >= (int)LabSabotageKind.GourdBomb
                        && packet.element <= (int)LabSabotageKind.Charm)
                        SabotageCast?.Invoke((LabSabotageKind)packet.element, packet.owner);
                    break;
                case "gourdBomb":
                    if (packet.sessionId == sessionId && !string.IsNullOrEmpty(packet.firstId)
                        && Finite(packet.x) && Finite(packet.y))
                        OnGourdBomb(packet.owner, packet.firstId,
                            new Vector2(Mathf.Clamp01(packet.x), Mathf.Clamp01(packet.y)));
                    break;
                case "targetImpact":
                    if (target != null && ValidPaintImpact(packet)
                        && impactReceipts.TryAccept(packet.sessionId, packet.round,
                        packet.firstId, (LabOrbElement)packet.element, packet.impactValue,
                        new Vector3(packet.x, packet.y, packet.z),
                        new Vector3(packet.wx, packet.wy, packet.wz), packet.spinImpulse))
                    {
                        // Cell state arrives separately from feedback; never rerun a local Stamp.
                        target.GetComponent<LabHangingTarget>()?.ApplyRemoteImpact(
                            new Vector3(packet.wx, packet.wy, packet.wz), packet.spinImpulse);
                        target.NotifyApprovedHit(new Vector3(packet.x, packet.y, packet.z), (LabOrbElement)packet.element);
                        impactFeedback?.PlayHit(target, new Vector3(packet.x, packet.y, packet.z),
                            (LabOrbElement)packet.element, packet.damage,
                            new Vector3(packet.wx, packet.wy, packet.wz),
                            false);
                        // 같은 타격은 위에서 한 번만 받아들이므로 소리·햅틱도 한 번만 난다.
                        if (packet.owner == LocalPlayerId)
                        {
                            Transform localHitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
                            LastLocalHitOffset = LabThrowSound.HitOffset(new Vector3(packet.x, packet.y, packet.z),
                                localHitbox.InverseTransformDirection(new Vector3(packet.wx, packet.wy, packet.wz)));
                            LocalThrowHit?.Invoke(packet.impactValue);
                        }
                    }
                    break;
                case "targetMiss":
                    if (packet.sessionId == sessionId && packet.owner == LocalPlayerId
                        && packet.firstId != lastMissNoticeId)
                    {
                        lastMissNoticeId = packet.firstId;
                        LocalThrowMissed?.Invoke();
                    }
                    break;
                case "projectile":
                    if (latest != null && packet.sessionId == sessionId && !HasPresentedProjectile(packet)
                        && target != null && config != null && Finite(packet.x) && Finite(packet.y)
                        && Finite(packet.z) && Finite(packet.wx) && Finite(packet.wy) && Finite(packet.wz))
                    {
                        Vector3 position = new Vector3(packet.x, packet.y, packet.z);
                        Vector3 velocity = new Vector3(packet.wx, packet.wy, packet.wz);
                        LabOrbElement element = packet.element >= (int)LabOrbElement.None && packet.element <= (int)LabOrbElement.Earth
                            ? (LabOrbElement)packet.element : LabOrbElement.None;
                        LabProjectile visual = LabProjectile.Create(position, config, projectileRoot, element,
                            packet.small ? config.BlastShrinkScale : 1f);
                        if (packet.sizeScale > 0f && packet.sizeScale != 1f) visual.SetRadiusScale(packet.sizeScale);
                        visual.SetMissSurface(battleFloor);
                        SyncJigwiFireSurfaces();
                        visual.SetFireSurfaces(JigwiFireSurfacesFor(packet.owner));
                        visual.LaunchVisual(velocity, config, target);
                    }
                    break;
            }
        }

        private void ReceiveSlotResult(LabNetworkPacket packet)
        {
            if (!slotSelectionPending || packet.requestId != pendingSlotRequestId
                || packet.slotNumber != pendingSlotNumber) return;
            if (!packet.accepted)
            {
                ClearPendingSlotSelection();
                SetNotice(packet.reason == "SLOT_TAKEN" ? "That P number is already selected."
                    : "P number selection was rejected: " + packet.reason);
                return;
            }
            if (packet.lobbyRevision <= 0) return;
            pendingSlotApproved = true;
            pendingSlotLobbyRevision = packet.lobbyRevision;
            TryCompleteSlotSelection();
        }

        private void TryCompleteSlotSelection()
        {
            // Approval and lobby data may be processed separately. Do not unlock the
            // controls or display a speculative assignment until both agree.
            if (!slotSelectionPending || !pendingSlotApproved
                || receivedLobbyRevision < pendingSlotLobbyRevision
                || playerSlots == null || playerSlots.GetSlot(LocalPlayerId) != pendingSlotNumber) return;
            ClearPendingSlotSelection();
            SetNotice(string.Empty);
        }

        private void ClearPendingSlotSelection()
        {
            slotSelectionPending = false;
            pendingSlotRequestId = 0;
            pendingSlotNumber = 0;
            slotRequestSentAt = 0d;
            pendingSlotApproved = false;
            pendingSlotLobbyRevision = 0;
        }

        private void ReceiveSessionConfig(LabNetworkPacket packet)
        {
            if (packet.configRevision < 1 || packet.targetParticipants < 2 || packet.targetParticipants > 5
                || packet.tuning == null || packet.tuning.Length != LabConfig.DeveloperFields.Count) return;
            string expected;
            try { expected = LabSessionSettings.Fingerprint(packet.tuning, packet.targetParticipants, LabProtocolVersion); }
            catch (ArgumentException) { return; }
            if (expected != packet.configFingerprint)
            {
                SetStatus("Host settings failed integrity check. Rejoin the room.");
                return;
            }
            if (clientConfigReady && packet.configRevision == configRevision
                && packet.configFingerprint == configFingerprint)
            {
                Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "config_ack",
                    configRevision = configRevision, configFingerprint = configFingerprint });
                return;
            }
            if (packet.configRevision < configRevision) return;
            if (!TryInstallValues(packet.tuning, out string error))
            {
                SetStatus("Host settings rejected: " + error);
                return;
            }
            targetParticipantCount = packet.targetParticipants;
            configRevision = packet.configRevision;
            configFingerprint = packet.configFingerprint;
            clientConfigReady = true;
            Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "config_ack",
                configRevision = configRevision, configFingerprint = configFingerprint });
            SetStatus("Settings confirmed; waiting for " + targetParticipantCount + " players.");
            Debug.Log("C6_LAB_CONFIG_READY revision=" + configRevision + " fingerprint=" + configFingerprint);
        }

        private void ReceiveSnapshotChunk(LabNetworkPacket packet)
        {
            if (!clientConfigReady || packet.configRevision != configRevision
                || packet.configFingerprint != configFingerprint
                || string.IsNullOrEmpty(packet.sessionId) || packet.snapshotSequence < 1
                || packet.snapshotSequence <= lobbySnapshotSequenceFloor
                || packet.chunkCount < 1 || packet.chunkCount > 1024
                || packet.chunkIndex < 0 || packet.chunkIndex >= packet.chunkCount
                || string.IsNullOrEmpty(packet.chunkData)
                || packet.chunkData.Length > LabSessionSettings.SnapshotChunkCharacters) return;
            if (packet.snapshotSequence < incomingSequence) return;
            if (packet.snapshotSequence > incomingSequence)
            {
                incomingSequence = packet.snapshotSequence;
                incomingChunkCount = packet.chunkCount;
                incomingChunks = new string[packet.chunkCount];
                incomingChunkReceived = 0;
            }
            if (packet.chunkCount != incomingChunkCount || incomingChunks == null) return;
            if (incomingChunks[packet.chunkIndex] == null)
            {
                incomingChunks[packet.chunkIndex] = packet.chunkData;
                incomingChunkReceived++;
            }
            else if (incomingChunks[packet.chunkIndex] != packet.chunkData) return;
            if (incomingChunkReceived != incomingChunkCount) return;
            string json = string.Concat(incomingChunks);
            incomingChunks = null;
            incomingChunkReceived = 0;
            if (json.Length > LabSessionSettings.MaximumSnapshotCharacters) return;
            try { if (Utf8.GetByteCount(json) > LabSessionSettings.MaximumSnapshotBytes) return; }
            catch (Exception) { return; }
            LabSnapshot snapshot;
            try { snapshot = JsonUtility.FromJson<LabSnapshot>(json); }
            catch (Exception) { return; }
            if (snapshot == null || snapshot.sessionId != packet.sessionId
                || snapshot.players == null || snapshot.players.Length != targetParticipantCount
                || !LabJigwiNetworkValidation.Valid(snapshot.jigwi)
                || !LabFlashNetworkValidation.Valid(snapshot)) return;
            if (latest != null && latest.sessionId == snapshot.sessionId && latest.round > snapshot.round) return;
            if (!PaintReceiver.Matches(snapshot))
            {
                pendingPaintSnapshot = snapshot;
                awaitingPaintSession = snapshot.sessionId;
                awaitingPaintRound = snapshot.round;
                RequestPaintCheckpoint();
                return;
            }
            ApplyPaintSnapshot(snapshot);
        }

        private void ApplyPaintSnapshot(LabSnapshot snapshot)
        {
            // A newer matched transaction supersedes any older snapshot waiting for recovery.
            pendingPaintSnapshot = null;
            bool logTransition = latest == null || latest.sessionId != snapshot.sessionId
                || latest.phase != snapshot.phase || latest.paused != snapshot.paused;
            if (sessionId != snapshot.sessionId || latest == null || latest.round != snapshot.round)
            {
                target?.GetComponent<LabHangingTarget>()?.ResetSwing();
                impactFeedback?.Clear();
            }
            sessionId = snapshot.sessionId;
            latest = snapshot;
            LabBoardFrame deferred = deferredBoardFrame;
            EnsureActionRound();
            // EnsureActionRound retires old display identity: install this atomic map afterwards.
            CopyPaintToDisplay(receivedPaintSurface, snapshot);
            if (!IsHost) latest = boardReplica.Overlay(latest);
            if (deferred != null && deferred.sessionId == snapshot.sessionId && deferred.round == snapshot.round)
                ReceiveBoardFrame(deferred);
            pendingDeveloperRestart = false;
            ClearPendingSlotSelection();
            if (target != null) target.GetComponent<LabGourdPaint>()?.RefreshCommittedMap();
            if (snapshot.paused || snapshot.phase != LabPhase.Playing) pauseRequestPending = false;
            impactReceipts.SetActive(snapshot.sessionId, snapshot.round);
            ApplyLocalPause(CanProcessBattleActions && latest.phase == LabPhase.Playing && latest.paused);
            bool targetPaused = latest.phase == LabPhase.Playing && latest.paused;
            if (target != null && Finite(latest.targetYawDegrees)
                && Finite(latest.targetYawSpeedDegreesPerSecond))
                target.GetComponent<LabHangingTarget>()?.ReconcileYaw(
                    latest.targetYawDegrees, latest.targetYawSpeedDegreesPerSecond, targetPaused);
            if (target != null)
                target.GetComponent<LabHangingTarget>()?.ReconcileRope(
                    latest.targetRopeCycleElapsed, latest.targetRopeHitOffset,
                    latest.targetRopeHitVelocity, targetPaused);
            SyncBoard();
            if (logTransition && latest.phase == LabPhase.Playing) SetNotice(string.Empty);
            SetStatus((latest.paused ? "PAUSED" : latest.phase.ToString())
                + ": " + targetParticipantCount + " / " + targetParticipantCount);
            if (logTransition) Debug.Log("C6_LAB_SNAPSHOT phase=" + latest.phase + " hp=" + latest.hp
                + " remaining=" + latest.remaining.ToString("F1") + " orbs=" + (latest.orbs?.Length ?? 0));
        }

        private uint displayedPaintVisualVersion;

        private void CopyPaintToDisplay(LabPaintSurface source, LabSnapshot snapshot)
        {
            if (displayedPaintSession == snapshot.sessionId && displayedPaintRound == snapshot.round
                && displayedPaintRevision == source.Revision && displayedPaintCount == snapshot.paintCount
                && displayedPaintVisualVersion == source.VisualVersion
                && clientPaintSurface.Columns == source.Columns && clientPaintSurface.Rows == source.Rows) return;
            var pixels = new Color32[source.CellCount];
            var painters = new byte[source.CellCount];
            source.CopyPixels(pixels);
            source.CopyPainterSlots(painters);
            if (!clientPaintSurface.TryReplacePixels(source.Columns, source.Rows, source.Revision, pixels, painters, snapshot.paintCount, source.CopyVisualStamps()))
                throw new InvalidOperationException("Invalid authoritative paint display state.");
            displayedPaintSession = snapshot.sessionId; displayedPaintRound = snapshot.round;
            displayedPaintRevision = source.Revision; displayedPaintCount = snapshot.paintCount;
            displayedPaintVisualVersion = source.VisualVersion;
        }

        private bool SendPaintFrames(ulong receiver, LabPaintFrame[] frames)
        {
            foreach (LabPaintFrame frame in frames)
                if (!QueueBulk(receiver, new LabNetworkPacket { type = "paint_cells", sessionId = frame.session,
                    round = frame.round, snapshotSequence = frame.epoch, configRevision = configRevision,
                    configFingerprint = configFingerprint, paintFrame = frame })) return false;
            return true;
        }

        private void ReceivePaintFrame(LabNetworkPacket packet)
        {
            LabPaintFrame frame = packet.paintFrame;
            if (!clientConfigReady || packet.configRevision != configRevision || packet.configFingerprint != configFingerprint
                || frame == null || frame.session != packet.sessionId || frame.round != packet.round
                || frame.epoch != packet.snapshotSequence || frame.epoch <= lobbySnapshotSequenceFloor
                || frame.epoch < PaintReceiver.Epoch) return;
            awaitingPaintSession = frame.session;
            awaitingPaintRound = frame.round;
            LabPaintReceive result = PaintReceiver.Receive(frame);
            if (result == LabPaintReceive.NeedCheckpoint) RequestPaintCheckpoint();
            if (result == LabPaintReceive.Applied && pendingPaintSnapshot != null && PaintReceiver.Matches(pendingPaintSnapshot))
            {
                LabSnapshot ready = pendingPaintSnapshot;
                pendingPaintSnapshot = null;
                ApplyPaintSnapshot(ready);
            }
        }

        private void RequestPaintCheckpoint()
        {
            if (IsHost || !IsConnected || string.IsNullOrEmpty(awaitingPaintSession)
                || awaitingPaintRound == 0 || Time.realtimeSinceStartupAsDouble < nextPaintRequestAt) return;
            nextPaintRequestAt = Time.realtimeSinceStartupAsDouble + 1d;
            Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "paint_resync",
                sessionId = awaitingPaintSession, round = awaitingPaintRound,
                configRevision = configRevision, configFingerprint = configFingerprint });
        }

        private void ResetPaintTransport()
        {
            peerPaintPublishers.Clear();
            peerSharedRounds.Clear();
            PaintReceiver.Reset();
            clientPaintSurface.Reset(new LabPaintGrowthSettings(), 1);
            pendingPaintSnapshot = null; awaitingPaintSession = null; awaitingPaintRound = 0;
            nextPaintRequestAt = 0d; paintCheckpointRequests.Clear();
        }

        private void RejectLocally(string orbId, string reason)
        {
            if (!string.IsNullOrEmpty(orbId) && board != null) board.RejectEdge(orbId);
            SetNotice(reason ?? "Request rejected.");
            Debug.LogWarning("C6_LAB_REJECT orb=" + orbId + " reason=" + reason);
        }

        private bool Send(ulong receiver, LabNetworkPacket packet, NetworkDelivery delivery = NetworkDelivery.ReliableFragmentedSequenced)
        {
            if (manager == null || !manager.IsListening || manager.ShutdownInProgress || manager.CustomMessagingManager == null) return false;
            try
            {
                byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(packet));
                if (bytes.Length < 1 || bytes.Length > MaximumPacketBytes - 8) return false;
                using (var writer = new FastBufferWriter(bytes.Length + 8, Allocator.Temp))
                {
                    writer.WriteValueSafe(packet.protocol);
                    writer.WriteValueSafe(bytes.Length);
                    writer.WriteBytesSafe(bytes);
                    manager.CustomMessagingManager.SendNamedMessage(MessageName, receiver, writer, delivery);
                    SentControlBytes += bytes.Length + 8;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("C6_LAB_SEND_FAILED " + e.GetType().Name);
                return false;
            }
        }

        private static bool TryRead(FastBufferReader reader, out LabNetworkPacket packet)
        {
            packet = null;
            int left = reader.Length - reader.Position;
            if (left < 9 || left > MaximumPacketBytes || !reader.TryBeginRead(left)) return false;
            try
            {
                reader.ReadValueSafe(out int version);
                reader.ReadValueSafe(out int count);
                if (version != 3 || count < 2 || count != reader.Length - reader.Position) return false;
                var bytes = new byte[count];
                reader.ReadBytesSafe(ref bytes, count);
                packet = JsonUtility.FromJson<LabNetworkPacket>(Utf8.GetString(bytes));
                return packet != null && packet.protocol == 3 && !string.IsNullOrEmpty(packet.type);
            }
            catch (Exception) { return false; }
        }

        private static bool ValidPaintImpact(LabNetworkPacket packet)
        {
            if (packet.damage < 0 || packet.impactValue <= 0) return false;
            if (packet.element == (int)LabOrbElement.None) return true;
            Vector3 direction = new Vector3(packet.paintX, packet.paintY, packet.paintZ);
            return Finite(direction.x) && Finite(direction.y) && Finite(direction.z)
                && direction.sqrMagnitude > .00001f && direction.sqrMagnitude < 1.01f
                && Finite(packet.paintR) && packet.paintR >= 0f && packet.paintR <= 1f
                && Finite(packet.paintG) && packet.paintG >= 0f && packet.paintG <= 1f
                && Finite(packet.paintB) && packet.paintB >= 0f && packet.paintB <= 1f
                && Finite(packet.paintA) && packet.paintA >= 0f && packet.paintA <= 1f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void ResetLocal()
        {
            clientSessionConnected = false;
            ResetGameStartHold();
            gameBattleFrozen = false;
            gameRosterLocked = false;
            gameStageIndex = 0; gameStageId = null; gameStageYokai = LabStageYokai.None;
            connectedRoomName = string.Empty;
            ResetPaintTransport();
            ApplyLocalPause(false);
            battleClock.Reset();
            pendingDeveloperRestart = false;
            ResetActionTransport();
            ResetBulkTransport();
            pauseRequestIds.Clear();
            slotRequestIds.Clear();
            pauseRequestPending = false;
            ClearPendingSlotSelection();
            ClearProjectiles();
            order.Clear();
            playerSlots = null;
            configAcknowledgements.Clear();
            pendingApprovals.Clear();
            visibleOrbIds.Clear();
            impactReceipts.Reset();
            impactFeedback?.Clear();
            if (target != null) target.GetComponent<LabHangingTarget>()?.ResetSwing();
            model = null;
            developerSolo = false;
            latest = null;
            sessionId = null;
            configRevision = 0;
            configFingerprint = null;
            clientConfigReady = false;
            snapshotSequence = 0;
            incomingSequence = 0;
            incomingChunks = null;
            incomingChunkCount = 0;
            incomingChunkReceived = 0;
            lobbySnapshotSequenceFloor = 0;
            lobbyRevision = 0;
            receivedLobbyRevision = 0;
            LastShotReport = string.Empty;
            requestSequence = 0;
            if (board != null) board.Clear();
            Changed?.Invoke();
        }

        private bool RoomPresenceBusy => model != null || gameFlowEnabled && gameRosterLocked;

        private void StartAdvertising(LabBonjourDiscovery registeredRoom = null)
        {
            DisposeDiscovery();
            if (!LabBonjourDiscovery.IsSupported) return;
            discovery = registeredRoom ?? new LabBonjourDiscovery(RoomFamily);
            discovery.Diagnostic += message => Debug.Log("C6_LAB_DISCOVERY " + message);
            discovery.Changed += OnDiscoveryChanged;
            bool ipv4 = !(transport is LabDualStackTransport dual) || dual.HasIpv4Listener;
            bool ipv6 = transport is LabDualStackTransport second && second.HasIpv6Listener;
            if (registeredRoom != null)
            {
                connectedRoomName = registeredRoom.RegisteredServiceName;
                advertisedName = connectedRoomName;
                // The reserved socket family is only a preparation hint. Publish the real Host listeners.
                if (!discovery.UpdatePresence(ipv4, ipv6, order.Count, targetParticipantCount, RoomPresenceBusy))
                    Debug.LogWarning("C6_LAB_DISCOVERY_UNAVAILABLE " + discovery.LastError);
                return;
            }
            // Authored room cards transfer their explicit sequential registration above.
            // Keep the existing direct Game Host API's unique name; it has no pending-room allocator.
            advertisedName = gameFlowEnabled ? "Hapio-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                : LabRoomNames.ServiceBase;
            if (!discovery.StartAdvertise(advertisedName, port, ipv4, ipv6, order.Count, targetParticipantCount))
            {
                Debug.LogWarning("C6_LAB_DISCOVERY_UNAVAILABLE " + discovery.LastError);
                DisposeDiscovery();
            }
            else Debug.Log("C6_LAB_LISTEN_FAMILIES ipv4=" + ipv4 + " ipv6=" + ipv6);
        }

        private void DisposeDiscovery(bool preserveRooms = false)
        {
            if (discovery != null)
            {
                discovery.Changed -= OnDiscoveryChanged;
                discovery.Dispose();
                discovery = null;
            }
            roomSearchActive = false;
            if (!preserveRooms)
            {
                roomCacheGraceActive = false;
                discoveredRooms = Array.Empty<LabDiscoveredRoom>();
            }
            if (!IsJoiningRoom) discoveryRoomKey = null;
            advertisedName = null;
            Changed?.Invoke();
        }

        private void OnDiscoveryChanged()
        {
            if (roomSearchActive && discovery != null)
            {
                RefreshDiscoveredRooms();
                Debug.Log("C6_LAB_ROOM_LIST count=" + discoveredRooms.Count);
            }
            else if (IsHost && discovery != null && discovery.AdvertisingConfirmed)
            {
                string confirmed = discovery.RegisteredServiceName ?? string.Empty;
                if (confirmed != connectedRoomName && ValidRoomName(confirmed))
                {
                    connectedRoomName = confirmed;
                    // Registration can finish after peers arrive. Send only when the confirmed name changes.
                    if (!developerSolo && model == null) BroadcastLobby();
                }
            }
            Changed?.Invoke();
        }

        private void RefreshDiscoveredRooms()
        {
            IReadOnlyList<LabDiscoveredRoom> fresh = discovery == null
                ? Array.Empty<LabDiscoveredRoom>() : LabRoomCatalog.GroupTargets(discovery.Targets);
            if (roomCacheGraceActive && Time.realtimeSinceStartupAsDouble - roomSearchStartedAt < 5d)
            {
                discoveredRooms = fresh.Concat(discoveredRooms)
                    .GroupBy(room => room.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()).OrderBy(room => LabRoomNames.SortNumber(room.Name))
                    .ThenBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(room => room.Port).ToArray();
            }
            else discoveredRooms = fresh;
        }

        private void ClearRoomSearchMessage()
        {
            roomSearchMessage = string.Empty;
            roomSearchFailed = false;
        }

        private void SetRoomSearchMessage(string message, bool failed)
        {
            roomSearchMessage = message ?? string.Empty;
            roomSearchFailed = failed;
            SetStatus(roomSearchMessage);
        }

        private void ReportRoomSearchFailure(string error, bool preserveMessage)
        {
            string message = preserveMessage && !string.IsNullOrEmpty(roomSearchMessage)
                ? roomSearchMessage + " Search unavailable: " + error : error;
            SetRoomSearchMessage(message, true);
        }

        private static bool ValidRoomName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true; // Registration may still be pending.
            return name.Length <= 63 && !name.Any(char.IsControl) && Encoding.UTF8.GetByteCount(name) <= 63;
        }

        private void ClearProjectiles()
        {
            if (projectileRoot == null) return;
            for (int i = 0; i < projectileRoot.childCount; i++)
            {
                GameObject projectile = projectileRoot.GetChild(i).gameObject;
                projectile.SetActive(false);
                Destroy(projectile);
            }
        }

        private void SetStatus(string status) { Status = status; Changed?.Invoke(); }
        private void SetNotice(string notice) { Notice = notice; Changed?.Invoke(); }

        private void OnDestroy()
        {
            UnsubscribeBoard(board);
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnConnected;
                manager.OnClientDisconnectCallback -= OnDisconnected;
                manager.OnTransportFailure -= OnTransportFailure;
            }
            Stop();
        }
    }
}
