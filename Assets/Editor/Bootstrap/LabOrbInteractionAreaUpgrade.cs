namespace C6Lab.Editor
{
    /// <summary>Preserves the previous menu entry names without restoring the obsolete rectangle.</summary>
    public static class LabOrbInteractionAreaUpgrade
    {
        [UnityEditor.MenuItem("C6 Lab/Fit Orb Interaction Area")]
        public static void Apply() => LabCurvedOrbAreaUpgrade.Apply();

        [UnityEditor.MenuItem("C6 Lab/Validate Orb Interaction Area")]
        public static void ValidateCurrentScene() => LabCurvedOrbAreaUpgrade.ValidateCurrentScene();
    }
}
