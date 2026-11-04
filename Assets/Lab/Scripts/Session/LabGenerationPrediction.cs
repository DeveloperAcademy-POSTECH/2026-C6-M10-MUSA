using System;

namespace C6Lab
{
    /// <summary>
    /// Whether a Guest's drum tap is certain to be approved, so its sound and vibration can play before the receipt (#457).
    /// The checks mirror LabModel.TryGenerate with a safety margin. The snapshot's stamina must cover every pending
    /// generate (the Host's stamina can only be higher, it recovers over time). Stored orbs plus pending generates must
    /// leave room for <paramref name="arrivalReserve"/> orbs the Host may add before it reaches this tap: passes from both
    /// neighbours, a fox fire drop, a five-colour bonus, items this player is opening. Anything uncertain waits for the
    /// receipt as before.
    /// </summary>
    public static class LabGenerationPrediction
    {
        /// <param name="pendingGenerates">Unanswered generate requests including this tap.</param>
        /// <param name="stored">Own orbs on the board that the Host counts (not in flight, not a jigwi supply).</param>
        public static bool Certain(float stamina, float cost, int pendingGenerates, int stored, int storageLimit,
            int arrivalReserve, bool playing, bool held, bool jigwiRisk)
        {
            if (!playing || held || jigwiRisk) return false;
            if (pendingGenerates < 1 || stored < 0 || storageLimit < 1 || arrivalReserve < 0) return false;
            if (float.IsNaN(stamina) || float.IsNaN(cost) || cost <= 0f) return false;
            if (stamina + .00001f < cost * pendingGenerates) return false;
            return stored + pendingGenerates + arrivalReserve <= storageLimit;
        }

        /// <summary>Orbs the Host may add to a board before an unseen tap: two neighbours, fox fire, a five-colour bonus, item opens.</summary>
        public static int ArrivalReserve(int foxFireMax, int fiveColorCount, int pendingItemOpens)
            => 2 + Math.Max(0, foxFireMax) + Math.Max(0, fiveColorCount) + Math.Max(0, pendingItemOpens);
    }
}
