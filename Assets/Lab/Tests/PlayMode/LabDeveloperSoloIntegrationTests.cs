using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Exercises the saved Scene's actual Host, board gesture events and 3D projectile.
    /// No synthetic network packet or direct LabModel mutation is used after starting Solo.
    /// </summary>
    public sealed class LabDeveloperSoloIntegrationTests
    {
        [UnityTest]
        public IEnumerator SavedDeveloperPanelCreatesAndRetunesTwoPlayerHostLobby()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabNetwork network = UnityEngine.Object.FindFirstObjectByType<LabNetwork>();
            LabDeveloperMode developerUi = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            LabSceneController sceneController = UnityEngine.Object.FindFirstObjectByType<LabSceneController>();
            LabSeatCamera seatCamera = UnityEngine.Object.FindFirstObjectByType<LabSeatCamera>();
            Assert.That(network, Is.Not.Null);
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(developerUi.applyHostButton, Is.Not.Null);
            Assert.That(developerUi.targetParticipantsSlider, Is.Not.Null);
            network.port = 17779;
            try
            {
                developerUi.Open();
                developerUi.targetParticipantsSlider.value = 2f;
                developerUi.valueInputs[0].text = "350";
                developerUi.ApplyAndHost();
                Assert.That(network.IsHost, Is.True, developerUi.feedbackText.text);
                Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
                Assert.That(network.config.MonsterMaxHp, Is.EqualTo(350));
                Assert.That(sceneController.config, Is.SameAs(network.config));
                Assert.That(seatCamera.config, Is.SameAs(network.config));

                yield return null;
                developerUi.Open();
                Assert.That(developerUi.targetParticipantsSlider.interactable, Is.False);
                developerUi.valueInputs[0].text = "400";
                developerUi.ApplyAndHost();
                Assert.That(network.IsHost, Is.True);
                Assert.That(network.config.MonsterMaxHp, Is.EqualTo(400));
                Assert.That(sceneController.config, Is.SameAs(network.config));
                Assert.That(seatCamera.config, Is.SameAs(network.config));
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
            Assert.That(network.config, Is.SameAs(developerUi.sourceConfig));
            Assert.That(sceneController.config, Is.SameAs(developerUi.sourceConfig));
            Assert.That(seatCamera.config, Is.SameAs(developerUi.sourceConfig));
        }

        [UnityTest]
        public IEnumerator SavedDeveloperPanelCanApplyAndRestartStraightThrowTuning()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabNetwork network = UnityEngine.Object.FindObjectsByType<LabNetwork>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.board != null && candidate.target != null);
            LabDeveloperMode developerUi = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            Assert.That(network, Is.Not.Null);
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.valueSliders.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.applyHostButton, Is.Not.Null);
            Assert.That(developerUi.targetParticipantsSlider.value, Is.EqualTo(3f));
            network.port = 17778;
            try
            {
                developerUi.Open();
                developerUi.valueSliders[36].value = .75f;
                Assert.That(developerUi.valueInputs[36].text, Is.EqualTo("2.3125"));
                developerUi.valueInputs[36].text = "1.6";
                Assert.That(developerUi.valueSliders[36].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[36], 1f, 1.6f))
                        .Within(.0001f));
                developerUi.valueInputs[37].text = "12";
                developerUi.valueInputs[38].text = "0.6";
                developerUi.ApplyAndStart();
                Assert.That(network.IsDeveloperSolo, Is.True, developerUi.feedbackText.text);
                Assert.That(network.config.ThrowPowerExponent, Is.EqualTo(1.6f));
                Assert.That(network.config.ThrowLoftOffsetDegrees, Is.EqualTo(12f));
                Assert.That(network.config.ThrowAirDamping, Is.EqualTo(.6f));

                developerUi.Open();
                developerUi.valueInputs[37].text = "-8";
                developerUi.ApplyAndStart();
                Assert.That(network.IsDeveloperSolo, Is.True, developerUi.feedbackText.text);
                Assert.That(network.config.ThrowLoftOffsetDegrees, Is.EqualTo(-8f));
                developerUi.EndSolo();
                Assert.That(network.config, Is.SameAs(developerUi.sourceConfig));
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloCanGenerateCombineAndHitTheCylinder()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null; // Let Awake/Start bind the saved Scene references.

            LabNetwork network = UnityEngine.Object.FindObjectsByType<LabNetwork>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.board != null && candidate.target != null);
            LabOrbBoard board = UnityEngine.Object.FindFirstObjectByType<LabOrbBoard>();
            LabDeveloperMode developerUi = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            Assert.That(network, Is.Not.Null, "The saved Lab Scene needs a LabNetwork.");
            Assert.That(board, Is.Not.Null, "The saved Lab Scene needs an orb board.");
            Assert.That(developerUi, Is.Not.Null, "The saved Lab Scene needs Developer Mode controls.");
            LabOrbAppearance appearance = network.config.OrbAppearance;
            Assert.That(appearance, Is.Not.Null,
                "The saved config must point to the shared Inspector-editable orb appearance.");
            Assert.That(appearance.VisualPrefab, Is.Not.Null);
            Assert.That(appearance.YinMaterial, Is.Not.Null);
            Assert.That(appearance.YangMaterial, Is.Not.Null);
            Assert.That(appearance.CombinedMaterial, Is.Not.Null);
            Assert.That(appearance.ShadowMaterial, Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count),
                "New throw tuning rows must be usable in the saved Scene's DEV panel.");
            Assert.That(network.board, Is.SameAs(board), "The Host must receive this board's gesture events.");
            Assert.That(network.target != null && network.target.Hitbox != null, Is.True);
            Assert.That(network.battleFloor, Is.Not.Null);
            Camera boardCamera = UnityEngine.Object.FindFirstObjectByType<LabSceneController>()?.orbCamera;
            Camera battleCamera = UnityEngine.Object.FindFirstObjectByType<LabSeatCamera>()?.battleCamera;
            Assert.That(boardCamera, Is.Not.Null);
            Assert.That(battleCamera, Is.Not.Null);
            Assert.That(board.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("LabOrbBoard")));
            Assert.That((boardCamera.cullingMask & (1 << board.gameObject.layer)) != 0, Is.True);
            Assert.That((battleCamera.cullingMask & (1 << board.gameObject.layer)) == 0, Is.True,
                "The new 3D board spheres must not appear in the battle camera.");
            Assert.That(boardCamera.depth, Is.GreaterThan(battleCamera.depth),
                "The board camera must render after the battle camera on mobile devices.");

            // Each solo round has a random session seed and draws are independent, so use
            // Yin/Yang-only chances and a cheap cost: up to 20 draws find both polarities.
            LabConfig savedConfig = network.config;
            LabConfig rawOnly = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = rawOnly.CaptureDeveloperValues();
            tuning[5] = 1f;
            tuning[39] = 50f; tuning[40] = 50f;
            tuning[41] = 0f; tuning[42] = 0f; tuning[43] = 0f; tuning[44] = 0f;
            Assert.That(rawOnly.TryApplyDeveloperValues(tuning, out string tuningError), Is.True, tuningError);
            network.config = rawOnly;

            // Keep the test independent of an ordinary app using the default port 7777.
            network.port = 17777;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True);
                Assert.That(network.IsHost, Is.True);
                Assert.That(network.ParticipantCount, Is.EqualTo(1));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(network.Snapshot.players.Length, Is.EqualTo(1));
                int initialHp = network.Snapshot.hp;

                LabOrbState yin = null;
                LabOrbState yang = null;
                for (int i = 0; i < 20 && (yin == null || yang == null); i++)
                {
                    int before = network.Snapshot.orbs.Length;
                    network.Generate();
                    Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(before + 1));
                    yin = network.Snapshot.orbs.FirstOrDefault(o => o.kind == LabOrbKind.Yin);
                    yang = network.Snapshot.orbs.FirstOrDefault(o => o.kind == LabOrbKind.Yang);
                }
                Assert.That(yin, Is.Not.Null);
                Assert.That(yang, Is.Not.Null);
                LabOrbView yinView = board.GetComponentInChildren<LabOrbView>();
                Assert.That(yinView, Is.Not.Null);
                Assert.That(yinView.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(yinView.GetComponent<SphereCollider>(), Is.Not.Null);
                Assert.That(yinView.Visual.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);

                // Generated orbs rise before accepting a drag. Then use the same public
                // board gesture methods that the pointer path calls, not TryCombine.
                yield return new WaitForSeconds(network.config.SpawnRiseDuration + .1f);
                Assert.That(board.TryGetMotion(yin.id, out Vector2 yin01, out _), Is.True);
                Assert.That(board.TryGetMotion(yang.id, out Vector2 yang01, out _), Is.True);
                Rect bounds = board.CenterBounds;
                Vector2 yinWorld = ToWorld(bounds, yin01);
                Vector2 yangWorld = ToWorld(bounds, yang01);
                Assert.That(board.TryBeginDragAtWorld(yinWorld, 1d), Is.True);
                Assert.That(board.DragToWorld(yangWorld, 1.4d), Is.True);
                Assert.That(board.EndDragAtWorld(yangWorld, 1.41d), Is.True);

                LabOrbState combined = network.Snapshot.orbs.SingleOrDefault(o => o.kind == LabOrbKind.Combined);
                Assert.That(combined, Is.Not.Null, "Direct Yin/Yang overlap should reach the Host through CombineRequested.");
                Assert.That(network.Snapshot.orbs.Any(o => o.id == yin.id || o.id == yang.id), Is.False);
                Assert.That(board.TryGetMotion(combined.id, out Vector2 combined01, out _), Is.True);
                LabOrbView combinedView = board.GetComponentsInChildren<LabOrbView>()
                    .Single(view => view.OrbId == combined.id);
                Assert.That(combinedView.HitSphere, Is.Not.Null);
                Assert.That(combinedView.Visual.GetComponentInChildren<MeshRenderer>(), Is.Not.Null,
                    "The attackable Combined orb must use the same 3D sphere presentation as Raw orbs.");

                // A centred upward release produces the same seat-zero arc as manual Solo play.
                // Keep the orb held while moving it into position, pause, then flick up.
                // Releasing and immediately re-grabbing would race the next physics step.
                Vector2 combinedWorld = ToWorld(bounds, combined01);
                Vector2 launchFrom = ToWorld(bounds, new Vector2(.5f, .25f));
                Assert.That(board.TryBeginDragAtWorld(combinedWorld, 2d), Is.True);
                Assert.That(board.DragToWorld(launchFrom, 3d), Is.True);
                Vector2 release = launchFrom + Vector2.up * (bounds.width * .2f);
                Assert.That(board.EndDragAtWorld(release, 3.04d), Is.True);
                Assert.That(network.Snapshot.orbs.Any(o => o.id == combined.id && o.inFlight), Is.True,
                    "ThrowRequested must cause a Host-approved launch.");
                LabProjectile projectile = UnityEngine.Object.FindFirstObjectByType<LabProjectile>();
                Assert.That(projectile, Is.Not.Null);
                Assert.That(projectile.GetComponent<SphereCollider>(), Is.Not.Null);
                Assert.That(projectile.VisualMount.GetComponentInChildren<MeshRenderer>(), Is.Not.Null,
                    "The thrown orb must retain the 3D sphere presentation.");

                for (int step = 0; step < 150 && network.Snapshot.hp == initialHp; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(network.Snapshot.hp, Is.EqualTo(initialHp - network.config.Damage),
                    "Only an actual cylinder collision should lower Host HP.");
                Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
                Assert.That(network.Notice, Is.EqualTo(network.LastShotReport));
                Assert.That(network.Snapshot.orbs.Any(o => o.id == combined.id), Is.False);
            }
            finally
            {
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(rawOnly);
            }
        }

        private static Vector2 ToWorld(Rect bounds, Vector2 position01) => new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, position01.x),
            Mathf.Lerp(bounds.yMin, bounds.yMax, position01.y));
    }
}
