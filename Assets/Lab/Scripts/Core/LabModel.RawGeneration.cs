using System.Collections.Generic;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        private const uint RawStreakDrawSalt = 0x2A75A324u;
        // Actual raw generation belongs to the player, even when drums or orbs change owners.
        private readonly Dictionary<ulong, RawGenerationStreak> rawGenerationStreaks =
            new Dictionary<ulong, RawGenerationStreak>();

        private readonly struct RawGenerationStreak
        {
            public readonly LabOrbKind Kind;
            public readonly int Count;

            public RawGenerationStreak(LabOrbKind kind, int count)
            {
                Kind = kind;
                Count = count;
            }
        }

        private LabOrbKind CorrectRawGenerationKind(LabOrbKind kind, ulong playerId, uint generationIndex)
        {
            // The weighted draw chooses raw versus item first. Never revive a zero-weight polarity.
            if (!rules.RawStreakCorrectionEnabled || !kind.IsRaw()
                || rules.GenerationChances[0] <= 0 || rules.GenerationChances[1] <= 0
                || !rawGenerationStreaks.TryGetValue(playerId, out var streak)) return kind;

            LabOrbKind opposite = streak.Kind == LabOrbKind.Yin ? LabOrbKind.Yang : LabOrbKind.Yin;
            // A maximum of one alternates; a maximum of two takes priority over the soft rule.
            if (streak.Count >= rules.RawStreakMaximum) return opposite;
            if (streak.Count < 2) return kind;

            int roll = (int)(DrawFor(seed ^ RawStreakDrawSalt, playerId, generationIndex) % 100UL);
            return roll < rules.RawStreakOppositeChancePercent ? opposite : streak.Kind;
        }

        private void RecordRawGeneration(ulong playerId, LabOrbKind kind)
        {
            // Failed/burnt attempts and fever's Combined results never reach raw history.
            // Items also leave the preceding raw run intact.
            if (!kind.IsRaw()) return;
            int count = 1;
            if (rawGenerationStreaks.TryGetValue(playerId, out var previous) && previous.Kind == kind)
                count = previous.Count < rules.RawStreakMaximum ? previous.Count + 1 : rules.RawStreakMaximum;
            rawGenerationStreaks[playerId] = new RawGenerationStreak(kind, count);
        }
    }
}
