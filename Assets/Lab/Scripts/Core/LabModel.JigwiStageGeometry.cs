using System.Linq;

namespace C6Lab
{
    public sealed partial class LabModel
    {
        /// <summary>A Host raycast found no reachable, useful surface for this preset.</summary>
        public bool RejectJigwiStageGeometry(string groupId, double now)
        {
            if (!JigwiStageEnabled || !TryAdvance(now, out _) || phase != LabPhase.Playing) return false;
            var group = jigwiStageGroups.FirstOrDefault(g => g.id == groupId);
            if (group == null || group.resolution != LabJigwiStageResolution.None) return false;
            ResolveJigwiStageGroup(group, LabJigwiStageResolution.Cancelled, now);
            UpdateJigwiStagePaintBlocker();
            return true;
        }
    }
}
