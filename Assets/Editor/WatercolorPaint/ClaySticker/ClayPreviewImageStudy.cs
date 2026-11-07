using System;
using System.Collections.Generic;
using System.IO;
using Hapio.Experiments;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public static class ClayPreviewImageStudy
    {
        [Serializable] public sealed class Report
        {
            public string unity, device;
            public string[] columns = { "No imported texture", "Fine clay", "Hand-made", "Fine fiber" };
            public string full = "Full gourd; scale1/contrast.5/relief.2; thickness.014/gloss.08; same original palette/artwork/light/view.";
            public string detail = "Intentional enlarged surface crop; scale.25/contrast1/relief1 for detail inspection; not the app defaults.";
            public string[] paths;
        }
        [MenuItem("C6 Lab/Experiments/Clay Images/Export Sample Comparison")]
        public static void ExportMenu() => Debug.Log("CLAY_IMAGE_STUDY " + Export());
        public static string Export()
        {
            ClayPreviewImageExperiment.ValidatePrepared();
            string[] samples = { "FineMatteClay", "HandKneadedClay", "FineHanjiFiber" };
            var imports = new List<ClayPreviewImageImport.Result>();
            var images = new List<Texture2D>();
            var report = new Report { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceType.ToString(),
                paths = new[] { "docs/images/clay-image-samples-full.png", "docs/images/clay-image-samples-detail.png" } };
            var metadata = new FileInfo(ClayPreviewImageExperiment.ManifestPath);
            string before = File.ReadAllText(metadata.FullName);
            try
            {
                foreach (string key in samples)
                {
                    var loaded = ClayPreviewImageImport.Load(ClayPreviewImageExperiment.Root + "/Sources/" + key + ".png", ClayPreviewImageImport.Purpose.SurfaceTexture);
                    if (!loaded.Succeeded) throw new InvalidOperationException(loaded.Error);
                    imports.Add(loaded);
                }
                Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(StickerCoatingExperiment.Folder + "/Color.png");
                Texture2D height = AssetDatabase.LoadAssetAtPath<Texture2D>(StickerCoatingExperiment.Folder + "/Height.exr");
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(StickerCoatingExperiment.SurfaceShaderPath);
                for (int row = 0; row < 2; row++)
                {
                    images.Clear();
                    for (int i = 0; i < 4; i++)
                    {
                        Texture texture = i == 0 ? null : imports[i - 1].Texture;
                        float scale = row == 0 ? 1f : .25f, strength = row == 0 ? .5f : 1f, relief = row == 0 ? .2f : 1f;
                        using (var renderer = new LabPaintStudyRenderer(true, shader, material =>
                        {
                            material.SetTexture("_HeightMap", height);
                            material.SetFloat("_ClayThickness", .014f); material.SetFloat("_ClayGloss", .08f);
                            material.SetFloat("_ClayContactStrength", .15f); material.SetFloat("_ClayDetailStrength", 1f);
                            material.SetFloat("_SpecularOnly", 0f);
                            Quaternion rotation = Quaternion.Euler(ClayPreviewController.DefaultLightElevation, ClayPreviewController.DefaultLightAzimuth - 180f, 0f);
                            Vector3 direction = -(rotation * Vector3.forward);
                            material.SetFloat("_ClayPreviewLighting", 1f);
                            material.SetVector("_ClayPreviewLightDirection", new Vector4(direction.x, direction.y, direction.z, 0f));
                            material.SetFloat("_ClayPreviewLightIntensity", 1f);
                            ClayPreviewImagePresentation.ApplyStickerTexture(material, texture, texture != null, scale, strength, relief);
                        }))
                            images.Add(renderer.RenderVisual(color, 0, row == 0 ? .72f : 1.45f));
                    }
                    Texture2D sheet = null;
                    try
                    {
                        sheet = LabPaintStudyRenderer.Combine(images.ToArray());
                        Directory.CreateDirectory(Path.GetDirectoryName(report.paths[row]));
                        File.WriteAllBytes(report.paths[row], sheet.EncodeToPNG());
                    }
                    finally
                    {
                        if (sheet != null) Object.DestroyImmediate(sheet);
                        foreach (Texture2D image in images) Object.DestroyImmediate(image);
                        images.Clear();
                    }
                }
                if (before != File.ReadAllText(metadata.FullName)) throw new InvalidOperationException("The sample study changed frozen manifest data.");
                string json = JsonUtility.ToJson(report, true);
                Directory.CreateDirectory("Temp/clay-image-study"); File.WriteAllText("Temp/clay-image-study/Report.json", json);
                return json;
            }
            finally
            {
                foreach (Texture2D image in images) if (image != null) Object.DestroyImmediate(image);
                foreach (var imported in imports) imported.Dispose();
            }
        }
    }
}
