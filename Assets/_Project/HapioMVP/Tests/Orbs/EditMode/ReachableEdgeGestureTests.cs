using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class ReachableEdgeGestureTests
    {
        private static GestureTuning Tuning(bool reachable = true, bool release = true)
            => new GestureTuning(.18f, 1.25f, .08f, .18f, true, true, release, .055f, reachable);
        private static Rect Lower(float width) => new Rect(0, 0, width, (width == 1206 ? 2622 : 2266) * .45f);
        private static OrbRecord Orb(string id = "a", OrbKind kind = OrbKind.Raw, OrbPolarity polarity = OrbPolarity.Yin)
            => new OrbRecord(id, kind, kind == OrbKind.Combined ? OrbPolarity.None : polarity, 1,
                OrbAuthorityState.Idle, new Vector2(.5f, .5f), EntrySide.None, 0);
        private static OrbGestureEngine Begin(float width, float x, OrbKind kind = OrbKind.Raw, bool reachable = true)
        {
            var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1, Orb(kind: kind), new Vector2(width * x, Lower(width).center.y), false, Lower(width), width, Tuning(reachable)), Is.True);
            return engine;
        }

        // Actual iPhone/iPad pixel widths. Spawn-column Raw and opposite-edge received Combined
        // can be sent outward directly, without first releasing and grabbing them in the center.
        [TestCase(1206f, .1f, OrbKind.Raw)] [TestCase(1206f, .9f, OrbKind.Raw)]
        [TestCase(1488f, .1f, OrbKind.Raw)] [TestCase(1488f, .9f, OrbKind.Raw)]
        [TestCase(1206f, .055f, OrbKind.Combined)] [TestCase(1206f, .945f, OrbKind.Combined)]
        [TestCase(1488f, .055f, OrbKind.Combined)] [TestCase(1488f, .945f, OrbKind.Combined)]
        public void NearEdgeSpawnAndReceivedOrbCanTransferOutwardOnRelease(float width, float start, OrbKind kind)
        {
            var engine = Begin(width, start, kind); var lower = Lower(width);
            var end = new Vector2(width * (start < .5f ? .01f : .99f), lower.center.y);
            Assert.That(engine.Move(1, end, lower, width), Is.Null, "Held edge is still local.");
            var result = engine.Up(1, end, lower, width, null);
            Assert.That(result?.Kind, Is.EqualTo(start < .5f ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight));
            Assert.That(engine.Up(1, end, lower, width, null), Is.Null, "One release cannot submit twice.");
        }

        [Test]
        public void EarlierScenesKeepTheUnchangedEighteenPercentRequirement()
        {
            Assert.That(Tuning(false).ReachableEdgeTransferDistance, Is.False);
            foreach (float width in new[] { 1206f, 1488f })
                foreach (float start in new[] { .1f, .9f, .055f, .945f })
                {
                    var engine = Begin(width, start, reachable: false);
                    Assert.That(engine.Up(1, new Vector2(width * (start < .5f ? .01f : .99f), Lower(width).center.y), Lower(width), width, null), Is.Null);
                    Assert.That(engine.HasPending, Is.False);
                }
        }

        [TestCase(1206f)] [TestCase(1488f)]
        public void DerivedThresholdIsInclusiveAndRejectsShorterOutwardMotion(float width)
        {
            // Down=.1W => max(.0275W, .05W), capped by .18W => .05W.
            // Both releases are inside the .055W edge band, isolating the distance condition.
            foreach (bool left in new[] { true, false })
            {
                float start = left ? .1f : .9f;
                var engine = Begin(width, start);
                Assert.That(engine.Up(1, new Vector2(width * (left ? .051f : .949f), Lower(width).center.y), Lower(width), width, null), Is.Null);
                engine = Begin(width, start);
                float inclusiveX = width * (left ? .05f : .95f);
                // 1206px right-side coordinates are 1085.4000244 -> 1145.6999512.
                // Their subtracted/normalized distance loses precision; do not fix this by
                // admitting genuinely shorter gestures or loosening this boundary assertion.
                Assert.That(engine.Up(1, new Vector2(inclusiveX + (left ? 1f : -1f), Lower(width).center.y), Lower(width), width, null), Is.Null,
                    "A release one pixel before the inclusive boundary remains too short.");
                engine = Begin(width, start);
                Assert.That(engine.Up(1, new Vector2(width * (left ? .05f : .95f), Lower(width).center.y), Lower(width), width, null), Is.Not.Null);
            }
        }

        [Test]
        public void TapsSmallJitterHoldingAndCancellationNeverTransfer()
        {
            foreach (float width in new[] { 1206f, 1488f })
                foreach (float start in new[] { .04f, .96f })
                {
                    var lower = Lower(width); var point = new Vector2(width * start, lower.center.y);
                    var engine = Begin(width, start);
                    Assert.That(engine.Up(1, point, lower, width, null), Is.Null);
                    engine = Begin(width, start);
                    var jitter = point + Vector2.right * (start < .5f ? -1f : 1f);
                    Assert.That(engine.Up(1, jitter, lower, width, null), Is.Null, "The derived minimum rejects pixel jitter.");
                    engine = Begin(width, start); var edge = new Vector2(start < .5f ? 0 : width - 1, point.y);
                    for (int i = 0; i < 3; i++) Assert.That(engine.Move(1, edge, lower, width), Is.Null);
                    engine.Cancel(1);
                    Assert.That(engine.Up(1, edge, lower, width, null), Is.Null);
                    Assert.That(engine.HasPending, Is.False);
                }
        }

        [Test]
        public void AReceivedOrbNeedsANewPointerAndItsOwnOutwardMotion()
        {
            const float width = 1206; var lower = Lower(width); var engine = new OrbGestureEngine();
            var entry = new Vector2(width * .055f, lower.center.y); var edge = new Vector2(0, entry.y);
            Assert.That(engine.Move(1, edge, lower, width), Is.Null);
            Assert.That(engine.Up(1, edge, lower, width, null), Is.Null);
            Assert.That(engine.Begin(1, Orb(), entry, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.Up(1, edge, lower, width, null)?.Kind, Is.EqualTo(OrbActionKind.TransferLeft));
            Assert.That(engine.ResolvePending("a"), Is.True);
            Assert.That(engine.Move(1, edge, lower, width), Is.Null);
            Assert.That(engine.Up(1, edge, lower, width, null), Is.Null);
        }

        [Test]
        public void InteriorPlacementAndInwardMotionAtAnEdgeRemainLocal()
        {
            foreach (float width in new[] { 1206f, 1488f })
            {
                var lower = Lower(width);
                foreach (var pair in new[] { new Vector2(.1f, .9f), new Vector2(.9f, .1f), new Vector2(.01f, .05f), new Vector2(.99f, .95f) })
                {
                    var engine = Begin(width, pair.x);
                    Assert.That(engine.Up(1, new Vector2(width * pair.y, lower.center.y), lower, width, null), Is.Null);
                    Assert.That(engine.HasPending, Is.False);
                }
            }
        }

        [Test]
        public void CenterAndOppositeEdgeTransfersRetainTheOriginalDistanceAndDirection()
        {
            foreach (float width in new[] { 1206f, 1488f })
                foreach (var pair in new[] { new Vector2(.5f, .01f), new Vector2(.5f, .99f), new Vector2(.055f, .99f), new Vector2(.945f, .01f) })
                {
                    var engine = Begin(width, pair.x);
                    var result = engine.Up(1, new Vector2(width * pair.y, Lower(width).center.y), Lower(width), width, null);
                    Assert.That(result?.Kind, Is.EqualTo(pair.y < pair.x ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight));
                }
        }

        [Test]
        public void DirectionDominanceAndValidLowerHeightAreStillRequired()
        {
            const float width = 1206; var lower = Lower(width);
            var engine = Begin(width, .1f);
            Assert.That(engine.Up(1, new Vector2(width * .01f, lower.center.y + width * .2f), lower, width, null), Is.Null);
            foreach (float height in new[] { -1f, lower.yMax, lower.yMax + 1, float.NaN, float.PositiveInfinity })
            {
                engine = Begin(width, .1f);
                Assert.That(engine.Up(1, new Vector2(width * .01f, height), lower, width, null), Is.Null);
            }
        }

        [Test]
        public void GeometryUsesTheActualLowerViewportOriginAndWidth()
        {
            const float screenWidth = 1206; var lower = new Rect(120, 102, 900, 800);
            foreach (bool left in new[] { true, false })
            {
                var engine = new OrbGestureEngine(); var start = new Vector2(lower.xMin + lower.width * (left ? .1f : .9f), lower.center.y);
                var end = new Vector2(lower.xMin + lower.width * (left ? .01f : .99f), lower.center.y);
                Assert.That(engine.Begin(1, Orb(), start, false, lower, screenWidth, Tuning()), Is.True);
                var result = engine.Up(1, end, lower, screenWidth, null);
                Assert.That(result?.Kind, Is.EqualTo(left ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight));
                Assert.That(result?.NormalizedPosition.y, Is.EqualTo(.5f));
            }
        }

        [Test]
        public void CombinationAndInvalidDropStillTakePriorityAtNearEdges()
        {
            const float width = 1206; var lower = Lower(width);
            foreach (bool left in new[] { true, false })
            {
                float start = left ? .1f : .9f; var end = new Vector2(width * (left ? .01f : .99f), lower.center.y);
                var engine = Begin(width, start);
                Assert.That(engine.Up(1, end, lower, width, new[] { new OrbDropTarget(Orb("b", polarity: OrbPolarity.Yang), end) })?.Kind, Is.EqualTo(OrbActionKind.Combine));
                engine = Begin(width, start);
                Assert.That(engine.Up(1, end, lower, width, new[] { new OrbDropTarget(Orb("b"), end) }), Is.Null);
                Assert.That(engine.HasPending, Is.False);
            }
        }

        [Test]
        public void UpperBattleCrossingStillLaunchesBeforeEdgeRelease()
        {
            const float width = 1206; var lower = Lower(width); var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1, Orb(kind: OrbKind.Combined), new Vector2(width * .1f, lower.yMax - 20), false, lower, width, Tuning()), Is.True);
            var end = new Vector2(width * .01f, lower.yMax + 20);
            Assert.That(engine.Move(1, end, lower, width)?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(engine.Up(1, new Vector2(end.x, lower.center.y), lower, width, null), Is.Null);
        }

        [Test]
        public void GeometryOptionDoesNotModifyTheOlderMoveTriggeredSwipePath()
        {
            const float width = 1206; var lower = Lower(width); var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1, Orb(), new Vector2(width * .1f, lower.center.y), false, lower, width, Tuning(true, false)), Is.True);
            Assert.That(engine.Move(1, new Vector2(width * .01f, lower.center.y), lower, width), Is.Null);
            Assert.That(engine.Up(1, new Vector2(width * .01f, lower.center.y), lower, width, null), Is.Null);
        }

        // Reproduces a reachable physical endpoint that the half-edge-band floor rejects.
        // These synthetic coordinates establish the source defect, not the cause of the
        // user's earlier missed gesture, whose raw Down/Up coordinates were not logged.
        [TestCase(1206f, OrbKind.Raw, false)] [TestCase(1206f, OrbKind.Raw, true)]
        [TestCase(1206f, OrbKind.Combined, false)] [TestCase(1206f, OrbKind.Combined, true)]
        [TestCase(1488f, OrbKind.Raw, false)] [TestCase(1488f, OrbKind.Raw, true)]
        [TestCase(1488f, OrbKind.Combined, false)] [TestCase(1488f, OrbKind.Combined, true)]
        public void OutermostGrabCanReachBothPhysicalReleaseCoordinates(float width, OrbKind kind, bool left)
        {
            var lower = Lower(width);
            foreach (float releaseInset in new[] { 1f, 0f })
            {
                // iPhone: right 1180 -> 1205/1206; left 26 -> 1/0.
                // iPad uses the same normalized grab depth at its actual screen width.
                float room = 26f * width / 1206f;
                var start = new Vector2(left ? room : width - room, lower.center.y);
                var end = new Vector2(left ? releaseInset : width - releaseInset, start.y);
                var engine = new OrbGestureEngine();
                Assert.That(engine.Begin(1, Orb(kind: kind), start, false, lower, width, Tuning()), Is.True);
                Assert.That(engine.Move(1, end, lower, width), Is.Null, "Holding at the edge must stay local until Up.");
                var decision = engine.Up(1, end, lower, width, null);
                Assert.That(decision?.Kind, Is.EqualTo(left ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight),
                    $"A deliberate outward drag must fit the physical screen: {start.x} -> {end.x}, width={width}.");
                Assert.That(engine.Up(1, end, lower, width, null), Is.Null, "The same release may submit only once.");
            }
        }

        [TestCase(OrbKind.Raw, false)] [TestCase(OrbKind.Raw, true)]
        [TestCase(OrbKind.Combined, false)] [TestCase(OrbKind.Combined, true)]
        public void OutermostGrabUsesViewportOriginAndWidth(OrbKind kind, bool left)
        {
            const float screenWidth = 1206;
            var lower = new Rect(120, 102, 900, 800);
            float room = 26f * lower.width / screenWidth;
            foreach (float releaseInset in new[] { 1f, 0f })
            {
                var start = new Vector2(left ? lower.xMin + room : lower.xMax - room, lower.center.y);
                var end = new Vector2(left ? lower.xMin + releaseInset : lower.xMax - releaseInset, start.y);
                var engine = new OrbGestureEngine();
                Assert.That(engine.Begin(1, Orb(kind: kind), start, false, lower, screenWidth, Tuning()), Is.True);
                var decision = engine.Up(1, end, lower, screenWidth, null);
                Assert.That(decision?.Kind, Is.EqualTo(left ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight));
                Assert.That(decision?.NormalizedPosition.y, Is.EqualTo(.5f));
            }
        }

        [Test]
        public void OutermostGrabsStillRejectTapsJitterAndCancelledPointers()
        {
            foreach (float width in new[] { 1206f, 1488f })
                foreach (var kind in new[] { OrbKind.Raw, OrbKind.Combined })
                    foreach (bool left in new[] { true, false })
                        foreach (float room in new[] { 1f, 2f, 26f * width / 1206f })
                        {
                            var lower = Lower(width);
                            var start = new Vector2(left ? room : width - room, lower.center.y);
                            foreach (float travel in new[] { 0f, 1f })
                            {
                                var engine = new OrbGestureEngine();
                                Assert.That(engine.Begin(1, Orb(kind: kind), start, false, lower, width, Tuning()), Is.True);
                                var end = start + Vector2.right * (left ? -travel : travel);
                                Assert.That(engine.Up(1, end, lower, width, null), Is.Null, "Zero/one-pixel movement is not an intentional transfer.");
                                Assert.That(engine.HasPending, Is.False);
                            }
                            var cancelled = new OrbGestureEngine();
                            Assert.That(cancelled.Begin(1, Orb(kind: kind), start, false, lower, width, Tuning()), Is.True);
                            cancelled.Cancel(1);
                            Assert.That(cancelled.Up(1, new Vector2(left ? 0 : width, start.y), lower, width, null), Is.Null);
                            Assert.That(cancelled.HasPending, Is.False);
                        }
        }

        [Test]
        public void OutermostGrabsPreserveLegacyOptOutAndRawUpperRejection()
        {
            foreach (float width in new[] { 1206f, 1488f })
                foreach (bool left in new[] { true, false })
                {
                    var lower = Lower(width);
                    var start = new Vector2(left ? 26 : width - 26, lower.center.y);
                    var end = new Vector2(left ? 0 : width, start.y);
                    foreach (var kind in new[] { OrbKind.Raw, OrbKind.Combined })
                    {
                        var legacy = new OrbGestureEngine();
                        Assert.That(legacy.Begin(1, Orb(kind: kind), start, false, lower, width, Tuning(false)), Is.True);
                        Assert.That(legacy.Up(1, end, lower, width, null), Is.Null);
                        Assert.That(legacy.HasPending, Is.False);
                    }
                    var raw = new OrbGestureEngine();
                    Assert.That(raw.Begin(1, Orb(), start, false, lower, width, Tuning()), Is.True);
                    Assert.That(raw.Up(1, new Vector2(end.x, lower.yMax + 1), lower, width, null), Is.Null);
                    Assert.That(raw.HasPending, Is.False);
                }
        }

        [TestCase(1206f, false)] [TestCase(1206f, true)]
        [TestCase(1488f, false)] [TestCase(1488f, true)]
        public void DeliberateMinimumHasAnInclusiveBoundary(float width, bool left)
        {
            var lower = Lower(width);
            float edgeBand = lower.width * Tuning().TransferEdgeFraction;
            float required = edgeBand * .125f;
            // Inside the minimum's domain, but with enough physical room to exercise it.
            float room = edgeBand * .2f;
            foreach (var kind in new[] { OrbKind.Raw, OrbKind.Combined })
                foreach (float offset in new[] { -1f, 0f, 1f })
                {
                    var start = new Vector2(left ? room : width - room, lower.center.y);
                    float boundary = left ? start.x - required : start.x + required;
                    var end = new Vector2(boundary + (left ? -offset : offset), start.y);
                    var engine = new OrbGestureEngine();
                    Assert.That(engine.Begin(1, Orb(kind: kind), start, false, lower, width, Tuning()), Is.True);
                    var decision = engine.Up(1, end, lower, width, null);
                    Assert.That(decision.HasValue, Is.EqualTo(offset >= 0), "One pixel short rejects; exact or beyond accepts.");
                    Assert.That(engine.LastTransferRequiredTravel, Is.EqualTo(required));
                    Assert.That(engine.LastReleaseReason, Is.EqualTo(offset < 0 ? "DISTANCE_TOO_SHORT" : "TRANSFER_APPROVED_CANDIDATE"));
                }
        }

        [Test]
        public void FormerHalfBandBoundaryDoesNotIntroduceADistanceCliff()
        {
            foreach (float width in new[] { 1206f, 1488f })
                foreach (bool left in new[] { true, false })
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var lower = Lower(width);
                        float edgeBand = lower.width * Tuning().TransferEdgeFraction;
                        float room = edgeBand * .5f + side;
                        var start = new Vector2(left ? room : width - room, lower.center.y);
                        var end = start + Vector2.right * (left ? -1 : 1) * edgeBand * .3f;
                        var engine = new OrbGestureEngine();
                        Assert.That(engine.Begin(1, Orb(), start, false, lower, width, Tuning()), Is.True);
                        Assert.That(engine.Up(1, end, lower, width, null)?.Kind,
                            Is.EqualTo(left ? OrbActionKind.TransferLeft : OrbActionKind.TransferRight));
                        Assert.That(engine.LastTransferRequiredTravel, Is.LessThan(edgeBand * .3f));
                    }
        }

        [TestCase("distance", "DISTANCE_TOO_SHORT")]
        [TestCase("horizontal", "NOT_HORIZONTAL")]
        [TestCase("edge", "NOT_AT_EDGE")]
        [TestCase("approved", "TRANSFER_APPROVED_CANDIDATE")]
        public void ReleaseDiagnosticsReportTheActualTransferGate(string scenario, string reason)
        {
            const float width = 1206;
            var lower = Lower(width);
            var start = new Vector2(scenario == "edge" ? width * .5f : 1180f, lower.center.y);
            var end = new Vector2(scenario == "distance" ? 1181 : scenario == "edge" ? 900 : 1205,
                start.y + (scenario == "horizontal" ? 24 : 0));
            var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1, Orb(kind: OrbKind.Combined), start, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.LastReleaseReason, Is.EqualTo("NONE"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);
            var decision = engine.Up(1, end, lower, width, null);
            Assert.That(decision.HasValue, Is.EqualTo(scenario == "approved"));
            Assert.That(engine.LastReleaseReason, Is.EqualTo(reason));
            Assert.That(engine.LastTransferRequiredTravel, Is.EqualTo(scenario == "edge" ? width * .18f : 13f));
            Assert.That(engine.StartRawPosition, Is.EqualTo(start), "The actual Down coordinate survives Up for the adapter's log.");
        }

        [Test]
        public void ReleaseDiagnosticsPreserveDropAndLaunchPriorityWithoutStaleDistance()
        {
            const float width = 1206;
            var lower = Lower(width);
            var start = new Vector2(1180, lower.center.y);
            var end = new Vector2(1205, start.y);
            var engine = new OrbGestureEngine();
            Assert.That(engine.Begin(1, Orb(), start, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.Up(1, start, lower, width, null), Is.Null);
            Assert.That(engine.LastTransferRequiredTravel, Is.GreaterThan(0));
            Assert.That(engine.Begin(2, Orb(), start, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero, "New Begin must clear the prior release's required distance.");
            Assert.That(engine.Up(2, end, lower, width, new[] { new OrbDropTarget(Orb("b"), end) }), Is.Null);
            Assert.That(engine.LastReleaseReason, Is.EqualTo("DROP_TARGET"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);

            Assert.That(engine.Begin(3, Orb(), start, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.Up(3, end, lower, width, new[] { new OrbDropTarget(Orb("b", polarity: OrbPolarity.Yang), end) })?.Kind,
                Is.EqualTo(OrbActionKind.Combine));
            Assert.That(engine.LastReleaseReason, Is.EqualTo("COMBINE_APPROVED_CANDIDATE"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);
            Assert.That(engine.ResolvePending("a"), Is.True);

            Assert.That(engine.Begin(4, Orb(kind: OrbKind.Combined), new Vector2(1180, lower.yMax - 20), false, lower, width, Tuning()), Is.True);
            Assert.That(engine.Up(4, new Vector2(1205, lower.yMax + 20), lower, width, null)?.Kind, Is.EqualTo(OrbActionKind.Launch));
            Assert.That(engine.LastReleaseReason, Is.EqualTo("LAUNCH_APPROVED_CANDIDATE"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);
            Assert.That(engine.ResolvePending("a"), Is.True);

            Assert.That(engine.Begin(5, Orb(), start, false, lower, width, Tuning()), Is.True);
            Assert.That(engine.Up(5, new Vector2(1205, lower.yMax + 1), lower, width, null), Is.Null);
            Assert.That(engine.LastReleaseReason, Is.EqualTo("RAW_UPPER"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);
            Assert.That(engine.Begin(6, Orb(), start, false, lower, width, Tuning()), Is.True);
            engine.Cancel(6);
            Assert.That(engine.Up(6, end, lower, width, null), Is.Null);
            Assert.That(engine.LastReleaseReason, Is.EqualTo("NO_ACTIVE_POINTER"));
            Assert.That(engine.LastTransferRequiredTravel, Is.Zero);
        }
    }
}
