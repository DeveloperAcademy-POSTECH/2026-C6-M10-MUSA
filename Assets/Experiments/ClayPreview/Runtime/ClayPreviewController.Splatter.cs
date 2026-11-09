using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hapio.Experiments
{
    public sealed partial class ClayPreviewController
    {
        public bool restoreSavedShapeOnStart = true;
        public ClayPreviewShapeSource ShapeSource { get; private set; }
        public bool SplatterEnabled => ShapeSource != ClayPreviewShapeSource.Original;
        public bool ShapeAvailable => PaletteAvailable && PresetCount == 4;
        public string ShapeError { get; private set; }
        public int ShapeRevision { get; private set; }
        public bool ShapeBusy => splatterDue >= 0 || PaletteBusy;
        public bool ShapeReady => !ShapeBusy && string.IsNullOrEmpty(ShapeError);
        public string ShapeImageFileName => imageShapeName;
        public string ShapeProfilePath => ClayPreviewShapeProfile.DefaultPath;
        public Texture2D ShapeMaskTexture => publishedShape?.brushes != null ? publishedShape.brushes[0] : null;
        public Texture2D ShapePreviewTexture => publishedShape?.preview;
        public float SplatterCore = .1625f, SplatterLength = .21f, SplatterWidth = .048f;
        public float SplatterRoundness = .85f, SplatterIrregularity = 1f, SplatterAsymmetry = 1f;
        public int SplatterSpikes = 9, SplatterSeed = 572, SplatterDroplets = 4;
        public float SplatterDropletSize = .04f, SplatterDropletDistance = .1438f;
        readonly List<Action> syncSplatter = new List<Action>();
        DropdownField shapeSourceField;
        VisualElement proceduralControls, shapeModule;
        Image splatterPreview;
        Label splatterStatus, shapeFileLabel;
        IntegerField shapeSeedField;
        byte[] imageShapeBytes;
        string imageShapeName;
        Texture2D generatedPreviewTexture;
        ShapeBundle requestedShape, publishedShape;
        ClayPreviewShapeProfile committedShape;
        float splatterDue = -1;
        bool UseSplatter => SplatterEnabled && ActivePreset == 3 && ShapeAvailable;
        bool UseReplay => !IsDefaultPalette || UseSplatter || ComparisonNeedsReplay || OmitShapeUndercoat;
        partial void BuildShapeDiagnosticsUi(VisualElement panel);
        partial void TickShapeDiagnostics();
        partial void ReleaseShapeDiagnostics();

        sealed class ShapeBundle : IDisposable
        {
            public Texture2D[] brushes;
            public Texture2D preview;
            public ClayPreviewShapeMask.Result image;
            public ClayPreviewShapeProfile profile;
            public void Dispose()
            {
                if (image != null) { image.Dispose(); image = null; }
                else
                {
                    if (brushes != null)
                        foreach (Texture2D brush in brushes) if (brush != null) DestroyOwned(brush);
                    if (preview != null) DestroyOwned(preview);
                }
                brushes = null; preview = null;
            }
        }

        void BuildSplatterUi(VisualElement panel)
        {
            AddLabel(panel, "STICKER OUTLINE / 스티커 외곽", "clay-section-label");
            shapeModule = new VisualElement { name = "shapeModule" }; panel.Add(shapeModule);
            shapeSourceField = new DropdownField("OUTLINE SOURCE", new List<string> { "Original / 기존 윤곽", "Image alpha / 이미지 원형", "Procedural / 절차 생성" }, (int)ShapeSource) { name = "shapeSource" };
            shapeModule.Add(shapeSourceField);
            shapeSourceField.RegisterValueChangedCallback(e => SetShapeSource((ClayPreviewShapeSource)shapeSourceField.index));
            AddButton(shapeModule, "Choose outline PNG…", "chooseShapeImage", PickShapeImage);
            shapeFileLabel = AddLabel(shapeModule, "No outline PNG · texture files are a separate control", "clay-status");
            splatterPreview = new Image { name = "splatterPreview", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            splatterPreview.AddToClassList("clay-file-thumbnail"); splatterPreview.style.height = 220; shapeModule.Add(splatterPreview);
            AddLabel(shapeModule, "MASK: 512 × 512 px · PNG alpha only · fixed 32 px padding · aspect ratio preserved.", "clay-status");
            AddLabel(shapeModule, "표시 최대 범위 = 실제 도색 셀 + 상하·좌우·대각선 2셀의 합집합. 범위 안에서만 표시되며 밖은 잘립니다. 한 셀 기준 5×5는 예시이고 고정 사각 규격이 아닙니다.", "clay-status");
            AddLabel(shapeModule, "현재 비교 지도: 256×128 = 32,768셀. 100% 완료에서는 표시 지원 제한을 우회하고 빈틈 바탕을 채웁니다. 표시 여유는 게임 도색 면적에 포함되지 않습니다.", "clay-status");
            proceduralControls = new Foldout { text = "Procedural shape settings / 생성 모양", value = false, name = "proceduralShapeSettings" }; shapeModule.Add(proceduralControls);
            syncSplatter.Clear();
            SplashSlider(proceduralControls,"BODY FULLNESS",.12f,.3f,()=>SplatterCore,v=>SplatterCore=v);
            SplashSlider(proceduralControls,"LOBE COUNT",5,20,()=>SplatterSpikes,v=>SplatterSpikes=Mathf.RoundToInt(v));
            SplashSlider(proceduralControls,"LOBE REACH",.05f,.3f,()=>SplatterLength,v=>SplatterLength=v);
            SplashSlider(proceduralControls,"LOBE WIDTH",.015f,.08f,()=>SplatterWidth,v=>SplatterWidth=v);
            SplashSlider(proceduralControls,"LOBE SOFTNESS",0,1,()=>SplatterRoundness,v=>SplatterRoundness=v);
            SplashSlider(proceduralControls,"IRREGULARITY",0,1,()=>SplatterIrregularity,v=>SplatterIrregularity=v);
            SplashSlider(proceduralControls,"BODY ASYMMETRY",0,1,()=>SplatterAsymmetry,v=>SplatterAsymmetry=v);
            SplashSlider(proceduralControls,"DROPLET COUNT",0,12,()=>SplatterDroplets,v=>SplatterDroplets=Mathf.RoundToInt(v));
            SplashSlider(proceduralControls,"DROPLET SIZE",.005f,.04f,()=>SplatterDropletSize,v=>SplatterDropletSize=v);
            SplashSlider(proceduralControls,"DROPLET DISTANCE",0,.15f,()=>SplatterDropletDistance,v=>SplatterDropletDistance=v);
            shapeSeedField = new IntegerField("SHAPE SEED") { name = "shapeSeed", value = SplatterSeed, isDelayed = true }; proceduralControls.Add(shapeSeedField);
            shapeSeedField.RegisterValueChangedCallback(e => { SplatterSeed = e.newValue; ScheduleSplatter(); });
            AddButton(proceduralControls,"New shape","newSplatter",()=> { SplatterSeed = unchecked(SplatterSeed+1); SyncShapeUi(); ScheduleSplatter(); });
            AddButton(proceduralControls,"Saved reference settings","referenceSplatter",SetReferenceSplatter);
            AddButton(proceduralControls,"Flowing clay","flowingClay",()=> SetOrganicSplatter(1));
            AddButton(proceduralControls,"Broad splash","broadClay",()=> SetOrganicSplatter(2));
            AddButton(shapeModule, "Save as startup shape", "saveShapeProfile", () => SaveShapeProfile());
            AddButton(shapeModule, "Reload saved shape", "loadShapeProfile", () => LoadShapeProfile());
            AddButton(shapeModule, "Reset shape defaults", "resetShapeProfile", ResetShapeProfile);
            splatterStatus = AddLabel(panel, string.Empty, "clay-status"); splatterStatus.name = "shapeStatus";
            BuildShapeDiagnosticsUi(panel);
            SyncShapeUi();
        }

        void SplashSlider(VisualElement panel,string title,float low,float high,Func<float> get,Action<float> set)
        {
            Label label; var slider=AddSlider(panel,title,"splash"+title.Replace(" ",""),low,high,out label);
            Action sync=()=> { slider.SetValueWithoutNotify(get()); label.text=get().ToString("0.###",CultureInfo.InvariantCulture); };
            syncSplatter.Add(sync); sync();
            slider.RegisterValueChangedCallback(e=> { set(e.newValue); sync(); ScheduleSplatter(); });
        }

        public bool SetShapeSource(ClayPreviewShapeSource source)
        {
            if (!ShapeAvailable) return ShapeFailure("Open the four-preset coating or image comparison with its palette record.");
            if (!Enum.IsDefined(typeof(ClayPreviewShapeSource), source)) return ShapeFailure("Unknown outline source.");
            if (source == ClayPreviewShapeSource.Image && imageShapeBytes == null)
                return ShapeFailure("Choose a transparent outline PNG first. Surface texture files do not define the outline.");
            ShapeSource = source; ShapeError = null;
            if (source != ClayPreviewShapeSource.Original) SelectCoatingWithoutAppearanceReset();
            SyncShapeUi(); ScheduleSplatter(); return true;
        }

        void SelectCoatingWithoutAppearanceReset()
        {
            if (ActivePreset == 3) return;
            float thickness = Thickness, gloss = Gloss, contact = ContactStrength, detail = DetailStrength;
            float azimuth = LightAzimuth, elevation = LightElevation, intensity = LightIntensity;
            SelectPreset(3);
            SetAppearance(thickness, gloss); SetSurfaceDetails(contact, detail); SetLighting(azimuth, elevation, intensity);
        }

        public void PickShapeImage()
        {
            if (!ShapeAvailable) { ShapeFailure("Open the four-preset image comparison first."); return; }
            manipulation?.Cancel(); AcceptShapeImage(ClayPreviewShapeMask.PickAndLoad());
        }
        public bool LoadShapeImageFile(string path) => AcceptShapeImage(ClayPreviewShapeMask.LoadFile(path));
        public bool LoadShapeImageBytes(byte[] bytes, string name) => AcceptShapeImage(ClayPreviewShapeMask.LoadBytes(bytes, name));
        bool AcceptShapeImage(ClayPreviewShapeMask.Result loaded)
        {
            using (loaded)
            {
                if (!ShapeAvailable) return ShapeFailure("Open the four-preset coating or image comparison first.");
                if (loaded == null) return ShapeFailure("No outline image was supplied.");
                if (loaded.Cancelled) return false;
                if (!loaded.Succeeded) return ShapeFailure(loaded.Error);
                imageShapeBytes = (byte[])loaded.SourceBytes.Clone(); imageShapeName = loaded.FileName;
                return SetShapeSource(ClayPreviewShapeSource.Image);
            }
        }

        public ClayPreviewShapeProfile CaptureShapeProfile()
        {
            var profile = new ClayPreviewShapeProfile { source = ShapeSource, core = SplatterCore, length = SplatterLength, width = SplatterWidth,
                roundness = SplatterRoundness, irregularity = SplatterIrregularity, asymmetry = SplatterAsymmetry,
                spikes = SplatterSpikes, seed = SplatterSeed, droplets = SplatterDroplets,
                dropletSize = SplatterDropletSize, dropletDistance = SplatterDropletDistance };
            if (imageShapeBytes != null)
            {
                profile.imagePngBase64 = Convert.ToBase64String(imageShapeBytes);
                profile.imageSha256 = ClayPreviewShapeMask.Hash(imageShapeBytes); profile.imageFileName = imageShapeName;
            }
            return profile;
        }

        public bool SaveShapeProfile(string path = null)
        {
            if (ShapeBusy) return ShapeFailure("Wait until the current outline replay finishes before saving.");
            if (!ClayPreviewShapeProfile.TrySave(path ?? ShapeProfilePath, CaptureShapeProfile(), out string error)) return ShapeFailure(error);
            ShapeError = null; SyncShapeUi(); return true;
        }
        public bool LoadShapeProfile(string path = null)
        {
            if (!ShapeAvailable) return ShapeFailure("Open the four-preset comparison first.");
            if (!ClayPreviewShapeProfile.TryLoad(path ?? ShapeProfilePath, out ClayPreviewShapeProfile profile, out string error)) return ShapeFailure(error);
            return ApplyShapeProfile(profile);
        }
        public void ResetShapeProfile() => ApplyShapeProfile(new ClayPreviewShapeProfile());
        bool ApplyShapeProfile(ClayPreviewShapeProfile profile)
        {
            if (!ShapeAvailable) return ShapeFailure("Open the four-preset coating or image comparison first.");
            if (!profile.Validate(out string error)) return ShapeFailure(error);
            byte[] bytes = null;
            if (!string.IsNullOrEmpty(profile.imagePngBase64))
            {
                if (!profile.TryGetImageBytes(out bytes, out error)) return ShapeFailure(error);
                using (var loaded = ClayPreviewShapeMask.LoadBytes(bytes, profile.imageFileName))
                    if (!loaded.Succeeded) return ShapeFailure(loaded.Error);
            }
            ApplyShapeFields(profile, bytes);
            if (ShapeSource != ClayPreviewShapeSource.Original) SelectCoatingWithoutAppearanceReset();
            ShapeError = null; SyncShapeUi(); ScheduleSplatter(); return true;
        }
        void ApplyShapeFields(ClayPreviewShapeProfile profile, byte[] bytes)
        {
            ShapeSource = profile.source; SplatterCore = profile.core; SplatterLength = profile.length; SplatterWidth = profile.width;
            SplatterRoundness = profile.roundness; SplatterIrregularity = profile.irregularity; SplatterAsymmetry = profile.asymmetry;
            SplatterSpikes = profile.spikes; SplatterSeed = profile.seed; SplatterDroplets = profile.droplets;
            SplatterDropletSize = profile.dropletSize; SplatterDropletDistance = profile.dropletDistance;
            imageShapeBytes = bytes; imageShapeName = profile.imageFileName;
        }
        void RestoreStartupShape()
        {
            committedShape = CaptureShapeProfile();
            if (restoreSavedShapeOnStart && ShapeAvailable && File.Exists(ShapeProfilePath)) LoadShapeProfile();
        }
        bool ShapeFailure(string error)
        { ShapeError = error ?? "The outline could not be applied. Previous result kept."; SyncShapeUi(); return false; }
        void SyncShapeUi()
        {
            shapeSourceField?.SetValueWithoutNotify(ShapeSource == ClayPreviewShapeSource.Original ? "Original / 기존 윤곽"
                : ShapeSource == ClayPreviewShapeSource.Image ? "Image alpha / 이미지 원형" : "Procedural / 절차 생성");
            shapeModule?.SetEnabled(ShapeAvailable);
            proceduralControls?.SetEnabled(ShapeSource == ClayPreviewShapeSource.Procedural);
            shapeSeedField?.SetValueWithoutNotify(SplatterSeed);
            foreach (var sync in syncSplatter) sync();
            if (splatterPreview != null) splatterPreview.image = ShapePreviewTexture;
            if (shapeFileLabel != null) shapeFileLabel.text = imageShapeBytes == null ? "No outline PNG · texture files are a separate control" : imageShapeName + " · original alpha outline";
            if (splatterStatus == null) return;
            splatterStatus.EnableInClassList("clay-error", !string.IsNullOrEmpty(ShapeError));
            splatterStatus.text = !ShapeAvailable ? "Shape controls need the four-preset coating/image comparison."
                : !string.IsNullOrEmpty(ShapeError) ? ShapeError + " · previous valid picture is kept"
                : ShapeBusy ? "Updating outline… previous completed picture is kept"
                : ActivePreset != 3 && SplatterEnabled ? "Selected outline is used only by preset 4 · current preset keeps its original outline"
                : ShapeSource == ClayPreviewShapeSource.Original ? "Original frozen outline · no imported reference is assumed"
                : ShapeSource == ClayPreviewShapeSource.Image ? "Image alpha applied · inspect support/cut-off diagnostics below"
                : "Procedural approximation · all settings and seed are saved together";
        }
        public void SetReferenceSplatter()
        {
            var profile = new ClayPreviewShapeProfile { source = ClayPreviewShapeSource.Procedural };
            if (imageShapeBytes != null)
            { profile.imagePngBase64 = Convert.ToBase64String(imageShapeBytes); profile.imageFileName = imageShapeName; profile.imageSha256 = ClayPreviewShapeMask.Hash(imageShapeBytes); }
            ApplyShapeProfile(profile);
        }
        public void SetOrganicSplatter(int style)
        {
            if (!ShapeAvailable) { ShapeFailure("Open the four-preset comparison first."); return; }
            SetReferenceSplatter();
            if(style==1) { SplatterSpikes=6;SplatterLength=.22f;SplatterWidth=.075f;SplatterAsymmetry=1; }
            if(style==2) { SplatterSpikes=11;SplatterLength=.12f;SplatterWidth=.07f;SplatterAsymmetry=.55f; }
            SyncShapeUi(); ScheduleSplatter();
        }
        void ScheduleSplatter()
        {
            // A newer file/slider selection invalidates the old pending GPU request immediately.
            paletteReplay?.CancelPending();
            if (requestedShape != null && requestedShape != publishedShape) requestedShape.Dispose();
            requestedShape = publishedShape;
            splatterDue = Time.realtimeSinceStartup + .25f;
        }
        void TickSplatter()
        {
            if (splatterDue < 0 || Time.realtimeSinceStartup < splatterDue) return;
            splatterDue = -1;
            if (!ShapeAvailable) { ShapeFailure("The fixed palette record is not available."); return; }
            ShapeBundle candidate = null;
            try
            {
                var profile = CaptureShapeProfile();
                if (!profile.Validate(out string error)) throw new InvalidOperationException(error);
                candidate = new ShapeBundle { profile = profile };
                if (UseSplatter && ShapeSource == ClayPreviewShapeSource.Image)
                {
                    candidate.image = ClayPreviewShapeMask.LoadBytes(imageShapeBytes, imageShapeName);
                    if (!candidate.image.Succeeded) throw new InvalidOperationException(candidate.image.Error);
                    candidate.brushes = new[] { candidate.image.Brush, candidate.image.Brush, candidate.image.Brush };
                    candidate.preview = candidate.image.Preview;
                }
                else if (UseSplatter)
                {
                    candidate.brushes = new Texture2D[3];
                    for (int i = 0; i < 3; i++) candidate.brushes[i] = GenerateSplatter(SplatterSeed, i == 0);
                    candidate.preview = generatedPreviewTexture; generatedPreviewTexture = null;
                }
                ShapeBundle previousRequest = requestedShape;
                requestedShape = candidate; candidate = null;
                RefreshPalettePreview();
                if (previousRequest != null && previousRequest != publishedShape) previousRequest.Dispose();
                if (!string.IsNullOrEmpty(PaletteError)) RejectShapeCandidate(PaletteError);
                else CommitShapeCandidate();
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                if (generatedPreviewTexture != null) { DestroyOwned(generatedPreviewTexture); generatedPreviewTexture = null; }
                RejectShapeCandidate(exception.Message);
            }
            SyncShapeUi();
        }
        void CommitShapeCandidate()
        {
            if (splatterDue >= 0 || requestedShape == null || requestedShape == publishedShape || UseReplay && !paletteReplay.Ready) return;
            ShapeBundle old = publishedShape;
            publishedShape = requestedShape; committedShape = publishedShape.profile; ShapeRevision++;
            old?.Dispose(); SyncShapeUi();
        }
        void RejectShapeCandidate(string error)
        {
            paletteReplay?.RestorePublished();
            RestoreShapeComparisonSelection();
            if (requestedShape != null && requestedShape != publishedShape) requestedShape.Dispose();
            requestedShape = publishedShape;
            if (committedShape != null)
            {
                byte[] bytes = null;
                if (!string.IsNullOrEmpty(committedShape.imagePngBase64)) committedShape.TryGetImageBytes(out bytes, out _);
                ApplyShapeFields(committedShape, bytes);
            }
            ShapeFailure(error);
        }
        void ReleaseSplatterTextures()
        {
            if (requestedShape != null && requestedShape != publishedShape) requestedShape.Dispose();
            publishedShape?.Dispose(); requestedShape = publishedShape = null;
            if (generatedPreviewTexture != null) DestroyOwned(generatedPreviewTexture);
            generatedPreviewTexture = null; imageShapeBytes = null;
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
            if(preview){generatedPreviewTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){hideFlags=HideFlags.DontSave};generatedPreviewTexture.SetPixels32(display);generatedPreviewTexture.Apply();}
            return tex;
        }
    }
}
