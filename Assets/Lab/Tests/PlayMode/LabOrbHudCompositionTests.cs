using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabOrbHudCompositionTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompositionPreservesLayoutProjectionAndParentsAndRestoresThemOnDisable()
        {
            RequireGraphics();
            CreateFixture(out LabHud hud, out LabOrbBoard board, out Camera camera,
                out Transform paper, out Transform backdrop);
            Rect viewport = camera.pixelRect;
            Matrix4x4 projection = camera.projectionMatrix;
            float aspect = camera.aspect, size = camera.orthographicSize;
            int originalMask = camera.cullingMask, originalBattleMask = hud.battleCamera.cullingMask;
            var originals = new List<Transform>();
            for (int i = 0; i < hud.battleUi.transform.childCount; i++) originals.Add(hud.battleUi.transform.GetChild(i));
            var decorations = new[] { hud.staminaFill.transform.parent, hud.leftNeighborBadge.transform, hud.rightNeighborBadge.transform };
            Canvas.ForceUpdateCanvases();
            var corners = decorations.Select(Corners).ToArray();
            Vector2 screenPoint = viewport.center + viewport.size * .17f;
            Ray ray = camera.ScreenPointToRay(screenPoint);

            LabOrbHudComposition.Attach(hud, board, camera);
            yield return null;
            var composition = hud.GetComponent<LabOrbHudComposition>();
            Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
            Assert.That(camera.targetTexture, Is.Null, "The board input camera must continue using screen coordinates.");
            Assert.That(camera.pixelRect, Is.EqualTo(viewport));
            Assert.That(camera.projectionMatrix, Is.EqualTo(projection));
            Assert.That(camera.aspect, Is.EqualTo(aspect));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
            Assert.That(camera.ScreenPointToRay(screenPoint).origin, Is.EqualTo(ray.origin));
            Assert.That(camera.ScreenPointToRay(screenPoint).direction, Is.EqualTo(ray.direction));
            Assert.That(composition.ForegroundCamera.targetTexture, Is.SameAs(composition.Texture));
            Assert.That(composition.ForegroundCamera.projectionMatrix, Is.EqualTo(projection));
            Assert.That(composition.ForegroundCamera.cullingMask & (1 << board.gameObject.layer), Is.Not.Zero);
            Assert.That(camera.cullingMask & (1 << board.gameObject.layer), Is.Zero);
            Assert.That(composition.Display.raycastTarget, Is.False, "The full viewport image must never consume a gesture.");
            Assert.That(composition.Display.transform.parent, Is.SameAs(hud.battleUi.transform));
            for (int i = 0; i < decorations.Length; i++)
            {
                Assert.That(decorations[i].parent, Is.SameAs(hud.battleUi.transform));
                Assert.That(Corners(decorations[i]), Is.EqualTo(corners[i]));
                Assert.That(decorations[i].GetSiblingIndex(), Is.LessThan(composition.Display.transform.GetSiblingIndex()));
            }
            Assert.That(hud.battleUi.transform.Find("ForegroundControl").GetSiblingIndex(),
                Is.GreaterThan(composition.Display.transform.GetSiblingIndex()));
            var actual = new Vector3[4];
            composition.Display.rectTransform.GetWorldCorners(actual);
            Assert.That(Vector2.Distance(actual[0], viewport.min), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(actual[2], viewport.max), Is.LessThan(.01f));
            int rear = LayerMask.NameToLayer(LabOrbHudComposition.PaperLayerName);
            Assert.That(paper.gameObject.layer, Is.EqualTo(rear));
            Assert.That(backdrop.gameObject.layer, Is.EqualTo(rear));
            Assert.That(composition.ForegroundCamera.cullingMask & (1 << rear), Is.Zero);
            Assert.That(hud.battleCamera.cullingMask & (1 << rear), Is.Zero);

            // Resizing only the display viewport must update its target without reconfiguring the board.
            RenderTexture previous = composition.Texture;
            camera.rect = new Rect(.1f, .05f, .8f, .65f);
            yield return null;
            Assert.That(composition.Texture, Is.Not.SameAs(previous));
            Assert.That(composition.Texture.width, Is.EqualTo(Mathf.RoundToInt(camera.pixelWidth)));
            Assert.That(composition.Texture.height, Is.EqualTo(Mathf.RoundToInt(camera.pixelHeight)));
            Assert.That(camera.targetTexture, Is.Null);

            Camera generated = composition.ForegroundCamera;
            RenderTexture owned = composition.Texture;
            composition.enabled = false;
            Assert.That(composition.IsCompositing, Is.False);
            Assert.That(camera.cullingMask, Is.EqualTo(originalMask));
            Assert.That(hud.battleCamera.cullingMask, Is.EqualTo(originalBattleMask));
            Assert.That(paper.gameObject.layer, Is.EqualTo(board.gameObject.layer));
            Assert.That(backdrop.gameObject.layer, Is.EqualTo(board.gameObject.layer));
            Assert.That(generated.enabled, Is.False);
            Assert.That(owned.IsCreated(), Is.False);
            yield return null;
            Assert.That(generated == null, Is.True);
            Assert.That(owned == null, Is.True);
            for (int i = 0; i < originals.Count; i++) Assert.That(originals[i].GetSiblingIndex(), Is.EqualTo(i));
            composition.enabled = true;
            yield return null;
            Assert.That(composition.IsCompositing, Is.True, "Returning to battle must recreate the foreground resources.");
        }

        [UnityTest]
        public IEnumerator InvalidCompositionKeepsTheNormalCameraAndSavedLayoutUsable()
        {
            RequireGraphics();
            CreateFixture(out LabHud hud, out LabOrbBoard board, out Camera camera, out Transform paper, out _);
            int mask = camera.cullingMask;
            var order = new List<Transform>();
            for (int i = 0; i < hud.battleUi.transform.childCount; i++) order.Add(hud.battleUi.transform.GetChild(i));
            paper.name = "Missing saved paper";
            LogAssert.Expect(LogType.Warning, new Regex("C6_LAB_ORB_HUD_FALLBACK.*paper renderer is missing"));
            LabOrbHudComposition.Attach(hud, board, camera);
            yield return null;
            var composition = hud.GetComponent<LabOrbHudComposition>();
            Assert.That(composition.IsCompositing, Is.False);
            Assert.That(composition.InitializationError, Does.Contain("paper renderer"));
            Assert.That(camera.cullingMask, Is.EqualTo(mask));
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(composition.Display, Is.Null);
            for (int i = 0; i < order.Count; i++) Assert.That(order[i].GetSiblingIndex(), Is.EqualTo(i));
        }

        [UnityTest]
        public IEnumerator ActualForegroundCameraKeepsTheRearArtworkOutAndPreservesSpriteAlpha()
        {
            RequireGraphics();
            CreateFixture(out LabHud hud, out LabOrbBoard board, out Camera camera,
                out Transform paper, out Transform backdrop);
            camera.orthographicSize = 2f;
            camera.aspect = 1f;
            Shader paperShader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(paperShader != null && paperShader.isSupported, Is.True);
            var paperMaterial = new Material(paperShader);
            paperMaterial.SetColor("_BaseColor", Color.blue);
            var white = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            white.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray());
            white.Apply();
            var sprite = Sprite.Create(white, new Rect(0f, 0f, 2f, 2f), Vector2.one * .5f, 2f);
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Real opaque geometry and a real SpriteRenderer would fill every empty pixel
                // if either rear object accidentally remained in the foreground camera's mask.
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                paper.gameObject.AddComponent<MeshFilter>().sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(quad);
                paper.GetComponent<Renderer>().sharedMaterial = paperMaterial;
                paper.localPosition = new Vector3(0f, 0f, 1f);
                paper.localScale = new Vector3(20f, 20f, 1f);
                var rearSprite = backdrop.GetComponent<SpriteRenderer>();
                rearSprite.sprite = sprite;
                rearSprite.color = Color.green;
                backdrop.localPosition = new Vector3(0f, 0f, 12f);
                backdrop.localScale = new Vector3(20f, 20f, 1f);
                SpriteRenderer opaque = AddSprite("Opaque foreground orb", board.transform, sprite,
                    Vector3.zero, Color.red);
                AddSprite("Half-transparent foreground orb", board.transform, sprite,
                    new Vector3(1.1f, 0f, 0f), new Color(1f, 0f, 0f, .5f));
                LabOrbHudComposition.Attach(hud, board, camera);
                yield return null;
                var composition = hud.GetComponent<LabOrbHudComposition>();
                Assert.That(composition.IsCompositing, Is.True, composition.InitializationError);
                Assert.That(opaque.gameObject.layer, Is.EqualTo(board.gameObject.layer));
                Assert.That(paper.gameObject.layer, Is.Not.EqualTo(board.gameObject.layer));
                Assert.That(backdrop.gameObject.layer, Is.Not.EqualTo(board.gameObject.layer));

                // Exercise the production camera and pipeline, not just the UI composite material.
                composition.ForegroundCamera.Render();
                RenderTexture captured = composition.Texture;
                RenderTexture.active = captured;
                readback = new Texture2D(captured.width, captured.height, TextureFormat.RGBA32, false, true);
                readback.ReadPixels(new Rect(0f, 0f, captured.width, captured.height), 0, 0);
                readback.Apply();
                Color empty = Sample(readback, .1f, .1f);
                Assert.That(empty.a, Is.LessThan(.02f), "No orb means transparent foreground, even over the opaque blue paper and green backdrop.");
                Assert.That(Mathf.Max(empty.r, empty.g, empty.b), Is.LessThan(.02f));
                Color core = Sample(readback, .5f, .5f);
                Assert.That(core.a, Is.GreaterThan(.98f));
                Assert.That(core.r, Is.GreaterThan(.95f));
                Assert.That(Mathf.Max(core.g, core.b), Is.LessThan(.03f), "The core must show the red foreground, without rear artwork contamination.");
                Color half = Sample(readback, .775f, .5f);
                Assert.That(half.a, Is.EqualTo(.5f).Within(.04f), "The real camera must retain half opacity, rather than force alpha to one or square it.");
                // Depending on the project colour space, stored premultiplied .5 is read as
                // linear .5 or its sRGB encoding (~.735); both keep the red channel and alpha.
                Assert.That(half.r, Is.InRange(.45f, .80f));
                Assert.That(Mathf.Max(half.g, half.b), Is.LessThan(.03f));
            }
            finally
            {
                RenderTexture.active = previous;
                if (root != null) root.SetActive(false);
                if (readback != null) Object.DestroyImmediate(readback);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(white);
                Object.DestroyImmediate(paperMaterial);
            }
        }

        private static SpriteRenderer AddSprite(string name, Transform parent, Sprite sprite,
            Vector3 position, Color color)
        {
            var root = new GameObject(name, typeof(SpriteRenderer));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        private static Color Sample(Texture2D texture, float x, float y) => texture.GetPixel(
            Mathf.RoundToInt((texture.width - 1) * x), Mathf.RoundToInt((texture.height - 1) * y));

        [Test]
        public void ForegroundCompositePreservesHalfTransparentColourWithoutMultiplyingAlphaTwice()
        {
            RequireGraphics();
            Shader shader = Resources.Load<Shader>(LabOrbHudComposition.CompositeShaderResource);
            Assert.That(shader != null && shader.isSupported, Is.True);
            var material = new Material(shader);
            var input = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var target = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            int previousDepthTest = Shader.GetGlobalInt("unity_GUIZTestMode");
            try
            {
                input.SetPixels(Enumerable.Repeat(new Color(0f, 0f, .5f, .5f), 4).ToArray());
                input.Apply();
                Assert.That(target.Create(), Is.True);
                RenderTexture.active = target;
                GL.Clear(false, true, Color.green);
                Shader.SetGlobalInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                Graphics.Blit(input, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, 8f, 8f), 0, 0);
                readback.Apply();
                Color actual = readback.GetPixel(4, 4);
                Assert.That(actual.r, Is.LessThan(.03f));
                Assert.That(actual.g, Is.EqualTo(.5f).Within(.04f));
                Assert.That(actual.b, Is.EqualTo(.5f).Within(.04f), "The captured blue must not darken to .25 by multiplying alpha twice.");
                Assert.That(actual.a, Is.EqualTo(1f).Within(.02f));
            }
            finally
            {
                Shader.SetGlobalInt("unity_GUIZTestMode", previousDepthTest);
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(material);
            }
        }

        private void CreateFixture(out LabHud hud, out LabOrbBoard board, out Camera camera,
            out Transform paper, out Transform backdrop)
        {
            root = new GameObject("Orb HUD composition fixture");
            var canvasObject = new GameObject("SavedOverlay", typeof(RectTransform), typeof(Canvas), typeof(LabHud));
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            hud = canvasObject.GetComponent<LabHud>();
            hud.enabled = false;
            var battle = NewRect("BattleUi", canvasObject.transform);
            battle.anchorMin = Vector2.zero; battle.anchorMax = Vector2.one; battle.sizeDelta = Vector2.zero;
            hud.battleUi = battle.gameObject;
            NewRect("ForegroundControl", battle);
            var stamina = NewRect("FigmaStaminaBar", battle);
            stamina.anchorMin = stamina.anchorMax = new Vector2(.55f, .05f);
            stamina.sizeDelta = new Vector2(223f, 44f);
            hud.staminaFill = NewRect("StaminaFill", stamina).gameObject.AddComponent<UnityEngine.UI.Image>();
            var left = NewRect("LeftNeighborBadge", battle); left.anchoredPosition = new Vector2(-120f, 25f);
            var right = NewRect("RightNeighborBadge", battle); right.anchoredPosition = new Vector2(120f, 25f);
            hud.leftNeighborBadge = left.gameObject; hud.rightNeighborBadge = right.gameObject;
            var boardObject = new GameObject("OrbBoard"); boardObject.layer = LayerMask.NameToLayer("LabOrbBoard");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            paper = new GameObject("OrbBoardPlate", typeof(MeshRenderer)).transform;
            paper.SetParent(board.transform, false); paper.gameObject.layer = boardObject.layer;
            var cameraObject = new GameObject("InputCamera"); cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.rect = new Rect(0f, 0f, 1f, .4771167f);
            camera.cullingMask = 1 << boardObject.layer;
            hud.orbCamera = camera;
            // The saved Lab and Battle Scenes name the camera's opaque backing this way (#264), not by the old name.
            backdrop = new GameObject("Board Hanji Backing", typeof(SpriteRenderer)).transform;
            backdrop.SetParent(camera.transform, false); backdrop.gameObject.layer = boardObject.layer;
            var battleCamera = new GameObject("BattleCamera"); battleCamera.transform.SetParent(root.transform, false);
            hud.battleCamera = battleCamera.AddComponent<Camera>();
            hud.battleCamera.cullingMask = ~(1 << boardObject.layer);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = new Vector2(45f, 47f);
            return rect;
        }

        private static Vector3[] Corners(Transform transform)
        {
            var corners = new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners); return corners;
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required for the foreground render target.");
        }
    }
}
