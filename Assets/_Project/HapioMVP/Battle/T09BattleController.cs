using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using C6.Prototype.Resources;
using C6.Prototype.Combination;
using UnityEngine;

namespace C6.Prototype.Battle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout), typeof(DirectConnectionSession), typeof(T09Hud))]
    public sealed class T09BattleController : MonoBehaviour, IOrbPointerSink
    {
        [SerializeField] private SplitScreenLayout layout;
        [SerializeField] private DirectConnectionSession connection;
        [SerializeField] private T09Hud hud;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private AttackLaunchFrame launchFrame;
        [SerializeField] private MonsterHitTarget target;

        [SerializeField] private DamagePopupLayer damagePopups;
        private int? observedHp;
        private Vector3? removedProxyPosition;
        private readonly HashSet<string> ownProxyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedOwnIds = new List<string>();
        // #20: closest pass of each own flight to the monster, for MISS placement and timing.
        private readonly Dictionary<string, MissWatch> missWatches = new Dictionary<string, MissWatch>(StringComparer.Ordinal);
        private const float MissPassMargin = .35f; // DEMO_TUNING_VALUE, world units past the closest approach
        private sealed class MissWatch { public float closest = float.PositiveInfinity; public Vector3 surface; public bool shown; }

        [SerializeField] private bool orbPhysicsEnabled;
        [SerializeField] private bool releaseThrowsEnabled;
        [SerializeField] private bool continuousTransfersEnabled;
        public bool ContinuousTransfersEnabled => continuousTransfersEnabled;
        public void ConfigureContinuousTransfers(bool enabled)
        {
            if (attack != null && attack.Connected) throw new InvalidOperationException("Choose passage mode before connecting.");
            continuousTransfersEnabled = enabled;
            if (attack != null) attack.ConfigureContinuousTransfers(enabled);
        }
        private readonly ThrowGestureSampler throwSampler = new ThrowGestureSampler();
        private ThrowHeldPreview throwPreview;
        public bool ReleaseThrowsEnabled => releaseThrowsEnabled;
        public bool ThrowArmed => releaseThrowsEnabled && gestures.ActiveOrb?.Kind == OrbKind.Combined &&
            gestures.LastRawPosition.y >= layout.BottomPixelRect.yMax;
        public void ConfigureReleaseThrows(bool enabled)
        {
            if (attack != null && attack.Connected) throw new InvalidOperationException("Choose throw mode before connecting.");
            if (attack != null) attack.ConfigureReleaseThrows(enabled);
            releaseThrowsEnabled = enabled;
        }
        private LocalOrbPhysicsBoard orbPhysics;
        private Rect physicsScreenBounds;
        private Rect physicsWorldBounds;
        private float physicsRadius;
        private Vector4 physicsTuning;
        private Vector2 physicsSampleTuning;
        private AttackSession attack;
        private ResourceSession resource;
        private CombinationSession combination;
        private BattleSession battle;
        private bool developmentSolo;
        private string displayedBattlePhase;
        private uint displayedBattleRound;
        private double lastConfirmedStamina = 100d;
        private PendingCombination pendingCombination;
        private OrbPointerInput input;
        private OrbGestureEngine gestures = new OrbGestureEngine();
        private readonly Dictionary<string, OrbView> views = new Dictionary<string, OrbView>();
        private readonly Dictionary<string, Vector2> display = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, GameObject> proxies = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, ulong> sequences = new Dictionary<string, ulong>();
        private readonly Dictionary<string, ulong> displayedTransfers = new Dictionary<string, ulong>();
        private bool transfersEnabled;
        private bool reachableEdgeTransferDistance;
        private readonly Dictionary<string, PendingInput> pending = new Dictionary<string, PendingInput>();
        private readonly HashSet<string> confirmedUnavailable = new HashSet<string>();
        private readonly HashSet<string> touchReceipts = new HashSet<string>();
        private Transform viewRoot, proxyRoot;
        private Material projectileMaterial, trailMaterial;
        private readonly Dictionary<string, ThrowFlightTrail> throwTrails = new Dictionary<string, ThrowFlightTrail>(StringComparer.Ordinal);
        private string sessionKey;
        private uint round;
        private bool? displayedDebugMode;
        private Vector2 grabOffset, dragStart, dragPointerStart;
        private float dragStartedAt;
        private bool dragIsTouch;
        private Rect previousSpace, previousLower;
        private Vector4 previousTuning;
        private float previousRadius;
        private string action = "Create HOST or JOIN / then Host START";
        private string detail = "Default: two players / DEV SOLO is an explicit test mode";
        public SplitScreenLayout Layout => layout;
        public T09Hud Hud => hud;
        public AttackSession Attack => attack;
        public ResourceSession Resource => resource;
        public CombinationSession Combination => combination;
        public BattleSession Battle => battle;
        public bool DevelopmentSolo => developmentSolo;
        public bool HasCombinationPending => pendingCombination != null;
        public OrbGestureEngine Gestures => gestures;
        public IReadOnlyDictionary<string, OrbView> Views => views;
        public int TouchLaunches { get; private set; }
        public int TouchBegins { get; private set; }
        public bool TransfersEnabled => transfersEnabled;
        public int SentTransfers { get; private set; }
        public int ReceivedTransfers { get; private set; }
        [SerializeField] private int maximumParticipants = 2;
        private int approvedPlayerNumber;
        public int MaximumParticipants => maximumParticipants;
        public void ConfigureMaximumParticipants(int maximum)
        {
            if (maximum != 2 && maximum != 5) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (attack != null && attack.Connected) throw new InvalidOperationException("Choose capacity before a connection.");
            maximumParticipants = maximum;
            if (hud != null) hud.ParticipantCapacity = maximum;
        }
        public void ConfigurePlayerNumber(int number)
        {
            if (number < 0 || number > maximumParticipants) throw new ArgumentOutOfRangeException(nameof(number));
            approvedPlayerNumber = number;
        }
        public bool OrbPhysicsEnabled => orbPhysicsEnabled;
        public LocalOrbPhysicsBoard OrbPhysics => orbPhysics;
        public void ConfigureOrbPhysics(bool enabled)
        {
            if (Application.isPlaying && views.Count > 0 && enabled != orbPhysicsEnabled)
                throw new InvalidOperationException("Choose orb physics before creating a round inventory.");
            orbPhysicsEnabled = enabled;
            if (Application.isPlaying && enabled) EnsureOrbPhysics();
            if (!enabled && orbPhysics != null) orbPhysics.SetPaused(true);
        }
        public void ConfigureTransfers(bool enabled) { transfersEnabled = enabled; }
        public void ConfigureReachableEdgeTransferDistance(bool enabled) { reachableEdgeTransferDistance = enabled; }
        public bool IsDefenseHeld => false;
        public bool CanInteract => isActiveAndEnabled && attack != null && attack.Connected && attack.Snapshot != null
            && attack.Snapshot.state == "Playing" && resource != null && resource.Connected
            && (!transfersEnabled || !resource.HasPending)
            && resource.Snapshot.playing && combination != null && combination.Connected && pendingCombination == null
            && battle != null && battle.CanAct;
        public event Action Changed;

        public void Configure(SplitScreenLayout split, DirectConnectionSession session, T09Hud overlay,
            Material sprite, AttackLaunchFrame frame, MonsterHitTarget hitTarget)
        { layout = split; connection = session; hud = overlay; spriteMaterial = sprite; launchFrame = frame; target = hitTarget; }
        private void Awake()
        {
            if (layout == null) layout = GetComponent<SplitScreenLayout>();
            if (connection == null) connection = GetComponent<DirectConnectionSession>();
            if (hud == null) hud = GetComponent<T09Hud>();
            input = GetComponent<OrbPointerInput>();
            // Explicit serialized asset reference keeps URP's actual shader in both builds.
            projectileMaterial = new Material(spriteMaterial) { name = "T09 Projectile Teal" };
            projectileMaterial.color = new Color(.25f, .85f, .77f);
            trailMaterial = new Material(spriteMaterial) { name = "T09 Throw Trail" };
            attack = gameObject.AddComponent<AttackSession>();
            attack.ConfigureInventory(false, 64);
            hud.ParticipantCapacity = maximumParticipants;
            attack.ConfigureReleaseThrows(releaseThrowsEnabled);
            attack.ConfigureContinuousTransfers(continuousTransfersEnabled);
            attack.Configure(connection, layout.Config, launchFrame, target, projectileMaterial);
            attack.Changed += OnStateChanged;
            attack.ValidHitAt += OnHostHitAt;
            attack.ProjectileMissed += OnHostProjectileMissed;
            // Stop completes asynchronously after the authority has already been released.
            connection.Changed += RefreshHud;
            attack.RequestResolved += OnResolved;
            resource = gameObject.AddComponent<ResourceSession>();
            resource.Configure(attack, layout.Config);
            resource.Changed += OnResourceChanged;
            resource.GenerationResolved += OnGenerationResolved;
            resource.RecoveryResolved += OnRecoveryResolved;
            combination = gameObject.AddComponent<CombinationSession>();
            combination.Configure(attack, resource, layout.Config);
            combination.Changed += OnCombinationChanged;
            combination.RequestResolved += OnCombinationResolved;
            battle = gameObject.AddComponent<BattleSession>();
            battle.Configure(attack, resource, combination, layout.Config);
            battle.Changed += OnBattleChanged;
            OrbView.SetSharedMaterial(spriteMaterial);
            OrbView.SetArtwork(layout.Config.OrbArt);
            OrbElements.Configure(layout.Config.TeamElements);
            viewRoot = new GameObject("T09 Local Orb Views").transform; viewRoot.SetParent(transform, false);
            proxyRoot = new GameObject("T09 Client Projectile Display Only").transform; proxyRoot.SetParent(transform, false);
            if (orbPhysicsEnabled) EnsureOrbPhysics();
        }
        private void Start()
        {
            hud.HostButton.onClick.AddListener(StartDevelopmentHost);
            hud.JoinButton.onClick.AddListener(JoinDevelopmentHost);
            hud.StartButton.onClick.AddListener(StartBattle);
            hud.SoloModeButton.onClick.AddListener(ToggleDevelopmentSolo);
            hud.RetryButton.onClick.AddListener(RetryBattle);
            hud.LobbyButton.onClick.AddListener(ReturnToLobby);
            hud.ResultEndButton.onClick.AddListener(EndDevelopmentTest);
            hud.EndButton.onClick.AddListener(EndDevelopmentTest);
            hud.GenerateButton.onClick.AddListener(GenerateOrb);
            hud.DebugFixtureButton.onClick.AddListener(StartDebugFixtureRound);
            RefreshHud();
            Debug.Log($"C6_T09_READY mode=NORMAL initialOrbs=0 build=13 device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} screen={Screen.width}x{Screen.height} safe={Screen.safeArea} staminaStart={layout.Config.StaminaStart} staminaMax={layout.Config.StaminaMax} generateCost={layout.Config.GenerateCost} recoveryPerSecond={layout.Config.StaminaRecoveryPerSecond} hitRecovery={layout.Config.StaminaHitRecovery}");
            Debug.Log("C6_T09_LOCAL_IPV4 addresses=[" + string.Join(",", DirectConnectionSession.GetLocalIPv4Addresses()) + "]");
        }
        public void StartDevelopmentHost() => StartDevelopmentHost(hud.Port);
        public bool StartDevelopmentHost(string port)
        {
            if (!isActiveAndEnabled) return false;
            ResetLocalProgress(); return attack.Host(port);
        }
        public void JoinDevelopmentHost() => JoinDevelopmentHost(hud.HostAddress, hud.Port);
        public bool JoinDevelopmentHost(string ip, string port)
        {
            if (!isActiveAndEnabled) return false;
            ResetLocalProgress(); return attack.Join(ip, port);
        }
        private void ResetLocalProgress()
        { TouchLaunches = TouchBegins = 0; touchReceipts.Clear(); action = "Starting NORMAL / empty inventory"; }
        public void ConfigureDevelopmentSolo(bool enabled)
        {
            if (battle == null || !connection.CanStart) throw new InvalidOperationException("Choose solo mode before connecting.");
            battle.ConfigureDevelopmentSolo(enabled); developmentSolo = enabled; RefreshHud();
        }
        public void ConfigureDevelopmentDuration(double? duration)
        {
            if (battle == null) throw new InvalidOperationException("Battle service is not ready.");
            battle.ConfigureDevelopmentDuration(duration); RefreshHud();
        }
        private void ToggleDevelopmentSolo()
        { if (connection.CanStart) ConfigureDevelopmentSolo(!developmentSolo); }
        public void StartBattle() => RequestHostStart();
        private Func<bool> approvedStart;
        private Action approvedRetry, approvedEnd;
        public void ConfigureApprovedLifecycle(Func<bool> start, Action retry, Action end)
        { approvedStart = start; approvedRetry = retry; approvedEnd = end; }
        public bool RequestHostStart()
        {
            if (!isActiveAndEnabled || battle == null || !battle.CanStart) return false;
            ClearLocalInput("Host Start");
            return approvedStart != null ? approvedStart() : battle.HostStart();
        }
        public void ResetDevelopmentRound() => RetryBattle();
        public void RetryBattle()
        {
            if (!isActiveAndEnabled || battle == null || !battle.IsHost) return;
            ClearLocalInput("Host Retry"); if (approvedRetry != null) approvedRetry(); else battle.RetryHost();
        }
        public void ReturnToLobby()
        {
            if (!isActiveAndEnabled || battle == null || !battle.IsHost) return;
            ClearLocalInput("Host Lobby"); if (approvedRetry != null) approvedRetry(); else battle.ReturnLobby();
        }
        public void EndDevelopmentTest()
        {
            ClearLocalInput("Session ended");
            if (approvedEnd != null) approvedEnd(); else if (battle != null) battle.EndSession(); else if (attack != null) attack.EndDevelopmentTest();
        }

        public void GenerateOrb() => RequestGenerate();
        public bool RequestGenerate()
        {
            if (!isActiveAndEnabled || battle == null || !battle.CanAct || resource == null || !resource.Connected || resource.HasPending || pendingCombination != null) return false;
            action = "GENERATE / waiting for Host";
            detail = "One Raw orb / confirmed cost " + layout.Config.GenerateCost;
            bool sent = resource.RequestGenerate();
            if (!sent && !resource.HasPending) { action = "GENERATE unavailable"; detail = resource.Status; }
            RefreshHud();
            return sent;
        }
        public void StartDebugFixtureRound()
        {
            if (!isActiveAndEnabled || battle == null || !battle.CanAct || resource == null || !resource.IsHost) return;
            CancelInteractions("Explicit DEBUG_TEST_MODE current-round Raw supply");
            if (resource.SupplyMixedDebugFixtureCurrentRound())
            {
                action = "DEBUG_TEST_MODE / mixed Yin + Yang";
                detail = "DEBUG RAW SUPPLY / timer continues / Yin + Yang only";
            }
            else { action = "Debug fixture unavailable"; detail = resource.Status; }
            RefreshHud();
        }
        private void OnResourceChanged()
        {
            // Only a mode transition changes this explanation; ordinary recovery updates keep the last result.
            if (resource.Connected && displayedDebugMode != resource.DebugTestMode)
            {
                displayedDebugMode = resource.DebugTestMode;
                if (resource.DebugTestMode)
                {
                    action = "DEBUG_TEST_MODE / mixed Yin + Yang";
                    detail = "DEBUG RAW SUPPLY / timer continues / Yin + Yang only";
                }
            }
            if (!CanInteract && isActiveAndEnabled) CancelInteractions("Resource state unavailable");
            gestures.SetInputEnabled(CanInteract);
            RefreshHud();
        }
        private void OnGenerationResolved(ResourceRequestReply reply)
        {
            if (reply == null || attack.Snapshot == null || reply.sessionId != attack.Snapshot.sessionId
                || reply.roundId != attack.Snapshot.roundId || !reply.known) return;
            if (battle != null && !battle.CanAct) { RefreshHud(); return; }
            bool debug = reply.operation == (int)ResourceRequestKind.DebugCombined;
            action = reply.accepted ? (debug ? "DEV HIT ORB APPROVED" : "GENERATE APPROVED") : "REJECTED / " + reply.reason;
            detail = reply.accepted
                ? (debug ? "DEBUG_TEST_MODE / free Combined fixture / hit bonus +" + layout.Config.StaminaHitRecovery
                    : "Raw cost " + layout.Config.GenerateCost + " / drop Yin onto Yang")
                : "Host rejected request; inventory and generation cost unchanged";
            Debug.Log($"C6_T09_GENERATE_UI accepted={reply.accepted} duplicate={reply.duplicate} operation={reply.operation} reason={reply.reason} request={reply.requestId} round={reply.roundId}");
            RefreshHud();
        }

        private void OnRecoveryResolved(ResourceRecoveryResult result)
        {
            if (result == null || !result.Accepted || result.IsDuplicate || result.PlayerId != attack.LocalPlayerId) return;
            if (battle != null && !battle.CanAct) { RefreshHud(); return; }
            action = "VALID HIT / personal recovery";
            detail = "Actual collision / +" + result.Added.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                + " Stamina / max " + layout.Config.StaminaMax;
            RefreshHud();
        }

        private void OnStateChanged()
        {
            var state = attack.Snapshot;
            if (state == null || !attack.Connected)
            {
                ClearViews(); ClearProxies(); ClearThrowTrails(); pending.Clear(); pendingCombination = null; sequences.Clear(); displayedTransfers.Clear(); confirmedUnavailable.Clear();
                gestures = new OrbGestureEngine(); gestures.SetInputEnabled(false); sessionKey = null; round = 0; displayedDebugMode = null;
                RefreshHud(); observedHp = null; missWatches.Clear(); return;
            }
            if (sessionKey != state.sessionId || round != state.roundId)
            {
                CancelInteractions("Confirmed session/round changed");
                ClearViews(); pending.Clear(); pendingCombination = null; sequences.Clear(); displayedTransfers.Clear(); confirmedUnavailable.Clear(); gestures = new OrbGestureEngine();
                SentTransfers = ReceivedTransfers = 0;
                hud.SetNetworkFieldsVisible(!attack.Connected); Canvas.ForceUpdateCanvases();
                sessionKey = state.sessionId; round = state.roundId; displayedDebugMode = null;
                observedHp = null; missWatches.Clear(); // a new round's vanished orbs are neither hits nor misses
                action = "Round " + round + " / waiting for Host Start";
                detail = "Empty start / generate after Host starts the battle";
            }
            var available = state.orbs.Where(o => o.owner == attack.LocalPlayerId && o.state == (int)OrbAuthorityState.Idle && !confirmedUnavailable.Contains(o.id)).ToArray();
            var ids = new HashSet<string>(available.Select(o => o.id));
            foreach (string id in views.Keys.ToArray())
                if (!ids.Contains(id))
                {
                    // A confirmed Launching/Projectile/Consumed record, never a local timeout, removes the view.
                    RemoveView(id);
                }
            foreach (var orb in available)
            {
                bool arrived = transfersEnabled && orb.transferCount > 0 &&
                    (!displayedTransfers.TryGetValue(orb.id, out var lastTransfer) || orb.transferCount > lastTransfer);
                if (!views.ContainsKey(orb.id))
                {
                    var view = new GameObject("T09 Orb " + orb.id).AddComponent<OrbView>(); view.transform.SetParent(viewRoot, false);
                    view.Configure(orb.id, (OrbKind)orb.kind, (OrbPolarity)orb.polarity, LayerMask.NameToLayer("C6Orbs"), RadiusWorld,
                        orb.kind == (int)OrbKind.Combined ? "COMB" : orb.polarity == (int)OrbPolarity.Yin ? "YIN" : "YANG");
                    view.SetHeldFeedbackEnabled(true);
                    views.Add(orb.id, view);
                    PositionView(orb.id, ConfirmedToScreen(orb));
                    if (orbPhysicsEnabled)
                    {
                        ConfigurePhysicsGeometry();
                        orbPhysics.Register(view);
                    }
                }
                if (arrived)
                {
                    // Mark before CanAct: committing a deadline can re-enter this callback.
                    displayedTransfers[orb.id] = orb.transferCount;
                    ReceivedTransfers++;
                    // This also handles A -> B -> A between two received snapshots. A new
                    // ownership epoch cancels an old drag, but ordinary snapshots preserve it.
                    if (gestures.ActiveOrb?.OrbId == orb.id) { LogPointerCancellation(gestures.ActivePointerId.Value, orb.id, "AUTHORITATIVE_ORB_CHANGE"); gestures.CancelAllPointers(); }
                    PositionView(orb.id, ConfirmedToScreen(orb));
                    if (continuousTransfersEnabled && orb.hasTransferMotion)
                    {
                        // An intervening ownership epoch supersedes our earlier outgoing request.
                        // Its delayed receipt must never lock or rewind this newly returned orb.
                        foreach (var stale in pending.Where(p => p.Value.request.OrbId == orb.id &&
                            p.Value.request.TransferMotion.HasValue && p.Value.request.SequenceNumber < orb.sequence).Select(p => p.Key).ToArray())
                            pending.Remove(stale);
                        orbPhysics.SetLocked(orb.id, IsPending(orb.id));
                        bool canRoll = isActiveAndEnabled && battle != null && battle.CanAct;
                        orbPhysics.SetPaused(!canRoll);
                        var motion = OrbTransferMotion.Decay(new OrbTransferMotion(
                            new Vector2(orb.transferVelocityX, orb.transferVelocityY), orb.transferServerTime),
                            attack.MotionServerTime, layout.Config.OrbFloorDeceleration, layout.Config.OrbStopSpeed);
                        if (canRoll && !orbPhysics.ResumeTransferred(orb.id, orb.entrySide == (int)EntrySide.Left, orb.pos.y, motion.Velocity))
                            throw new InvalidOperationException("Approved ownership arrival could not resume its physical view.");
                        Debug.Log($"C6_P4_RESUME orb={orb.id} owner={orb.owner} count={orb.transferCount} entry={orb.entrySide} vx={motion.Velocity.x:R} vy={motion.Velocity.y:R} height={orb.pos.y:R} serverTime={motion.ServerTime:R} playing={canRoll}");
                    }
                    action = "RECEIVED / " + ((EntrySide)orb.entrySide).ToString().ToUpperInvariant() + " EDGE";
                    detail = continuousTransfersEnabled ? "Same orb / keeps rolling across screens" : "Same orb / grab it to combine, attack or pass again";
                    Debug.Log($"C6_T11_RECEIVED orb={orb.id} owner={orb.owner} kind={orb.kind} entry={orb.entrySide} height={orb.pos.y:R} count={orb.transferCount} sequence={orb.sequence} round={round}");
                }
                displayedTransfers[orb.id] = orb.transferCount;
            }
            if (!CanInteract && isActiveAndEnabled) CancelInteractions("Test phase " + state.state);
            gestures.SetInputEnabled(CanInteract);
            RefreshLocalStates(); UpdateProxies(); ShowObservedDamage(state); UpdateThrowTrails(); RefreshHud();
        }
        /// <summary>All usable lower space is available for free dragging and combination.</summary>
        public Rect OrbGridScreenRect
        {
            get
            {
                if (layout == null || hud == null) return Rect.zero;
                Rect space = hud.OrbWorkspaceScreenRect;
                float margin = 4f * (hud.Canvas != null ? hud.Canvas.scaleFactor : 1f);
                return new Rect(space.xMin + margin, space.yMin + margin,
                    Mathf.Max(1f, space.width - 2f * margin), Mathf.Max(1f, space.height - 2f * margin));
            }
        }
        private Vector2 GridToScreen(Vector2 slot)
        {
            Rect grid = OrbGridScreenRect;
            return grid.min + Vector2.Scale(new Vector2(Mathf.Clamp01(slot.x), Mathf.Clamp01(slot.y)), grid.size);
        }
        // Normalize transfer height over the actual center travel range. This includes the
        // caption/radius padding before normalization, so different aspect ratios preserve
        // relative height without a second, device-dependent vertical clamp after receipt.
        public Rect TransferScreenRect
        {
            get
            {
                Rect space = hud.OrbWorkspaceScreenRect;
                float bottom = Mathf.Max(RadiusPixels * 1.8f, RadiusPixels * 1.72f + LabelPixels * .5f + 2f);
                float minimum = space.yMin + bottom;
                float maximum = Mathf.Max(minimum, space.yMax - RadiusPixels);
                return new Rect(OrbGridScreenRect.xMin, minimum, OrbGridScreenRect.width, Mathf.Max(.001f, maximum - minimum));
            }
        }
        private Vector2 ConfirmedToScreen(OrbWire orb)
        {
            if (!transfersEnabled || orb.transferCount == 0) return GridToScreen(orb.pos);
            Rect area = TransferScreenRect;
            return area.min + Vector2.Scale(orb.pos, area.size);
        }
        public bool BeginPointer(int pointerId, Vector2 raw, bool overUi)
        {
            OrbRecord record = null;
            if (CanInteract && !overUi && layout.TryScreenToOrbPlane(raw, out var world))
            {
                Physics2D.SyncTransforms();
                OrbView view;
                if (orbPhysicsEnabled) { view = FindDisplayedPhysicsOrb(world); LogCatchProbe(world, view); }
                else
                {
                    var hit = Physics2D.OverlapPoint(world, 1 << LayerMask.NameToLayer("C6Orbs"));
                    view = hit == null ? null : hit.GetComponentInParent<OrbView>();
                }
                var wire = view == null ? null : attack.Snapshot.orbs.FirstOrDefault(o => o.id == view.OrbId);
                if (wire != null && wire.owner == attack.LocalPlayerId && !IsPending(wire.id)) record = wire.ToRecord();
            }
            gestures.SetInputEnabled(CanInteract);
            bool began = gestures.Begin(pointerId, record, raw, overUi, layout.BottomPixelRect, Screen.width, Tuning);
            if (began)
            {
                dragStart = GetViewScreenPosition(record.OrbId); grabOffset = dragStart - raw;
                if (orbPhysicsEnabled)
                {
                    orbPhysics.Grab(record.OrbId, Time.unscaledTimeAsDouble);
                    PositionView(record.OrbId, dragStart, true);
                }
                if (releaseThrowsEnabled) throwSampler.Begin(raw / Mathf.Max(1f, Screen.width), Time.unscaledTimeAsDouble);
                dragPointerStart = raw; dragStartedAt = Time.unscaledTime;
                dragIsTouch = input != null && input.IsActiveTouchPointer(pointerId);
                if (dragIsTouch) TouchBegins++;
                action = "Dragging " + record.Kind;
                detail = releaseThrowsEnabled && record.Kind == OrbKind.Combined ? "Swipe into battle and release to throw" : continuousTransfersEnabled ? "Drag Yin onto Yang / push sideways to roll across screens" : transfersEnabled ? "Drop on Yin + Yang / release at a side edge to pass"
                    : record.Kind == OrbKind.Raw ? "Drop onto the opposite Yin / Yang" : "Drag across the upper battle boundary to attack";
                Debug.Log($"C6_T09_POINTER_BEGIN source={(dragIsTouch ? "TOUCH" : pointerId == OrbPointerInput.MousePointerId ? "MOUSE" : "DEBUG")} pointer={pointerId} orb={record.OrbId} kind={record.Kind} rawX={raw.x:F3} rawY={raw.y:F3} centerX={dragStart.x:F3} centerY={dragStart.y:F3} screenWidth={Screen.width} round={round}");
            }
            RefreshLocalStates(); RefreshHud(); return began;
        }
        // #4 catch assist: a moving orb is easier to grab. The pick radius grows with speed
        // (1x at rest, OrbCatchRadiusScale at max release speed). Physics colliders are unchanged.
        private float CatchRadiusScale(string id)
        {
            float extra = layout.Config.OrbCatchRadiusScale - 1f;
            if (extra <= 0f || orbPhysics == null || physicsTuning.w <= 0f || !orbPhysics.TryGetVelocity(id, out var velocity)) return 1f;
            return 1f + extra * Mathf.Clamp01(velocity.magnitude / physicsTuning.w);
        }
        // #4 diagnostics: on every touch-down, log the nearest orb's distance, pick radius and speed.
        // HIT/MISS lines show whether fast orbs are missed by position (distance > radius) or by state.
        private void LogCatchProbe(Vector2 world, OrbView picked)
        {
            OrbView nearest = null; float best = float.PositiveInfinity;
            foreach (var pair in views)
            {
                if (pair.Value == null || !pair.Value.isActiveAndEnabled) continue;
                float d = ((Vector2)pair.Value.transform.position - world).sqrMagnitude;
                if (d < best) { best = d; nearest = pair.Value; }
            }
            if (nearest == null) return;
            float pixelsPerWorld = layout.BottomPixelRect.height / Mathf.Max(0.0001f, 2f * layout.OrbCamera.orthographicSize);
            float radius = nearest.Collider.radius * Mathf.Abs(nearest.transform.lossyScale.x);
            float speed = orbPhysics != null && orbPhysics.TryGetVelocity(nearest.OrbId, out var v) ? v.magnitude : 0f;
            float maxSpeed = physicsTuning.w;
            var wire = attack?.Snapshot?.orbs.FirstOrDefault(o => o.id == nearest.OrbId);
            string blocked = wire == null ? "NO_WIRE" : wire.owner != attack.LocalPlayerId ? "NOT_MINE"
                : wire.state != (int)OrbAuthorityState.Idle ? "STATE_" + wire.state : IsPending(nearest.OrbId) ? "PENDING"
                : orbPhysics != null && orbPhysics.TryGetPendingEdge(nearest.OrbId, out _) ? "EDGE_PENDING" : "OK";
            Debug.Log($"C6_CATCH_{(picked != null ? "HIT" : "MISS")} orb={nearest.OrbId} distPx={Mathf.Sqrt(best) * pixelsPerWorld:F0} " +
                $"basePx={radius * pixelsPerWorld:F0} pickPx={radius * CatchRadiusScale(nearest.OrbId) * pixelsPerWorld:F0} " +
                $"speedPx={speed * pixelsPerWorld:F0} speedRatio={(maxSpeed > 0 ? speed / maxSpeed : 0):F2} state={blocked}");
        }
        private OrbView FindDisplayedPhysicsOrb(Vector2 world)
        {
            // Interpolation renders one physics step behind the body. Picking the future collider
            // can miss a fast visible orb, so use its displayed circle with the same physical radius.
            OrbView nearest = null;
            float best = float.PositiveInfinity;
            foreach (var pair in views)
            {
                var view = pair.Value;
                if (!view.isActiveAndEnabled || !view.Collider.enabled || IsPending(pair.Key)) continue;
                float radius = view.Collider.radius * Mathf.Abs(view.transform.lossyScale.x) * CatchRadiusScale(pair.Key);
                float distance = ((Vector2)view.transform.position - world).sqrMagnitude;
                if (distance > radius * radius || distance > best) continue;
                if (distance == best && nearest != null && string.CompareOrdinal(pair.Key, nearest.OrbId) >= 0) continue;
                nearest = view; best = distance;
            }
            return nearest;
        }
        public void MovePointer(int pointerId, Vector2 raw)
        {
            if (!isActiveAndEnabled || !CanInteract) { CancelPointer(pointerId); return; }
            if (gestures.ActivePointerId != pointerId) return;
            string id = gestures.ActiveOrb.OrbId;
            var decision = gestures.Move(pointerId, raw, layout.BottomPixelRect, Screen.width);
            PositionView(id, raw + grabOffset, true);
            if (releaseThrowsEnabled)
            {
                throwSampler.Add(raw / Mathf.Max(1f, Screen.width), Time.unscaledTimeAsDouble);
                RefreshThrowPreview(raw);
            }
            if (decision.HasValue) SubmitDecision(decision.Value, dragIsTouch, pointerId, "MOVE");
        }
        public void EndPointer(int pointerId, Vector2 raw)
        {
            if (!isActiveAndEnabled || !CanInteract) { CancelPointer(pointerId); return; }
            string id = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            OrbRecord source = id == null ? null : gestures.ActiveOrb;
            bool touch = dragIsTouch;
            bool hadQualifiedAction = gestures.HasPending;
            if (id != null) PositionView(id, raw + grabOffset, true);
            Vector2 droppedCenter = id != null && views.ContainsKey(id) ? GetViewScreenPosition(id) : raw;
            // Choose the nearest displayed target before filtering polarity. A same-Yin drop must
            // not unexpectedly select a slightly farther Yang and become a successful combination.
            bool validDrop = FinitePoint(raw) && FinitePoint(droppedCenter) && layout.BottomPixelRect.Contains(raw);
            OrbDropTarget? nearest = source == null || !validDrop ? null : NearestDropTarget(source.OrbId, droppedCenter);
            IReadOnlyList<OrbDropTarget> targets = nearest.HasValue
                ? new[] { nearest.Value } : Array.Empty<OrbDropTarget>();
            OrbThrowInput? releaseInput = null;
            if (releaseThrowsEnabled && id != null)
            {
                bool sampled = throwSampler.TryRelease(raw / Mathf.Max(1f, Screen.width), Time.unscaledTimeAsDouble,
                    layout.Config.ThrowSampleWindow, layout.Config.ThrowMinDuration, out var sampledInput);
                if (sampled && raw.y >= layout.BottomPixelRect.yMax && raw.y <= Screen.height &&
                    ThrowMapping.TryCalculate(OrbGestureEngine.NormalizeClamped(raw, layout.BottomPixelRect), sampledInput,
                        CurrentThrowBasis, CurrentThrowTuning, out _, out _)) releaseInput = sampledInput;
                ClearThrowPreview();
            }
            var decision = gestures.Up(pointerId, raw, layout.BottomPixelRect, Screen.width, targets, droppedCenter,
                !releaseThrowsEnabled || releaseInput.HasValue);
            if (source != null)
                Debug.Log($"C6_T12_POINTER_END source={(touch ? "TOUCH" : pointerId == OrbPointerInput.MousePointerId ? "MOUSE" : "DEBUG")} pointer={pointerId} orb={source.OrbId} kind={source.Kind} startX={dragPointerStart.x:F3} startY={dragPointerStart.y:F3} rawX={raw.x:F3} rawY={raw.y:F3} deltaX={raw.x - dragPointerStart.x:F3} deltaY={raw.y - dragPointerStart.y:F3} heldSeconds={Time.unscaledTime - dragStartedAt:F3} reason={gestures.LastReleaseReason} requiredTravel={gestures.LastTransferRequiredTravel:F3} dropTarget={nearest?.Orb.OrbId ?? "none"} decision={decision?.Kind.ToString() ?? "none"} round={round}");
            bool submittedAction = decision.HasValue || !hadQualifiedAction && validDrop && source != null && nearest.HasValue;
            if (decision.HasValue) SubmitDecision(decision.Value, touch, pointerId, "UP", releaseInput);
            else if (!hadQualifiedAction && validDrop && source != null && nearest.HasValue)
                SubmitCombination(source.OrbId, nearest.Value.Orb.OrbId, touch);
            else if (!hadQualifiedAction && source != null && source.Kind == OrbKind.Raw
                && FinitePoint(raw) && raw.y >= layout.BottomPixelRect.yMax)
            {
                action = "RAW CANNOT ATTACK";
                detail = "First drop Yin onto Yang to create a Combined orb";
            }
            if (releaseThrowsEnabled && source != null && !submittedAction && !validDrop)
            {
                PositionView(id, dragStart);
                if (source.Kind == OrbKind.Combined)
                { action = "THROW CANCELLED"; detail = "Swipe upward and release while moving"; }
            }
            // Only a normal, action-free release on the board can carry local momentum.
            // A synchronous Host rejection is still an action, not a second physical throw.
            if (orbPhysicsEnabled && id != null)
                orbPhysics.Release(id, Time.unscaledTimeAsDouble, validDrop && !hadQualifiedAction && !submittedAction);
            RefreshLocalStates(); RefreshHud();
        }

        private static bool FinitePoint(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y);

        private OrbDropTarget? NearestDropTarget(string sourceId, Vector2 center)
        {
            OrbDropTarget? nearest = null;
            float best = float.PositiveInfinity;
            foreach (var wire in attack.Snapshot.orbs)
            {
                if (wire.id == sourceId || wire.owner != attack.LocalPlayerId
                    || wire.state != (int)OrbAuthorityState.Idle || !views.ContainsKey(wire.id) || IsPending(wire.id)) continue;
                Vector2 position = GetViewScreenPosition(wire.id);
                float squared = (position - center).sqrMagnitude;
                float distance = Mathf.Sqrt(squared) / Mathf.Max(1f, Screen.width);
                if (distance > layout.Config.CombinationRadiusFraction
                    && !Mathf.Approximately(distance, layout.Config.CombinationRadiusFraction)) continue;
                if (squared > best || nearest.HasValue && squared == best
                    && string.CompareOrdinal(wire.id, nearest.Value.Orb.OrbId) >= 0) continue;
                nearest = new OrbDropTarget(wire.ToRecord(), position);
                best = squared;
            }
            return nearest;
        }

        private void SubmitCombination(string sourceId, string targetId, bool fromTouch)
        {
            if (pendingCombination != null || combination == null || !combination.Connected
                || !views.ContainsKey(sourceId) || !views.ContainsKey(targetId))
            {
                gestures.ResolvePending(sourceId);
                action = "COMBINATION UNAVAILABLE"; detail = "Wait for the current Host confirmation";
                return;
            }
            ulong sourceSequence = LastSequence(sourceId);
            ulong targetSequence = LastSequence(targetId);
            ulong maximum = Math.Max(sourceSequence, targetSequence);
            if (maximum == ulong.MaxValue)
            {
                gestures.ResolvePending(sourceId);
                attack.FailUnconfirmedRequest(); return;
            }
            ulong sequence = maximum + 1;
            sequences[sourceId] = sequences[targetId] = sequence;
            var request = new CombinationRequest(sessionKey, round, Guid.NewGuid().ToString("N"),
                sourceId, targetId, sequence,
                OrbGestureEngine.NormalizeClamped(GetViewScreenPosition(sourceId), OrbGridScreenRect),
                OrbGestureEngine.NormalizeClamped(GetViewScreenPosition(targetId), OrbGridScreenRect));
            // Both IDs lock before a synchronous local Host callback can return.
            pendingCombination = new PendingCombination { request = request, touch = fromTouch, sentAt = Time.unscaledTime };
            RefreshLocalStates();
            action = "COMBINING / waiting for Host";
            detail = "Both materials locked / no resource cost";
            bool sent = combination.Submit(request);
            if (!sent && pendingCombination != null) detail = "Confirmation missing; both materials stay locked";
            RefreshLocalStates(); RefreshHud();
        }

        private ulong LastSequence(string id)
        {
            ulong local = sequences.TryGetValue(id, out var value) ? value : 0;
            var wire = attack.Snapshot?.orbs.FirstOrDefault(orb => orb.id == id);
            return Math.Max(local, wire?.sequence ?? 0);
        }

        private void OnCombinationChanged()
        {
            gestures.SetInputEnabled(CanInteract);
            RefreshLocalStates(); RefreshHud();
        }

        private void OnCombinationResolved(CombinationReply reply)
        {
            var held = pendingCombination;
            if (reply == null || held == null || reply.sessionId != sessionKey || reply.roundId != round
                || reply.requestId != held.request.RequestId || reply.sourceOrbId != held.request.SourceOrbId
                || reply.targetOrbId != held.request.TargetOrbId || reply.sequence != held.request.SequenceNumber
                || !reply.sourcePosition.Equals(held.request.SourcePosition)
                || !reply.targetPosition.Equals(held.request.TargetPosition)) return;
            if (!reply.known || reply.sourcePending || reply.targetPending || combination.HasPending)
            {
                detail = combination.AwaitingInventoryConfirmation ? "Host approved / waiting for confirmed inventory"
                    : reply.known ? "Host reports a material reservation; keep both locked" : "Host has no confirmed receipt; keep both locked";
                RefreshLocalStates(); RefreshHud(); return;
            }
            pendingCombination = null;
            gestures.ResolvePending(reply.sourceOrbId);
            if (reply.accepted)
            {
                confirmedUnavailable.Add(reply.sourceOrbId);
                confirmedUnavailable.Add(reply.targetOrbId);
                RemoveView(reply.sourceOrbId); RemoveView(reply.targetOrbId);
                action = "YIN + YANG / COMBINED";
                detail = "Two materials consumed / new Combined can attack";
                Debug.Log($"C6_T09_COMBINE_UI accepted=true request={reply.requestId} source={reply.sourceOrbId} target={reply.targetOrbId} combined={reply.originalCombined?.id ?? "none"} sourceInput={(held.touch ? "TOUCH" : "DEBUG_OR_MOUSE")} round={round}");
            }
            else
            {
                action = "COMBINATION REJECTED / " + reply.reason;
                detail = "Use two different Raw orbs: one Yin and one Yang";
                Debug.Log($"C6_T09_COMBINE_UI accepted=false request={reply.requestId} reason={reply.reason} round={round}");
            }
            ReconcileConfirmedOrb(reply.currentSource);
            ReconcileConfirmedOrb(reply.currentTarget);
            ReconcileConfirmedOrb(reply.currentCombined);
            // The receipt may arrive before its inventory snapshot. Tombstones above keep both
            // old Idle records unavailable; the new Combined is created only from confirmed inventory.
            OnStateChanged();
            RefreshLocalStates(); RefreshHud();
        }

        private void ReconcileConfirmedOrb(OrbWire orb)
        {
            if (orb == null || orb.owner != attack.LocalPlayerId) return;
            if (orb.state != (int)OrbAuthorityState.Idle)
            { confirmedUnavailable.Add(orb.id); RemoveView(orb.id); }
            else if (!confirmedUnavailable.Contains(orb.id) && views.ContainsKey(orb.id))
                PositionView(orb.id, ConfirmedToScreen(orb));
        }

        private void RemoveView(string id)
        {
            if (!views.TryGetValue(id, out var view)) return;
            if (orbPhysicsEnabled) orbPhysics.Remove(id);
            view.gameObject.SetActive(false); Destroy(view.gameObject); views.Remove(id); display.Remove(id);
        }

        private void SubmitDecision(OrbGestureDecision decision, bool fromTouch, int pointerId, string origin, OrbThrowInput? throwInput = null)
        {
            string sourceInput = fromTouch ? "TOUCH" : pointerId == OrbPointerInput.MousePointerId ? "MOUSE" : "DEBUG";
            Debug.Log($"C6_T09_DECISION kind={decision.Kind} sourceInput={sourceInput} origin={origin} pointer={pointerId} orb={decision.OrbId} rawX={decision.RawPosition.x:F2} rawY={decision.RawPosition.y:F2} screenWidth={Screen.width} gridWidth={OrbGridScreenRect.width:F2} swipeThreshold={Screen.width * layout.Config.HorizontalSwipeFraction:F2} round={round}");
            if (decision.Kind == OrbActionKind.Combine)
            { SubmitCombination(decision.OrbId, decision.OtherOrbId, fromTouch); return; }
            ulong old = LastSequence(decision.OrbId);
            if (old == ulong.MaxValue) { gestures.ResolvePending(decision.OrbId); attack.FailUnconfirmedRequest(); return; }
            ulong seq = old + 1;
            sequences[decision.OrbId] = seq;
            bool transfer = decision.Kind == OrbActionKind.TransferLeft || decision.Kind == OrbActionKind.TransferRight;
            // Transfer height is relative to usable orb space on each display, excluding HUD.
            // Keep the visible center, not a finger offset or a device-specific pixel height.
            Vector2 position = transfer ? OrbGestureEngine.NormalizeClamped(GetViewScreenPosition(decision.OrbId), TransferScreenRect)
                : decision.NormalizedPosition;
            var request = new OrbActionRequest(sessionKey, round, Guid.NewGuid().ToString("N"), decision.OrbId, decision.OtherOrbId,
                decision.Kind, seq, position, throwInput);
            pending[request.RequestId] = new PendingInput { request = request, touch = fromTouch, sentAt = Time.unscaledTime };
            RefreshLocalStates();
            action = transfer ? "PASSING " + (decision.Kind == OrbActionKind.TransferLeft ? "LEFT" : "RIGHT") + " / waiting for Host"
                : "Waiting for Host / " + decision.Kind;
            if (transfer) detail = "Orb locked until Host confirms ownership";
            bool sent = attack.Submit(request);
            if (!sent) { detail = "Host request not confirmed; querying authority"; }
            RefreshLocalStates(); RefreshHud();
        }
        private void OnPhysicsEdgeCrossed(OrbEdgeCrossing crossing)
        {
            string id = crossing.OrbId;
            // Resource generation can be pending independently; only this orb's transaction locks it.
            var orb = attack?.Snapshot?.orbs.FirstOrDefault(o => o.id == id);
            if (!continuousTransfersEnabled || !isActiveAndEnabled || attack == null || !attack.Connected ||
                battle == null || !battle.CanAct || orb == null || orb.owner != attack.LocalPlayerId ||
                orb.state != (int)OrbAuthorityState.Idle || IsPending(id) || gestures.ActiveOrb?.OrbId == id)
            { orbPhysics.ResolveRejectedEdge(id); return; }
            ulong previous = LastSequence(id);
            if (previous == ulong.MaxValue) { attack.FailUnconfirmedRequest(); return; }
            ulong sequence = previous + 1;
            sequences[id] = sequence;
            var motion = new OrbTransferMotion(crossing.VelocityBoardWidthsPerSecond, attack.MotionServerTime);
            var request = new OrbActionRequest(sessionKey, round, Guid.NewGuid().ToString("N"), id, null,
                crossing.ToRight ? OrbActionKind.TransferRight : OrbActionKind.TransferLeft,
                sequence, new Vector2(crossing.ToRight ? 1f : 0f, crossing.Height01), transferMotion: motion);
            pending[request.RequestId] = new PendingInput { request = request, touch = false, sentAt = Time.unscaledTime };
            Debug.Log($"C6_P4_EDGE_CAPTURE orb={id} request={request.RequestId} direction={request.Kind} vx={motion.Velocity.x:R} vy={motion.Velocity.y:R} height={crossing.Height01:R} source=PHYSICS serverTime={motion.ServerTime:R}");
            RefreshLocalStates();
            attack.Submit(request);
            RefreshLocalStates(); RefreshHud();
        }
        private void OnResolved(AttackRequestReply reply)
        {
            if (reply == null || reply.sessionId != sessionKey || reply.roundId != round ||
                !pending.TryGetValue(reply.requestId, out var inputRequest)) return;
            if (reply.orbId != inputRequest.request.OrbId) return;
            if (!reply.known || reply.pending) { detail = "Host has not confirmed this request; keep locked"; RefreshHud(); return; }
            pending.Remove(reply.requestId); gestures.ResolvePending(reply.orbId);
            bool transfer = inputRequest.request.Kind == OrbActionKind.TransferLeft || inputRequest.request.Kind == OrbActionKind.TransferRight;
            if (reply.accepted && !transfer)
            {
                // An authenticated approval is enough to remove 2D; a stale Idle snapshot cannot recreate it.
                confirmedUnavailable.Add(reply.orbId);
                RemoveView(reply.orbId);
            }
            if (reply.accepted && inputRequest.request.Kind == OrbActionKind.Launch && inputRequest.touch && touchReceipts.Add(reply.requestId))
            {
                TouchLaunches++;
                Debug.Log($"C6_T09_TOUCH_LAUNCH count={TouchLaunches} request={reply.requestId} orb={reply.orbId} round={round} owner={attack.LocalPlayerId}");
            }
            if (transfer)
            {
                if (reply.accepted) SentTransfers++;
                // A delayed receipt can follow a return transfer. Reconcile with the latest
                // aggregate; never tombstone an orb merely because it once left this player.
                OnStateChanged();
                Debug.Log($"C6_T11_TRANSFER_UI accepted={reply.accepted} request={reply.requestId} orb={reply.orbId} direction={inputRequest.request.Kind} reason={reply.reason} round={round}");
            }
            action = (reply.accepted ? transfer ? "PASS CONFIRMED" : "LAUNCH APPROVED" : transfer ? "PASS REJECTED" : "REJECTED") + " / " + reply.reason;
            detail = reply.accepted ? transfer ? "Same orb / ownership confirmed by Host" : "Same ID / 2D removed / actual Host physics"
                : "Host confirmed current state; no duplicate spawn";
            if (!reply.accepted && inputRequest.request.TransferMotion.HasValue)
                orbPhysics.ResolveRejectedEdge(reply.orbId);
            else if (!reply.accepted && reply.confirmedOrb != null && !reply.pending && views.ContainsKey(reply.orbId))
                PositionView(reply.orbId, ConfirmedToScreen(reply.confirmedOrb));
            RefreshLocalStates(); RefreshHud();
        }
        private bool IsPending(string id) => pending.Values.Any(p => p.request.OrbId == id)
            || pendingCombination != null && (pendingCombination.request.SourceOrbId == id || pendingCombination.request.TargetOrbId == id);
        public void CancelPointer(int pointerId)
        {
            string id = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            if (id != null) LogPointerCancellation(pointerId, id, "POINTER_CANCEL");
            gestures.Cancel(pointerId);
            if (id != null)
            {
                throwSampler.Clear(); ClearThrowPreview();
                if (releaseThrowsEnabled) { action = "DRAG CANCELLED"; detail = "Orb returned / swipe upward and release to throw"; }
            }
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            if (orbPhysicsEnabled && id != null) orbPhysics.Release(id, Time.unscaledTimeAsDouble, false);
            RefreshLocalStates(); RefreshHud();
        }
        public void CancelInteractions(string reason)
        {
            string id = gestures.ActiveOrb?.OrbId;
            if (id != null) LogPointerCancellation(gestures.ActivePointerId.Value, id, reason);
            gestures.CancelAllPointers();
            throwSampler.Clear(); ClearThrowPreview();
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            if (orbPhysicsEnabled && id != null) orbPhysics.Release(id, Time.unscaledTimeAsDouble, false);
            // Pending stays locked until a host receipt/new round. Cancelling a touch does not respawn.
            RefreshLocalStates();
        }
        private void LogPointerCancellation(int pointerId, string orbId, string reason)
        {
            Debug.Log($"C6_T12_POINTER_CANCEL source={(dragIsTouch ? "TOUCH" : pointerId == OrbPointerInput.MousePointerId ? "MOUSE" : "DEBUG")} pointer={pointerId} orb={orbId} startX={dragPointerStart.x:F3} startY={dragPointerStart.y:F3} lastX={gestures.LastRawPosition.x:F3} lastY={gestures.LastRawPosition.y:F3} heldSeconds={Time.unscaledTime - dragStartedAt:F3} reason={(reason ?? "UNSPECIFIED").Replace(' ', '_')} round={round}");
        }
        private void Update()
        {
            if (releaseThrowsEnabled && gestures.HasActivePointer)
                throwSampler.Add(gestures.LastRawPosition / Mathf.Max(1f, Screen.width), Time.unscaledTimeAsDouble);
            WatchOwnFlightsForMiss();
            var held = pendingCombination;
            if (held != null)
            {
                float elapsed = Time.unscaledTime - held.sentAt;
                if (elapsed > 3f && !held.queried)
                { held.queried = true; combination.QueryPending(held.request); }
                if (elapsed > 8f && pendingCombination != null)
                {
                    action = "NETWORK ERROR / unconfirmed combination";
                    detail = "Session ends with both materials locked; no replacement spawn";
                    attack.FailUnconfirmedRequest(); pendingCombination = null;
                }
            }
            foreach (var item in pending.Values.ToArray())
            {
                float elapsed = Time.unscaledTime - item.sentAt;
                if (elapsed > 3f && !item.queried) { item.queried = true; attack.QueryPending(item.request); }
                if (elapsed > 8f)
                {
                    action = "NETWORK ERROR / unconfirmed request";
                    detail = "Stop session; never duplicate or relaunch on timeout";
                    attack.FailUnconfirmedRequest(); pending.Clear(); break;
                }
            }
        }
        private void LateUpdate()
        {
            if (hud == null || hud.Canvas == null) return;
            var tuning = new Vector4(layout.Config.HorizontalSwipeFraction, layout.Config.HorizontalDominance,
                layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction);
            if (previousSpace != hud.OrbWorkspaceScreenRect || previousLower != layout.BottomPixelRect || previousTuning != tuning || previousRadius != RadiusPixels)
            {
                CancelInteractions("Geometry changed"); previousSpace = hud.OrbWorkspaceScreenRect;
                previousLower = layout.BottomPixelRect; previousTuning = tuning; previousRadius = RadiusPixels;
                if (orbPhysicsEnabled) ConfigurePhysicsGeometry();
                foreach (string id in views.Keys.ToArray()) PositionView(id, Denormalize(display[id]));
            }
            if (orbPhysicsEnabled)
            {
                ConfigurePhysicsGeometry();
                foreach (var pair in views)
                    display[pair.Key] = OrbGestureEngine.NormalizeClamped(GetViewScreenPosition(pair.Key), layout.BottomPixelRect);
            }
        }
        private void UpdateProxies()
        {
            if (attack.IsHost || attack.Snapshot == null) { ClearProxies(); return; }
            var ids = new HashSet<string>(attack.Snapshot.projectiles.Select(p => p.id));
            foreach (var id in proxies.Keys.ToArray()) if (!ids.Contains(id))
            {
                if (proxies[id] != null) removedProxyPosition = proxies[id].transform.position;
                if (ownProxyIds.Remove(id)) removedOwnIds.Add(id);
                Destroy(proxies[id]); proxies.Remove(id);
            }
            foreach (var wire in attack.Snapshot.projectiles)
            {
                if (!proxies.TryGetValue(wire.id, out var proxy))
                {
                    proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere); proxy.name = "Display only " + wire.id;
                    proxy.transform.SetParent(proxyRoot, false); proxy.layer = LayerMask.NameToLayer("C6Battle");
                    var collider = proxy.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    proxy.GetComponent<Renderer>().sharedMaterial = projectileMaterial; proxies.Add(wire.id, proxy);
                    if (wire.owner == attack.LocalPlayerId) ownProxyIds.Add(wire.id);
                    if (releaseThrowsEnabled) proxy.AddComponent<BallisticProjectileDisplay>();
                }
                var moving = proxy.GetComponent<BallisticProjectileDisplay>();
                if (moving != null && wire.ballistic) moving.Accept(wire); else proxy.transform.position = wire.position;
                proxy.transform.localScale = Vector3.one * wire.radius * 2f;
            }
        }
        public Vector2 GetViewScreenPosition(string id) => layout.OrbCamera.WorldToScreenPoint(views[id].transform.position);
        private float RadiusPixels => Mathf.Max(1f, Mathf.Min(Screen.width * layout.Config.OrbRadiusScreenFraction,
            OrbGridScreenRect.width / 5f * .35f * layout.Config.OrbRadiusCapScale,
            OrbGridScreenRect.height / 4f * .28f * layout.Config.OrbRadiusCapScale));
        private float LabelPixels => Mathf.Max(1f, Mathf.Min(12f * Mathf.Max(.1f, hud.Canvas.scaleFactor), OrbGridScreenRect.height / 4f * .25f));
        private float RadiusWorld => 2f * layout.OrbCamera.orthographicSize * RadiusPixels / Mathf.Max(1f, layout.BottomPixelRect.height);
        private GestureTuning Tuning => new GestureTuning(layout.Config.HorizontalSwipeFraction, layout.Config.HorizontalDominance,
            layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction,
            launchAtBattleBoundary: true, allowHorizontalTransfer: transfersEnabled && !continuousTransfersEnabled,
            transferOnEdgeRelease: transfersEnabled && !continuousTransfersEnabled, transferEdgeFraction: layout.Config.OrbRadiusScreenFraction,
            reachableEdgeTransferDistance: reachableEdgeTransferDistance, launchOnRelease: releaseThrowsEnabled);
        private Vector2 Denormalize(Vector2 n) => layout.BottomPixelRect.min + Vector2.Scale(n, layout.BottomPixelRect.size);
        private void PositionView(string id, Vector2 point, bool directDragSample = false)
        {
            if (!views.TryGetValue(id, out var view) || float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y)) return;
            view.transform.localScale = Vector3.one * RadiusWorld / Mathf.Max(.0001f, view.Collider.radius);
            view.SetLabelPixelHeight(layout.OrbCamera, LabelPixels);
            Rect space = hud.OrbWorkspaceScreenRect;
            float x = Mathf.Max(RadiusPixels, view.GetLabelHalfWidthPixels(layout.OrbCamera) + 2f);
            point.x = Mathf.Clamp(point.x, space.xMin + x, Mathf.Max(space.xMin + x, space.xMax - x));
            // Reserve the held caption's full height even while idle so a grab cannot jump.
            float bottomPadding = Mathf.Max(RadiusPixels * 1.8f, RadiusPixels * 1.72f + LabelPixels * .5f + 2f);
            point.y = Mathf.Clamp(point.y, space.yMin + bottomPadding, Mathf.Max(space.yMin + bottomPadding, space.yMax - RadiusPixels));
            if (orbPhysicsEnabled && physicsScreenBounds.width > 0f)
            {
                point.x = Mathf.Clamp(point.x, physicsScreenBounds.xMin, physicsScreenBounds.xMax);
                point.y = Mathf.Clamp(point.y, physicsScreenBounds.yMin, physicsScreenBounds.yMax);
            }
            if (layout.TryScreenToOrbPlane(point, out var world))
            {
                view.transform.position = world;
                if (orbPhysicsEnabled && orbPhysics != null)
                    orbPhysics.SetPosition(id, world, directDragSample, Time.unscaledTimeAsDouble);
                display[id] = OrbGestureEngine.NormalizeClamped(point, layout.BottomPixelRect);
            }
        }
        private ThrowTuning CurrentThrowTuning => new ThrowTuning(layout.Config.ThrowMinUpSpeed, layout.Config.ThrowMaxInputSpeed,
            layout.Config.ThrowForwardGain, layout.Config.ThrowUpGain, layout.Config.ThrowLateralGain,
            layout.Config.ThrowMaxWorldSpeed, layout.Config.ThrowGravity, layout.Config.ThrowBounce,
            layout.Config.ThrowLifetime, layout.Config.ProjectileRadius, layout.Config.ThrowSampleWindow, layout.Config.ThrowMinDuration);

        // The local release check must use the same participant frame the Host applies to this seat.
        private ProjectileLaunchBasis CurrentThrowBasis
        {
            get
            {
                int participantCount = attack?.OrderedParticipantIds.Count ?? 0;
                return approvedPlayerNumber < 1 || participantCount < 2 || approvedPlayerNumber > participantCount
                    ? launchFrame.Basis : ParticipantLaunchFrame.Calculate(launchFrame.Basis, approvedPlayerNumber, participantCount);
            }
        }
        private void RefreshThrowPreview(Vector2 raw)
        {
            if (!ThrowArmed)
            {
                bool wasVisible = throwPreview != null && throwPreview.Visible;
                ClearThrowPreview();
                if (wasVisible)
                { action = "Dragging Combined"; detail = "Move below to place or pass / swipe upward and release to throw"; RefreshHud(); }
                return;
            }
            if (throwPreview == null) throwPreview = gameObject.AddComponent<ThrowHeldPreview>();
            if (views.TryGetValue(gestures.ActiveOrb.OrbId, out var view))
                throwPreview.Show(hud.Canvas, view, raw, RadiusPixels);
            action = "THROW READY"; detail = "Swipe upward and release / bring back down to cancel";
            RefreshHud();
        }
        private void ClearThrowPreview() { if (throwPreview != null) throwPreview.Hide(); }
        private void RefreshLocalStates()
        {
            foreach (var pair in views)
            {
                bool locked = IsPending(pair.Key);
                pair.Value.SetLocalState(locked ? LocalOrbState.Pending :
                    gestures.ActiveOrb?.OrbId == pair.Key ? LocalOrbState.Dragging : LocalOrbState.Idle);
                if (orbPhysicsEnabled) orbPhysics.SetLocked(pair.Key, locked);
            }
            if (orbPhysicsEnabled) orbPhysics.SetPaused(!isActiveAndEnabled || battle == null || !battle.CanAct);
        }

        private void EnsureOrbPhysics()
        {
            if (orbPhysics == null)
            {
                orbPhysics = gameObject.AddComponent<LocalOrbPhysicsBoard>();
                orbPhysics.EdgeCrossed += OnPhysicsEdgeCrossed;
            }
            orbPhysics.SetPaused(battle == null || !battle.CanAct);
        }

        private void ConfigurePhysicsGeometry()
        {
            if (!orbPhysicsEnabled || hud == null || hud.Canvas == null || layout.OrbCamera == null) return;
            EnsureOrbPhysics();
            Rect space = hud.OrbWorkspaceScreenRect;
            float xPadding = RadiusPixels;
            foreach (var view in views.Values)
            {
                view.transform.localScale = Vector3.one * RadiusWorld / Mathf.Max(.0001f, view.Collider.radius);
                view.SetLabelPixelHeight(layout.OrbCamera, LabelPixels);
                xPadding = Mathf.Max(xPadding, view.GetLabelHalfWidthPixels(layout.OrbCamera) + 2f);
            }
            float bottom = Mathf.Max(RadiusPixels * 1.8f, RadiusPixels * 1.72f + LabelPixels * .5f + 2f);
            var min = new Vector2(space.xMin + xPadding, space.yMin + bottom);
            var max = new Vector2(Mathf.Max(min.x + .001f, space.xMax - xPadding),
                Mathf.Max(min.y + .001f, space.yMax - RadiusPixels));
            if (!layout.TryScreenToOrbPlane(min, out var a) || !layout.TryScreenToOrbPlane(max, out var b)) return;
            var bounds = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            // Both axes use the same world-units-per-pixel scale; tall screens never turn circles into ellipses.
            float boardWidth = space.width * 2f * layout.OrbCamera.orthographicSize / Mathf.Max(1f, layout.BottomPixelRect.height);
            orbPhysics.ConfigureHorizontalPassage(continuousTransfersEnabled, boardWidth);
            var c = layout.Config;
            var values = new Vector4(c.OrbRestitution, c.OrbFloorDeceleration * boardWidth,
                c.OrbStopSpeed * boardWidth, c.OrbMaxReleaseSpeed * boardWidth);
            var sample = new Vector2(c.OrbReleaseSampleWindow, c.OrbContactFriction);
            physicsScreenBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (bounds == physicsWorldBounds && physicsRadius == RadiusWorld && values == physicsTuning && sample == physicsSampleTuning) return;
            if (gestures.HasActivePointer) CancelInteractions("Physical geometry or tuning changed");
            physicsWorldBounds = bounds; physicsRadius = RadiusWorld; physicsTuning = values; physicsSampleTuning = sample;
            orbPhysics.Configure(bounds, RadiusWorld, new OrbPhysicsTuning(values.x, values.y, values.z, values.w, sample.x, sample.y));
        }
        private void OnBattleChanged()
        {
            string phase = battle?.Snapshot?.phase ?? "Boot";
            uint battleRound = battle?.Snapshot?.roundId ?? 0;
            if (phase != displayedBattlePhase || battleRound != displayedBattleRound)
            {
                ClearLocalInput("Battle phase / round changed");
                displayedBattlePhase = phase; displayedBattleRound = battleRound;
                action = phase == "Playing" ? "GENERATE / COMBINE / ATTACK" : phase.ToUpperInvariant();
                detail = phase == "Playing" ? continuousTransfersEnabled ? "Roll sideways to pass / drag to combine / swipe COMB up to throw" : releaseThrowsEnabled ? "Combine below / swipe COMB upward and release to throw" : transfersEnabled ? "Combine anywhere / COMB up to attack / release at sides to pass"
                    : "Combine Yin + Yang anywhere below / drag COMB into battle"
                    : phase == "Ready" ? "Host can START / every battle begins empty with 100 Stamina"
                    : phase == "Lobby" ? "Connect two players, or choose DEV SOLO before hosting"
                    : phase == "Victory" || phase == "Defeat" ? "Result confirmed by Host / actions and recovery stopped"
                    : battle?.Status ?? "Create a Host or join its IP";
            }
            if (battle != null && !battle.CanAct) CancelInteractions("Battle is not Playing");
            gestures.SetInputEnabled(CanInteract);
            RefreshLocalStates(); RefreshHud();
        }
        private void ClearLocalInput(string reason)
        {
            CancelInteractions(reason);
            pending.Clear(); pendingCombination = null;
            gestures = new OrbGestureEngine(); gestures.SetInputEnabled(false);
            RefreshLocalStates();
        }
        private void RefreshHud()
        {
            if (hud == null || hud.Canvas == null || attack == null || connection == null) return;
            // CanAct can commit a deadline result synchronously; render the resulting snapshots.
            bool canAct = battle != null && battle.CanAct;
            var s = attack.Snapshot;
            var state = battle?.Snapshot;
            string phase = state?.phase ?? "Boot";
            bool terminal = phase == "Victory" || phase == "Defeat";
            bool host = battle != null && battle.IsHost;
            bool connected = battle != null && battle.Connected;
            bool canStart = battle != null && battle.CanStart;
            hud.SetNetworkFieldsVisible(!attack.Connected);
            hud.SetStatus(attack.Connected ? ((attack.IsHost ? "HOST / P" : "CLIENT / P") + (approvedPlayerNumber > 0 ? approvedPlayerNumber : attack.IsHost ? 1 : 2)) : connection.State.ToString().ToUpperInvariant(), action,
                connection.State == DirectConnectionState.Failed ? connection.Message : detail);
            hud.SetControls(connection.CanStart, connection.CanStart, canStart, !connection.CanStart,
                canAct && resource != null && resource.CanGenerate && pendingCombination == null,
                canAct && resource != null && resource.IsHost && resource.Connected && !resource.HasPending
                    && pendingCombination == null && layout.Config.ResourceDebugToolsEnabled && (Application.isEditor || Debug.isDebugBuild),
                connection.CanStart && (Application.isEditor || Debug.isDebugBuild));
            hud.SetProgress(state?.observedMonsterHp ?? s?.hp ?? layout.Config.MonsterMaxHp,
                s != null ? s.maxHp : layout.Config.MonsterMaxHp, s?.totalHits ?? 0, state?.roundId ?? s?.roundId ?? 0, s?.resets ?? 0);
            var resources = resource?.Snapshot;
            var player = resource?.LocalPlayer;
            if (player != null) lastConfirmedStamina = player.stamina;
            else if (!terminal && phase != "NetworkError") lastConfirmedStamina = layout.Config.StaminaStart;
            hud.SetResources(lastConfirmedStamina,
                resources?.maximum ?? layout.Config.StaminaMax, resources?.generateCost ?? layout.Config.GenerateCost,
                canAct && resources != null && resources.playing ? resources.regenerationRate : 0d,
                player?.storedOrbs ?? 0, resources?.storageLimit ?? layout.Config.OrbStorageLimit,
                resource != null && resource.HasPending, resource != null && resource.DebugTestMode);
            hud.SetBattle(phase, state?.remaining ?? 0, state?.teamHp ?? 0, state?.duration ?? layout.Config.BattleDurationSeconds,
                state?.participants ?? 0, state?.developmentSolo ?? developmentSolo, state?.shortDuration ?? false,
                connection.CanStart, state?.observedMonsterHp ?? s?.hp ?? layout.Config.MonsterMaxHp,
                lastConfirmedStamina, host && connected);
            Changed?.Invoke();
        }
        private void ClearViews()
        {
            if (orbPhysicsEnabled && orbPhysics != null) orbPhysics.Clear();
            foreach (var view in views.Values) { if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); } }
            views.Clear(); display.Clear();
        }
        private void ClearProxies() { foreach (var proxy in proxies.Values) if (proxy != null) Destroy(proxy); proxies.Clear(); ownProxyIds.Clear(); }
        // Only the thrower sees the flight trail. It follows the view already on this screen:
        // the Host's physics body or a client's display proxy. Trails fade and destroy themselves.
        private void UpdateThrowTrails()
        {
            if (!releaseThrowsEnabled || attack.Snapshot == null) return;
            foreach (var wire in attack.Snapshot.projectiles)
            {
                if (wire.owner != attack.LocalPlayerId || throwTrails.ContainsKey(wire.id)) continue;
                Transform followed = attack.IsHost ? attack.ProjectileTransform(wire.id)
                    : proxies.TryGetValue(wire.id, out var proxy) && proxy != null ? proxy.transform : null;
                if (followed != null) throwTrails.Add(wire.id, ThrowFlightTrail.Create(transform, followed, wire, trailMaterial));
            }
            foreach (var id in throwTrails.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray()) throwTrails.Remove(id);
        }
        private void ClearThrowTrails() { foreach (var trail in throwTrails.Values) if (trail != null) Destroy(trail.gameObject); throwTrails.Clear(); }

        // Host: exact contact from its own physics. Clients: an HP drop in the snapshot, at the orb view
        // that disappeared in the same update. Display only; HP and damage stay Host-authoritative.
        private void OnHostHitAt(AttackHitResult hit, Vector3 position)
        {
            missWatches.Remove(hit.OrbId);
            if (damagePopups != null) damagePopups.Show(hit.HpBefore - hit.HpAfter, position, layout.BattleCamera);
        }
        private void ShowObservedDamage(AttackSnapshot state)
        {
            int? previous = observedHp; observedHp = state.hp;
            Vector3? removed = removedProxyPosition; removedProxyPosition = null;
            var ownRemoved = removedOwnIds.ToArray(); removedOwnIds.Clear();
            if (attack.IsHost || damagePopups == null) return;
            bool missed = ObservedMisses(previous, state.hp, state.state == AttackBattleState.Playing.ToString(), ownRemoved.Length) > 0;
            // An HP drop cannot be attributed to one orb, so none of the vanished own orbs becomes a MISS.
            foreach (var id in ownRemoved) { if (missed) ShowMissOnce(id); missWatches.Remove(id); }
            int damage = ObservedDamage(previous, state.hp);
            if (damage > 0) damagePopups.Show(damage, removed ?? MonsterFallbackPoint, layout.BattleCamera);
        }
        public static int ObservedDamage(int? previousHp, int hp) =>
            previousHp.HasValue && hp < previousHp.Value ? previousHp.Value - hp : 0;
        // #20 Host: only its own orb that expired while the battle can still act. Round end and
        // target clear cancel flights without an outcome, so they never reach this handler.
        private void OnHostProjectileMissed(ProjectileOutcome outcome)
        {
            if (outcome.AttackerPlayerId == attack.LocalPlayerId && battle != null && battle.CanAct) ShowMissOnce(outcome.OrbId);
            missWatches.Remove(outcome.OrbId);
        }
        // #20 Clients: own orbs vanished in a Playing snapshot without an HP drop. An HP drop in the same
        // snapshot cannot be attributed to a specific orb, so it never produces a MISS.
        public static int ObservedMisses(int? previousHp, int hp, bool playing, int removedOwnProjectiles) =>
            previousHp.HasValue && playing && hp >= previousHp.Value ? removedOwnProjectiles : 0;
        // #20 A flight has passed the monster once it is clearly farther than its closest approach.
        public static bool PassedTarget(float closestDistance, float currentDistance, float margin) =>
            closestDistance > 0f && !float.IsInfinity(closestDistance) && currentDistance >= closestDistance + margin;

        private Collider MonsterHitbox => target != null ? target.GetComponentInChildren<Collider>() : null;
        private Vector3 MonsterFallbackPoint
        {
            get { var hitbox = MonsterHitbox; return hitbox != null ? hitbox.bounds.center : Vector3.zero; }
        }
        // Display only, for the local thrower: follows the flight already on this screen (Host physics body
        // or client proxy). Hit/miss results stay Host-authoritative; a rare bounce-back hit after a MISS
        // still shows its damage.
        private void WatchOwnFlightsForMiss()
        {
            var hitbox = MonsterHitbox;
            if (!releaseThrowsEnabled || damagePopups == null || hitbox == null || attack == null || attack.Snapshot == null
                || battle == null || !battle.CanAct) return;
            foreach (var wire in attack.Snapshot.projectiles)
            {
                if (wire.owner != attack.LocalPlayerId) continue;
                Transform flight = attack.IsHost ? attack.ProjectileTransform(wire.id)
                    : proxies.TryGetValue(wire.id, out var proxy) && proxy != null ? proxy.transform : null;
                if (flight == null) continue;
                if (!missWatches.TryGetValue(wire.id, out var watch)) missWatches.Add(wire.id, watch = new MissWatch());
                if (watch.shown) continue;
                Vector3 surface = hitbox.ClosestPoint(flight.position);
                float distance = Vector3.Distance(surface, flight.position);
                if (distance < watch.closest) { watch.closest = distance; watch.surface = surface; }
                else if (PassedTarget(watch.closest, distance, MissPassMargin)) ShowMissOnce(wire.id);
            }
        }
        private void ShowMissOnce(string orbId)
        {
            if (damagePopups == null) return;
            if (!missWatches.TryGetValue(orbId, out var watch)) missWatches.Add(orbId, watch = new MissWatch());
            if (watch.shown) return;
            watch.shown = true;
            damagePopups.ShowMiss(float.IsInfinity(watch.closest) ? MonsterFallbackPoint : watch.surface, layout.BattleCamera);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteractions("Focus lost"); }
        private void OnApplicationPause(bool paused) { if (paused) CancelInteractions("Paused"); }
        private void OnDisable()
        {
            string id = gestures.ActiveOrb?.OrbId;
            if (id != null) LogPointerCancellation(gestures.ActivePointerId.Value, id, "COMPONENT_DISABLED");
            gestures.SetInputEnabled(false);
            throwSampler.Clear(); ClearThrowPreview();
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            if (orbPhysicsEnabled && id != null) orbPhysics.Release(id, Time.unscaledTimeAsDouble, false);
            RefreshLocalStates();
            // This component owns pending confirmation deadlines; disabling it must not orphan them.
            if (attack != null && attack.Connected && (pendingCombination != null || pending.Count > 0))
                attack.FailUnconfirmedRequest();
        }
        private void OnDestroy()
        {
            if (orbPhysics != null) orbPhysics.EdgeCrossed -= OnPhysicsEdgeCrossed;
            if (connection != null) connection.Changed -= RefreshHud;
            if (attack != null) { attack.Changed -= OnStateChanged; attack.RequestResolved -= OnResolved; attack.ValidHitAt -= OnHostHitAt; attack.ProjectileMissed -= OnHostProjectileMissed; }
            if (resource != null) { resource.Changed -= OnResourceChanged; resource.GenerationResolved -= OnGenerationResolved; resource.RecoveryResolved -= OnRecoveryResolved; }
            if (combination != null) { combination.Changed -= OnCombinationChanged; combination.RequestResolved -= OnCombinationResolved; }
            if (battle != null) battle.Changed -= OnBattleChanged;
            ClearThrowPreview();
            if (projectileMaterial != null) Destroy(projectileMaterial);
            if (trailMaterial != null) Destroy(trailMaterial);
            OrbView.SetArtwork(null);
            Changed = null;
        }
        private sealed class PendingInput
        { public OrbActionRequest request; public bool touch, queried; public float sentAt; }
        private sealed class PendingCombination
        { public CombinationRequest request; public bool touch, queried; public float sentAt; }
    }
}
