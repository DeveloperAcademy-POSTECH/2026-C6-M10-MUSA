using System;

namespace C6Lab
{
    public enum LabPhase { Idle, Playing, Victory, Defeat }
    // Items and the drum are appended so the serialized numbers of existing kinds stay unchanged.
    // FoxFire is the nine-tailed fox's fake orb (#251).
    public enum LabOrbKind { Yin, Yang, Combined, Relay, SecretBox, Potion, Bomb, Drum, FoxFire }
    // Gourd is appended for bombs the gourd drops as sabotage (#177).
    public enum LabOrbOrigin { Unknown, Generated, Combined, Gourd }
    // The yokai's sabotages the sabotage mix draws from (#251), in the order of their DEV weights. Numbers are serialized.
    public enum LabSabotageKind { GourdBomb, SwipeBlock, FoxFire, Charm }

    /// <summary>
    /// How a drum handed to a neighbour arrives on its new board (#178). Stop eases it to rest at a
    /// Host-drawn spot; Pass keeps it going on to the next player; Coast lets a drum a blast flung
    /// across slide on its own speed but still come to rest on that board. Numbers are serialized.
    /// </summary>
    public enum LabDrumArrival { None, Stop, Pass, Coast }

    /// <summary>Why a body crossed a side edge, reported with its handoff request (#178). Numbers are serialized.</summary>
    public enum LabEdgeCrossingKind
    {
        /// <summary>Swiped, flicked or dragged across by the player.</summary>
        Swipe,
        /// <summary>A drum still running on through this board after a Pass arrival.</summary>
        Passing,
        /// <summary>Flung across by a bomb blast.</summary>
        Pushed
    }

    public static class LabOrbKindRules
    {
        public static bool IsRaw(this LabOrbKind kind) => kind == LabOrbKind.Yin || kind == LabOrbKind.Yang;
        public static bool IsItem(this LabOrbKind kind) => kind >= LabOrbKind.Relay && kind <= LabOrbKind.Bomb;
        /// <summary>
        /// The generate drum moves and passes between screens like an orb, but it is kept in its
        /// own snapshot list so orb rules (lifetime, storage, combining, throwing, bombs) never apply.
        /// </summary>
        public static bool IsDrum(this LabOrbKind kind) => kind == LabOrbKind.Drum;
        /// <summary>Items used in place by a double tap once unlocked.</summary>
        public static bool IsDoubleTapUsable(this LabOrbKind kind) => kind == LabOrbKind.Potion || kind == LabOrbKind.SecretBox;
        /// <summary>
        /// Orbs that an upward flick may throw at the target: Combined, and a relay orb or bomb once
        /// handoffs have opened its lock (#212). A locked bomb can only be passed on.
        /// </summary>
        public static bool CanThrow(this LabOrbKind kind, int lockSwipes) =>
            kind == LabOrbKind.Combined || ((kind == LabOrbKind.Relay || kind == LabOrbKind.Bomb) && lockSwipes == 0);

        /// <summary>
        /// How long an orb lasts on the boards, the same on the Host and every screen (#235): items the
        /// item lifetime, which is a locked bomb's fuse; everything else, an unlocked bomb included, the
        /// orb lifetime.
        /// </summary>
        public static float LifetimeFor(this LabOrbKind kind, int lockSwipes, float orbLifetime, float itemLifetime) =>
            kind.IsItem() && !(kind == LabOrbKind.Bomb && lockSwipes == 0) ? itemLifetime : orbLifetime;
    }

    /// <summary>
    /// 오행 속성. 음/양 구슬은 None이고, 결합 구슬만 Host가 다섯 속성 중 하나를 정한다.
    /// 값의 순서는 네트워크 스냅샷에 정수로 저장되므로 바꾸지 않는다.
    /// </summary>
    public enum LabOrbElement { None, Fire, Water, Wood, Metal, Earth }

    /// <summary>One Host-approved orb. Positions and velocities are normalized to the board.</summary>
    [Serializable]
    public sealed class LabOrbState
    {
        public string id;
        public ulong owner;
        /// <summary>
        /// Ownership epoch. Starts at one and advances only after an accepted handoff, so a late
        /// request from an earlier visit to the same board cannot use this orb again.
        /// </summary>
        public ulong ownershipVersion = 1;
        public LabOrbKind kind;
        public LabOrbElement element;
        /// <summary>Handoffs to a neighbor's screen still needed before an item can be used; 0 means unlocked.</summary>
        public int lockSwipes;
        /// <summary>Unlocked relay orb stage: 1 = x1, 2 = x2, 3 = MAX. 0 for every other orb.</summary>
        public int relayStage;
        public LabOrbOrigin origin;
        public ulong originOwner;
        public float x, y, vx, vy;
        public double createdAt;
        /// <summary>
        /// Host time of the handoff that unlocked this item, so the receiving screen can play the
        /// unlock effect; -1 when no handoff unlocked it or it has been handed on since.
        /// </summary>
        public double unlockedAt = -1d;
        public ulong revision;
        public bool inFlight;
        /// <summary>For a drum handed to this board (#178): how it arrives. None for every orb.</summary>
        public LabDrumArrival arrival;
        /// <summary>
        /// Share of the board width, from the edge the drum entered by, where it comes to rest (Stop),
        /// or the farthest it may slide (Coast); -1 otherwise.
        /// </summary>
        public float stopAt = -1f;
        /// <summary>Host time of the handoff that set the arrival, so only the receiving screen applies it; -1 when none.</summary>
        public double arrivedAt = -1d;
        /// <summary>Host time a bomb blast set this orb alight (#213); -1 when it never burned.</summary>
        public double burnedAt = -1d;
        /// <summary>A bomb blast shrank this orb for the rest of its life, and any orb combined from it (#213).</summary>
        public bool small;

