namespace C6Lab
{
    /// <summary>2:2 청홍 땅따먹기 (#281)의 팀. 숫자는 스냅샷에 저장되므로 바꾸지 않는다. None은 팀전이 아니거나 무승부다.</summary>
    public enum LabTeam { None, Blue, Red }

    /// <summary>
    /// 청홍 땅따먹기의 자리 규칙. 2명(1:1, #394)·4명(2:2, #281)·6명(3:3, #394)이 두 팀으로 똑같이 나뉜다: 앞 절반의
    /// P번호가 청팀, 뒤 절반이 홍팀이다. 청은 물(파랑), 홍은 불(빨강) 구슬·물감을 쓴다. seat는 언제나 P번호 - 1이다.
    /// - 1:1: 팀원이 없어 구슬을 넘기지 않는다(양쪽 가장자리가 막힘). 박을 사이에 두고 마주 앉는다.
    /// - 2:2: 팀의 왼쪽 사람(P1·P3)은 오른쪽 가장자리로만, 오른쪽 사람(P2·P4)은 왼쪽 가장자리로만 팀원에게 넘긴다.
    ///   박 둘레에서는 청·홍·청·홍(P1, P3, P2, P4)으로 엇갈려 선다.
    /// - 3:3: 팀끼리 붙어 앞 3 · 뒤 3으로 마주 앉는다. 청은 P2·P1·P3, 홍은 P5·P4·P6 순서로 서서 P1과 P4가 각 팀의 가운데다.
    ///   양쪽 가장자리 모두 그쪽 팀원에게 넘기고, 팀 끝 사람이 바깥으로 넘기면 반대쪽 끝 팀원에게 돌아간다(막힌 벽 없음).
    /// </summary>
    public static class LabTeams
    {
        /// <summary>2:2의 인원. 인원을 받지 않는 예전 도우미(TeamOfSeat(seat) 등)는 2:2 기준이다.</summary>
        public const int Players = 4;
        /// <summary>ClosedSide가 양쪽 가장자리가 모두 막혔다고 알리는 값 (1:1).</summary>
        public const int BothSides = 2;

        // 박 둘레 자리(반시계, 0부터)마다 앉는 P번호 - 1. 둘레 번호가 하나 늘면 그 사람의 화면 오른쪽이다.
        private static readonly int[] RingTwo = { 0, 1 }, RingFour = { 0, 2, 1, 3 }, RingSix = { 0, 2, 4, 3, 5, 1 };

        /// <summary>한 팀의 인원: 2명 판 1, 4명 판 2, 6명 판 3. 땅따먹기를 할 수 없는 인원이면 0.</summary>
        public static int TeamSize(int players) => players == 2 || players == 4 || players == 6 ? players / 2 : 0;

        /// <summary>땅따먹기를 할 수 있는 인원인지 (2·4·6명).</summary>
        public static bool IsTeamCount(int players) => TeamSize(players) > 0;

        /// <summary>방 목록·전투·결과에 쓰는 팀 인원 표기 "1:1"·"2:2"·"3:3". 땅따먹기 인원이 아니면 빈 문자열.</summary>
        public static string MatchLabel(int players)
        {
            int size = TeamSize(players);
            return size > 0 ? size + ":" + size : string.Empty;
        }

        /// <summary>이 인원의 판에서 이 자리의 팀: 앞 절반은 청, 뒤 절반은 홍.</summary>
        public static LabTeam SeatTeam(int seat, int players)
        {
            int size = TeamSize(players);
            return size == 0 || seat < 0 || seat >= players ? LabTeam.None : seat < size ? LabTeam.Blue : LabTeam.Red;
        }

        public static LabTeam TeamOfSeat(int seat) => SeatTeam(seat, Players);

        public static LabOrbElement ElementOf(LabTeam team) =>
            team == LabTeam.Blue ? LabOrbElement.Water : team == LabTeam.Red ? LabOrbElement.Fire : LabOrbElement.None;

        public static LabTeam TeamOfElement(LabOrbElement element) =>
            element == LabOrbElement.Water ? LabTeam.Blue : element == LabOrbElement.Fire ? LabTeam.Red : LabTeam.None;

        public static LabTeam Opponent(LabTeam team) =>
            team == LabTeam.Blue ? LabTeam.Red : team == LabTeam.Red ? LabTeam.Blue : LabTeam.None;

        /// <summary>2:2: 이 자리에서 그쪽 가장자리로 팀원에게 넘길 수 있는지. 바깥쪽 가장자리는 막혀 있다.</summary>
        public static bool EdgeOpen(int seat, bool right) => seat >= 0 && seat < Players && (seat % 2 == 0) == right;

        /// <summary>2:2: 같은 팀의 다른 한 명 자리 (P1↔P2, P3↔P4).</summary>
        public static int TeammateSeat(int seat) => seat ^ 1;

