namespace C6Lab
{
    /// <summary>2:2 청홍 땅따먹기 (#281)의 팀. 숫자는 스냅샷에 저장되므로 바꾸지 않는다. None은 팀전이 아니거나 무승부다.</summary>
    public enum LabTeam { None, Blue, Red }

    /// <summary>
    /// 2:2 청홍 땅따먹기 (#281)의 자리 규칙. P1·P2가 청팀, P3·P4가 홍팀이다. 판은 팀 안 두 명만 나눈다:
    /// 팀의 왼쪽 사람(P1·P3)은 오른쪽 가장자리로만, 오른쪽 사람(P2·P4)은 왼쪽 가장자리로만 팀원에게 넘긴다.
    /// 박 둘레에서는 청·홍·청·홍(P1, P3, P2, P4)으로 엇갈려 선다. 청은 물(파랑), 홍은 불(빨강) 구슬·물감을 쓴다.
    /// seat는 언제나 P번호 - 1이다.
    /// </summary>
    public static class LabTeams
    {
        public const int Players = 4;

        public static LabTeam TeamOfSeat(int seat) =>
            seat == 0 || seat == 1 ? LabTeam.Blue : seat == 2 || seat == 3 ? LabTeam.Red : LabTeam.None;

        public static LabOrbElement ElementOf(LabTeam team) =>
            team == LabTeam.Blue ? LabOrbElement.Water : team == LabTeam.Red ? LabOrbElement.Fire : LabOrbElement.None;

        public static LabTeam TeamOfElement(LabOrbElement element) =>
            element == LabOrbElement.Water ? LabTeam.Blue : element == LabOrbElement.Fire ? LabTeam.Red : LabTeam.None;

        public static LabTeam Opponent(LabTeam team) =>
            team == LabTeam.Blue ? LabTeam.Red : team == LabTeam.Red ? LabTeam.Blue : LabTeam.None;

        /// <summary>이 자리에서 그쪽 가장자리로 팀원에게 넘길 수 있는지. 바깥쪽 가장자리는 막혀 있다.</summary>
        public static bool EdgeOpen(int seat, bool right) => seat >= 0 && seat < Players && (seat % 2 == 0) == right;

        /// <summary>같은 팀의 다른 한 명 자리 (P1↔P2, P3↔P4).</summary>
        public static int TeammateSeat(int seat) => seat ^ 1;

        /// <summary>박 둘레에서 서는 순서. 청·홍·청·홍이 되도록 P2와 P3만 자리를 바꾼다.</summary>
        public static int RingSeat(int seat) => seat == 1 ? 2 : seat == 2 ? 1 : seat;
    }
}
