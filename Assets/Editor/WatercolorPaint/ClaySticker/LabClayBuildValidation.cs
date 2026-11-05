using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Read-only guard. An export never silently regenerates the approved artwork.</summary>
    public sealed class LabClayBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -90;
        public void OnPreprocessBuild(BuildReport report)
        {
            var settings=LabWatercolorStampSettings.CaptureDefault();
            if(!settings.enabled || !settings.UsesClayStickers)return;
            Validate();
        }
        public static void Validate()
        {
            string directory=LabClayStickerBaker.Folder;
            using(var sha=SHA256.Create())using(var stream=new MemoryStream())
            {
                byte[] prefix=System.Text.Encoding.UTF8.GetBytes("clay-data-v1-alpha-distance-rounded-height");stream.Write(prefix,0,prefix.Length);
                foreach(string name in new[]{"Broad","Long","Cluster"})
                {
                    string path=directory+"/"+name+"Data.png";
                    if(!File.Exists(path))throw new BuildFailedException("Missing clay asset: "+path+". Bake Clay Data before exporting.");
                    byte[] bytes=File.ReadAllBytes(path);stream.Write(bytes,0,bytes.Length);
                }
                string expected=string.Concat(sha.ComputeHash(stream.ToArray()).Select(b=>b.ToString("x2")));
                string pathVersion=directory+"/Version.txt";
                if(!File.Exists(pathVersion) || File.ReadAllText(pathVersion).Trim()!=expected
                    || LabClayStickerSettings.BakedBrushVersion!=expected)
                    throw new BuildFailedException("The approved clay data version differs. Re-bake and verify before exporting.");
            }
        }
    }
}