        /// <summary>
        /// 이 자리에서 그쪽 가장자리로 넘기면 받는 팀원의 자리. 막혀 있으면 -1이다. 1:1은 언제나 -1, 2:2는 안쪽
        /// 가장자리만, 3:3은 박 둘레에서 그쪽에 앉은 팀원(팀 끝이면 반대쪽 끝 팀원)이다.
        /// </summary>
        public static int ReceiverSeat(int seat, bool right, int players)
        {
            int size = TeamSize(players);
            if (size < 2 || seat < 0 || seat >= players) return -1;
            if (size == 2) return EdgeOpen(seat, right) ? TeammateSeat(seat) : -1;
            int[] order = RingOrder(players);
            int ring = RingSpot(seat, players);
            LabTeam team = SeatTeam(seat, players);
            for (int step = 1; step < players; step++)
            {
                int other = order[(ring + (right ? step : players - step)) % players];
                if (SeatTeam(other, players) == team) return other;
            }
            return -1;
        }

        /// <summary>2:2: 박 둘레에서 서는 순서. 청·홍·청·홍이 되도록 P2와 P3만 자리를 바꾼다.</summary>
        public static int RingSeat(int seat) => seat == 1 ? 2 : seat == 2 ? 1 : seat;

        /// <summary>이 인원의 땅따먹기에서 박 둘레의 자리 번호. 다른 인원이면 P번호 순서 그대로다.</summary>
        public static int RingSpot(int seat, int players)
        {
            int[] order = RingOrder(players);
            int ring = order != null ? System.Array.IndexOf(order, seat) : -1;
            return ring >= 0 ? ring : seat;
        }

        private static int[] RingOrder(int players) =>
            players == 2 ? RingTwo : players == 4 ? RingFour : players == 6 ? RingSix : null;

        /// <summary>땅따먹기 스냅샷(1:1·2:2·3:3)인지. 화면·카메라·던지는 위치는 이때만 팀 자리 규칙을 쓴다. 솔로는 아니다.</summary>
        public static bool TeamSeats(LabSnapshot snapshot) =>
            snapshot != null && snapshot.teamTerritory && snapshot.players != null && IsTeamCount(snapshot.players.Length);

        /// <summary>박 둘레의 자리 번호. 땅따먹기면 팀 자리 순서를 따르고, 그 밖에는 P번호 순서 그대로다.</summary>
        public static int RingSeatFor(LabSnapshot snapshot, int seat) =>
            TeamSeats(snapshot) ? RingSpot(seat, snapshot.players.Length) : seat;

        /// <summary>
        /// 이 사람 판의 막힌 가장자리: 오른쪽 1, 왼쪽 -1, 양쪽 BothSides(1:1), 없으면 0 (팀전이 아니거나 솔로, 3:3).
        /// </summary>
        public static int ClosedSide(LabSnapshot snapshot, ulong player)
        {
            if (!TeamSeats(snapshot)) return 0;
            int players = snapshot.players.Length;
            LabPlayerState state = System.Array.Find(snapshot.players, item => item != null && item.id == player);
            if (state == null || state.seat < 0 || state.seat >= players) return 0;
            bool right = ReceiverSeat(state.seat, true, players) < 0, left = ReceiverSeat(state.seat, false, players) < 0;
            return right && left ? BothSides : right ? 1 : left ? -1 : 0;
        }

        /// <summary>ClosedSide 값에서 그쪽 가장자리(오른쪽이면 right)가 막혔는지.</summary>
        public static bool SideClosed(int closedSide, bool right) =>
            closedSide == BothSides || closedSide == (right ? 1 : -1);

        /// <summary>이 사람의 팀. 팀전 솔로는 혼자 청팀이다.</summary>
        public static LabTeam TeamOf(LabSnapshot snapshot, ulong player)
        {
            if (snapshot == null || !snapshot.teamTerritory || snapshot.players == null) return LabTeam.None;
            if (snapshot.players.Length == 1) return LabTeam.Blue;
            LabPlayerState state = System.Array.Find(snapshot.players, item => item != null && item.id == player);
            return state != null ? SeatTeam(state.seat, snapshot.players.Length) : LabTeam.None;
        }

        /// <summary>미니맵에 보일 사람: 땅따먹기면 우리 팀만, 그 밖에는 모두.</summary>
        public static LabPlayerState[] VisibleTeammates(LabSnapshot snapshot, ulong player)
        {
            if (!TeamSeats(snapshot)) return snapshot?.players;
            LabTeam team = TeamOf(snapshot, player);
            int players = snapshot.players.Length;
            return System.Array.FindAll(snapshot.players, item => item != null && SeatTeam(item.seat, players) == team);
        }
    }
}
