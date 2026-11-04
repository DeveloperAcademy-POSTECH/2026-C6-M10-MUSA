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
        private readonly System.Collections.Generic.HashSet<LabOrbKind> tutorialPassKinds =
            new System.Collections.Generic.HashSet<LabOrbKind>();
        private int tutorialPassSide;

        /// <summary>
        /// Which kinds may cross a side edge to a story neighbor right now, over either edge. Nothing else crosses: like a
        /// solo board, an orb that reaches an edge bounces back. The tutorial opens only the kind its step passes.
        /// </summary>
        public void TutorialAllowPasses(params LabOrbKind[] kinds) => TutorialAllowPasses(0, kinds);

        /// <summary>
        /// The same over one edge only: <paramref name="side"/> +1 the right edge (5단계: the bomb to 금옥이), -1 the
        /// left, 0 both (4단계: the drum, which 묵이 hands back).
        /// </summary>
        public void TutorialAllowPasses(int side, params LabOrbKind[] kinds)
        {
            tutorialPassKinds.Clear();
            tutorialPassSide = side > 0 ? 1 : side < 0 ? -1 : 0;
            if (kinds != null) foreach (LabOrbKind kind in kinds) tutorialPassKinds.Add(kind);
        }

        public bool TutorialPassAllowed(LabOrbKind kind) => tutorialSeats && tutorialPassKinds.Contains(kind);

        public bool TutorialPassAllowed(LabOrbKind kind, bool toRight) => TutorialPassAllowed(kind)
            && (tutorialPassSide == 0 || tutorialPassSide > 0 == toRight);

        /// <summary>Whether this edge crossing may go on to a story neighbor (false outside the tutorial).</summary>
        private bool TutorialCrossingAllowed(string orbId, bool toRight)
        {
            if (!tutorialSeats || latest == null || string.IsNullOrEmpty(orbId)) return false;
            foreach (LabOrbState drum in latest.drums ?? System.Array.Empty<LabOrbState>())
                if (drum != null && drum.id == orbId) return TutorialPassAllowed(drum.kind, toRight);
            foreach (LabOrbState orb in latest.orbs ?? System.Array.Empty<LabOrbState>())
                if (orb != null && orb.id == orbId) return TutorialPassAllowed(orb.kind, toRight);
            return false;
        }

        /// <summary>
        /// 5단계: the gourd drops a bomb with <paramref name="lockSwipes"/> locks at <paramref name="landing"/> on my board.
        /// The drop is announced before the snapshot that holds it, so it falls in from above with the gourd's warning.
        /// </summary>
        public bool TutorialDropGourdBomb(Vector2 landing, int lockSwipes)
        {
            if (!TutorialHostReady || !model.TutorialDropGourdBomb(LocalPlayerId, landing, lockSwipes,
                battleClock.Now(Time.realtimeSinceStartupAsDouble), out _)) return false;
            PublishSnapshot(true);
            return true;
        }

        /// <summary>
        /// 6단계: the gourd holds one of my side edges shut (+1 right, -1 left) until lifted (0). It is the game's own
        /// swipe block (#215) — the red edge, its opening flashes, the gourd's warning and the bounce — with no countdown
        /// written on the edge, since it waits for the player instead of running out.
        /// </summary>
        public bool TutorialBlockSwipe(int side)
        {
            if (!TutorialHostReady || !model.TutorialBlockSwipe(LocalPlayerId, side,
                battleClock.Now(Time.realtimeSinceStartupAsDouble))) return false;
            if (board != null) board.SetSwipeBlockCountdown(false);
            PublishSnapshot(true);
            return true;
        }

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

        /// <summary>
        /// 3단계: give a missed orb back. It appears at <paramref name="landing"/> (0..1) on my board, falling in from
        /// above like the gourd's drops (#177).
        /// </summary>
        public bool TutorialDropOrb(LabOrbKind kind, LabOrbElement element, Vector2 landing)
        {
            if (!TutorialHostReady || !model.TutorialDropOrb(LocalPlayerId, kind, element, landing,
                battleClock.Now(Time.realtimeSinceStartupAsDouble), out LabOrbState orb)) return false;
            // Before the snapshot that holds it, so the board drops it in instead of popping it up.
            if (board != null) board.ExpectDrop(orb.id);
            PublishSnapshot(true);
            return true;
        }

        /// <summary>
        /// A story neighbor passes <paramref name="orbId"/> back to me over our shared edge, entering at
        /// <paramref name="height"/> (0..1) and sliding in at <paramref name="speed"/> board widths per second.
        /// </summary>
        public bool TutorialHandOver(ulong from, string orbId, float height, float speed)
        {
            if (!TutorialHostReady || !IsVirtualSeat(from)) return false;
            if (!model.TutorialHandOver(from, orbId, LocalPlayerId, height, speed,
                battleClock.Now(Time.realtimeSinceStartupAsDouble), out string reason))
            {
                Debug.LogWarning("C6_LAB_TUTORIAL_HAND_OVER_REFUSED " + reason);
                return false;
            }
            PublishSnapshot(true);
            return true;
        }

        /// <summary>A story neighbor makes an orb of its own and passes it to me (4단계: 금옥이의 양구슬).</summary>
        public bool TutorialGiveFrom(ulong from, LabOrbKind kind, LabOrbElement element, float height, float speed)
        {
            if (!TutorialHostReady || !IsVirtualSeat(from)) return false;
            double now = battleClock.Now(Time.realtimeSinceStartupAsDouble);
            if (!model.TutorialDropOrb(from, kind, element, new Vector2(.5f, Mathf.Clamp01(height)), now, out LabOrbState made))
                return false;
            return TutorialHandOver(from, made.id, height, speed);
        }

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
