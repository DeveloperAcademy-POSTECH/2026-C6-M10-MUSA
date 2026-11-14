using UnityEditor;

namespace Hapio.Editor
{
    /// <summary>Explicit preparation of demo-only saved UI. Export remains read-only.</summary>
    public static class StaffStageDemoSetup
    {
        [MenuItem("Hapio/Demo/Prepare Staff Stage Demo")]
        public static void Prepare()
        {
            StaffDemoUiAssets.CreateOrUpdate();
            StaffStageDemoStoryAssets.CreateOrUpdate();
            AssetDatabase.SaveAssets();
            GameDemoBuild.Validate();
            UnityEngine.Debug.Log("HAPIO_STAFF_STAGE_DEMO_ASSETS_READY");
        }
    }
}
