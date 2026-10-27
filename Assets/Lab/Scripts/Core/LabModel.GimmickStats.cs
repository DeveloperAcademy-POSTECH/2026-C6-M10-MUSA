namespace C6Lab
{
    public sealed partial class LabModel
    {
        private void RecordYaguangUnique(bool isCombo, double at)
        {
            gimmickStats.AddYaguang(isCombo);
            if (lastCommonCastAt == at) gimmickStats.simultaneous++;
        }

        private void PopulateYaguangStats(LabGimmickStats stats)
        {
            if (teamMode) stats.schedulePolicy = "none";
            else if (rules != null && rules.YaguangTimeline)
            {
                stats.schedulePolicy = "yaguang-timetable";
                // These generic interval controls are inactive for the scheduled stage.
                stats.commonInterval = stats.uniqueInterval = 0f;
                stats.timetableEndSeconds = (float)rules.YaguangTimelineEndSeconds;
            }
        }
    }
}
