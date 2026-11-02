using System.Collections.Generic;

namespace C6Lab
{
    /// <summary>
    /// Per-peer rate limit for recovery requests the Host answers with a full resend (board_checkpoint, paint_resync).
    /// A request is accepted when the peer has none recorded or its last accepted one is at least the interval old,
    /// and only an accepted request is recorded, so a refused one never pushes the next window back (#435).
    /// </summary>
    public sealed class LabPeerRequestThrottle
    {
        private readonly Dictionary<ulong, double> acceptedAt = new Dictionary<ulong, double>();

        public int Count => acceptedAt.Count;

        public bool TryAccept(ulong peer, double now, double intervalSeconds)
        {
            if (acceptedAt.TryGetValue(peer, out double last) && now - last < intervalSeconds) return false;
            acceptedAt[peer] = now;
            return true;
        }

        public void Forget(ulong peer) => acceptedAt.Remove(peer);

        public void Clear() => acceptedAt.Clear();
    }
}
