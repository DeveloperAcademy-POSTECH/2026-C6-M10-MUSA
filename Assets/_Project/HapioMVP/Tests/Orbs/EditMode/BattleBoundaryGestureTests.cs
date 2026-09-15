using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class BattleBoundaryGestureTests
    {
        private static readonly Rect Lower = new Rect(0f, 0f, 1000f, 600f);
        private static readonly GestureTuning Boundary = new GestureTuning(.18f, 1.25f, .08f, .18f, true);
        private static readonly GestureTuning FreeWorkspace = new GestureTuning(.18f, 1.25f, .08f, .18f, true, false);
        private OrbGestureEngine engine;

        [SetUp] public void SetUp() => engine = new OrbGestureEngine();

        [Test]
        public void DefaultConstructorKeepsEarlierBandLaunchAndHorizontalTransfer()
        {
            var original = new GestureTuning(.18f, 1.25f, .08f, .18f);
            Assert.That(original.LaunchAtBattleBoundary, Is.False);
            Assert.That(original.AllowHorizontalTransfer, Is.True);
            Begin(Combined(), new Vector2(500f, 400f), original);
            Assert.That(engine.Move(1, new Vector2(500f, 500f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
            engine = new OrbGestureEngine();
            Begin(Raw(), new Vector2(500f, 300f), original);
            Assert.That(engine.Move(1, new Vector2(680f, 300f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
        }

        [Test]
        public void OldBandIsOrdinaryDragSpaceUntilTheActualBattleBoundary()
        {
            Begin(Combined(), new Vector2(500f, 400f));
            foreach (float y in new[] { 492f, 550f, 599.99f })
            {
                Assert.That(engine.Move(1, new Vector2(500f, y), Lower, 1000f), Is.Null);
                Assert.That(engine.HasPending, Is.False);
            }
            Assert.That(engine.Move(1, new Vector2(500f, 600f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void StartingInsideTheFormerBandCanLaunchWithoutAnExitAndReentry()
        {
            Begin(Combined(), new Vector2(500f, 550f));
            Assert.That(engine.Move(1, new Vector2(500f, 580f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(500f, 601f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [TestCase(600f)]
        [TestCase(601f)]
        [TestCase(950f)]
        public void ExactBoundaryAndFastJumpsReserveOneLaunchWithOriginalRawEndpoint(float endY)
        {
            Begin(Combined(), new Vector2(500f, 400f));
            Vector2 endpoint = new Vector2(500f, endY);
            var first = engine.Move(1, endpoint, Lower, 1000f);
            Assert.That(first?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(first?.RawPosition, Is.EqualTo(endpoint));
            Assert.That(first?.NormalizedPosition, Is.EqualTo(new Vector2(.5f, 1f)));
            Assert.That(engine.Move(1, new Vector2(500f, 1100f), Lower, 1000f), Is.Null);
            Assert.That(engine.Up(1, new Vector2(500f, 1200f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.PendingDecision?.RawPosition, Is.EqualTo(endpoint));
            Assert.That(engine.HasActivePointer, Is.False);
        }

        [TestCase(50f, -50f)]
        [TestCase(950f, 1050f)]
        public void SegmentPassingExactlyThroughEitherBoundaryCornerLaunches(float startX, float endX)
        {
            Begin(Combined(), new Vector2(startX, 550f));
            Assert.That(engine.Move(1, new Vector2(endX, 650f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void CrossingOutsideTheHorizontalBoundaryThenMovingAboveItDoesNotInventEntry()
        {
            Begin(Combined(), new Vector2(990f, 550f));
            Assert.That(engine.Move(1, new Vector2(1100f, 650f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 650f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 590f), Lower, 1000f), Is.Null,
                "Returning downward into the workspace is not an upward launch.");
            Assert.That(engine.Move(1, new Vector2(900f, 610f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void BoundaryLaunchOutranksHorizontalTransferWhenOneSampleQualifiesForBoth()
        {
            Begin(Combined(), new Vector2(500f, 550f));
            Assert.That(engine.Move(1, new Vector2(900f, 610f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void EarlierTransferStillCannotBecomeALaterBoundaryLaunchWhenTransferIsEnabled()
        {
            Begin(Combined(), new Vector2(500f, 400f));
            Assert.That(engine.Move(1, new Vector2(700f, 400f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.TransferRight));
            Assert.That(engine.Move(1, new Vector2(700f, 700f), Lower, 1000f), Is.Null);
            Assert.That(engine.PendingDecision?.Kind, Is.EqualTo(OrbActionKind.TransferRight));
        }

        [Test]
        public void FinalUpSampleCanCrossTheBoundaryWithoutAPrecedingMoveEvent()
        {
            Begin(Combined(), new Vector2(500f, 550f));
            Assert.That(engine.Up(1, new Vector2(500f, 750f), Lower, 1000f, null)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
            Assert.That(engine.HasPending, Is.True);
            Assert.That(engine.HasActivePointer, Is.False);
        }

        [Test]
        public void CombinedReleaseWithinTheFormerBandDoesNotLaunch()
        {
            Begin(Combined(), new Vector2(500f, 400f));
            Assert.That(engine.Up(1, new Vector2(500f, 550f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.HasPending, Is.False);
        }

        [Test]
        public void RawCrossingBoundaryRemainsAnUnconsumedCombinationMaterial()
        {
            var raw = Raw();
            Begin(raw, new Vector2(500f, 400f), FreeWorkspace);
            Assert.That(engine.Move(1, new Vector2(500f, 750f), Lower, 1000f), Is.Null);
            Assert.That(engine.Up(1, new Vector2(500f, 750f), Lower, 1000f, null), Is.Null);
            Assert.That(engine.HasPending, Is.False);
            Assert.That(raw.Kind, Is.EqualTo(OrbKind.Raw));
            Assert.That(raw.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
        }

        [Test]
        public void RawCanReturnFromAboveBoundaryAndCombineOnReleaseInTheFormerBand()
        {
            Begin(Raw(), new Vector2(500f, 400f), FreeWorkspace);
            Assert.That(engine.Move(1, new Vector2(500f, 750f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(530f, 550f), Lower, 1000f), Is.Null);
            var target = new OrbDropTarget(Raw("target", OrbPolarity.Yang), new Vector2(530f, 550f));
            var result = engine.Up(1, new Vector2(530f, 550f), Lower, 1000f, new[] { target });
            Assert.That(result?.Kind, Is.EqualTo(OrbActionKind.Combine));
            Assert.That(result?.OtherOrbId, Is.EqualTo("target"));
        }

        [TestCase(100f, 900f)]
        [TestCase(900f, 100f)]
        public void DisabledTransferLetsRawCrossTheWholeWorkspaceAndCombineOnlyOnUp(float startX, float endX)
        {
            Begin(Raw(), new Vector2(startX, 300f), FreeWorkspace);
            Vector2 end = new Vector2(endX, 300f);
            var target = new OrbDropTarget(Raw("target", OrbPolarity.Yang), end);
            for (int sample = 1; sample <= 8; sample++)
            {
                Assert.That(engine.Move(1, Vector2.Lerp(new Vector2(startX, 300f), end, sample / 8f), Lower, 1000f), Is.Null);
                Assert.That(engine.HasActivePointer, Is.True);
                Assert.That(engine.HasPending, Is.False);
            }
            Assert.That(engine.Up(1, end, Lower, 1000f, new[] { target })?.Kind,
                Is.EqualTo(OrbActionKind.Combine));
        }

        [Test]
        public void DisabledTransferLetsCombinedMoveAcrossWorkspaceThenLaunchAtBoundary()
        {
            Begin(Combined(), new Vector2(100f, 300f), FreeWorkspace);
            Assert.That(engine.Move(1, new Vector2(900f, 300f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 550f), Lower, 1000f), Is.Null);
            Assert.That(engine.Move(1, new Vector2(900f, 650f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        [Test]
        public void OffsetLowerRectDefinesBoundaryRatherThanScreenHeightOrOldBand()
        {
            var offset = new Rect(100f, 200f, 800f, 400f);
            Assert.That(engine.Begin(1, Combined(), new Vector2(500f, 450f), false, offset, 1200f, Boundary), Is.True);
            Assert.That(engine.Move(1, new Vector2(500f, 599.99f), offset, 1200f), Is.Null);
            var result = engine.Move(1, new Vector2(500f, 600f), offset, 1200f);
            Assert.That(result?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(result?.NormalizedPosition, Is.EqualTo(new Vector2(.5f, 1f)));
        }

        [Test]
        public void InvalidSampleDoesNotPoisonTheFollowingBoundaryCrossing()
        {
            Begin(Combined(), new Vector2(500f, 550f));
            Assert.That(engine.Move(1, new Vector2(500f, float.PositiveInfinity), Lower, 1000f), Is.Null);
            Assert.That(engine.LastRawPosition, Is.EqualTo(new Vector2(500f, 550f)));
            Assert.That(engine.Move(1, new Vector2(500f, 601f), Lower, 1000f)?.Kind,
                Is.EqualTo(OrbActionKind.Launch));
        }

        private void Begin(OrbRecord orb, Vector2 point, GestureTuning tuning = null)
            => Assert.That(engine.Begin(1, orb, point, false, Lower, 1000f, tuning ?? Boundary), Is.True);

        private static OrbRecord Raw(string id = "source", OrbPolarity polarity = OrbPolarity.Yin)
            => new OrbRecord(id, OrbKind.Raw, polarity, 1, OrbAuthorityState.Idle,
                new Vector2(.5f, .5f), EntrySide.None, 0);
        private static OrbRecord Combined()
            => new OrbRecord("source", OrbKind.Combined, OrbPolarity.None, 1, OrbAuthorityState.Idle,
                new Vector2(.5f, .5f), EntrySide.None, 0);
    }
}
