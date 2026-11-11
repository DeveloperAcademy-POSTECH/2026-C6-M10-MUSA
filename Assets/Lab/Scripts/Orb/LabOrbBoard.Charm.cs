using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 매혹 벽 (#664): 옆자리 사람이 매혹에 걸려 있는 동안 그쪽 가장자리가 분홍빛 벽이 된다. 그쪽으로 넘기거나 밀린 구슬·북은
    /// 위아래 벽처럼 튕겨 돌아온다(Host도 RECEIVER_CHARMED로 거절한다). 양옆이 다 걸렸으면 양쪽이 닫힌다. 걸린 사람 자신의
    /// 판은 벽 대신 통째로 멈춘다(<see cref="SetCharm"/>).
    /// </summary>
    public sealed partial class LabOrbBoard
    {
        /// <summary>The charm's pink, the same family as the charmed player's heart haze.</summary>
        public static readonly Color CharmWallTint = new Color(.93f, .38f, .6f);

        // -1 left, +1 right, LabTeams.BothSides, 0 none.
        private int charmWallSide;
        private LabOrbTeamWall charmWallRight, charmWallLeft;

        /// <summary>The edges shut toward a charmed neighbour now: -1 left, +1 right, LabTeams.BothSides, 0 none.</summary>
        public int CharmWallSide => charmWallSide;
        /// <summary>How many orbs have bounced off a charm wall on this board.</summary>
        public int CharmWallBounces { get; private set; }

        /// <summary>Shut (or open) the edges toward charmed neighbours, as LabSnapshot.CharmWallSide gives them.</summary>
        public void SetCharmWalls(int side)
        {
            side = !configured ? 0 : side == LabTeams.BothSides ? side : Math.Sign(side);
            if (side == charmWallSide && (side == 0 || CharmWallsShown)) return;
            charmWallSide = side;
            LayCharmWalls();
        }

        private bool CharmWallsShown => (!CharmWallOn(true) || charmWallRight != null && charmWallRight.Visible)
            && (!CharmWallOn(false) || charmWallLeft != null && charmWallLeft.Visible);

        /// <summary>Whether that edge of this board is shut toward a charmed neighbour now.</summary>
        private bool CharmWallOn(bool right) => LabTeams.SideClosed(charmWallSide, right);

        private void LayCharmWalls()
        {
            LayCharmWall(ref charmWallRight, true);
            LayCharmWall(ref charmWallLeft, false);
        }

        private void LayCharmWall(ref LabOrbTeamWall wall, bool right)
        {
            if (!CharmWallOn(right) || !configured)
            {
                if (wall != null) wall.Hide();
                return;
            }
            if (wall == null)
            {
                wall = LabOrbTeamWall.Create(transform, "Charm Wall Edge");
                wall.Tint = CharmWallTint;
            }
            float edgeX = right ? bounds.xMax : bounds.xMin;
            Vector2 range = areaShape.VerticalRange(edgeX, 0f);
            wall.Show(right, edgeX, range.x, range.y, bounds.width);
        }
    }
}
