using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#509 B 화면: 물방울 3칸/"지금!"/어긋남 표시를 스냅샷 값만으로 정하는지(불기둥 없음).</summary>
    public sealed class LabJigwiSyncPillarViewTests
    {
        private const ulong Me = 2, Partner = 1;
        private GameObject root;
        private LabJigwiSyncPillarView view;
        private static readonly Rect Clip = new Rect(-300f, -200f, 600f, 400f);

        [SetUp]
        public void Create()
        {
            root = new GameObject("sync-pillar-test", typeof(RectTransform), typeof(Canvas));
            view = root.AddComponent<LabJigwiSyncPillarView>();
            view.Build((RectTransform)root.transform, null);
        }

        [TearDown]
        public void Destroy() => Object.DestroyImmediate(root);

        [Test]
        public void APickedScreenShowsTheDropsAndTheyFillWithEachSync()
        {
            var round = Round();
            view.Present(round, round.players[1], true, Clip, 26d, false, false);
            Assert.That(view.DropsVisible, Is.True);
            Assert.That(view.FilledDrops, Is.Zero);
            round.syncCount = 2;
            view.Present(round, round.players[1], true, Clip, 27d, false, false);
            Assert.That(view.FilledDrops, Is.EqualTo(2));
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, "B's drops never swallow taps.");
        }

        [Test]
        public void AnOpenWindowCuesOnlyThePickedSeatThatHasNotHitAndVibratesOnce()
        {
            var round = Round();
            view.Present(round, round.players[1], true, Clip, 26d, false, true);
            round.syncWindowAt = 26.2d; round.players[0].targets[0].extinguishedAt = 26.2d;
            view.Present(round, round.players[1], true, Clip, 26.3d, false, true);
            Assert.That(view.NowCueVisible, Is.True);
            view.Present(round, round.players[1], true, Clip, 26.5d, false, true);
            Assert.That(view.VibrationCount, Is.EqualTo(1), "One buzz per window, not per frame.");
            round.players[1].targets[0].extinguishedAt = 26.6d;
            view.Present(round, round.players[1], true, Clip, 26.6d, false, true);
            Assert.That(view.NowCueVisible, Is.False, "I already hit inside the window.");
        }

        [Test]
        public void AHelperSeesTheDropsWithoutTheCueAndAResetShakesThem()
        {
            var round = Round();
            view.Present(round, null, false, Clip, 26d, false, false);
            Assert.That(view.DropsVisible, Is.True);
            round.syncWindowAt = 26.5d; view.Present(round, null, false, Clip, 26.6d, false, false);
            Assert.That(view.NowCueVisible, Is.False, "A helper never gets 'now'."); round.syncWindowAt = -1d;
            round.syncResetAt = 27d;
            view.Present(round, null, false, Clip, 27.1d, false, false);
            Assert.That(view.ResetCueVisible, Is.True);
            view.Present(round, null, false, Clip, 28d, false, false);
            Assert.That(view.ResetCueVisible, Is.False);
            round.resolution = LabJigwiABResolution.Completed;
            view.Present(round, null, false, Clip, 28d, false, false);
            Assert.That(view.DropsVisible, Is.False);
        }

        private static LabJigwiABRound Round() => new LabJigwiABRound
        {
            id = "b-sync", slot = 1, kind = LabJigwiABKind.GroupFire,
            warningAt = 23d, activeAt = 25d, endsAt = 40d,
            players = new[]
            {
                new LabJigwiABPlayerFire { playerId = Partner, targets = new[] { new LabJigwiABFireTarget { id = "p", x = .5f, y = .62f } } },
                new LabJigwiABPlayerFire { playerId = Me, targets = new[] { new LabJigwiABFireTarget { id = "m", x = .5f, y = .62f } } }
            }
        };
    }
}
