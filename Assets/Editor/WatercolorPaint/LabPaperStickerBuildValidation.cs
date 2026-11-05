using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Read-only build gate. Export never silently rebakes a round's artwork.</summary>
    public sealed class LabPaperStickerBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -84;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!LabWatercolorStampSettings.CaptureDefault().UsesPaperStickers) return;
            try
            {
                LabPaperStickerBaker.Validate();
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(LabPaperStickerBaker.OutputDirectory + "/OpaquePaperStickerRuntime.shader") == null)
                    throw new InvalidOperationException("The opaque paper sticker shader is missing.");
            }
            catch (Exception exception)
            { throw new BuildFailedException("Opaque paper sticker data is not ready: " + exception.Message); }
        }
    }
}
