using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 (#477): 연습 판에서 나는 가운데 자리(P2)라 카메라가 박을 둘째 자리 각도(3명이면 120°)에서 본다. 박의 요괴는
    /// 뚜껑·손잡이·끈과 함께 첫 자리(P1) 쪽을 보고 있어 옆모습만 보였다. 그 장식 묶음(GourdYokai_Details)만 박의 세로축으로
    /// 내 자리 각도만큼 돌려 정면을 보게 한다. 장식 애니메이션은 그 안쪽(요괴·뚜껑·눈)만 움직여 돌린 값이 유지되고, 색이 묻는 흰 박
    /// 몸통은 그대로라 칠하기·맞히기는 바뀌지 않는다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        private const string YokaiDetailsPath = "GourdYokai_Details";
        private const string GourdShellPath = "White Gourd";
        private GameObject turnedVisual;

        /// <summary>How far the gourd's yokai was turned to face me (degrees about the gourd's vertical axis).</summary>
        public float YokaiTurnDegrees { get; private set; }

        /// <summary>The yokai details of the gourd now on screen have been turned toward my seat.</summary>
        public bool YokaiFacesMe => turnedVisual != null && battle != null && battle.target != null
            && battle.target.MountedVisual == turnedVisual;

        /// <summary>The turn from the first seat to <paramref name="ring"/> of <paramref name="count"/>, as the seat cameras go round.</summary>
        public static float YokaiTurnFor(int ring, int count, bool clockwise) =>
            count < 1 || ring < 0 ? 0f : (clockwise ? 360f : -360f) * ring / count;

        private void FaceYokaiToMe()
        {
            LabTarget target = battle != null ? battle.target : null;
            GameObject visual = target != null ? target.MountedVisual : null;
            if (visual == null || visual == turnedVisual) return;
            LabSnapshot snapshot = session != null && session.Network != null ? session.Network.Snapshot : null;
            if (snapshot?.players == null) return;
            LabPlayerState me = null;
            foreach (LabPlayerState player in snapshot.players)
                if (player != null && player.id == session.Network.LocalPlayerId) me = player;
            if (me == null) return;
            int ring = LabTeams.RingSeatFor(snapshot, me.seat);
            if (ring < 0) return;
            turnedVisual = visual;
            Transform details = visual.transform.Find(YokaiDetailsPath);
            if (details == null || details.parent != visual.transform) return;
            YokaiTurnDegrees = YokaiTurnFor(ring, snapshot.players.Length, !LabTeams.TeamSeats(snapshot));
            // About the gourd's own vertical axis (its local up through the shell's middle), so a swing at that moment
            // does not tilt it.
            Transform shell = visual.transform.Find(GourdShellPath);
            Renderer body = shell != null ? shell.GetComponent<Renderer>() : null;
            Vector3 axis = body != null ? visual.transform.InverseTransformPoint(body.bounds.center) : Vector3.zero;
            axis.y = 0f;
            Quaternion turn = Quaternion.AngleAxis(YokaiTurnDegrees, Vector3.up);
            details.localPosition = axis + turn * (details.localPosition - axis);
            details.localRotation = turn * details.localRotation;
        }
    }
}
