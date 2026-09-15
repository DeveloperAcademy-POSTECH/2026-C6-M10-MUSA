using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using C6.Prototype.Networking;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout), typeof(DirectConnectionSession), typeof(T05Hud))]
    public sealed class T05OrbController : MonoBehaviour, IOrbPointerSink
    {
        [SerializeField] private SplitScreenLayout layout;
        [SerializeField] private DirectConnectionSession session;
        [SerializeField] private T05Hud hud;
        [SerializeField] private Material spriteMaterial;
        private readonly HostOrbRegistry registry = new HostOrbRegistry(true);
        private readonly Dictionary<string, OrbView> views = new Dictionary<string, OrbView>();
        private readonly Dictionary<string, Vector2> displayPositions = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, ulong> sequences = new Dictionary<string, ulong>();
        private readonly List<OrbDropTarget> dropTargets = new List<OrbDropTarget>();
        private ReadOnlyDictionary<string, OrbView> viewReader;
        private OrbGestureEngine gestures = new OrbGestureEngine();
        private Transform viewRoot;
        private bool developmentRequested;
        private bool interactionEnabled = true;
        private bool wired;
        private Rect previousLower;
        private Rect previousWorkspace;
        private float baseRadius;
        private Vector4 previousTuning;
        private float previousRadiusFraction;
        private Vector2 grabOffset;
        private Vector2 dragStartScreen;

        public SplitScreenLayout Layout => layout;
        public DirectConnectionSession Session => session;
        public T05Hud Hud => hud;
        public HostOrbRegistry Registry => registry;
        public OrbGestureEngine Gestures => gestures;
        public IReadOnlyDictionary<string, OrbView> Views => viewReader ??= new ReadOnlyDictionary<string, OrbView>(views);
        public bool IsHostReady => developmentRequested && session != null &&
            session.State == DirectConnectionState.Connected && session.OwnedManager != null &&
            session.OwnedManager.IsHost && registry.HasSession;
        public int RequestCount { get; private set; }
        public int AcceptedReservations { get; private set; }
        public string LastAction { get; private set; } = "Start DEV HOST to load 3 test orbs";
        public string LastReason { get; private set; } = "Reservations only / no gameplay transitions";
        public OrbGestureDecision? LatestDecision { get; private set; }
        public OrbReservationResult LatestReservationResult { get; private set; }
        public bool IsDefenseHeld => false;
        public event Action Changed;

        public void Configure(SplitScreenLayout split, DirectConnectionSession connection, T05Hud overlay, Material material)
        {
            layout = split; session = connection; hud = overlay; spriteMaterial = material;
        }

        private void Awake()
        {
            if (layout == null) layout = GetComponent<SplitScreenLayout>();
            if (session == null) session = GetComponent<DirectConnectionSession>();
            if (hud == null) hud = GetComponent<T05Hud>();
        }

        private void OnEnable()
        {
            if (session != null) session.Changed += OnSessionChanged;
        }

        private void Start()
        {
            hud.HostButton.onClick.AddListener(OnHostButton);
            hud.ResetButton.onClick.AddListener(ResetDevelopmentFixture);
            hud.EndButton.onClick.AddListener(EndDevelopmentTest);
            wired = true;
            UpdateHud();
        }

        private void OnHostButton() => StartDevelopmentHost();

        public void StartDevelopmentHost(string portText = null)
        {
            if (!isActiveAndEnabled || !(Application.isEditor || Debug.isDebugBuild) || session == null || !session.CanStart) return;
            developmentRequested = true;
            interactionEnabled = true;
            LastAction = "Starting explicit development Host";
            session.StartHost(portText ?? DirectConnectionSession.DefaultPort.ToString());
            OnSessionChanged();
        }

        private void OnSessionChanged()
        {
            bool serverReady = developmentRequested && session.State == DirectConnectionState.Connected &&
                session.OwnedManager != null && session.OwnedManager.IsHost;
            if (serverReady && !registry.HasSession)
            {
                registry.BeginSession(Guid.NewGuid().ToString("N"), 1);
                SupplyFixture();
            }
            else if (!serverReady && registry.HasSession)
            {
                // Ending the entire development session invalidates its pending requests.
                CancelInteractions("Development session ended");
                registry.ClearSession();
                gestures = new OrbGestureEngine();
                ClearViews();
            }
            gestures.SetInputEnabled(isActiveAndEnabled && IsHostReady && interactionEnabled);
            UpdateHud();
        }

        public void ResetDevelopmentFixture()
        {
            if (!isActiveAndEnabled || !IsHostReady) return;
            CancelInteractions("Confirmed development round reset");
            registry.ResetRound(checked(registry.RoundId + 1));
            gestures = new OrbGestureEngine();
            interactionEnabled = true;
            SupplyFixture();
        }

        private void SupplyFixture()
        {
            if (session.OwnedManager == null || !session.OwnedManager.IsHost)
                throw new InvalidOperationException("Development fixtures require the real NGO Host.");
            ClearViews();
            sequences.Clear();
            RequestCount = AcceptedReservations = 0;
            LatestDecision = null;
            LatestReservationResult = null;
            ulong owner = session.OwnedManager.LocalClientId;
            registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yin, new Vector2(.30f, .54f));
            registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, OrbPolarity.Yang, new Vector2(.46f, .54f));
            registry.RegisterDevelopmentOrb(owner, OrbKind.Combined, OrbPolarity.None, new Vector2(.73f, .54f));
            OrbView.SetSharedMaterial(spriteMaterial);
            viewRoot = new GameObject("T05 Host-registered Fixture Views").transform;
            viewRoot.SetParent(transform, false);
            baseRadius = RadiusWorld;
            foreach (var orb in registry.Snapshot())
            {
                var view = new GameObject("Orb " + orb.OrbId).AddComponent<OrbView>();
                view.transform.SetParent(viewRoot, false);
                view.Configure(orb.OrbId, orb.Kind, orb.Polarity, LayerMask.NameToLayer("C6Orbs"), baseRadius);
                views.Add(orb.OrbId, view);
                displayPositions.Add(orb.OrbId, orb.NormalizedPosition);
            }
            PositionViews();
            Physics2D.SyncTransforms();
            gestures.SetInputEnabled(interactionEnabled);
            LastAction = "3 fixture orbs / owner " + owner;
            LastReason = "DEV supply; not initial Raw5 generation";
            Debug.Log($"C6_T05_FIXTURE mode=DEV_HOST session={registry.SessionId} round={registry.RoundId} owner={owner} count={views.Count}");
            UpdateHud();
        }

        public void EndDevelopmentTest()
        {
            SetInteractionEnabled(false);
            developmentRequested = false;
            session.Stop();
            OnSessionChanged();
            LastAction = "Development test ended";
            LastReason = "No launch, transfer, combination or resource changes";
            UpdateHud();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (!enabled)
            {
                string id = gestures.ActiveOrb?.OrbId;
                // Latch a still-held pointer until its own Up/Cancel; don't clear that exclusion.
                gestures.SetInputEnabled(false);
                if (id != null && !registry.IsPending(id)) SetViewScreenPosition(id, dragStartScreen);
                LastAction = "Input disabled";
                LastReason = "Release the current pointer before another gesture";
                RefreshLocalStates();
            }
            interactionEnabled = enabled;
            gestures.SetInputEnabled(enabled && isActiveAndEnabled && IsHostReady);
            UpdateHud();
        }

        public bool BeginPointer(int pointerId, Vector2 rawPosition, bool startedOverUi)
        {
            OrbRecord orb = null;
            OrbView view = null;
            if (IsHostReady && interactionEnabled && !startedOverUi && layout.TryScreenToOrbPlane(rawPosition, out var point))
            {
                Physics2D.SyncTransforms();
                var hit = Physics2D.OverlapPoint(point, 1 << LayerMask.NameToLayer("C6Orbs"));
                if (hit != null) view = hit.GetComponentInParent<OrbView>();
                if (view != null && registry.TryGet(view.OrbId, out var candidate) &&
                    candidate.OwnerPlayerId == session.OwnedManager.LocalClientId && !registry.IsPending(candidate.OrbId)) orb = candidate;
            }
            gestures.SetInputEnabled(isActiveAndEnabled && IsHostReady && interactionEnabled);
            bool began = gestures.Begin(pointerId, orb, rawPosition, startedOverUi, layout.BottomPixelRect, Screen.width, Tuning);
            if (began)
            {
                dragStartScreen = GetViewScreenPosition(orb.OrbId);
                grabOffset = dragStartScreen - rawPosition;
                LastAction = "Dragging " + orb.Kind + " " + orb.Polarity;
                LastReason = "Pointer " + pointerId + " / raw coordinates before clamp";
                Debug.Log($"C6_T05_POINTER_BEGIN pointer={pointerId} orb={orb.OrbId} kind={orb.Kind}");
            }
            RefreshLocalStates();
            UpdateHud();
            return began;
        }

        public void MovePointer(int pointerId, Vector2 rawPosition)
        {
            if (!isActiveAndEnabled) { CancelPointer(pointerId); return; }
            if (gestures.ActivePointerId != pointerId) return;
            string orbId = gestures.ActiveOrb.OrbId;
            var decision = gestures.Move(pointerId, rawPosition, layout.BottomPixelRect, Screen.width);
            SetViewScreenPosition(orbId, rawPosition + grabOffset);
            if (decision.HasValue) Reserve(decision.Value);
        }

        public void EndPointer(int pointerId, Vector2 rawPosition)
        {
            if (!isActiveAndEnabled) { CancelPointer(pointerId); return; }
            string orbId = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            dropTargets.Clear();
            foreach (var record in registry.Snapshot())
                if (views.ContainsKey(record.OrbId))
                    dropTargets.Add(new OrbDropTarget(record, GetViewScreenPosition(record.OrbId), registry.IsPending(record.OrbId)));
            var decision = gestures.Up(pointerId, rawPosition, layout.BottomPixelRect, Screen.width, dropTargets);
            if (orbId != null) SetViewScreenPosition(orbId, rawPosition + grabOffset);
            if (decision.HasValue) Reserve(decision.Value);
            else if (orbId != null)
            {
                LastAction = "Placed / no transition reserved";
                LastReason = "Local position only; registry metadata unchanged";
                UpdateHud();
            }
            RefreshLocalStates();
        }

        private void Reserve(OrbGestureDecision decision)
        {
            if (!IsHostReady) { CancelInteractions("Host no longer available"); return; }
            ulong sequence = sequences.TryGetValue(decision.OrbId, out var previous) ? checked(previous + 1) : 1;
            sequences[decision.OrbId] = sequence;
            var request = new OrbActionRequest(registry.SessionId, registry.RoundId, Guid.NewGuid().ToString("N"),
                decision.OrbId, decision.OtherOrbId, decision.Kind, sequence, decision.NormalizedPosition);
            // Same authority entry point for Host input; sender comes from NGO, never from the UI.
            var result = registry.Reserve(session.OwnedManager.LocalClientId, request);
            RequestCount++;
            if (result.Accepted && !result.IsDuplicate) AcceptedReservations++;
            LatestDecision = decision;
            LatestReservationResult = result;
            LastAction = (result.Accepted ? "RESERVED " : "REJECTED ") + decision.Kind;
            LastReason = result.Accepted ? "Locked / RESET FIXTURE to continue this orb" : result.Reason;
            // Candidate delivery is confirmed. Registry pending locks remain until a confirmed round reset.
            gestures.ResolvePending(decision.OrbId);
            if (!result.Accepted && registry.TryGet(decision.OrbId, out var record) && !registry.IsPending(record.OrbId))
                SetViewScreenPosition(record.OrbId, Denormalize(record.NormalizedPosition));
            RefreshLocalStates();
            Debug.Log($"C6_T05_RESERVATION action={decision.Kind} accepted={result.Accepted} duplicate={result.IsDuplicate} orb={decision.OrbId} sequence={sequence} round={registry.RoundId} reason={result.Reason}");
            UpdateHud();
        }

        public void CancelPointer(int pointerId)
        {
            string id = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            gestures.Cancel(pointerId);
            if (id != null && !registry.IsPending(id)) SetViewScreenPosition(id, dragStartScreen);
            if (id != null)
            {
                LastAction = "Drag cancelled";
                LastReason = "Pointer cancelled; local position restored";
                UpdateHud();
            }
            RefreshLocalStates();
        }

        public void CancelInteractions(string reason)
        {
            string id = gestures.ActiveOrb?.OrbId;
            gestures.CancelAllPointers();
            if (id != null && !registry.IsPending(id)) SetViewScreenPosition(id, dragStartScreen);
            if (id != null)
            {
                LastAction = "Drag cancelled";
                LastReason = reason;
                UpdateHud();
            }
            RefreshLocalStates();
        }

        public Vector2 GetViewScreenPosition(string orbId) => layout.OrbCamera.WorldToScreenPoint(views[orbId].transform.position);
        private float RadiusPixels => Screen.width * layout.Config.OrbRadiusScreenFraction;
        private float RadiusWorld => 2f * layout.OrbCamera.orthographicSize * RadiusPixels / Mathf.Max(1f, layout.BottomPixelRect.height);
        private GestureTuning Tuning => new GestureTuning(layout.Config.HorizontalSwipeFraction,
            layout.Config.HorizontalDominance, layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction);
        private Vector2 Denormalize(Vector2 point) => new Vector2(layout.BottomPixelRect.xMin + point.x * layout.BottomPixelRect.width,
            layout.BottomPixelRect.yMin + point.y * layout.BottomPixelRect.height);

        private void SetViewScreenPosition(string id, Vector2 point)
        {
            if (!views.TryGetValue(id, out var view) || !Finite(point)) return;
            Rect space = hud.OrbWorkspaceScreenRect;
            float radius = RadiusPixels;
            float horizontalPadding = Mathf.Max(radius, view.GetLabelHalfWidthPixels(layout.OrbCamera) + 2f);
            float minX = space.xMin + horizontalPadding, maxX = space.xMax - horizontalPadding;
            float minY = space.yMin + radius * 1.8f, maxY = space.yMax - radius;
            point.x = minX <= maxX ? Mathf.Clamp(point.x, minX, maxX) : space.center.x;
            point.y = minY <= maxY ? Mathf.Clamp(point.y, minY, maxY) : space.center.y;
            if (layout.TryScreenToOrbPlane(point, out var world))
            {
                view.transform.position = world;
                displayPositions[id] = OrbGestureEngine.NormalizeClamped(point, layout.BottomPixelRect);
            }
        }

        private void PositionViews()
        {
            foreach (var pair in views)
            {
                pair.Value.transform.localScale = Vector3.one * (RadiusWorld / Mathf.Max(.0001f, baseRadius));
                pair.Value.SetLabelPixelHeight(layout.OrbCamera);
                SetViewScreenPosition(pair.Key, Denormalize(displayPositions[pair.Key]));
            }
        }

        private void LateUpdate()
        {
            if (hud == null || hud.Canvas == null || layout == null) return;
            var lower = layout.BottomPixelRect;
            hud.SetAttackZone(OrbGestureEngine.AttackZone(lower, layout.Config.AttackZoneHeightFraction));
            var workspace = hud.OrbWorkspaceScreenRect;
            var tuning = new Vector4(layout.Config.HorizontalSwipeFraction, layout.Config.HorizontalDominance,
                layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction);
            if (previousLower != lower || previousWorkspace != workspace || previousTuning != tuning ||
                !Mathf.Approximately(previousRadiusFraction, layout.Config.OrbRadiusScreenFraction))
            {
                CancelInteractions("Screen geometry changed; begin a new gesture");
                previousLower = lower; previousWorkspace = workspace;
                previousTuning = tuning; previousRadiusFraction = layout.Config.OrbRadiusScreenFraction;
                PositionViews();
            }
        }

        private void RefreshLocalStates()
        {
            foreach (var pair in views)
                pair.Value.SetLocalState(registry.IsPending(pair.Key) ? LocalOrbState.Pending :
                    gestures.ActiveOrb?.OrbId == pair.Key ? LocalOrbState.Dragging : LocalOrbState.Idle);
        }

        private void ClearViews()
        {
            if (viewRoot != null) { viewRoot.gameObject.SetActive(false); Destroy(viewRoot.gameObject); }
            viewRoot = null; views.Clear(); displayPositions.Clear();
        }

        private void UpdateHud()
        {
            if (hud == null || hud.Canvas == null) return;
            hud.SetStatus(IsHostReady ? "DEV HOST / P1" : session.State.ToString().ToUpperInvariant(), LastAction,
                session.State == DirectConnectionState.Failed ? session.Message : LastReason);
            hud.SetControls((Application.isEditor || Debug.isDebugBuild) && session.CanStart,
                IsHostReady, session.State == DirectConnectionState.Connected || session.State == DirectConnectionState.StartingHost);
            Changed?.Invoke();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteractions("Focus lost"); }
        private void OnApplicationPause(bool paused) { if (paused) CancelInteractions("Touch interrupted by application pause"); }
        private void OnDisable()
        {
            if (session != null) session.Changed -= OnSessionChanged;
            CancelInteractions("Input screen disabled");
            gestures.SetInputEnabled(false);
        }
        private void OnDestroy()
        {
            if (wired && hud != null && hud.HostButton != null)
            {
                hud.HostButton.onClick.RemoveListener(OnHostButton);
                hud.ResetButton.onClick.RemoveListener(ResetDevelopmentFixture);
                hud.EndButton.onClick.RemoveListener(EndDevelopmentTest);
            }
            registry.ClearSession();
            Changed = null;
        }
        private static bool Finite(Vector2 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y);
    }
}
