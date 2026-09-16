using System;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("C6.Prototype.Lobby.EditModeTests")]

namespace C6.Prototype.Lobby.Discovery
{
    // Shared by re-registration and periodic TXT updates for one discovery instance. A new
    // room may start above zero; q is an ordering value, not a room or gameplay identity.
    internal sealed class DiscoveryHeartbeatSequence
    {
        internal ulong Current { get; private set; }
        internal DiscoveryHeartbeatSequence(ulong initialValue = 0) { Current = initialValue; }
        internal bool TryAdvance()
        {
            if (Current == ulong.MaxValue) return false;
            Current++; return true;
        }
    }

    /// <summary>Bounded recovery of one discovered service. Refresh creates a fresh service.</summary>
    internal sealed class DiscoveryRecoveryState
    {
        internal const int MaximumAttempts = 3;
        internal int Attempts { get; private set; }
        internal double NextAttemptAt { get; private set; } = double.PositiveInfinity;
        internal void StartAttempt() { Attempts++; NextAttemptAt = double.PositiveInfinity; }
        internal bool Schedule(double now)
        {
            if (Attempts >= MaximumAttempts) return false;
            NextAttemptAt = now + Math.Max(1, Attempts); // DEMO_TUNING_VALUE:1s then2s.
            return true;
        }
        internal bool IsDue(double now) => now >= NextAttemptAt;
        internal void ConfirmLiveHeartbeat() { Attempts = 1; NextAttemptAt = double.PositiveInfinity; }
    }

    internal sealed class DiscoveryHeartbeatLease
    {
        internal bool HasReceived { get; private set; }
        internal ulong Sequence { get; private set; }
        internal double ReceivedAt { get; private set; }
        internal double ExpiresAt => ReceivedAt + DiscoveryRoomCatalog.DefaultLifetimeSeconds;
        internal bool TryRefresh(ulong sequence, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) ||
                (HasReceived && (sequence <= Sequence || now < ReceivedAt))) return false;
            HasReceived = true; Sequence = sequence; ReceivedAt = now; return true;
        }
        internal bool IsFresh(double now) => HasReceived && now >= ReceivedAt && now < ExpiresAt;
    }
}