        /// <summary>An unlock older than this, for example seen after a reconnect, shows the item already open.</summary>
        public const double UnlockEffectWindowSeconds = 1d;
        /// <summary>An arrival older than this, for example seen after a reconnect, is not replayed.</summary>
        public const double ArrivalWindowSeconds = 1d;

        public LabOrbState Copy() => (LabOrbState)MemberwiseClone();

        public bool JustUnlocked(double serverTime) =>
            unlockedAt >= 0d && serverTime - unlockedAt <= UnlockEffectWindowSeconds;

        public bool JustArrived(double serverTime) =>
            arrival != LabDrumArrival.None && arrivedAt >= 0d && serverTime - arrivedAt <= ArrivalWindowSeconds;

        /// <summary>Seconds left in this orb's lifetime at the given Host time (#235); never below 0.</summary>
        public float LifeSecondsLeft(double serverTime, float orbLifetime, float itemLifetime) =>
            (float)Math.Max(0d, createdAt + kind.LifetimeFor(lockSwipes, orbLifetime, itemLifetime) - serverTime);

        /// <summary>Seconds this orb still burns at the given Host time; 0 once out, or when it never burned.</summary>
        public float BurnSecondsLeft(double serverTime, float burnSeconds) =>
            burnedAt < 0d ? 0f : (float)Math.Max(0d, burnedAt + burnSeconds - serverTime);
    }

    [Serializable]
    public sealed class LabPlayerState
    {
        public ulong id;
        public float stamina;
        public int personalHp;
        public int seat;

        // 결과 대시보드용 이번 라운드 기록. Host가 성공한 행동만 센다.
        /// <summary>구슬 생성 성공 수</summary>
        public int generatedTotal;
        /// <summary>결합 성공 수 (내가 만든 결합 구슬)</summary>
        public int combinedTotal;
        /// <summary>이웃에게 넘겨준 구슬 수 (북 제외)</summary>
        public int passedTotal;
        /// <summary>이웃에게 넘겨준 북 수</summary>
        public int drumPassedTotal;
        /// <summary>아이템 사용 수: 릴레이·폭탄은 던졌을 때, 비밀상자·포션은 더블탭 사용했을 때</summary>
        public int relayUsed, secretBoxUsed, potionUsed, bombUsed;

        /// <summary>요괴에 맞힌 횟수 (Host가 타격을 확정한 것만)</summary>
        public int hitTotal;
        /// <summary>요괴에게 준 총 피해 (남은 HP보다 큰 피해는 남은 만큼만)</summary>
        public int damageTotal;
        /// <summary>요괴 HP를 0으로 만든 마지막 한 방(막타)을 쳤는지. 승리했을 때만 한 명이 true.</summary>
        public bool lastHit;
        /// <summary>북 점유 시간(초): 북을 하나 이상 점유한 시간. 여러 개여도 한 번만 센다. 북 점유율 = 이 값 / playedSeconds</summary>
        public double drumSeconds;
        /// <summary>
        /// 박 색상 콤보 실패 페널티(#222)가 끝나는 전투 시각. 이 시각 전까지 이 사람이 던지는 공과
        /// 칠 면적이 줄어든다. 페널티가 없으면 0 이하. 남은 시간 = 이 값 - 스냅샷 serverTime.
        /// </summary>
        public double comboPenaltyUntil;
        /// <summary>
        /// 박 색상 콤보 성공 보상(#222)이 끝나는 전투 시각. 이 시각 전까지 이 사람이 던지는 공과
        /// 칠 면적이 커진다. 페널티와 보상은 마지막 결과 하나만 남는다(하나를 걸면 다른 하나는 끝난다).
        /// </summary>
        public double comboRewardUntil;

        public int ItemsUsed => relayUsed + secretBoxUsed + potionUsed + bombUsed;

        public LabPlayerState Copy() => (LabPlayerState)MemberwiseClone();
    }

