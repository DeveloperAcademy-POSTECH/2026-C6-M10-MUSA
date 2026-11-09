using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    public sealed partial class ClayPreviewController
    {
        public bool SplatterEnabled { get; private set; }
        public float SplatterCore = .22f, SplatterLength = .24f, SplatterWidth = .043f;
        public float SplatterRoundness = 1f, SplatterIrregularity = 1f;
        public float SplatterAsymmetry = .75f;
        public int SplatterSpikes = 14, SplatterSeed = 572, SplatterDroplets;
        public float SplatterDropletSize = .018f, SplatterDropletDistance = .07f;
        Texture2D[] splatterBrushes;
        Texture2D splatterPreviewTexture;
        Image splatterPreview;
        Toggle splatterToggle;
        Label splatterStatus;
        readonly List<Action> syncSplatter = new List<Action>();
        float splatterDue = -1;
        bool UseSplatter => SplatterEnabled && ActivePreset == 3;
        bool UseReplay => !IsDefaultPalette || UseSplatter;

        void BuildSplatterUi(VisualElement panel)
        {
            AddLabel(panel, "SPLASH SHAPE", "clay-section-label");
            splatterToggle = new Toggle("Apply splash shape · coating preset") { value = SplatterEnabled };
            panel.Add(splatterToggle);
            splatterToggle.RegisterValueChangedCallback(e => { SplatterEnabled = e.newValue; ScheduleSplatter(); });
            splatterPreview = new Image { name = "splatterPreview", scaleMode = ScaleMode.ScaleToFit };
            splatterPreview.style.height = 220;
            splatterPreview.style.backgroundColor = new Color(.92f,.91f,.89f); panel.Add(splatterPreview);
            syncSplatter.Clear();
            SplashSlider(panel,"BODY FULLNESS",.12f,.3f,()=>SplatterCore,v=>SplatterCore=v);
            SplashSlider(panel,"LOBE COUNT",5,20,()=>SplatterSpikes,v=>SplatterSpikes=Mathf.RoundToInt(v));
            SplashSlider(panel,"LOBE REACH",.05f,.3f,()=>SplatterLength,v=>SplatterLength=v);
            SplashSlider(panel,"LOBE WIDTH",.015f,.08f,()=>SplatterWidth,v=>SplatterWidth=v);
            SplashSlider(panel,"LOBE SOFTNESS",0,1,()=>SplatterRoundness,v=>SplatterRoundness=v);
            SplashSlider(panel,"IRREGULARITY",0,1,()=>SplatterIrregularity,v=>SplatterIrregularity=v);
            SplashSlider(panel,"BODY ASYMMETRY",0,1,()=>SplatterAsymmetry,v=>SplatterAsymmetry=v);
            SplashSlider(panel,"DROPLET COUNT",0,12,()=>SplatterDroplets,v=>SplatterDroplets=Mathf.RoundToInt(v));
            SplashSlider(panel,"DROPLET SIZE",.005f,.04f,()=>SplatterDropletSize,v=>SplatterDropletSize=v);
            SplashSlider(panel,"DROPLET DISTANCE",0,.15f,()=>SplatterDropletDistance,v=>SplatterDropletDistance=v);
            var seed = new IntegerField("SHAPE SEED") { value = SplatterSeed, isDelayed = true }; panel.Add(seed);
            seed.RegisterValueChangedCallback(e=> { SplatterSeed=e.newValue; ScheduleSplatter(); });
            AddButton(panel,"New shape","newSplatter",()=>{ SplatterSeed = unchecked(SplatterSeed+1); seed.SetValueWithoutNotify(SplatterSeed); ScheduleSplatter(); });
            AddButton(panel,"Soft clay splash","referenceSplatter",()=>{ SetReferenceSplatter(); seed.SetValueWithoutNotify(SplatterSeed); });
            AddButton(panel,"Flowing clay","flowingClay",()=> SetOrganicSplatter(1));
            AddButton(panel,"Broad splash","broadClay",()=> SetOrganicSplatter(2));
            splatterStatus=AddLabel(panel,"Original outline · enable to compare", "clay-status");
        }
        void SplashSlider(VisualElement panel,string title,float low,float high,Func<float> get,Action<float> set)
        {
            Label label; var slider=AddSlider(panel,title,"splash"+title.Replace(" ",""),low,high,out label);
            Action sync=()=>{ slider.SetValueWithoutNotify(get()); label.text=get().ToString("0.###",System.Globalization.CultureInfo.InvariantCulture); };
            syncSplatter.Add(sync); sync();
            slider.RegisterValueChangedCallback(e=>{ set(e.newValue); sync(); ScheduleSplatter(); });
        }
        public void SetReferenceSplatter()
        {
            SplatterCore=.17f; SplatterSpikes=8; SplatterLength=.21f; SplatterWidth=.048f;
            SplatterRoundness=.85f; SplatterIrregularity=.85f; SplatterAsymmetry=.75f; SplatterSeed=572;
            SplatterDroplets=4; SplatterDropletSize=.018f; SplatterDropletDistance=.07f;
            SplatterEnabled=true; splatterToggle?.SetValueWithoutNotify(true);
            foreach(var sync in syncSplatter) sync(); ScheduleSplatter();
        }
        public void SetOrganicSplatter(int style)
        {
            SetReferenceSplatter();
            if(style==1) { SplatterSpikes=6;SplatterLength=.22f;SplatterWidth=.075f;SplatterAsymmetry=1; }
            if(style==2) { SplatterSpikes=11;SplatterLength=.12f;SplatterWidth=.07f;SplatterAsymmetry=.55f; }
            foreach(var sync in syncSplatter)sync(); ScheduleSplatter();
        }
        void ScheduleSplatter() { splatterDue=Time.realtimeSinceStartup+.25f; }
        void TickSplatter()
        {
            if(splatterDue<0 || Time.realtimeSinceStartup<splatterDue) return;
            splatterDue=-1;
            // Release users of old brushes before releasing their owned textures.
            paletteReplay?.Clear(); BindCurrentPreset(); ReleaseSplatterTextures();
            if(UseSplatter)
            {
                splatterBrushes=new Texture2D[3];
                for(int i=0;i<3;i++) splatterBrushes[i]=GenerateSplatter(SplatterSeed,i==0);
            }
            if(splatterPreview!=null) splatterPreview.image=splatterPreviewTexture;
            if(splatterStatus!=null) splatterStatus.text=UseSplatter?"Replaying fixed hits · preview only":"Original outline · select coating preset";
            RefreshPalettePreview();
        }
        Texture2D GenerateSplatter(int seed,bool preview)
        {
            const int size=512;
            var random=new System.Random(seed);
            float body=Mathf.Clamp(SplatterCore,.12f,.3f),reach=Mathf.Clamp(SplatterLength,.05f,.3f);
            float width=Mathf.Clamp(SplatterWidth,.015f,.08f),variation=Mathf.Clamp01(SplatterIrregularity);
            float asymmetry=Mathf.Clamp01(SplatterAsymmetry),softness=Mathf.Clamp01(SplatterRoundness);
            int count=Mathf.Clamp(SplatterSpikes,5,20);
            var centers=new List<Vector2>(); var radii=new List<float>();
            float phase=(float)random.NextDouble()*Mathf.PI*2;
            // Overlapping offset masses form an irregular body: no fixed circular rim.
            for(int i=0;i<3;i++)
            {
                float angle=phase+i*Mathf.PI*2/3;
                centers.Add(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*body*.35f*asymmetry);
                radii.Add(body*Mathf.Lerp(.7f,.95f,(float)random.NextDouble()));
            }
            for(int i=0;i<count;i++)
            {
                float angle=phase+(i+((float)random.NextDouble()-.5f)*.7f*variation)*Mathf.PI*2/count;
                Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),side=new Vector2(-d.y,d.x);
                float len=reach*Mathf.Lerp(1,Mathf.Lerp(.4f,1.5f,(float)random.NextDouble()),variation);
                float w=width*Mathf.Lerp(1,Mathf.Lerp(.6f,1.5f,(float)random.NextDouble()),variation);
                float bend=((float)random.NextDouble()*2-1)*asymmetry*.4f;
                for(int j=0;j<7;j++)
                {
                    float t=j/6f;
                    centers.Add(d*(body*.55f+len*t)+side*(len*bend*t*t));
                    // Thick necks and round terminal beads merge into soft lobes.
                    float radius=w*Mathf.Lerp(1.2f,Mathf.Lerp(.65f,1.35f,softness),t);
                    radius*=1-.12f*Mathf.Sin(t*Mathf.PI);
                    radii.Add(radius);
                }
            }
            float maximum=.01f;
            for(int i=0;i<centers.Count;i++)maximum=Mathf.Max(maximum,centers[i].magnitude+radii[i]);
            float fit=.43f/maximum;
            for(int i=0;i<centers.Count;i++){centers[i]*=fit;radii[i]*=fit;}
            float blend=Mathf.Lerp(.008f,.035f,softness)*fit;
            int mainCount=centers.Count;
            for(int i=0;i<Mathf.Clamp(SplatterDroplets,0,12);i++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float radius=Mathf.Clamp(SplatterDropletSize,.005f,.04f)*Mathf.Lerp(.65f,1.2f,(float)random.NextDouble());
                float dist=Mathf.Min(.47f-radius,(body+reach*.65f)*fit+Mathf.Clamp(SplatterDropletDistance,0,.15f));
                centers.Add(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*dist);radii.Add(radius);
            }
            var pixels=new Color32[size*size];var display=preview?new Color32[pixels.Length]:null;
            float aa=1f/size;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 position=new Vector2((x+.5f)/size-.5f,(y+.5f)/size-.5f);
                float distance=10;
                for(int j=0;j<centers.Count;j++)
                {
                    float next=(position-centers[j]).magnitude-radii[j];
                    if(j>=mainCount){distance=Mathf.Min(distance,next);continue;}
                    float h=Mathf.Clamp01(.5f+.5f*(next-distance)/blend);
                    distance=Mathf.Lerp(next,distance,h)-blend*h*(1-h);
                }
                byte coverage=(byte)Mathf.RoundToInt((1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-aa,aa,distance)))*255);
                byte height=(byte)Mathf.RoundToInt(Mathf.SmoothStep(0,1,Mathf.Clamp01(-distance/.025f))*255);
                int index=y*size+x;pixels[index]=new Color32(coverage,0,height,coverage);
                if(preview)display[index]=new Color32(25,35,245,coverage);
            }
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,true){name="Runtime organic clay brush",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            tex.SetPixels32(pixels);tex.Apply(false,false);
            if(preview){splatterPreviewTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};splatterPreviewTexture.SetPixels32(display);splatterPreviewTexture.Apply();}
            return tex;
        }
        static void Curve(List<Vector2> points,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {for(int i=0;i<12;i++){float t=i/12f,u=1-t;points.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}}
        void ReleaseSplatterTextures()
        { if(splatterBrushes!=null)foreach(var tex in splatterBrushes)if(tex!=null)DestroyOwned(tex);splatterBrushes=null;if(splatterPreviewTexture!=null)DestroyOwned(splatterPreviewTexture);splatterPreviewTexture=null; }
    }
}
