using System;
using System.Collections.Generic;

namespace C6Lab
{
    /// <summary>
    /// The thrower's own shots that were presented on contact before the Host decided (#459): at most one presentation
    /// per launch request, found again by the projectile ID the Host's targetImpact/targetMiss name. The record keeps
    /// what was applied to the gourd so the Host's result can correct it, or take it back. A decided launch is
    /// forgotten, so a later launch of the same orb ID is its own entry.
    /// </summary>
    public sealed class LabLocalHitPresentation
    {
        public enum Outcome { Hit, Miss }

        public readonly struct Presented
        {
            public readonly Outcome Outcome;
            public readonly LabHangingTarget.AppliedImpact Applied;
            public readonly double PresentedAt;

            public Presented(Outcome outcome, LabHangingTarget.AppliedImpact applied, double presentedAt)
            {
                Outcome = outcome; Applied = applied; PresentedAt = presentedAt;
            }
        }

        private readonly Dictionary<long, string> projectiles = new Dictionary<long, string>();
        private readonly Dictionary<long, Presented> presented = new Dictionary<long, Presented>();
        // Launches whose presented hit was taken back by time, by request so the projectile notice can still name the
        // Host's ID. A late Host verdict for one must not replay it; a rollback or relaunch of the orb ends it.
        private readonly Dictionary<long, string> expired = new Dictionary<long, string>();
        public int Count => presented.Count;

        /// <summary>Presented hits still waiting for the Host's verdict.</summary>
        public int PendingHits
        {
            get
            {
                int count = 0;
                foreach (Presented value in presented.Values) if (value.Outcome == Outcome.Hit) count++;
                return count;
            }
        }

        /// <summary>A launch whose visual copy may report a contact; the projectile ID may still be provisional.</summary>
        public void Register(long requestId, string projectileId)
        {
            if (requestId <= 0) return;
            projectiles[requestId] = projectileId;
            ForgetExpired(projectileId); // A relaunch of the same orb is a new shot.
        }

        /// <summary>The Host's projectile notice names the approved projectile ID for this request.</summary>
        public void Bind(long requestId, string projectileId)
        {
            if (requestId <= 0 || string.IsNullOrEmpty(projectileId)) return;
            if (projectiles.ContainsKey(requestId)) projectiles[requestId] = projectileId;
            else if (expired.ContainsKey(requestId)) expired[requestId] = projectileId;
        }

        public bool CanPresent(long requestId) => projectiles.ContainsKey(requestId) && !presented.ContainsKey(requestId);

        /// <summary>Record the one presentation of a registered, undecided launch.</summary>
        public bool TryPresent(long requestId, Presented value)
        {
            if (!CanPresent(requestId)) return false;
            presented.Add(requestId, value);
            return true;
        }

        /// <summary>The Host decided this projectile: forget the launch and hand back what was presented for it, once.</summary>
        public bool TryTake(string projectileId, out Presented value)
        {
            value = default;
            if (string.IsNullOrEmpty(projectileId)) return false;
            long request = 0;
            foreach (var pair in projectiles)
                if (pair.Value == projectileId) { request = pair.Key; break; }
            return request != 0 && TryTakeRequest(request, out value);
        }

        /// <summary>The launch was decided, rolled back or abandoned: forget it and hand back its presentation.</summary>
        public bool TryTakeRequest(long requestId, out Presented value)
        {
            projectiles.Remove(requestId);
            expired.Remove(requestId);
            if (!presented.TryGetValue(requestId, out value)) return false;
            presented.Remove(requestId);
            return true;
        }

        /// <summary>
        /// Presented hits the Host never answered within <paramref name="maximumAge"/>. The Host sends nothing when it
        /// voids a contact (round over, curtain, supply), so these are taken back by time.
        /// </summary>
        public void TakeExpiredHits(double now, double maximumAge, List<Presented> expired)
        {
            List<long> requests = null;
            foreach (var pair in presented)
                if (pair.Value.Outcome == Outcome.Hit && now - pair.Value.PresentedAt >= maximumAge)
                    (requests ??= new List<long>()).Add(pair.Key);
            if (requests == null) return;
            foreach (long request in requests)
            {
                projectiles.TryGetValue(request, out string projectile);
                if (TryTakeRequest(request, out Presented value)) expired.Add(value);
                this.expired[request] = projectile;
            }
        }

        /// <summary>A Host verdict arrived for a hit already taken back by time: it was presented once, not twice.</summary>
        public bool TryTakeExpired(string projectileId)
        {
            if (string.IsNullOrEmpty(projectileId)) return false;
            foreach (var pair in expired)
                if (pair.Value == projectileId) { expired.Remove(pair.Key); return true; }
            return false;
        }

        private void ForgetExpired(string projectileId)
        {
            if (string.IsNullOrEmpty(projectileId)) return;
            long found = 0;
            foreach (var pair in expired) if (pair.Value == projectileId) { found = pair.Key; break; }
            if (found != 0) expired.Remove(found);
        }

        public void Clear()
        {
            projectiles.Clear(); presented.Clear(); expired.Clear();
        }
    }
}
