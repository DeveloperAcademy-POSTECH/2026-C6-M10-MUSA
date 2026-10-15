using System;

namespace C6Lab
{
    public enum LabPhase { Idle, Playing, Victory, Defeat }
    // Items and the drum are appended so the serialized numbers of existing kinds stay unchanged.
    public enum LabOrbKind { Yin, Yang, Combined, Relay, SecretBox, Potion, Bomb, Drum }
    public enum LabOrbOrigin { Unknown, Generated, Combined }

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
        /// <summary>Orbs that an upward flick may throw at the target.</summary>
        public static bool CanThrow(this LabOrbKind kind, int lockSwipes) =>
            kind == LabOrbKind.Combined || ((kind == LabOrbKind.Relay || kind == LabOrbKind.Bomb) && lockSwipes == 0);
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

        /// <summary>An unlock older than this, for example seen after a reconnect, shows the item already open.</summary>
        public const double UnlockEffectWindowSeconds = 1d;

        public LabOrbState Copy() => (LabOrbState)MemberwiseClone();

        public bool JustUnlocked(double serverTime) =>
            unlockedAt >= 0d && serverTime - unlockedAt <= UnlockEffectWindowSeconds;
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
        /// <summary>북을 하나 이상 가지고 있던 시간(초). 북을 여러 개 가져도 한 번만 센다.</summary>
        public double drumSeconds;

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
        /// <summary>Host-owned presentation pose, synchronized separately from HP and collision.</summary>
        public float targetYawDegrees, targetYawSpeedDegreesPerSecond;
        public LabPlayerState[] players = Array.Empty<LabPlayerState>();
        public LabOrbState[] orbs = Array.Empty<LabOrbState>();
        /// <summary>Generate drums. Only a player whose board holds one can create orbs.</summary>
        public LabOrbState[] drums = Array.Empty<LabOrbState>();
    }
}
