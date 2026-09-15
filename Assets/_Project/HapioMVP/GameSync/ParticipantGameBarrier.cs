using System;
using System.Collections.Generic;
using System.Linq;

namespace C6.Prototype.GameSync
{
    /// <summary>
    /// One ACK and one response deadline per authenticated remote participant. A chatty participant
    /// cannot confirm another participant's initial state or keep their expired connection alive.
    /// The caller validates session, round, nonce and initial-state hash before calling these methods.
    /// </summary>
    public sealed class ParticipantGameBarrier
    {
        private readonly Dictionary<ulong, PeerResponseWatchdog> responses = new Dictionary<ulong, PeerResponseWatchdog>();
        private readonly HashSet<ulong> acknowledgements = new HashSet<ulong>();
        public int ExpectedCount => responses.Count;
        public int AcknowledgedCount => acknowledgements.Count;
        public bool AllAcknowledged => ExpectedCount > 0 && AcknowledgedCount == ExpectedCount;

        public void Configure(IEnumerable<ulong> participantIds)
        {
            if (participantIds == null) throw new ArgumentNullException(nameof(participantIds));
            var ids = participantIds.ToArray();
            if (ids.Length < 1 || ids.Length > 4 || ids.Distinct().Count() != ids.Length)
                throw new ArgumentException("One to four distinct remote participants are required.", nameof(participantIds));
            responses.Clear(); acknowledgements.Clear();
            foreach (ulong id in ids) responses.Add(id, new PeerResponseWatchdog());
        }

        public bool Contains(ulong id) => responses.ContainsKey(id);
        public bool Acknowledge(ulong id) => responses.ContainsKey(id) && acknowledgements.Add(id);
        public bool Observe(ulong id, double now)
        {
            if (!responses.TryGetValue(id, out var response)) return false;
            return response.IsArmed ? response.Observe(now) : response.Begin(now);
        }
        public void Arm(double now)
        {
            foreach (var response in responses.Values) response.Begin(now);
        }
        public bool TryExpired(double now, double timeout, out ulong participantId)
        {
            foreach (var item in responses)
                if (item.Value.IsExpired(now, timeout)) { participantId = item.Key; return true; }
            participantId = 0; return false;
        }
        public void Reset()
        {
            acknowledgements.Clear();
            foreach (var response in responses.Values) response.Reset();
        }
        public void Clear() { responses.Clear(); acknowledgements.Clear(); }
    }
}
