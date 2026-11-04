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
    /// #507: saved Home → DEV solo → Stages 1, 2, 3. Every stage mounts its baked gourd through the gourd model contract
    /// (paint, hitbox, rope socket, yokai, finish copy). Solo production binding only.
    /// </summary>
    public sealed class GameStageGourdIntegrationTests
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
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start(); network.port = (ushort)((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
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
            Scene cleanup = SceneManager.CreateScene("Stage 2 gourd cleanup");
            SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            session = null; network = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryStageMountsItsGourdWithItsPaintHitboxRopeYokaiAndFinishCopy()
        {
            session.StartSolo();
            string[] ids = { "Stage01_Gumiho_Battle", "Stage02_Yagwanggwi_Battle", "Stage03_Jigwi_Battle" };
            for (int stage = 1; stage <= 3; stage++)
            {
                if (stage > 1)
                {
                    Assert.That(network.PauseBattle(), Is.True);
                    Assert.That(session.DeveloperNextStage(), Is.True);
                }
                yield return WaitPlaying(stage);
                AssertStageGourd(stage, ids[stage - 1]);
            }
        }

        private void AssertStageGourd(int stage, string id)
        {
            string label = "Stage " + stage + ": ";
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, label + paint.InitializationError);
            Assert.That(paint.RigFallbackReason, Is.Null, label);
            LabGourdVisualRig rig = paint.Rig;
            Assert.That(rig, Is.Not.Null, label + "uses its baked contract, not the white gourd.");
            Assert.That(paint.VisualInstance.name, Does.StartWith(id), label);
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Rear_Rope")), Is.False, label);

            // Hits: the collider is the hit shell, in the paint shell's space.
            LabTarget target = network.target;
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(rig.hitShell.sharedMesh), label);
            Matrix4x4 hitbox = target.Hitbox.transform.localToWorldMatrix, shell = rig.hitShell.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++) Assert.That(hitbox[i], Is.EqualTo(shell[i]).Within(1e-4f), label);
            Camera camera = Object.FindFirstObjectByType<GameBattleBindings>().battleCamera;
            Vector3 aim = target.Hitbox.bounds.center;
            RaycastHit[] hits = Physics.RaycastAll(camera.transform.position, (aim - camera.transform.position).normalized, 100f);
            Assert.That(hits.Any(h => h.collider == target.Hitbox), Is.True, label + "a shot from the camera meets the hit shell.");
            RaycastHit hit = hits.First(h => h.collider == target.Hitbox);
            Vector3 hitDirection = target.Hitbox.transform.InverseTransformPoint(hit.point).normalized;
            Vector3 paintDirection = paint.Shell.InverseTransformPoint(hit.point).normalized;
            Assert.That(Vector3.Distance(hitDirection, paintDirection), Is.LessThan(1e-4f), label + "the Host's paint direction is the shell's.");

            // The continuous rope ends at the model's rope socket.
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            Assert.That(hanging.RopeStyle, Is.EqualTo(LabRopeStyle.Curved), label);
            Assert.That(hanging.RopeSocket, Is.SameAs(rig.ropeSocket), label);
            Transform lastSegment = hanging.ContinuousRope.GetChild(hanging.ContinuousRope.childCount - 1);
            Vector3 ropeEnd = lastSegment.position + lastSegment.up * (lastSegment.lossyScale.y - .004f);
            Assert.That(Vector3.Distance(ropeEnd, rig.ropeSocket.position), Is.LessThan(.01f), label + "the rope meets the gourd at its socket.");

            // Paint binding on the body and the lid.
            paint.RefreshCommittedMap();
            Assert.That(paint.PaintExtras.Count, Is.EqualTo(1), label);
            foreach (Renderer renderer in new Renderer[] { rig.paintShell, paint.PaintExtras[0] })
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_PaintMapReady"), Is.EqualTo(1f), label + renderer.name);
                Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(paint.VisualPaintTexture), label + renderer.name);
                Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_UNDERPAINT_MAP"), Is.True, label + renderer.name);
            }

            // The yokai: closed at rest, out for a game event (same triggers as the black yokai).
            var yokai = paint.VisualInstance.GetComponent<LabGourdYokai>();
            Assert.That(yokai, Is.Not.Null, label);
            Assert.That(yokai.Ready, Is.True, label);
            Assert.That(yokai.Profile, Is.SameAs(rig.yokaiProfile), label);
            AnimationState state = rig.yokaiAnimation[rig.yokaiAnimation.clip.name];
            Assert.That(state.time, Is.EqualTo(rig.yokaiProfile.lidClosedAt).Within(1e-4f), label + "closed until an event.");
            Transform lid = rig.yokaiAnimation.GetComponentsInChildren<Transform>(true).Single(t => t.name.StartsWith("Lid_Hinge"));
            Quaternion closed = lid.localRotation;
            yokai.enabled = false;
            var warning = new LabSnapshot { sessionId = "stage" + stage, round = 1, phase = LabPhase.Playing, serverTime = 17.1d,
                comboWarningAt = 17d, comboNextAt = 20d };
            for (int i = 0; i < 40; i++) yokai.Advance(warning, 17.1d + i * .05d, .05f);
            Assert.That(yokai.Holding, Is.True, label);
            Assert.That(Quaternion.Angle(lid.localRotation, closed), Is.GreaterThan(20f), label + "the lid opens for the combo warning.");
            yokai.BombDropped();
            for (int i = 0; i < 10; i++) yokai.Advance(warning, 19.1d + i * .05d, .05f);
            yokai.enabled = true;

            // The finish presentation copies the painted body and lid, never the removed model rope.
            var finish = target.GetComponent<LabPaintFinishPresentation>();
            Assert.That(finish, Is.Not.Null, label);
            var copy = new GameObject("Stage " + stage + " finish copy check").transform;
            try
            {
                typeof(LabPaintFinishPresentation).GetMethod("CopyMeshes", Private).Invoke(finish, new object[] { target.transform, copy });
                string[] copied = copy.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name).ToArray();
                Assert.That(copied.Any(n => n.StartsWith("Paint Shell")), Is.True, label);
                Assert.That(copied.Any(n => n.StartsWith("Lid Paint")), Is.True, label);
                Assert.That(copied.Any(n => n.StartsWith("Rear_Rope")), Is.False, label);
            }
            finally { Object.Destroy(copy.gameObject); }
        }

        private IEnumerator WaitPlaying(int stage) => Until(() => session.State.phase == GameFlowPhase.Playing
            && session.State.stageIndex == stage && !session.IsLoadingScene && network.HasBattleReferences
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle, "Stage " + stage + " playing");

        private static IEnumerator Until(Func<bool> condition, string reason)
        {
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail(reason);
                yield return null;
            }
        }
    }
}
