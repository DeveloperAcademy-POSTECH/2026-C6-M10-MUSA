using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C6Lab
{
    /// <summary>Wire identity of B-only water. Legacy/manual supply has no B identity.</summary>
    public static class LabJigwiSupplyIdentity
    {
        public static bool ValidEvent(string eventId, string session, uint round)
        {
            if (string.IsNullOrEmpty(session) || round == 0 || string.IsNullOrEmpty(eventId) || eventId.Length > 256) return false;
            string prefix = "jigwi-ab/" + session + "/" + round.ToString(CultureInfo.InvariantCulture) + "/";
            if (!eventId.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string suffix = eventId.Substring(prefix.Length);
            return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int slot)
                && slot >= 1 && slot <= 5 && slot % 2 == 1 && suffix == slot.ToString(CultureInfo.InvariantCulture);
        }

        public static bool Valid(LabOrbState state, string session, uint round, LabPlayerState[] members = null)
        {
            if (state == null) return false;
            if (string.IsNullOrEmpty(state.jigwiABSupplyEventId)) return true;
            return ValidEvent(state.jigwiABSupplyEventId, session, round)
                && state.kind == LabOrbKind.Combined && state.element == LabOrbElement.Water
                && state.jigwiSupply && state.origin == LabOrbOrigin.Gourd && state.originOwner != ulong.MaxValue
                && (members == null || members.Any(player => player != null && player.id == state.originOwner));
        }
    }

    /// <summary>At most three completed B identities, scoped to the current session/round.
    /// Retiring supplied water never changes normal orbs, player stamina, stream sequence or paint.</summary>
    public sealed class LabJigwiSupplyRetirementGate
    {
        private readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        private string session;
        private uint round;
        public int Count => retired.Count;

        public void SetActive(string nextSession, uint nextRound)
        {
            if (session == nextSession && round == nextRound) return;
            session = nextSession; round = nextRound; retired.Clear();
        }
        public bool TryRetire(string sourceSession, uint sourceRound, string eventId) =>
            session == sourceSession && round == sourceRound && LabJigwiSupplyIdentity.ValidEvent(eventId, session, round)
                && retired.Add(eventId);
        public bool Cancels(string eventId) => eventId != null && retired.Contains(eventId);
        public bool Cancels(LabOrbState state) => state != null && Cancels(state.jigwiABSupplyEventId);

        public bool Observe(string sourceSession, uint sourceRound, LabJigwiABSnapshot state, LabPhase phase)
        {
            if (session != sourceSession || round != sourceRound || state == null || !state.enabled) return false;
            bool changed = false;
            foreach (LabJigwiABRound item in state.rounds ?? Array.Empty<LabJigwiABRound>())
                if (item != null && item.kind == LabJigwiABKind.GroupFire
                    && item.resolution != LabJigwiABResolution.None)
                    changed |= TryRetire(sourceSession, sourceRound, item.id);
            // A terminal battle snapshot can precede that round's final AB metadata.
            if (phase == LabPhase.Victory || phase == LabPhase.Defeat || state.fever)
                for (int slot = 1; slot <= 5; slot += 2)
                    changed |= TryRetire(sourceSession, sourceRound,
                        "jigwi-ab/" + session + "/" + round.ToString(CultureInfo.InvariantCulture) + "/" + slot);
            return changed;
        }
    }
}
