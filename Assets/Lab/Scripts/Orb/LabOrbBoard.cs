using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace C6Lab
{
    /// <summary>A local physics observation. The Host must still approve an ownership transfer.</summary>
    public readonly struct LabOrbEdgeCrossing
    {
        public string OrbId { get; }
        public bool ToRight { get; }
        public float Height01 { get; }
        public Vector2 VelocityInBoardWidthsPerSecond { get; }
        /// <summary>Why it crossed: a player's swipe, a drum passing on, or a bomb blast.</summary>
        public LabEdgeCrossingKind Kind { get; }

        public LabOrbEdgeCrossing(string orbId, bool toRight, float height01, Vector2 velocity,
            LabEdgeCrossingKind kind = LabEdgeCrossingKind.Swipe)
        {
            OrbId = orbId;
            ToRight = toRight;
            Height01 = height01;
            VelocityInBoardWidthsPerSecond = velocity;
            Kind = kind;
        }
    }

    /// <summary>An intentional Combined flick, measured in board widths and seconds.</summary>
    public readonly struct LabOrbThrowGesture
    {
        public string OrbId { get; }
        public Vector2 Velocity { get; }
        public float ReleaseX01 { get; }
        public float DistanceInBoardWidths { get; }

        public LabOrbThrowGesture(string orbId, Vector2 velocity, float releaseX01, float distance)
        {
            OrbId = orbId;
            Velocity = velocity;
            ReleaseX01 = releaseX01;
            DistanceInBoardWidths = distance;
        }
    }

    /// <summary>
    /// Only the owner simulates a free orb. A Host-approved transfer creates that same orb ID
    /// on the receiver with its outgoing height and velocity. Gameplay requests are events;
    /// this board never changes Host-owned HP, inventory, or orb identity itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class LabOrbBoard : MonoBehaviour
    {
        private sealed class Entry
        {
            public LabOrbView View;
            public bool LocalOwner;
            public bool Locked;
            public bool Held;
            public bool EdgePending;
            /// <summary>A gourd bomb still falling onto this board; it cannot be touched until it lands.</summary>
            public bool Dropping;
            /// <summary>How a drum handed to this board is still arriving (#178); None once it is the player's.</summary>
            public LabDrumArrival Arrival;
            /// <summary>+1 when the arriving drum came in from the left and travels right, -1 the other way.</summary>
            public float ArrivalDirection;
            /// <summary>World x where an arriving drum comes to rest (Stop), or the farthest it may slide (Coast).</summary>
            public float ArrivalStopX;
            /// <summary>A drum a bomb blast flung, until it slows to its drift; crossing now says so to the Host.</summary>
            public bool Blasted;
            /// <summary>A bomb blast shrank this orb for the rest of its life (#213).</summary>
            public bool Small;
            public float JigwiScale = 1f;
            public float JigwiABScale = 1f;
            /// <summary>
            /// A bomb the player swiped or dragged on (#237): it slides until it stops, and passes at a side
            /// edge however slowly. Once it stops it drifts by itself again and this is cleared.
            /// </summary>
            public bool PlayerSent;
            /// <summary>When the held body last followed the finger, for a heavy bomb's lag (#212).</summary>
            public double LastDragAt;
            public string PendingPartnerId;
            public LabOrbEdgeCrossing PendingEdge;
            public Vector2 PressVelocity;
            public Vector2 GrabOffset;
            public bool SimulationHoldCaptured;
            public Vector3 SimulationHoldVelocity, SimulationHoldAngularVelocity;
            public float DriftSeed;
            public float DriftHeading;
            public bool HasDriftHeading;
            public bool Provisional;
            public bool HiddenForPending;
            public MotionState? BeforeRequest;
            /// <summary>A fox orb cannot be touched off until this time (#251): a moment after it lands.</summary>
            public float FoxSafeUntil;
            /// <summary>
            /// An unlocked bomb (#328): when it came to rest here, -1 while it is still moving in, and the earliest time
            /// this board may ask again to open it after the Host turned a request down.
            /// </summary>
            public float BombRestingSince = -1f;
            public float BombNextOpenAt;
            public float SafetyRestSeconds;
            public bool SafetyNudging;
            public float SafetyNudgeSeconds;
            public float SafetyCombineGuardUntil;
        }

        private readonly struct MotionState
        {
            public readonly Vector2 Position01;
            public readonly Vector2 Velocity;
            public MotionState(Vector2 position01, Vector2 velocity)
            { Position01 = position01; Velocity = velocity; }
        }

        private sealed class CombinePreview
        {
            public string FirstId, SecondId;
            public MotionState FirstMotion, SecondMotion;
        }

        private sealed class GenerationPreview
        {
            public GameObject Root;
            public Material Material;
        }

        private readonly struct PointerSample
        {
            public readonly Vector2 Position;
            public readonly double Time;
            public PointerSample(Vector2 position, double time) { Position = position; Time = time; }
        }

        private const float ContactFriction = .15f;
        private const float WallThickness = .5f;
        private const double ReleaseSampleWindow = .12d;
        private const double TapMaxSeconds = .3d;
        private const double DoubleTapWindow = .35d;
        // Drum drift: how fast its velocity eases toward the drift path (board widths/s²), how near
        // a side edge or wall (board widths) it starts easing away, how much faster than its drift
        // an outward push must be to pass it to a neighbour, and how its art turns as the path curves.
        private const float DrumDriftAcceleration = .3f;
        private const float DrumEdgeMargin = .15f;
        private const float DrumPassSpeedFactor = 1.5f;
        private const float DrumDriftSpinGain = .3f;
        private const float DrumDriftMaxSpin = 90f;
        // A drum handed to this board (#178) reaches its spot within this many seconds even when it came in slowly,
        private const float ArrivalMaxSeconds = 1.2f;
        // and brakes no harder than this (board widths/s²), so a hard swipe slows on entry and then glides in.
        private const float ArrivalMaxBrake = 8f;
        // It has arrived once this close to its spot (board widths) or this slow (board widths/s).
        private const float ArrivalTolerance = .005f;
        private const float ArrivalRestSpeed = .02f;
        // A drum passing through runs on to the next player at no less than this (board widths/s).
        private const float PassMinSpeed = .9f;
        // An unlocked bomb (#328) rests this long where it stopped, so it is seen to arrive, then opens; a request
        // the Host turned down is asked again after the retry time.
        private const float BombOpenPauseSeconds = .2f;
        private const float BombOpenRetrySeconds = 1f;
        // The opening bomb's puff is this many times its size, so the change into two attack orbs is plain to see.
        private const float BombOpenSmokeScale = 2.2f;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly HashSet<string> spawnedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockEffectIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> expectedDrops = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombinePreview> combinePreviews = new Dictionary<string, CombinePreview>(StringComparer.Ordinal);
        private readonly Dictionary<string, GenerationPreview> generationPreviews = new Dictionary<string, GenerationPreview>(StringComparer.Ordinal);
        private readonly Dictionary<string, MotionState> pendingLaunches = new Dictionary<string, MotionState>(StringComparer.Ordinal);
        private readonly List<PointerSample> samples = new List<PointerSample>();
        private readonly List<Entry> updateEntries = new List<Entry>();
        private LabConfig config;
        [Header("Combo SMALL/BIG on the board (#257) - art only, physics unchanged")]
        [Tooltip("콤보 실패(SMALL) 동안 판 구슬 그림 배율. 던지는 공(0.1배)보다 완화한다.")]
        [SerializeField, Range(.2f, 1f)] private float comboSmallScale = .6f;
        [Tooltip("콤보 성공(BIG) 동안 판 구슬 그림 배율. 던지는 공(2.5배)보다 완화한다.")]
        [SerializeField, Range(1f, 2.5f)] private float comboBigScale = 1.4f;
        [Tooltip("크기가 바뀌는 데 걸리는 시간 (초, 대략)")]
        [SerializeField, Min(0f)] private float comboScaleSeconds = .25f;
        private int comboLook;
        private Camera inputCamera;
        /// <summary>이 판을 비추는 카메라. 판 구슬 그림이 이 카메라 쪽을 본다 (LabOrbSpriteArt).</summary>
        public Camera ViewCamera => inputCamera;
        private Rect bounds;
        private int blockedSide;
        private float blockSecondsLeft;
        private LabOrbEdgeBlock edgeBlock;
        /// <summary>2:2 fever (#281): how much bigger a turned orb starts, and how long it takes to settle.</summary>
        private const float FeverTurnScale = 1.45f, FeverTurnSeconds = .35f;
        // 2:2 (#281): the closed outer edge of this board for the whole battle.
        private int teamWallSide;
        private LabOrbTeamWall teamWall;
        /// <summary>The board keeps its own swipe-block countdown unless the Host's differs by more than this.</summary>
        private const float BlockResyncSeconds = .3f;
        private LabOrbAreaShape areaShape;
        private readonly List<Mesh> areaWallMeshes = new List<Mesh>();
        private Rect inputScreenRect;
        private bool hasInputScreenRect;
        private PhysicsMaterial contactMaterial;
        private GameObject wallRoot;
        private string heldId;
        private Vector2 pressPoint;
        private double pressTime;
        private string lastTapId;
        private double lastTapTime;
        private bool heldByTouch;
        private bool interactionEnabled = true;
        private bool simulationHeld;
        public bool SimulationHeld => simulationHeld;
        private bool uiInputBlocked;
        private bool configured;
        private bool jigwiOverheat;
        private bool contactCombineRequest;

        public int Count
        {
            get
            {
                int visible = 0;
                foreach (Entry entry in entries.Values) if (!entry.HiddenForPending) visible++;
                return visible;
            }
        }
        /// <summary>A stable enumeration for reconciliation; callers may remove bodies while iterating this copy.</summary>
        public string[] OrbIds
        {
            get
            {
                var ids = new string[entries.Count];
                entries.Keys.CopyTo(ids, 0);
                return ids;
            }
        }
        /// <summary>The edge the gourd's swipe block (#215) holds shut on this board: -1 left, +1 right, 0 none.</summary>
        public int BlockedSide => blockedSide;
        /// <summary>Seconds this board still shows the swipe block.</summary>
        public float BlockSecondsLeft => blockedSide != 0 ? blockSecondsLeft : 0f;
        /// <summary>How many of this board's own passes have bounced off a blocked edge.</summary>
        public int BlockBounces { get; private set; }
        public LabOrbEdgeBlock EdgeBlock => edgeBlock;
        /// <summary>2:2 (#281): the closed outer edge: -1 left, +1 right, 0 none.</summary>
        public int TeamWallSide => teamWallSide;
        /// <summary>How many crossings have bounced off the 2:2 outer wall.</summary>
        public int TeamWallBounces { get; private set; }
        public LabOrbTeamWall TeamWall => teamWall;
        /// <summary>World-space limits for orb centres; the visual extends by OrbRadius.</summary>
        public Rect CenterBounds => bounds;
        public LabOrbAreaShape AreaShape => areaShape;
        public bool InteractionEnabled => interactionEnabled;
        public bool UiInputBlocked => uiInputBlocked;
        public event Action<LabOrbEdgeCrossing> EdgeCrossed;
        /// <summary>
        /// 결합 요청. (첫 구슬, 두 번째 구슬, 결합 위치 0..1, 결합 구슬이 이어받을 속도 보드 폭/초)
        /// </summary>
        public event Action<string, string, Vector2, Vector2> CombineRequested;
        /// <summary>Read inside CombineRequested only: true for passive physical contact, false for manual drop.</summary>
        public bool IsContactCombineRequest => contactCombineRequest;
        /// <summary>
        /// A fox orb touched a Yin or Yang orb (#251): (fox orb, the orb it took). Both already went up in blue smoke
        /// here and wait hidden for the Host; a rejection brings them back.
        /// </summary>
        public event Action<string, string> FoxFireTouched;
        /// <summary>
        /// A fox orb went up in its blue smoke here because a player cleared it (#351): a double tap, or it touched a Yin or
        /// Yang orb. Not when its lifetime ran out or fever time cleared it (<see cref="OrbVanished"/>). (fox orb ID)
        /// </summary>
        public event Action<string> FoxFireCleared;
        /// <summary>An unlocked bomb that stopped here went up in smoke to open into two attack orbs (#328, #351). (bomb ID)</summary>
        public event Action<string> BombOpening;
        /// <summary>Manual release during Jigwi overheat; inputs already wait hidden for approval.</summary>
        public event Action<string, string> JigwiBurnRequested;
        public bool JigwiOverheated => jigwiOverheat;
        public void SetJigwiOverheat(bool active) => jigwiOverheat = active;
        /// <summary>The velocity is input displacement divided by board width and seconds.</summary>
        public event Action<LabOrbThrowGesture> ThrowRequested;
        /// <summary>내가 생성한 구슬이 북에서 튀어나오기 시작할 때 한 번 알린다. (구슬 ID)</summary>
        public event Action<string> SpawnStarted;
        /// <summary>A double tap on an unlocked usable item. The Host applies or rejects its effect.</summary>
        public event Action<string> UseRequested;
        /// <summary>A tap on a drum asks the Host to generate an orb from it. (drum ID)</summary>
        public event Action<string> DrumTapped;
        /// <summary>A gourd bomb finished falling onto this board and can now be touched. (bomb ID)</summary>
        public event Action<string> DropLanded;

        public void Configure(LabConfig tuning, Camera camera, Rect worldCenterBounds)
        {
            if (tuning == null || camera == null || worldCenterBounds.width <= 0f || worldCenterBounds.height <= 0f
                || !Finite(worldCenterBounds.min) || !Finite(worldCenterBounds.max))
                throw new ArgumentException("Config, camera, and finite positive board centre bounds are required.");
            ConfigureArea(tuning, camera, LabOrbAreaShape.FromCenterBounds(worldCenterBounds, tuning.OrbRadius));
        }

        public void ConfigureArea(LabConfig tuning, Camera camera, LabOrbAreaShape shape)
        {
            if (tuning == null || camera == null || shape == null
                || (shape.IsCurved && !shape.CanFitRadius(Mathf.Max(tuning.OrbRadius, tuning.DrumRadius))))
                throw new ArgumentException("Config, camera, and an area large enough for orbs and drums are required.");
            CancelDrag();
            config = tuning;
            inputCamera = camera;
            areaShape = shape;
            bounds = shape.CenterBounds;
            configured = true;
            LabOrbTransferGuide.Ensure(this);
            if (contactMaterial == null)
                contactMaterial = new PhysicsMaterial("Lab orb contact") { hideFlags = HideFlags.DontSave };
            contactMaterial.bounciness = config.OrbRestitution;
            contactMaterial.dynamicFriction = ContactFriction;
            contactMaterial.staticFriction = ContactFriction;
            contactMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            BuildHorizontalWalls();
            foreach (var entry in entries.Values)
            {
                entry.View.Configure(entry.View.OrbId, entry.View.Kind, RadiusFor(entry.View.Kind, entry.Small, entry.JigwiScale, entry.JigwiABScale), entry.View.Element);
                entry.View.ConfigureAppearance(config.OrbAppearance);
                SetLayerRecursively(entry.View.transform, gameObject.layer);
                entry.View.HitSphere.sharedMaterial = contactMaterial;
                entry.View.Body.position = OnPlane(Clamp(XY(entry.View.Body.position), entry.View.HitSphere.radius));
                StopMotion(entry.View.Body);
                entry.EdgePending = false;
                SetBodyMode(entry);
            }
        }

        /// <summary>
        /// Change only the board geometry, keeping identities and Host-request state intact.
        /// Positions and motion retain their normalized board units. A larger body's vertical
        /// clearance may clamp its position; a held gesture defers the entire resize instead.
        /// </summary>
        public bool TryResizeBounds(Rect worldCenterBounds)
        {
            if (!configured || heldId != null || worldCenterBounds.width <= 0f || worldCenterBounds.height <= 0f
                || !Finite(worldCenterBounds.min) || !Finite(worldCenterBounds.max)) return false;
            return TryResizeArea(LabOrbAreaShape.FromCenterBounds(worldCenterBounds, config.OrbRadius));
        }

        public bool TryResizeArea(LabOrbAreaShape shape)
        {
            if (!configured || heldId != null || shape == null) return false;
            Rect worldCenterBounds = shape.CenterBounds;
            float widthScale = worldCenterBounds.width / bounds.width;
            if (!Finite(widthScale) || widthScale <= 0f) return false;
            float additionalRadius = Mathf.Max(0f, config.DrumRadius - config.OrbRadius);
            foreach (var entry in entries.Values)
            {
                if (entry.View == null) continue;
                additionalRadius = Mathf.Max(additionalRadius, entry.View.HitSphere.radius - config.OrbRadius);
                if (!shape.CanFitRadius(entry.View.HitSphere.radius)) return false;
                if (!entry.View.Body.isKinematic && !Finite(XY(entry.View.Body.linearVelocity) * widthScale)) return false;
            }
            if (worldCenterBounds.height < additionalRadius * 2f) return false;
            if (!shape.CanFitRadius(Mathf.Max(config.DrumRadius, config.OrbRadius))) return false;
            if (areaShape.ApproximatelyEquals(shape)) return true;

            LabOrbAreaShape previous = areaShape;
            areaShape = shape;
            bounds = worldCenterBounds;
            foreach (var entry in entries.Values)
            {
                if (entry.View == null) continue;
                Rigidbody body = entry.View.Body;
                Vector2 point = XY(body.position);
                float radius = entry.View.HitSphere.radius;
                Vector2 normalized = NormalizeForBody(previous, point, radius);
                Vector3 velocity = body.linearVelocity;
                bool sleeping = !body.isKinematic && body.IsSleeping();
                body.position = OnPlane(ToWorld(normalized, radius));
                // Kinematic bodies already hold a zero velocity. Do not reset their pending
                // transfer/throw/combine state or change collision mode while remapping them.
                if (!body.isKinematic)
                {
                    body.linearVelocity = OnPlane(XY(velocity) * widthScale);
                    if (sleeping) body.Sleep();
                }
            }
            BuildHorizontalWalls();
            return true;
        }

        /// <summary>
        /// Limit only where a new pointer gesture may begin. A gesture already holding an orb
        /// continues outside this rectangle so it can still pass an edge or release a throw.
        /// </summary>
        /// <summary>조합판의 화면 영역(픽셀). 콤보 보상 별(#257)이 이 위쪽 가장자리까지 떨어진다.</summary>
        public bool TryGetInputScreenRect(out Rect screenRect)
        {
            screenRect = inputScreenRect;
            return hasInputScreenRect;
        }

        public void SetInputScreenRect(Rect screenRect)
        {
            if (screenRect.width <= 0f || screenRect.height <= 0f
                || !Finite(screenRect.min) || !Finite(screenRect.max))
                throw new ArgumentException("A finite positive input screen rectangle is required.", nameof(screenRect));
            inputScreenRect = screenRect;
            hasInputScreenRect = true;
        }

        private bool CanStartPointerAtScreen(Vector2 screenPoint)
        {
            if (hasInputScreenRect && (screenPoint.x < inputScreenRect.xMin || screenPoint.x > inputScreenRect.xMax
                || screenPoint.y < inputScreenRect.yMin || screenPoint.y > inputScreenRect.yMax)) return false;
            return areaShape == null || !areaShape.IsCurved
                || (TryScreenToWorld(screenPoint, out Vector2 point) && areaShape.Contains(point, 0f));
        }

        /// <summary>
        /// Add a newly approved orb, or update its kind/owner. Existing local physics is never
        /// reset by a repeated snapshot. Positions are 0..1; velocity is board widths/second.
        /// justUnlocked plays the unlock effect once per orb, however many snapshots repeat it.
        /// A small orb, shrunk by a bomb blast (#213), is smaller to see, to grab and to bump into;
        /// burnSeconds is how much longer that blast's fire burns on it.
        /// </summary>
        public LabOrbView UpsertOrb(string id, LabOrbKind kind, Vector2 normalizedPosition,
            Vector2 velocityInBoardWidthsPerSecond = default, bool localOwner = true,
            bool playSpawn = false, LabOrbElement element = LabOrbElement.None, int lockSwipes = 0,
            int relayStage = 0, float fuseSeconds = -1f, bool justUnlocked = false,
            LabDrumArrival arrival = LabDrumArrival.None, float stopAt = -1f, bool small = false,
            float burnSeconds = 0f, float lifeSeconds = -1f, float jigwiScale = 1f, bool fever = false, bool fiveColor = false,
            float jigwiABScale = 1f, float jigwiABBurnSeconds = 0f, bool jigwiABHeld = false)
        {
            RequireConfigured();
            small &= !kind.IsDrum();
            jigwiScale = kind.IsRaw() && !float.IsNaN(jigwiScale) && !float.IsInfinity(jigwiScale)
                ? Mathf.Clamp(jigwiScale, .1f, 1f) : 1f;
            jigwiABScale = (kind.IsRaw() || kind == LabOrbKind.Combined) && Finite(jigwiABScale)
                ? Mathf.Clamp(jigwiABScale, .2f, 1f) : 1f;
            if (string.IsNullOrWhiteSpace(id) || !Finite(normalizedPosition) || !Finite(velocityInBoardWidthsPerSecond))
                throw new ArgumentException("A finite orb identity, position and velocity are required.");
            if (entries.TryGetValue(id, out var existing))
            {
                // Old snapshots must not put materials or a locally launched orb back on screen
                // while the command's explicit result is still outstanding.
                if (existing.HiddenForPending) return existing.View;
                if (existing.LocalOwner != localOwner) ResetRecovery(existing);
                existing.LocalOwner = localOwner;
                existing.View.FuseTotalSeconds = config.ItemLifetimeSeconds;
                if (existing.View.Kind != kind || existing.View.Element != element || existing.Small != small
                    || !Mathf.Approximately(existing.JigwiScale, jigwiScale)
                    || !Mathf.Approximately(existing.JigwiABScale, jigwiABScale))
                {
                    bool shrinking = small && !existing.Small && existing.View.Kind == kind;
                    // 2:2 fever (#281): a Yin or Yang the Host turned into a team orb swells and settles as it changes.
                    bool feverTurned = existing.View.Kind.IsRaw() && kind == LabOrbKind.Combined;
                    existing.Small = small;
                    existing.JigwiScale = jigwiScale;
                    existing.JigwiABScale = jigwiABScale;
                    existing.View.Configure(id, kind, RadiusFor(kind, small, jigwiScale, jigwiABScale), element);
                    if (shrinking) existing.View.PlayShrink(1f / config.BlastShrinkScale, ShrinkSeconds);
                    else if (feverTurned) existing.View.PlayShrink(FeverTurnScale, FeverTurnSeconds);
                    existing.View.Body.mass = kind == LabOrbKind.Bomb ? config.BombMass : 1f;
                    SetLayerRecursively(existing.View.transform, this.gameObject.layer);
                }
                if (existing.View.LockSwipes != lockSwipes || existing.View.RelayStage != relayStage
                    || existing.View.FuseSeconds != fuseSeconds)
                    existing.View.SetItemState(lockSwipes, relayStage, fuseSeconds);
                if (justUnlocked && unlockEffectIds.Add(id)) existing.View.PlayUnlockEffect();
                existing.View.SetBurn(burnSeconds);
                existing.View.SetJigwiABBurn(jigwiABBurnSeconds, jigwiABHeld);
                existing.View.SetLife(lifeSeconds);
                existing.View.SetFever(fever);
                existing.View.SetFiveColor(fiveColor);
                if (fiveColor) fiveColorIds.Add(id); else fiveColorIds.Remove(id);
                SetBodyMode(existing);
                return existing.View;
            }

            Vector2 start = ToWorld(normalizedPosition, RadiusFor(kind, small, jigwiScale, jigwiABScale));
            Vector2 popFrom = start;
            if (playSpawn) start = OutsideDrums(start, velocityInBoardWidthsPerSecond, out popFrom);
            start = Clamp(start, RadiusFor(kind, small, jigwiScale, jigwiABScale));
            var gameObject = new GameObject("Orb " + id);
            gameObject.layer = this.gameObject.layer;
            gameObject.transform.SetParent(transform, false);
            gameObject.transform.position = OnPlane(start);
            var view = gameObject.AddComponent<LabOrbView>();
            view.Configure(id, kind, RadiusFor(kind, small, jigwiScale, jigwiABScale), element);
            view.ConfigureAppearance(config.OrbAppearance);
            view.FuseTotalSeconds = config.ItemLifetimeSeconds;
            view.SetItemState(lockSwipes, relayStage, fuseSeconds);
            if (justUnlocked && unlockEffectIds.Add(id)) view.PlayUnlockEffect();
            view.SetBurn(burnSeconds);
            view.SetJigwiABBurn(jigwiABBurnSeconds, jigwiABHeld);
            view.SetLife(lifeSeconds);
            view.SetFever(fever);
            view.SetFiveColor(fiveColor);
            if (fiveColor) fiveColorIds.Add(id);
            SetLayerRecursively(gameObject.transform, gameObject.layer);
            view.HitSphere.sharedMaterial = contactMaterial;
            view.HitOrb += (other, speed) => OnOrbHit(view, other, speed);
            var body = view.Body;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // A bomb is heavy (#212): orbs bump off it rather than shoving it.
            body.mass = kind == LabOrbKind.Bomb ? config.BombMass : 1f;
            body.position = OnPlane(start);
            var entry = new Entry { View = view, LocalOwner = localOwner, DriftSeed = DriftSeedFor(id),
                Dropping = expectedDrops.Remove(id), Small = small, JigwiScale = jigwiScale, JigwiABScale = jigwiABScale };
            entries.Add(id, entry);
            if (kind == LabOrbKind.FoxFire)
            {
                // Safe for a moment after it lands, so one falling among orbs is not an unfair loss (#251).
                LabOrbAppearance look = config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
                entry.FoxSafeUntil = Time.time + config.FoxFireSafeSeconds
                    + (entry.Dropping ? look.DropFallSeconds + look.DropBounceSeconds : 0f);
            }
            SetBodyMode(entry);
            if (entry.Dropping) PlayDrop(entry, start);
            Vector3 initialVelocity = OnPlane(Vector2.ClampMagnitude(velocityInBoardWidthsPerSecond * bounds.width,
                config.OrbMaxReleaseSpeed * bounds.width));
            if (!body.isKinematic) body.linearVelocity = initialVelocity;
            else if (simulationHeld && localOwner)
            {
                entry.SimulationHoldCaptured = true;
                entry.SimulationHoldVelocity = initialVelocity;
                entry.SimulationHoldAngularVelocity = Vector3.zero;
            }
            // Starts from the speed it came in with, set just above. An unlocked bomb (#328) glides in like a drum.
            if (arrival != LabDrumArrival.None && (kind.IsDrum() || kind == LabOrbKind.Bomb) && !body.isKinematic)
                StartArrival(entry, arrival, stopAt, normalizedPosition.x);
            if (playSpawn && spawnedIds.Add(id))
            {
                SpawnStarted?.Invoke(id);
                // The orb flies on its launch at once, so a burst never waits. For the short pop it
                // passes through other orbs and drums (walls still stop it), so orbs leaving together
                // cannot jam on each other or shove the drum.
                var passedThrough = new List<Collider>();
                foreach (var other in entries.Values)
                {
                    if (other == entry || other.View == null) continue;
                    Physics.IgnoreCollision(view.HitSphere, other.View.HitSphere, true);
                    passedThrough.Add(other.View.HitSphere);
                }
                view.PlaySpawnPop(gameObject.transform.InverseTransformVector(OnPlane(popFrom - start)),
                    config.SpawnPopStartScale, config.SpawnPopDuration, () =>
                    {
                        if (view == null || view.HitSphere == null) return;
                        foreach (Collider other in passedThrough)
                            if (other != null) Physics.IgnoreCollision(view.HitSphere, other, false);
                    });
            }
            return view;
        }

        /// <summary>
        /// A generated orb starts at its drum's centre on the Host. Begin its body just outside that
        /// drum, along its launch direction, so the two never start overlapped and burst apart;
        /// drumCenter is where its art pops out from.
        /// </summary>
        private Vector2 OutsideDrums(Vector2 start, Vector2 launch, out Vector2 drumCenter)
        {
            Vector2 direction = launch.sqrMagnitude > 1e-8f ? launch.normalized : Vector2.up;
            foreach (var entry in entries.Values)
            {
                if (entry.View == null || !entry.View.Kind.IsDrum()) continue;
                Vector2 center = XY(entry.View.Body.position);
                float clearance = entry.View.HitSphere.radius + config.OrbRadius;
                if ((start - center).sqrMagnitude >= clearance * clearance) continue;
                drumCenter = center;
                return center + direction * clearance;
            }
            drumCenter = start;
            return start;
        }

        /// <summary>
        /// A locked bomb blasted on this board. The bomb leaves in a cartoon blast, and every orb and drum whose
        /// centre is within the blast radius is flung straight away from it, hardest right beside it
        /// and fading to nothing at the edge. Nothing is removed; an orb flung past a side edge goes
        /// to the neighbour through the usual Host-approved handoff. An orb held under a finger is
        /// knocked out of the hand too. Orbs already waiting on a Host answer stay put. The blast
        /// centre is where this screen shows the bomb, or the Host's last position for it if it is
        /// gone. Returns how many bodies were pushed. Every flung orb that is not a drum or a bomb is
        /// added to <paramref name="burned"/>, for the Host to set alight (#213).
        /// </summary>
        public int Blast(string bombId, Vector2 normalizedPosition, ICollection<string> burned = null)
        {
            if (!configured || !Finite(normalizedPosition)) return 0;
            Vector2 center = bombId != null && entries.TryGetValue(bombId, out var bomb) && bomb.View != null
                ? XY(bomb.View.Body.position) : ToWorld(normalizedPosition, config.OrbRadius);
            if (bombId != null) RemoveOrb(bombId);
            float reach = config.BombBlastRadius * bounds.width;
            float strength = config.BombBlastStrength * bounds.width;
            // The cartoon blast shows even when the push is tuned off.
            LabBombBlastEffect.Play(transform, center, reach > 0f ? reach : .4f * bounds.width);
            if (reach <= 0f || strength <= 0f) return 0;

            int pushed = 0;
            updateEntries.Clear();
            updateEntries.AddRange(entries.Values);
            foreach (var entry in updateEntries)
            {
                if (entry.View == null || !entry.LocalOwner || entry.Locked || entry.EdgePending || entry.Dropping
                    || entry.HiddenForPending) continue;
                Vector2 away = XY(entry.View.Body.position) - center;
                float distance = away.magnitude;
                if (distance > reach) continue;
                if (entry.Held) CancelDrag();
                entry.Arrival = LabDrumArrival.None;
                entry.PlayerSent = false;
                // Anything flung tells the Host it was pushed if it crosses: a drum then stops inside the
                // next board (#178), and the gourd's swipe block never stops it (#215).
                entry.Blasted = true;
                if (!entry.View.Kind.IsDrum() && entry.View.Kind != LabOrbKind.Bomb) burned?.Add(entry.View.OrbId);
                Vector2 direction = distance > 1e-5f ? away / distance : Vector2.up;
                Vector2 velocity = XY(entry.View.Body.linearVelocity) + direction * strength * (1f - distance / reach);
                entry.View.Body.linearVelocity = OnPlane(velocity);
                pushed++;
            }
            return pushed;
        }

        /// <summary>
        /// The gourd's swipe block on this player (#215): side -1 holds the left edge shut, +1 the right,
        /// 0 (or no seconds) lifts it. While it is on, this board's own passes that way bounce off the edge
        /// like a wall; a blast's push and a drum running on through still cross, and nothing is stopped
        /// coming in. The edge glows red with the seconds left in its middle. The board counts down by
        /// itself and only takes the Host's seconds when they differ by more than a moment.
        /// </summary>
        private LabCharmEdgeFlash charmEdge;
        private bool charmed;

        /// <summary>This board's player is charmed (#251): an upward swipe is only a flick that bounces off the top.</summary>
        public bool Charmed => charmed;
        public LabCharmEdgeFlash CharmEdge => charmEdge;

        /// <summary>
        /// 매혹 (#251): 이 판의 주인이 헤롱헤롱해 공격(위로 던지기)을 못 한다. secondsLeft가 0보다 크면 걸린 것이고, 던지는 쪽
        /// 가장자리(판 위쪽 곡선)가 빨갛게 은은히 빛난다. 풀리면 부드럽게 사라진다.
        /// </summary>
        public void SetCharm(float secondsLeft)
        {
            if (configured && secondsLeft > 0f && !float.IsInfinity(secondsLeft))
            {
                charmed = true;
                if (charmEdge == null) charmEdge = LabCharmEdgeFlash.Create(transform);
                charmEdge.Show(areaShape.UpperBoundary, bounds.width);
                return;
            }
            if (!charmed) return;
            charmed = false;
            if (charmEdge != null) charmEdge.Release();
        }

        /// <summary>
        /// 2:2 (#281): close one side for the whole battle. Swipes, flicks, blast pushes and drums passing on all
        /// bounce off it like the top and bottom walls; only the teammate's side passes.
        /// </summary>
        public void SetTeamWall(int side)
        {
            side = configured ? Math.Sign(side) : 0;
            if (side == teamWallSide && (side == 0 || teamWall != null && teamWall.Visible)) return;
            teamWallSide = side;
            if (side != 0) ShowTeamWall();
            else if (teamWall != null) teamWall.Hide();
        }

        private void ShowTeamWall()
        {
            if (teamWall == null) teamWall = LabOrbTeamWall.Create(transform);
            float edgeX = teamWallSide > 0 ? bounds.xMax : bounds.xMin;
            Vector2 range = areaShape.VerticalRange(edgeX, 0f);
            teamWall.Show(teamWallSide > 0, edgeX, range.x, range.y, bounds.width);
        }

        public void SetSwipeBlock(int side, float secondsLeft)
        {
            side = Math.Sign(side);
            if (!configured || side == 0 || !(secondsLeft > 0f) || float.IsInfinity(secondsLeft))
            {
                LiftSwipeBlock();
                return;
            }
            if (side != blockedSide || Mathf.Abs(blockSecondsLeft - secondsLeft) > BlockResyncSeconds)
                blockSecondsLeft = secondsLeft;
            blockedSide = side;
            ShowSwipeBlock();
        }

        private void LiftSwipeBlock()
        {
            blockedSide = 0;
            blockSecondsLeft = 0f;
            if (edgeBlock != null) edgeBlock.Hide();
        }

        private void ShowSwipeBlock()
        {
            if (edgeBlock == null) edgeBlock = LabOrbEdgeBlock.Create(transform);
            // The side planes are the board's visible side edges; the area gives the edge's height there.
            float edgeX = blockedSide > 0 ? bounds.xMax : bounds.xMin;
            Vector2 range = areaShape.VerticalRange(edgeX, 0f);
            edgeBlock.Show(blockedSide > 0, edgeX, range.x, range.y, bounds.width, config.OrbRadius);
            edgeBlock.SetSeconds(blockSecondsLeft);
        }

        private void LateUpdate()
        {
            ApplyComboLook();
            ApplyFlashGlare();
            SyncBoardTrails();
            // Follows the board if its area was resized.
            if (teamWallSide != 0) ShowTeamWall();
            if (blockedSide == 0) return;
            blockSecondsLeft -= Time.deltaTime;
            if (blockSecondsLeft <= 0f) LiftSwipeBlock();
            // Follows the board if its area was resized.
            else ShowSwipeBlock();
        }

        /// <summary>
        /// 이 기기 플레이어의 콤보 효과 (#257): -1 SMALL(페널티), 1 BIG(보상), 0 없음. 콤보 HUD가 매 프레임 정한다.
        /// 판의 음양·결합 구슬 그림이 이 동안 작아지거나 커진다(물리 크기는 그대로).
        /// </summary>
        public void SetComboLook(int look) => comboLook = Math.Sign(look);

        private float flashGlare;

        /// <summary>
        /// 야광귀 섬광 (#268): 이 기기 플레이어가 눈부신 정도 (0~1). 섬광 화면이 매 프레임 정한다.
        /// 판의 음양·결합 구슬과 아이템(여우구슬 포함)이 이만큼 하얗게 보이고, 북은 그대로다. 조작·물리는 그대로다.
        /// </summary>
        public void SetFlashGlare(float amount) =>
            flashGlare = float.IsNaN(amount) || float.IsInfinity(amount) ? 0f : Mathf.Clamp01(amount);
        public float FlashGlare => flashGlare;

        private void ApplyFlashGlare()
        {
            foreach (var entry in entries.Values)
                if (entry.View != null)
                    entry.View.SetGlare(entry.View.Kind.IsDrum() || flashRevealed.Contains(entry.View.OrbId) ? 0f : flashGlare);
        }

        // 흔들어 풀기 (#330): 이번 섬광에서 색을 되찾은 구슬. 이 구슬들만 하얗지 않고, 나중에 생긴 구슬은 하얗게 나온다.
        private readonly HashSet<string> flashRevealed = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>아직 하얗게 덮여 색을 되찾지 못한 판 구슬 수 (북 제외, 아이템 포함)</summary>
        public int FlashWhiteCount => entries.Values.Count(entry => entry.View != null && !entry.View.Kind.IsDrum()
            && !flashRevealed.Contains(entry.View.OrbId));
        /// <summary>이번 섬광에서 색을 되찾은 구슬 수</summary>
        public int FlashRevealedCount => flashRevealed.Count;
        /// <summary>이 구슬이 이번 섬광에서 색을 되찾았는지</summary>
        public bool FlashRevealed(string orbId) => orbId != null && flashRevealed.Contains(orbId);

        /// <summary>새 섬광: 모든 구슬을 다시 하얗게 덮을 수 있게 되찾은 목록을 비운다.</summary>
        public void ResetFlashReveal() => flashRevealed.Clear();

        /// <summary>
        /// 한 번 흔들 때마다 (#330): 아직 하얀 구슬 하나를 무작위로 골라 색을 돌려주고 톡 튀게 한다. 돌려준 구슬 ID, 없으면 null.
        /// 화면 표시만 바꾸며 규칙·물리는 그대로다.
        /// </summary>
        public string RevealFlashOrb(float popScale, float popSeconds)
        {
            Entry[] white = entries.Values.Where(entry => entry.View != null && !entry.View.Kind.IsDrum()
                && !flashRevealed.Contains(entry.View.OrbId)).ToArray();
            if (white.Length == 0) return null;
            Entry pick = white[UnityEngine.Random.Range(0, white.Length)];
            flashRevealed.Add(pick.View.OrbId);
            pick.View.SetGlare(0f);
            if (popScale > 0f && popSeconds > 0f && pick.View.isActiveAndEnabled)
                pick.View.PlaySpawnPop(Vector3.zero, popScale, popSeconds);
            return pick.View.OrbId;
        }

        /// <summary>다 흔들었다: 남은 하얀 구슬을 한꺼번에 돌려준다 (#330, 최대 횟수를 채웠을 때).</summary>
        public void RevealAllFlashOrbs()
        {
            foreach (var entry in entries.Values)
                if (entry.View != null && !entry.View.Kind.IsDrum()) flashRevealed.Add(entry.View.OrbId);
        }
        public int ComboLook => comboLook;
        /// <summary>판 구슬이 지금 콤보 효과로 향하는 그림 배율 (#257)</summary>
        public float ComboLookScale => comboLook < 0 ? comboSmallScale : comboLook > 0 ? comboBigScale : 1f;

        private void ApplyComboLook()
        {
            float target = ComboLookScale;
            foreach (var entry in entries.Values)
            {
                LabOrbView view = entry.View;
                if (view == null) continue;
                bool sized = view.Kind.IsRaw() || view.Kind == LabOrbKind.Combined;
                view.SetComboScaleTarget(sized ? target : 1f);
                view.StepComboScale(Time.deltaTime, comboScaleSeconds);
            }
        }

        /// <summary>
        /// 결합(오행) 구슬의 얕은 꼬리 (#254). DEV `Board Orb Trail`이 켜져 있으면 결합 구슬마다 꼬리를 붙이고,
        /// 던진 공 대비 실제 판 구슬 반지름(축소 상태 포함)으로 줄인다. 숨긴 요청 대기 구슬에는 남기지 않는다.
        /// </summary>
        private void SyncBoardTrails()
        {
            if (!configured || config == null) return;
            bool on = config.BoardOrbTrailEnabled;
            foreach (var entry in entries.Values)
            {
                LabOrbView view = entry.View;
                if (view == null) continue;
                var trail = view.GetComponent<LabOrbBoardTrail>();
                bool wanted = on && !entry.HiddenForPending && view.gameObject.activeInHierarchy
                    && view.Kind == LabOrbKind.Combined && view.Element != LabOrbElement.None;
                if (trail == null)
                {
                    if (!wanted) continue;
                    trail = view.gameObject.AddComponent<LabOrbBoardTrail>();
                }
                float radius = view.HitSphere != null ? view.HitSphere.radius : config.OrbRadius;
                // #257 콤보 SMALL/BIG 그림 배율도 꼬리에 곱한다 (물리 반지름은 그대로라 따로 곱함)
                trail.Sync(view, config.OrbAppearance, wanted,
                    radius / Mathf.Max(.001f, config.ThrowRadius) * view.ComboScaleTarget);
            }
        }

        /// <summary>The fewest seconds left on a burning bomb fuse on this screen; negative when none burns.</summary>
        public float ShortestFuse()
        {
            float shortest = -1f;
            foreach (var entry in entries.Values)
            {
                if (entry.View == null || !entry.LocalOwner || entry.View.Kind != LabOrbKind.Bomb) continue;
                float left = entry.View.FuseSeconds;
                if (left >= 0f && (shortest < 0f || left < shortest)) shortest = left;
            }
            return shortest;
        }

        /// <summary>
        /// The gourd is dropping this bomb onto this screen (#177). Call before its snapshot arrives:
        /// when the bomb first appears it falls in from above the orb area and bounces. Until it lands it cannot be grabbed or pushed and nothing
        /// bumps into it.
        /// </summary>
        public void ExpectDrop(string bombId)
        {
            if (!string.IsNullOrEmpty(bombId) && !entries.ContainsKey(bombId)) expectedDrops.Add(bombId);
        }

        public bool IsDropping(string id) => id != null && entries.TryGetValue(id, out var entry) && entry.Dropping;

        /// <summary>How a drum handed to this board is still arriving; None once it rests or the player takes it.</summary>
        public LabDrumArrival ArrivalOf(string id) =>
            id != null && entries.TryGetValue(id, out var entry) ? entry.Arrival : LabDrumArrival.None;

        /// <summary>
        /// A drum just handed to this board (#178), entering by the side nearer entryX01 with the
        /// speed it was swiped at. Stop: it glides on and comes to rest on the Host's spot, stopAt of
        /// the board width in from that side; a slow drum is sped up to get there within
        /// ArrivalMaxSeconds and a fast one slowed to brake no harder than ArrivalMaxBrake. Pass: it
        /// runs on across to the next player at no less than PassMinSpeed. Coast (flung across by a
        /// blast): it keeps the speed the blast gave it and slows like any drum, but brakes harder
        /// when it must to stop no farther than stopAt in.
        /// </summary>
        private void StartArrival(Entry entry, LabDrumArrival arrival, float stopAt, float entryX01)
        {
            if (arrival == LabDrumArrival.None) return;
            entry.Arrival = arrival;
            entry.ArrivalDirection = entryX01 < .5f ? 1f : -1f;
            var body = entry.View.Body;
            Vector2 velocity = XY(body.linearVelocity);
            float along = velocity.x * entry.ArrivalDirection;
            if (arrival != LabDrumArrival.Pass)
            {
                float share = Mathf.Clamp01(Finite(stopAt) ? stopAt : .5f);
                entry.ArrivalStopX = entry.ArrivalDirection > 0f
                    ? bounds.xMin + share * bounds.width : bounds.xMax - share * bounds.width;
            }
            if (arrival == LabDrumArrival.Coast) return;
            if (arrival == LabDrumArrival.Pass)
                along = Mathf.Max(along, PassMinSpeed * bounds.width);
            else
            {
                float distance = Mathf.Max(0f, (entry.ArrivalStopX - XY(body.position).x) * entry.ArrivalDirection);
                float slowest = 2f * distance / ArrivalMaxSeconds;
                float fastest = Mathf.Sqrt(2f * ArrivalMaxBrake * bounds.width * distance);
                along = Mathf.Clamp(along, slowest, Mathf.Max(slowest, fastest));
            }
            body.linearVelocity = OnPlane(new Vector2(entry.ArrivalDirection * along, velocity.y));
        }

        /// <summary>
        /// One physics step of an arriving drum. Stop brakes just hard enough to come to rest on its
        /// spot (v² = 2·a·d, recomputed each step so a bump on the way is absorbed), then hands it
        /// back to its usual drift. Pass keeps running on. Up and down motion slows like an orb's.
        /// </summary>
        private Vector2 Arrive(Entry entry, Vector2 velocity, Vector2 position)
        {
            entry.View.DriveDrumSpin(0f, config.DrumSpinStrength);
            float side = entry.ArrivalDirection;
            float step = Time.fixedDeltaTime;
            float floorStep = config.OrbFloorDeceleration * bounds.width * step;
            float vertical = Mathf.MoveTowards(velocity.y, 0f, floorStep);
            float along = velocity.x * side;
            if (entry.Arrival == LabDrumArrival.Pass)
                return new Vector2(side * Mathf.Max(along - floorStep, PassMinSpeed * bounds.width), vertical);

            float remaining = (entry.ArrivalStopX - position.x) * side;
            if (entry.Arrival == LabDrumArrival.Coast)
            {
                // Slides on the blast's speed and slows like any drum; its drift takes over once it is
                // gentle. Only when that would carry it past its limit does it brake harder, to stop there.
                float driftSpeed = config.DrumDriftSpeed * bounds.width;
                if (remaining <= ArrivalTolerance * bounds.width || along <= Mathf.Max(driftSpeed, ArrivalRestSpeed * bounds.width))
                {
                    entry.Arrival = LabDrumArrival.None;
                    return new Vector2(remaining <= ArrivalTolerance * bounds.width ? 0f : velocity.x, vertical);
                }
                float limit = along * along / (2f * remaining);
                float slowing = Mathf.Max(config.OrbFloorDeceleration * bounds.width, limit);
                return new Vector2(side * Mathf.Max(0f, along - slowing * step), vertical);
            }
            if (remaining <= ArrivalTolerance * bounds.width || along <= ArrivalRestSpeed * bounds.width)
            {
                entry.Arrival = LabDrumArrival.None;
                return new Vector2(0f, vertical);
            }
            float brake = along * along / (2f * remaining);
            return new Vector2(side * Mathf.Max(0f, along - brake * step), vertical);
        }

        private void PlayDrop(Entry entry, Vector2 landing)
        {
            LabOrbAppearance look = config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            LabOrbView view = entry.View;
            float radius = view.HitSphere.radius;
            // It falls at once from just above the top of the orb area, straight down onto its spot;
            // its growing shadow marks where it will land (#212).
            Vector2 drop = new Vector2(0f, Mathf.Max(bounds.yMax + 2f * radius - landing.y, 2f * radius));
            string id = view.OrbId;
            view.PlayDrop(view.transform.InverseTransformVector(OnPlane(drop)),
                look.DropFallSeconds, look.DropBounceSeconds,
                view.transform.InverseTransformVector(OnPlane(Vector2.up * (look.DropBounceHeight * radius))).y,
                () =>
                {
                    if (!entries.TryGetValue(id, out var landed) || landed != entry || view == null) return;
                    entry.Dropping = false;
                    SetBodyMode(entry);
                    DropLanded?.Invoke(id);
                });
        }

        /// <summary>
        /// Show an immediate, physically movable combination without inventing the Host's random
        /// element. The two real inputs remain disabled until the command commits or is rejected.
        /// </summary>
        public bool TryCreateCombinePreview(string temporaryId, string firstId, string secondId,
            Vector2 center01, Vector2 velocity)
        {
            if (!configured || string.IsNullOrWhiteSpace(temporaryId) || entries.ContainsKey(temporaryId)
                || generationPreviews.ContainsKey(temporaryId) || firstId == secondId
                || !Finite(center01) || !Finite(velocity)
                || firstId == null || secondId == null
                || !entries.TryGetValue(firstId, out Entry first)
                || !entries.TryGetValue(secondId, out Entry second)
                || first.HiddenForPending || second.HiddenForPending
                || !first.LocalOwner || !second.LocalOwner
                || !first.View.Kind.IsRaw() || !second.View.Kind.IsRaw()
                || first.View.Kind == second.View.Kind) return false;

            var preview = new CombinePreview
            {
                FirstId = firstId, SecondId = secondId,
                FirstMotion = first.BeforeRequest ?? CaptureMotion(first),
                SecondMotion = second.BeforeRequest ?? CaptureMotion(second)
            };
            if (heldId == firstId || heldId == secondId) CancelDrag();
            HidePending(first);
            HidePending(second);
            LabOrbView view = UpsertOrb(temporaryId, LabOrbKind.Combined, center01, velocity,
                element: LabOrbElement.None, small: first.Small || second.Small,
                jigwiABScale: Mathf.Min(first.JigwiABScale, second.JigwiABScale));
            entries[temporaryId].Provisional = true;
            combinePreviews.Add(temporaryId, preview);
            return view != null;
        }

        /// <summary>
        /// Promote the same body to the approved identity. Its current local motion, held gesture,
        /// and any subsequent pending transfer/launch survive the acknowledgement.
        /// </summary>
        public bool CommitCombinePreview(string temporaryId, LabOrbState approved)
        {
            if (temporaryId == null || approved == null || string.IsNullOrWhiteSpace(approved.id)
                || approved.kind != LabOrbKind.Combined
                || !combinePreviews.TryGetValue(temporaryId, out CombinePreview preview)
                || !entries.TryGetValue(temporaryId, out Entry entry)) return false;

            combinePreviews.Remove(temporaryId);
            RemoveOrb(preview.FirstId);
            RemoveOrb(preview.SecondId);
            // A recovery snapshot may beat the receipt and already create the approved ID.
            // Keep the body the finger has been moving, rather than showing both copies.
            if (approved.id != temporaryId && entries.TryGetValue(approved.id, out Entry duplicate))
            {
                duplicate.View.gameObject.SetActive(false);
                RemoveOrb(approved.id);
            }
            entries.Remove(temporaryId);
            entries[approved.id] = entry;
            entry.Provisional = false;
            entry.Small = approved.small;
            entry.JigwiScale = approved.jigwiRadiusScale;
            entry.JigwiABScale = approved.jigwiABRadiusScale;
            entry.View.Configure(approved.id, approved.kind,
                RadiusFor(approved.kind, approved.small, approved.jigwiRadiusScale, approved.jigwiABRadiusScale), approved.element);
            entry.View.SetItemState(approved.lockSwipes, approved.relayStage, -1f);
            SetLayerRecursively(entry.View.transform, gameObject.layer);
            entry.View.gameObject.name = "Orb " + approved.id;
            if (heldId == temporaryId) heldId = approved.id;
            if (lastTapId == temporaryId) lastTapId = approved.id;
            if (entry.EdgePending)
                entry.PendingEdge = new LabOrbEdgeCrossing(approved.id, entry.PendingEdge.ToRight,
                    entry.PendingEdge.Height01, entry.PendingEdge.VelocityInBoardWidthsPerSecond, entry.PendingEdge.Kind);
            if (pendingLaunches.TryGetValue(temporaryId, out MotionState launch))
            {
                pendingLaunches.Remove(temporaryId);
                pendingLaunches[approved.id] = launch;
            }
            spawnedIds.Add(approved.id);
            return true;
        }

        public bool RejectCombinePreview(string temporaryId)
        {
            if (temporaryId == null || !combinePreviews.TryGetValue(temporaryId, out CombinePreview preview)) return false;
            combinePreviews.Remove(temporaryId);
            RemoveOrb(temporaryId);
            RestorePending(preview.FirstId, preview.FirstMotion);
            RestorePending(preview.SecondId, preview.SecondMotion);
            return true;
        }

        /// <summary>
        /// A neutral, collider-free request marker. It is deliberately not an orb: generation kind,
        /// color, launch direction and random draw remain Host decisions.
        /// </summary>
        public bool TryCreateGenerationPreview(string temporaryId, Vector2 center01)
        {
            if (!configured || string.IsNullOrWhiteSpace(temporaryId) || entries.ContainsKey(temporaryId)
                || generationPreviews.ContainsKey(temporaryId) || !Finite(center01)) return false;
            var root = new GameObject("Pending generation " + temporaryId);
            root.layer = gameObject.layer;
            root.transform.SetParent(transform, false);
            root.transform.position = OnPlane(ToWorld(center01, config.OrbRadius)) - Vector3.forward * .02f;
            var ring = root.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = config.OrbRadius * .12f;
            ring.startColor = ring.endColor = new Color(.88f, .88f, .88f, .7f);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * config.OrbRadius);
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material material = shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : null;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(.88f, .88f, .88f, .7f));
                if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(.88f, .88f, .88f, .7f));
                ring.sharedMaterial = material;
            }
            generationPreviews.Add(temporaryId, new GenerationPreview { Root = root, Material = material });
            StartCoroutine(AnimateGenerationPreview(temporaryId, root));
            return true;
        }

        private IEnumerator AnimateGenerationPreview(string id, GameObject root)
        {
            float elapsed = 0f;
            while (root != null && generationPreviews.ContainsKey(id))
            {
                elapsed += Time.deltaTime;
                float size = Mathf.Lerp(.3f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.01f, config.SpawnPopDuration)));
                root.transform.localScale = Vector3.one * size;
                yield return null;
            }
        }

        public bool RemoveGenerationPreview(string temporaryId)
        {
            if (temporaryId == null || !generationPreviews.TryGetValue(temporaryId, out GenerationPreview preview)) return false;
            generationPreviews.Remove(temporaryId);
            if (preview.Root != null) Destroy(preview.Root);
            if (preview.Material != null) Destroy(preview.Material);
            return true;
        }

        /// <summary>Keep the launch's board body for an explicit rejection, but remove it from view and collision now.</summary>
        public bool BeginPendingLaunch(string id)
        {
            if (id == null || !entries.TryGetValue(id, out Entry entry)
                || !entry.LocalOwner || entry.HiddenForPending || pendingLaunches.ContainsKey(id)) return false;
            pendingLaunches.Add(id, entry.BeforeRequest ?? CaptureMotion(entry));
            if (heldId == id) CancelDrag();
            HidePending(entry);
            return true;
        }

        /// <summary>Reversible immediate burn preview. Repeated snapshot/receipt calls cannot replay it.</summary>
        public bool PlayJigwiBurnPreview(string firstId, string secondId = null)
        {
            bool played = PlayJigwiBurnOne(firstId);
            if (secondId != null && secondId != firstId) played |= PlayJigwiBurnOne(secondId);
            return played;
        }

        private bool PlayJigwiBurnOne(string id)
        {
            if (id == null || !entries.TryGetValue(id, out Entry entry) || entry.View == null
                || entry.View.Kind.IsDrum() || entry.HiddenForPending || !entry.LocalOwner) return false;
            Vector3 point = entry.View.transform.position;
            float radius = entry.View.HitSphere.radius;
            if (!BeginPendingLaunch(id)) return false;
            LabJigwiBurnEffect.Play(transform, point, radius, config != null ? config.OrbAppearance : null);
            return true;
        }

        /// <summary>Failed generation still spends its cost, but burns a presentation instead of hiding the drum.</summary>
        public void PlayJigwiGenerationBurn(Vector2 position01)
        {
            if (!configured || !Finite(position01)) return;
            LabJigwiBurnEffect.Play(transform, OnPlane(ToWorld(position01, config.OrbRadius)),
                config.OrbRadius, config.OrbAppearance);
        }

        public bool RejectPendingLaunch(string id)
        {
            if (id == null || !pendingLaunches.TryGetValue(id, out MotionState motion)) return false;
            pendingLaunches.Remove(id);
            return RestorePending(id, motion);
        }

        public bool CommitPendingLaunch(string id)
        {
            if (id == null || !pendingLaunches.ContainsKey(id)) return false;
            pendingLaunches.Remove(id);
            return RemoveOrb(id);
        }

        public bool IsProvisional(string id) => id != null
            && ((entries.TryGetValue(id, out Entry entry) && entry.Provisional) || generationPreviews.ContainsKey(id));

        /// <summary>Appearance already known locally, including a provisional combination's inherited size.</summary>
        private readonly HashSet<string> fiveColorIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>이 판 구슬이 섬광 파훼 보상 오색구슬인지 (#329). 던질 때 투사체도 오색구슬 모습이 된다.</summary>
        public bool IsFiveColor(string id) => id != null && fiveColorIds.Contains(id);

        public bool TryGetLocalAppearance(string id, out LabOrbElement element, out bool small)
        {
            element = LabOrbElement.None;
            small = false;
            if (id == null || !entries.TryGetValue(id, out Entry entry)) return false;
            element = entry.View.Element;
            small = entry.Small;
            return true;
        }

        public bool IsSnapshotSuppressed(string id) => id != null
            && entries.TryGetValue(id, out Entry entry) && entry.HiddenForPending;

        public bool ShouldPreserveFromSnapshot(string id) => id != null
            && entries.TryGetValue(id, out Entry entry) && (entry.Provisional || entry.HiddenForPending);

        private MotionState CaptureMotion(Entry entry) => new MotionState(
            NormalizeForBody(areaShape, XY(entry.View.Body.position), entry.View.HitSphere.radius),
            XY(entry.View.Body.linearVelocity) / bounds.width);

        private void HidePending(Entry entry)
        {
            entry.Locked = true;
            entry.HiddenForPending = true;
            StopMotion(entry.View.Body);
            SetBodyMode(entry);
            entry.View.gameObject.SetActive(false);
        }

        private bool RestorePending(string id, MotionState motion)
        {
            if (!entries.TryGetValue(id, out Entry entry)) return false;
            entry.HiddenForPending = false;
            entry.Locked = false;
            entry.EdgePending = false;
            entry.PendingPartnerId = null;
            entry.BeforeRequest = null;
            entry.View.gameObject.SetActive(true);
            entry.View.Body.position = OnPlane(ToWorld(motion.Position01, entry.View.HitSphere.radius));
            SetBodyMode(entry);
            if (!entry.View.Body.isKinematic)
                entry.View.Body.linearVelocity = OnPlane(motion.Velocity * bounds.width);
            return true;
        }

        /// <summary>Raised when an orb's lifetime ran out on this board and it went (#235).</summary>
        public event Action<string> OrbVanished;

        /// <summary>
        /// The Host says this orb's lifetime ran out (#235): it leaves this board, unlike an orb that is
        /// handed on, combined or thrown. Returns false when this board does not show it.
        /// </summary>
        public bool Vanish(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry) || entry.View == null) return false;
            // It goes up in a puff of smoke where this screen shows it (#235); a fox orb's is blue (#251).
            if (entry.View.Kind == LabOrbKind.FoxFire) PlayFoxSmoke(entry);
            else LabOrbVanishSmoke.Play(transform, entry.View.transform.position, entry.View.HitSphere.radius,
                config != null ? config.OrbAppearance : null);
            RemoveOrb(id);
            OrbVanished?.Invoke(id);
            return true;
        }

        public bool RemoveOrb(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            // Definitive removal discards the preview and its retained materials. A rejection
            // removes this record first and explicitly restores the surviving inputs instead.
            if (combinePreviews.TryGetValue(id, out CombinePreview preview))
            {
                combinePreviews.Remove(id);
                RemoveOrb(preview.FirstId);
                RemoveOrb(preview.SecondId);
            }
            if (heldId == id) CancelDrag();
            entries.Remove(id);
            pendingLaunches.Remove(id);
            Retire(entry);
            return true;
        }

        public void Clear()
        {
            CancelDrag();
            LiftSwipeBlock();
            SetTeamWall(0);
            charmed = false;
            jigwiOverheat = false;
            flashGlare = 0f;
            flashRevealed.Clear();
            if (charmEdge != null) charmEdge.Hide();
            foreach (var entry in entries.Values)
                Retire(entry);
            entries.Clear();
            spawnedIds.Clear();
            unlockEffectIds.Clear();
            expectedDrops.Clear();
            combinePreviews.Clear();
            pendingLaunches.Clear();
            foreach (GenerationPreview preview in generationPreviews.Values)
            {
                if (preview.Root != null) Destroy(preview.Root);
                if (preview.Material != null) Destroy(preview.Material);
            }
            generationPreviews.Clear();
            foreach (LabJigwiBurnEffect effect in GetComponentsInChildren<LabJigwiBurnEffect>())
                if (effect != null) Destroy(effect.gameObject);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (!enabled) { CancelDrag(); ResetRestingRecovery(); }
            interactionEnabled = enabled;
        }

        /// <summary>Opt-in start barrier: pin only this board's physics while the loading Canvas keeps updating.</summary>
        public void SetSimulationHeld(bool held)
        {
            if (simulationHeld == held) return;
            if (held) { CancelDrag(); ResetRestingRecovery(); }
            simulationHeld = held;
            foreach (var entry in entries.Values)
                if (entry.View != null) SetBodyMode(entry);
        }

        /// <summary>Block pointer gestures while a modal UI is open, without pausing orb physics.</summary>
        public void SetUiInputBlocked(bool blocked)
        {
            if (blocked) { CancelDrag(); ResetRestingRecovery(); }
            uiInputBlocked = blocked;
        }

        public bool SetOrbLocked(string id, bool locked)
        {
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            if (locked && heldId == id) CancelDrag();
            entry.Locked = locked;
            if (locked) { ResetRecovery(entry); StopMotion(entry.View.Body); }
            SetBodyMode(entry);
            return true;
        }

        /// <summary>
        /// A rejected transfer/throw/combination restores idle physics. If a combination
        /// had locked two materials, both become movable again.
        /// </summary>
        public bool RejectEdge(string id)
        {
            if (id != null && pendingLaunches.ContainsKey(id)) return RejectPendingLaunch(id);
            if (id != null && combinePreviews.ContainsKey(id)) return RejectCombinePreview(id);
            string rejectedPreview = null;
            foreach (KeyValuePair<string, CombinePreview> pair in combinePreviews)
                if (pair.Value.FirstId == id || pair.Value.SecondId == id) { rejectedPreview = pair.Key; break; }
            if (rejectedPreview != null) return RejectCombinePreview(rejectedPreview);
            if (id == null || !entries.TryGetValue(id, out var entry)) return false;
            if (entry.PendingPartnerId != null && entries.TryGetValue(entry.PendingPartnerId, out var partner))
            {
                partner.PendingPartnerId = null;
                partner.Locked = false;
                StopMotion(partner.View.Body);
                SetBodyMode(partner);
            }
            entry.PendingPartnerId = null;
            entry.EdgePending = false;
            entry.Locked = false;
            entry.Arrival = LabDrumArrival.None;
            entry.Blasted = false;
            entry.BeforeRequest = null;
            StopMotion(entry.View.Body);
            entry.View.Body.position = OnPlane(Clamp(XY(entry.View.Body.position), entry.View.HitSphere.radius));
            SetBodyMode(entry);
            return true;
        }

        public bool TryGetMotion(string id, out Vector2 normalizedPosition,
            out Vector2 velocityInBoardWidthsPerSecond)
        {
            normalizedPosition = velocityInBoardWidthsPerSecond = default;
            if (!configured || id == null || !entries.TryGetValue(id, out var entry)) return false;
            normalizedPosition = NormalizeForBody(areaShape, XY(entry.View.Body.position), entry.View.HitSphere.radius);
            velocityInBoardWidthsPerSecond = XY(entry.View.Body.linearVelocity) / bounds.width;
            return true;
        }

        public bool TryBeginDragAtWorld(Vector2 worldPoint, double now)
        {
            if (!configured || simulationHeld || !interactionEnabled || heldId != null || !Finite(worldPoint) || !Finite(now)) return false;
            if (areaShape.IsCurved && !areaShape.Contains(worldPoint, 0f)) return false;
            Entry nearest = null;
            // A real body under the finger wins over another orb's enlarged accessibility reach.
            // Within each tier retain the existing radius-relative ordering (including the drum).
            int bestTier = int.MaxValue;
            float best = 1f;
            foreach (var entry in entries.Values)
            {
                if (!entry.LocalOwner || entry.Locked || entry.EdgePending || entry.Held || entry.Dropping
                    || entry.HiddenForPending || UnlockedBomb(entry)) continue;
                float radius = entry.View.HitSphere.radius;
                float originalReach = radius * 1.25f;
                float reach = GripReach(entry);
                float distance = (XY(entry.View.Body.position) - worldPoint).sqrMagnitude;
                if (distance > reach * reach) continue;
                int tier = !EdgeControlsActive ? 0 : distance <= radius * radius ? 0
                    : distance <= originalReach * originalReach ? 1 : 2;
                if (tier > bestTier) continue;
                float rankedReach = tier < 2 ? originalReach : reach;
                float closeness = distance / (rankedReach * rankedReach);
                if (tier == bestTier && closeness > best) continue;
                bestTier = tier;
                best = closeness;
                nearest = entry;
            }
            if (nearest == null) return false;
            heldId = nearest.View.OrbId;
            nearest.Held = true;
            ResetRecovery(nearest);
            nearest.SafetyCombineGuardUntil = 0f;
            BeginTransferIntent(worldPoint, now);
            nearest.PlayerSent = false;
            nearest.LastDragAt = now;
            nearest.Arrival = LabDrumArrival.None;
            nearest.Blasted = false;
            nearest.PressVelocity = XY(nearest.View.Body.linearVelocity);
            // A drum is large, and a bomb's art is larger than its body: keep the grabbed point under the
            // finger instead of jumping the centre there (which would also jerk the bomb's roll).
            bool keepsGrabPoint = nearest.View.Kind.IsDrum() || nearest.View.Kind == LabOrbKind.Bomb;
            nearest.GrabOffset = keepsGrabPoint ? XY(nearest.View.Body.position) - worldPoint : Vector2.zero;
            StopMotion(nearest.View.Body);
            SetBodyMode(nearest);
            samples.Clear();
            AddSample(worldPoint, now);
            pressPoint = worldPoint;
            pressTime = now;
            return true;
        }

        public bool DragToWorld(Vector2 worldPoint, double now)
        {
            if (heldId == null || !entries.TryGetValue(heldId, out var entry) || !Finite(worldPoint) || !Finite(now))
                return false;
            Vector2 target = Clamp(worldPoint + entry.GrabOffset, entry.View.HitSphere.radius);
            // A heavy bomb (#212) trails the finger, closing the gap at a rate set by its drag lag
            // for however long passed since it last followed; anything else sticks to the finger.
            if (entry.View.Kind == LabOrbKind.Bomb && config.BombDragLag > 0f)
            {
                float passed = (float)Math.Max(0d, now - entry.LastDragAt);
                target = Vector2.Lerp(XY(entry.View.Body.position), target, 1f - Mathf.Exp(-passed / config.BombDragLag));
            }
            entry.LastDragAt = now;
            UpdateTransferIntent(worldPoint, now);
            entry.View.Body.position = OnPlane(target);
            AddSample(worldPoint, now);
            return true;
        }

        public bool EndDragAtWorld(Vector2 worldPoint, double now)
        {
            if (!DragToWorld(worldPoint, now)) { CancelDrag(); return false; }
            var entry = entries[heldId];
            string id = heldId;
            Vector2 inputVelocity = Vector2.ClampMagnitude(
                ReleaseVelocity(worldPoint, now, out float swipeDistance), config.ThrowMaxSwipeSpeed);
            int assistedSide = PreparedTransferSide;
            Vector2 assistedVelocity = assistedTransferVelocity;
            heldId = null;
            ResetTransferIntent();
            samples.Clear();
            entry.Held = false;
            entry.View.HitSphere.isTrigger = false;

            // Overheat punishes an intentional release, never holding/cancelling, contact physics or incoming orbs.
            // A valid manual Yin/Yang overlap consumes both; otherwise only the released non-drum burns.
            if (jigwiOverheat && !entry.View.Kind.IsDrum())
            {
                Entry partner = config.DropCombineEnabled && entry.View.Kind.IsRaw()
                    ? FindOppositeOverlap(entry) : null;
                if (partner != null && !partner.View.Kind.IsRaw()) partner = null;
                string partnerId = partner != null ? partner.View.OrbId : null;
                PlayJigwiBurnPreview(id, partnerId);
                JigwiBurnRequested?.Invoke(id, partnerId);
                return true;
            }

            // A tap is short and nearly still. Two taps on the same unlocked item use it in place.
            bool tapped = now - pressTime <= TapMaxSeconds && (worldPoint - pressPoint).magnitude <= config.OrbRadius * .5f;
            if (tapped && entry.View.Kind.IsDrum())
            {
                // Every tap generates, so rapid taps fire in a burst. The drum keeps drifting the way
                // it was going; only a drag or a flick moves it.
                lastTapId = null;
                SetBodyMode(entry);
                entry.View.Body.linearVelocity = OnPlane(entry.PressVelocity);
                DrumTapped?.Invoke(id);
                return true;
            }
            if (tapped && entry.View.Kind.IsDoubleTapUsable() && entry.View.LockSwipes == 0)
            {
                if (lastTapId == id && now - lastTapTime <= DoubleTapWindow)
                {
                    lastTapId = null;
                    // A double tap clears a fox orb (#328): it goes up in its blue smoke at once.
                    if (entry.View.Kind == LabOrbKind.FoxFire)
                    {
                        PlayFoxSmoke(entry);
                        FoxFireCleared?.Invoke(id);
                    }
                    entry.Locked = true;
                    StopMotion(entry.View.Body);
                    SetBodyMode(entry);
                    UseRequested?.Invoke(id);
                    return true;
                }
                lastTapId = id;
                lastTapTime = now;
            }
            else lastTapId = null;

            // An intentional raw overlap keeps its existing manual combine before assisted passing.
            if (config.DropCombineEnabled && (entry.View.Kind.IsRaw() || FoxReady(entry)))
            {
                Entry target = FindOppositeOverlap(entry);
                if (target != null)
                {
                    if (entry.View.Kind == LabOrbKind.FoxFire) TouchFoxFire(entry, target);
                    else if (target.View.Kind == LabOrbKind.FoxFire) TouchFoxFire(target, entry);
                    else RequestCombine(entry, target);
                    return true;
                }
            }

            // A deliberate manual pass can finish safely inside the screen. It enters the same
            // Host-validated crossing path as a freely moving body at the actual side plane.
            // A fox orb is never passed (#328), so it only slides like any flick.
            if (assistedSide != 0 && entry.View.Kind != LabOrbKind.FoxFire)
            {
                SetBodyMode(entry);
                Vector2 assistedWorldVelocity = TransferReleaseVelocity(entry, assistedSide, inputVelocity, assistedVelocity);
                Vector2 position = XY(entry.View.Body.position);
                position.x = assistedSide > 0 ? bounds.xMax : bounds.xMin;
                position = Clamp(position, entry.View.HitSphere.radius);
                ProcessEdgeCrossing(entry, assistedSide > 0, position, assistedWorldVelocity, LabEdgeCrossingKind.Swipe);
                return true;
            }

            // Side exits keep their existing screen-to-screen handoff even on a diagonal flick.
            bool outwardSide = (worldPoint.x <= bounds.xMin && inputVelocity.x < 0f)
                || (worldPoint.x >= bounds.xMax && inputVelocity.x > 0f);
            // Charmed (#251), an upward swipe is no throw: the orb slides up like any flick and bounces off the top.
            if (!charmed && entry.View.Kind.CanThrow(entry.View.LockSwipes) && !outwardSide
                && LabThrowMath.IsThrowGesture(inputVelocity, swipeDistance, config))
            {
                entry.BeforeRequest = CaptureMotion(entry);
                entry.Locked = true;
                StopMotion(entry.View.Body);
                SetBodyMode(entry);
                float releaseX01 = Mathf.InverseLerp(bounds.xMin, bounds.xMax, worldPoint.x);
                ThrowRequested?.Invoke(new LabOrbThrowGesture(id, inputVelocity, releaseX01, swipeDistance));
                return true;
            }

            SetBodyMode(entry);
            // A heavy bomb leaves the finger slower, so it takes a harder swipe to pass it on (#212).
            float heft = entry.View.Kind == LabOrbKind.Bomb ? config.BombReleaseSpeed : 1f;
            Vector2 worldVelocity = inputVelocity * bounds.width * heft;
            entry.View.Body.linearVelocity = OnPlane(Vector2.ClampMagnitude(worldVelocity,
                config.OrbMaxReleaseSpeed * bounds.width * heft));
            // A bomb the player lets go of is on its way to the neighbour until it stops (#237).
            entry.PlayerSent = entry.View.Kind == LabOrbKind.Bomb && worldVelocity.sqrMagnitude > 0f;
            return true;
        }

        public void CancelDrag()
        {
            if (heldId != null && entries.TryGetValue(heldId, out var entry))
            {
                entry.Held = false;
                entry.View.HitSphere.isTrigger = false;
                StopMotion(entry.View.Body);
                SetBodyMode(entry);
            }
            heldId = null;
            ResetTransferIntent();
            samples.Clear();
        }

        private void Update()
        {
            if (!configured || simulationHeld || !interactionEnabled || uiInputBlocked) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (heldId != null)
            {
                if (heldByTouch)
                {
                    var touch = Touchscreen.current?.primaryTouch;
                    if (touch == null) { CancelDrag(); return; }
                    if (!TryScreenToWorld(touch.position.ReadValue(), out var point)) { CancelDrag(); return; }
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled) CancelDrag();
                    else if (touch.press.wasReleasedThisFrame) EndDragAtWorld(point, now);
                    else if (touch.press.isPressed) DragToWorld(point, now);
                    else CancelDrag();
                }
                else
                {
                    var mouse = Mouse.current;
                    if (mouse == null) { CancelDrag(); return; }
                    if (!TryScreenToWorld(mouse.position.ReadValue(), out var point)) { CancelDrag(); return; }
                    if (mouse.leftButton.wasReleasedThisFrame) EndDragAtWorld(point, now);
                    else if (mouse.leftButton.isPressed) DragToWorld(point, now);
                    else CancelDrag();
                }
                return;
            }

            var primary = Touchscreen.current?.primaryTouch;
            if (primary != null && primary.press.wasPressedThisFrame
                && CanStartPointerAtScreen(primary.position.ReadValue())
                && !PointerStartedOverUi(primary.position.ReadValue())
                && TryScreenToWorld(primary.position.ReadValue(), out var touchPoint)
                && TryBeginDragAtWorld(touchPoint, now))
            {
                heldByTouch = true;
                return;
            }
            var pointer = Mouse.current;
            if (pointer != null && pointer.leftButton.wasPressedThisFrame
                && CanStartPointerAtScreen(pointer.position.ReadValue())
                && !PointerStartedOverUi(pointer.position.ReadValue())
                && TryScreenToWorld(pointer.position.ReadValue(), out var mousePoint)
                && TryBeginDragAtWorld(mousePoint, now))
                heldByTouch = false;
        }

        private static bool PointerStartedOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            for (int i = 0; i < hits.Count; i++)
                if (hits[i].module is UnityEngine.UI.GraphicRaycaster) return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (!configured || simulationHeld) return;
            OpenRestingBombs();
            updateEntries.Clear();
            updateEntries.AddRange(entries.Values);
            foreach (var entry in updateEntries)
            {
                if (entry.View == null) continue;
                bool drum = entry.View.Kind.IsDrum();
                // A bomb wanders like the drum, so it cannot simply be left lying in a corner.
                bool drifts = drum || entry.View.Kind == LabOrbKind.Bomb;
                if (!entry.LocalOwner || entry.Locked || entry.Held || entry.EdgePending || entry.Dropping
                    || entry.HiddenForPending)
                {
                    if (drum) entry.View.DriveDrumSpin(0f, config.DrumSpinStrength);
                    continue;
                }
                var body = entry.View.Body;
                Vector2 velocity = XY(body.linearVelocity);
                Vector2 position = XY(body.position);
                float speed = velocity.magnitude;
                // Taken before this step's slide, which may stop it right at the edge (#237).
                bool sent = entry.PlayerSent && entry.View.Kind == LabOrbKind.Bomb;
                Vector2 incoming = velocity;
                // Config motion is expressed in board widths, while Rigidbody uses world units.
                // Convert at this boundary so different camera/board sizes retain the same feel.
                if (entry.Arrival != LabDrumArrival.None) velocity = Arrive(entry, velocity, position);
                else if (drifts) velocity = Drift(entry, velocity, position);
                else if (speed <= config.OrbStopSpeed * bounds.width)
                {
                    // A blast-flung orb that has come to rest is an ordinary orb again.
                    velocity = Vector2.zero;
                    entry.Blasted = false;
                }
                else velocity = velocity.normalized * Mathf.Max(0f,
                    speed - config.OrbFloorDeceleration * bounds.width * Time.fixedDeltaTime);
                body.linearVelocity = OnPlane(velocity);
                ApplyRestingRecovery(entry, position, ref velocity);
                // A bomb the player sent on that reaches a side edge going outward passes however slowly,
                // even on the step its slide runs out (#237).
                if (sent) velocity = incoming;
                if (velocity.x == 0f) continue;
                bool left = position.x <= bounds.xMin && velocity.x < 0f;
                bool right = position.x >= bounds.xMax && velocity.x > 0f;
                if (!left && !right) continue;
                if (drifts && !sent && Mathf.Abs(velocity.x) < DrumPassSpeedFactor * config.DrumDriftSpeed * bounds.width)
                {
                    // A drum or bomb never drifts onto a neighbour's screen by itself; only a push or
                    // a swipe passes it. A gentle drift that reaches the edge turns back instead.
                    body.position = OnPlane(Clamp(position, entry.View.HitSphere.radius));
                    body.linearVelocity = OnPlane(new Vector2(-velocity.x, velocity.y));
                    continue;
                }
                // A drum still passing through tells the Host so it stops on the near side next.
                LabEdgeCrossingKind kind = entry.Arrival == LabDrumArrival.Pass ? LabEdgeCrossingKind.Passing
                    : entry.Blasted ? LabEdgeCrossingKind.Pushed : LabEdgeCrossingKind.Swipe;
                ProcessEdgeCrossing(entry, right, position, velocity, kind);
            }
        }

        private static bool UnlockedBomb(Entry entry) => entry.View != null && entry.View.Kind == LabOrbKind.Bomb
            && entry.View.LockSwipes == 0;

        /// <summary>
        /// 해금 폭탄 (#328): 잠금이 풀린 폭탄이 이 판에 미끄러져 들어와 멈추면 잠깐 뒤 연기와 함께 사라지고, 그 자리를 Host에
        /// 알려 공격 구슬 두 개로 연다. 열 수 없다는 답이 오면 잠시 뒤 다시 묻는다. 잠금이 풀린 폭탄은 잡을 수 없다.
        /// </summary>
        private void OpenRestingBombs()
        {
            if (!interactionEnabled) return;
            updateEntries.Clear();
            updateEntries.AddRange(entries.Values);
            foreach (Entry entry in updateEntries)
            {
                if (!UnlockedBomb(entry) || !entry.LocalOwner || entry.Provisional) continue;
                if (entry.Arrival != LabDrumArrival.None || entry.Dropping || entry.Held || entry.Locked
                    || entry.EdgePending || entry.HiddenForPending)
                {
                    entry.BombRestingSince = -1f;
                    continue;
                }
                if (entry.BombRestingSince < 0f) entry.BombRestingSince = Time.time;
                if (Time.time - entry.BombRestingSince < BombOpenPauseSeconds || Time.time < entry.BombNextOpenAt) continue;
                entry.BombRestingSince = -1f;
                entry.BombNextOpenAt = Time.time + BombOpenRetrySeconds;
                LabOrbVanishSmoke.Play(transform, entry.View.transform.position, entry.View.HitSphere.radius * BombOpenSmokeScale,
                    config != null ? config.OrbAppearance : null);
                BombOpening?.Invoke(entry.View.OrbId);
                entry.Locked = true;
                StopMotion(entry.View.Body);
                SetBodyMode(entry);
                UseRequested?.Invoke(entry.View.OrbId);
            }
        }

        /// <summary>
        /// A drum or bomb left alone drifts on its own along a smooth noise path in any direction,
        /// easing away from the side edges and the top and bottom walls, at the drum's drift speed
        /// and turning. A push or a flick first slides and slows like an orb; the drift takes over
        /// again once it is gentle. A drum's art turns as the path curves.
        /// </summary>
        private Vector2 Drift(Entry entry, Vector2 velocity, Vector2 position)
        {
            float driftSpeed = config.DrumDriftSpeed * bounds.width;
            float step = Time.fixedDeltaTime;
            float speed = velocity.magnitude;
            if (entry.PlayerSent)
            {
                // A bomb the player sent on slides like an orb until it stops (#237), rather than slowing
                // into a drift that would ease it back from the edge; only then does it drift again.
                entry.View.DriveDrumSpin(0f, config.DrumSpinStrength);
                entry.HasDriftHeading = false;
                float next = speed - config.OrbFloorDeceleration * config.BombSlideDeceleration * bounds.width * step;
                if (next > config.OrbStopSpeed * bounds.width) return velocity.normalized * next;
                entry.PlayerSent = false;
                return Vector2.zero;
            }
            if (speed > driftSpeed * 1.05f + .0001f)
            {
                entry.View.DriveDrumSpin(0f, config.DrumSpinStrength);
                entry.HasDriftHeading = false;
                // A heavy bomb slides to a stop sooner than a drum or an orb (#212).
                float slowing = config.OrbFloorDeceleration
                    * (entry.View.Kind == LabOrbKind.Bomb ? config.BombSlideDeceleration : 1f);
                return velocity.normalized * Mathf.Max(driftSpeed, speed - slowing * bounds.width * step);
            }

            // Gentle again: a blast that flung it is spent.
            entry.Blasted = false;
            float time = Time.time * config.DrumDriftTurning;
            float heading = Mathf.PerlinNoise(entry.DriftSeed, time) * 4f * Mathf.PI;
            float pace = Mathf.Lerp(.5f, 1f, Mathf.PerlinNoise(entry.DriftSeed + 31.7f, time * .7f));
            Vector2 desired = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * pace;
            float inset = Mathf.Clamp(entry.View.HitSphere.radius - config.OrbRadius, 0f, bounds.height * .5f);
            float margin = bounds.width * DrumEdgeMargin;
            desired.x += EaseAway(position.x - bounds.xMin, margin) - EaseAway(bounds.xMax - position.x, margin);
            if (areaShape.IsCurved)
            {
                desired += CurveRepulsion(areaShape.LowerBoundary, position, entry.View.HitSphere.radius, margin, false);
                desired += CurveRepulsion(areaShape.UpperBoundary, position, entry.View.HitSphere.radius, margin, true);
            }
            else desired.y += EaseAway(position.y - (bounds.yMin + inset), margin)
                    - EaseAway(bounds.yMax - inset - position.y, margin);
            desired = Vector2.ClampMagnitude(desired, 1f) * driftSpeed;

            float turn = entry.HasDriftHeading
                ? Mathf.DeltaAngle(entry.DriftHeading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) / step : 0f;
            entry.DriftHeading = heading;
            entry.HasDriftHeading = true;
            entry.View.DriveDrumSpin(driftSpeed > 0f
                ? Mathf.Clamp(turn * DrumDriftSpinGain, -DrumDriftMaxSpin, DrumDriftMaxSpin) : 0f,
                config.DrumSpinStrength);
            return Vector2.MoveTowards(velocity, desired, DrumDriftAcceleration * bounds.width * step);
        }

        /// <summary>How strongly to steer away from a boundary this far off: 0 beyond the margin, 2 at it.</summary>
        private static float EaseAway(float distance, float margin) =>
            margin <= 0f ? 0f : 2f * Mathf.Clamp01(1f - distance / margin);

        private static Vector2 CurveRepulsion(IReadOnlyList<Vector2> curve, Vector2 position,
            float radius, float margin, bool upper)
        {
            float closest = float.PositiveInfinity;
            Vector2 steering = Vector2.zero;
            for (int i = 1; i < curve.Count; i++)
            {
                Vector2 segment = curve[i] - curve[i - 1];
                float t = Mathf.Clamp01(Vector2.Dot(position - curve[i - 1], segment) / segment.sqrMagnitude);
                Vector2 delta = position - (curve[i - 1] + segment * t);
                if (delta.sqrMagnitude >= closest) continue;
                closest = delta.sqrMagnitude;
                Vector2 inward = (upper ? new Vector2(segment.y, -segment.x)
                    : new Vector2(-segment.y, segment.x)).normalized;
                steering = inward * EaseAway(Vector2.Dot(delta, inward) - radius, margin);
            }
            return steering;
        }

        /// <summary>A stable per-drum offset into the drift noise, so drums on one board wander apart.</summary>
        private static float DriftSeedFor(string id)
        {
            int hash = 17;
            unchecked { foreach (char character in id) hash = hash * 31 + character; }
            return (hash & 0xFFFF) * .01f;
        }

        /// <summary>
        /// 두 구슬을 그 자리에 멈추고 방장에게 결합을 요청한다.
        /// 놓아서 결합과 닿으면 결합이 함께 쓴다.
        /// </summary>
        private void RequestCombine(Entry first, Entry second, bool passiveContact = false)
        {
            first.BeforeRequest = CaptureMotion(first);
            second.BeforeRequest = CaptureMotion(second);
            first.Locked = true;
            second.Locked = true;
            first.PendingPartnerId = second.View.OrbId;
            second.PendingPartnerId = first.View.OrbId;
            // 멈추기 전에 두 구슬의 속도를 기록한다. 잡고 있던 구슬은 속도가 0이다.
            Vector2 velocity = CombinedVelocity(XY(first.View.Body.linearVelocity),
                XY(second.View.Body.linearVelocity)) / bounds.width;
            StopMotion(first.View.Body);
            StopMotion(second.View.Body);
            SetBodyMode(first);
            SetBodyMode(second);
            Vector2 middle = ToNormalized((XY(first.View.Body.position) + XY(second.View.Body.position)) * .5f);
            // The event signature stays compatible with existing Scene/Network listeners. The network
            // copies this synchronous cause into the request, so overheat never burns a passive collision.
            bool previousCause = contactCombineRequest;
            contactCombineRequest = passiveContact;
            try { CombineRequested?.Invoke(first.View.OrbId, second.View.OrbId, middle, velocity); }
            finally { contactCombineRequest = previousCause; }
        }

        /// <summary>
        /// 결합 구슬이 이어받을 속도. 같은 무게의 두 구슬이 붙는 것처럼 두 속도의 평균을 쓴다.
        /// 규칙을 바꿀 때는 이 함수만 고친다.
        /// </summary>
        private static Vector2 CombinedVelocity(Vector2 first, Vector2 second) => (first + second) * .5f;

        /// <summary>
        /// 구슬끼리 부딪혔을 때, 음+양이고 충분히 빠르게 부딪혔으면 결합을 요청
        /// 두 구슬 모두 신호를 보내지먼, 첫 요청에서 짝 표시가 붙어 두 번째 신호는 건너뛴다.
        /// </summary>
        private void OnOrbHit(LabOrbView self, LabOrbView other, float worldSpeed)
        {
            if (!configured || !interactionEnabled || !config.ContactCombineEnabled) return;
            if (!entries.TryGetValue(self.OrbId, out var first)
                || !entries.TryGetValue(other.OrbId, out var second)) return;
            if (!CanContactCombine(first) || !CanContactCombine(second)) return;
            if (first.View.Kind == second.View.Kind) return;

            // 물리 속도는 월드 단위라, 다른 튜닝 값처럼 보드 너비 단위로 바꿔서 비교한다.
            float speed = worldSpeed / bounds.width;
            if (speed < config.CombineSpeed) return;

            if (first.View.Kind == LabOrbKind.FoxFire) TouchFoxFire(first, second);
            else if (second.View.Kind == LabOrbKind.FoxFire) TouchFoxFire(second, first);
            else
            {
                // Recovery is a convenience movement, not a player's collision-combine input.
                if (RecoveryContact(first, second)) return;
                RequestCombine(first, second, true);
            }
        }

        /// <summary>
        /// 여우불 (#251): 여우구슬과 음·양 구슬이 닿거나 겹쳐지면 둘 다 그 자리에서 푸른 연기로 사라진다.
        /// 이 화면에서 바로 보여 주고 Host의 확인을 기다린다 (거절되면 둘 다 돌아온다).
        /// </summary>
        private void TouchFoxFire(Entry fox, Entry orb)
        {
            PlayFoxSmoke(fox);
            PlayFoxSmoke(orb);
            FoxFireCleared?.Invoke(fox.View.OrbId);
            BeginPendingLaunch(fox.View.OrbId);
            BeginPendingLaunch(orb.View.OrbId);
            FoxFireTouched?.Invoke(fox.View.OrbId, orb.View.OrbId);
        }

        private void PlayFoxSmoke(Entry entry)
        {
            LabOrbAppearance look = config != null && config.OrbAppearance != null ? config.OrbAppearance : LabOrbAppearance.Defaults;
            LabOrbVanishSmoke.Play(transform, entry.View.transform.position, entry.View.HitSphere.radius, look,
                look.FoxFireSmokeColor);
        }

        /// <summary>A fox orb that has landed and is past its safe moment (#251).</summary>
        private static bool FoxReady(Entry entry) =>
            entry.View.Kind == LabOrbKind.FoxFire && !entry.Dropping && Time.time >= entry.FoxSafeUntil;

        /// <summary>Whether this board shows the orb as a fox orb (#251).</summary>
        public bool IsFoxFire(string id) => id != null && entries.TryGetValue(id, out var entry)
            && entry.View != null && entry.View.Kind == LabOrbKind.FoxFire;

        /// <summary>닿으면 결합에 쓸 수 있는 구슬인지 확인한다. 여우구슬은 보호 시간이 지난 뒤부터 (#251).</summary>
        private static bool CanContactCombine(Entry entry)
        {
            return (entry.View.Kind.IsRaw() || FoxReady(entry))
                && entry.LocalOwner
                && !entry.Held
                && !entry.Locked
                && !entry.EdgePending
                && !entry.HiddenForPending
                && entry.PendingPartnerId == null;
        }


        private Entry FindOppositeOverlap(Entry dragged)
        {
            Entry nearest = null;
            float best = Mathf.Pow(config.OrbRadius * 2f, 2f);
            foreach (var candidate in entries.Values)
            {
                // Yin meets Yang; a fox orb meets either, and either meets a fox orb past its safe moment (#251).
                bool partner = dragged.View.Kind == LabOrbKind.FoxFire ? candidate.View.Kind.IsRaw()
                    : candidate.View.Kind.IsRaw() ? candidate.View.Kind != dragged.View.Kind : FoxReady(candidate);
                if (candidate == dragged || !partner || !candidate.LocalOwner
                    || candidate.Locked || candidate.EdgePending || candidate.Held || candidate.HiddenForPending) continue;
                float distance = (XY(candidate.View.Body.position) - XY(dragged.View.Body.position)).sqrMagnitude;
                if (distance <= best) { best = distance; nearest = candidate; }
            }
            return nearest;
        }

        private void BuildHorizontalWalls()
        {
            if (wallRoot == null)
            {
                wallRoot = new GameObject("Physics walls - top and bottom");
                wallRoot.transform.SetParent(transform, false);
            }
            wallRoot.layer = gameObject.layer;
            while (wallRoot.transform.childCount > 0)
            {
                var old = wallRoot.transform.GetChild(0);
                var collider = old.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
                old.SetParent(null);
                Destroy(old.gameObject);
            }
            foreach (Mesh mesh in areaWallMeshes) if (mesh != null) Destroy(mesh);
            areaWallMeshes.Clear();
            if (areaShape.IsCurved)
            {
                CreateCurvedWall("Top curve", areaShape.UpperBoundary, true);
                CreateCurvedWall("Bottom curve", areaShape.LowerBoundary, false);
                return;
            }
            CreateWall("Top", bounds.yMax + config.OrbRadius + WallThickness * .5f);
            CreateWall("Bottom", bounds.yMin - config.OrbRadius - WallThickness * .5f);
        }

        // Only the two paper rims are solid. No face spans a side portal.
        private void CreateCurvedWall(string label, IReadOnlyList<Vector2> curve, bool upper)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float depth = Mathf.Max(config.OrbRadius, config.DrumRadius) + WallThickness;
            float outside = upper ? WallThickness : -WallThickness;
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                bool forward = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) > 0f;
                triangles.Add(first); triangles.Add(first + (forward ? 1 : 2)); triangles.Add(first + (forward ? 2 : 1));
                triangles.Add(first); triangles.Add(first + (forward ? 2 : 3)); triangles.Add(first + (forward ? 3 : 2));
            }
            for (int i = 1; i < curve.Count; i++)
            {
                Vector2 a = curve[i - 1], b = curve[i];
                Vector3 af = new Vector3(a.x, a.y, -depth), ab = new Vector3(a.x, a.y, depth);
                Vector3 bf = new Vector3(b.x, b.y, -depth), bb = new Vector3(b.x, b.y, depth);
                Vector3 offset = Vector3.up * outside;
                Quad(af, bf, bb, ab, upper ? Vector3.down : Vector3.up);
                Quad(af + offset, ab + offset, bb + offset, bf + offset, upper ? Vector3.up : Vector3.down);
                Quad(af, af + offset, bf + offset, bf, Vector3.back);
                Quad(ab, bb, bb + offset, ab + offset, Vector3.forward);
                if (i == 1) Quad(af, ab, ab + offset, af + offset, Vector3.left);
                if (i == curve.Count - 1) Quad(bf, bf + offset, bb + offset, bb, Vector3.right);
            }
            var mesh = new Mesh { name = label + " collision", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            areaWallMeshes.Add(mesh);
            var wall = new GameObject(label);
            wall.layer = gameObject.layer;
            wall.transform.SetParent(wallRoot.transform, false);
            // Samples are world coordinates even if the board is moved in the scene.
            wall.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var collider = wall.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = contactMaterial;
        }

        private void CreateWall(string label, float worldY)
        {
            var wall = new GameObject(label);
            wall.layer = gameObject.layer;
            wall.transform.SetParent(wallRoot.transform, false);
            wall.transform.position = new Vector3(bounds.center.x, worldY, 0f);
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(bounds.width + config.OrbRadius * 4f,
                WallThickness, config.OrbRadius * 2f + WallThickness);
            collider.sharedMaterial = contactMaterial;
        }

        private void SetBodyMode(Entry entry)
        {
            var body = entry.View.Body;
            if (simulationHeld && !body.isKinematic && !entry.SimulationHoldCaptured)
            {
                entry.SimulationHoldCaptured = true;
                entry.SimulationHoldVelocity = body.linearVelocity;
                entry.SimulationHoldAngularVelocity = body.angularVelocity;
            }
            bool movable = !simulationHeld && entry.LocalOwner && !entry.Held && !entry.Locked && !entry.EdgePending && !entry.Dropping
                && !entry.HiddenForPending;
            bool desiredKinematic = !movable;
            if (body.isKinematic != desiredKinematic)
            {
                StopMotion(body);
                body.isKinematic = desiredKinematic;
                if (movable) body.linearVelocity = Vector3.zero;
            }
            if (!simulationHeld && !body.isKinematic && entry.SimulationHoldCaptured)
            {
                body.linearVelocity = entry.SimulationHoldVelocity;
                body.angularVelocity = entry.SimulationHoldAngularVelocity;
                entry.SimulationHoldCaptured = false;
            }
            // A falling gourd bomb is still in the air: nothing bumps into it until it lands.
            body.detectCollisions = !entry.Dropping && !entry.HiddenForPending;
            entry.View.HitSphere.isTrigger = entry.Held;
        }

        private static void Retire(Entry entry)
        {
            if (entry.View == null) return;
            // Destroy is deferred in Play Mode. Remove its collider immediately so a
            // transfer/retry snapshot cannot collide with a visually removed orb.
            StopMotion(entry.View.Body);
            entry.View.Body.detectCollisions = false;
            entry.View.HitSphere.enabled = false;
            Destroy(entry.View.gameObject);
        }

        private void AddSample(Vector2 worldPoint, double now)
        {
            if (samples.Count > 0 && now < samples[samples.Count - 1].Time) { samples.Clear(); return; }
            if (samples.Count > 0 && now == samples[samples.Count - 1].Time)
                samples[samples.Count - 1] = new PointerSample(worldPoint, now);
            else samples.Add(new PointerSample(worldPoint, now));
            while (samples.Count > 2 && samples[1].Time < now - ReleaseSampleWindow)
                samples.RemoveAt(0);
            if (samples.Count > 32) samples.RemoveAt(0);
        }

        private Vector2 ReleaseVelocity(Vector2 endPoint, double now, out float distanceInBoardWidths)
        {
            distanceInBoardWidths = 0f;
            if (samples.Count < 2) return Vector2.zero;
            double earliest = now - ReleaseSampleWindow;
            var first = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Time < earliest) continue;
                if (samples[i - 1].Time <= earliest && samples[i].Time > earliest)
                {
                    var before = samples[i - 1];
                    var after = samples[i];
                    float portion = (float)((earliest - before.Time) / (after.Time - before.Time));
                    first = new PointerSample(Vector2.LerpUnclamped(before.Position, after.Position, portion), earliest);
                }
                else first = samples[i - 1];
                break;
            }
            double elapsed = now - first.Time;
            Vector2 displacement = endPoint - first.Position;
            distanceInBoardWidths = displacement.magnitude / bounds.width;
            return elapsed > .001d ? displacement / (float)(elapsed * bounds.width) : Vector2.zero;
        }

        private bool TryScreenToWorld(Vector2 screenPoint, out Vector2 worldPoint)
        {
            worldPoint = default;
            if (inputCamera == null) return false;
            var ray = inputCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance) || distance < 0f) return false;
            worldPoint = ray.GetPoint(distance);
            return Finite(worldPoint);
        }

        private Vector2 ToWorld(Vector2 normalized, float radius) => areaShape.Clamp(
            areaShape.ToWorld(normalized, areaShape.IsCurved ? radius : config.OrbRadius), radius);
        private Vector2 NormalizeForBody(LabOrbAreaShape shape, Vector2 point, float radius) =>
            shape.ToNormalized(point, shape.IsCurved ? radius : config.OrbRadius);
        private Vector2 ToNormalized(Vector2 world) => areaShape.ToNormalized(world, config.OrbRadius);
        private Vector2 Clamp(Vector2 world) => areaShape.Clamp(world, config.OrbRadius);

        /// <summary>
        /// Keeps a body larger than an orb clear of the top and bottom walls. Side edges stay at the
        /// orb limits, where a drum hands off to a neighbour just like an orb.
        /// </summary>
        private Vector2 Clamp(Vector2 world, float radius)
        {
            return areaShape.Clamp(world, radius);
        }

        /// <summary>How long a blast-shrunk orb's art takes to squeeze down to its new size (#213).</summary>
        private const float ShrinkSeconds = .3f;
        private float RadiusFor(LabOrbKind kind, bool small = false, float jigwiScale = 1f, float jigwiABScale = 1f)
        {
            if (kind.IsDrum()) return config.DrumRadius;
            float existing = (small ? config.BlastShrinkScale : 1f) * (kind.IsRaw() ? jigwiScale : 1f);
            float size = jigwiABScale < 1f && (kind.IsRaw() || kind == LabOrbKind.Combined)
                ? Mathf.Min(existing, jigwiABScale) : existing;
            return config.OrbRadius * size;
        }

        /// <summary>Owner-local prediction captures B size without changing the projectile's paint share.</summary>
        public float JigwiABRadiusScaleFor(string id) => id != null && entries.TryGetValue(id, out Entry entry)
            ? entry.JigwiABScale : 1f;
        private static Vector2 XY(Vector3 value) => new Vector2(value.x, value.y);
        private static Vector3 OnPlane(Vector2 value) => new Vector3(value.x, value.y, 0f);
        private static void StopMotion(Rigidbody body)
        {
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
        private void RequireConfigured()
        {
            if (!configured) throw new InvalidOperationException("Configure the board before adding orbs.");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);

        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void OnDisable() => CancelDrag();
        private void OnDestroy()
        {
            if (contactMaterial != null) Destroy(contactMaterial);
            foreach (Mesh mesh in areaWallMeshes) if (mesh != null) Destroy(mesh);
            foreach (GenerationPreview preview in generationPreviews.Values)
                if (preview.Material != null) Destroy(preview.Material);
        }
    }
}
