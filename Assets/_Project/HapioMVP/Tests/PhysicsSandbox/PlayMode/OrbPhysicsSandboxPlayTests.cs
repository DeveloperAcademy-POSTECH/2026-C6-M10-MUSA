using System.Collections;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6.Prototype.PhysicsSandbox.Tests
{
    public sealed class OrbPhysicsSandboxPlayTests
    {
        private GameObject root;
        private Scene scene;
        private ScreenLayoutConfig config;
        private OrbPhysicsSandbox sandbox;
        private OrbSandboxSeed[] seeds;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("Sandbox test", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            root = new GameObject("Sandbox fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            var cameraObject = new GameObject("Test camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6;
            seeds = new OrbSandboxSeed[4];
            for (int i = 0; i < seeds.Length; i++)
            {
                var go = new GameObject("Seed " + i, typeof(SpriteRenderer), typeof(CircleCollider2D));
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(i < 2 ? -4 : 4, i % 2 == 0 ? -1 : 1, 0);
                seeds[i] = go.AddComponent<OrbSandboxSeed>();
                seeds[i].initialBoard = i < 2 ? 0 : 1;
                seeds[i].polarity = i % 2 == 0 ? OrbPolarity.Yin : OrbPolarity.Yang;
            }
            sandbox = root.AddComponent<OrbPhysicsSandbox>();
            sandbox.Configure(config, null, camera, seeds);
            sandbox.showHelp = false;
            root.SetActive(true);
            yield return null;
            Assert.That(sandbox.IsReady, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(config);
            yield return null;
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
        }

        [Test]
        public void PreplacedSeedsBecomeSameGamePhysicsAndKeepStableIdentity()
        {
            Assert.That(sandbox.GetBoard(0).Count, Is.EqualTo(2));
            Assert.That(sandbox.GetBoard(1).Count, Is.EqualTo(2));
            foreach (var seed in seeds)
            {
                Assert.That(seed.View, Is.Not.Null);
                Assert.That(seed.View.gameObject, Is.SameAs(seed.gameObject));
                Assert.That(seed.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(seed.View.Collider.isTrigger, Is.False);
            }
        }

        [Test]
        public void LiveTuningResizesExistingCollidersAndMaterialWithoutChangingSavedConfig()
        {
            string original = JsonUtility.ToJson(config);
            var identity = seeds[0].View.GetEntityId();
            sandbox.applyGameSizeCap = false; // 이 테스트는 요청 반지름이 그대로 적용되는지만 확인
            sandbox.tuning.orbRadiusScreenFraction = .08f;
            sandbox.tuning.orbRestitution = .27f;
            sandbox.ApplyTuning();
            var collider = seeds[0].View.Collider;
            float radius = collider.radius * Mathf.Abs(collider.transform.lossyScale.x);
            Assert.That(radius, Is.EqualTo(sandbox.leftWorkspace.width * .08f).Within(.0001f));
            Assert.That(collider.sharedMaterial.bounciness, Is.EqualTo(.27f).Within(.0001f));
            Assert.That(seeds[0].View.GetEntityId(), Is.EqualTo(identity));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PortalPreservesOrbIdentityDirectionHeightAndVelocity(bool right)
        {
            var seed = seeds[0];
            var view = seed.View;
            var source = sandbox.GetBoard(0);
            var target = sandbox.GetBoard(1);
            var body = seed.GetComponent<Rigidbody2D>();
            body.position = new Vector2(right ? source.CenterBounds.xMax : source.CenterBounds.xMin,
                Mathf.Lerp(source.CenterBounds.yMin, source.CenterBounds.yMax, .7f));
            body.linearVelocity = new Vector2(right ? 3 : -3, .4f);
            source.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            sandbox.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(seed.View, Is.SameAs(view));
            Assert.That(seed.CurrentBoard, Is.EqualTo(1));
            Assert.That(sandbox.TransferCount, Is.EqualTo(1));
            Assert.That(source.Count, Is.EqualTo(1));
            Assert.That(target.Count, Is.EqualTo(3));
            Assert.That(body.position.x, Is.EqualTo(right ? target.CenterBounds.xMin : target.CenterBounds.xMax).Within(.0001f));
            Assert.That(body.position.y, Is.EqualTo(Mathf.Lerp(target.CenterBounds.yMin, target.CenterBounds.yMax, .7f)).Within(.0001f));
            Assert.That(Mathf.Sign(body.linearVelocity.x), Is.EqualTo(right ? 1 : -1));
            Assert.That(body.linearVelocity.x, Is.EqualTo(right ? 3 : -3).Within(.03f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(.4f).Within(.01f));
        }

        [Test]
        public void ResetReturnsTransferredSeedAndStopsMotionWithoutRespawning()
        {
            var seed = seeds[0];
            var identity = seed.View.GetEntityId();
            Vector2 initial = seed.GetComponent<Rigidbody2D>().position;
            var source = sandbox.GetBoard(0);
            var body = seed.GetComponent<Rigidbody2D>();
            body.position = new Vector2(source.CenterBounds.xMax, source.CenterBounds.center.y);
            body.linearVelocity = Vector2.right * 3;
            source.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            sandbox.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            sandbox.ResetOrbs();
            Assert.That(seed.CurrentBoard, Is.Zero);
            Assert.That(seed.View.GetEntityId(), Is.EqualTo(identity));
            Assert.That(Vector2.Distance(body.position, initial), Is.LessThan(.001f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(sandbox.GetBoard(0).Count, Is.EqualTo(2));
            Assert.That(sandbox.GetBoard(1).Count, Is.EqualTo(2));
        }

        [Test]
        public void ClosedPortalsKeepOrbOnItsBoard()
        {
            sandbox.portalsEnabled = false;
            sandbox.ApplyTuning();
            var source = sandbox.GetBoard(0);
            var body = seeds[0].GetComponent<Rigidbody2D>();
            body.position = new Vector2(source.CenterBounds.xMax + .1f, source.CenterBounds.center.y);
            body.linearVelocity = Vector2.right * 3;
            source.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            sandbox.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(seeds[0].CurrentBoard, Is.Zero);
            Assert.That(sandbox.TransferCount, Is.Zero);
            Assert.That(body.position.x, Is.LessThanOrEqualTo(source.CenterBounds.xMax));
            Assert.That(body.linearVelocity.x, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void SavingTuningOnlyUpdatesSevenPhysicsFields()
        {
            JsonUtility.FromJsonOverwrite("{\"monsterMaxHp\":777,\"throwGravity\":7.2,\"staminaMax\":432}", config);
            sandbox.tuning.orbRestitution = .31f;
            sandbox.tuning.orbFloorDeceleration = .8f;
            sandbox.tuning.ApplyToConfig(config);
            Assert.That(config.OrbRestitution, Is.EqualTo(.31f).Within(.0001f));
            Assert.That(config.OrbFloorDeceleration, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(config.MonsterMaxHp, Is.EqualTo(777));
            Assert.That(config.ThrowGravity, Is.EqualTo(7.2f).Within(.0001f));
            Assert.That(config.StaminaMax, Is.EqualTo(432));
        }

        [Test]
        public void RepeatedSetupApplyDoesNotDuplicateBoardsOrViews()
        {
            sandbox.ApplyTuning(); sandbox.ApplyTuning(); sandbox.ResetOrbs(); sandbox.ResetOrbs();
            Assert.That(root.GetComponentsInChildren<LocalOrbPhysicsBoard>().Length, Is.EqualTo(2));
            Assert.That(root.GetComponentsInChildren<OrbView>().Length, Is.EqualTo(4));
        }
    }
}
