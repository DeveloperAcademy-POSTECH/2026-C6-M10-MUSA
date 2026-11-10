using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio.Tests
{
    public sealed class GameFigmaStrokeTests
    {
        [Test]
        public void OriginalOutsideStrokeKeepsLogicalBoundsAtMobileScales()
        {
            var start = GameFigmaStrokeLibrary.Find("Start");
            Assert.That(start, Is.Not.Null);
            foreach (float scale in new[] { .75f, 1f, 3f })
            {
                Rect logical = new Rect(0,0,293*scale,56*scale);
                Rect draw = start.Bounds(logical);
                Assert.That(draw.yMax-logical.yMax, Is.EqualTo(4*scale).Within(.001f));
                Assert.That(draw.xMin, Is.LessThan(logical.xMin));
                Assert.That(draw.width, Is.EqualTo(295*scale).Within(.001f));
            }
            foreach (string key in new[] {"Start","Create","Chip","ChipSelected","RoomDark","RoomDarkPressed","RoomLight","RoomLightPressed","Mode","ModeDisabled","SealWait","SealReady","Time","Pause","Paint","TeamPaint","Stamina","StaminaCells"})
            {
                var surface = GameFigmaStrokeLibrary.Find(key);
                Assert.That(surface?.sprite, Is.Not.Null, key);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(surface.sprite));
                Assert.That(importer.mipmapEnabled, Is.False, key);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), key);
            }
        }

        [Test]
        public void SavedButtonsKeepNavigationTargetAndDoNotDrawTwoBackgrounds()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Hapio/Art/UI/GameLobby.prefab");
            try
            {
                var view = root.GetComponent<GameLobbyView>();
                var oldBounds = ((RectTransform)view.enterButton.transform).rect;
                GameFigmaButton.Apply(view); GameFigmaButton.Apply(view);
                var parts = root.GetComponentsInChildren<GameFigmaButton>(true);
                Assert.That(parts.Length, Is.EqualTo(11));
                foreach (var part in parts)
                {
                    Assert.That(part.button.targetGraphic, Is.SameAs(part.original));
                    Assert.That(part.original.enabled, Is.False);
                    Assert.That(part.artwork.raycastTarget, Is.True);
                    Assert.That(part.GetComponentsInChildren<GameFigmaSurface>(true).Length, Is.EqualTo(1));
                }
                Assert.That(((RectTransform)view.enterButton.transform).rect, Is.EqualTo(oldBounds));
                var room = view.roomTemplate.GetComponent<GameFigmaButton>();
                room.button.interactable = true; room.original.color = new Color(1,1,1,.5f); room.Refresh();
                Assert.That(room.artwork.SurfaceName, Is.EqualTo("RoomDark"));
                room.OnPointerDown(new PointerEventData(null) {button=PointerEventData.InputButton.Left});
                Assert.That(room.artwork.SurfaceName, Is.EqualTo("RoomDarkPressed"));
                room.OnPointerUp(new PointerEventData(null));
                Assert.That(room.artwork.SurfaceName, Is.EqualTo("RoomDark"));
                room.original.color = new Color(1,1,1,.7f); room.Refresh();
                Assert.That(room.artwork.SurfaceName, Is.EqualTo("RoomLight"));
                room.button.interactable = false; room.OnPointerDown(new PointerEventData(null));
                Assert.That(room.artwork.SurfaceName, Is.EqualTo("RoomLight"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void BrushChildStillRoutesTapToExistingButton()
        {
            var canvas = new GameObject("Stroke tap test",typeof(Canvas),typeof(GraphicRaycaster));
            var events = new GameObject("Events",typeof(EventSystem));
            var camera = new GameObject("Stroke test camera").AddComponent<Camera>();
            var target = new RenderTexture(402,874,24);
            try
            {
                camera.targetTexture=target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
                canvas.GetComponent<Canvas>().worldCamera = camera;
                canvas.GetComponent<Canvas>().planeDistance = 1;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Art/UI/GameLobby.prefab");
                var button = Object.Instantiate(prefab.GetComponent<GameLobbyView>().enterButton, canvas.transform);
                button.gameObject.SetActive(true);
                var rect = (RectTransform)button.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f; rect.anchoredPosition=Vector2.zero;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var data = new PointerEventData(events.GetComponent<EventSystem>()) {
                    position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center)),
                    button=PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); canvas.GetComponent<GraphicRaycaster>().Raycast(data,hits);
                Assert.That(hits, Is.Not.Empty);
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(handler, Is.SameAs(button.gameObject));
                int clicks=0; button.onClick.AddListener(()=>clicks++);
                ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
                Assert.That(clicks,Is.EqualTo(1));
                button.interactable=false; ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
                Assert.That(clicks,Is.EqualTo(1));
            }
            finally { camera.targetTexture=null; Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(canvas); Object.DestroyImmediate(events); }
        }
    }
}
