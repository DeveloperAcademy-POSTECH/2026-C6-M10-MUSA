using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public sealed class LabHighResolutionPaintBaker : IDisposable
    {
        public const string MaskPath = "Assets/Lab/Resources/GourdPaint/Patterns/Splatter.png";
        private readonly Texture2D brush;
        private readonly Material material;
        public readonly LabPaintStudyMask Mask;
        private bool disposed;

        public LabHighResolutionPaintBaker()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/HighResolutionPaint/LabHighResolutionPaint.shader");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("High-resolution paint shader unavailable.");
            brush = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                if (!brush.LoadImage(File.ReadAllBytes(MaskPath), false)) throw new InvalidOperationException("Splatter PNG could not be read.");
                Mask = new LabPaintStudyMask(brush.width, brush.height, brush.GetPixels32());
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetTexture("_Brush", brush);
            }
            catch { Dispose(); throw; }
        }

        public RenderTexture Bake(LabHighResolutionPaintFixture fixture, int stage, int columns,
            float density = .82f, float mottle = .22f, float grain = .25f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabHighResolutionPaintBaker));
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            if (stage < 0 || stage > 3 || (columns != 1024 && columns != 2048)) throw new ArgumentOutOfRangeException();
            var result = new RenderTexture(columns, columns / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                name = "High-resolution visual fixture", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false
            };
            var commands = new CommandBuffer { name = "Offline gourd brush accumulation" };
            try
            {
                if (!result.Create()) throw new InvalidOperationException("Could not allocate comparison paint texture.");
                commands.SetRenderTarget(result);
                commands.ClearRenderTarget(false, true, Color.clear);
                for (int i = 0; i <= stage; i++)
                {
                    var stamp = fixture.Stamps[i];
                    var block = new MaterialPropertyBlock();
                    block.SetVector("_Center", stamp.Center); block.SetVector("_Right", stamp.Right); block.SetVector("_Up", stamp.Up);
                    block.SetFloat("_Radius", stamp.Radius);
                    // ColorFor already returns linear pigment. SetColor would convert it again
                    // in a Linear project, so pass raw vector channels to this data shader.
                    block.SetVector("_Ink", new Vector4(stamp.Color.r, stamp.Color.g, stamp.Color.b, stamp.Color.a));
                    block.SetFloat("_Density", Mathf.Clamp01(density)); block.SetFloat("_Mottle", Mathf.Clamp01(mottle));
                    block.SetFloat("_Grain", Mathf.Clamp01(grain));
                    commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, block);
                }
                Graphics.ExecuteCommandBuffer(commands);
                return result;
            }
            catch { result.Release(); Object.DestroyImmediate(result); throw; }
            finally { commands.Release(); }
        }

        public static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); Object.DestroyImmediate(texture);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (material != null) Object.DestroyImmediate(material);
            if (brush != null) Object.DestroyImmediate(brush);
        }
    }
}
