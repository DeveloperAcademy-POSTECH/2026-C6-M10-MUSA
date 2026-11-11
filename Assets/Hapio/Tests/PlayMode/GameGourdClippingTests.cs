using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// The gourd is never cut by a flat edge: in battle its part sunk below the ground still draws (the ground, the
    /// scenery and the gourd's motion are unchanged), and the completed gourd viewer draws past its square when zoomed.
    /// Saved Home → DEV solo, the real Battle and result Scenes and GPU rendering.
    /// </summary>
    [Category("Slow")]
    [Category("GPU")]
    public sealed class GameGourdClippingTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameSession session;
        private LabNetwork network;

        [UnitySetUp]
        public IEnumerator LoadSavedHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() => GameSession.Instance != null && !GameSession.Instance.IsLoadingScene, "Saved Home");
            session = GameSession.Instance; network = session.Network;
            yield return null;
            network.port = AvailableDualFamilyPort();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                network.Stop(); Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Gourd clipping cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheSunkPartOfTheGourdDrawsOverTheGroundAndNothingElseChanges([Values(1, 3)] int stage)
        {
            yield return ReachStage(stage);
            var overGround = Object.FindAnyObjectByType<GameGourdOverGround>();
            Assert.That(overGround, Is.Not.Null, "Every Hapio battle adds the pass.");
            var hanging = network.target.GetComponent<LabHangingTarget>();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(network.target.GetComponentsInChildren<Renderer>().Where(r => r.enabled)
                .All(r => (r.renderingLayerMask & GameGourdOverGround.GourdBit) != 0), Is.True, "Every gourd renderer is marked.");
            Assert.That(network.battleFloor.GetComponentsInChildren<Renderer>(true)
                .All(r => (r.renderingLayerMask & GameGourdOverGround.GroundBit) != 0), Is.True, "Every ground renderer is marked.");

            // At rest the gourd is above the ground: the pass changes no pixel at all.
            yield return HoldRope(hanging, -.3f);
            int restChanged = Difference(overGround, out Rect restArea);
            Rect restGourd = ScreenBounds(network.BattleCamera, network.target.GetComponentsInChildren<Renderer>().Where(r => r.enabled));
            TestContext.WriteLine("rest changed=" + restChanged + " area=" + restArea + " gourd=" + restGourd
                + " ropeOffset=" + hanging.CurrentRopeOffset + " floorY=" + network.battleFloor.transform.position.y);
            Assert.That(restChanged, Is.Zero, "Nothing is sunk at rest, so the frame is identical (" + restArea + ").");

            // At the lowest rope point with a full bounce it sinks: only pixels inside the gourd's screen bounds change.
            yield return HoldRope(hanging, 5f);
            Assert.That(hanging.CurrentRopeOffset, Is.GreaterThan(1f), "The gourd is held at its lowest point.");
            int changed = Difference(overGround, out Rect changedArea);
            Assert.That(changed, Is.GreaterThan(200), "The sunk part is drawn.");
            Rect gourd = ScreenBounds(network.BattleCamera, network.target.GetComponentsInChildren<Renderer>().Where(r => r.enabled));
            Assert.That(gourd.Contains(changedArea.min) && gourd.Contains(changedArea.max), Is.True,
                "Only the gourd's own pixels change (changed " + changedArea + ", gourd " + gourd + ").");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TheViewerDrawsPastItsSquareAtTheSameSizeWhenZoomed()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            LabModel model = Model();
            ulong painter = model.CopyPlayers().Single().id;
            for (int i = 0; i < 4000 && !model.PaintSurface.IsComplete; i++)
            {
                float y = 1f - 2f * ((i % 997) + .5f) / 997f, radius = Mathf.Sqrt(1f - y * y), angle = i * 2.39996323f;
                model.PaintSurface.RegisterHit(new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle)),
                    model.PaintColorFor(LabOrbElement.Fire), painter, 1);
            }
            yield return Until(() => session.State.phase == GameFlowPhase.StageResult && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult && session.PreviewTexture != null, "The stage result", 60d);
            Vector2 frame = session.PreviewFrameScale;
            Assert.That(frame.x, Is.EqualTo(GameGourdPreviewPresentation.FrameHorizontal).Within(.01f));
            Assert.That(frame.y, Is.EqualTo(GameGourdPreviewPresentation.FrameVertical).Within(.01f));
            Texture texture = session.PreviewTexture;
            Assert.That(texture.width, Is.EqualTo(Mathf.RoundToInt(512 * GameGourdPreviewPresentation.FrameHorizontal)));
            Assert.That(texture.height, Is.EqualTo(Mathf.RoundToInt(512 * GameGourdPreviewPresentation.FrameVertical)));

            session.OpenGourdViewer();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var aspect = Object.FindAnyObjectByType<GamePreviewAspect>();
            var image = aspect.GetComponent<UnityEngine.UI.RawImage>();
            var frameImage = image.transform.parent.Cast<Transform>().Select(t => t.GetComponent<UnityEngine.UI.RawImage>())
                .Single(r => r != null && r != image);
            Assert.That(image.raycastTarget && image.color.a == 0f, Is.True, "The square stays the transparent gesture surface.");
            Assert.That(frameImage.raycastTarget, Is.False);
            Assert.That(frameImage.enabled && frameImage.texture == texture, Is.True);
            Assert.That(image.rectTransform.rect.width, Is.EqualTo(image.rectTransform.rect.height).Within(.5f), "The square is kept.");
            Assert.That(frameImage.rectTransform.rect.width, Is.EqualTo(image.rectTransform.rect.width * frame.x).Within(.5f));
            Assert.That(frameImage.rectTransform.rect.height, Is.EqualTo(image.rectTransform.rect.height * frame.y).Within(.5f));
            Vector3[] a = new Vector3[4], b = new Vector3[4];
            image.rectTransform.GetWorldCorners(a); frameImage.rectTransform.GetWorldCorners(b);
            Assert.That(Vector3.Distance((a[0] + a[2]) * .5f, (b[0] + b[2]) * .5f), Is.LessThan(.5f), "Same centre.");

            // The same view inside the square: the field of view widens exactly as much as the texture.
            var preview = (LabGourdResultPreview)typeof(GameSession).GetField("preview", Private).GetValue(session);
            float half = Mathf.Tan(preview.PreviewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            Assert.That(half / frame.y, Is.EqualTo(Mathf.Tan(17.5f * Mathf.Deg2Rad)).Within(1e-4f));
            Assert.That(preview.PreviewCamera.aspect, Is.EqualTo(frame.x / frame.y).Within(1e-4f));
            session.ZoomPreview(1f);
            Assert.That(session.PreviewZoom, Is.EqualTo(1f), "Zoom range is unchanged.");
        }

        private int Difference(GameGourdOverGround pass, out Rect changedArea)
        {
            Camera camera = network.BattleCamera;
            var withPass = Capture(camera);
            pass.enabled = false;
            var without = Capture(camera);
            pass.enabled = true;
            // Stage art animated by real time (fire, sparks) moves between captures even with game time held:
            // a second capture with the pass masks those pixels out.
            var again = Capture(camera);
            int width = withPass.width, changed = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            Color32[] p = withPass.GetPixels32(), q = without.GetPixels32(), r = again.GetPixels32();
            var differs = new bool[p.Length];
            for (int i = 0; i < p.Length; i++)
                differs[i] = Mathf.Abs(p[i].r - r[i].r) + Mathf.Abs(p[i].g - r[i].g) + Mathf.Abs(p[i].b - r[i].b) <= 6
                    && Mathf.Abs(p[i].r - q[i].r) + Mathf.Abs(p[i].g - q[i].g) + Mathf.Abs(p[i].b - q[i].b) > 6;
            for (int i = 0; i < p.Length; i++)
            {
                if (!differs[i]) continue;
                // A lone pixel (no changed neighbour) is rasterisation noise, not a drawn part of the gourd.
                bool neighbour = i % width > 0 && differs[i - 1] || i % width < width - 1 && differs[i + 1]
                    || i >= width && differs[i - width] || i + width < p.Length && differs[i + width];
                if (!neighbour) continue;
                changed++;
                int x = i % width, y = i / width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            Vector2 scale = new Vector2(camera.pixelWidth / (float)width, camera.pixelHeight / (float)withPass.height);
            changedArea = changed == 0 ? Rect.zero : Rect.MinMaxRect(camera.pixelRect.x + minX * scale.x, camera.pixelRect.y + minY * scale.y,
                camera.pixelRect.x + (maxX + 1) * scale.x, camera.pixelRect.y + (maxY + 1) * scale.y);
            Object.Destroy(withPass); Object.Destroy(without); Object.Destroy(again);
            return changed;
        }

        /// <summary>The battle camera's view rendered off screen, with its own projection and the pass injected as in play.</summary>
        private static Texture2D Capture(Camera camera)
        {
            var target = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            Rect rect = camera.rect;
            camera.targetTexture = target;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            camera.targetTexture = previousTarget;
            camera.rect = rect;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            return image;
        }

        private static Rect ScreenBounds(Camera camera, System.Collections.Generic.IEnumerable<Renderer> renderers)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = camera.WorldToScreenPoint(corner);
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                    minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
                }
            }
            return Rect.MinMaxRect(minX - 2f, minY - 2f, maxX + 2f, maxY + 2f);
        }

        /// <summary>Hold the gourd's rope offset (positive is down) through the fixed steps, then let the render catch up.</summary>
        private static IEnumerator HoldRope(LabHangingTarget hanging, float offset)
        {
            FieldInfo vertical = typeof(LabHangingTarget).GetField("verticalOffset", Private);
            FieldInfo velocity = typeof(LabHangingTarget).GetField("verticalVelocity", Private);
            // A previous hold froze time for its captures; fixed steps (and this wait) need it running again.
            Time.timeScale = 1f;
            double until = Time.realtimeSinceStartupAsDouble + 4d;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                vertical.SetValue(hanging, offset); velocity.SetValue(hanging, 0f);
                yield return new WaitForFixedUpdate();
                bool lowest = offset > 0f && hanging.CurrentRopeOffset > 1.05f;
                bool highest = offset < 0f && hanging.CurrentRopeOffset < -.2f;
                if (lowest || highest) break;
            }
            Time.timeScale = 0f;
            yield return null;
        }

        private IEnumerator ReachStage(int stage)
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            for (int next = 2; next <= stage; next++)
            {
                Assert.That(network.PauseBattle(), Is.True);
                Assert.That(session.DeveloperNextStage(), Is.True);
                yield return WaitPlaying(next);
            }
            yield return Until(() => network.Snapshot != null && !network.Snapshot.paused && !network.GameStartHeld, "Battle running");
        }

        private LabModel Model() => (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Stage " + stage + " playing");

        private static IEnumerator Until(Func<bool> condition, string reason, double seconds = 60d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(reason);
                yield return null;
            }
        }

        private static ushort AvailableDualFamilyPort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("The test could not reserve a dual-family UDP Host port.");
        }
    }
}
