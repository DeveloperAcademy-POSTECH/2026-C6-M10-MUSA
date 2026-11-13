using System;
using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    [Category("GPU")]
    public sealed class GameViewerReturnBackgroundTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("missing-entry")]
        [TestCase("missing-sprite")]
        [TestCase("missing-texture")]
        public void ReturnBackgroundSurvivesMissingSourceAndRecoversWithoutRebuildingButton(string failure)
        {
            var library = Resources.Load<GameFigmaStrokeLibrary>("Hapio/UI/FigmaStrokeLibrary");
            var original = library.surfaces;
            var source = GameFigmaStrokeLibrary.Find("ResultAction");
            var root = new GameObject("Return background fixture", typeof(RectTransform), typeof(Canvas));
            var camera = new GameObject("Return background camera").AddComponent<Camera>();
            var target = new RenderTexture(320, 120, 24);
            Sprite broken = null;
            Texture2D ownedTexture = null;
            try
            {
                Assert.That(source?.sprite, Is.Not.Null);
                root.layer = 30;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.orthographic = true;
                camera.orthographicSize = 60;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 30;
                camera.targetTexture = target;
                var view = root.AddComponent<GameSealedGourdView>();
                view.design = (RectTransform)root.transform;
                var go = new GameObject("Return", typeof(RectTransform), typeof(Image), typeof(Button));
                go.layer = 30;
                go.transform.SetParent(root.transform, false);
                var button = go.GetComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                var theme = Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
                typeof(GameSealedGourdView).GetMethod("BuildButton", Private)
                    .Invoke(view, new object[] { button, "결과로 돌아가기", theme.medium, 755f, true });
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.anchoredPosition = Vector2.zero;
                var artwork = go.GetComponentInChildren<GameFigmaSurface>(true);
                var fill = go.GetComponentsInChildren<LabRoundedGraphic>(true).Single();
                Assert.That(artwork.enabled && !fill.enabled, Is.True);
                AssertRendered(camera, target, artwork);

                if (failure == "missing-entry") library.surfaces = original.Where(item => item != source).ToArray();
                else
                {
                    if (failure == "missing-texture")
                    {
                        ownedTexture = new Texture2D(8, 8);
                        broken = Sprite.Create(ownedTexture, new Rect(0, 0, 8, 8), Vector2.one * .5f);
                        Object.DestroyImmediate(ownedTexture);
                        Assert.That(broken.texture == null, Is.True, "Only the owned test texture was removed.");
                    }
                    library.surfaces = original.Select(item => item == source ? Copy(source, broken) : item).ToArray();
                }
                Refresh(view);
                Assert.That(fill.enabled && !artwork.enabled, Is.True);
                Assert.That(fill.color, Is.EqualTo(GameLobbySeatColors.Versus));
                AssertRendered(camera, target, fill);

                // A fresh row with the same name must replace the graphic's old cached row.
                library.surfaces = original.Select(item => item == source ? Copy(source, source.sprite) : item).ToArray();
                Refresh(view);
                Assert.That(artwork.enabled && !fill.enabled, Is.True);
                Assert.That(artwork.mainTexture, Is.SameAs(source.sprite.texture));
                AssertRendered(camera, target, artwork);
                Assert.That(go.GetComponentsInChildren<GameFigmaSurface>(true).Length, Is.EqualTo(1));
                Assert.That(go.GetComponentsInChildren<LabRoundedGraphic>(true).Length, Is.EqualTo(1));
                Assert.That(go.GetComponentInChildren<Text>().text, Is.EqualTo("결과로 돌아가기"));
                Assert.That(button.targetGraphic.enabled && button.targetGraphic.raycastTarget, Is.True);
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(234, 45)));
            }
            finally
            {
                library.surfaces = original;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
                if (broken != null) Object.DestroyImmediate(broken);
                if (ownedTexture != null) Object.DestroyImmediate(ownedTexture);
            }
        }

        private static void Refresh(GameSealedGourdView view) => typeof(GameSealedGourdView)
            .GetMethod("RefreshReturnBackground", Private).Invoke(view, new object[] { false });

        private static GameFigmaStrokeLibrary.Surface Copy(GameFigmaStrokeLibrary.Surface source, Sprite sprite)
            => new GameFigmaStrokeLibrary.Surface { name = source.name, source = source.source, sprite = sprite,
                size = source.size, offset = source.offset, exportSize = source.exportSize };

        private static void AssertRendered(Camera camera, RenderTexture target, Graphic background)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var mesh = background.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(4), "The visible background needs actual canvas geometry.");
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                read.Apply();
                Assert.That(read.GetPixels32().Count(pixel => pixel.a > 128), Is.GreaterThan(5000),
                    "The render must contain the filled button, not only its caption.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(read);
            }
        }
    }
}
