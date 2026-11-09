using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// #498: private copies are really drawn under the preparing cover and are gone before the countdown, with the real
    /// board, projectiles, impact pool and paint untouched. Whether Metal then skips the first-use stall is a device check.
    /// </summary>
    [Category("Slow"), Category("GPU")]
    public sealed class GameBattleRenderWarmupTests
    {
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
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (session != null) { network.Stop(); Object.Destroy(session.gameObject); yield return null; }
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Render warmup cleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CopiesDrawUnderTheCoverAndAreGoneBeforeTheCountdown()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Needs a real graphics device.");
            session.StartSolo();
            LabBattleRenderWarmup warmup = null;
            yield return Until(() => (warmup = Object.FindAnyObjectByType<LabBattleRenderWarmup>()) != null, "The warmup exists");
            // A batch run has no screen, so a screen camera never renders; give the real camera a target for this check.
            Camera battleCamera = network.board.GetComponent<GameBattleBindings>() != null
                ? network.board.GetComponent<GameBattleBindings>().battleCamera : Object.FindAnyObjectByType<GameBattleBindings>().battleCamera;
            var batchTarget = new RenderTexture(390, 844, 24);
            if (Application.isBatchMode) battleCamera.targetTexture = batchTarget;
            yield return Until(() => warmup.Root != null, "The warmup starts");
            Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.SynchronizingBattle));
            int orbs = network.board.Count, boardLayer = LayerMask.NameToLayer("LabOrbBoard");
            var revision = network.PaintSurface.Revision; var paintCount = network.PaintSurface.PaintCount;
            var impacts = Object.FindAnyObjectByType<LabImpactParticleVfx>();
            int pooled = impacts.GetComponentsInChildren<LabImpactParticleBurst>(true).Length;
            var drawn = new HashSet<string>();
            var motifs = new HashSet<int>();
            while (warmup.Root != null)
            {
                foreach (Renderer renderer in warmup.Root.GetComponentsInChildren<Renderer>())
                    if (renderer.isVisible && renderer.enabled)
                    {
                        drawn.Add((renderer.gameObject.layer == boardLayer ? "board " : "world ") + renderer.GetType().Name);
                        if (renderer.name == "Brush Motifs" && renderer.GetComponent<ParticleSystem>().particleCount > 0) motifs.Add(renderer.GetComponentInParent<LabProjectile>().transform.GetSiblingIndex());
                    }
                Assert.That(session.State.phase, Is.EqualTo(GameFlowPhase.SynchronizingBattle), "Never into the countdown.");
                Assert.That(warmup.Root.GetComponentsInChildren<Rigidbody>().All(body => !body.detectCollisions), Is.True);
                Assert.That(warmup.Root.GetComponentsInChildren<Collider>().Any(collider => collider.enabled), Is.False);
                yield return null;
            }
            battleCamera.targetTexture = null;
            batchTarget.Release(); Object.Destroy(batchTarget);
            Assert.That(warmup.Finished, Is.True);
            Assert.That(warmup.DrawnFrames, Is.GreaterThanOrEqualTo(LabBattleRenderWarmup.MinimumFrames));
            Assert.That(drawn, Is.SupersetOf(new[] { "world MeshRenderer", "world ParticleSystemRenderer", "world TrailRenderer",
                "board SpriteRenderer", "board MeshRenderer" }));

            Assert.That(motifs.Count, Is.EqualTo(5), "All five elements' trail particles really emitted.");

            yield return Until(() => session.State.phase != GameFlowPhase.SynchronizingBattle, "Countdown");
            yield return null;
            Assert.That(Object.FindObjectsByType<LabProjectile>(FindObjectsInactive.Include).Length, Is.Zero);
            Assert.That(Object.FindObjectsByType<LabElementFlightTrail>(FindObjectsInactive.Include).Length, Is.Zero);
            Assert.That(network.projectileRoot.childCount, Is.Zero);
            Assert.That(impacts.ActiveBurstCount, Is.Zero);
            Assert.That(impacts.GetComponentsInChildren<LabImpactParticleBurst>(true).Length, Is.EqualTo(pooled));
            Assert.That(network.board.Count, Is.EqualTo(orbs));
            Assert.That(network.PaintSurface.Revision, Is.EqualTo(revision));
            Assert.That(network.PaintSurface.PaintCount, Is.EqualTo(paintCount));
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Any(item => item.name.StartsWith("Render warmup", StringComparison.Ordinal)), Is.False);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing, "Playing");
        }

        [UnityTest]
        public IEnumerator AShortBudgetEndsItWithoutDrawingAndNeverHoldsTheStart()
        {
            var host = new GameObject("Warmup without drawing").AddComponent<LabBattleRenderWarmup>();
            host.Request(false);
            Assert.That(host.Finished, Is.True);
            Assert.That(host.Root, Is.Null);
            yield return null;
            Assert.That(host.Root, Is.Null);
            Object.Destroy(host.gameObject);
        }

        private static IEnumerator Until(Func<bool> condition, string reason, double seconds = 60d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(reason);
                yield return null;
            }
        }
    }
}
