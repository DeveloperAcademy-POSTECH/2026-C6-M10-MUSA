using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Independent GPU checks for the V3 material; no baked preset or game round is needed.</summary>
    public static class RoundedClaySurfaceChecks
    {
        const string ShaderPath = "Assets/Experiments/ClayPreview/Runtime/Shaders/RoundedClayStickerSurface.shader";
        const int Columns = 1024, Rows = 512, RenderSize = 512;
        [Serializable] public sealed class Report
        {
            public string unity, graphicsDevice, timingNote;
            public int checksPassed, capturedFrames, fringeBluePixels, matteHeightChangedPixels;
            public float undercoatMaximumDifference, fringeMaximumDifference, zeroThicknessMaximumDifference;
            public float maximumRenderedChannel, minimumContactChannel;
            public double wallMillisecondsIncludingReadback;
            public string[] checks;
        }

        [MenuItem("C6 Lab/Experiments/Rounded Clay/Run Surface GPU Checks")]
        static void RunMenu() => UnityEngine.Debug.Log(Run());

        public static string Run()
        {
            Require(!EditorApplication.isPlaying && !EditorApplication.isCompiling,
                "Stop Play mode and finish compilation before running the rounded-clay checks.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
                && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat),
                "A real graphics device with a floating-point render target is required.");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Require(shader != null && shader.isSupported, "The rounded-clay experiment shader is unavailable.");
            Require(!ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"),
                "The rounded-clay experiment shader has compilation errors.");
            var timer = Stopwatch.StartNew();
            var report = new Report
            {
                unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                timingNote = "Wall time includes scene preparation, GPU rendering and synchronous CPU readback; it is not GPU execution time."
            };
            var checks = new List<string>();
            Texture2D paint = null, height = null;
            try
            {
                ConstantMaps(out paint, out height);
                using (var preview = new FloatPreview(shader, paint, height))
                {
                    Color[] matte = preview.Render(.014f, 0, .15f, 1);
                    Color[] glossy = preview.Render(.014f, 1, .15f, 1);
                    report.capturedFrames += 2;
                    report.undercoatMaximumDifference = Difference(matte, glossy);
                    Require(report.undercoatMaximumDifference <= .00001f,
                        "Completed undercoat acquired gloss despite zero real-sticker coverage.");
                    checks.Add("completed raw-A-zero undercoat is identical at gloss zero and one on the actual gourd");
                }
                DestroyMaps(ref paint, ref height);

                BandedMaps(out paint, out height);
                using (var preview = new FloatPreview(shader, paint, height))
                {
                    Color[] matte = preview.Render(.014f, 0, .15f, 1);
                    Color[] glossy = preview.Render(.014f, 1, .15f, 1);
                    report.capturedFrames += 2;
                    float fringeDifference = 0;
                    for (int i = 0; i < matte.Length; i++)
                    {
                        // The color fixture puts a two-texel red guard outside each
                        // real sticker. Pure blue therefore excludes allowed bilinear
                        // coverage AA but includes the five-tap normal-filter fringe.
                        if (!Blue(matte[i])) continue;
                        report.fringeBluePixels++;
                        fringeDifference = Mathf.Max(fringeDifference, Delta(matte[i], glossy[i]));
                    }
                    report.fringeMaximumDifference = fringeDifference;
                    Require(report.fringeBluePixels > 500 && fringeDifference <= .00001f,
                        "Filtered sticker normals leaked gloss onto adjacent raw-A-zero blue undercoat.");
                    checks.Add("normal-filter fringes do not extend gloss beyond actual coverage, including adjacent undercoat");

                    Color[] flatOff = preview.Render(0, .08f, 0, 0);
                    Color[] flatOn = preview.Render(0, .08f, .5f, 1);
                    report.capturedFrames += 2;
                    report.zeroThicknessMaximumDifference = Difference(flatOff, flatOn);
                    Require(report.zeroThicknessMaximumDifference <= .00001f,
                        "Contact or microdetail changed the image at zero thickness.");
                    checks.Add("zero thickness disables added contact shade and clay microdetail exactly");

                    Color[] flatMatte = preview.Render(0, 0, .15f, 0);
                    Color[] raisedMatte = preview.Render(.014f, 0, .15f, 0);
                    report.capturedFrames += 2;
                    for (int i = 0; i < flatMatte.Length; i++)
                        if (Colored(flatMatte[i]) && Colored(raisedMatte[i]) && Delta(flatMatte[i], raisedMatte[i]) > .003f)
                            report.matteHeightChangedPixels++;
                    Require(report.matteHeightChangedPixels > 100,
                        "Height/contact changes were not visible inside the colored gourd when gloss was zero.");
                    checks.Add("matte raised stickers remain visibly different from flat paint without any gloss contribution");

                    Color[] noContact = preview.Render(.02f, 0, 0, 1);
                    Color[] maximumContact = preview.Render(.02f, 0, .5f, 1);
                    report.capturedFrames += 2;
                    float minimum = float.PositiveInfinity;
                    int compared = 0, darkened = 0;
                    for (int i = 0; i < noContact.Length; i++)
                    {
                        // Select from the unshaded fixture only: a broken black
                        // result must remain in the comparison instead of being filtered out.
                        if (!FixturePaint(noContact[i])) continue;
                        compared++;
                        minimum = Mathf.Min(minimum, MaxRgb(maximumContact[i]));
                        Require(maximumContact[i].r <= noContact[i].r + .00002f
                            && maximumContact[i].g <= noContact[i].g + .00002f
                            && maximumContact[i].b <= noContact[i].b + .00002f,
                            "Contact-only adjustment brightened the clay instead of adding bounded shade.");
                        if (Delta(noContact[i], maximumContact[i]) > .003f) darkened++;
                    }
                    report.minimumContactChannel = minimum;
                    Require(compared > 500 && darkened > 100 && minimum > .08f,
                        "Maximum contact produced black outlines or did not exercise the raised boundaries.");
                    checks.Add("maximum contact/detail settings remain finite and produce bounded soft shade rather than black outlines");

                    foreach (Vector2 view in new[] { new Vector2(0,0), new Vector2(90,0), new Vector2(180,0),
                        new Vector2(-90,0), new Vector2(45,80), new Vector2(45,-80) })
                    {
                        Color[] pixels = preview.Render(.02f, 1, .5f, 1, view.x, view.y);
                        report.capturedFrames++;
                        Require(pixels.Count(Colored) > 250, "A seam/pole view did not actually show the painted gourd.");
                        foreach (Color pixel in pixels)
                            report.maximumRenderedChannel = Mathf.Max(report.maximumRenderedChannel, MaxRgb(pixel));
                    }
                    checks.Add("longitude-seam and both polar views render finite floating-point images with visible clay");
                }
                report.checks = checks.ToArray(); report.checksPassed = checks.Count;
                report.wallMillisecondsIncludingReadback = timer.Elapsed.TotalMilliseconds;
                return JsonUtility.ToJson(report, true);
            }
            finally { DestroyMaps(ref paint, ref height); }
        }

        static bool Blue(Color c) => c.b > c.r * 1.5f && c.b > c.g * 1.15f && c.b > .15f;
        static bool FixturePaint(Color c) => Blue(c) || (c.r > c.g * 2 && c.r > c.b * 2 && c.r > .15f);
        static bool Colored(Color c) => Mathf.Max(c.r, Mathf.Max(c.g,c.b)) - Mathf.Min(c.r, Mathf.Min(c.g,c.b)) > .10f;
        static float MaxRgb(Color c) => Mathf.Max(c.r, Mathf.Max(c.g,c.b));
        static float Delta(Color a, Color b) => Mathf.Max(Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g)),
            Mathf.Max(Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a)));
        static float Difference(Color[] a, Color[] b)
        { Require(a.Length == b.Length, "GPU frame sizes differ."); float result=0;for(int i=0;i<a.Length;i++)result=Mathf.Max(result,Delta(a[i],b[i]));return result; }
        static void ConstantMaps(out Texture2D paint, out Texture2D height)
        {
            paint = Map(TextureFormat.RGBA32); height = Map(TextureFormat.RGBAHalf);
            paint.SetPixels(Enumerable.Repeat(new Color(.22f,.42f,.66f,1),Columns*Rows).ToArray());
            height.SetPixels(Enumerable.Repeat(new Color(.16f,0,0,0),Columns*Rows).ToArray());
            paint.Apply(false,false);height.Apply(false,false);
        }
        static void BandedMaps(out Texture2D paint, out Texture2D height)
        {
            paint=Map(TextureFormat.RGBA32);height=Map(TextureFormat.RGBAHalf);
            paint.filterMode=FilterMode.Point;
            var colors=new Color[Columns*Rows];var relief=new Color[colors.Length];
            const int period=128,half=64,guard=2;
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
            {
                int local=x%period;bool sticker=local<half;
                bool red=sticker || local<half+guard || local>=period-guard;
                colors[y*Columns+x]=red?new Color(.74f,.22f,.17f,1):new Color(.22f,.42f,.66f,1);
                if(sticker)
                {
                    float edge=Mathf.Min(local+.5f,half-local-.5f);
                    float rounded=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/20));
                    float slope=Mathf.Clamp((local-half*.5f)/(half*.5f),-1,1)*.35f;
                    relief[y*Columns+x]=new Color(.16f+.72f*rounded,.5f+.5f*slope/.466307658f,.5f,1);
                }
                else relief[y*Columns+x]=new Color(.16f,0,0,0);
            }
            paint.SetPixels(colors);height.SetPixels(relief);paint.Apply(false,false);height.Apply(false,false);
        }
        static Texture2D Map(TextureFormat format) => new Texture2D(Columns,Rows,format,false,true)
        {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
        static void DestroyMaps(ref Texture2D paint,ref Texture2D height)
        {if(paint!=null)Object.DestroyImmediate(paint);if(height!=null)Object.DestroyImmediate(height);paint=height=null;}
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("Rounded clay GPU check: "+message);}

        /// <summary>Private actual-prefab renderer with a float target, so NaN/Inf cannot hide in an LDR PNG.</summary>
        sealed class FloatPreview : IDisposable
        {
            Scene scene;
            GameObject instance;
            MeshRenderer body;
            Camera camera;
            RenderTexture target;
            Material clay;
            readonly List<Material> materials=new List<Material>();
            public FloatPreview(Shader shader,Texture paint,Texture height)
            {
                try
                {
                    GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LabPaintStudyRenderer.PrefabPath);
                    Require(prefab!=null,"The actual saved WhiteGourd prefab is missing.");
                    scene=EditorSceneManager.NewPreviewScene();
                    instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    instance.hideFlags=HideFlags.HideAndDontSave;
                    foreach(Transform child in instance.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
                    foreach(MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Material[] copies=renderer.sharedMaterials;
                        for(int i=0;i<copies.Length;i++)
                        {
                            if(copies[i]==null)continue;
                            var copy=new Material(copies[i]){hideFlags=HideFlags.HideAndDontSave};materials.Add(copy);copies[i]=copy;
                            if(copy.shader!=null&&copy.shader.name=="C6Lab/GourdPaint")
                            {
                                Require(body==null,"The saved gourd has multiple body paint materials.");body=renderer;
                                copy.SetFloat("_PaintMapReady",0);copy.SetFloat("_PaperContrast",0);
                                copy.SetFloat("_PaperBump",0);copy.SetFloat("_PaperSmoothness",0);
                            }
                        }
                        renderer.sharedMaterials=copies;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    }
                    Require(body!=null,"The saved gourd has no actual paint-shell mesh.");
                    clay=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                    clay.SetTexture("_PaintMap",paint);clay.SetTexture("_HeightMap",height);
                    clay.SetTexture("_PainterMap",Texture2D.blackTexture);clay.SetFloat("_PaintMapReady",1);
                    clay.SetFloat("_PlayerPaintPatternsEnabled",0);
                    var coat=new GameObject("Rounded clay float GPU check"){hideFlags=HideFlags.HideAndDontSave,layer=30};
                    coat.transform.SetParent(body.transform,false);
                    coat.AddComponent<MeshFilter>().sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
                    var draw=coat.AddComponent<MeshRenderer>();draw.sharedMaterial=clay;
                    draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=false;
                    Bounds expanded=draw.GetComponent<MeshFilter>().sharedMesh.bounds;expanded.Expand(.05f);draw.localBounds=expanded;
                    var cameraObject=new GameObject("Rounded clay check camera"){hideFlags=HideFlags.HideAndDontSave};
                    SceneManager.MoveGameObjectToScene(cameraObject,scene);camera=cameraObject.AddComponent<Camera>();
                    camera.enabled=false;camera.cameraType=CameraType.Preview;camera.scene=scene;camera.orthographic=true;
                    camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
                    camera.backgroundColor=new Color(.075f,.095f,.115f,1);camera.nearClipPlane=.01f;camera.farClipPlane=20;
                    camera.allowHDR=true;camera.allowMSAA=false;
                    target=new RenderTexture(RenderSize,RenderSize,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)
                    {hideFlags=HideFlags.HideAndDontSave};Require(target.Create(),"Floating-point target creation failed.");camera.targetTexture=target;
                    var lightObject=new GameObject("Rounded clay check light"){hideFlags=HideFlags.HideAndDontSave};
                    SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();
                    light.type=LightType.Directional;light.intensity=1;light.cullingMask=1<<30;
                    lightObject.transform.rotation=Quaternion.Euler(35,-35,0);
                }
                catch{Dispose();throw;}
            }
            public Color[] Render(float thickness,float gloss,float contact,float detail,float yaw=20,float pitch=10)
            {
                clay.SetFloat("_ClayThickness",thickness);clay.SetFloat("_ClayGloss",gloss);
                clay.SetFloat("_ClayContactStrength",contact);clay.SetFloat("_ClayDetailStrength",detail);
                instance.transform.rotation=Quaternion.Euler(pitch,yaw,0);instance.transform.position-=body.bounds.center;
                Bounds bounds=body.bounds;
                foreach(Renderer part in instance.GetComponentsInChildren<Renderer>())if(part.enabled)bounds.Encapsulate(part.bounds);
                camera.transform.SetPositionAndRotation(bounds.center+Vector3.forward*4,Quaternion.Euler(0,180,0));
                camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.12f/1.45f;
                RenderTexture previous=RenderTexture.active;Texture2D copy=null;
                try
                {
                    camera.Render();RenderTexture.active=target;
                    copy=new Texture2D(RenderSize,RenderSize,TextureFormat.RGBAFloat,false,true){hideFlags=HideFlags.HideAndDontSave};
                    copy.ReadPixels(new Rect(0,0,RenderSize,RenderSize),0,0);copy.Apply(false,false);
                    Color[] pixels=copy.GetPixels();
                    foreach(Color pixel in pixels)
                        Require(float.IsFinite(pixel.r)&&float.IsFinite(pixel.g)&&float.IsFinite(pixel.b)&&float.IsFinite(pixel.a)
                            &&Mathf.Max(Mathf.Abs(pixel.r),Mathf.Max(Mathf.Abs(pixel.g),Mathf.Abs(pixel.b)))<8,
                            "The floating-point render contains NaN, infinity or unbounded color.");
                    return pixels;
                }
                finally{RenderTexture.active=previous;if(copy!=null)Object.DestroyImmediate(copy);}
            }
            public void Dispose()
            {
                if(camera!=null)camera.targetTexture=null;
                if(target!=null){target.Release();Object.DestroyImmediate(target);target=null;}
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                foreach(Material material in materials)if(material!=null)Object.DestroyImmediate(material);
                materials.Clear();if(clay!=null)Object.DestroyImmediate(clay);clay=null;
            }
        }
    }
}
