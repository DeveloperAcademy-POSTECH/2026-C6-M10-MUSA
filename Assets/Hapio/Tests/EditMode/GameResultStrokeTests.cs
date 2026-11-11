using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class GameResultStrokeTests
    {
        [TestCase(402, 874, 0, 0)]
        [TestCase(402, 874, 62, 34)]
        [TestCase(430, 932, 62, 34)]
        [TestCase(768, 1024, 24, 20)]
        public void GameOverPaperCoversDisplayOutsideSafeArea(int width, int height, int top, int bottom)
        {
            var scene=EditorSceneManager.OpenScene(GameScenePaths.GameOver,OpenSceneMode.Additive);
            try
            {
                var view=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameOverCharacterView>(true)).Single();
                var root=(RectTransform)view.GetComponentInParent<Canvas>().transform;
                root.GetComponent<Canvas>().enabled=false;
                root.sizeDelta=new Vector2(width,height);
                var safe=(RectTransform)view.design.parent;
                safe.anchorMin=new Vector2(0,bottom/(float)height);
                safe.anchorMax=new Vector2(1,1-top/(float)height);
                safe.offsetMin=safe.offsetMax=Vector2.zero;
                view.design.localScale=Vector3.one*Mathf.Min(width/402f,(height-top-bottom)/874f);
                var paper=view.design.Find("Parchment").GetComponent<RawImage>();
                view.gameObject.SetActive(true);
                paper.RecalculateClipping();
                paper.SetClipRect(new Rect(0,0,100,100),true);
                view.CoverCanvasBackground();
                Assert.That(paper.maskable,Is.False,"Full-screen paper must bypass the safe-area content mask.");
                Assert.That(paper.canvasRenderer.hasRectClipping,Is.False,"Release the clip registered before the view's first layout.");
                Assert.That(paper.texture,Is.Not.Null);
                var corners=new Vector3[4];root.GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var local=paper.rectTransform.InverseTransformPoint(corner);
                    var bounds=paper.rectTransform.rect;
                    Assert.That(local.x,Is.InRange(bounds.xMin-.01f,bounds.xMax+.01f));
                    Assert.That(local.y,Is.InRange(bounds.yMin-.01f,bounds.yMax+.01f));
                }
                Assert.That(view.design.GetComponent<RectMask2D>().enabled,Is.True);
                if(top==0 && bottom==0)
                    Assert.That(paper.rectTransform.sizeDelta,Is.EqualTo(new Vector2(704,1041)));
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }

        [TestCase("Home", "SealReady")]
        [TestCase("Viewer", "ResultAction")]
        [TestCase("Next", "ResultAction")]
        public void SavedResultBrushKeepsOriginalButtonTapAndDisabledGate(string action,string key)
        {
            var scene=EditorSceneManager.OpenScene(action=="Home"?GameScenePaths.GameOver:GameScenePaths.StageResult,OpenSceneMode.Additive);
            var canvas=new GameObject("Stroke click test",typeof(Canvas),typeof(GraphicRaycaster));
            var camera=new GameObject("Stroke test camera").AddComponent<Camera>();
            var events=new GameObject("Stroke test events",typeof(EventSystem));
            var target=new RenderTexture(402,874,24);
            try
            {
                var screen=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameScreen>(true)).Single();
                Button source=action=="Home"?screen.gameOverView.homeButton:action=="Viewer"?screen.territoryView.table.viewerButton:screen.territoryView.table.nextButton;
                var saved=source.GetComponent<GameFigmaButton>();
                Assert.That(saved,Is.Not.Null);
                Assert.That(saved.artwork.SurfaceName,Is.EqualTo(key));
                Assert.That(saved.original.enabled,Is.False);
                var before=((RectTransform)source.transform).rect;
                GameFigmaButton.Attach(source,key);GameFigmaButton.Attach(source,key);
                Assert.That(source.GetComponentsInChildren<GameFigmaSurface>(true).Length,Is.EqualTo(1));
                Assert.That(((RectTransform)source.transform).rect,Is.EqualTo(before));
                var button=Object.Instantiate(source,canvas.transform);button.gameObject.SetActive(true);button.interactable=true;
                var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=Vector2.zero;
                var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;camera.targetTexture=target;
                Canvas.ForceUpdateCanvases();camera.Render();
                var pointer=new PointerEventData(events.GetComponent<EventSystem>()){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
                var hits=new List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                Assert.That(hits,Is.Not.Empty);
                var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(handler,Is.SameAs(button.gameObject));
                int clicks=0;button.onClick.AddListener(()=>clicks++);ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);
                Assert.That(clicks,Is.EqualTo(1));
                button.interactable=false;ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);
                Assert.That(clicks,Is.EqualTo(1));
            }
            finally
            {
                camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(canvas);Object.DestroyImmediate(events);EditorSceneManager.CloseScene(scene,true);
            }
        }
    }
}
