using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
using Stopwatch=System.Diagnostics.Stopwatch;
namespace C6Lab.Experiments.Editor
{
    /// <summary>Independent V4 GPU contracts; never replaces the production/V3 runners.</summary>
    public static class CoatingClayGpuChecks
    {
        const string Root="Assets/Experiments/ClayPreview/Runtime/Shaders/";
        const int Width=1024,Height=512;
        [Serializable] public sealed class Report
        {
            public string unity,gpu,timingScope;
            public int checksPassed,opaquePixels,partialPixels,undercoatPixels;
            public float maxTilt,octError,partialError,wrongOctDistance,matteDifference,undercoatDifference,fringeDifference,smallSpecularMaximum;
            public double wallMillisecondsIncludingReadback;
            public string[] checks;
        }
        [MenuItem("C6 Lab/Experiments/Rounded Clay/Run Coating V4 GPU Checks")]
        static void Menu()=>Debug.Log(Run());
        public static string Run()
        {
            Need(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stop Play mode and finish compilation.");
            Need(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null&&SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)
                &&SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf),"A real Half/Float GPU target is required.");
            Shader compositor=Load("StickerCoatingRuntime.shader"),surface=Load("StickerCoatingSurface.shader"),v3=Load("RoundedClayStickerSurface.shader");
            var timer=Stopwatch.StartNew();var checks=new List<string>();var r=new Report{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString(),
                timingScope="Stopped-Editor wall time includes preparation, GPU waits and synchronous CPU readback; not GPU execution time."};
            using(var pair=ReplayPair(null,null,compositor))checks.Add("production real-journal color RGBA and Half height R/A bytes are exact");
            Texture2D[] brushes=new[]{"Broad","Long","Cluster"}.Select(n=>AssetDatabase.LoadAssetAtPath<Texture2D>(RoundedClayExperiment.Root+"/RoundedBevel/"+n+"Data.png")).ToArray();
            Need(brushes.All(b=>b!=null),"Prepare the existing V3 rounded brush data before running V4 checks.");
            using(var pair=ReplayPair(brushes,new LabClayStickerSettings{minimumDensity=1,maximumDensity=1},compositor))
            {
                checks.Add("V3 brushes/pigment retain exact real-journal color RGBA and Half height R/A bytes");
                CheckNormals(compositor,brushes,r,checks);
                Texture2D paint=null,height=null;
                try
                {
                    Maps(false,out paint,out height);
                    using(var p=new RoundedClaySurfaceChecks.FloatPreview(surface,paint,height))
                    {
                        r.undercoatDifference=Difference(p.Render(.014f,0,.15f,1),p.Render(.014f,1,.15f,1));
                        Need(r.undercoatDifference<=.00001f,"Zero-coverage complete undercoat acquired gloss.");
                        checks.Add("raw-A-zero completion undercoat is identical at gloss zero and one");
                    }
                    using(var a=new RoundedClaySurfaceChecks.FloatPreview(v3,pair.color0,pair.height0))
                    using(var b=new RoundedClaySurfaceChecks.FloatPreview(surface,pair.color1,pair.height1))
                    {
                        r.matteDifference=Difference(a.Render(.014f,0,.15f,1),b.Render(.014f,0,.15f,1));
                        Need(r.matteDifference<=.00001f,"V4 changed the total V3 image at gloss zero.");
                        checks.Add("gloss-zero V4 diffuse/contact/detail equals V3 for the same real journal");
                    }
                    Destroy(ref paint,ref height);Maps(true,out paint,out height);
                    using(var p=new RoundedClaySurfaceChecks.FloatPreview(surface,paint,height))
                    {
                        Color[] matte=p.Render(.014f,0,.15f,1),glossy=p.Render(.014f,1,.15f,1);int exercised=0;
                        for(int i=0;i<matte.Length;i++)if(Blue(matte[i])){r.undercoatPixels++;r.fringeDifference=Mathf.Max(r.fringeDifference,Delta(matte[i],glossy[i]));}
                            else if(Delta(matte[i],glossy[i])>.0001f)exercised++;
                        Need(r.undercoatPixels>500&&exercised>100&&r.fringeDifference<=.00001f,"Point normals leaked beyond coverage AA, or the fixture never reflected.");
                        checks.Add("point-normal highlights remain inside coverage including the adjacent undercoat fringe");
                        foreach(Vector2 view in new[]{new Vector2(0,0),new Vector2(90,0),new Vector2(180,0),new Vector2(-90,0),new Vector2(45,80),new Vector2(45,-80)})
                            Need(p.Render(.02f,1,.5f,1,view.x,view.y).Count(Colored)>250,"A finite seam/pole view did not show the colored object.");
                        checks.Add("six actual-mesh longitude and polar views remain finite at maximum controls");
                        p.SetCaptureSize(160);p.SetBackground(Color.black);p.SetCoatingOnly(true);p.Material.SetFloat("_SpecularOnly",1);
                        Color[] black=p.Render(.014f,0,.15f,1),spec=p.Render(.014f,1,.15f,1);
                        Need(black.Length==160*160&&black.All(c=>Max(c)<=.00001f),"Small-screen specular-only gloss-zero output is not black.");
                        r.smallSpecularMaximum=spec.Max(Max);
                        Need(spec.Count(c=>Max(c)>.0001f)>10&&r.smallSpecularMaximum<=.301f,"Small-screen specular output is missing or unbounded.");
                        checks.Add("160-pixel reflection-only output is black at gloss zero and bounded/visible at gloss one");
                    }
                }
                finally{Destroy(ref paint,ref height);}
            }
            r.checks=checks.ToArray();r.checksPassed=checks.Count;r.wallMillisecondsIncludingReadback=timer.Elapsed.TotalMilliseconds;
            Need(r.checksPassed==10,"Not all ten contracts executed.");return JsonUtility.ToJson(r,true);
        }
        static void CheckNormals(Shader shader,Texture2D[] brushes,Report r,List<string> checks)
        {
            var m=new Material(shader){hideFlags=HideFlags.HideAndDontSave};RenderTexture empty=null,lower=null,solo=null,stack=null;Texture2D full=null,partial=null;
            RenderTexture savedActive=RenderTexture.active;
            try
            {
                empty=Target();lower=Target();solo=Target();stack=Target();Clear(empty,Color.clear);full=Brush(1);partial=Brush(128/255f);
                Vector3[] vectors={Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down,
                    new Vector3(.2f,.2f,-1).normalized,new Vector3(-.2f,-.2f,-1).normalized,new Vector3(1,-2,3).normalized,new Vector3(-2,1,-3).normalized};
                foreach(Vector3 center in vectors)
                {
                    r.octError=Mathf.Max(r.octError,Vector3.Distance(center,Decode(Encode(center))));
                    Stamp(m,full,center,0);Graphics.Blit(empty,solo,m,1);Color[] pixels=Read(solo);int paid=0;
                    foreach(Color p in pixels)
                    {
                        Need(Finite(p)&&p.a>=0&&p.a<=1.001f&&p.g>=0&&p.b>=0&&p.g<=p.a+.001f&&p.b<=p.a+.001f,"Invalid premultiplied full-normal data.");
                        if(p.a<.05f)continue;paid++;float tilt=Vector3.Angle(center,Decode(new Vector2(p.g,p.b)/p.a));r.maxTilt=Mathf.Max(r.maxTilt,tilt);
                        Need(tilt<=25.5f,"A full normal exceeds its centre-frame 25-degree bound.");
                    }
                    Need(paid>100,"An oct fold/pole fixture was not actually covered.");
                    if(Mathf.Abs(center.y)<.99f)Need(Vector3.Angle(center,Decode(At(pixels,center)))<1,"GPU encoding selected the wrong centre/hemisphere.");
                }
                Need(r.octError<.00001f,"Oct canonical/fold roundtrip failed.");
                checks.Add("oct fold roundtrips and GPU normals are finite/premultiplied and bounded to 25 degrees at seam/pole fixtures");
                Vector3 top=Quaternion.AngleAxis(12,Vector3.up)*Vector3.forward;
                Stamp(m,brushes[0],Vector3.forward,-25);Graphics.Blit(empty,lower,m,1);Stamp(m,brushes[1],top,35);
                Graphics.Blit(empty,solo,m,1);Graphics.Blit(lower,stack,m,1);Color[] a=Read(solo),b=Read(stack),old=Read(lower);int lifted=0;
                for(int i=0;i<a.Length;i++)if(a[i].a>=.9999f&&old[i].a>.1f)
                {
                    r.opaquePixels++;Need(Vector3.Distance(Decode(new Vector2(a[i].g,a[i].b)/a[i].a),Decode(new Vector2(b[i].g,b[i].b)/b[i].a))<.003f,"Lower normals survived an opaque top.");
                    if(b[i].r>a[i].r+.001f)lifted++;
                }
                Need(r.opaquePixels>100&&lifted>100,"Opaque overlap did not exercise a raised lower sheet.");
                checks.Add("opaque top equals isolated-top normals while the legacy lower-sheet R carry remains");
                Vector2 oldOct=Encode(new Vector3(.2f,.2f,-1).normalized);Vector3 next=new Vector3(-.2f,-.2f,-1).normalized;
                Clear(lower,new Color(.4f,oldOct.x,oldOct.y,1));Stamp(m,partial,next,0);Graphics.Blit(empty,solo,m,1);Graphics.Blit(lower,stack,m,1);
                a=Read(solo);b=Read(stack);old=Read(lower);
                for(int i=0;i<a.Length;i++)if(a[i].a>.2f&&a[i].a<.8f)
                {
                    r.partialPixels++;Vector3 incoming=Decode(new Vector2(a[i].g,a[i].b)/a[i].a),previous=Decode(new Vector2(old[i].g,old[i].b));
                    Vector3 expected=(incoming*a[i].a+previous*(1-a[i].a)).normalized,actual=Decode(new Vector2(b[i].g,b[i].b)/b[i].a);
                    float error=Vector3.Distance(expected,actual);r.partialError=Mathf.Max(r.partialError,error);Need(error<.006f,"Partial coverage was not blended as decoded 3D unit normals.");
                    Vector3 wrong=Decode(Vector2.Lerp(Encode(previous),Encode(incoming),a[i].a));r.wrongOctDistance=Mathf.Max(r.wrongOctDistance,Vector3.Distance(actual,wrong));
                }
                Need(r.partialPixels>100&&r.wrongOctDistance>.5f,"Partial fixture never exercised an oct-fold mismatch.");
                checks.Add("partial coverage composites decoded 3D directions rather than averaging oct coordinates across a fold");
            }
            finally{RenderTexture.active=savedActive;foreach(var t in new[]{empty,lower,solo,stack})if(t!=null){t.Release();Object.DestroyImmediate(t);}if(full!=null)Object.DestroyImmediate(full);if(partial!=null)Object.DestroyImmediate(partial);Object.DestroyImmediate(m);}
        }
        sealed class Pair:IDisposable
        {public Texture2D color0,height0,color1,height1;public void Dispose(){foreach(var t in new[]{color0,height0,color1,height1})if(t!=null)Object.DestroyImmediate(t);}}
        static Pair ReplayPair(Texture2D[] brushes,LabClayStickerSettings pigment,Shader compositor)
        {
            var settings=new LabWatercolorStampSettings{mode=LabPaintStampMode.ClaySticker,revealSeconds=0};settings.Validate();var source=Resources.Load<LabConfig>("LabConfig");Need(source!=null,"Saved config missing.");
            string config=EditorJsonUtility.ToJson(source),random=JsonUtility.ToJson(UnityEngine.Random.state);LabPaintGrowthSettings rules=source.PaintGrowthSettings;rules.DryingEnabled=false;rules.GrowthPercentPerSecond=100;
            var surface=new LabPaintSurface(rules,123u);if(surface.Pattern==LabPaintPattern.Splatter)surface.SetPaperStickerContour();var pair=new Pair();
            using(var journal=new LabPaintArtJournal(Path.Combine(Application.temporaryCachePath,"coating-check-"+Guid.NewGuid().ToString("N")),"coating-check",surface.Columns,surface.Rows,settings.Version))
            using(var before=new LabClayStickerRuntime(brushes,pigment))using(var after=new LabClayStickerRuntime(brushes,pigment,compositor))
            {
                try
                {
                    Need(surface.AttachArtJournal(journal),"Attach real Host-rule journal.");Need(surface.RegisterHit(Vector3.forward,Color.blue,1,1),"Real first hit rejected.");
                    for(int i=0;i<3;i++){Need(surface.RegisterHit(Vector3.forward,i%2==0?Color.red:Color.green,2,2),"Real re-hit rejected.");surface.Advance(1);}
                    string hash=Hash(surface);Need(journal.Commit(surface.Revision,hash),"Commit real journal.");string prefix=journal.PrefixHash;Finish(before,journal,settings);Finish(after,journal,settings);
                    pair.color0=Capture(before.PaintTexture,TextureFormat.RGBA32);pair.height0=Capture(before.HeightTexture,TextureFormat.RGBAHalf);
                    pair.color1=Capture(after.PaintTexture,TextureFormat.RGBA32);pair.height1=Capture(after.HeightTexture,TextureFormat.RGBAHalf);
                    Need(pair.color0.GetPixels32().SequenceEqual(pair.color1.GetPixels32()),"Actual-journal color RGBA changed.");byte[] x=pair.height0.GetRawTextureData(),y=pair.height1.GetRawTextureData();Need(x.Length==y.Length,"Half map sizes differ.");
                    for(int i=0;i<x.Length;i+=8)Need(x[i]==y[i]&&x[i+1]==y[i+1]&&x[i+6]==y[i+6]&&x[i+7]==y[i+7],"Actual-journal Half R/A source bytes changed.");
                    Need(Hash(surface)==hash&&journal.PrefixHash==prefix&&EditorJsonUtility.ToJson(source)==config&&JsonUtility.ToJson(UnityEngine.Random.state)==random,"Authority, journal, saved config or game RNG changed.");return pair;
                }
                catch{pair.Dispose();throw;}
            }
        }
        static void Finish(LabClayStickerRuntime runtime,LabPaintArtJournal journal,LabWatercolorStampSettings settings)
        {for(int i=0;i<4096;i++){RenderTexture previous=RenderTexture.active;try{runtime.Update(journal,null,null,1024,settings,i*.016,true);}finally{RenderTexture.active=previous;}Need(runtime.Error==null,runtime.Error??"Runtime failed.");if(runtime.Ready)return;AsyncGPUReadback.WaitAllRequests();}throw new InvalidOperationException("V4 replay exceeded its bounded wait.");}
        static Shader Load(string name){var s=AssetDatabase.LoadAssetAtPath<Shader>(Root+name);Need(s!=null&&s.isSupported,"Unavailable shader: "+name);Need(!ShaderUtil.GetShaderMessages(s).Any(m=>m.severity.ToString()=="Error"),"Shader compile error: "+name);return s;}
        static Texture2D Capture(RenderTexture t,TextureFormat format)
        {RenderTexture previous=RenderTexture.active;var copy=new Texture2D(t.width,t.height,format,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};try{RenderTexture.active=t;copy.ReadPixels(new Rect(0,0,t.width,t.height),0,0);copy.Apply(false,false);return copy;}catch{Object.DestroyImmediate(copy);throw;}finally{RenderTexture.active=previous;}}
        static Color[] Read(RenderTexture t){var copy=Capture(t,TextureFormat.RGBAFloat);try{return copy.GetPixels();}finally{Object.DestroyImmediate(copy);}}
        static RenderTexture Target(){var t=new RenderTexture(Width,Height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};Need(t.Create(),"GPU target failed.");return t;}
        static void Clear(RenderTexture t,Color c){RenderTexture previous=RenderTexture.active;try{RenderTexture.active=t;GL.Clear(false,true,c);}finally{RenderTexture.active=previous;}}
        static Texture2D Brush(float alpha){var t=new Texture2D(512,512,TextureFormat.RGBA32,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};t.SetPixels(Enumerable.Repeat(new Color(alpha,0,1,alpha),512*512).ToArray());t.Apply(false,false);return t;}
        static void Stamp(Material m,Texture2D brush,Vector3 center,float rotation)
        {Vector3 right=Vector3.ProjectOnPlane(Vector3.right,center);if(right.sqrMagnitude<.001f)right=Vector3.ProjectOnPlane(Vector3.forward,center);right=Quaternion.AngleAxis(rotation,center)*right.normalized;m.SetTexture("_Brush",brush);m.SetVector("_Center",center);m.SetVector("_Right",right);m.SetVector("_Up",Vector3.Cross(center,right));m.SetFloat("_Radius",.7f);}
        static Vector2 At(Color[] p,Vector3 center){float u=Mathf.Atan2(center.z,center.x)/(2*Mathf.PI)+.5f,v=.5f-center.y*.5f;int x=((Mathf.RoundToInt(u*Width-.5f)%Width)+Width)%Width,y=Mathf.Clamp(Mathf.RoundToInt(v*Height-.5f),0,Height-1);Color c=p[y*Width+x];Need(c.a>.05f,"Centre sample uncovered.");return new Vector2(c.g,c.b)/c.a;}
        static Vector2 Encode(Vector3 normal){Vector3 n=normal.normalized;n/=Mathf.Abs(n.x)+Mathf.Abs(n.y)+Mathf.Abs(n.z);Vector2 p=new Vector2(n.x,n.y);if(n.z<0)p=new Vector2((1-Mathf.Abs(p.y))*Sign(p.x),(1-Mathf.Abs(p.x))*Sign(p.y));return p*.5f+Vector2.one*.5f;}
        static Vector3 Decode(Vector2 e){Vector2 p=e*2-Vector2.one;var n=new Vector3(p.x,p.y,1-Mathf.Abs(p.x)-Mathf.Abs(p.y));float t=Mathf.Clamp01(-n.z);n.x+=n.x>=0?-t:t;n.y+=n.y>=0?-t:t;return n.normalized;}
        static float Sign(float v)=>v>=0?1:-1;
        static bool Finite(Color c)=>float.IsFinite(c.r)&&float.IsFinite(c.g)&&float.IsFinite(c.b)&&float.IsFinite(c.a);
        static bool Blue(Color c)=>c.b>c.r*1.5f&&c.b>c.g*1.15f&&c.b>.15f;
        static bool Colored(Color c)=>Max(c)-Mathf.Min(c.r,Mathf.Min(c.g,c.b))>.1f;
        static float Max(Color c)=>Mathf.Max(c.r,Mathf.Max(c.g,c.b));
        static float Delta(Color a,Color b)=>Mathf.Max(Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g)),Mathf.Max(Mathf.Abs(a.b-b.b),Mathf.Abs(a.a-b.a)));
        static float Difference(Color[] a,Color[] b){Need(a.Length==b.Length,"Frame sizes differ.");float max=0;for(int i=0;i<a.Length;i++)max=Mathf.Max(max,Delta(a[i],b[i]));return max;}
        static string Hash(LabPaintSurface s){var c=new Color32[s.CellCount];var p=new byte[s.CellCount];s.CopyPixels(c);s.CopyPainterSlots(p);return LabPaintPublisher.Hash(c,p);}
        static void Maps(bool bands,out Texture2D paint,out Texture2D height)
        {
            paint=Map(TextureFormat.RGBA32);height=Map(TextureFormat.RGBAHalf);if(bands)paint.filterMode=FilterMode.Point;var colors=new Color[Width*Height];var values=new Color[colors.Length];
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {int local=x%128;bool coated=bands&&local<64,red=bands&&(coated||local<66||local>=126);colors[y*Width+x]=red?new Color(.74f,.22f,.17f,1):new Color(.22f,.42f,.66f,1);
                if(coated){float u=(x+.5f)/Width,v=(y+.5f)/Height,dy=1-2*v,ring=Mathf.Sqrt(Mathf.Max(0,1-dy*dy)),angle=(u-.5f)*Mathf.PI*2;Vector2 oct=Encode(new Vector3(Mathf.Cos(angle)*ring,dy,Mathf.Sin(angle)*ring));values[y*Width+x]=new Color(.88f,oct.x,oct.y,1);}else values[y*Width+x]=new Color(.16f,0,0,0);}
            paint.SetPixels(colors);height.SetPixels(values);paint.Apply(false,false);height.Apply(false,false);
        }
        static Texture2D Map(TextureFormat f)=>new Texture2D(Width,Height,f,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
        static void Destroy(ref Texture2D a,ref Texture2D b){if(a!=null)Object.DestroyImmediate(a);if(b!=null)Object.DestroyImmediate(b);a=b=null;}
        static void Need(bool value,string message){if(!value)throw new InvalidOperationException("Coating V4 GPU check: "+message);}
    }
}
