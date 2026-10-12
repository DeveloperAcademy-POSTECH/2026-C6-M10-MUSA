using System;

namespace C6Lab
{
    public enum LabPhase { Idle, Playing, Victory, Defeat }
    // Items are appended so the serialized numbers of existing kinds stay unchanged.
    public enum LabOrbKind { Yin, Yang, Combined, Relay, SecretBox, Potion, Bomb }
    public enum LabOrbOrigin { Unknown, Generated, Combined }
    /// <summary>Five-element attribute of a Combined orb (화·수·목·금·토). Other kinds keep None.</summary>
    public enum LabOrbElement { None, Fire, Water, Wood, Metal, Earth }

    public static class LabOrbKindRules
    {
        public static bool IsRaw(this LabOrbKind kind) => kind == LabOrbKind.Yin || kind == LabOrbKind.Yang;
        public static bool IsItem(this LabOrbKind kind) => kind >= LabOrbKind.Relay && kind <= LabOrbKind.Bomb;
    }

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