    [Serializable]
    public sealed class LabSnapshot
    {
        public string sessionId;
        public uint round;
        public LabPhase phase;
        /// <summary>Host-approved full-session pause; the battle phase remains Playing.</summary>
        public bool paused;
        public double serverTime, remaining;
        /// <summary>이번 라운드에 실제로 진행된 전투 시간(초). 결과 화면의 북 점유율 기준.</summary>
        public double playedSeconds;
        public int hp;
        /// <summary>Host-approved fraction of the whole gourd shell covered by elemental paint.</summary>
        public float paintCoverage;
        /// <summary>Paint always completes at exactly 100 percent of the round's cells.</summary>
        public int paintGoalPercent;
        /// <summary>Dimensions and identity of the Host-owned paint checkpoint for this snapshot.</summary>
        public int paintColumns, paintRows;
        public uint paintRevision;
        public int paintCoveredCount, paintCellCount, paintCount;
        /// <summary>Host-owned presentation pose, synchronized separately from HP and collision.</summary>
        public float targetYawDegrees, targetYawSpeedDegreesPerSecond;
        /// <summary>Host rope cycle phase and hit spring state for the moving target collider.</summary>
        public float targetRopeCycleElapsed, targetRopeHitOffset, targetRopeHitVelocity;
        public LabPlayerState[] players = Array.Empty<LabPlayerState>();
        public LabOrbState[] orbs = Array.Empty<LabOrbState>();
        /// <summary>Generate drums. Only a player whose board holds one can create orbs.</summary>
        public LabOrbState[] drums = Array.Empty<LabOrbState>();
        /// <summary>
        /// The gourd's swipe block (#215): the player whose own passes one way are stopped, which way,
        /// and the Host times it started and ends. swipeBlockedUntil is -1 while none is on.
        /// </summary>
        public ulong swipeBlockedPlayer;
        public bool swipeBlockedRight;
        public double swipeBlockStartedAt = -1d, swipeBlockedUntil = -1d;

        /// <summary>Whether a swipe block is on this player at this snapshot.</summary>
        public bool SwipeBlockOn(ulong player) => swipeBlockedUntil > serverTime && swipeBlockedPlayer == player;

        /// <summary>
        /// The nine-tailed fox's charm (#251): the player too dazed to attack (throw upward), the Host times it
        /// started and ends, and how many times the players beside them have tapped to wake them. charmUntil is
        /// -1 while no one is charmed.
        /// </summary>
        public ulong charmedPlayer;
        public double charmStartedAt = -1d, charmUntil = -1d;
        public int charmTaps;

        /// <summary>Whether this player is charmed at this snapshot.</summary>
        public bool CharmOn(ulong player) => charmUntil > serverTime && serverTime >= charmStartedAt && charmedPlayer == player;

        // 박 색상 콤보 실험 (#222). DEV에서 끄면 모두 기본값(콤보 없음)이다.
        // 시각은 serverTime과 같은 전투 시계 기준이다.
        /// <summary>지금 진행 중인 콤보가 있는지</summary>
        public bool comboActive;
        /// <summary>진행 중인(또는 마지막) 콤보의 4칸 색. 콤보가 한 번도 안 나왔으면 빈 배열.</summary>
        public LabOrbElement[] comboColors = Array.Empty<LabOrbElement>();
        /// <summary>맞힌 칸 수 (0~4)</summary>
        public int comboProgress;
        /// <summary>진행 중인 콤보가 나온 시각과 제한 시간이 끝나는 시각</summary>
        public double comboStartedAt = -1d;
        public double comboEndsAt = -1d;
        /// <summary>이번 라운드에 나온 콤보 수. 바뀌면 새 콤보가 나온 것이다.</summary>
        public uint comboCount;
        /// <summary>마지막으로 끝난 콤보의 결과·번호·끝난 시각·틀린 색을 던진 사람(틀린 색일 때만)</summary>
        public LabComboResult comboResult;
        public uint comboResultCount;
        public double comboResultAt = -1d;
        public ulong comboFailedBy;
        /// <summary>
        /// 다음 콤보 4칸이 나오는 시각과 그 예고(COMBO! 3, 2, 1)가 시작되는 시각 (#241). 둘 다 전투 시계 기준이고,
        /// 콤보가 진행 중이거나 꺼져 있으면 -1이다. 예고 시간이 0이면 두 시각이 같다(예고 없음).
        /// 예고 시작 시각은 전투 시작보다 이를 수 있다(첫 시작 시각이 예고 시간보다 짧을 때).
        /// </summary>
        public double comboNextAt = -1d;
        public double comboWarningAt = -1d;

        /// <summary>이 시각에 콤보 예고 중인지 (#241). 예고 중에는 콤보가 없으므로 맞힌 구슬도 판정하지 않는다.</summary>
        public bool ComboWarningOn(double now) => comboNextAt > comboWarningAt && now >= comboWarningAt && now < comboNextAt;
    }
}
