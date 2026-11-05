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
            Assert.That(overlay.Paper.gameObject.activeSelf, Is.True, "It does not vanish at once.");
            Assert.That(overlay.PaperAlpha, Is.EqualTo(1f));
            overlay.AdvanceFinish(overlay.FinishSeconds * .8f);
            Assert.That(overlay.PaperAlpha, Is.GreaterThan(0f).And.LessThan(1f), "Fading away slowly.");
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
        public void TheGuideIsEvenlySampledAndOrderFree()
        {
            var forward = new LabJigwiTalismanStroke(LabJigwiTalismanStroke.Water);
            var backward = new LabJigwiTalismanStroke(LabJigwiTalismanStroke.Water);
            Vector2[] points = LabJigwiTalismanStroke.Water;
            Assert.That(forward.Samples.Length, Is.InRange(60, 200));
            foreach (Vector2 point in points) Assert.That(point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f, Is.True);
            for (int i = 0; i < points.Length; i++) forward.Paint(points[i], points[i], .07f);
            for (int i = points.Length - 1; i >= 0; i--) backward.Paint(points[i], points[i], .07f);
            Assert.That(forward.Coverage, Is.EqualTo(1f)); Assert.That(backward.Coverage, Is.EqualTo(1f));
            forward.Clear(); Assert.That(forward.CoveredCount, Is.Zero);
        }

        private static void Trace(LabJigwiTalismanOverlay target)
        {
            foreach (Vector2 point in LabJigwiTalismanStroke.Water) target.PaintNormalized(point, point);
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
