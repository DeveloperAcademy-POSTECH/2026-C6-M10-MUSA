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
