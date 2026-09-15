using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6.Prototype.Orbs
{
    /// <summary>Values injected from the project's one configuration asset.</summary>
    public sealed class GestureTuning
    {
        public float HorizontalDistanceFraction { get; }
        public float DominanceRatio { get; }
        public float DropDistanceFraction { get; }
        public float AttackZoneHeightFraction { get; }
        public bool LaunchAtBattleBoundary { get; }
        public bool LaunchOnRelease { get; }
        public bool AllowHorizontalTransfer { get; }
        public bool TransferOnEdgeRelease { get; }
        public float TransferEdgeFraction { get; }
        public bool ReachableEdgeTransferDistance { get; }

        public GestureTuning(float horizontalDistanceFraction, float dominanceRatio,
            float dropDistanceFraction, float attackZoneHeightFraction, bool launchAtBattleBoundary = false,
            bool allowHorizontalTransfer = true, bool transferOnEdgeRelease = false,
            float transferEdgeFraction = .055f, bool reachableEdgeTransferDistance = false, bool launchOnRelease = false)
        {
            RequirePositiveFinite(horizontalDistanceFraction, nameof(horizontalDistanceFraction));
            RequirePositiveFinite(dominanceRatio, nameof(dominanceRatio));
            RequirePositiveFinite(dropDistanceFraction, nameof(dropDistanceFraction));
            if (!OrbGestureEngine.IsFinite(attackZoneHeightFraction) ||
                attackZoneHeightFraction <= 0f || attackZoneHeightFraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(attackZoneHeightFraction));

            HorizontalDistanceFraction = horizontalDistanceFraction;
            DominanceRatio = dominanceRatio;
            DropDistanceFraction = dropDistanceFraction;
            AttackZoneHeightFraction = attackZoneHeightFraction;
            LaunchAtBattleBoundary = launchAtBattleBoundary;
            LaunchOnRelease = launchOnRelease;
            AllowHorizontalTransfer = allowHorizontalTransfer;
            if (!OrbGestureEngine.IsFinite(transferEdgeFraction) || transferEdgeFraction <= 0f || transferEdgeFraction > .25f)
                throw new ArgumentOutOfRangeException(nameof(transferEdgeFraction));
            TransferOnEdgeRelease = transferOnEdgeRelease;
            TransferEdgeFraction = transferEdgeFraction;
            ReachableEdgeTransferDistance = reachableEdgeTransferDistance;
        }

        static void RequirePositiveFinite(float value, string name)
        {
            if (!OrbGestureEngine.IsFinite(value) || value <= 0f)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public readonly struct OrbDropTarget
    {
        public OrbRecord Orb { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsPending { get; }

        public OrbDropTarget(OrbRecord orb, Vector2 screenPosition, bool isPending = false)
        {
            Orb = orb;
            ScreenPosition = screenPosition;
            IsPending = isPending;
        }
    }

    /// <summary>A candidate request only; this value never changes an authoritative orb.</summary>
    public readonly struct OrbGestureDecision
    {
        public OrbActionKind Kind { get; }
        public string OrbId { get; }
        public string OtherOrbId { get; }
        public Vector2 RawPosition { get; }
        public Vector2 NormalizedPosition { get; }

        public OrbGestureDecision(OrbActionKind kind, string orbId, string otherOrbId,
            Vector2 rawPosition, Vector2 normalizedPosition)
        {
            Kind = kind;
            OrbId = orbId;
            OtherOrbId = otherOrbId;
            RawPosition = rawPosition;
            NormalizedPosition = normalizedPosition;
        }
    }

    /// <summary>
    /// One local pointer and one outstanding candidate. The adapter supplies collider hits,
    /// UI start results, current camera geometry, and a confirmed resolution for pending work.
    /// No network, input device, rendering, physics, or game-state mutation occurs here.
    /// </summary>
    public sealed class OrbGestureEngine
    {
        readonly HashSet<int> excludedPointers = new HashSet<int>();
        GestureTuning tuning;
        Vector2 startRawPosition;
        Vector2 previousRawPosition;

        public bool InputEnabled { get; private set; } = true;
        public int? ActivePointerId { get; private set; }
        public OrbRecord ActiveOrb { get; private set; }
        public OrbGestureDecision? PendingDecision { get; private set; }
        public bool HasActivePointer => ActivePointerId.HasValue;
        public bool HasPending => PendingDecision.HasValue;
        public Vector2 StartRawPosition => startRawPosition;
        public Vector2 LastRawPosition => previousRawPosition;
        public string LastReleaseReason { get; private set; } = "NONE";
        // Zero means this release never evaluated transfer distance (e.g. a launch/drop).
        public float LastTransferRequiredTravel { get; private set; }

        public bool Begin(int pointerId, OrbRecord orb, Vector2 rawPosition, bool startedOverUi,
            Rect lowerRect, float screenWidth, GestureTuning gestureTuning)
        {
            // A repeated Begin must not convert the owning pointer into an excluded pointer.
            if (ActivePointerId == pointerId || excludedPointers.Contains(pointerId)) return false;
            if (startedOverUi || !InputEnabled || HasActivePointer || HasPending || orb == null ||
                orb.AuthorityState != OrbAuthorityState.Idle || gestureTuning == null ||
                !IsFinite(rawPosition) || !IsValidGeometry(lowerRect, screenWidth) ||
                !lowerRect.Contains(rawPosition))
            {
                // UI starts and extra fingers stay excluded until their own Up/Cancel.
                excludedPointers.Add(pointerId);
                return false;
            }

            ActivePointerId = pointerId;
            ActiveOrb = orb;
            tuning = gestureTuning;
            startRawPosition = previousRawPosition = rawPosition;
            LastReleaseReason = "NONE";
            LastTransferRequiredTravel = 0f;
            return true;
        }

        public OrbGestureDecision? Move(int pointerId, Vector2 rawPosition,
            Rect lowerRect, float screenWidth)
        {
            if (!InputEnabled || ActivePointerId != pointerId || HasPending ||
                !IsFinite(rawPosition) || !IsValidGeometry(lowerRect, screenWidth)) return null;

            Vector2 previous = previousRawPosition;
            previousRawPosition = rawPosition;
            Rect zone = AttackZone(lowerRect, tuning.AttackZoneHeightFraction);
            // T09 explicitly opts into the upper edge of the lower play area. All earlier
            // controllers keep their existing band and entry semantics through the false default.
            bool enteredLaunchRegion = tuning.LaunchAtBattleBoundary
                ? previous.y < lowerRect.yMax && rawPosition.y >= lowerRect.yMax &&
                    SegmentIntersectsClosedRect(previous, rawPosition,
                        new Rect(lowerRect.xMin, lowerRect.yMax, lowerRect.width, 0f))
                : !ContainsClosed(zone, previous) && SegmentIntersectsClosedRect(previous, rawPosition, zone);
            // Launch wins if the same raw sample also qualifies as a horizontal swipe. The
            // segment test catches exact-edge contact and fast jumps past the battle boundary.
            if (!tuning.LaunchOnRelease && ActiveOrb.Kind == OrbKind.Combined && enteredLaunchRegion)
                return Reserve(OrbActionKind.Launch, null, rawPosition, lowerRect);

            Vector2 delta = rawPosition - startRawPosition;
            float horizontal = Mathf.Abs(delta.x);
            // Compare in the configured screen-normalized domain. Multiplying a fractional
            // threshold back into pixels can round an inclusive boundary above the raw sample.
            // Relative float equivalence handles the inclusive normalized boundary consistently
            // across runtimes; this is not a fixed pixel allowance or additional gesture tuning.
            float horizontalFraction = horizontal / screenWidth;
            if (tuning.AllowHorizontalTransfer && !tuning.TransferOnEdgeRelease && (horizontalFraction >= tuning.HorizontalDistanceFraction ||
                 Mathf.Approximately(horizontalFraction, tuning.HorizontalDistanceFraction)) &&
                horizontal >= Mathf.Abs(delta.y) * tuning.DominanceRatio)
                return Reserve(delta.x < 0f ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight,
                    null, rawPosition, lowerRect);
            return null;
        }

        public OrbGestureDecision? Up(int pointerId, Vector2 rawPosition, Rect lowerRect,
            float screenWidth, IReadOnlyList<OrbDropTarget> dropTargets, Vector2? dropPosition = null, bool allowReleaseLaunch = true)
        {
            excludedPointers.Remove(pointerId);
            LastReleaseReason = "NONE";
            LastTransferRequiredTravel = 0f;
            if (ActivePointerId != pointerId)
            {
                LastReleaseReason = "NO_ACTIVE_POINTER";
                return null;
            }

            OrbGestureDecision? decision = Move(pointerId, rawPosition, lowerRect, screenWidth);
            if (!HasPending && InputEnabled && tuning.LaunchOnRelease && allowReleaseLaunch &&
                ActiveOrb.Kind == OrbKind.Combined && IsFinite(rawPosition) && IsValidGeometry(lowerRect, screenWidth) &&
                rawPosition.y >= lowerRect.yMax && rawPosition.x >= lowerRect.xMin && rawPosition.x <= lowerRect.xMax)
                decision = Reserve(OrbActionKind.Launch, null, rawPosition, lowerRect);
            if (decision.HasValue)
            {
                LastReleaseReason = decision.Value.Kind == OrbActionKind.Launch
                    ? "LAUNCH_APPROVED_CANDIDATE" : "TRANSFER_APPROVED_CANDIDATE";
                if (decision.Value.Kind != OrbActionKind.Launch)
                    LastTransferRequiredTravel = tuning.HorizontalDistanceFraction * screenWidth;
            }
            if (!HasPending && InputEnabled && IsFinite(rawPosition) && IsFinite(dropPosition ?? rawPosition) &&
                IsValidGeometry(lowerRect, screenWidth) && lowerRect.Contains(rawPosition) &&
                ActiveOrb.Kind == OrbKind.Raw)
            {
                OrbRecord target = FindDropTarget(dropPosition ?? rawPosition, screenWidth, dropTargets);
                if (target != null)
                {
                    decision = Reserve(OrbActionKind.Combine, target.OrbId, rawPosition, lowerRect);
                    LastReleaseReason = "COMBINE_APPROVED_CANDIDATE";
                }
            }
            // T11 keeps the entire lower area available for repositioning/combination. Only
            // a release at the outgoing edge is a transfer; reaching an edge while held is not.
            // A drop on another orb (even an invalid combination) takes precedence at any x.
            if (!HasPending && InputEnabled && tuning.AllowHorizontalTransfer && tuning.TransferOnEdgeRelease &&
                (dropTargets == null || dropTargets.Count == 0) && IsFinite(rawPosition) &&
                IsValidGeometry(lowerRect, screenWidth) && rawPosition.y >= lowerRect.yMin && rawPosition.y < lowerRect.yMax)
            {
                Vector2 delta = rawPosition - startRawPosition;
                float fraction = Mathf.Abs(delta.x) / screenWidth;
                LastTransferRequiredTravel = tuning.HorizontalDistanceFraction * screenWidth;
                bool distance = fraction >= tuning.HorizontalDistanceFraction || Mathf.Approximately(fraction, tuning.HorizontalDistanceFraction);
                if (tuning.ReachableEdgeTransferDistance)
                {
                    // T12 opt-in: a first-column or received orb may have less than 18% of
                    // the display left before the outgoing edge. Derive a reachable gesture
                    // from its Down position, without changing the shared Config or old scenes.
                    // Within the outer edge band, a half-band floor can exceed the physical
                    // room remaining (1206px: Down1180 -> Up1205/1206 has only 25/26px).
                    // One eighth of the existing band retains deliberate movement without
                    // adding Config state. Starts outside the band keep their old threshold.
                    // A grab closer to the edge than this minimum still cannot transfer;
                    // accepting a tap or pixel jitter is not a substitute for outward travel.
                    float edgeBand = lowerRect.width * tuning.TransferEdgeFraction;
                    float outwardRoom = delta.x < 0f ? startRawPosition.x - lowerRect.xMin
                        : lowerRect.xMax - startRawPosition.x;
                    float requiredTravel = Mathf.Min(tuning.HorizontalDistanceFraction * screenWidth,
                        Mathf.Max(edgeBand * .125f, outwardRoom * .5f));
                    LastTransferRequiredTravel = requiredTravel;
                    // Compare the attainable end coordinate in the same float pixel space.
                    // Subtracting two large right-side positions and then normalizing loses
                    // precision (1206px: .9W -> .95W) and can reject an inclusive boundary.
                    // This adds no pixel tolerance: even a representable endpoint short of
                    // this boundary remains short; the earlier scene path above is unchanged.
                    float boundary = delta.x < 0f ? startRawPosition.x - requiredTravel
                        : startRawPosition.x + requiredTravel;
                    distance = delta.x < 0f ? rawPosition.x <= boundary : rawPosition.x >= boundary;
                }
                bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) * tuning.DominanceRatio;
                bool atEdge = delta.x < 0f ? rawPosition.x <= lowerRect.xMin + lowerRect.width * tuning.TransferEdgeFraction
                    : rawPosition.x >= lowerRect.xMax - lowerRect.width * tuning.TransferEdgeFraction;
                if (distance && horizontal && atEdge)
                {
                    decision = Reserve(delta.x < 0f ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight, null, rawPosition, lowerRect);
                    LastReleaseReason = "TRANSFER_APPROVED_CANDIDATE";
                }
                else LastReleaseReason = !distance ? "DISTANCE_TOO_SHORT"
                    : !horizontal ? "NOT_HORIZONTAL" : "NOT_AT_EDGE";
            }
            if (LastReleaseReason == "NONE")
            {
                // Diagnostics describe the same early gates above; they do not authorize
                // a request or change drop/launch/transfer precedence.
                if (HasPending) LastReleaseReason = "ACTION_ALREADY_PENDING";
                else if (!InputEnabled) LastReleaseReason = "INPUT_DISABLED";
                else if (!IsFinite(rawPosition)) LastReleaseReason = "INVALID_POSITION";
                else if (!IsValidGeometry(lowerRect, screenWidth)) LastReleaseReason = "INVALID_GEOMETRY";
                else if (ActiveOrb.Kind == OrbKind.Raw && rawPosition.y >= lowerRect.yMax) LastReleaseReason = "RAW_UPPER";
                else if (rawPosition.y < lowerRect.yMin || rawPosition.y >= lowerRect.yMax) LastReleaseReason = "OUTSIDE_LOWER";
                else if (dropTargets != null && dropTargets.Count > 0) LastReleaseReason = "DROP_TARGET";
                else if (!IsFinite(dropPosition ?? rawPosition)) LastReleaseReason = "INVALID_DROP_POSITION";
                else if (!tuning.AllowHorizontalTransfer) LastReleaseReason = "TRANSFER_DISABLED";
                else LastReleaseReason = "NO_RELEASE_ACTION";
            }
            ReleaseActivePointer();
            return decision;
        }

        public void Cancel(int pointerId)
        {
            excludedPointers.Remove(pointerId);
            if (ActivePointerId == pointerId) ReleaseActivePointer();
            // A cancelled finger cannot prove an outstanding request was rejected.
        }

        public void CancelAllPointers()
        {
            ReleaseActivePointer();
            excludedPointers.Clear();
            // The adapter calls this on focus/screen/session loss. Pending still needs a response.
        }

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;
            if (!enabled && ActivePointerId.HasValue)
            {
                excludedPointers.Add(ActivePointerId.Value);
                ReleaseActivePointer();
            }
        }

        /// <summary>
        /// Call only when the adapter explicitly resolves this candidate: a confirmed rejection,
        /// confirmed reset, or successful handoff to the authoritative pending registry. A successful
        /// handoff must keep registry-pending orbs unavailable to subsequent Begin calls.
        /// </summary>
        public bool ResolvePending(string orbId)
        {
            if (!PendingDecision.HasValue ||
                !StringComparer.Ordinal.Equals(PendingDecision.Value.OrbId, orbId)) return false;
            PendingDecision = null;
            if (ActivePointerId.HasValue)
            {
                // Resolving a request while a finger is down never starts another gesture.
                excludedPointers.Add(ActivePointerId.Value);
                ReleaseActivePointer();
            }
            return true;
        }

        public static Rect AttackZone(Rect lowerRect, float heightFraction)
        {
            return new Rect(lowerRect.xMin, lowerRect.yMax - lowerRect.height * heightFraction,
                lowerRect.width, lowerRect.height * heightFraction);
        }

        public static Vector2 NormalizeClamped(Vector2 rawPosition, Rect lowerRect)
        {
            return new Vector2(Mathf.Clamp01((rawPosition.x - lowerRect.xMin) / lowerRect.width),
                Mathf.Clamp01((rawPosition.y - lowerRect.yMin) / lowerRect.height));
        }

        OrbGestureDecision Reserve(OrbActionKind kind, string otherOrbId, Vector2 rawPosition, Rect lowerRect)
        {
            var decision = new OrbGestureDecision(kind, ActiveOrb.OrbId, otherOrbId,
                rawPosition, NormalizeClamped(rawPosition, lowerRect));
            PendingDecision = decision;
            return decision;
        }

        OrbRecord FindDropTarget(Vector2 rawPosition, float screenWidth,
            IReadOnlyList<OrbDropTarget> dropTargets)
        {
            if (dropTargets == null) return null;
            float nearestDistanceSquared = float.PositiveInfinity;
            OrbRecord nearest = null;
            for (int i = 0; i < dropTargets.Count; i++)
            {
                OrbDropTarget candidate = dropTargets[i];
                OrbRecord other = candidate.Orb;
                if (other == null || candidate.IsPending || !IsFinite(candidate.ScreenPosition) ||
                    other.Kind != OrbKind.Raw || other.AuthorityState != OrbAuthorityState.Idle ||
                    other.OwnerPlayerId != ActiveOrb.OwnerPlayerId ||
                    StringComparer.Ordinal.Equals(other.OrbId, ActiveOrb.OrbId) ||
                    !AreOpposite(ActiveOrb.Polarity, other.Polarity)) continue;
                float squared = (candidate.ScreenPosition - rawPosition).sqrMagnitude;
                float normalizedDistance = Mathf.Sqrt(squared) / screenWidth;
                if (normalizedDistance > tuning.DropDistanceFraction &&
                    !Mathf.Approximately(normalizedDistance, tuning.DropDistanceFraction)) continue;
                if (squared > nearestDistanceSquared) continue;
                if (nearest != null && squared == nearestDistanceSquared &&
                    StringComparer.Ordinal.Compare(other.OrbId, nearest.OrbId) >= 0) continue;
                nearest = other;
                nearestDistanceSquared = squared;
            }
            return nearest;
        }

        static bool AreOpposite(OrbPolarity first, OrbPolarity second)
        {
            return first == OrbPolarity.Yin && second == OrbPolarity.Yang ||
                first == OrbPolarity.Yang && second == OrbPolarity.Yin;
        }

        void ReleaseActivePointer()
        {
            ActivePointerId = null;
            ActiveOrb = null;
            tuning = null;
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        static bool IsValidGeometry(Rect rect, float screenWidth)
        {
            return IsFinite(screenWidth) && screenWidth > 0f &&
                IsFinite(rect.xMin) && IsFinite(rect.yMin) && IsFinite(rect.xMax) && IsFinite(rect.yMax) &&
                IsFinite(rect.width) && IsFinite(rect.height) && rect.width > 0f && rect.height > 0f;
        }

        static bool ContainsClosed(Rect rect, Vector2 point)
        {
            return point.x >= rect.xMin && point.x <= rect.xMax &&
                point.y >= rect.yMin && point.y <= rect.yMax;
        }

        static bool SegmentIntersectsClosedRect(Vector2 start, Vector2 end, Rect rect)
        {
            float entry = 0f;
            float exit = 1f;
            return ClipAxis(start.x, end.x - start.x, rect.xMin, rect.xMax, ref entry, ref exit) &&
                ClipAxis(start.y, end.y - start.y, rect.yMin, rect.yMax, ref entry, ref exit);
        }

        static bool ClipAxis(float start, float delta, float minimum, float maximum,
            ref float entry, ref float exit)
        {
            if (delta == 0f) return start >= minimum && start <= maximum;
            float first = (minimum - start) / delta;
            float second = (maximum - start) / delta;
            if (first > second) { float swap = first; first = second; second = swap; }
            entry = Mathf.Max(entry, first);
            exit = Mathf.Min(exit, second);
            return entry <= exit;
        }
    }
}
