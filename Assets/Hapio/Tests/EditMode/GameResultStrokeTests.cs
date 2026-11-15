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

        [Test]
        public void StoryResultStrokeUsesTheRoundedGraphicAndTextButtonsShowPressFeedback()
        {
            var page = new GameObject("Story actions", typeof(RectTransform));
            try
            {
                var button = GameViewerHelpUi.Hit(page.transform, "Retry", 0, 0, 234, 45);
                var shape = GameViewerHelpUi.Rect(page.transform, "Story Brush Layer", 0, 0, 234, 45)
                    .gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
                GameFigmaButton.AttachResultArtwork(button, shape);
                var stroke = button.GetComponent<GameFigmaButton>();
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                Assert.That(stroke.artwork.SurfaceName, Is.EqualTo("ResultAction"));
                Assert.That(shape.enabled, Is.False);
                Assert.That(stroke.artwork.raycastTarget, Is.False);
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
                stroke.OnPointerDown(pointer);
                Assert.That(stroke.artwork.SurfaceName, Is.EqualTo("ResultActionPressed"));

                var exit = GameViewerHelpUi.Hit(page.transform, "Exit", 0, 50, 95, 45);
                var caption = GameViewerHelpUi.Label(exit.transform, "Label", "나가기", null, 18, Color.black, 0, 0, 95, 45);
                GameFigmaButton.AttachResultText(exit, caption);
                var feedback = exit.GetComponent<GameFigmaButton>();
                var fill = exit.GetComponentInChildren<C6Lab.LabRoundedGraphic>(true);
                Assert.That(fill.enabled, Is.False);
                feedback.OnPointerDown(pointer);
                Assert.That(fill.enabled, Is.True);
                Assert.That(caption.color, Is.EqualTo(GameResultStrokes.ActionNormal));
                feedback.OnPointerUp(pointer);
                Assert.That(fill.enabled, Is.False);
                Assert.That(caption.color, Is.EqualTo(Color.black));
            }
            finally { Object.DestroyImmediate(page); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerritoryResultButtonDrawsAndRemainsClickableWhenStrokeArtIsMissing(bool retry)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePaths.StageResult, OpenSceneMode.Additive);
            var canvas = new GameObject("Result background test", typeof(Canvas));
            var camera = new GameObject("Result background camera").AddComponent<Camera>();
            var target = new RenderTexture(402, 874, 24);
            var library = Resources.Load<GameFigmaStrokeLibrary>("Hapio/UI/FigmaStrokeLibrary");
            var allSurfaces = library.surfaces;
            var normal = GameFigmaStrokeLibrary.Find("ResultAction");
            Sprite savedSprite = normal.sprite;
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<GameTerritoryResultView>(true)).Single();
                GameResultStrokes.Apply(view);
                var button = Object.Instantiate(retry ? view.table.nextButton : view.table.viewerButton, canvas.transform);
                button.gameObject.SetActive(true);
                button.interactable = true;
                var part = button.GetComponent<GameFigmaButton>();
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.anchoredPosition = Vector2.zero;
                var c = canvas.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = camera;
                c.planeDistance = 1;
                camera.targetTexture = target;

                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                AssertBackground(retry ? part.original : part.artwork, camera);
                Assert.That(part.artwork.enabled, Is.EqualTo(!retry));
                if (retry) Assert.That(part.original.color, Is.EqualTo(GameResultStrokes.SecondaryActionNormal));
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
                var caption = button.GetComponentInChildren<Text>();
                Assert.That(caption.color, Is.EqualTo((Color)new Color32(250, 246, 241, 255)));
                Assert.That(caption.fontSize * caption.rectTransform.localScale.x, Is.EqualTo(20));

                part.OnPointerDown(pointer);
                Assert.That(part.artwork.SurfaceName, Is.EqualTo(retry ? "ResultAction" : "ResultActionPressed"));
                AssertBackground(part.artwork, camera);
                part.OnPointerUp(pointer);

                library.surfaces = allSurfaces.Where(item => item.name != "ResultActionPressed").ToArray();
                part.OnPointerDown(pointer);
                Assert.That(part.artwork.SurfaceName, Is.EqualTo("ResultAction"));
                AssertBackground(part.artwork, camera);
                part.OnPointerUp(pointer);

                normal.sprite = null;
                part.Refresh();
                Assert.That(part.artwork.enabled, Is.False);
                Assert.That(part.original.raycastTarget, Is.True);
                AssertBackground(part.original, camera);
                var normalColor = retry ? GameResultStrokes.SecondaryActionNormal : GameResultStrokes.ActionNormal;
                Assert.That(part.original.color, Is.EqualTo(normalColor));
                part.OnPointerDown(pointer);
                Assert.That(part.original.color, Is.EqualTo(retry ? GameResultStrokes.ActionNormal : GameResultStrokes.ActionPressed));
                part.OnPointerUp(pointer);
                Assert.That(part.original.color, Is.EqualTo(normalColor));
                button.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1));
                button.interactable = false;
                button.OnPointerClick(pointer);
                Assert.That(clicks, Is.EqualTo(1));

                normal.sprite = savedSprite;
                part.Refresh();
                Assert.That(part.original.enabled, Is.EqualTo(retry));
                AssertBackground(retry ? part.original : part.artwork, camera);
            }
            finally
            {
                normal.sprite = savedSprite;
                library.surfaces = allSurfaces;
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(canvas);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertBackground(Graphic graphic, Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Assert.That(graphic.isActiveAndEnabled, Is.True);
            Assert.That(graphic.canvasRenderer.GetMesh()?.vertexCount, Is.GreaterThanOrEqualTo(4));
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
