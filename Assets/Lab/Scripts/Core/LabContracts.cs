using System;

namespace C6Lab
{
    public enum LabPhase { Idle, Playing, Victory, Defeat }
    public enum LabOrbKind { Yin, Yang, Combined }
    public enum LabOrbOrigin { Unknown, Generated, Combined }

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
        public LabOrbOrigin origin;
        public ulong originOwner;
        public float x, y, vx, vy;
        public double createdAt;
        public ulong revision;
        public bool inFlight;

        public LabOrbState Copy() => (LabOrbState)MemberwiseClone();
    }

    [Serializable]
    public sealed class LabPlayerState
    {
        public ulong id;
        public float stamina;
        public int seat;

        public LabPlayerState Copy() => (LabPlayerState)MemberwiseClone();
    }

    [Serializable]
    public sealed class LabSnapshot
    {
        public string sessionId;
        public uint round;
        public LabPhase phase;
        public double serverTime, remaining;
        public int hp;
        public LabPlayerState[] players = Array.Empty<LabPlayerState>();
        public LabOrbState[] orbs = Array.Empty<LabOrbState>();
    }
}
