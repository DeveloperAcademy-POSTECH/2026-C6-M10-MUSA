using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Figma composites these fixed backgrounds over white in sRGB, before Unity's linear UI blend.</summary>
    public static class OpeningBackgroundBaker
    {
        public static void Bake(OpeningRevisionBuilder.Manifest manifest)
        {
            var written = new HashSet<string>();
            foreach (var frame in manifest.frames)
            foreach (var item in frame.items)
            {
                if (!item.precompositedBackground || !written.Add(item.path)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(item.backgroundSourcePath)))
                        throw new InvalidDataException(item.backgroundSourcePath);
                    var pixels = texture.GetPixels32();
                    // Filtered Figma exports already contain paint opacity; raw fills do not.
                    float opacity = item.opacityInSource ? 1f : item.figmaOpacity;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color32 p = pixels[i];
                        float a = p.a / 255f * opacity;
                        pixels[i] = new Color32(
                            (byte)Mathf.RoundToInt(p.r * a + 255 * (1 - a)),
                            (byte)Mathf.RoundToInt(p.g * a + 255 * (1 - a)),
                            (byte)Mathf.RoundToInt(p.b * a + 255 * (1 - a)), 255);
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply(false, false);
                    Directory.CreateDirectory(Path.GetDirectoryName(item.path));
                    File.WriteAllBytes(item.path, ImageConversion.EncodeToPNG(texture));
                    AssetDatabase.ImportAsset(item.path, ImportAssetOptions.ForceSynchronousImport);
                }
                finally { Object.DestroyImmediate(texture); }
            }
        }
    }
}
