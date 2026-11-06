using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Bounded Editor GPU contract checks, never a substitute for device/aesthetic approval.</summary>
    public static class LabClayRuntimeChecks
    {
        const string LegacyRuntimePath="Assets/Editor/WatercolorPaint/ClaySticker/LabClayLegacyRuntimeReference.shader";
        const float MaximumSlope=.466307658f; // tan(25 degrees), the shared height-map encoding contract.
        [Serializable] public sealed class Result
        { public string unity,gpu;public string[] checks;public int firstCells,threeRehitCells,completionCells;public int checksPassed; }
        public static string Run()
        {
            Require(!EditorApplication.isPlaying&&!EditorApplication.isCompiling,"Stop Play mode and finish compilation first.");
            Require(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"A real GPU is required.");
            string random=JsonUtility.ToJson(UnityEngine.Random.state);
            var report=new Result{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceType.ToString()};
            var checks=new List<string>();
            var settings=new LabWatercolorStampSettings{mode=LabPaintStampMode.ClaySticker,revealSeconds=0};
            settings.Validate();
            var source=Resources.Load<LabConfig>("LabConfig");Require(source!=null,"Saved LabConfig missing.");
            string config=EditorJsonUtility.ToJson(source);
            LabPaintGrowthSettings rules=source.PaintGrowthSettings;rules.DryingEnabled=false;rules.GrowthPercentPerSecond=100;
            var surface=new LabPaintSurface(rules,123u);
            using(var journal=Journal(settings,surface.Columns,surface.Rows))
            using(var runtime=new LabClayStickerRuntime())
            using(var replay=new LabClayStickerRuntime())
            {
                Require(surface.AttachArtJournal(journal),"Attach real surface journal.");
                Require(surface.RegisterHit(Vector3.forward,Color.red,1,1),"Initial real hit rejected.");
                report.firstCells=surface.CoveredCount;
                Commit(journal,surface);
                string hash=Hash(surface),prefix=journal.PrefixHash;
                Finish(runtime,journal,settings,false);
                Require(Read(runtime.PaintTexture).Any(p=>p.a==255),"Original clay shape did not render opaque pigment.");
                Require(Read(runtime.HeightTexture).Any(p=>p.r>0),"Rounded clay height did not render.");
                Require(Hash(surface)==hash&&journal.PrefixHash==prefix,"Rendering changed real authority or journal.");
                checks.Add("real first hit emits opaque original silhouette and rounded height without authority changes");
                for(int hit=0;hit<3;hit++)
                {Require(surface.RegisterHit(Vector3.forward,hit%2==0?Color.blue:Color.green,2,2),"Re-hit rejected.");surface.Advance(1);}
                report.threeRehitCells=surface.CoveredCount;Commit(journal,surface);hash=Hash(surface);
                Finish(runtime,journal,settings,false);Finish(replay,journal,settings,true);
                Require(Read(runtime.PaintTexture).SequenceEqual(Read(replay.PaintTexture)),"Color differs with replay read/draw budget.");
                Require(ReadFloat(runtime.HeightTexture).SequenceEqual(ReadFloat(replay.HeightTexture)),"Height/normal/coverage differs with replay read/draw budget.");
                Require(Hash(surface)==hash,"Growth rendering changed gameplay map.");
                checks.Add("real three re-hits match bounded/full replay in color and height");
                var expected=Read(runtime.PaintTexture);var expectedHeight=ReadFloat(runtime.HeightTexture);
                runtime.HeightTexture.Release();Require(!runtime.Ready,"Lost height map incorrectly remains ready.");
                Finish(runtime,journal,settings,true);
                Require(Read(runtime.PaintTexture).SequenceEqual(expected)&&ReadFloat(runtime.HeightTexture).SequenceEqual(expectedHeight),"Height resource loss changed restored artwork.");
                checks.Add("height-only resource loss invalidates readiness and restores exact artwork");
                for(int hit=0;hit<100&&!surface.IsComplete;hit++)
                {Require(surface.RegisterHit(Vector3.forward,hit%2==0?Color.red:Color.blue,1,1),"Completion hit rejected.");surface.Advance(1);}
                Require(surface.IsComplete,"Actual paid growth cannot reach completion.");
                report.completionCells=surface.CoveredCount;Commit(journal,surface);
                Require(journal.Seal(surface.Revision,Hash(surface)),"Seal completed surface.");hash=Hash(surface);
                Finish(runtime,journal,settings,false);
                Require(Read(runtime.PaintTexture).All(p=>p.a==255),"Completed actual surface retains unpainted visual gaps.");
                Require(Hash(surface)==hash,"Completion display changed actual gameplay map.");
                checks.Add("100 percent actual completion fills every output texel with opaque clay");
            }
            CheckStrength(settings,checks);
            CheckErase(settings,checks);
            CheckOldStrokes(settings,checks);
            CheckDenseSupport(settings,checks);
            CheckLegacyColorAndRelief(settings,checks);
            CheckLocalCoatingNormals(checks);
            LabClayVisualStudy.CheckLocalGlossContracts(checks);
            Require(random==JsonUtility.ToJson(UnityEngine.Random.state),"Clay renderer consumed Unity RNG.");
            Require(config==EditorJsonUtility.ToJson(source),"Clay checks changed saved LabConfig.");
            checks.Add("Unity RNG and saved configuration remain unchanged");
            report.checks=checks.ToArray();report.checksPassed=checks.Count;return JsonUtility.ToJson(report,true);
        }
        static void CheckDenseSupport(LabWatercolorStampSettings settings,List<string> checks)
        {
            using(var journal=Journal(settings,64,32))using(var runtime=new LabClayStickerRuntime())
            {
                const int center=16*64+32;
                Append(journal,Stamp(Color.blue,1),new[]{center});Finish(runtime,journal,settings,true);
                var logical=ReadField<Color32[]>(runtime,"supportPixels");
                var shown=ReadField<Color32[]>(runtime,"displayedSupportPixels");
                var counts=ReadField<byte[]>(runtime,"supportCounts");
                Require(logical.Count(p=>p.a!=0)==1,"Display halo changed the actual logical support.");
                Require(shown.Count(p=>p.a!=0)==25&&counts.Count(c=>c!=0)==25,
                    "An isolated interior cell must support every cell of its inclusive 5x5 neighborhood.");
                for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
                {
                    int i=(16+dy)*64+32+dx;
                    Require(shown[i].a==255&&counts[i]==1,"Dense halo skipped an intermediate +/-1 cell.");
                }
                // With the previous sparse {-2,0,2} shader, this exact diagonal +1/+1
                // location fell between every support sample and became a transparent gap.
                var frame=Read(runtime.PaintTexture);int scale=runtime.PaintTexture.width/64;
                int x=33*scale+scale/2,y=17*scale+scale/2;
                Require(frame[y*runtime.PaintTexture.width+x].a>240,
                    "The actual GPU display still cuts a gap at the original silhouette's +1/+1 support cell.");
                Require(journal.AppendErase(center,2)&&journal.Commit(2,new string('b',64)),"Commit isolated drying.");
                Finish(runtime,journal,settings,true);
                Require(ReadField<byte[]>(runtime,"supportCounts").All(c=>c==0),"Support counts survived complete drying.");
                Require(ReadField<Color32[]>(runtime,"displayedSupportPixels").All(p=>p.a==0),"Displayed support survived complete drying.");

                // Wrap longitude and clamp the pole. Overlapping sources must count only
                // unique targets, remove symmetrically, and keep the still-live neighbor.
                var pole=Stamp(Color.red,2);pole.center=Direction(0,64,32);
                pole.right=Vector3.ProjectOnPlane(Vector3.forward,pole.center).normalized;
                Append(journal,pole,new[]{0,1});Finish(runtime,journal,settings,true);
                shown=ReadField<Color32[]>(runtime,"displayedSupportPixels");counts=ReadField<byte[]>(runtime,"supportCounts");
                Require(shown[63].a==255&&shown[0].a==255&&counts[0]==2&&counts.Max(c=>(int)c)<=25,
                    "Wrapped/clamped overlapping support has duplicate or missing targets.");
                Require(journal.AppendErase(0,3)&&journal.Commit(3,new string('c',64)),"Commit one pole source drying.");
                Finish(runtime,journal,settings,true);
                Require(ReadField<byte[]>(runtime,"supportCounts")[0]==1
                    &&ReadField<Color32[]>(runtime,"displayedSupportPixels")[0].a==255,
                    "Drying one source deleted its neighbor's still-live support.");
                Require(journal.AppendErase(1,4)&&journal.Commit(4,new string('d',64)),"Commit final pole drying.");
                Finish(runtime,journal,settings,true);
                Require(ReadField<byte[]>(runtime,"supportCounts").All(c=>c==0)
                    &&ReadField<Color32[]>(runtime,"displayedSupportPixels").All(p=>p.a==0),
                    "Pole clamping/wrapping Add and Sub are asymmetric.");
                checks.Add("dense two-cell halo has no +/-1 GPU gaps, preserves logical cells, and dries symmetrically at seams/poles");
            }
        }
        static T ReadField<T>(LabClayStickerRuntime runtime,string name)
        {
            var field=typeof(LabClayStickerRuntime).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Require(field!=null,"Missing clay support cache "+name);return (T)field.GetValue(runtime);
        }
        static void CheckStrength(LabWatercolorStampSettings settings,List<string> checks)
        {
            using(var weak=Journal(settings,64,32))using(var strong=Journal(settings,64,32))
            using(var a=new LabClayStickerRuntime())using(var b=new LabClayStickerRuntime())
            {
                var low=Stamp(Color.blue,1);low.impact01=1;var high=low;high.impact01=255;
                int[] cells=Rectangle(64,24,40,10,22);
                Append(weak,low,cells);Append(strong,high,cells);
                Finish(a,weak,settings,true);Finish(b,strong,settings,true);
                Color32[] x=Read(a.PaintTexture),y=Read(b.PaintTexture);
                int count=0;long lx=0,ly=0;
                for(int i=0;i<x.Length;i++)if(x[i].a==255&&y[i].a==255)
                {count++;lx+=x[i].r+x[i].g+x[i].b;ly+=y[i].r+y[i].g+y[i].b;}
                Require(count>100&&lx>ly,"Weak clay must be lighter pigment, not transparent or identical.");
                Require(x.Select(p=>p.a).SequenceEqual(y.Select(p=>p.a)),"Impact strength changed coverage alpha.");
                checks.Add("impact changes opaque pigment density while preserving identical shape alpha");
            }
        }
        internal static void CheckErase(LabWatercolorStampSettings settings,List<string> checks,Func<LabClayStickerRuntime> factory=null)
        {
            using(var journal=Journal(settings,64,32))using(var runtime=factory?.Invoke()??new LabClayStickerRuntime())
            {
                int[] cells=Rectangle(64,24,40,10,22);Append(journal,Stamp(Color.red,1),cells);Finish(runtime,journal,settings,false);
                foreach(int cell in cells)Require(journal.AppendErase(cell,9),"Record erase.");
                Require(journal.Commit(9,new string('a',64)),"Commit erasure.");Finish(runtime,journal,settings,true);
                Require(Read(runtime.PaintTexture).All(p=>p.a==0),"All dried clay retains a phantom outside halo.");
                Require(ReadFloat(runtime.HeightTexture).All(p=>p.Equals(Color.clear)),"All dried clay retains old height, local normals or coating support.");
                Append(journal,Stamp(Color.blue,2),cells);Finish(runtime,journal,settings,false);
                var blue=Read(runtime.PaintTexture).Where(p=>p.a==255).ToArray();
                Require(blue.Length>100&&blue.All(p=>p.b>=p.r),"Erased red layers leaked through blue repaint.");
                checks.Add("drying clears every accumulated layer and outside halo; repaint cannot revive old pigment");
            }
        }
        internal static void CheckOldStrokes(LabWatercolorStampSettings settings,List<string> checks,Func<LabClayStickerRuntime> factory=null)
        {
            using(var journal=Journal(settings,64,32))using(var runtime=factory?.Invoke()??new LabClayStickerRuntime())using(var replay=factory?.Invoke()??new LabClayStickerRuntime())
            {
                uint first=0;
                for(int i=0;i<129;i++)
                {
                    int cell=(12+i/34)*64+15+i%34;
                    var stamp=Stamp(i%2==0?Color.red:Color.blue,(uint)i+1);stamp.center=Direction(cell,64,32);
                    stamp.right=Vector3.ProjectOnPlane(Vector3.forward,stamp.center).normalized;stamp.radius=.18f;
                    uint id=journal.BeginStroke(stamp);Require(id!=0,"Begin retained stroke.");if(i==0)first=id;
                    Require(journal.AppendPaint(id,cell,(uint)i+1),"Append retained cell.");
                    Require(journal.Commit((uint)i+1,new string('a',64)),"Commit retained stroke.");
                }
                // Grow stroke 1 after the bounded 128-descriptor cache has evicted it.
                Require(journal.AppendPaint(first,17*64+32,130),"Append old growth.");
                Require(journal.Commit(130,new string('b',64)),"Commit old growth.");
                Finish(runtime,journal,settings,false);Finish(replay,journal,settings,true);
                Require(Read(runtime.PaintTexture).SequenceEqual(Read(replay.PaintTexture)),"129-stroke artwork or old growth differs after replay.");
                Require(ReadFloat(runtime.HeightTexture).SequenceEqual(ReadFloat(replay.HeightTexture)),"129-stroke height/normal/coverage differs after replay.");
                Require(runtime.AppliedThrough==journal.LastSequence&&runtime.RenderedThrough==journal.LastSequence,"Read/application/GPU completion were conflated.");
                checks.Add("129 strokes and old-stroke growth survive bounded descriptor eviction and exact replay");
            }
        }
        static void CheckLegacyColorAndRelief(LabWatercolorStampSettings settings,List<string> checks)
        {
            Shader reference=AssetDatabase.LoadAssetAtPath<Shader>(LegacyRuntimePath);
            Require(reference!=null&&reference.isSupported,"The immutable v1 GPU compositor reference is unavailable.");
            using(var journal=Journal(settings,64,32))using(var current=new LabClayStickerRuntime())using(var legacy=new LabClayStickerRuntime())
            {
                // Each runtime owns this material. Swap only the disposable reference
                // instance after its empty preparation, never a Resources/shared asset.
                Finish(legacy,journal,settings,true);
                ReadField<Material>(legacy,"material").shader=reference;
                Append(journal,Stamp(Color.red,1),Rectangle(64,24,32,10,22));
                Finish(current,journal,settings,false);Finish(legacy,journal,settings,true);
                CompareLegacyChannels(current,legacy,"first original stamp");
                for(int i=0;i<3;i++)
                {
                    var stamp=Stamp(i==1?Color.red:Color.blue,(uint)(i+2));
                    stamp.center=Quaternion.AngleAxis(i*7+8,Vector3.up)*Vector3.right;
                    stamp.right=Vector3.ProjectOnPlane(Vector3.forward,stamp.center).normalized;
                    Append(journal,stamp,Rectangle(64,32+i*3,35+i*3,10,22));
                    Finish(current,journal,settings,false);Finish(legacy,journal,settings,true);
                    CompareLegacyChannels(current,legacy,"overlap "+i);
                }
                // Complete every paid cell so the unchanged legacy undercoat R rule is
                // checked too. GB/A deliberately differ: they are the new coating data.
                var occupied=ReadField<Color32[]>(current,"supportPixels");
                int[] remaining=Enumerable.Range(0,occupied.Length).Where(i=>occupied[i].a==0).ToArray();
                Append(journal,Stamp(Color.green,9),remaining);
                Require(journal.Seal(2,new string('b',64)),"Seal legacy comparison.");
                Finish(current,journal,settings,false);Finish(legacy,journal,settings,true);
                CompareLegacyChannels(current,legacy,"complete undercoat");
                checks.Add("v1 GPU reference preserves exact pigment RGBA and displacement R through first, overlaps and complete undercoat");
            }
        }
        static void CompareLegacyChannels(LabClayStickerRuntime current,LabClayStickerRuntime legacy,string fixture)
        {
            Require(Read(current.PaintTexture).SequenceEqual(Read(legacy.PaintTexture)),"v1 pigment calculation changed at "+fixture+".");
            Color[] a=ReadFloat(current.HeightTexture),b=ReadFloat(legacy.HeightTexture);
            Require(a.Length==b.Length&&a.Select(p=>p.r).SequenceEqual(b.Select(p=>p.r)),"v1 displacement R changed at "+fixture+".");
            CheckNormalEncoding(a,fixture);
        }
        static void CheckLocalCoatingNormals(List<string> checks)
        {
            Shader shader=Resources.Load<Shader>(LabClayStickerRuntime.ShaderResource);
            Require(shader!=null&&shader.isSupported,"Current clay compositor is unavailable.");
            var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            RenderTexture blank=null,lower=null,onlyTop=null,overlap=null;
            RenderTexture savedActive=RenderTexture.active;
            try
            {
                blank=Target();lower=Target();onlyTop=Target();overlap=Target();
                var previous=RenderTexture.active;
                try{RenderTexture.active=blank;GL.Clear(false,true,Color.clear);}finally{RenderTexture.active=previous;}
                Vector3[] centers={Vector3.right,Vector3.left,new Vector3(.015f,.999f,0).normalized};
                foreach(string name in new[]{"BroadData","LongData","ClusterData"})
                foreach(Vector3 center in centers)
                {
                    var brush=Resources.Load<Texture2D>(LabClayStickerSettings.ResourceFolder+name);
                    Require(brush!=null,"Missing original brush for local-normal check: "+name);
                    SetDirectStamp(material,brush,center,0);
                    Graphics.Blit(blank,onlyTop,material,1);
                    Color[] pixels=ReadFloat(onlyTop);CheckNormalEncoding(pixels,name);
                    Require(pixels.Count(p=>p.a>.05f&&new Vector2(p.g/p.a-.5f,p.b/p.a-.5f).sqrMagnitude>.0004f)>32,
                        "Local normal is neutral across the original sticker interior: "+name+".");
                }
                var broad=Resources.Load<Texture2D>(LabClayStickerSettings.ResourceFolder+"BroadData");
                var top=Resources.Load<Texture2D>(LabClayStickerSettings.ResourceFolder+"LongData");
                SetDirectStamp(material,broad,Vector3.right,-25);Graphics.Blit(blank,lower,material,1);
                SetDirectStamp(material,top,Quaternion.AngleAxis(12,Vector3.up)*Vector3.right,35);
                Graphics.Blit(blank,onlyTop,material,1);Graphics.Blit(lower,overlap,material,1);
                Color[] solo=ReadFloat(onlyTop),stack=ReadFloat(overlap);int compared=0,raised=0;
                for(int i=0;i<solo.Length;i++)if(solo[i].a>=.9999f)
                {
                    compared++;
                    Require(Mathf.Abs(solo[i].g/solo[i].a-stack[i].g/stack[i].a)<.002f
                        &&Mathf.Abs(solo[i].b/solo[i].a-stack[i].b/stack[i].a)<.002f,
                        "A covered lower sheet altered the top sticker's local normal.");
                    if(stack[i].r>solo[i].r+.001f)raised++;
                }
                Require(compared>100&&raised>100,"The local-normal comparison did not actually cover overlapping raised stickers.");
                CheckNormalEncoding(stack,"top-sticker overlap");
                checks.Add("all original brushes retain non-neutral finite local slopes bounded to 25 degrees at seam/pole fixtures");
                checks.Add("fully covered top-sticker normals match the isolated top while legacy lower-sheet height remains present");
            }
            finally
            {
                // Direct Blit fixtures leave their destination bound; unbind before disposal.
                RenderTexture.active=savedActive;
                foreach(var texture in new[]{blank,lower,onlyTop,overlap})if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}
                Object.DestroyImmediate(material);
            }
        }
        static void SetDirectStamp(Material material,Texture2D brush,Vector3 center,float rotation)
        {
            Vector3 right=Vector3.ProjectOnPlane(Vector3.forward,center).normalized;
            right=Quaternion.AngleAxis(rotation,center)*right;
            material.SetTexture("_Brush",brush);material.SetVector("_Center",center);material.SetVector("_Right",right);
            material.SetVector("_Up",Vector3.Cross(center,right));material.SetFloat("_Radius",.7f);
        }
        static void CheckNormalEncoding(Color[] pixels,string fixture)
        {
            foreach(Color pixel in pixels)
            {
                if(!Finite(pixel.r)||!Finite(pixel.g)||!Finite(pixel.b)||!Finite(pixel.a))
                    throw new InvalidOperationException("Non-finite local coating data at "+fixture+".");
                if(pixel.a<0||pixel.a>1.001f||pixel.g<0||pixel.b<0||pixel.g>pixel.a+.001f||pixel.b>pixel.a+.001f)
                    throw new InvalidOperationException("Coating slopes escaped premultiplied coverage at "+fixture+".");
                if(pixel.a<=.01f)continue; // Avoid amplifying subnormal half/UNorm edge quantization.
                Vector2 slope=new Vector2(pixel.g/pixel.a*2-1,pixel.b/pixel.a*2-1)*MaximumSlope;
                if(slope.magnitude>MaximumSlope*1.015f)
                    throw new InvalidOperationException("The local coating normal exceeds the 25-degree bound at "+fixture+".");
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static RenderTexture Target()
        {
            var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
            var target=new RenderTexture(512,256,0,format,RenderTextureReadWrite.Linear)
                {filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            Require(target.Create(),"Could not allocate local-normal GPU fixture.");return target;
        }
        static LabPaintArtJournal Journal(LabWatercolorStampSettings settings,int columns,int rows)=>new LabPaintArtJournal(
            Path.Combine(Application.temporaryCachePath,"clay-check-"+Guid.NewGuid().ToString("N")),"clay-check",columns,rows,settings.Version);
        static LabPaintVisualStamp Stamp(Color color,uint seed)=>new LabPaintVisualStamp
        {sequence=1,center=Vector3.right,right=Vector3.forward,radius=.7f,color=color,painterSlot=1,pattern=LabPaintPattern.Splatter,seed=seed,impact01=128};
        static int[] Rectangle(int columns,int left,int right,int bottom,int top)=>Enumerable.Range(bottom,top-bottom).SelectMany(y=>Enumerable.Range(left,right-left).Select(x=>y*columns+x)).ToArray();
        static Vector3 Direction(int cell,int columns,int rows)
        {float y=1-2*(cell/columns+.5f)/rows,a=((cell%columns+.5f)/columns-.5f)*Mathf.PI*2,r=Mathf.Sqrt(Mathf.Max(0,1-y*y));return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);}
        static void Append(LabPaintArtJournal journal,LabPaintVisualStamp stamp,int[] cells)
        {uint id=journal.BeginStroke(stamp);Require(id!=0,"Begin clay stroke.");foreach(int cell in cells)Require(journal.AppendPaint(id,cell,1),"Append clay cell.");Require(journal.Commit(1,new string('a',64)),"Commit clay cells.");}
        static void Commit(LabPaintArtJournal journal,LabPaintSurface surface)=>Require(journal.Commit(surface.Revision,Hash(surface)),"Commit real surface.");
        static string Hash(LabPaintSurface surface)
        {var colors=new Color32[surface.CellCount];var slots=new byte[surface.CellCount];surface.CopyPixels(colors);surface.CopyPainterSlots(slots);return LabPaintPublisher.Hash(colors,slots);}
        static void Finish(LabClayStickerRuntime runtime,LabPaintArtJournal journal,LabWatercolorStampSettings settings,bool full)
        {
            for(int i=0;i<4096;i++)
            {
                RenderTexture previous=RenderTexture.active;
                try{runtime.Update(journal,null,null,1024,settings,i*.016,full);}
                finally{RenderTexture.active=previous;}
                Require(runtime.Error==null,runtime.Error??"Clay error.");
                if(runtime.Ready)return;
                // This is a stopped-Editor contract runner only. The actual runtime uses
                // asynchronous callbacks and never blocks its frame on this GPU wait.
                AsyncGPUReadback.WaitAllRequests();
            }
            throw new InvalidOperationException("Clay replay did not finish within the bounded Editor check.");
        }
        static Color32[] Read(RenderTexture texture)
        {
            var previous=RenderTexture.active;var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
            try{RenderTexture.active=texture;copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();return copy.GetPixels32();}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(copy);}
        }
        static Color[] ReadFloat(RenderTexture texture)
        {
            var previous=RenderTexture.active;var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true);
            try{RenderTexture.active=texture;copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();return copy.GetPixels();}
            finally{RenderTexture.active=previous;Object.DestroyImmediate(copy);}
        }
        static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    }
}
