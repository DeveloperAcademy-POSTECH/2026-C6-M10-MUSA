using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class OrbGestureEngineTests
    {
        static readonly Rect Lower = new Rect(0f, 0f, 1000f, 600f);
        static readonly GestureTuning Tuning = new GestureTuning(.18f, 1.25f, .08f, .18f);
        OrbGestureEngine engine;

        [SetUp] public void SetUp() => engine = new OrbGestureEngine();

        [Test]
        public void UiStartCannotBecomeOrbGestureUntilThatPointerEnds()
        {
            Assert.That(Begin(null, 7, true), Is.False);
            Assert.That(Begin(Raw(), 7), Is.False);
            Assert.That(engine.Move(7, new Vector2(800f, 250f), Lower, 1000f), Is.Null);
            engine.Up(7, new Vector2(500f, 250f), Lower, 1000f, null);
            Assert.That(Begin(Raw(), 7), Is.True);
        }

        [Test]
        public void ExtraPointerCannotStealAndIsIgnoredUntilItsOwnUp()
        {
            Assert.That(Begin(Raw(), 1), Is.True);
            Assert.That(Begin(Raw(), 2), Is.False);
            Assert.That(engine.Move(2, new Vector2(900f, 250f), Lower, 1000f), Is.Null);
            Assert.That(engine.ActivePointerId, Is.EqualTo(1));
            engine.Up(1, new Vector2(500f, 250f), Lower, 1000f, null);
            Assert.That(Begin(Raw("second"), 2), Is.False);
            engine.Up(2, new Vector2(500f, 250f), Lower, 1000f, null);
            Assert.That(Begin(Raw("second"), 2), Is.True);
        }

        [Test]
        public void DuplicateBeginDoesNotBreakTheOwningPointer()
        {
            Begin(Raw(), 1);
            Assert.That(Begin(Raw("second"), 1), Is.False);
            Assert.That(engine.Move(1, new Vector2(700f, 250f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
        }

        [TestCase(680f, OrbActionKind.TransferRight)]
        [TestCase(320f, OrbActionKind.TransferLeft)]
        public void HorizontalDistanceUsesScreenWidthAndIncludesThreshold(float x, OrbActionKind expected)
        {
            Assert.That(Begin(Raw()), Is.True, "The boundary fixture must start a valid pointer.");
            var below = new Vector2(500f + Mathf.Sign(x - 500f) * 179f, 250f);
            Assert.That(engine.Move(1, below, Lower, 1000f), Is.Null,
                "A movement clearly below the configured fraction must remain a drag.");
            var decision = engine.Move(1, new Vector2(x, 250f), Lower, 1000f);
            Assert.That(decision?.Kind, Is.EqualTo(expected),
                BoundaryDiagnostics(Mathf.Abs(x - 500f), 1000f, Tuning.HorizontalDistanceFraction));
            Assert.That(decision?.OrbId, Is.EqualTo("source"));
            Assert.That(decision?.OtherOrbId, Is.Null);
            Assert.That(engine.HasPending, Is.True);
        }

        [Test]
        public void HorizontalDominanceIsDistanceBasedAndRejectsSteepMotion()
        {
            Begin(Raw());
            Assert.That(engine.Move(1, new Vector2(700f, 411f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(700f, 410f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
        }

        [Test]
        public void EarlierSwipeCannotBeReplacedByLaterZoneEntryOrDrop()
        {
            Begin(Combined());
            var first = engine.Move(1, new Vector2(700f, 250f), Lower, 1000f);
            Assert.That(first?.Kind, Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(engine.Move(1, new Vector2(750f, 570f), Lower, 1000f), Is.Null);
            Assert.That(engine.Up(1, new Vector2(750f, 570f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.PendingDecision?.Kind, Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(engine.HasActivePointer, Is.False);
        }

        [Test]
        public void ZoneOutranksSwipeWhenBothBecomeTrueInOneSample()
        {
            Begin(Combined());
            var decision = engine.Move(1, new Vector2(900f, 500f), Lower, 1000f);
            Assert.That(decision?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(engine.Move(1, new Vector2(0f, 0f), Lower, 1000f), Is.Null);
            Assert.That(engine.PendingDecision?.Kind, Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void RawEnteringZoneNeverRequestsLaunchOrChangesItsRecord()
        {
            OrbRecord raw = Raw();
            Begin(raw);
            Assert.That(engine.Move(1, new Vector2(500f, 700f), Lower, 1000f), Is.Null);
            Assert.That(engine.Up(1, new Vector2(500f, 700f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.HasPending, Is.False);
            Assert.That(raw.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(raw.Kind, Is.EqualTo(OrbKind.Raw));
        }

        [Test]
        public void RawStillTransfersWhenZoneAndHorizontalConditionsCoincide()
        {
            Begin(Raw());
            Assert.That(engine.Move(1, new Vector2(900f, 500f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
        }

        [Test]
        public void FastJumpThroughZoneLaunchesEvenWithEndpointOutsideViewport()
        {
            Begin(Combined());
            var result = engine.Move(1, new Vector2(500f, 950f), Lower, 1000f);
            Assert.That(result?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(result?.RawPosition, Is.EqualTo(new Vector2(500f, 950f)));
            Assert.That(result?.NormalizedPosition, Is.EqualTo(new Vector2(.5f, 1f)));
        }

        [Test]
        public void CornerCrossingUsesRawSegmentAndZoneWinsAtCorner()
        {
            Assert.That(engine.Begin(1, Combined(), new Vector2(950f, 450f), false,
                Lower, 1000f, Tuning), Is.True);
            // Cross x = 1000 at y = 500, inside the zone, before ending outside the viewport.
            Assert.That(engine.Move(1, new Vector2(1200f, 700f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void MovementAboveZoneOutsideItsHorizontalExtentDoesNotInventAnEntry()
        {
            Assert.That(engine.Begin(1, Combined(), new Vector2(990f, 450f), false,
                Lower, 1000f, Tuning), Is.True);
            // Leaves the side before reaching the band and never crosses back through it.
            Assert.That(engine.Move(1, new Vector2(1100f, 550f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(1100f, 700f), Lower, 1000f), Is.Null);
        }

        [Test]
        public void PointerStartingInsideZoneNeedsARealExitAndReentry()
        {
            Assert.That(engine.Begin(1, Combined(), new Vector2(500f, 550f), false,
                Lower, 1000f, Tuning), Is.True);
            Assert.That(engine.Move(1, new Vector2(500f, 570f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(500f, 450f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(500f, 510f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void RawPositionTriggersEdgeSwipeBeforeDisplayClamp()
        {
            Assert.That(engine.Begin(1, Raw(), new Vector2(950f, 250f), false,
                Lower, 1000f, Tuning), Is.True);
            var decision = engine.Move(1, new Vector2(1150f, 250f), Lower, 1000f);
            Assert.That(decision?.Kind, Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(decision?.RawPosition.x, Is.EqualTo(1150f));
            Assert.That(decision?.NormalizedPosition.x, Is.EqualTo(1f));
        }

        [Test]
        public void PassingOverOppositeRawDoesNothingUntilExplicitUp()
        {
            Begin(Raw());
            var targets = new[] { new OrbDropTarget(Raw("other", OrbPolarity.Yang), new Vector2(530f, 260f)) };
            Assert.That(engine.Move(1, new Vector2(530f, 260f), Lower, 1000f), Is.Null);
            Assert.That(engine.HasPending, Is.False);
            var decision = engine.Up(1, new Vector2(530f, 260f), Lower, 1000f, targets);
            Assert.That(decision?.Kind, Is.EqualTo(OrbActionKind.Combine));
            Assert.That(decision?.OtherOrbId, Is.EqualTo("other"));
        }

        [Test]
        public void UpEvaluatesFinalSwipeBeforeNearbyCombination()
        {
            Begin(Raw());
            var targets = new[] { new OrbDropTarget(Raw("other", OrbPolarity.Yang), new Vector2(750f, 250f)) };
            var decision = engine.Up(1, new Vector2(750f, 250f), Lower, 1000f, targets);
            Assert.That(decision?.Kind, Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(decision?.OtherOrbId, Is.Null);
        }

        [Test]
        public void DropSelectsNearestThenOrdinalIdRegardlessOfCandidateOrder()
        {
            var far = new OrbDropTarget(Raw("0-far", OrbPolarity.Yang), new Vector2(550f, 250f));
            var equalB = new OrbDropTarget(Raw("b", OrbPolarity.Yang), new Vector2(510f, 250f));
            var equalA = new OrbDropTarget(Raw("a", OrbPolarity.Yang), new Vector2(490f, 250f));
            foreach (var targets in new[] { new[] { far, equalB, equalA }, new[] { equalA, far, equalB } })
            {
                engine = new OrbGestureEngine();
                Begin(Raw());
                Assert.That(engine.Up(1, new Vector2(500f, 250f), Lower, 1000f, targets)?.OtherOrbId,
                    Is.EqualTo("a"));
            }
        }

        [Test]
        public void DropExcludesWrongKindPolarityOwnerIdentityStatePendingAndDistance()
        {
            Begin(Raw());
            Vector2 at = new Vector2(500f, 250f);
            var targets = new[]
            {
                new OrbDropTarget(Raw("source", OrbPolarity.Yang), at),
                new OrbDropTarget(Raw("same", OrbPolarity.Yin), at),
                new OrbDropTarget(Raw("owner", OrbPolarity.Yang, 2), at),
                new OrbDropTarget(Combined("combined"), at),
                new OrbDropTarget(Raw("consumed", OrbPolarity.Yang, 1, OrbAuthorityState.Consumed), at),
                new OrbDropTarget(Raw("pending", OrbPolarity.Yang), at, true),
                new OrbDropTarget(Raw("far", OrbPolarity.Yang), at + new Vector2(80.1f, 0f)),
                new OrbDropTarget(Raw("nan", OrbPolarity.Yang), new Vector2(float.NaN, 250f)),
                new OrbDropTarget(null, at)
            };
            Assert.That(engine.Up(1, at, Lower, 1000f, targets), Is.Null);
            Assert.That(engine.HasPending, Is.False);
        }

        [Test]
        public void DropRadiusUsesScreenWidthRatherThanLowerRectHeight()
        {
            Assert.That(Begin(Raw()), Is.True, "The boundary fixture must start a valid pointer.");
            var target = new OrbDropTarget(Raw("edge", OrbPolarity.Yang), new Vector2(580f, 250f));
            Assert.That(engine.Up(1, new Vector2(500f, 250f), Lower, 1000f, new[] { target })?.Kind,
                Is.EqualTo(OrbActionKind.Combine), BoundaryDiagnostics(80f, 1000f, Tuning.DropDistanceFraction));
        }

        [Test]
        public void ReleaseOutsideLowerRectDoesNotCombineWithNearbyBorderTarget()
        {
            Assert.That(engine.Begin(1, Raw(), new Vector2(20f, 250f), false,
                Lower, 1000f, Tuning), Is.True);
            var target = new OrbDropTarget(Raw("border", OrbPolarity.Yang), new Vector2(20f, 250f));
            Assert.That(engine.Up(1, new Vector2(-10f, 250f), Lower, 1000f, new[] { target }), Is.Null);
            Assert.That(engine.HasPending, Is.False);
            Assert.That(engine.HasActivePointer, Is.False);
        }

        [Test]
        public void CancelReleasesDragAndAllowsFreshGestureWithoutMakingRequest()
        {
            Begin(Raw());
            engine.Cancel(99);
            Assert.That(engine.ActivePointerId, Is.EqualTo(1));
            engine.Cancel(1);
            Assert.That(engine.HasActivePointer, Is.False);
            Assert.That(engine.HasPending, Is.False);
            Assert.That(Begin(Raw(), 2), Is.True);
        }

        [Test]
        public void PendingSurvivesUpCancelFocusLossAndInputDisableUntilConfirmedResolution()
        {
            Begin(Combined());
            engine.Move(1, new Vector2(500f, 550f), Lower, 1000f);
            engine.Cancel(1);
            engine.CancelAllPointers();
            engine.SetInputEnabled(false);
            engine.SetInputEnabled(true);
            Assert.That(engine.HasPending, Is.True);
            Assert.That(Begin(Raw("other"), 2), Is.False);
            Assert.That(engine.ResolvePending("wrong-id"), Is.False);
            Assert.That(engine.ResolvePending("source"), Is.True);
            // The second finger began while pending and must end before becoming eligible.
            Assert.That(Begin(Raw("other"), 2), Is.False);
            engine.Up(2, new Vector2(500f, 250f), Lower, 1000f, null);
            Assert.That(Begin(Raw("other"), 2), Is.True);
        }

        [Test]
        public void ResolvingPendingWhileFingerRemainsDownRequiresNewGesture()
        {
            Begin(Raw());
            engine.Move(1, new Vector2(750f, 250f), Lower, 1000f);
            Assert.That(engine.ResolvePending("source"), Is.True);
            Assert.That(engine.HasActivePointer, Is.False);
            Assert.That(engine.Move(1, new Vector2(900f, 250f), Lower, 1000f), Is.Null);
            Assert.That(Begin(Raw(), 1), Is.False);
            engine.Up(1, new Vector2(500f, 250f), Lower, 1000f, null);
            Assert.That(Begin(Raw(), 1), Is.True);
        }

        [Test]
        public void DisableCancelsPointerAndDoesNotResumeItOnReenable()
        {
            Begin(Raw());
            engine.SetInputEnabled(false);
            Assert.That(engine.HasActivePointer, Is.False);
            Assert.That(engine.Move(1, new Vector2(900f, 250f), Lower, 1000f), Is.Null);
            Assert.That(Begin(Raw(), 2), Is.False);
            engine.SetInputEnabled(true);
            Assert.That(Begin(Raw(), 1), Is.False);
            Assert.That(Begin(Raw(), 2), Is.False);
            engine.Cancel(1);
            engine.Cancel(2);
            Assert.That(Begin(Raw(), 1), Is.True);
        }

        [Test]
        public void InactiveAuthorityCannotBeSelectedAndNoNewGestureMeansNoAction()
        {
            Assert.That(Begin(Raw("unavailable", OrbPolarity.Yin, 1, OrbAuthorityState.Projectile)), Is.False);
            engine.CancelAllPointers();
            Assert.That(engine.Move(1, new Vector2(900f, 550f), Lower, 1000f), Is.Null);
            Assert.That(engine.Up(1, new Vector2(900f, 550f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.HasPending, Is.False);
        }

        [Test]
        public void InvalidRawSamplesOrGeometryDoNotCreateRequestsOrPoisonNextValidSample()
        {
            Begin(Raw());
            Assert.That(engine.Move(1, new Vector2(float.NaN, 100f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 100f), new Rect(), 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 100f), Lower, float.PositiveInfinity), Is.Null);
            Assert.That(engine.LastRawPosition, Is.EqualTo(new Vector2(500f, 250f)));
            Assert.That(engine.Move(1, new Vector2(700f, 250f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
        }

        [Test]
        public void OffsetViewportAndInjectedThresholdsDetermineActions()
        {
            Rect offset = new Rect(100f, 200f, 800f, 400f);
            var custom = new GestureTuning(.3f, 2f, .04f, .25f);
            Assert.That(engine.Begin(1, Combined(), new Vector2(500f, 300f), false,
                offset, 1200f, custom), Is.True);
            Assert.That(OrbGestureEngine.AttackZone(offset, custom.AttackZoneHeightFraction),
                Is.EqualTo(new Rect(100f, 500f, 800f, 100f)));
            Assert.That(engine.Move(1, new Vector2(800f, 300f), offset, 1200f), Is.Null);
            var decision = engine.Move(1, new Vector2(860f, 300f), offset, 1200f);
            Assert.That(decision?.Kind, Is.EqualTo(OrbActionKind.TransferRight),
                BoundaryDiagnostics(360f, 1200f, custom.HorizontalDistanceFraction));
            Assert.That(decision?.NormalizedPosition, Is.EqualTo(new Vector2(.95f, .25f)));
        }

        [Test]
        public void TuningRejectsNonfiniteAndNonpositiveValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GestureTuning(0f, 1f, .1f, .2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GestureTuning(.1f, float.NaN, .1f, .2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GestureTuning(.1f, 1f, -1f, .2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GestureTuning(.1f, 1f, .1f, 1.1f));
        }

        bool Begin(OrbRecord orb, int pointerId = 1, bool overUi = false) =>
            engine.Begin(pointerId, orb, new Vector2(500f, 250f), overUi, Lower, 1000f, Tuning);

        string BoundaryDiagnostics(float distance, float width, float threshold)
        {
            float fraction = distance / width;
            return string.Format(CultureInfo.InvariantCulture,
                "distance={0:R}, width={1:R}, fraction={2:R} (double {3:G17}), " +
                "threshold={4:R} (double {5:G17}), >= {6}, <= {7}, approximately={8}, " +
                "active={9}, pointer={10}, pending={11}, lastRaw={12}, orb={13}",
                distance, width, fraction, (double)fraction, threshold, (double)threshold,
                fraction >= threshold, fraction <= threshold, Mathf.Approximately(fraction, threshold),
                engine.HasActivePointer, engine.ActivePointerId, engine.HasPending, engine.LastRawPosition,
                engine.ActiveOrb?.OrbId ?? "none");
        }

        static OrbRecord Raw(string id = "source", OrbPolarity polarity = OrbPolarity.Yin,
            ulong owner = 1, OrbAuthorityState state = OrbAuthorityState.Idle) =>
            new OrbRecord(id, OrbKind.Raw, polarity, owner, state, new Vector2(.5f, .5f), EntrySide.None, 0);

        static OrbRecord Combined(string id = "source") =>
            new OrbRecord(id, OrbKind.Combined, OrbPolarity.None, 1, OrbAuthorityState.Idle,
                new Vector2(.5f, .5f), EntrySide.None, 0);
    }
}
