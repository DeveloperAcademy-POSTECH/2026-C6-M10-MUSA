namespace C6Lab
{
    /// <summary>
    /// 튜토리얼 (#477): 이 기기 혼자 여는 3자리 연습 판. 가운데가 나(P2)이고, 오른쪽 이웃(앞 번호 P1)·왼쪽 이웃(다음 번호 P3)은
    /// 화면에만 있는 가상 이웃이라 연결된 기기가 없고, 그 자리로 가는 패킷은 보내지 않는다. 혼자 판(developerSolo)의 흐름을
    /// 그대로 쓰되 개발 빌드가 아니어도 열리고, 판을 열면 북을 내 판 가운데에 둔다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>The story character on the player's right: the previous seat, P1 (<see cref="LabPlayerSlots.NeighborSeat"/>).</summary>
        public const ulong TutorialRightSeatId = 0x7475_7400_0001UL;
        /// <summary>The story character on the player's left: the next seat, P3.</summary>
        public const ulong TutorialLeftSeatId = 0x7475_7400_0003UL;
        private bool tutorialSeats;

        /// <summary>This device's practice board with two virtual neighbors.</summary>
        public bool IsTutorial => tutorialSeats;

        /// <summary>A seat with no device behind it: nothing is ever sent there.</summary>
        public bool IsVirtualSeat(ulong playerId) => tutorialSeats
            && (playerId == TutorialLeftSeatId || playerId == TutorialRightSeatId);

        /// <summary>Open the practice room without starting combat before Battle finishes loading.</summary>
        public bool PrepareTutorialGame()
        {
            if (!gameFlowEnabled || !CanChooseConnection) return false;
            if (!LabModel.ValidRoomSize(targetParticipantCount, false)) targetParticipantCount = LabModel.DefaultParticipants;
            Host();
            if (!IsHost || order.Count != 1) return false;
            developerSolo = true;
            tutorialSeats = true;
            DisposeDiscovery();
            return true;
        }

        private void BeginTutorialModel(double beginAt)
        {
            // P1..P3 in seat order: right neighbor, me, left neighbor.
            model.Begin(sessionId, new[] { TutorialRightSeatId, LocalPlayerId, TutorialLeftSeatId }, beginAt, config,
                stageYokai: gameFlowEnabled ? gameStageYokai : LabStageYokai.None);
            model.TutorialHandDrumsTo(LocalPlayerId);
        }
    }
}
