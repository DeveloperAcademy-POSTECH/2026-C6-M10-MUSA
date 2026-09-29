using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Battle.Tests
{
    public sealed class MonsterInterferenceOverlayTests
    {
        [Test]
        public void SinglePlayerSeesOnlyTheirBlockedSide()
        {
            var snapshot = PlayingSnapshot(
                MonsterInterferenceKind.SinglePlayerDirectionBlock,
                MonsterTransferDirection.Left);
            snapshot.interferenceHasTarget = true;
            snapshot.interferenceTarget = 5;

            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 5),
                Is.EqualTo(MonsterTransferDirection.Left));
            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 4),
                Is.EqualTo(MonsterTransferDirection.None));
        }

        [TestCase(MonsterTransferDirection.Left, MonsterTransferDirection.Right)]
        [TestCase(MonsterTransferDirection.Right, MonsterTransferDirection.Left)]
        public void AllPlayerRestrictionMarksTheForbiddenSide(
            MonsterTransferDirection allowed,
            MonsterTransferDirection forbidden)
        {
            var snapshot = PlayingSnapshot(
                MonsterInterferenceKind.AllPlayersDirectionRestriction,
                allowed);

            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 0),
                Is.EqualTo(forbidden));
            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 4),
                Is.EqualTo(forbidden));
        }

        [Test]
        public void InactiveOrNonPlayingStateHasNoVisualRestriction()
        {
            var snapshot = PlayingSnapshot(
                MonsterInterferenceKind.SinglePlayerDirectionBlock,
                MonsterTransferDirection.Right);
            snapshot.interferenceHasTarget = true;
            snapshot.interferenceTarget = 2;

            snapshot.interferenceActive = false;
            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 2),
                Is.EqualTo(MonsterTransferDirection.None));

            snapshot.interferenceActive = true;
            snapshot.phase = BattlePhase.Ready.ToString();
            Assert.That(
                T09BattleController.RestrictedInterferenceDirection(snapshot, 2),
                Is.EqualTo(MonsterTransferDirection.None));
        }

        [Test]
        public void RedEdgeBoundsStayInsideTheOrbBoard()
        {
            var board = new Rect(10f, 20f, 300f, 240f);

            Assert.That(
                MonsterInterferenceOverlay.LeftEdgeRect(board, 18f),
                Is.EqualTo(new Rect(10f, 20f, 18f, 240f)));
            Assert.That(
                MonsterInterferenceOverlay.RightEdgeRect(board, 18f),
                Is.EqualTo(new Rect(292f, 20f, 18f, 240f)));
        }

        [Test]
        public void EdgeAndMessageUseTheSameFinalFade()
        {
            Assert.That(MonsterInterferenceOverlay.FadeAlpha(99d, 100d, .8f), Is.EqualTo(1f));
            Assert.That(MonsterInterferenceOverlay.FadeAlpha(99.6d, 100d, .8f), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(MonsterInterferenceOverlay.FadeAlpha(100d, 100d, .8f), Is.Zero);
        }

        [Test]
        public void MessageMatchesTheApprovedKoreanCopy()
        {
            Assert.That(
                MonsterInterferenceOverlay.DefaultMessage,
                Is.EqualTo("요괴의 방해로 구슬을 자유롭게 전달하기 어려워졌다..."));
        }

        private static BattleSnapshot PlayingSnapshot(
            MonsterInterferenceKind kind,
            MonsterTransferDirection direction)
        {
            return new BattleSnapshot
            {
                phase = BattlePhase.Playing.ToString(),
                interferenceActive = true,
                interferenceKind = (int)kind,
                interferenceDirection = (int)direction
            };
        }
    }
}
