using System;
using System.Collections.Generic;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        // Blast source survives the bomb's removal until the holder's bounded report arrives.
        private readonly HashSet<string> jigwiGourdBlastIds = new HashSet<string>(StringComparer.Ordinal);
        private void TrackJigwiGourdBlast(LabOrbState bomb)
        {
            if (JigwiStageEnabled && bomb.origin == LabOrbOrigin.Gourd)
                jigwiGourdBlastIds.Add(bomb.id);
        }

        private void ClearJigwiGourdBlastEffects()
        {
            foreach (string id in jigwiGourdBlastIds) unreportedBlasts.Remove(id);
            foreach (LabOrbState orb in orbs.Values)
            {
                // A launched projectile keeps its approved radius and physical trajectory.
                if (orb.inFlight || orb.gourdBlastBurnedAt < 0d) continue;
                if (orb.gourdBlastOnlySmall) orb.small = false;
                orb.burnedAt = orb.ordinaryBlastBurnedAt;
                orb.gourdBlastOnlySmall = false;
                orb.gourdBlastBurnedAt = -1d;
                orb.revision = ++revision;
            }
            jigwiGourdBlastIds.Clear();
        }
    }
}
