using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Actual saved gourd + real journal + production clay renderer. Disposable stopped-Editor stills only.</summary>
    public static class LabClayVisualStudy
    {
        const string LegacySurfacePath="Assets/Editor/WatercolorPaint/ClaySticker/LabClayLegacySurfaceReference.shader";
        [Serializable] public sealed class Report
        {
            public string unity,gpu,scope;
            public bool scenePreserved,configPreserved,randomPreserved;
            public List<Entry> fixtures = new List<Entry>();
        }
        [Serializable] public sealed class Entry
        {
            public string name,authorityBefore,authorityAfter;
            public int hits,cells;
            public ulong applied,rendered,finalSequence;
            public bool complete;
        }
        [Serializable] public sealed class LocalGlossReport
        {
            public string unity,gpu,scope;
            public string[] rowOrder={"first","first-plus-three","same-color-overlap","two-color-overlap"};
            public string[] viewOrder={"front","right","back","left","oblique","upper-oblique"};
            public float[] glossColumns={0,.35f,1};
            public float thickness=.01f;
            public bool scenePreserved,configPreserved,randomPreserved;
            public List<Entry> fixtures=new List<Entry>();
        }
        [MenuItem("C6 Lab/Experiments/Clay Sticker/Export Local Gloss Comparison")]
        public static void ExportLocalGlossMenu()
        {
            string directory=EditorUtility.OpenFolderPanel("Local sticker gloss evidence outside Assets","","");
            if(!string.IsNullOrEmpty(directory))ExportLocalGlossComparison(directory);
        }
        /// <summary>Same real rules/artwork, fixed thickness, only gloss changes across each row.</summary>
        public static string ExportLocalGlossComparison(string directory)
        {
            Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stop Play mode and finish compilation first.");
            Require(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Actual GPU rendering is required.");
            string path=EvidenceDirectory(directory),scenes=Scenes(),random=JsonUtility.ToJson(UnityEngine.Random.state);
            var saved=Resources.Load<LabConfig>("LabConfig");Require(saved!=null,"Saved LabConfig is missing.");
            string config=EditorJsonUtility.ToJson(saved);Directory.CreateDirectory(path);
            var report=new LocalGlossReport{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),
                scope="Stopped-Editor private WhiteGourd/material copies using production v2 journal maps. Gloss columns 0/.35/1 at thickness .01. Authority unchanged. Not device, networking or Mac Player verification."};
            var settings=new LabWatercolorStampSettings{mode=LabPaintStampMode.ClaySticker,revealSeconds=0};settings.Validate();
            var frames=new List<Texture2D>();
            try
            {
                for(int fixture=0;fixture<4;fixture++)using(var study=new Fixture(saved,settings,report.rowOrder[fixture]))
                {
                    study.Hit(Vector3.forward,LabOrbElement.Water,1,.5f);
                    if(fixture==1)for(int i=0;i<3;i++)study.Hit(Vector3.forward,LabOrbElement.Water,1,.5f,true);
                    if(fixture>=2)study.Hit(Quaternion.AngleAxis(12,Vector3.up)*Vector3.forward,
                        fixture==2?LabOrbElement.Water:LabOrbElement.Fire,2,.5f,true);
                    study.Finish();
                    using(var preview=new Preview(study))foreach(float gloss in report.glossColumns)
                    {
                        var frame=preview.Render(report.thickness,gloss,0,1.45f);frames.Add(frame);
                        Save(path,"clay-local-"+report.rowOrder[fixture]+"-gloss-"+GlossName(gloss)+".png",frame);
                    }
                    report.fixtures.Add(study.Check());
                }
                Grid(path,"clay-local-gloss-first-rehits-overlaps.png",frames,3);DestroyFrames(frames);
                using(var full=new Fixture(saved,settings,"five-color-complete"))
                {
                    Complete(full);
                    using(var preview=new Preview(full))foreach(float gloss in report.glossColumns)
                    {
                        float[] yaw={0,90,180,-90,45,45};
                        for(int i=0;i<6;i++)frames.Add(preview.Render(report.thickness,gloss,yaw[i],1,i==5?40:0));
                        Grid(path,"clay-local-gloss-completed-six-views-"+GlossName(gloss)+".png",frames,3);
                        DestroyFrames(frames);
                    }
                    Shader legacy=AssetDatabase.LoadAssetAtPath<Shader>(LegacySurfacePath);
                    Require(legacy!=null&&legacy.isSupported,"The immutable v1 surface GPU reference is unavailable.");
                    using(var before=new Preview(full.Runtime.PaintTexture,full.Runtime.HeightTexture,legacy))
                    using(var after=new Preview(full))
                    {
                        // Top row: legacy surface. Bottom row: local sticker gloss.
                        // Identical v2 maps, front pose, thickness and gloss columns.
                        foreach(float gloss in report.glossColumns)frames.Add(before.Render(report.thickness,gloss,0,1));
                        foreach(float gloss in report.glossColumns)frames.Add(after.Render(report.thickness,gloss,0,1));
                        Grid(path,"clay-local-gloss-before-after.png",frames,3);DestroyFrames(frames);
                    }
                    report.fixtures.Add(full.Check());
                }
                report.scenePreserved=scenes==Scenes();report.configPreserved=config==EditorJsonUtility.ToJson(saved);
                report.randomPreserved=random==JsonUtility.ToJson(UnityEngine.Random.state);
                Require(report.scenePreserved&&report.configPreserved&&report.randomPreserved,"Local-gloss study altered shared Scene/config/RNG state.");
                string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(path,"clay-local-gloss-report.json"),json);return json;
            }
            finally{DestroyFrames(frames);}
        }

        internal static void CheckLocalGlossContracts(List<string> checks)
        {
            var source=Resources.Load<LabConfig>("LabConfig");Require(source!=null,"Saved LabConfig is missing.");
            Shader legacy=AssetDatabase.LoadAssetAtPath<Shader>(LegacySurfacePath);
            Require(legacy!=null&&legacy.isSupported,"The immutable v1 surface GPU reference is unavailable.");
            var settings=new LabWatercolorStampSettings{mode=LabPaintStampMode.ClaySticker,revealSeconds=0};settings.Validate();
            using(var study=new Fixture(source,settings,"zero-gloss-reference"))
            {
                study.Hit(Vector3.forward,LabOrbElement.Water,1,.5f);
                study.Hit(Quaternion.AngleAxis(12,Vector3.up)*Vector3.forward,LabOrbElement.Fire,2,.5f,true);study.Finish();
                using(var current=new Preview(study))using(var reference=new Preview(study.Runtime.PaintTexture,study.Runtime.HeightTexture,legacy))
                {
                    Texture2D actual=null,expected=null;
                    try
                    {
                        actual=current.Render(.01f,0,20,1.45f,10);expected=reference.Render(.01f,0,20,1.45f,10);
                        Require(MaxChannelDelta(actual,expected)<=1,"Gloss zero changed the legacy diffuse/contact/geometry image.");
                    }
                    finally{if(actual!=null)Object.DestroyImmediate(actual);if(expected!=null)Object.DestroyImmediate(expected);}
                }
                study.Check();
            }
            checks.Add("gloss zero matches the immutable v1 surface diffuse/contact/geometry image within one output byte");

            Texture2D paint=null,height=null,matte=null,glossy=null;
            try
            {
                // Complete-only undercoat: opaque displayed pigment and the old .16 R
                // lift, but no actual sticker's GB/A coating data anywhere on the mesh.
                paint=ConstantMap(new Color(.25f,.45f,.7f,1));height=ConstantMap(new Color(.16f,0,0,0),true);
                using(var preview=new Preview(paint,height))
                {
                    matte=preview.Render(.01f,0,20,1.45f,10);glossy=preview.Render(.01f,1,20,1.45f,10);
                    Require(VisibleBluePigment(matte),"The complete-only undercoat fixture did not render visible pigment.");
                    Require(MaxChannelDelta(matte,glossy)==0,"Completion-only undercoat acquired gloss without a real sticker.");
                }
            }
            finally{foreach(var image in new[]{paint,height,matte,glossy})if(image!=null)Object.DestroyImmediate(image);}
            checks.Add("opaque complete-only undercoat is pixel-identical at gloss zero and one without real-sticker support");

            paint=height=matte=glossy=null;
            try
            {
                BoundaryUndercoatMaps(out paint,out height);
                using(var preview=new Preview(paint,height))
                {
                    matte=preview.Render(.01f,0,0,1.45f);glossy=preview.Render(.01f,1,0,1.45f);
                    Require(VisibleBluePigment(matte),"The adjacent-undercoat fixture did not render visible pigment.");
                    Require(MaxChannelDelta(matte,glossy)==0,"Filtered neighboring sticker support leaked gloss onto zero-support completion undercoat.");
                }
            }
            finally{foreach(var image in new[]{paint,height,matte,glossy})if(image!=null)Object.DestroyImmediate(image);}
            checks.Add("undercoat adjacent to a real sticker remains pixel-identical at gloss zero/one outside the unfiltered support mask");
        }

        static string EvidenceDirectory(string directory)
        {
            Require(Path.IsPathRooted(directory),"Use an absolute evidence output directory.");
            string path=Path.GetFullPath(directory),assets=Path.GetFullPath(Application.dataPath);
            Require(path!=assets&&!path.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Keep evidence outside Assets.");return path;
        }
        static Texture2D ConstantMap(Color value,bool half=false)
        {
            var map=new Texture2D(16,8,half?TextureFormat.RGBAHalf:TextureFormat.RGBA32,false,true)
                {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
            map.SetPixels(Enumerable.Repeat(value,128).ToArray());map.Apply(false,false);return map;
        }
        static void BoundaryUndercoatMaps(out Texture2D paint,out Texture2D height)
        {
            const int columns=128,rows=64,boundary=96;
            paint=new Texture2D(columns,rows,TextureFormat.RGBA32,false,true);
            height=new Texture2D(columns,rows,TextureFormat.RGBAHalf,false,true);
            var pigments=new Color[columns*rows];var relief=new Color[pigments.Length];
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                int i=y*columns+x;
                relief[i]=x<boundary?new Color(.8f,.5f,.5f,1):new Color(.16f,0,0,0);
                // The first undercoat texel is deliberately invisible. Consequently
                // every visible paint sample has center A==0, while the normal's
                // 2..5-texel filter can still reach the neighboring real sticker.
                pigments[i]=x>boundary&&x<columns-2?new Color(.25f,.45f,.7f,1):Color.clear;
            }
            foreach(var map in new[]{paint,height})
            {
                map.hideFlags=HideFlags.HideAndDontSave;map.filterMode=FilterMode.Bilinear;
                map.wrapModeU=TextureWrapMode.Repeat;map.wrapModeV=TextureWrapMode.Clamp;
            }
            paint.SetPixels(pigments);paint.Apply(false,false);height.SetPixels(relief);height.Apply(false,false);
        }
        static int MaxChannelDelta(Texture2D a,Texture2D b)
        {
            Require(a.width==b.width&&a.height==b.height,"Surface comparison frame sizes differ.");
            Color32[] x=a.GetPixels32(),y=b.GetPixels32();int maximum=0;
            for(int i=0;i<x.Length;i++)maximum=Math.Max(maximum,Math.Max(Math.Max(Math.Abs(x[i].r-y[i].r),Math.Abs(x[i].g-y[i].g)),
                Math.Max(Math.Abs(x[i].b-y[i].b),Math.Abs(x[i].a-y[i].a))));return maximum;
        }
        static bool VisibleBluePigment(Texture2D image)=>image.GetPixels32().Count(p=>p.b>p.r+35&&p.b>p.g+10)>100;
        static string GlossName(float gloss)=>gloss==0?"0":gloss==1?"1":"035";
        static void DestroyFrames(List<Texture2D> frames){foreach(var image in frames)if(image!=null)Object.DestroyImmediate(image);frames.Clear();}
        static void Complete(Fixture full)
        {
            const int samples=256;
            for(int i=0;i<samples&&!full.Surface.IsComplete;i++)
            {
                float y=1-2*((i+.5f)/samples),angle=i*2.39996323f,r=Mathf.Sqrt(Mathf.Max(0,1-y*y));
                full.Hit(new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r),(LabOrbElement)(1+i%5),1+i%5,.3f+(i%4)*.2f,true);
            }
            Require(full.Surface.IsComplete,"The actual fixture did not reach 100% within its bounded hit count.");full.Seal();full.Finish();
        }
        [MenuItem("C6 Lab/Experiments/Clay Sticker/Export Actual Gourd Comparison")]
        public static void ExportMenu()
        {
            string directory=EditorUtility.OpenFolderPanel("Actual clay-gourd evidence outside Assets","","");
            if(!string.IsNullOrEmpty(directory))Export(directory);
        }
        public static string Export(string directory)
        {
            Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stop Play mode and finish compilation first.");
            Require(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Actual GPU rendering is required.");
            Require(Path.IsPathRooted(directory),"Use an absolute evidence output directory.");
            string path=Path.GetFullPath(directory),assets=Path.GetFullPath(Application.dataPath);
            Require(path!=assets&&!path.StartsWith(assets+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Keep evidence outside Assets.");
            string scenes=Scenes(),random=JsonUtility.ToJson(UnityEngine.Random.state);
            var saved=Resources.Load<LabConfig>("LabConfig");Require(saved!=null,"Saved LabConfig is missing.");
            string config=EditorJsonUtility.ToJson(saved);
            Directory.CreateDirectory(path);
            var report=new Report{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),scope="Production clay runtime and surface shader on disposable saved WhiteGourd copies. Real Host-rule first hit, re-hits and full completion. Static fixture only disables drying; live game, networking, iOS and Mac Player are not exercised here. Strength values are presentation hints; thickness and gloss are private material controls."};
            var settings=new LabWatercolorStampSettings{mode=LabPaintStampMode.ClaySticker,revealSeconds=0};settings.Validate();
            var frames=new List<Texture2D>();
            try
            {
                // Rows: first / first + 3 re-hits / two colors. Columns: ordinary clay / thicker satin clay.
                for(int fixture=0;fixture<3;fixture++)
                using(var study=new Fixture(saved,settings,"fixture-"+fixture))
                {
                    study.Hit(Vector3.forward,LabOrbElement.Water,1,.5f);
                    if(fixture==1)for(int i=0;i<3;i++)study.Hit(Vector3.forward,LabOrbElement.Water,1,.5f,true);
                    if(fixture==2)study.Hit(Quaternion.AngleAxis(12,Vector3.up)*Vector3.forward,LabOrbElement.Fire,2,.5f,true);
                    study.Finish();
                    using(var preview=new Preview(study))
                    {
                        frames.Add(preview.Render(.006f,.25f,0,1.45f));
                        frames.Add(preview.Render(.014f,.65f,0,1.45f));
                        Save(path,"clay-"+(fixture==0?"first":fixture==1?"first-plus-three":"two-colors")+".png",frames[frames.Count-1]);
                    }
                    report.fixtures.Add(study.Check());
                }
                Grid(path,"clay-first-rehits-overlap.png",frames,2);
                foreach(var frame in frames)Object.DestroyImmediate(frame);frames.Clear();
                // Same actual hit/color/geometry, differing only Host-authored strength.
                for(int i=0;i<2;i++)using(var study=new Fixture(saved,settings,"strength-"+i))
                {
                    study.Hit(Vector3.forward,LabOrbElement.Fire,1,i);study.Finish();
                    using(var preview=new Preview(study))frames.Add(preview.Render(.01f,.35f,0,1.8f));
                    report.fixtures.Add(study.Check());
                }
                Grid(path,"clay-impact-weak-strong.png",frames,2);
                foreach(var frame in frames)Object.DestroyImmediate(frame);frames.Clear();
                using(var full=new Fixture(saved,settings,"five-color-complete"))
                {
                    const int samples=256;
                    for(int i=0;i<samples&&!full.Surface.IsComplete;i++)
                    {
                        // Deterministic Fibonacci locations. These are real RegisterHit calls,
                        // not direct pixel installation or a pre-painted illustration.
                        float y=1-2*((i+.5f)/samples),angle=i*2.39996323f,r=Mathf.Sqrt(Mathf.Max(0,1-y*y));
                        var d=new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
                        full.Hit(d,(LabOrbElement)(1+i%5),1+i%5,.3f+(i%4)*.2f,true);
                    }
                    Require(full.Surface.IsComplete,"The actual fixture did not reach 100% within its bounded hit count.");
                    full.Seal();full.Finish();
                    using(var preview=new Preview(full))
                    {
                        float[] yaw={0,90,180,-90,45,45};
                        for(int i=0;i<6;i++)frames.Add(preview.Render(.01f,.35f,yaw[i],1,i==5?40:0));
                        Grid(path,"clay-completed-six-views.png",frames,3);
                        Save(path,"clay-completed-front.png",frames[0]);
                        foreach(var frame in frames)Object.DestroyImmediate(frame);frames.Clear();
                        // Rows thickness 0/.02, columns gloss 0/1, identical artwork.
                        foreach(float thickness in new[]{0f,.02f})foreach(float gloss in new[]{0f,1f})
                            frames.Add(preview.Render(thickness,gloss,35,1.45f,12));
                        Grid(path,"clay-thickness-gloss-endpoints.png",frames,2);
                        Texture2D png=preview.CapturePng(.01f,.35f);
                        try{Save(path,"clay-production-png.png",png);}finally{Object.DestroyImmediate(png);}
                    }
                    report.fixtures.Add(full.Check());
                }
                report.scenePreserved=scenes==Scenes();report.configPreserved=config==EditorJsonUtility.ToJson(saved);
                report.randomPreserved=random==JsonUtility.ToJson(UnityEngine.Random.state);
                Require(report.scenePreserved&&report.configPreserved&&report.randomPreserved,"Actual study altered shared Scene/config/RNG state.");
                string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(path,"clay-visual-report.json"),json);
                return json;
            }
            finally{foreach(var frame in frames)if(frame!=null)Object.DestroyImmediate(frame);}
        }

        internal sealed class Fixture:IDisposable
        {
            public readonly LabPaintSurface Surface;
            public readonly LabPaintArtJournal Journal;
            public readonly LabClayStickerRuntime Runtime=new LabClayStickerRuntime();
            readonly LabWatercolorStampSettings settings;
            readonly string name;
            string originalHash;
            public Fixture(LabConfig source,LabWatercolorStampSettings settings,string name)
            {
                this.settings=settings;this.name=name;
                LabPaintGrowthSettings rules=source.PaintGrowthSettings;rules.DryingEnabled=false;
                Surface=new LabPaintSurface(rules,123u);
                if(Surface.Pattern==LabPaintPattern.Splatter)Surface.SetPaperStickerContour();
                Journal=new LabPaintArtJournal(Path.Combine(Application.temporaryCachePath,"clay-visual-"+Guid.NewGuid().ToString("N")),"clay-visual-"+name,Surface.Columns,Surface.Rows,settings.Version);
                Require(Surface.AttachArtJournal(Journal),"Attach actual clay fixture journal.");
            }
            public void Hit(Vector3 direction,LabOrbElement color,int player,float strength,bool grow=false)
            {
                Require(Surface.RegisterHit(direction,LabPaintPalette.ColorFor(color),(ulong)player,player,1f,strength),"Actual fixture hit was rejected.");
                if(grow)Surface.Advance(1d);
                Require(Journal.Commit(Surface.Revision,Hash(Surface)),Journal.Error??"Commit actual fixture.");
            }
            public void Seal(){Require(Journal.Seal(Surface.Revision,Hash(Surface)),Journal.Error??"Seal actual clay fixture.");}
            public void Finish()
            {
                originalHash=Hash(Surface);
                for(int i=0;i<4096;i++)
                {
                    Runtime.Update(Journal,null,null,1024,settings,i*.016,true);
                    Require(Runtime.Error==null,Runtime.Error??"Clay runtime error.");
                    if(Runtime.Ready)return;
                    // Bounded stopped-Editor still export only. The live renderer remains asynchronous.
                    AsyncGPUReadback.WaitAllRequests();
                }
                throw new InvalidOperationException("Actual clay fixture replay did not finish.");
            }
            public Entry Check()
            {
                string after=Hash(Surface);Require(after==originalHash,"Rendering changed actual surface authority.");
                Require(Runtime.Ready&&Runtime.RenderedThrough==Journal.LastSequence,"Displayed GPU artwork is not final.");
                return new Entry{name=name,authorityBefore=originalHash,authorityAfter=after,hits=Surface.PaintCount,cells=Surface.CoveredCount,complete=Surface.IsComplete,applied=Runtime.AppliedThrough,rendered=Runtime.RenderedThrough,finalSequence=Journal.LastSequence};
            }
            public void Dispose(){Runtime.Dispose();Journal?.Dispose();}
        }
        sealed class Preview:IDisposable
        {
            readonly LabPaintStudyRenderer renderer;
            Material clay;
            public Preview(Fixture study):this(study.Runtime.PaintTexture,study.Runtime.HeightTexture){}
            public Preview(Texture paint,Texture height,Shader surfaceShader=null)
            {
                try
                {
                    renderer=new LabPaintStudyRenderer(true,configureStudySurface:material=>{
                        material.SetFloat("_PaperContrast",0);material.SetFloat("_PaperBump",0);material.SetFloat("_PaperSmoothness",0);
                    },configureStudyInstance:root=>{
                        MeshRenderer body=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.sharedMaterials.Any(m=>m&&m.shader.name=="C6Lab/GourdPaint"));
                        Shader shader=surfaceShader??Resources.Load<Shader>(LabClayStickerRuntime.SurfaceShaderResource);
                        Require(shader!=null&&shader.isSupported,"Production clay surface shader is missing.");
                        Require(!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"Clay surface shader has compilation errors.");
                        clay=new Material(shader){name="Actual clay study material",hideFlags=HideFlags.HideAndDontSave};
                        clay.SetTexture("_PaintMap",paint);clay.SetTexture("_HeightMap",height);
                        clay.SetTexture("_PainterMap",Texture2D.blackTexture);clay.SetFloat("_PaintMapReady",1);
                        clay.SetFloat("_PlayerPaintPatternsEnabled",0);
                        var go=new GameObject("Actual curved clay overlay"){hideFlags=HideFlags.HideAndDontSave,layer=body.gameObject.layer};
                        go.transform.SetParent(body.transform,false);go.AddComponent<MeshFilter>().sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
                        var draw=go.AddComponent<MeshRenderer>();draw.sharedMaterial=clay;draw.shadowCastingMode=ShadowCastingMode.Off;draw.receiveShadows=false;
                    });
                }
                catch{Dispose();throw;}
            }
            public Texture2D Render(float thickness,float gloss,float yaw=0,float zoom=1,float pitch=0)
            {Set(thickness,gloss);return renderer.RenderVisual(Texture2D.blackTexture,yaw,zoom,pitch);}
            public Texture2D CapturePng(float thickness,float gloss)
            {Set(thickness,gloss);return renderer.CaptureStudyPng(Texture2D.blackTexture,null);}
            void Set(float thickness,float gloss){clay.SetFloat("_ClayThickness",thickness);clay.SetFloat("_ClayGloss",gloss);}
            public void Dispose(){renderer?.Dispose();if(clay!=null)Object.DestroyImmediate(clay);}
        }
        static string Scenes()=>string.Join("|",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>{var s=SceneManager.GetSceneAt(i);return s.handle+":"+s.path+":"+s.isDirty;}));
        static string Hash(LabPaintSurface surface)
        {var colors=new Color32[surface.CellCount];var owners=new byte[surface.CellCount];surface.CopyPixels(colors);surface.CopyPainterSlots(owners);return LabPaintPublisher.Hash(colors,owners);}
        static void Grid(string directory,string name,IList<Texture2D> frames,int columns)
        {
            int size=LabPaintStudyRenderer.RenderSize,rows=(frames.Count+columns-1)/columns;
            var sheet=new Texture2D(columns*size,rows*size,TextureFormat.RGBA32,false,false);
            try{for(int i=0;i<frames.Count;i++)sheet.SetPixels32(i%columns*size,(rows-1-i/columns)*size,size,size,frames[i].GetPixels32());sheet.Apply();Save(directory,name,sheet);}
            finally{Object.DestroyImmediate(sheet);}
        }
        static void Save(string directory,string name,Texture2D image)=>File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG());
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
