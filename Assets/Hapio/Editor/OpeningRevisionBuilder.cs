using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Explicit import of the approved 28-frame revision; keeps existing asset GUIDs and Home bindings.</summary>
    public static class OpeningRevisionBuilder
    {
        const string Root = "Assets/Hapio/Story/Opening/";
        const string Art = "Assets/Hapio/Art/Story/Opening/Revision28/";
        [Serializable] public class Manifest { public Frame[] frames; }
        [Serializable] public class Frame { public int number; public string id, speaker, dialogue, artworkContinuityKey; public Item[] items; public TextItem[] texts; }
        [Serializable] public class Item
        {
            public string id, name, key, path;
            public float x,y,width,height,boxWidth,boxHeight,rotation,alpha,wp=1,hp=1,lp,tp;
            public bool cover,bottom,crop,dialogue,shadow,inset;
            public float ix,iy,iw,ih;
            public string backgroundSourcePath;
            public float figmaOpacity;
            public bool precompositedBackground, opacityInSource;
        }
        [Serializable] public class TextItem
        {
            public string id,text,richText; public float x,y,width,height,rotation,size,lineHeight;
            public bool dialogue,white;
        }
        [MenuItem("Hapio/Story/Apply Approved 28 Frame Opening")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"Revision28.json"));
            if (manifest.frames.Length!=28) throw new InvalidOperationException("Expected 28 approved frames.");
            OpeningBackgroundBaker.Bake(manifest);
            foreach(var item in manifest.frames.SelectMany(f=>f.items))
                if (!File.Exists(item.path)) throw new FileNotFoundException(item.path);
            foreach(var path in manifest.frames.SelectMany(f=>f.items).Select(i=>i.path).Distinct())
                if (path.StartsWith(Art)) StoryWorkspaceBuilder.ImportSprite(path);
            var cuts=new List<StoryCutDefinition>();
            foreach(var frame in manifest.frames.OrderBy(f=>f.number)) cuts.Add(BuildFrame(frame));
            var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>(Root+"Opening.asset");
            sequence.cuts=cuts;EditorUtility.SetDirty(sequence);
            foreach(var name in new[]{"OpeningPreview","BatchPreview"})
            {
                var preview=AssetDatabase.LoadAssetAtPath<StorySequence>(Root+name+".asset");
                if(preview==null)continue; preview.cuts=new List<StoryCutDefinition>(cuts);EditorUtility.SetDirty(preview);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_OPENING_28_READY: 28 exact Figma frames; existing title and cloud handoff retained.");
        }
        static RectTransform Box(string name,Transform parent,float x,float y,float w,float h)
            => StoryWorkspaceBuilder.Rect(name,parent,new Rect(x,y,w,h));
        static StoryCutDefinition BuildFrame(Frame f)
        {
            string id=$"OP_{f.number:000}", path=Root+"Views/"+id+".prefab";
            var root=new GameObject(id,typeof(RectTransform),typeof(StoryCutView));
            try
            {
                var rootRect=(RectTransform)root.transform;rootRect.anchorMin=Vector2.zero;rootRect.anchorMax=Vector2.one;rootRect.offsetMin=Vector2.zero;rootRect.offsetMax=Vector2.zero;
                var backing=Box("Paper backing",root.transform,0,0,402,874).gameObject.AddComponent<Image>();
                backing.color=Color.white;backing.raycastTarget=false;
                root.AddComponent<RectMask2D>();
                var view=root.GetComponent<StoryCutView>();
                view.sourceFigmaNumber=f.number;view.sourceFigmaNodeId=f.id;view.artworkContinuityKey=f.artworkContinuityKey;
                view.hideEmptyDialogue=true;view.layers=Array.Empty<StoryLayer>();view.beats=Array.Empty<StoryVisualBeat>();
                // Every revised cut owns its dialogue. Empty groups intentionally suppress the old shared paper.
                var words=Box("Authored Dialogue",root.transform,0,0,402,874);
                view.authoredDialogue=words.gameObject.AddComponent<CanvasGroup>();view.authoredDialogue.blocksRaycasts=false;
                var motions=new List<OpeningLayerMotion>();
                int ordinal=0;
                foreach(var item in f.items)
                {
                    var parent=item.dialogue?words:root.transform;
                    var image=AddImage(parent,item,out var motionRoot);
                    if(!item.dialogue && ordinal++>0 && item.width>15 && item.height>15)
                    {
                        var motion=motionRoot.gameObject.AddComponent<OpeningLayerMotion>();
                        motion.delay=.08f+Mathf.Min(ordinal*.055f,.25f);motion.seconds=.65f;
                        Configure(motion,f.number,item.id,ordinal);
                        if(motion.kind==OpeningLayerMotion.Kind.Toss) ConfigureFlight(motion,item);
                        motions.Add(motion);
                    }
                }
                foreach(var text in f.texts)
                {
                    var textRect=AddText(text.dialogue?words:root.transform,text);
                    if(!text.dialogue)
                    {
                        var reveal=textRect.gameObject.AddComponent<OpeningLayerMotion>();
                        reveal.delay=.19f;reveal.seconds=.65f;motions.Add(reveal);
                    }
                }
                words.SetAsLastSibling();
                if(f.number==15)
                {
                    // Carry the intact gourd from frame 14 into the authored shattered pose of frame 15.
                    var gourd=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"Revision28.json")).frames.Single(x=>x.number==14).items.Single(x=>x.id=="4553:2469");
                    AddImage(root.transform,gourd,out var falling);
                    var floor=f.items.Single(x=>x.id=="4593:3393");
                    // The opaque silhouette's lowest point at 28 degrees, relative to its bottom pivot.
                    // Align that contact point (not the transparent image rectangle) with the floor shadow.
                    var contact=new Vector2(-59.78f,-14.84f);
                    var startPivot=new Vector2(gourd.x+gourd.width/2,-gourd.y-gourd.height);
                    var landing=new Vector2(floor.x+floor.width/2,-floor.y-floor.height/2);
                    var drop=falling.gameObject.AddComponent<OpeningLayerMotion>();drop.kind=OpeningLayerMotion.Kind.Drop;drop.delay=.1f;drop.seconds=2.5f;drop.travel=landing-startPivot-contact;drop.angle=28;motions.Add(drop);
                    drop.fadeAtEnd=true;view.waitForEntrance=true;
                }
                view.openingMotions=motions.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {Object.DestroyImmediate(root);}
            string cutPath=Root+"Cuts/"+id+".asset";
            var cut=AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(cutPath);
            if(cut==null){cut=ScriptableObject.CreateInstance<StoryCutDefinition>();AssetDatabase.CreateAsset(cut,cutPath);}
            cut.cutId=id;cut.viewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<StoryCutView>();
            cut.pages=new List<StoryDialoguePage>{new StoryDialoguePage{speaker=f.speaker,dialogue=f.dialogue,autoReadSeconds=f.number==23?14:4}};
            cut.direction=$"Approved Figma frame {f.number} ({f.id}); native text and independent artwork motion.";
            EditorUtility.SetDirty(cut);return cut;
        }
        static Image AddImage(Transform parent,Item i,out RectTransform motion)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(i.path);
            if(sprite==null)throw new InvalidOperationException("Sprite not imported: "+i.path);
            var place=Box(i.name+" ["+i.id+"]",parent,i.x+(i.boxWidth-i.width)/2,i.y+(i.boxHeight-i.height)/2,i.width,i.height);
            place.pivot=Vector2.one*.5f;place.anchoredPosition+=new Vector2(i.width/2,-i.height/2);place.localRotation=Quaternion.Euler(0,0,i.rotation);
            motion=Box("Motion",place,0,0,i.width,i.height);motion.pivot=new Vector2(.5f,0);motion.anchoredPosition=new Vector2(i.width/2,-i.height);
            Rect r=new Rect(0,0,i.width,i.height);
            if(i.inset)r=new Rect(i.ix,i.iy,i.iw,i.ih);
            else if(i.crop)r=new Rect(i.lp*i.width,i.tp*i.height,i.wp*i.width,i.hp*i.height);
            else if(i.cover)
            {
                float s=Mathf.Max(i.width/sprite.rect.width,i.height/sprite.rect.height);var size=sprite.rect.size*s;
                r=new Rect((i.width-size.x)/2,i.bottom?i.height-size.y:(i.height-size.y)/2,size.x,size.y);
                if(i.id=="4553:2443"){float bleed=(i.width+4)/i.width;var grown=size*bleed;r=new Rect((i.width-grown.x)/2,(i.height-grown.y)/2,grown.x,grown.y);}
            }
            if(!i.inset && (i.crop || r.width>i.width+.01f||r.height>i.height+.01f))
            {
                if(Mathf.Abs(i.rotation)>.01f || (!i.dialogue && i.width<402 && i.height<874)){var maskImage=motion.gameObject.AddComponent<Image>();maskImage.raycastTarget=false;motion.gameObject.AddComponent<Mask>().showMaskGraphic=false;}
                else motion.gameObject.AddComponent<RectMask2D>();
            }
            var image=Box("Artwork",motion,r.x,r.y,r.width,r.height).gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=new Color(1,1,1,i.alpha);image.raycastTarget=false;
            if(i.shadow){var shadow=image.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.25f);shadow.effectDistance=new Vector2(0,-4);}
            return image;
        }
        static RectTransform AddText(Transform parent,TextItem t)
        {
            var r=Box("Text ["+t.id+"]",parent,t.x,t.y,t.width,t.height);
            r.pivot=Vector2.one*.5f;r.anchoredPosition+=new Vector2(t.width/2,-t.height/2);r.localRotation=Quaternion.Euler(0,0,t.rotation);
            var text=r.gameObject.AddComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
            const float density=4; r.sizeDelta*=density;r.localScale=Vector3.one/density;
            text.fontSize=Mathf.RoundToInt(t.size*density);text.supportRichText=true;text.text=t.richText;
            text.alignment=TextAnchor.MiddleLeft;text.color=t.white?Color.white:Color.black;text.raycastTarget=false;
            text.horizontalOverflow=t.dialogue?HorizontalWrapMode.Overflow:HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            if(t.lineHeight>0)
            {
                var generator=new TextGenerator();generator.Populate("가\n가",text.GetGenerationSettings(new Vector2(4000,4000)));
                if(generator.lines.Count>1)text.lineSpacing=t.lineHeight*density/(generator.lines[0].topY-generator.lines[1].topY);
            }
            return r;
        }
        static void ConfigureFlight(OpeningLayerMotion motion, Item item)
        {
            // Page-space paths keep rotated brooms and boxes inside the action, above the dialogue.
            Vector2 landing;
            switch (item.id)
            {
                case "4553:2459": landing=new Vector2(145,520);motion.travel=new Vector2(55,290);motion.cycleSeconds=3.2f;motion.phase=.12f;break;
                case "4553:2457": landing=new Vector2(260,460);motion.travel=new Vector2(65,240);motion.cycleSeconds=3.7f;motion.phase=.62f;break;
                case "4553:2461": landing=new Vector2(110,570);motion.travel=new Vector2(65,260);motion.cycleSeconds=2.6f;motion.phase=.35f;break;
                default: landing=new Vector2(285,510);motion.travel=new Vector2(50,290);motion.cycleSeconds=3f;motion.phase=.83f;break;
            }
            motion.pathOffset=new Vector2(landing.x-item.x-item.boxWidth/2, item.y+item.boxHeight/2-landing.y);
            motion.angle=38;
            var rect=(RectTransform)motion.transform;
            rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition+=new Vector2(0,item.height/2);
        }
        static void Configure(OpeningLayerMotion m,int frame,string id,int order)
        {
            m.phase=order*.47f;
            if(frame==7&&id=="4553:4271"){m.kind=OpeningLayerMotion.Kind.Sigh;m.delay=.55f;m.seconds=1.7f;}
            if(frame==6&&(id=="4553:2651"||id=="4553:2652")){m.kind=OpeningLayerMotion.Kind.Cloud;m.travel=new Vector2(9,3);}
            if(frame==12&&(id=="4553:2454"||id=="4553:2455")){m.kind=OpeningLayerMotion.Kind.Sweep;m.travel=new Vector2(27,4);m.angle=12;}
            if(frame==13&&(id=="4553:2459"||id=="4553:2461"||id=="4553:2462"||id=="4553:2457")){m.kind=OpeningLayerMotion.Kind.Toss;m.travel=new Vector2(45,100);m.angle=30;}
            if(frame==14&&id=="4553:2469"){m.kind=OpeningLayerMotion.Kind.Teeter;m.travel=new Vector2(4,0);m.angle=10;m.seconds=1.5f;}
            if(frame==15 && id!="4593:3393")
            {
                m.delay=2.6f;
                if(id=="4553:2473"){m.seconds=OpeningLayerMotion.ImpactFadeSeconds;m.travel=Vector2.zero;}
                else {m.kind=OpeningLayerMotion.Kind.Smoke;m.seconds=.45f;m.travel=new Vector2(4,35);}
            }
            if(frame==15&&id=="4593:3393"){m.kind=OpeningLayerMotion.Kind.GroundShadow;m.delay=.1f;m.seconds=2.5f;}
            if(frame==16&&(id=="4553:2488"||id=="4688:4901"||id=="4688:4903")){m.kind=OpeningLayerMotion.Kind.Smoke;m.travel=new Vector2(5,45);m.seconds=1.1f;m.fadeAtEnd=true;}
            if(frame==16&&(id=="4688:3476"||id=="4688:3477"||id=="4688:3478"))
            {m.kind=OpeningLayerMotion.Kind.Escape;m.delay=.35f+(order%3)*.18f;m.seconds=1.45f;m.travel=id=="4688:3476"?new Vector2(0,-600):new Vector2(id=="4688:3477"?150:-150,-300);m.angle=id=="4688:3477"?28:-28;}
        }
    }
}
