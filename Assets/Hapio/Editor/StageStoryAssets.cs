using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>One-time import. Existing authored prefabs, text and sequences are never overwritten.</summary>
    public static class StageStoryAssets
    {
        public const string Root = "Assets/Hapio/Story/StageStories";
        public const string Art = "Assets/Hapio/Art/Story/StageStories/";
        public const string Preview = Root + "/StageStoryPreview.unity";
        [Serializable] public class Manifest { public Frame[] frames; }
        [Serializable] public class Frame { public string nodeId, name; public Node[] nodes; }
        [Serializable] public class Rgb { public float r = 1, g = 1, b = 1; }
        [Serializable] public class Node
        {
            public string nodeId, name, kind, key, text, align, assetPath;
            public float x, y, width, height, rotation, alpha = 1, size, lineHeight, radius, shadowX, shadowY, shadowAlpha;
            public bool shadow;
            public bool flipY, cover, crop, vector, bold;
            public float wPercent = 1, hPercent = 1, leftPercent, topPercent;
            public Rgb color = new Rgb();
            public Rect Rect => new Rect(x, y, width, height);
        }
        private static readonly string[] Fronts = {"4210:3167", "4210:3213", "4210:3287"};
        private static readonly string[] Backs = {"4210:2908", "4210:2974", "4210:3040"};
        private static readonly string[] Names = {"Gumiho", "Yagwanggui", "Jigwi"};
        private static readonly string[][] Scenes = {new[]{"4210:2897","4210:2881"}, new[]{"4210:3248","4210:3264"}, new[]{"4210:3242","4210:3257"}};
        private static readonly string[] Endings = {"4210:3318","4210:3333","4210:3352","4210:3356","4210:3358","4210:3374","4210:3449"};
        private static Manifest source;
        private static Material inkMaterial;
        private static bool refreshing;
        private static Frame FrameFor(string id) => source.frames.Single(f => f.nodeId == id);
        private static Font Font(bool bold) => AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/" + (bold ? "ShillaCultureBold.otf" : "Shilla_Culture(M).otf"));
        private static RectTransform Rect(string name, Transform parent, Rect bounds) => StoryWorkspaceBuilder.Rect(name, parent, bounds);
        [MenuItem("Hapio/Story/Create Missing Stage Stories And Ending")]
        public static void CreateMissing() => Build(false);
        internal static void Build(bool refresh)
        {
            refreshing=refresh;
            source = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/ImportedFrames.json"));
            Directory.CreateDirectory(Root + "/Views"); Directory.CreateDirectory(Root + "/Cuts");
            AssetDatabase.Refresh();
            inkMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/FigmaInkOverlay.mat");
            if(inkMaterial==null){inkMaterial=new Material(Shader.Find("Hapio/Story/Figma Ink Overlay"));AssetDatabase.CreateAsset(inkMaterial,Root+"/FigmaInkOverlay.mat");}
            foreach (var file in Directory.GetFiles(Art, "*.png")) StoryWorkspaceBuilder.ImportSprite(file);
            var all = new List<StoryCutDefinition>();
            for (int stage = 0; stage < 3; stage++)
            {
                var list = new List<StoryCutDefinition> { CreateCut(Names[stage] + "_SealCard", Fronts[stage], stage) };
                for (int i=0; i<2; i++) list.Add(CreateCut(Names[stage] + "_After_" + (i+1).ToString("00"), Scenes[stage][i]));
                Sequence("After" + Names[stage], list); all.AddRange(list);
            }
            var ending = new List<StoryCutDefinition>();
            for (int i=0; i<Endings.Length; i++) ending.Add(CreateCut("Ending_"+(i+1).ToString("00"), Endings[i]));
            Sequence("Ending", ending); all.AddRange(ending); Sequence("AllStageStories", all);
            var failure = new List<StoryCutDefinition> { CreateCut("Failure_01", "4210:2860"), CreateCut("Failure_Dashboard", "4210:3106") };
            Sequence("Failure", failure);
            AssetDatabase.SaveAssets();
            if (!File.Exists(Preview) || refreshing) CreatePreview();
            AssetDatabase.Refresh();
            Debug.Log("HAPIO_STAGE_STORIES_READY: 16 success/ending cuts plus 2 failure cuts; three reversible cards.");
        }
        [MenuItem("Hapio/Story/Open Stage Stories Preview")]
        public static void OpenPreview() => EditorSceneManager.OpenScene(Preview);
        private static StoryCutDefinition CreateCut(string id, string nodeId, int cardIndex = -1)
        {
            string viewPath = Root + "/Views/"+id+".prefab", cutPath=Root+"/Cuts/"+id+".asset";
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(cutPath);
            if (!File.Exists(viewPath) || refreshing)
            {
                var root = new GameObject(id, typeof(RectTransform), typeof(StoryCutView));
                try
                {
                    ((RectTransform)root.transform).sizeDelta = new Vector2(402,874);
                    var view=root.GetComponent<StoryCutView>(); view.hideEmptyDialogue=true; view.sourceFigmaNodeId=nodeId;
                    var frame=FrameFor(nodeId); var layers=new List<StoryLayer>();
                    bool ending=Endings.Take(6).Contains(nodeId);
                    var body=ending ? frame.nodes.LastOrDefault(n=>n.kind=="text" && n.size>18) : null;
                    var name=ending ? frame.nodes.FirstOrDefault(n=>n.kind=="text" && n.size<=18) : null;
                    if (body!=null)
                    {
                        view.overrideDialogueLayout=true;view.dialogueFontSize=body.size;view.dialogueRect=body.Rect;
                        view.speakerRect = new Rect(57,713,91,33);view.centerSpeakerName=true;
                        view.speakerBadge=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Story/Opening/OP_002/SpeakerBadge.png");
                    }
                    if (cardIndex>=0) BuildCard(root, frame, FrameFor(Backs[cardIndex]));
                    else
                    {
                        foreach (var n in frame.nodes)
                        {
                            if (body!=null && (n.kind=="text" || n.name=="image 1128" || n.name=="image 1140")) continue;
                            var element=Element(root.transform,n);
                            if (n.kind=="image" || n.kind=="solid")
                            {
                                var layer=element.gameObject.AddComponent<StoryLayer>();layer.artwork=n.kind=="solid"?null:element.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.sprite!=null);
                                layer.isBackground=layers.Count==0;layer.delay=layers.Count==0?0:Mathf.Min(layers.Count*.06f,.35f);
                                layer.startHeight=1;layer.enterSeconds=.65f;layer.enterOffset=layer.isBackground?Vector2.zero:new Vector2(0,-12);
                                if(n.kind=="solid"){layer.enterOffset=Vector2.zero;layer.enterSeconds=1.2f;}
                                layers.Add(layer);
                            }
                        }
                        if(nodeId=="4210:3106") BuildGaugeAndActions(root);
                    }
                    view.layers=layers.ToArray();
                    StageStoryDirection.Configure(id, view);
                    StageStoryPolishAssets.Configure(id, view);
                    PrefabUtility.SaveAsPrefabAsset(root,viewPath);
                } finally {Object.DestroyImmediate(root);}
            }
            if(cut!=null) { if(refreshing) { cut.viewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(viewPath).GetComponent<StoryCutView>(); var body=FrameFor(nodeId).nodes.LastOrDefault(n=>n.kind=="text"&&n.size>18); var refreshedSpeaker=FrameFor(nodeId).nodes.FirstOrDefault(n=>n.kind=="text"&&n.size<=18); if(Endings.Contains(nodeId)) {bool hasDialogue=Endings.Take(6).Contains(nodeId); cut.pages[0].dialogue=hasDialogue?body?.text??"":"";cut.pages[0].speaker=hasDialogue?refreshedSpeaker?.text??"":"";} EditorUtility.SetDirty(cut); } return cut; }
            cut=ScriptableObject.CreateInstance<StoryCutDefinition>();cut.cutId=id;cut.viewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(viewPath).GetComponent<StoryCutView>();
            var data=FrameFor(nodeId);bool end=Endings.Take(6).Contains(nodeId);
            var text=end?data.nodes.LastOrDefault(n=>n.kind=="text"&&n.size>18):null;
            var speaker=end?data.nodes.FirstOrDefault(n=>n.kind=="text"&&n.size<=18):null;
            cut.pages.Add(new StoryDialoguePage { speaker=speaker?.text??"", dialogue=text?.text??"", autoReadSeconds=cardIndex>=0?12:4 });
            cut.direction=cardIndex>=0?"카드를 누르거나 좌우로 밀어 앞·뒷면 전환. 게임 연결은 StoryResultActions 이벤트 사용.":"피그마 "+nodeId+" 원본 배치. 실제 스테이지 진입은 팀원 연결 예정.";
            StageStoryDirection.ConfigureCut(cut);
            AssetDatabase.CreateAsset(cut,cutPath);return cut;
        }
        internal static RectTransform Element(Transform parent, Node n, float offsetX=0, float offsetY=0)
        {
            var rect=Rect(n.name+" ["+n.nodeId+"]",parent,new Rect(n.x-offsetX,n.y-offsetY,n.width,n.height));
            rect.pivot=Vector2.one*.5f;rect.anchoredPosition+=new Vector2(n.width*.5f,-n.height*.5f);
            rect.localRotation=Quaternion.Euler(0,0,n.rotation);if(n.flipY)rect.localScale=new Vector3(1,-1,1);
            var group=rect.gameObject.AddComponent<CanvasGroup>();group.alpha=n.alpha;
            if(n.kind=="text")
            {
                var text=Rect("Text",rect,new Rect(0,0,n.width,n.height)).gameObject.AddComponent<Text>();text.font=Font(n.bold);
                text.fontSize=Mathf.Max(32,Mathf.CeilToInt(n.size));float scale=n.size/text.fontSize;
                text.rectTransform.localScale=Vector3.one*scale;text.rectTransform.sizeDelta/=scale;
                text.text=n.text;text.color=new Color(n.color.r,n.color.g,n.color.b);text.raycastTarget=false;
                text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
                text.alignment=n.align=="CENTER"?TextAnchor.UpperCenter:n.align=="RIGHT"?TextAnchor.UpperRight:TextAnchor.UpperLeft;
                if(n.lineHeight>0){var gen=new TextGenerator();gen.Populate("가\n가",text.GetGenerationSettings(new Vector2(1000,1000)));if(gen.lines.Count>1)text.lineSpacing=n.lineHeight/(scale*(gen.lines[0].topY-gen.lines[1].topY));}
            }
            else if(n.kind=="solid")
            {
                if(n.radius>0) {var round=rect.gameObject.AddComponent<C6Lab.LabRoundedGraphic>();round.radius=n.radius;round.color=new Color(n.color.r,n.color.g,n.color.b,n.alpha);group.alpha=1;round.raycastTarget=false;}
                else {var image=rect.gameObject.AddComponent<Image>();image.color=new Color(n.color.r,n.color.g,n.color.b,n.alpha);image.material=inkMaterial;group.alpha=1;image.raycastTarget=false;}
            }
            else
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(string.IsNullOrEmpty(n.assetPath)?Art+n.key:n.assetPath);
                if(sprite==null)throw new InvalidOperationException("Missing source sprite "+n.key);
                var target=rect;
                float w=n.width,h=n.height;
                if(n.cover||n.crop)
                {
                    if(Mathf.Abs(n.rotation)>.001f){var maskImage=rect.gameObject.AddComponent<Image>();maskImage.raycastTarget=false;rect.gameObject.AddComponent<Mask>().showMaskGraphic=false;}else rect.gameObject.AddComponent<RectMask2D>();float ratio=Mathf.Max(w/sprite.rect.width,h/sprite.rect.height);
                    float iw=n.crop?w*n.wPercent:sprite.rect.width*ratio,ih=n.crop?h*n.hPercent:sprite.rect.height*ratio;
                    target=Rect("Artwork",rect,new Rect(n.crop?w*n.leftPercent:(w-iw)/2,n.crop?h*n.topPercent:(h-ih)/2,iw,ih));
                }
                var image=target.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=new Color(1,1,1,n.alpha);group.alpha=1;image.raycastTarget=false;
                if(n.vector)image.material=inkMaterial;
                if(n.shadow){var shadow=image.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,n.shadowAlpha);shadow.effectDistance=new Vector2(n.shadowX,-n.shadowY);}
                if(n.name.StartsWith("enhanced_0e5896")||n.name=="image 1689"||n.name=="image 1688")image.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hapio/Art/Story/Materials/FigmaPaperBackground.mat");
            }
            return rect;
        }
        private static void BuildCard(GameObject root, Frame frontFrame, Frame backFrame)
        {
            const float x=44,y=163,w=313.7531f,h=570.4908f;
            var body=Rect("Card Turn Pivot",root.transform,new Rect(x,y,w,h));body.pivot=Vector2.one*.5f;body.anchoredPosition+=new Vector2(w/2,-h/2);
            var front=Rect("Front",body,new Rect(0,0,w,h));var back=Rect("Back",body,new Rect(0,0,w,h));
            // Card body bounds separate the flipping paper from the fixed header/clouds/instructions.
            Func<Node,bool> inside=n=>n.y>=y-1 && n.y<h+y-1 && n.x>=x-1 && n.x+n.width<=x+w+1;
            foreach(var n in frontFrame.nodes)Element(inside(n)?front:root.transform,n,inside(n)?x:0,inside(n)?y:0);
            foreach(var n in backFrame.nodes.Where(inside))Element(back,n,x,y);
            body.SetAsLastSibling();
            var surface=body.gameObject.AddComponent<Image>();surface.color=Color.clear;surface.raycastTarget=true;
            var flip=body.gameObject.AddComponent<StoryCardFlip>();flip.card=body;flip.front=front.gameObject;flip.back=back.gameObject;
            back.gameObject.SetActive(false);
            var actions=root.AddComponent<StoryResultActions>();
            var inspect=backFrame.nodes.First(n=>n.kind=="text"&&n.text=="봉인 박 둘러보기");
            var button=Rect("Inspect Gourd Action",back,new Rect(inspect.x-x-12,inspect.y-y-5,inspect.width+24,inspect.height+10));
            var hit=button.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var b=button.gameObject.AddComponent<Button>();b.targetGraphic=hit;UnityEventTools.AddPersistentListener(b.onClick,actions.InspectGourd);
            CenterInspectLabel(back);
        }

        internal static void CenterInspectLabel(RectTransform back)
        {
            var label = back.GetComponentsInChildren<Text>(true).Single(t => t.text == "봉인 박 둘러보기");
            var frame = (RectTransform)back.Cast<Transform>().Single(t => t.name.StartsWith("Frame 263 [", StringComparison.Ordinal));
            var caption = (RectTransform)label.transform.parent;
            var button = (RectTransform)back.Find("Inspect Gourd Action");
            foreach (var rect in new[] {caption, button})
            {
                rect.anchorMin = frame.anchorMin;
                rect.anchorMax = frame.anchorMax;
                rect.pivot = frame.pivot;
                rect.sizeDelta = frame.sizeDelta;
                rect.anchoredPosition = frame.anchoredPosition;
            }
            var text = label.rectTransform;
            text.anchorMin = text.anchorMax = text.pivot = Vector2.one * .5f;
            text.sizeDelta = frame.sizeDelta / text.localScale.x;
            text.anchoredPosition = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
        }

        private static void BuildGaugeAndActions(GameObject root)
        {
            var fill=root.GetComponentsInChildren<Image>(true).Single(i=>i.transform.parent.name.Contains("[4210:3163]")||i.name.Contains("[4210:3163]"));
            var node=fill.transform.parent.name.Contains("[4210:3163]")?fill.transform.parent.GetComponent<RectTransform>():fill.rectTransform;
            node.anchoredPosition=new Vector2(201.5f,-228.25f);node.sizeDelta=new Vector2(253,13.5f);
            fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;
            var gauge=root.AddComponent<StorySealGauge>();gauge.fill=fill;gauge.percentage=root.GetComponentsInChildren<Text>().Single(t=>t.text=="78%");
            var actions=root.AddComponent<StoryResultActions>();
            Action<string,UnityEngine.Events.UnityAction> button=(label,callback)=>{var text=root.GetComponentsInChildren<Text>().Single(t=>t.text==label);var hit=Rect(label+" Action",root.transform,new Rect(label=="다시 도전"?77:155,label=="다시 도전"?721:774,label=="다시 도전"?234:95,45)).gameObject.AddComponent<Image>();hit.color=Color.clear;var b=hit.gameObject.AddComponent<Button>();b.targetGraphic=hit;UnityEventTools.AddPersistentListener(b.onClick,callback);};
            button("다시 도전",actions.Retry);button("나가기",actions.Exit);
        }
        private static StorySequence Sequence(string name,List<StoryCutDefinition> cuts)
        {
            string path=Root+"/"+name+".asset";var sequence=AssetDatabase.LoadAssetAtPath<StorySequence>(path);if(sequence!=null)return sequence;
            sequence=ScriptableObject.CreateInstance<StorySequence>();sequence.storyId="stage-story-"+name;sequence.cuts=cuts;AssetDatabase.CreateAsset(sequence,path);return sequence;
        }
        private static void CreatePreview()
        {
            var previous=SceneManager.GetActiveScene();var sourceScene=EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene,OpenSceneMode.Additive);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                foreach(var item in sourceScene.GetRootGameObjects()){var clone=Object.Instantiate(item);clone.name=item.name;SceneManager.MoveGameObjectToScene(clone,scene);}
                var player=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<StoryPlayer>(true)).Single();
                var title=player.GetComponent<StoryOpeningTitle>();if(title!=null){Object.DestroyImmediate(title.titlePage);title.controls.SetActive(true);Object.DestroyImmediate(title);}
                player.sequence=AssetDatabase.LoadAssetAtPath<StorySequence>(Root+"/AllStageStories.asset");player.playOnStart=true;
                var menu=player.gameObject.AddComponent<StageStoryShowcase>();menu.player=player;
                menu.groups=new[]{"AllStageStories","AfterGumiho","AfterYagwanggui","AfterJigwi","Ending","Failure"}.Select(n=>AssetDatabase.LoadAssetAtPath<StorySequence>(Root+"/"+n+".asset")).ToArray();
                string[] labels={"전체","구미호 후","야광귀 후","지귀 후","엔딩","실패"};
                var toolbar=player.nextButton.transform.parent;
                for(int i=0;i<labels.Length;i++)
                {
                    var clone=Object.Instantiate(player.nextButton,toolbar);clone.name="Group "+i;clone.onClick=new Button.ButtonClickedEvent();
                    var r=(RectTransform)clone.transform;r.anchoredPosition=new Vector2(i*67,-34);r.sizeDelta=new Vector2(65,27);
                    clone.GetComponentInChildren<Text>().text=labels[i];UnityEventTools.AddIntPersistentListener(clone.onClick,menu.PlayGroup,i);
                }
                StageStoryDirection.AddPreviewListener(scene);
                EditorSceneManager.SaveScene(scene,Preview);
            }
            finally {EditorSceneManager.CloseScene(scene,true);EditorSceneManager.CloseScene(sourceScene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
    }
}
