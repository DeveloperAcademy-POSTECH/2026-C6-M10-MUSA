using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using C6Lab;

namespace Hapio.Editor
{
    public static class StageReadyAssets
    {
        const string Art="Assets/Hapio/Art/Story/StageReady/";
        const string Path="Assets/Hapio/Resources/Hapio/Story/StageReadyView.prefab";
        [MenuItem("Hapio/Story/Update Stage Ready and Quit UI")]
        public static void Build()
        {
            foreach(string file in Directory.GetFiles(Art,"*.png")) StoryWorkspaceBuilder.ImportSprite(file);
            var root=new GameObject("StageReadyView",typeof(RectTransform),typeof(StoryPageFit));
            var v=root.AddComponent<StoryStageReadyView>();
            try
            {
                var page=Rect("WaitingPage",root.transform,new Rect(0,0,402,874));
                page.gameObject.AddComponent<RectMask2D>();v.waitingPage=page.gameObject;
                var shield=page.gameObject.AddComponent<UnityEngine.UI.Image>();shield.color=Color.white;shield.raycastTarget=true;
                v.paper=Picture(page,"Paper",new Rect(-74,-167,704,1041));
                // Let the oversized paper bleed beyond the authored member width on wider phones.
                v.paper.GetComponent<UnityEngine.UI.Image>().maskable=false;
                v.paper.GetComponent<UnityEngine.UI.Image>().material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hapio/Art/Story/Materials/FigmaPaperBackground.mat");
                v.heading=Text(page,"Heading","동지들이 채비 중이오.",new Rect(1,184,400,32),25,true,GameLobbySeatColors.Brown);
                v.count=Text(page,"Count","전원 준비 완료 0/5",new Rect(1,636,400,30),22,true,GameLobbySeatColors.Brown);
                v.memberCaption=Text(page,"MemberCaption","방장이 다음 봉인을\n준비하고 있소.",new Rect(1,522.1426f,383.5677f,50),19.083f,false,Color.black,25);
                v.runnerSlot=Rect("RunnerSlot",page,new Rect(62,279,279.339f,260));
                v.runner=v.runnerSlot.gameObject.AddComponent<UnityEngine.UI.Image>();v.runner.raycastTarget=false;
                var party=Rect("Party",page,new Rect(0,0,402,874));v.party=party.gameObject;
                Picture(party,"Shadow",new Rect(109,576,145,17.857f));
                Picture(party,"Shadow",new Rect(47,511,145,17.857f));
                var sh=Picture(party,"Shadow",new Rect(212,548,126.949f,15.634f));sh.localRotation=Quaternion.Euler(0,0,-1.67f);
                sh=Picture(party,"Shadow",new Rect(241,488,126.949f,15.634f));sh.localRotation=Quaternion.Euler(0,0,-1.67f);
                foreach(var img in party.GetComponentsInChildren<UnityEngine.UI.Image>()) { img.color=new Color(1,1,1,.4f); img.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hapio/Story/StageStories/FigmaInkOverlay.mat"); }
                Picture(party,"Geumok",new Rect(213.52f,295.84f,143.478f,215.218f));
                Picture(party,"Muki",new Rect(116.26f,249,169.707f,254.561f));
                Picture(party,"Buli",new Rect(46,304.27f,157.333f,221.129f));
                var suli=Picture(party,"Suli",new Rect(192.34f,370.78f,142.174f,199.824f));suli.pivot=new Vector2(.5f,1);suli.anchoredPosition+=new Vector2(71.087f,0);suli.localScale=new Vector3(-1,1,1);
                Picture(party,"Manbok",new Rect(108.76f,408.97f,138.472f,181.01f));
                v.start=Button(page,"Start",new Rect(27,729,349,58),GameLobbySeatColors.Ready,27);
                v.startFill=v.start.targetGraphic;v.startCaption=Text(v.start.transform,"Caption","요괴 봉인 시작",new Rect(0,12,349,34),24,true,GameLobbySeatColors.LightText);
                var quitHit=Rect("Quit",root.transform,new Rect(24,35,78,44));
                var hit=quitHit.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;
                v.quit=quitHit.gameObject.AddComponent<UnityEngine.UI.Button>();v.quit.targetGraphic=hit;
                // The exported underline includes its 1.42 pt stroke above/below the 70 pt baseline.
                Picture(quitHit,"Quit",new Rect(8,8,62,28.667f));
                var modal=Rect("QuitModal",root.transform,new Rect(0,0,402,874));v.popup=modal.gameObject;
                var dim=modal.gameObject.AddComponent<UnityEngine.UI.Image>();dim.color=new Color(29/255f,29/255f,29/255f,.7f);dim.raycastTarget=true;
                dim.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hapio/Story/StageStories/FigmaInkOverlay.mat");
                GameScreenEdgeCover.Ensure(dim);
                var panel=Rect("QuitCard",modal,new Rect(61,340,281,200));
                var border=panel.gameObject.AddComponent<LabRoundedGraphic>();border.radius=23;border.color=new Color(248/255f,216/255f,180/255f,1);border.raycastTarget=true;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic=true;
                var inner=Rect("PaperClip",panel,new Rect(5,5,271,190));
                var innerMask=inner.gameObject.AddComponent<LabRoundedGraphic>();innerMask.radius=18;innerMask.color=Color.white;
                inner.gameObject.AddComponent<Mask>().showMaskGraphic=false;
                var paper=Picture(inner,"Paper",new Rect(-73.5f,-186.5f,357,528));paper.GetComponent<UnityEngine.UI.Image>().material=v.paper.GetComponent<UnityEngine.UI.Image>().material;
                Text(panel,"Title","정말 그만 두겠소?",new Rect(5,29.5f,271,36),27,true,new Color(32/255f,28/255f,24/255f));
                Text(panel,"Body","지금 그만 두면 첫 요괴부터\n다시 봉인해야 하오.",new Rect(5,84,271,44),18,false,new Color(44/255f,44/255f,39/255f));
                v.confirmQuit=Button(panel,"ConfirmQuit",new Rect(20.5f,147.5f,114,38),GameLobbySeatColors.Disabled,20);
                Text(v.confirmQuit.transform,"Caption","그만두겠소.",new Rect(0,11.5f,114,20),15.345f,true,GameLobbySeatColors.LightText);
                v.cancelQuit=Button(panel,"CancelQuit",new Rect(151.5f,147.5f,114,38),GameLobbySeatColors.Brown,20);
                Text(v.cancelQuit.transform,"Caption","계속하겠소.",new Rect(0,11.5f,114,20),15.345f,true,GameLobbySeatColors.LightText);
                v.ResetView();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally {Object.DestroyImmediate(root);}
        }
        static RectTransform Rect(string name,Transform parent,Rect b)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);StoryStageReadyView.Place(r,b);return r;
        }
        static RectTransform Picture(Transform parent,string name,Rect b)
        {
            var r=Rect(name,parent,b);var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();
            i.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png");i.raycastTarget=false;return r;
        }
        static UnityEngine.UI.Button Button(Transform parent,string name,Rect b,Color color,float radius)
        {
            var r=Rect(name,parent,b);var g=r.gameObject.AddComponent<LabRoundedGraphic>();g.radius=radius;g.color=color;g.raycastTarget=true;
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=g;
            var colors=button.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=colors.disabledColor=Color.white;colors.pressedColor=new Color(.85f,.85f,.85f);button.colors=colors;return button;
        }
        static UnityEngine.UI.Text Text(Transform parent,string name,string value,Rect b,float size,bool bold,Color color,float line=0)
        {
            return StageStoryAssets.Element(parent,new StageStoryAssets.Node{name=name,nodeId="687",kind="text",text=value,x=b.x,y=b.y,width=b.width,height=b.height,size=size,bold=bold,align="CENTER",lineHeight=line,color=new StageStoryAssets.Rgb{r=color.r,g=color.g,b=color.b}}).GetComponentInChildren<UnityEngine.UI.Text>();
        }
    }
}
