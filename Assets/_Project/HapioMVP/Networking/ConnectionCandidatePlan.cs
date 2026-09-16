using System;
using System.Collections.Generic;

namespace C6.Prototype.Networking
{
    /// <summary>One bounded, immutable Join intent. It never changes identity or retries admission denial.</summary>
    public sealed class ConnectionCandidatePlan
    {
        public const int MaximumCandidates = 8;
        public const double TotalBudgetSeconds = 60;
        private readonly string[] addresses;
        private readonly double startedAt;
        public int Index { get; private set; }
        public int Count => addresses.Length;
        public string Current => addresses[Index];
        public bool HasNext => Index + 1 < Count;
        private ConnectionCandidatePlan(string[] values, double now) { addresses = values; startedAt = now; }

        public static bool TryCreate(IEnumerable<string> values, double now, out ConnectionCandidatePlan plan)
        {
            plan = null;
            if (values == null || !Finite(now) || now < 0) return false;
            var normalized = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            int inspected = 0;
            foreach (string value in values)
            {
                if (++inspected > MaximumCandidates) return false;
                if (!DirectConnectionValidation.TryParseAddress(value, out string address)) return false;
                if (unique.Add(address)) normalized.Add(address);
            }
            if (normalized.Count == 0) return false;
            plan = new ConnectionCandidatePlan(normalized.ToArray(), now);
            return true;
        }
        public bool BudgetExpired(double now) => !Finite(now) || now < startedAt || now - startedAt >= TotalBudgetSeconds;
        public bool CanRetry(bool connected, string approvalReason, double now) => !connected
            && string.IsNullOrEmpty(approvalReason) && HasNext && !BudgetExpired(now);
        public bool MoveNext(double now)
        {
            if (!HasNext || BudgetExpired(now)) return false;
            Index++;
            return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
