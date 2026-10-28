namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        private LabJigwiStagePresentation jigwiStagePresentation;

        // #341 replaces the old paint-surface A/B policy. Keep these call-site names as
        // harmless compatibility seams; never calibrate roots, block paint or create old art.
        private bool CalibrateJigwiStageGeometry(LabSnapshot snapshot, double now) => false;

        private void SyncJigwiStagePresentation()
        {
            if (jigwiStagePresentation == null) jigwiStagePresentation = GetComponent<LabJigwiStagePresentation>();
            if (jigwiStagePresentation != null && jigwiStagePresentation.VisibleGroups > 0)
                jigwiStagePresentation.ClearMarks();
        }
    }
}
