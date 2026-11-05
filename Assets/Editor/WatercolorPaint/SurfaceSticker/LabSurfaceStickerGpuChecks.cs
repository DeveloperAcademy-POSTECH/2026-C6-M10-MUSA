using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Small shader-contract probe. A command buffer draws real geometry through pass 0 into
    /// a private linear target; no Scene, prefab, importer, camera, asset or game is modified.
    /// This does not verify the final gourd artwork, physical device cost or live-game wiring.
    /// </summary>
    public static class LabSurfaceStickerGpuChecks
    {
        [Serializable]
        public sealed class Result
        {
            public string gpu, unity;
            public int passed;
            public string[] checks;
            public int maximumJointAlphaError;
            public int outsideAlpha;
            public int redInteriorAlpha, blueInteriorAlpha;
            public int assetOutsideAlpha, assetInsideAlpha;
        }

        public static Result Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run the GPU probe in the stopped, compiled Editor.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A real graphics device is required.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LabSurfaceStickerPrototype.Folder + "/SurfaceSticker.shader");
            if (shader == null || !shader.isSupported || ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"))
                throw new InvalidOperationException("The surface-sticker shader is missing or unsupported.");
            var checks = new List<string>();
            var resources = new List<Object>();
            var result = new Result { gpu = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion };
            Matrix4x4 savedView = Shader.GetGlobalMatrix("unity_MatrixV");
            Matrix4x4 savedProjection = Shader.GetGlobalMatrix("unity_MatrixP");
            RenderTexture oldTarget = RenderTexture.active;
            try
            {
                var rt = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "Surface sticker contract probe", hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                resources.Add(rt);
                Require(rt.Create(), "Cannot create the private shader target.");
                var plane = new Mesh { name = "Surface sticker probe quad", hideFlags = HideFlags.HideAndDontSave };
                resources.Add(plane);
                plane.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
                plane.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
                plane.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                plane.uv2 = plane.uv;
                plane.colors = Enumerable.Repeat(Color.white, 4).ToArray();
                // Both winding orders let the actual Cull Back shader select one side
                // across APIs whose render-target projection flips handedness.
                plane.triangles = new[] { 0,2,1,0,3,2, 0,1,2,0,2,3 };
                plane.RecalculateBounds();

                Texture2D white = Texture(1, 1, new[] { new Color32(255,255,255,255) }); resources.Add(white);
                Texture2D redMask = Texture(4, 2, RepeatRow(new byte[] { 0,255,0,0 }, false)); resources.Add(redMask);
                Texture2D blueMask = Texture(4, 2, RepeatRow(new byte[] { 0,0,255,0 }, false)); resources.Add(blueMask);
                Texture2D top = Texture(4, 2, RepeatRow(new byte[] { 0,1,2,0 }, true)); resources.Add(top);
                var red = Material(shader, white, redMask, top, Color.red, 1); resources.Add(red);
                var blue = Material(shader, white, blueMask, top, Color.blue, 2); resources.Add(blue);

                Color32[] frame = Draw(rt, plane, new[] { red, blue });
                Color32 outside = Pixel(frame, 8, 64), left = Pixel(frame, 48, 64), right = Pixel(frame, 80, 64);
                result.outsideAlpha = outside.a; result.redInteriorAlpha = left.a; result.blueInteriorAlpha = right.a;
                Require(outside.a == 0, "Unpaid outside pixels were rendered."); checks.Add("unpaid outside remains transparent");
                Require(left.a == 255 && left.r >= 254 && left.g <= 1 && left.b <= 1,
                    "Red interior is translucent or contaminated by lower color: left=" + left + ", right=" + right);
                Require(right.a == 255 && right.b >= 254 && right.g <= 1 && right.r <= 1,
                    "Blue interior is translucent or contaminated by lower color."); checks.Add("paid interiors are opaque and retain asset color");
                for (int x = 57; x <= 70; x++)
                {
                    Color32 pixel = Pixel(frame, x, 64);
                    result.maximumJointAlphaError = Math.Max(result.maximumJointAlphaError, 255 - pixel.a);
                    Require(pixel.a == 255, "A transparent seam appeared between touching stickers at x=" + x);
                    Require((pixel.r >= 254 && pixel.b <= 1) || (pixel.b >= 254 && pixel.r <= 1),
                        "Touching categorical stickers mixed RGB at x=" + x);
                }
                checks.Add("categorical joint has no alpha crack or RGB mix");

                Color32[] reversed = Draw(rt, plane, new[] { blue, red });
                Require(frame.SequenceEqual(reversed), "Categorical top ownership changed with renderer submission order.");
                checks.Add("top sticker wins independently of submission order");

                Texture2D asset = Texture(4, 1, new[] {
                    new Color32(255,255,255,0), new Color32(255,255,255,255),
                    new Color32(255,255,255,255), new Color32(255,255,255,0)
                }); resources.Add(asset);
                var raw = Material(shader, asset, white, top, Color.blue, 1); resources.Add(raw);
                raw.SetFloat("_UseAssetAlpha", 1); raw.SetFloat("_UseCoverage", 0); raw.SetFloat("_UseTopOwner", 0);
                Color32[] rawFrame = Draw(rt, plane, new[] { raw });
                result.assetOutsideAlpha = Pixel(rawFrame, 8, 64).a; result.assetInsideAlpha = Pixel(rawFrame, 64, 64).a;
                Require(result.assetOutsideAlpha == 0 && result.assetInsideAlpha == 255,
                    "Original RGBA silhouette alpha was not respected.");
                checks.Add("raw asset alpha cuts the background while retaining opaque interior");

                CheckGeometryGuards(checks);
                result.checks = checks.ToArray(); result.passed = checks.Count;
                return result;
            }
            finally
            {
                RenderTexture.active = oldTarget;
                using (var restore = new CommandBuffer { name = "Restore sticker probe matrices" })
                { restore.SetViewProjectionMatrices(savedView, savedProjection); Graphics.ExecuteCommandBuffer(restore); }
                for (int i = resources.Count - 1; i >= 0; i--)
                    if (resources[i] != null) Object.DestroyImmediate(resources[i]);
            }
        }

        private static void CheckGeometryGuards(List<string> checks)
        {
            var stamp = new LabPaintVisualStamp { center = Vector3.forward, right = Vector3.right, radius = .3f };
            bool empty = false, invalidCell = false, invalidShape = false;
            try { LabSurfaceStickerMesh.Fit(stamp, Array.Empty<int>(), 256, 128); } catch (ArgumentException) { empty = true; }
            try { LabSurfaceStickerMesh.Fit(stamp, new[] { 256 * 128 }, 256, 128); } catch (ArgumentOutOfRangeException) { invalidCell = true; }
            try { LabSurfaceStickerMesh.Fit(stamp, new[] { 0 }, 1, 128); } catch (ArgumentOutOfRangeException) { invalidShape = true; }
            Require(empty && invalidCell && invalidShape, "Invalid paid footprints were not rejected.");
            checks.Add("geometry rejects empty, out-of-range and invalid-size footprints");
            Vector3 p = new Vector3(.4f, .3f, 1f);
            Require((LabSurfaceStickerMesh.AuthorityUV(p) - LabSurfaceStickerMesh.AuthorityUV(p * 3f)).sqrMagnitude < 1e-10f,
                "Authority UV depends on body radius rather than direction.");
            Vector2 seamA = LabSurfaceStickerMesh.AuthorityUV(new Vector3(-1,0,.00001f));
            Vector2 seamB = LabSurfaceStickerMesh.AuthorityUV(new Vector3(-1,0,-.00001f));
            Require(Mathf.Abs(seamA.x - seamB.x) > .99f && Mathf.Abs(seamA.y - seamB.y) < 1e-6f,
                "Longitude wrap is not represented at the authority seam.");
            checks.Add("geometry uses radius-independent, wrapped body-local authority coordinates");
        }

        private static Material Material(Shader shader, Texture sticker, Texture mask, Texture top, Color tint, int layer)
        {
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture("_StickerTex", sticker); material.SetTexture("_CoverageTex", mask); material.SetTexture("_TopIdTex", top);
            material.SetVector("_Tint", new Vector4(tint.r,tint.g,tint.b,1)); material.SetFloat("_TintFromLuminance", 0);
            material.SetFloat("_UseAssetAlpha", 0); material.SetFloat("_UseCoverage", 1); material.SetFloat("_UseTopOwner", 1);
            material.SetFloat("_LayerIndex", layer); material.SetFloat("_SurfaceCoordinatesFromPosition", 0);
            material.SetFloat("_ShadowStrength", 0); material.SetFloat("_PaperLightStrength", 0);
            return material;
        }

        private static Texture2D Texture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
        }

        private static Color32[] RepeatRow(byte[] values, bool ids)
        {
            var pixels = new Color32[values.Length * 2];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = ids ? new Color32(values[i % values.Length],0,0,255)
                    : new Color32(255,255,255,values[i % values.Length]);
            return pixels;
        }

        private static Color32[] Draw(RenderTexture target, Mesh plane, Material[] materials)
        {
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1,1,-1))
                * Matrix4x4.TRS(new Vector3(0,0,-2), Quaternion.identity, Vector3.one).inverse;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,.1f,10f), true);
            using (var buffer = new CommandBuffer { name = "Surface sticker GPU contract probe" })
            {
                buffer.SetRenderTarget(target); buffer.ClearRenderTarget(false, true, Color.clear);
                buffer.SetViewport(new Rect(0,0,target.width,target.height));
                buffer.SetViewProjectionMatrices(view, projection);
                foreach (Material material in materials) buffer.DrawMesh(plane, Matrix4x4.identity, material, 0, 0);
                Graphics.ExecuteCommandBuffer(buffer);
            }
            var readback = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true)
                { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0); readback.Apply(false,false);
                return readback.GetPixels32();
            }
            finally { Object.DestroyImmediate(readback); }
        }
        private static Color32 Pixel(Color32[] frame, int x, int y) => frame[y * 128 + x];
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
