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
    /// #507 PR-2: saved Home → DEV solo → Stages 1, 2, 3. Stage 2 mounts the baked Yagwanggwi gourd through the gourd model
    /// contract (paint, hitbox, yokai, finish copy); Stages 1 and 3 keep the white gourd. Solo production binding only.
    /// </summary>
    public sealed class GameStage2GourdIntegrationTests
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
        public IEnumerator OnlyStageTwoMountsTheYagwanggwiGourdWithItsPaintHitboxYokaiAndFinishCopy()
        {
            session.StartSolo();
            yield return WaitPlaying(1);
            AssertWhiteGourd("Stage 1");

            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(2);
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.RigFallbackReason, Is.Null);
            LabGourdVisualRig rig = paint.Rig;
            Assert.That(rig, Is.Not.Null, "Stage 2 uses the baked contract, not the white gourd.");
            Assert.That(paint.VisualInstance.name, Does.StartWith("Stage02_Yagwanggwi_Battle"));
            Assert.That(paint.VisualInstance.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Rear_Rope")), Is.False);

            // Hits: the collider is the hit shell, in the paint shell's space.
            LabTarget target = network.target;
            Assert.That(target.Hitbox.sharedMesh, Is.SameAs(rig.hitShell.sharedMesh));
            Matrix4x4 hitbox = target.Hitbox.transform.localToWorldMatrix, shell = rig.hitShell.transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++) Assert.That(hitbox[i], Is.EqualTo(shell[i]).Within(1e-4f));
            Camera camera = Object.FindFirstObjectByType<GameBattleBindings>().battleCamera;
            Vector3 aim = target.Hitbox.bounds.center;
            RaycastHit[] hits = Physics.RaycastAll(camera.transform.position, (aim - camera.transform.position).normalized, 100f);
            Assert.That(hits.Any(h => h.collider == target.Hitbox), Is.True, "A shot from the camera meets the Stage 2 hit shell.");
            RaycastHit hit = hits.First(h => h.collider == target.Hitbox);
            Vector3 hitDirection = target.Hitbox.transform.InverseTransformPoint(hit.point).normalized;
            Vector3 paintDirection = paint.Shell.InverseTransformPoint(hit.point).normalized;
            Assert.That(Vector3.Distance(hitDirection, paintDirection), Is.LessThan(1e-4f), "The Host's paint direction is the shell's.");

            // The continuous rope (#511) ends at the model's rope socket.
            LabHangingTarget hanging = target.GetComponent<LabHangingTarget>();
            Assert.That(hanging.RopeStyle, Is.EqualTo(LabRopeStyle.Curved));
            Assert.That(hanging.RopeSocket, Is.SameAs(rig.ropeSocket));
            Transform lastSegment = hanging.ContinuousRope.GetChild(hanging.ContinuousRope.childCount - 1);
            Vector3 ropeEnd = lastSegment.position + lastSegment.up * (lastSegment.lossyScale.y - .004f);
            Assert.That(Vector3.Distance(ropeEnd, rig.ropeSocket.position), Is.LessThan(.01f), "The rope meets the gourd at its socket.");

            // Paint binding on the body and the lid.
            paint.RefreshCommittedMap();
            Assert.That(paint.PaintExtras.Count, Is.EqualTo(1));
            foreach (Renderer renderer in new Renderer[] { rig.paintShell, paint.PaintExtras[0] })
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_PaintMapReady"), Is.EqualTo(1f), renderer.name);
                Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(paint.VisualPaintTexture), renderer.name);
                Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_UNDERPAINT_MAP"), Is.True, renderer.name);
            }

            // The yokai: closed at rest, out for a game event (same triggers as the black yokai).
            var yokai = paint.VisualInstance.GetComponent<LabGourdYokai>();
            Assert.That(yokai, Is.Not.Null);
            Assert.That(yokai.Ready, Is.True);
            Assert.That(yokai.Profile, Is.SameAs(rig.yokaiProfile));
            AnimationState state = rig.yokaiAnimation[rig.yokaiAnimation.clip.name];
            Assert.That(state.time, Is.EqualTo(rig.yokaiProfile.lidClosedAt).Within(1e-4f), "Closed until an event.");
            Transform lid = rig.yokaiAnimation.transform.Find("Vessel_Forward_Tilt.002/Lid_Hinge.001");
            Quaternion closed = lid.localRotation;
            yokai.enabled = false;
            var warning = new LabSnapshot { sessionId = "stage2", round = 1, phase = LabPhase.Playing, serverTime = 17.1d,
                comboWarningAt = 17d, comboNextAt = 20d };
            for (int i = 0; i < 40; i++) yokai.Advance(warning, 17.1d + i * .05d, .05f);
            Assert.That(yokai.Holding, Is.True);
            Assert.That(Quaternion.Angle(lid.localRotation, closed), Is.GreaterThan(20f), "The lid opens for the combo warning.");
            yokai.BombDropped();
            for (int i = 0; i < 10; i++) yokai.Advance(warning, 19.1d + i * .05d, .05f);
            yokai.enabled = true;

            // The finish presentation copies the painted body and lid, never the removed model rope.
            var finish = target.GetComponent<LabPaintFinishPresentation>();
            Assert.That(finish, Is.Not.Null);
            var copy = new GameObject("Stage 2 finish copy check").transform;
            try
            {
                typeof(LabPaintFinishPresentation).GetMethod("CopyMeshes", Private).Invoke(finish, new object[] { target.transform, copy });
                string[] copied = copy.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.name).ToArray();
                Assert.That(copied.Any(n => n.StartsWith("Paint Shell")), Is.True);
                Assert.That(copied.Any(n => n.StartsWith("Lid Paint")), Is.True);
                Assert.That(copied.Any(n => n.StartsWith("Rear_Rope")), Is.False);
            }
            finally { Object.Destroy(copy.gameObject); }

            Assert.That(network.PauseBattle(), Is.True);
            Assert.That(session.DeveloperNextStage(), Is.True);
            yield return WaitPlaying(3);
            AssertWhiteGourd("Stage 3");
        }

        private void AssertWhiteGourd(string stage)
        {
            LabGourdPaint paint = network.target.GetComponent<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, stage + ": " + paint.InitializationError);
            Assert.That(paint.Rig, Is.Null, stage + " keeps the white gourd.");
            Assert.That(paint.visualPrefab, Is.SameAs(Resources.Load<GameObject>("GourdPaint/WhiteGourd")), stage);
            Assert.That(paint.Shell.name, Is.EqualTo("White Gourd"), stage);
            Assert.That(network.target.Hitbox.sharedMesh, Is.SameAs(paint.Shell.GetComponent<MeshFilter>().sharedMesh), stage);
            Assert.That(network.target.GetComponent<LabHangingTarget>().RopeSocket, Is.Null, stage + ": the rope ends at the hitbox top.");
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
