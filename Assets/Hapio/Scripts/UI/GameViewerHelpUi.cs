using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Shared 402x874 authoring helpers; all artwork comes from the local Figma library.</summary>
    public static class GameViewerHelpUi
    {
        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            r.SetParent(parent,false); r.gameObject.layer=parent.gameObject.layer;
            Place(r,x,y,width,height); return r;
        }
        public static void Place(RectTransform r,float x,float y,float width,float height)
        {
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(width,height);
        }
        public static void Stretch(RectTransform r)
        { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
        public static GameFigmaSurface Surface(Transform parent,string key,float x,float y,float w,float h)
        {
            var s=Rect(parent,key,x,y,w,h).gameObject.AddComponent<GameFigmaSurface>();
            s.Show(key);s.raycastTarget=false;return s;
        }
        public static Text Label(Transform parent,string name,string value,Font font,int size,Color color,float x,float y,float w,float h,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();
            t.text=value;t.font=font;t.fontSize=size;t.color=color;t.alignment=alignment;
            t.horizontalOverflow=HorizontalWrapMode.Overflow;t.verticalOverflow=VerticalWrapMode.Overflow;t.raycastTarget=false;return t;
        }
        public static Button Hit(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=Color.clear;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.None;return b;
        }
        public static RectTransform Design(Transform parent,string name)
        {
            var r=Rect(parent,name,0,0,402,874);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;return r;
        }
        public static void Fit(RectTransform design)
        {
            var canvas=design.GetComponentInParent<Canvas>().rootCanvas;
            float factor=canvas.scaleFactor;
            design.localScale=Vector3.one*Mathf.Min(Screen.safeArea.width/factor/402f,Screen.safeArea.height/factor/874f);
        }
    }
}
