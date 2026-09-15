using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Resources
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SplitScreenLayout), typeof(DirectConnectionSession), typeof(T07Hud))]
    public sealed class T07ResourceController : MonoBehaviour, IOrbPointerSink
    {
        [SerializeField] private SplitScreenLayout layout;
        [SerializeField] private DirectConnectionSession connection;
        [SerializeField] private T07Hud hud;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private AttackLaunchFrame launchFrame;
        [SerializeField] private MonsterHitTarget target;
        private AttackSession attack;
        private ResourceSession resource;
        private OrbPointerInput input;
        private OrbGestureEngine gestures = new OrbGestureEngine();
        private readonly Dictionary<string, OrbView> views = new Dictionary<string, OrbView>();
        private readonly Dictionary<string, Vector2> display = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, GameObject> proxies = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, ulong> sequences = new Dictionary<string, ulong>();
        private readonly Dictionary<string, PendingInput> pending = new Dictionary<string, PendingInput>();
        private readonly HashSet<string> confirmedUnavailable = new HashSet<string>();
        private readonly HashSet<string> touchReceipts = new HashSet<string>();
        private Transform viewRoot, proxyRoot;
        private Material projectileMaterial;
        private string sessionKey;
        private uint round;
        private bool? displayedDebugMode;
        private Vector2 grabOffset, dragStart;
        private bool dragIsTouch;
        private Rect previousSpace, previousLower;
        private Vector4 previousTuning;
        private float previousRadius;
        private string action = "Start DEV HOST or join its IP";
        private string detail = "Normal start: 0 orbs / Stamina 100 / Generate one for 20";
        public SplitScreenLayout Layout => layout;
        public T07Hud Hud => hud;
        public AttackSession Attack => attack;
        public ResourceSession Resource => resource;
        public OrbGestureEngine Gestures => gestures;
        public IReadOnlyDictionary<string, OrbView> Views => views;
        public int TouchLaunches { get; private set; }
        public int TouchBegins { get; private set; }
        public bool IsDefenseHeld => false;
        public bool CanInteract => isActiveAndEnabled && attack != null && attack.Connected && attack.Snapshot != null
            && attack.Snapshot.state == "Playing" && resource != null && resource.Connected
            && resource.Snapshot.playing;
        public event Action Changed;

        public void Configure(SplitScreenLayout split, DirectConnectionSession session, T07Hud overlay,
            Material sprite, AttackLaunchFrame frame, MonsterHitTarget hitTarget)
        { layout = split; connection = session; hud = overlay; spriteMaterial = sprite; launchFrame = frame; target = hitTarget; }
        private void Awake()
        {
            if (layout == null) layout = GetComponent<SplitScreenLayout>();
            if (connection == null) connection = GetComponent<DirectConnectionSession>();
            if (hud == null) hud = GetComponent<T07Hud>();
            input = GetComponent<OrbPointerInput>();
            // Explicit serialized asset reference keeps URP's actual shader in both builds.
            projectileMaterial = new Material(spriteMaterial) { name = "T07 Projectile Teal" };
            projectileMaterial.color = new Color(.25f, .85f, .77f);
            attack = gameObject.AddComponent<AttackSession>();
            attack.ConfigureInventory(false, 64);
            attack.Configure(connection, layout.Config, launchFrame, target, projectileMaterial);
            attack.Changed += OnStateChanged;
            // Stop completes asynchronously after the authority has already been released.
            connection.Changed += RefreshHud;
            attack.RequestResolved += OnResolved;
            resource = gameObject.AddComponent<ResourceSession>();
            resource.Configure(attack, layout.Config);
            resource.Changed += OnResourceChanged;
            resource.GenerationResolved += OnGenerationResolved;
            resource.RecoveryResolved += OnRecoveryResolved;
            OrbView.SetSharedMaterial(spriteMaterial);
            viewRoot = new GameObject("T07 Local Orb Views").transform; viewRoot.SetParent(transform, false);
            proxyRoot = new GameObject("T07 Client Projectile Display Only").transform; proxyRoot.SetParent(transform, false);
        }
        private void Start()
        {
            hud.HostButton.onClick.AddListener(StartDevelopmentHost);
            hud.JoinButton.onClick.AddListener(JoinDevelopmentHost);
            hud.ResetButton.onClick.AddListener(ResetDevelopmentRound);
            hud.EndButton.onClick.AddListener(EndDevelopmentTest);
            hud.GenerateButton.onClick.AddListener(GenerateOrb);
            hud.DebugFixtureButton.onClick.AddListener(StartDebugFixtureRound);
            hud.DebugCombinedButton.onClick.AddListener(RequestDebugHitOrb);
            RefreshHud();
            Debug.Log($"C6_T07_READY mode=NORMAL initialOrbs=0 build=9 device={SystemInfo.deviceModel} os={SystemInfo.operatingSystem} screen={Screen.width}x{Screen.height} safe={Screen.safeArea} staminaStart={layout.Config.StaminaStart} staminaMax={layout.Config.StaminaMax} generateCost={layout.Config.GenerateCost} recoveryPerSecond={layout.Config.StaminaRecoveryPerSecond} hitRecovery={layout.Config.StaminaHitRecovery}");
            Debug.Log("C6_T07_LOCAL_IPV4 addresses=[" + string.Join(",", DirectConnectionSession.GetLocalIPv4Addresses()) + "]");
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
        public void ResetDevelopmentRound()
        {
            if (!isActiveAndEnabled || !attack.IsHost) return;
            CancelInteractions("Explicit normal resource reset");
            action = "RESET EMPTY / full stamina";
            detail = "Normal start has no pre-generated orbs";
            resource.ResetNormalRound();
        }
        public void EndDevelopmentTest()
        { CancelInteractions("Development test ended"); attack.EndDevelopmentTest(); }

        public void GenerateOrb() => RequestGenerate();
        public bool RequestGenerate()
        {
            if (!isActiveAndEnabled || resource == null || !resource.Connected || resource.HasPending) return false;
            action = "GENERATE / waiting for Host";
            detail = "One Raw orb / confirmed cost " + layout.Config.GenerateCost;
            bool sent = resource.RequestGenerate();
            if (!sent && !resource.HasPending) { action = "GENERATE unavailable"; detail = resource.Status; }
            RefreshHud();
            return sent;
        }
        public void StartDebugFixtureRound()
        {
            if (!isActiveAndEnabled || resource == null || !resource.IsHost) return;
            CancelInteractions("Explicit DEBUG_TEST_MODE fixture round");
            if (resource.BeginDebugFixtureRound())
            {
                action = "DEBUG_TEST_MODE / fixed Raw fixture";
                detail = "P1: Yin 5 / P2: Yang 5 / separate from normal generation";
            }
            else { action = "Debug fixture unavailable"; detail = resource.Status; }
            RefreshHud();
        }
        public void RequestDebugHitOrb()
        {
            if (!isActiveAndEnabled || resource == null || !resource.CanRequestDebugCombined) return;
            action = "DEV HIT ORB / waiting for Host";
            detail = "Explicit free Combined fixture / actual hit recovery test";
            resource.RequestDebugCombined();
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
                    action = "DEBUG_TEST_MODE / fixed Raw fixture";
                    detail = "P1: Yin 5 / P2: Yang 5 / separate from normal generation";
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
            bool debug = reply.operation == (int)ResourceRequestKind.DebugCombined;
            action = reply.accepted ? (debug ? "DEV HIT ORB APPROVED" : "GENERATE APPROVED") : "REJECTED / " + reply.reason;
            detail = reply.accepted
                ? (debug ? "DEBUG_TEST_MODE / free Combined fixture / hit bonus +" + layout.Config.StaminaHitRecovery
                    : "One Raw orb created by Host / cost " + layout.Config.GenerateCost)
                : "Host rejected request; inventory and generation cost unchanged";
            Debug.Log($"C6_T07_GENERATE_UI accepted={reply.accepted} duplicate={reply.duplicate} operation={reply.operation} reason={reply.reason} request={reply.requestId} round={reply.roundId}");
            RefreshHud();
        }

        private void OnRecoveryResolved(ResourceRecoveryResult result)
        {
            if (result == null || !result.Accepted || result.IsDuplicate || result.PlayerId != attack.LocalPlayerId) return;
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
                ClearViews(); ClearProxies(); pending.Clear(); sequences.Clear(); confirmedUnavailable.Clear();
                gestures = new OrbGestureEngine(); gestures.SetInputEnabled(false); sessionKey = null; round = 0; displayedDebugMode = null;
                RefreshHud(); return;
            }
            if (sessionKey != state.sessionId || round != state.roundId)
            {
                CancelInteractions("Confirmed session/round changed");
                ClearViews(); pending.Clear(); sequences.Clear(); confirmedUnavailable.Clear(); gestures = new OrbGestureEngine();
                hud.SetNetworkFieldsVisible(!attack.Connected); Canvas.ForceUpdateCanvases();
                sessionKey = state.sessionId; round = state.roundId; displayedDebugMode = null;
                action = "NORMAL round " + round + " / empty start";
                detail = "Stamina starts full / one Raw orb costs " + layout.Config.GenerateCost;
            }
            var available = state.orbs.Where(o => o.owner == attack.LocalPlayerId && o.state == (int)OrbAuthorityState.Idle && !confirmedUnavailable.Contains(o.id)).ToArray();
            var ids = new HashSet<string>(available.Select(o => o.id));
            foreach (string id in views.Keys.ToArray())
                if (!ids.Contains(id))
                {
                    // A confirmed Launching/Projectile/Consumed record, never a local timeout, removes the view.
                    views[id].gameObject.SetActive(false); Destroy(views[id].gameObject); views.Remove(id); display.Remove(id);
                }
            foreach (var orb in available)
                if (!views.ContainsKey(orb.id))
                {
                    var view = new GameObject("T07 Orb " + orb.id).AddComponent<OrbView>(); view.transform.SetParent(viewRoot, false);
                    view.Configure(orb.id, (OrbKind)orb.kind, (OrbPolarity)orb.polarity, LayerMask.NameToLayer("C6Orbs"), RadiusWorld,
                        orb.kind == (int)OrbKind.Combined ? "COMB" : orb.polarity == (int)OrbPolarity.Yin ? "YIN" : "YANG");
                    views.Add(orb.id, view);
                    PositionView(orb.id, GridToScreen(orb.pos));
                }
            if (!CanInteract && isActiveAndEnabled) CancelInteractions("Test phase " + state.state);
            gestures.SetInputEnabled(CanInteract);
            RefreshLocalStates(); UpdateProxies(); RefreshHud();
        }
        /// <summary>Five columns by four rows occupy the usable region below the launch band.</summary>
        public Rect OrbGridScreenRect
        {
            get
            {
                if (layout == null || hud == null) return Rect.zero;
                Rect space = hud.OrbWorkspaceScreenRect;
                float margin = 4f * (hud.Canvas != null ? hud.Canvas.scaleFactor : 1f);
                float launchBottom = OrbGestureEngine.AttackZone(layout.BottomPixelRect, layout.Config.AttackZoneHeightFraction).yMin;
                float maximum = Mathf.Max(space.yMin + margin, Mathf.Min(space.yMax, launchBottom) - margin);
                return new Rect(space.xMin + margin, space.yMin + margin,
                    Mathf.Max(1f, space.width - 2f * margin), Mathf.Max(1f, maximum - space.yMin - margin));
            }
        }
        private Vector2 GridToScreen(Vector2 slot)
        {
            Rect grid = OrbGridScreenRect;
            return grid.min + Vector2.Scale(new Vector2(Mathf.Clamp01(slot.x), Mathf.Clamp01(slot.y)), grid.size);
        }
        public bool BeginPointer(int pointerId, Vector2 raw, bool overUi)
        {
            OrbRecord record = null;
            if (CanInteract && !overUi && layout.TryScreenToOrbPlane(raw, out var world))
            {
                Physics2D.SyncTransforms(); var hit = Physics2D.OverlapPoint(world, 1 << LayerMask.NameToLayer("C6Orbs"));
                var view = hit == null ? null : hit.GetComponentInParent<OrbView>();
                var wire = view == null ? null : attack.Snapshot.orbs.FirstOrDefault(o => o.id == view.OrbId);
                if (wire != null && wire.owner == attack.LocalPlayerId && !IsPending(wire.id)) record = wire.ToRecord();
            }
            gestures.SetInputEnabled(CanInteract);
            bool began = gestures.Begin(pointerId, record, raw, overUi, layout.BottomPixelRect, Screen.width, Tuning);
            if (began)
            {
                dragStart = GetViewScreenPosition(record.OrbId); grabOffset = dragStart - raw;
                dragIsTouch = input != null && input.IsActiveTouchPointer(pointerId);
                if (dragIsTouch) TouchBegins++;
                action = "Dragging " + record.Kind;
                detail = "Raw cannot launch / one action per pointer";
                Debug.Log($"C6_T07_POINTER_BEGIN source={(dragIsTouch ? "TOUCH" : pointerId == OrbPointerInput.MousePointerId ? "MOUSE" : "DEBUG")} pointer={pointerId} orb={record.OrbId} round={round}");
            }
            RefreshLocalStates(); RefreshHud(); return began;
        }
        public void MovePointer(int pointerId, Vector2 raw)
        {
            if (!isActiveAndEnabled || !CanInteract) { CancelPointer(pointerId); return; }
            if (gestures.ActivePointerId != pointerId) return;
            string id = gestures.ActiveOrb.OrbId;
            var decision = gestures.Move(pointerId, raw, layout.BottomPixelRect, Screen.width);
            PositionView(id, raw + grabOffset);
            if (decision.HasValue) SubmitDecision(decision.Value, dragIsTouch);
        }
        public void EndPointer(int pointerId, Vector2 raw)
        {
            if (!isActiveAndEnabled || !CanInteract) { CancelPointer(pointerId); return; }
            string id = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            bool touch = dragIsTouch;
            var decision = gestures.Up(pointerId, raw, layout.BottomPixelRect, Screen.width, Array.Empty<OrbDropTarget>());
            if (id != null) PositionView(id, raw + grabOffset);
            if (decision.HasValue) SubmitDecision(decision.Value, touch);
            RefreshLocalStates(); RefreshHud();
        }
        private void SubmitDecision(OrbGestureDecision decision, bool fromTouch)
        {
            ulong seq = sequences.TryGetValue(decision.OrbId, out var old) ? checked(old + 1) : 1;
            sequences[decision.OrbId] = seq;
            var request = new OrbActionRequest(sessionKey, round, Guid.NewGuid().ToString("N"), decision.OrbId, decision.OtherOrbId,
                decision.Kind, seq, decision.NormalizedPosition);
            // T07 authority rejects non-Launch actions; no hidden transfer/combine implementation.
            pending[request.RequestId] = new PendingInput { request = request, touch = fromTouch, sentAt = Time.unscaledTime };
            action = "Waiting for Host / " + decision.Kind;
            bool sent = attack.Submit(request);
            if (!sent) { detail = "Host request not confirmed; querying authority"; }
            RefreshLocalStates(); RefreshHud();
        }
        private void OnResolved(AttackRequestReply reply)
        {
            if (reply == null || reply.sessionId != sessionKey || reply.roundId != round ||
                !pending.TryGetValue(reply.requestId, out var inputRequest)) return;
            if (reply.orbId != inputRequest.request.OrbId) return;
            if (!reply.known) { detail = "Host has not confirmed this request; keep locked"; RefreshHud(); return; }
            pending.Remove(reply.requestId); gestures.ResolvePending(reply.orbId);
            if (reply.accepted)
            {
                // An authenticated approval is enough to remove 2D; a stale Idle snapshot cannot recreate it.
                confirmedUnavailable.Add(reply.orbId);
                if (views.TryGetValue(reply.orbId, out var launched))
                { launched.gameObject.SetActive(false); Destroy(launched.gameObject); views.Remove(reply.orbId); display.Remove(reply.orbId); }
            }
            if (reply.accepted && inputRequest.request.Kind == OrbActionKind.Launch && inputRequest.touch && touchReceipts.Add(reply.requestId))
            {
                TouchLaunches++;
                Debug.Log($"C6_T07_TOUCH_LAUNCH count={TouchLaunches} request={reply.requestId} orb={reply.orbId} round={round} owner={attack.LocalPlayerId}");
            }
            action = (reply.accepted ? "LAUNCH APPROVED" : "REJECTED") + " / " + reply.reason;
            detail = reply.accepted ? "Same ID / 2D removed / actual Host physics" : "Host confirmed original state; no duplicate spawn";
            if (!reply.accepted && reply.confirmedOrb != null && !reply.pending && views.ContainsKey(reply.orbId))
                PositionView(reply.orbId, GridToScreen(reply.confirmedOrb.pos));
            RefreshLocalStates(); RefreshHud();
        }
        private bool IsPending(string id) => pending.Values.Any(p => p.request.OrbId == id);
        public void CancelPointer(int pointerId)
        {
            string id = gestures.ActivePointerId == pointerId ? gestures.ActiveOrb?.OrbId : null;
            gestures.Cancel(pointerId);
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            RefreshLocalStates();
        }
        public void CancelInteractions(string reason)
        {
            string id = gestures.ActiveOrb?.OrbId; gestures.CancelAllPointers();
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            // Pending stays locked until a host receipt/new round. Cancelling a touch does not respawn.
            RefreshLocalStates();
        }
        private void Update()
        {
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
            hud.SetAttackZone(OrbGestureEngine.AttackZone(layout.BottomPixelRect, layout.Config.AttackZoneHeightFraction));
            var tuning = new Vector4(layout.Config.HorizontalSwipeFraction, layout.Config.HorizontalDominance,
                layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction);
            if (previousSpace != hud.OrbWorkspaceScreenRect || previousLower != layout.BottomPixelRect || previousTuning != tuning || previousRadius != RadiusPixels)
            {
                CancelInteractions("Geometry changed"); previousSpace = hud.OrbWorkspaceScreenRect;
                previousLower = layout.BottomPixelRect; previousTuning = tuning; previousRadius = RadiusPixels;
                foreach (string id in views.Keys.ToArray()) PositionView(id, Denormalize(display[id]));
            }
        }
        private void UpdateProxies()
        {
            if (attack.IsHost || attack.Snapshot == null) { ClearProxies(); return; }
            var ids = new HashSet<string>(attack.Snapshot.projectiles.Select(p => p.id));
            foreach (var id in proxies.Keys.ToArray()) if (!ids.Contains(id)) { Destroy(proxies[id]); proxies.Remove(id); }
            foreach (var wire in attack.Snapshot.projectiles)
            {
                if (!proxies.TryGetValue(wire.id, out var proxy))
                {
                    proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere); proxy.name = "Display only " + wire.id;
                    proxy.transform.SetParent(proxyRoot, false); proxy.layer = LayerMask.NameToLayer("C6Battle");
                    var collider = proxy.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    proxy.GetComponent<Renderer>().sharedMaterial = projectileMaterial; proxies.Add(wire.id, proxy);
                }
                proxy.transform.position = wire.position; proxy.transform.localScale = Vector3.one * wire.radius * 2f;
            }
        }
        public Vector2 GetViewScreenPosition(string id) => layout.OrbCamera.WorldToScreenPoint(views[id].transform.position);
        private float RadiusPixels => Mathf.Max(1f, Mathf.Min(Screen.width * layout.Config.OrbRadiusScreenFraction,
            OrbGridScreenRect.width / 5f * .35f, OrbGridScreenRect.height / 4f * .28f));
        private float LabelPixels => Mathf.Max(1f, Mathf.Min(12f * Mathf.Max(.1f, hud.Canvas.scaleFactor), OrbGridScreenRect.height / 4f * .25f));
        private float RadiusWorld => 2f * layout.OrbCamera.orthographicSize * RadiusPixels / Mathf.Max(1f, layout.BottomPixelRect.height);
        private GestureTuning Tuning => new GestureTuning(layout.Config.HorizontalSwipeFraction, layout.Config.HorizontalDominance,
            layout.Config.CombinationRadiusFraction, layout.Config.AttackZoneHeightFraction);
        private Vector2 Denormalize(Vector2 n) => layout.BottomPixelRect.min + Vector2.Scale(n, layout.BottomPixelRect.size);
        private void PositionView(string id, Vector2 point)
        {
            if (!views.TryGetValue(id, out var view) || float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y)) return;
            view.transform.localScale = Vector3.one * RadiusWorld / Mathf.Max(.0001f, view.Collider.radius);
            view.SetLabelPixelHeight(layout.OrbCamera, LabelPixels);
            Rect space = hud.OrbWorkspaceScreenRect;
            float x = Mathf.Max(RadiusPixels, view.GetLabelHalfWidthPixels(layout.OrbCamera) + 2f);
            point.x = Mathf.Clamp(point.x, space.xMin + x, Mathf.Max(space.xMin + x, space.xMax - x));
            point.y = Mathf.Clamp(point.y, space.yMin + RadiusPixels * 1.8f, Mathf.Max(space.yMin + RadiusPixels * 1.8f, space.yMax - RadiusPixels));
            if (layout.TryScreenToOrbPlane(point, out var world))
            { view.transform.position = world; display[id] = OrbGestureEngine.NormalizeClamped(point, layout.BottomPixelRect); }
        }
        private void RefreshLocalStates()
        {
            foreach (var pair in views) pair.Value.SetLocalState(IsPending(pair.Key) ? LocalOrbState.Pending :
                gestures.ActiveOrb?.OrbId == pair.Key ? LocalOrbState.Dragging : LocalOrbState.Idle);
        }
        private void RefreshHud()
        {
            if (hud == null || hud.Canvas == null) return;
            var s = attack.Snapshot;
            hud.SetNetworkFieldsVisible(!attack.Connected);
            hud.SetStatus(attack.Connected ? (attack.IsHost ? "DEV HOST / P1" : "CLIENT / P2") : connection.State.ToString().ToUpperInvariant(), action,
                connection.State == DirectConnectionState.Failed ? connection.Message : detail);
            hud.SetControls(connection.CanStart, connection.CanStart,
                resource != null && resource.IsHost && resource.Connected && !resource.HasPending,
                !connection.CanStart, resource != null && resource.CanGenerate,
                resource != null && resource.IsHost && resource.Connected && !resource.HasPending
                    && layout.Config.ResourceDebugToolsEnabled && (Application.isEditor || Debug.isDebugBuild),
                resource != null && resource.CanRequestDebugCombined);
            hud.SetProgress(s?.hp ?? layout.Config.MonsterMaxHp, layout.Config.MonsterMaxHp, s?.totalHits ?? 0,
                s?.roundId ?? 0, s?.resets ?? 0);
            var resources = resource?.Snapshot;
            var player = resource?.LocalPlayer;
            hud.SetResources(player?.stamina ?? layout.Config.StaminaStart,
                resources?.maximum ?? layout.Config.StaminaMax, resources?.generateCost ?? layout.Config.GenerateCost,
                resources != null && resources.playing ? resources.regenerationRate : 0d,
                player?.storedOrbs ?? 0, resources?.storageLimit ?? layout.Config.OrbStorageLimit,
                resource != null && resource.HasPending, resource != null && resource.DebugTestMode);
            Changed?.Invoke();
        }
        private void ClearViews()
        {
            foreach (var view in views.Values) { if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); } }
            views.Clear(); display.Clear();
        }
        private void ClearProxies() { foreach (var proxy in proxies.Values) if (proxy != null) Destroy(proxy); proxies.Clear(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteractions("Focus lost"); }
        private void OnApplicationPause(bool paused) { if (paused) CancelInteractions("Paused"); }
        private void OnDisable()
        {
            string id = gestures.ActiveOrb?.OrbId;
            gestures.SetInputEnabled(false);
            if (id != null && !IsPending(id)) PositionView(id, dragStart);
            RefreshLocalStates();
        }
        private void OnDestroy()
        {
            if (connection != null) connection.Changed -= RefreshHud;
            if (attack != null) { attack.Changed -= OnStateChanged; attack.RequestResolved -= OnResolved; }
            if (resource != null) { resource.Changed -= OnResourceChanged; resource.GenerationResolved -= OnGenerationResolved; resource.RecoveryResolved -= OnRecoveryResolved; }
            if (projectileMaterial != null) Destroy(projectileMaterial);
            Changed = null;
        }
        private sealed class PendingInput
        { public OrbActionRequest request; public bool touch, queried; public float sentAt; }
    }
}
