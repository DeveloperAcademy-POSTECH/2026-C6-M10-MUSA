using UnityEngine;

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
            // The gauge only refills once the tutorial has shown it running out (1단계).
            model.TutorialHoldStaminaRecovery(true, beginAt);
        }

        private bool TutorialHostReady => tutorialSeats && IsHost && model != null && latest != null
            && latest.phase == LabPhase.Playing;

        /// <summary>My next drum taps make exactly these orbs, in order (1단계: 음·양·음).</summary>
        public bool TutorialScriptGenerations(params LabOrbKind[] kinds) =>
            TutorialHostReady && model.TutorialScriptGenerations(LocalPlayerId, kinds);

        /// <summary>The same, with each orb's launch (board widths per second) so it always rests in the same place.</summary>
        public bool TutorialScriptGenerations(System.Collections.Generic.IReadOnlyList<LabOrbKind> kinds,
            System.Collections.Generic.IReadOnlyList<Vector2> launches) =>
            TutorialHostReady && model.TutorialScriptGenerations(LocalPlayerId, kinds, launches);

        /// <summary>My next combinations make exactly these elements, in order (2단계: 화).</summary>
        public bool TutorialScriptCombinations(params LabOrbElement[] elements) =>
            TutorialHostReady && model.TutorialScriptCombinations(LocalPlayerId, elements);

        /// <summary>Hold or release the stamina refill, and show it at once.</summary>
        public bool TutorialHoldStamina(bool held) => TutorialCapStamina(held ? 0f : float.PositiveInfinity);

        /// <summary>Let the stamina refill only up to <paramref name="ceiling"/> (1단계: a sliver for the short cue).</summary>
        public bool TutorialCapStamina(float ceiling)
        {
            if (!TutorialHostReady
                || !model.TutorialCapStaminaRecovery(ceiling, battleClock.Now(Time.realtimeSinceStartupAsDouble))) return false;
            PublishSnapshot(true);
            return true;
        }

        /// <summary>Whether the refill is held right now.</summary>
        public bool TutorialStaminaHeld => tutorialSeats && model != null && model.TutorialStaminaHeld;

        /// <summary>The "not enough stamina" shake, flash and sound, as if a tap had been refused (#418).</summary>
        public void TutorialPlayStaminaShort()
        {
            if (TutorialHostReady) LocalStaminaShort?.Invoke();
        }
    }
}
