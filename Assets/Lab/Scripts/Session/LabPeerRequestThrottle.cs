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

    /// <summary>
    /// Reports when a nonzero value has made no progress for at least the given time; value 0 means nothing to watch.
    /// The Guest uses it so only a paint frame that received no new fragment for a second asks for a checkpoint (#437).
    /// </summary>
    public sealed class LabStallWatch
    {
        private readonly double stallSeconds;
        private long value;
        private int progress;
        private double since;

        public LabStallWatch(double stallSeconds) => this.stallSeconds = stallSeconds;

        public bool Stalled(long current, int currentProgress, double now)
        {
            if (current != value || currentProgress != progress) { value = current; progress = currentProgress; since = now; }
            return current != 0 && now - since >= stallSeconds;
        }

        public void Clear() { value = 0; progress = 0; since = 0d; }
    }
}
