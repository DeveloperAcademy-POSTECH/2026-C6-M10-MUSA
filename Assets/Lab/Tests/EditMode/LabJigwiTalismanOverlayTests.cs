using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#479 지귀 부적 화면: 언제 부적이 판을 덮고 판 조작을 막는지 (스냅샷만 본다).</summary>
    public sealed class LabJigwiTalismanOverlayTests
    {
        private const ulong Local = 2, Other = 1;
        private GameObject root;
        private LabJigwiTalismanOverlay overlay;

        [SetUp]
        public void Create()
        {
            root = new GameObject("talisman-test", typeof(RectTransform), typeof(Canvas));
            var child = new GameObject("talisman", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            overlay = child.AddComponent<LabJigwiTalismanOverlay>();
            overlay.Bind(null, null);
        }

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(root);

        [Test]
        public void TheTalismanCoversOnlyAnUnwrittenSeatDuringTheActiveA()
        {
            LabSnapshot snapshot = Snapshot(-1d);
            overlay.Present(snapshot, Local, 10.5d);
            Assert.That(overlay.Visible, Is.False, "The one-second warning does not cover the board yet.");
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.Visible, Is.True);
            Assert.That(overlay.RoundId, Is.EqualTo("ab-a"));
            Assert.That(overlay.WantsBoardBlocked, Is.True);
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True);
            foreach (var graphic in overlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "The talisman never swallows other UI taps.");
            overlay.Present(snapshot, 99, 11d);
            Assert.That(overlay.Visible, Is.False, "A player outside the round has no talisman.");
            overlay.Present(Snapshot(11.5d), Local, 12d);
            Assert.That(overlay.Visible, Is.False, "A written talisman uncovers the board.");
            Assert.That(overlay.WantsBoardBlocked, Is.False);
            overlay.Present(snapshot, Local, 26d);
            Assert.That(overlay.Visible, Is.False, "The A window is half open.");
        }

        [Test]
        public void BAndPausedOrFinishedBattlesNeverShowTheTalisman()
        {
            LabSnapshot snapshot = Snapshot(-1d);
            snapshot.jigwiAB.rounds[0].kind = LabJigwiABKind.GroupFire;
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.Visible, Is.False);
            snapshot = Snapshot(-1d); snapshot.phase = LabPhase.Victory;
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.Visible, Is.False);
            overlay.Present(null, Local, 11d);
            Assert.That(overlay.Visible, Is.False);
        }

        [Test]
        public void TracingTheWholeGuideFinishesOnceAndUncoversTheBoard()
        {
            LabSnapshot snapshot = Snapshot(-1d);
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.Coverage, Is.Zero);
            // 위쪽(ㅁ)만 쓰면 아직 덜 썼다.
            foreach (Vector2 point in LabJigwiTalismanStroke.Water) if (point.y > .7f) overlay.PaintNormalized(point, point);
            Assert.That(overlay.Coverage, Is.GreaterThan(0f).And.LessThan(.6f));
            Assert.That(overlay.CompletedCount, Is.Zero); Assert.That(overlay.Visible, Is.True);
            Trace(overlay);
            Assert.That(overlay.CompletedCount, Is.EqualTo(1));
            Assert.That(overlay.Visible, Is.False); Assert.That(overlay.WantsBoardBlocked, Is.False);
            overlay.Present(snapshot, Local, 12d);
            Assert.That(overlay.Visible, Is.False, "While the Host confirms, the finished talisman stays down.");
            Assert.That(overlay.CompletedCount, Is.EqualTo(1));
            LabSnapshot next = Snapshot(-1d); next.jigwiAB.rounds[0].id = "ab-a2";
            overlay.Present(next, Local, 12d);
            Assert.That(overlay.Visible, Is.True, "The next A brings a fresh talisman.");
            Assert.That(overlay.Coverage, Is.Zero);
        }

        [Test]
        public void AFinishedTalismanGlowsThenFadesWithoutBlockingTheBoard()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            Trace(overlay);
            // #509: the written "물" glows first, then the talisman fades out; the board is already free.
            Assert.That(overlay.Visible, Is.False); Assert.That(overlay.WantsBoardBlocked, Is.False);
            Assert.That(overlay.Finishing, Is.True);
            Assert.That(overlay.LastCue, Is.Not.EqualTo("Steam"), "#540: the fire just stops — no steam hiss.");
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True, "It does not vanish at once.");
            Assert.That(overlay.PaperAlpha, Is.EqualTo(1f));
            overlay.AdvanceFinish(overlay.FinishSeconds - .15f);
            Assert.That(overlay.LastCue, Is.EqualTo("Burst"), "The burst sounds once after the sweep.");
            Assert.That(overlay.PaperAlpha, Is.GreaterThan(0f).And.LessThan(1f), "#540: the folded talisman orb is dropping to the board.");
            overlay.Present(Snapshot(-1d), Local, 11.5d);
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True, "The Host has not confirmed yet; it keeps fading.");
            overlay.AdvanceFinish(overlay.FinishSeconds);
            Assert.That(overlay.Finishing, Is.False);
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.False);
            Assert.That(overlay.PaperAlpha, Is.EqualTo(1f), "Reset for the next A.");
            foreach (var graphic in overlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void ScribblesAwayFromTheGuideNeverCount()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            overlay.PaintNormalized(new Vector2(0f, 0f), new Vector2(.05f, .05f));
            overlay.PaintNormalized(new Vector2(.97f, .97f), new Vector2(.99f, .2f));
            Assert.That(overlay.Coverage, Is.Zero);
            overlay.PaintNormalized(new Vector2(float.NaN, 0f), new Vector2(.5f, .5f));
            Assert.That(overlay.Coverage, Is.Zero);
            Assert.That(overlay.CompletedCount, Is.Zero);
        }

        [Test]
        public void ALetterWithItsTailStillBlankDoesNotFinish()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            Vector2[] points = LabJigwiTalismanStroke.Water;
            int[][] strokes = LabJigwiTalismanStroke.WaterStrokes;
            int[] tail = strokes[strokes.Length - 1];
            // #533: everything but the end of the ㄹ tail (the brush reach still covers two of the five skipped dots,
            // so three stay blank) — enough dots counted, but the tail visibly blank.
            for (int s = 0; s < strokes.Length; s++)
                for (int i = 0; i < strokes[s].Length; i++)
                    if (s < strokes.Length - 1 || i < tail.Length - 5) overlay.PaintNormalized(points[strokes[s][i]], points[strokes[s][i]]);
            Assert.That(overlay.Coverage, Is.GreaterThan(.9f));
            Assert.That(overlay.CompletedCount, Is.Zero, "A blank tail is not a finished letter.");
            Assert.That(overlay.Stroke.LongestGap(strokes.Length - 1), Is.GreaterThan(1));
            // Touching the tail end fills it and the letter finishes.
            overlay.PaintNormalized(points[tail[tail.Length - 3]], points[tail[tail.Length - 3]]);
            Assert.That(overlay.CompletedCount, Is.EqualTo(1));
            Assert.That(overlay.Stroke.LongestGap(strokes.Length - 1), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void TheFinishPlaysThreeEvenNotesThenTheAccent()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            Trace(overlay);
            Assert.That(overlay.NotesPlayedCount, Is.Zero, "#540: the fire goes out and a breath passes before the melody.");
            Assert.That(overlay.StampVisible, Is.False, "The seal waits for the accent.");
            overlay.AdvanceFinish(overlay.AccentSeconds - .3f);
            Assert.That(overlay.NotesPlayedCount, Is.EqualTo(3), "솔, 도, 시 with rests between.");
            Assert.That(overlay.StampVisible, Is.False, "Still resting before the accent.");
            overlay.AdvanceFinish(.4f);
            Assert.That(overlay.NotesPlayedCount, Is.EqualTo(4), "Three notes, rests, then the accented 라♭.");
            Assert.That(overlay.StampVisible, Is.True, "The seal lands with the accent.");
            Assert.That(overlay.LastCue, Is.EqualTo("Burst"));
        }

        [Test]
        public void AnUnfinishedTalismanBurnsAwayWhenTheARunsOut()
        {
            LabSnapshot snapshot = Snapshot(-1d);
            overlay.Present(snapshot, Local, 11d);
            Vector2 point = LabJigwiTalismanStroke.Water[0];
            overlay.PaintNormalized(point, point);
            // #533: the A ends with this seat still writing — Jigwi's fire burns the talisman.
            overlay.Present(snapshot, Local, 26d);
            Assert.That(overlay.Visible, Is.False); Assert.That(overlay.WantsBoardBlocked, Is.False);
            Assert.That(overlay.Burning, Is.True);
            Assert.That(overlay.BurnCount, Is.EqualTo(1));
            Assert.That(overlay.LastCue, Is.EqualTo("Burn"));
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True, "It burns where it was, not vanishing at once.");
            overlay.Present(snapshot, Local, 26.5d);
            Assert.That(overlay.BurnCount, Is.EqualTo(1), "It burns once.");
            overlay.AdvanceBurn(overlay.BurnTotalSeconds * .9f);
            Assert.That(overlay.PaperAlpha, Is.LessThan(1f), "The ash scatters away.");
            overlay.AdvanceBurn(overlay.BurnTotalSeconds);
            Assert.That(overlay.Burning, Is.False);
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.False);
            Assert.That(overlay.Coverage, Is.Zero, "The next A starts from a clean talisman.");
            foreach (var graphic in overlay.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void TheTalismanBurnsInFromItsEdgesSlowlyFromTheStart()
        {
            LabSnapshot snapshot = Snapshot(-1d); // the A runs 11 s → 26 s
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.WarnLevel, Is.Zero, "Nothing burned at the very start.");
            overlay.Present(snapshot, Local, 18.5d);
            Assert.That(overlay.WarnLevel, Is.GreaterThan(0f).And.LessThan(.5f), "Halfway through, it has burned slowly — less than half.");
            overlay.Present(snapshot, Local, 26d - .001d);
            Assert.That(overlay.WarnLevel, Is.EqualTo(1f).Within(.01f), "At the end it has burned to the middle.");
            overlay.Present(snapshot, 99, 24.5d);
            Assert.That(overlay.WarnLevel, Is.Zero, "No talisman, no warning.");
        }

        [Test]
        public void TheFireballKindlesGrowsAndThenStopsOrFalls()
        {
            // #540: warning 10 s → active 11 s → ends 26 s. The orbs are untouched; only the fireball shows the time.
            LabSnapshot snapshot = Snapshot(-1d);
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 9.9d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.None));
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 10.5d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.None),
                "#540: it catches when Jigwi's voice says 타올라라, not at the very start of the warning.");
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 10.85d, out float p, out _), Is.EqualTo(LabJigwiFireball.Phase.Kindle));
            Assert.That(p, Is.EqualTo(.5f).Within(.01f));
            LabJigwiFireball.Evaluate(snapshot, Local, 12d, out _, out float early);
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 24d, out _, out float late), Is.EqualTo(LabJigwiFireball.Phase.Grow));
            Assert.That(late, Is.GreaterThan(early), "It grows as time runs out.");
            Assert.That(LabJigwiFireball.Evaluate(snapshot, 99, 24d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.None), "Only my own.");
            LabSnapshot written = Snapshot(20d);
            Assert.That(LabJigwiFireball.Evaluate(written, Local, 20.2d, out _, out float stopped), Is.EqualTo(LabJigwiFireball.Phase.Douse),
                "Writing it stops the fire where it is.");
            Assert.That(stopped, Is.EqualTo(LabJigwiFireball.Grown(9f / 15f)).Within(.001f), "It stops at the size it had.");
            Assert.That(LabJigwiFireball.Evaluate(written, Local, 21d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.None));
            LabJigwiABRound round = snapshot.jigwiAB.rounds[0];
            round.resolution = LabJigwiABResolution.Expired; round.resolvedAt = 26d;
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 26.2d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.Fall),
                "Not written in time: it falls onto the board.");
            Assert.That(LabJigwiFireball.Evaluate(snapshot, Local, 27d, out _, out _), Is.EqualTo(LabJigwiFireball.Phase.None));
        }

        [Test]
        public void TheWarningDrivesTheOmenBeforeTheTalismanArrives()
        {
            LabSnapshot snapshot = Snapshot(-1d); // warning 10 s → active 11 s
            overlay.Present(snapshot, Local, 9.9d);
            Assert.That(overlay.OmenLevel, Is.Zero, "Nothing is coming yet.");
            overlay.Present(snapshot, Local, 10.5d);
            Assert.That(overlay.OmenLevel, Is.EqualTo(.5f).Within(.02f), "#540: halfway through the 3D omen.");
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.False, "#540: the paper only lands on the board when the A starts.");
            Assert.That(overlay.WantsBoardBlocked, Is.False, "The board is not blocked until the A starts.");
            overlay.Present(snapshot, Local, 11d);
            Assert.That(overlay.OmenLevel, Is.Zero, "The omen is over once the talisman lies on the board.");
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True);
            overlay.Present(snapshot, 99, 10.5d);
            Assert.That(overlay.OmenLevel, Is.Zero, "A seat outside the A sees no omen.");
        }

        [Test]
        public void JigwiTauntsOnceWhenTheAWarningAppears()
        {
            LabSnapshot snapshot = Snapshot(-1d); // warning at 10 s
            overlay.Present(snapshot, Local, 9.5d);
            Assert.That(overlay.VoiceCount, Is.Zero);
            overlay.Present(snapshot, Local, 10.1d);
            Assert.That(overlay.VoiceCount, Is.EqualTo(1)); Assert.That(overlay.LastVoice, Is.EqualTo("A"));
            overlay.Present(snapshot, Local, 10.3d);
            Assert.That(overlay.VoiceCount, Is.EqualTo(1), "Once per warning.");
            var late = new GameObject("late", typeof(RectTransform)).AddComponent<LabJigwiTalismanOverlay>();
            late.transform.SetParent(root.transform, false); late.Bind(null, null);
            late.Present(snapshot, Local, 12d);
            Assert.That(late.VoiceCount, Is.Zero, "A screen that joins late does not replay a past warning.");
        }

        [Test]
        public void AFinishedOrEndedGameNeverBurnsTheTalisman()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            LabSnapshot over = Snapshot(-1d); over.phase = LabPhase.Victory;
            overlay.Present(over, Local, 26d);
            Assert.That(overlay.Burning, Is.False, "The battle ending is not a failed talisman.");
            overlay.Present(Snapshot(-1d), Local, 11d);
            overlay.Present(Snapshot(11.5d), Local, 26d);
            Assert.That(overlay.Burning, Is.False, "A talisman the Host already counted never burns.");
        }

        [Test]
        public void CoinScratchScribblingSmudgesAndStartsOver()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            // #533: rubbing back and forth over the whole paper, like scratching a lottery ticket.
            for (int i = 0; i <= 40; i++)
            {
                float y = i / 40f;
                overlay.PaintNormalized(new Vector2(.02f, y), new Vector2(.98f, y));
            }
            Assert.That(overlay.SmudgeCount, Is.GreaterThan(0));
            Assert.That(overlay.LastCue, Is.EqualTo("Smudge"));
            Assert.That(overlay.CompletedCount, Is.Zero, "Scribbling never finishes the talisman.");
            Assert.That(overlay.Visible, Is.True);
        }

        [Test]
        public void TracingTheLetterNeverSmudges()
        {
            overlay.Present(Snapshot(-1d), Local, 11d);
            Vector2[] points = LabJigwiTalismanStroke.Water;
            // Writing each stroke as one continuous drag along the letter stays on the guide.
            foreach (int[] order in LabJigwiTalismanStroke.WaterStrokes)
                for (int i = 1; i < order.Length; i++) overlay.PaintNormalized(points[order[i - 1]], points[order[i]]);
            Assert.That(overlay.SmudgeCount, Is.Zero);
            Assert.That(overlay.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void TheGuideIsEvenlySampledAndEveryPointBelongsToOneStroke()
        {
            Vector2[] points = LabJigwiTalismanStroke.Water;
            Assert.That(points.Length, Is.InRange(60, 200));
            foreach (Vector2 point in points) Assert.That(point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f, Is.True);
            var seen = new bool[points.Length];
            foreach (int[] order in LabJigwiTalismanStroke.WaterStrokes)
                foreach (int index in order)
                {
                    Assert.That(index, Is.InRange(0, points.Length - 1));
                    Assert.That(seen[index], Is.False, "Point " + index + " is listed twice.");
                    seen[index] = true;
                }
            Assert.That(System.Array.TrueForAll(seen, x => x), Is.True, "Every point belongs to a stroke.");
        }

        [Test]
        public void TheGuideFollowsStrokeOrderButAnyOrderStillPaints()
        {
            Vector2[] points = LabJigwiTalismanStroke.Water;
            var ordered = new LabJigwiTalismanStroke(points, LabJigwiTalismanStroke.WaterStrokes);
            foreach (int[] order in LabJigwiTalismanStroke.WaterStrokes)
                foreach (int index in order) ordered.Paint(points[index], points[index], .07f);
            Assert.That(ordered.Coverage, Is.EqualTo(1f));
            Assert.That(ordered.NextPoint, Is.EqualTo(-1));
            ordered.Clear();
            Assert.That(ordered.CoveredCount, Is.Zero); Assert.That(ordered.CurrentStroke, Is.Zero);

            // #533: writing the last stroke (ㄹ bottom) first still paints; the guide keeps pointing at the first stroke.
            int[] last = LabJigwiTalismanStroke.WaterStrokes[LabJigwiTalismanStroke.WaterStrokes.Length - 1];
            foreach (int index in last) ordered.Paint(points[index], points[index], .07f);
            // The brush also touches nearby points on other strokes in free-order mode.
            int expectedCount = 0;
            for (int i = 0; i < points.Length; i++)
            {
                bool touched = System.Array.Exists(last, index => Vector2.Distance(points[index], points[i]) <= .07f);
                Assert.That(ordered.IsCovered(i), Is.EqualTo(touched), "The brush footprint at point " + i);
                if (touched) expectedCount++;
            }
            Assert.That(ordered.CoveredCount, Is.EqualTo(expectedCount));
            Assert.That(ordered.CurrentStroke, Is.Zero);
            Assert.That(ordered.NextPoint, Is.EqualTo(LabJigwiTalismanStroke.WaterStrokes[0][0]), "The guide points at the first stroke's start.");
            // Once the first stroke is written the guide jumps past strokes already written to the next unwritten one.
            foreach (int index in LabJigwiTalismanStroke.WaterStrokes[0]) ordered.Paint(points[index], points[index], .07f);
            Assert.That(ordered.NextPoint, Is.EqualTo(LabJigwiTalismanStroke.WaterStrokes[1][0]));

            // The strict mode (kept for later tuning) still blocks a later stroke.
            var strict = new LabJigwiTalismanStroke(points, LabJigwiTalismanStroke.WaterStrokes) { EnforceOrder = true };
            foreach (int index in last) strict.Paint(points[index], points[index], .07f);
            Assert.That(strict.CoveredCount, Is.Zero);

            // Without a stroke order any point can be painted (the old behaviour).
            var free = new LabJigwiTalismanStroke(points);
            for (int i = points.Length - 1; i >= 0; i--) free.Paint(points[i], points[i], .07f);
            Assert.That(free.Coverage, Is.EqualTo(1f));
        }

        private static void Trace(LabJigwiTalismanOverlay target)
        {
            Vector2[] points = LabJigwiTalismanStroke.Water;
            foreach (int[] order in LabJigwiTalismanStroke.WaterStrokes)
                foreach (int index in order) target.PaintNormalized(points[index], points[index]);
        }

        private static LabSnapshot Snapshot(double localTalismanAt) => new LabSnapshot
        {
            sessionId = "talisman", round = 1, phase = LabPhase.Playing,
            players = new[] { new LabPlayerState { id = Other, seat = 0 }, new LabPlayerState { id = Local, seat = 1 } },
            jigwiAB = new LabJigwiABSnapshot
            {
                enabled = true, startedAt = 0d, cutoffAt = 100d,
                rounds = new[]
                {
                    new LabJigwiABRound
                    {
                        id = "ab-a", slot = 0, kind = LabJigwiABKind.PersonalBurn,
                        warningAt = 10d, activeAt = 11d, endsAt = 26d,
                        players = new[]
                        {
                            new LabJigwiABPlayerFire { playerId = Other },
                            new LabJigwiABPlayerFire { playerId = Local, talismanAt = localTalismanAt }
                        }
                    }
                }
            }
        };
    }
}
