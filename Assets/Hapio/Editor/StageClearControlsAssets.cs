using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Hapio.Editor
{
    public static class StageClearControlsAssets
    {
        [MenuItem("Hapio/Story/Update Stage Clear Host Controls")]
        public static void Build()
        {
            string[] names = {"Gumiho", "Yagwanggui", "Jigwi"};
            for (int i=0;i<3;i++)
            {
                string path=StageStoryAssets.Root+"/Views/"+names[i]+"_After_02.prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=root.GetComponent<StoryCutView>();
                    var controls=root.GetComponent<StoryStageClearControls>() ?? root.AddComponent<StoryStageClearControls>();
                    controls.finalStage=i==2;
                    if(view.continueButton!=null) view.continueButton.gameObject.SetActive(false);
                    view.continueButton=null; view.requireContinueButton=true; view.waitForEntrance=true;
                    var heading=root.GetComponentsInChildren<UnityEngine.UI.Text>(true).FirstOrDefault(t=>t.text.StartsWith("다음 요괴"));
                    if(heading==null) heading=Label(root.transform,"NextYokaiHeading", "다음 요괴 잡으러 ...",new Rect(80,i==0?645:143,243,30),25,i==0?"51473b":"41392f",true);
                    else
                    {
                        heading.text="다음 요괴 잡으러 ...";
                        var rect=(RectTransform)heading.transform.parent;
                        rect.sizeDelta=new Vector2(243,30);
                        rect.anchoredPosition=new Vector2(201.5f,-(i==0?660:158));
                        heading.fontSize=32; heading.rectTransform.localScale=Vector3.one*(25f/32);
                        heading.rectTransform.sizeDelta=new Vector2(243,30)/(25f/32);
                        heading.alignment=TextAnchor.UpperCenter;
                    }
                    controls.heading=heading;
                    if(controls.waiting==null) controls.waiting=Label(root.transform,"HostWaiting", "방장의 시작을 기다리시오.",new Rect(129,i==0?670:172,143,24),15,"68645c",true);
                    if(controls.nextSeal==null)
                    {
                        var hit=StoryWorkspaceBuilder.Rect("NextSeal",root.transform,new Rect(270,764,114,52));
                        var img=hit.gameObject.AddComponent<UnityEngine.UI.Image>(); img.color=Color.clear;
                        controls.nextSeal=hit.gameObject.AddComponent<UnityEngine.UI.Button>();controls.nextSeal.targetGraphic=img;
                        Label(hit,"Caption","다음 봉인",new Rect(16,14,71,22),18.898f,"3f3d38",false);
                        StageStoryAssets.Element(hit,new StageStoryAssets.Node {name="Arrow",nodeId="4593:3048",kind="image",assetPath=StageStoryAssets.Art+"Polish/ContinueArrow.png",x=91.446f,y=19.866f,width=12.1613f,height=10.2676f,rotation=-90.5455f});
                    }
                    controls.Present(false,false,false);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
        private static UnityEngine.UI.Text Label(Transform parent,string name,string text,Rect bounds,float size,string hex,bool bold)
        {
            Color color; ColorUtility.TryParseHtmlString("#"+hex,out color);
            return StageStoryAssets.Element(parent,new StageStoryAssets.Node {name=name,nodeId="host-clear-647",kind="text",text=text,x=bounds.x,y=bounds.y,width=bounds.width,height=bounds.height,size=size,bold=bold,align="CENTER",color=new StageStoryAssets.Rgb {r=color.r,g=color.g,b=color.b}}).GetComponentInChildren<UnityEngine.UI.Text>();
        }
    }
}
