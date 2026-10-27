using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Local input/accessibility tuning. Values never change Host rules or the wire format.</summary>
    [Serializable]
    public sealed class LabOrbEdgeControlSettings
    {
        public bool Enabled = true;
        [Tooltip("390 폭 기준 전달 구간 너비(UI 단위)")][Range(0f, 100f)] public float TransferInset = 36f;
        [Tooltip("전달 의도를 만드는 최소 가로 이동(UI 단위)")][Min(1f)] public float MinimumDrag = 12f;
        [Tooltip("안쪽으로 이만큼 되돌리면 전달 취소(UI 단위)")][Min(1f)] public float TurnBackTolerance = 4f;
        [Tooltip("축소 구슬도 잡을 수 있는 최소 터치 반지름(UI 단위)")][Min(0f)] public float MinimumGripRadius = 22f;
        [Min(0f)] public float RestDelay = .3f;
        [Tooltip("정체 판정 속도(보드 폭/초)")][Min(0f)] public float RestSpeed = .035f;
        [Tooltip("안쪽으로 밀어주는 최고 속도(보드 폭/초)")][Min(0f)] public float NudgeSpeed = .18f;
        [Tooltip("안전선과 구슬 사이 여백(UI 단위)")][Min(0f)] public float TargetPadding = 4f;
        [Tooltip("하단 곡선/안전영역에서 추가 확보할 여백(UI 단위)")][Min(0f)] public float BottomPadding = 16f;
    }

    public sealed partial class LabOrbBoard
    {
        [Header("Screen-edge controls (#310) - local input only")]
        [SerializeField] private LabOrbEdgeControlSettings edgeControls = new LabOrbEdgeControlSettings();
        public LabOrbEdgeControlSettings EdgeControlSettings => edgeControls ?? (edgeControls = new LabOrbEdgeControlSettings());
        private int preparedTransferSide;
        private Vector2 transferAnchor, transferLastPoint, assistedTransferVelocity, transferIntentPoint;
        private float transferExtreme;
        private double transferLastAt;
        private bool edgeScreenOverride;
        private Rect edgeViewportOverride, edgeSafeOverride;

        public int PreparedTransferSide
        {
            get
            {
                if (!EdgeControlsActive || heldId == null || preparedTransferSide == 0
                    || !entries.TryGetValue(heldId, out Entry entry) || entry.View == null) return 0;
                TransferGates(out float left, out float right);
                float bodyX = entry.View.Body.position.x;
                // Heavy bombs trail the hand and large items keep their grab offset. A finger
                // reaching the strip must not teleport a body that has not itself arrived there.
                return preparedTransferSide < 0 ? (bodyX <= left ? -1 : 0) : (bodyX >= right ? 1 : 0);
            }
        }
        private bool EdgeControlsActive => configured && EdgeControlSettings.Enabled && interactionEnabled
            && !simulationHeld && !uiInputBlocked;

        /// <summary>Explicit camera viewport and safe-area geometry for render-target hosts and deterministic tests.</summary>
        public void SetEdgeControlScreenGeometry(Rect viewport, Rect safeArea)
        {
            if (viewport.width <= 0f || viewport.height <= 0f || safeArea.width <= 0f || safeArea.height <= 0f
                || !Finite(viewport.min) || !Finite(viewport.max) || !Finite(safeArea.min) || !Finite(safeArea.max))
                throw new ArgumentException("Finite positive screen and safe-area rectangles are required.");
            edgeViewportOverride = viewport;
            edgeSafeOverride = safeArea;
            edgeScreenOverride = true;
            ResetTransferIntent();
        }
        public void ClearEdgeControlScreenGeometry() { edgeScreenOverride = false; ResetTransferIntent(); }

        private float EdgeSetting(float value, float fallback, float minimum, float maximum) =>
            Mathf.Clamp(Finite(value) ? value : fallback, minimum, maximum);

        private float ReferenceWorldUnit
        {
            get
            {
                Rect viewport = EdgeViewport;
                if (inputCamera != null && viewport.width > 0f
                    && TryScreenToWorld(viewport.center, out Vector2 a)
                    && TryScreenToWorld(viewport.center + Vector2.right * (viewport.width / 390f), out Vector2 b))
                {
                    float unit = (b - a).magnitude;
                    if (unit > 0f && Finite(unit)) return unit;
                }
                return bounds.width / 390f;
            }
        }
        private Rect EdgeViewport => edgeScreenOverride ? edgeViewportOverride
            : hasInputScreenRect ? inputScreenRect : inputCamera != null ? inputCamera.pixelRect : new Rect();
        private Rect EdgeSafeArea => edgeScreenOverride ? edgeSafeOverride : Screen.safeArea;

        private void TransferGates(out float left, out float right)
        {
            float inset = EdgeSetting(EdgeControlSettings.TransferInset, 36f, 0f, 100f) * ReferenceWorldUnit;
            left = bounds.xMin + inset;
            right = bounds.xMax - inset;
            Rect viewport = EdgeViewport, safe = EdgeSafeArea;
            if (viewport.width > 0f && safe.width > 0f)
            {
                if (safe.xMin > viewport.xMin && safe.xMin < viewport.xMax
                    && TryScreenToWorld(new Vector2(safe.xMin, viewport.center.y), out Vector2 safeLeft))
                    left = Mathf.Max(left, safeLeft.x + inset);
                if (safe.xMax < viewport.xMax && safe.xMax > viewport.xMin
                    && TryScreenToWorld(new Vector2(safe.xMax, viewport.center.y), out Vector2 safeRight))
                    right = Mathf.Min(right, safeRight.x - inset);
            }
            left = Mathf.Clamp(left, bounds.xMin, bounds.xMin + bounds.width * .3f);
            right = Mathf.Clamp(right, bounds.xMax - bounds.width * .3f, bounds.xMax);
        }

        private float GripReach(Entry entry)
        {
            float reach = entry.View.HitSphere.radius * 1.25f;
            if (EdgeControlsActive)
                reach = Mathf.Max(reach, EdgeSetting(EdgeControlSettings.MinimumGripRadius, 22f, 0f, 64f) * ReferenceWorldUnit);
            return reach;
        }

        private void BeginTransferIntent(Vector2 point, double now)
        {
            ResetTransferIntent();
            transferAnchor = transferLastPoint = point;
            transferExtreme = point.x;
            transferLastAt = now;
        }
        private void ResetTransferIntent()
        {
            preparedTransferSide = 0;
            assistedTransferVelocity = Vector2.zero;
        }
        private void UpdateTransferIntent(Vector2 point, double now)
        {
            Vector2 step = point - transferLastPoint;
            double elapsed = now - transferLastAt;
            if (!EdgeControlsActive) { ResetTransferIntent(); return; }
            TransferGates(out float left, out float right);
            int side = point.x <= left ? -1 : point.x >= right ? 1 : 0;
            float unit = ReferenceWorldUnit;
            float turnBack = EdgeSetting(EdgeControlSettings.TurnBackTolerance, 4f, 1f, 40f) * unit;
            float minimum = EdgeSetting(EdgeControlSettings.MinimumDrag, 12f, 1f, 80f) * unit;
            if (preparedTransferSide != 0)
            {
                transferExtreme = preparedTransferSide > 0 ? Mathf.Max(transferExtreme, point.x) : Mathf.Min(transferExtreme, point.x);
                Vector2 afterIntent = point - transferIntentPoint;
                bool turnedUpward = afterIntent.y >= minimum * 1.5f
                    && afterIntent.y > Mathf.Abs(afterIntent.x) * 1.5f;
                if (side != preparedTransferSide || (transferExtreme - point.x) * preparedTransferSide >= turnBack || turnedUpward)
                {
                    ResetTransferIntent();
                    transferAnchor = point;
                    transferExtreme = point.x;
                }
            }
            Vector2 travel = point - transferAnchor;
            if (preparedTransferSide == 0 && side != 0 && travel.x * side >= minimum
                && Mathf.Abs(travel.x) >= Mathf.Abs(travel.y) * .75f)
            {
                preparedTransferSide = side;
                transferExtreme = point.x;
                transferIntentPoint = point;
            }
            if (preparedTransferSide != 0 && step.x * preparedTransferSide > unit * .1f && elapsed > .001d)
                assistedTransferVelocity = Vector2.ClampMagnitude(step / (float)(elapsed * bounds.width), config.OrbMaxReleaseSpeed);
            transferLastPoint = point;
            transferLastAt = now;
        }

        public bool TryGetTransferGuide(out Vector2 worldPosition, out bool right)
        {
            worldPosition = default;
            right = false;
            int side = PreparedTransferSide;
            if (side == 0 || !entries.TryGetValue(heldId, out Entry entry) || entry.View.Kind == LabOrbKind.FoxFire
                || entry.Locked || entry.EdgePending || jigwiOverheat || teamWallSide == side || blockedSide == side) return false;
            if (config.DropCombineEnabled && (entry.View.Kind.IsRaw() || FoxReady(entry)) && FindOppositeOverlap(entry) != null)
                return false;
            TransferGates(out float left, out float rightGate);
            right = side > 0;
            worldPosition = Clamp(new Vector2(right ? rightGate : left, entry.View.Body.position.y), entry.View.HitSphere.radius);
            return true;
        }

        private Vector2 TransferReleaseVelocity(Entry entry, int side, Vector2 release, Vector2 remembered)
        {
            Vector2 result = release.x * side > 0f ? release : remembered;
            float heft = entry.View.Kind == LabOrbKind.Bomb ? config.BombReleaseSpeed : 1f;
            float maximum = config.OrbMaxReleaseSpeed * heft;
            result = Vector2.ClampMagnitude(result * heft, maximum);
            // An intentional slow drag or a pause at the inner gate still makes a single handoff.
            result.x = side * Mathf.Max(Mathf.Abs(result.x), Mathf.Min(.12f * heft, maximum));
            return Vector2.ClampMagnitude(result, maximum) * bounds.width;
        }

        private void ProcessEdgeCrossing(Entry entry, bool right, Vector2 position, Vector2 velocity, LabEdgeCrossingKind kind)
        {
            if (entry.EdgePending || entry.Locked || entry.HiddenForPending) return;
            ResetRecovery(entry);
            Rigidbody body = entry.View.Body;
            // A fox orb never leaves its board (#328): both side edges stop it like walls; a double tap clears it.
            if (entry.View.Kind == LabOrbKind.FoxFire)
            {
                body.position = OnPlane(Clamp(position, entry.View.HitSphere.radius));
                body.linearVelocity = OnPlane(new Vector2(-velocity.x * config.OrbRestitution, velocity.y));
                return;
            }
            if (teamWallSide == (right ? 1 : -1))
            {
                body.position = OnPlane(Clamp(position, entry.View.HitSphere.radius));
                body.linearVelocity = OnPlane(new Vector2(-velocity.x * config.OrbRestitution, velocity.y));
                TeamWallBounces++;
                if (teamWall != null) teamWall.Flash();
                return;
            }
            if (kind == LabEdgeCrossingKind.Swipe && blockedSide == (right ? 1 : -1))
            {
                body.position = OnPlane(Clamp(position, entry.View.HitSphere.radius));
                body.linearVelocity = OnPlane(new Vector2(-velocity.x * config.OrbRestitution, velocity.y));
                BlockBounces++;
                if (edgeBlock != null) edgeBlock.Flash();
                return;
            }
            var crossing = new LabOrbEdgeCrossing(entry.View.OrbId, right,
                NormalizeForBody(areaShape, position, entry.View.HitSphere.radius).y, velocity / bounds.width, kind);
            entry.EdgePending = true;
            entry.PendingEdge = crossing;
            body.position = OnPlane(Clamp(position, entry.View.HitSphere.radius));
            StopMotion(body);
            SetBodyMode(entry);
            EdgeCrossed?.Invoke(crossing);
        }

        private void ResetRestingRecovery()
        {
            foreach (Entry entry in entries.Values)
            {
                if (entry.SafetyNudging && entry.View != null) StopMotion(entry.View.Body);
                ResetRecovery(entry);
            }
        }
        private static void ResetRecovery(Entry entry)
        {
            entry.SafetyRestSeconds = 0f;
            entry.SafetyNudging = false;
            entry.SafetyNudgeSeconds = 0f;
        }
        private bool RecoveryContact(Entry first, Entry second)
        {
            if (!first.SafetyNudging && !second.SafetyNudging
                && Time.time >= first.SafetyCombineGuardUntil && Time.time >= second.SafetyCombineGuardUntil) return false;
            first.SafetyCombineGuardUntil = second.SafetyCombineGuardUntil = Time.time + .15f;
            return true;
        }

        private void ApplyRestingRecovery(Entry entry, Vector2 position, ref Vector2 velocity)
        {
            if (!EdgeControlsActive || entry.View.Kind.IsDrum() || entry.View.Kind == LabOrbKind.Bomb
                || entry.View.Kind == LabOrbKind.FoxFire || entry.Arrival != LabDrumArrival.None || entry.Blasted)
            { ResetRecovery(entry); return; }
            TransferGates(out float left, out float right);
            float unit = ReferenceWorldUnit;
            float radius = entry.View.HitSphere.radius;
            float padding = EdgeSetting(EdgeControlSettings.TargetPadding, 4f, 0f, 64f) * unit;
            Vector2 target = position;
            if (position.x < left + radius) target.x = left + radius + padding;
            else if (position.x > right - radius) target.x = right - radius - padding;
            Vector2 range = areaShape.VerticalRange(target.x, radius);
            float bottom = range.x + EdgeSetting(EdgeControlSettings.BottomPadding, 16f, 0f, 100f) * unit;
            Rect viewport = EdgeViewport, safe = EdgeSafeArea;
            if (viewport.height > 0f && safe.yMin > viewport.yMin && safe.yMin < viewport.yMax
                && TryScreenToWorld(new Vector2(viewport.center.x, safe.yMin), out Vector2 safeBottom))
                bottom = Mathf.Max(bottom, safeBottom.y + radius + padding);
            target.y = Mathf.Max(target.y, Mathf.Min(bottom, range.y));
            target = Clamp(target, radius);
            Vector2 delta = target - position;
            if (delta.sqrMagnitude <= unit * unit * .25f) { ResetRecovery(entry); return; }
            float maximum = EdgeSetting(EdgeControlSettings.NudgeSpeed, .18f, 0f, 1f) * bounds.width;
            float resting = EdgeSetting(EdgeControlSettings.RestSpeed, .035f, 0f, .5f) * bounds.width;
            if (!entry.SafetyNudging)
            {
                // Any live movement, including slow outward travel, gets its normal physical handoff first.
                if (velocity.magnitude > resting || (position.x <= left && velocity.x < 0f)
                    || (position.x >= right && velocity.x > 0f)) { ResetRecovery(entry); return; }
                entry.SafetyRestSeconds += Time.fixedDeltaTime;
                if (entry.SafetyRestSeconds < EdgeSetting(EdgeControlSettings.RestDelay, .3f, 0f, 5f)) return;
                entry.SafetyNudging = true;
            }
            else if (velocity.magnitude > maximum * 1.5f || entry.SafetyNudgeSeconds > 1.5f)
            { ResetRecovery(entry); return; }
            entry.SafetyNudgeSeconds += Time.fixedDeltaTime;
            entry.SafetyCombineGuardUntil = Time.time + .15f;
            // Velocity (rather than teleportation/kinematic movement) keeps contacts and the curved walls physical.
            velocity = Vector2.ClampMagnitude(delta / .18f, maximum);
            entry.View.Body.linearVelocity = OnPlane(velocity);
        }
    }
}
