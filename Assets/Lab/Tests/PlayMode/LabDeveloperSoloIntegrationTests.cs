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
        public IEnumerator SavedHudAndDeveloperSliderShowLocalPlayerHpAcrossSoloRestart()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;

            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            LabDeveloperMode developerUi = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.network, Is.Not.Null);
            Assert.That(hud.playerHpText, Is.Not.Null, "The saved Canvas must bind its player HP text to LabHud.");
            Assert.That(hud.playerHpText.transform.parent.name, Is.EqualTo("BattleHeader"));
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(49));
            Assert.That(developerUi.valueSliders.Length, Is.EqualTo(49));
            Assert.That(hud.playerHpText.text, Is.EqualTo("PLAYER HP —"));

            LabNetwork network = hud.network;
            network.port = 17776;
            try
            {
                developerUi.Open();
                developerUi.valueInputs[48].text = "125";
                Assert.That(developerUi.valueSliders[48].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[48], 300f, 125f))
                        .Within(.0001f));
                developerUi.ApplyAndStart();
                Assert.That(network.IsDeveloperSolo, Is.True, developerUi.feedbackText.text);
                yield return null;
                Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(125));
                Assert.That(hud.playerHpText.text, Is.EqualTo("PLAYER HP 125 / 125"));

                developerUi.Open();
                developerUi.valueInputs[48].text = "80";
                developerUi.ApplyAndStart();
                yield return null;
                Assert.That(network.Snapshot.round, Is.EqualTo(2u));
                Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(80));
                Assert.That(hud.playerHpText.text, Is.EqualTo("PLAYER HP 80 / 80"));
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
            yield return null;
            Assert.That(hud.playerHpText.text, Is.EqualTo("PLAYER HP —"));
        }

        [UnityTest]
        public IEnumerator SavedLobbyCanChooseTwoToFivePlayersBeforeHosting()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            LabHud hud = roots.SelectMany(root => root.GetComponentsInChildren<LabHud>(true))
                .FirstOrDefault();
            LabDeveloperMode developerUi = roots.SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true))
                .FirstOrDefault();
            LabNetwork network = hud == null ? null : hud.network;
            Assert.That(network, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(hud.network, Is.SameAs(network));
            Assert.That(developerUi.network, Is.SameAs(network),
                "The ordinary lobby and DEV panel must edit the same room-size value.");
            Assert.That(hud.hostButton, Is.Not.Null);
            Assert.That(hud.decreasePlayersButton, Is.Not.Null);
            Assert.That(hud.increasePlayersButton, Is.Not.Null);
            Assert.That(hud.peersText, Is.Not.Null);
            Assert.That(hud.roomNameText, Is.Not.Null);
            Assert.That(hud.roomListPanel, Is.Not.Null);
            Assert.That(hud.refreshRoomsButton, Is.Not.Null);
            Assert.That(hud.cancelRoomSearchButton, Is.Not.Null);
            Assert.That(hud.roomListContent, Is.Not.Null);
            Assert.That(hud.roomListRowTemplate, Is.Not.Null);
            Assert.That(hud.roomSearchStatusText, Is.Not.Null);
            Assert.That(hud.roomListContent.IsChildOf(hud.roomListPanel.transform), Is.True,
                "Search results must appear inside the saved room-list panel.");
            Assert.That(hud.roomListRowTemplate.transform.IsChildOf(hud.roomListContent), Is.True);
            Assert.That(hud.roomListRowTemplate.GetComponentInChildren<UnityEngine.UI.Text>(true), Is.Not.Null,
                "A discovered room needs a readable button label.");
            Assert.That(hud.roomListContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childControlHeight,
                Is.True, "Room rows must use their preferred height instead of collapsing to zero.");
            Assert.That(hud.roomListRowTemplate.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight,
                Is.GreaterThan(0f));
            RectTransform title = hud.roomListPanel.transform.Find("RoomListTitle") as RectTransform;
            Assert.That(title, Is.Not.Null);
            Assert.That(title.anchorMin.y, Is.EqualTo(1f),
                "The room heading must stay above the scrolling rows, including on tall screens.");
            Assert.That(hud.roomSearchStatusText.rectTransform.anchorMin.y, Is.EqualTo(1f),
                "The room-search hint must remain visible above the scrolling rows.");
            Assert.That(hud.roomListPanel.activeSelf, Is.False,
                "The room browser must not cover the lobby before searching.");
            Assert.That(hud.roomListRowTemplate.gameObject.activeSelf, Is.False,
                "The template is not an actual discovered room.");

            Assert.That(network.TargetParticipantCount, Is.EqualTo(3));
            hud.decreasePlayersButton.onClick.Invoke();
            Assert.That(network.TargetParticipantCount, Is.EqualTo(2));
            yield return null;
            Assert.That(hud.decreasePlayersButton.interactable, Is.False);
            Assert.That(hud.peersText.text, Does.Contain("ROOM PLAYERS 2 / 5"));

            developerUi.Open();
            Assert.That(developerUi.targetParticipantsSlider.value, Is.EqualTo(2f));
            developerUi.targetParticipantsSlider.value = 5f;
            Assert.That(network.TargetParticipantCount, Is.EqualTo(5));
            developerUi.Close();
            yield return null;
            Assert.That(hud.increasePlayersButton.interactable, Is.False);
            hud.decreasePlayersButton.onClick.Invoke();
            Assert.That(network.TargetParticipantCount, Is.EqualTo(4));

            // Use the saved ordinary HOST button, not the DEV room-creation shortcut.
            hud.port.text = "17780";
            try
            {
                hud.hostButton.onClick.Invoke();
                Assert.That(network.IsHost, Is.True, network.Notice);
                Assert.That(network.TargetParticipantCount, Is.EqualTo(4));
                yield return null;
                Assert.That(hud.decreasePlayersButton.gameObject.activeSelf, Is.False);
                Assert.That(hud.increasePlayersButton.gameObject.activeSelf, Is.False);
                Assert.That(hud.roomNameText.gameObject.activeSelf, Is.True);
                Assert.That(hud.roomNameText.text, Is.Not.Empty,
                    "The Host must see the registered room name or an explicit registration status.");
                Assert.That(developerUi.targetParticipantsSlider.interactable, Is.False);
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
        }

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
            Assert.That(appearance.ThrowTrailMaterial, Is.Not.Null,
                "The saved orb appearance must provide the flight trail material.");
            Assert.That(network.impactFeedback, Is.Not.Null,
                "The saved LabCanvas must be connected to the Host-approved impact feedback.");
            Assert.That(network.impactFeedback.GetComponent<Canvas>(), Is.Not.Null);
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
                Assert.That(combined.element, Is.Not.EqualTo(LabOrbElement.None), "Every Combined orb has a five-element attribute.");
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
                Assert.That(projectile.FlightTrail, Is.Not.Null);
                Assert.That(projectile.FlightTrail.emitting, Is.True);
                Assert.That(projectile.FlightTrail.sharedMaterial, Is.SameAs(appearance.ThrowTrailMaterial));
                Color expectedDamageColor = projectile.FlightTrail.startColor;

                for (int step = 0; step < 150 && network.Snapshot.hp == initialHp; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(network.Snapshot.hp, Is.EqualTo(initialHp - network.config.Damage),
                    "Only an actual cylinder collision should lower Host HP.");
                Transform impactLayer = network.impactFeedback.transform.Find("Impact Feedback");
                Assert.That(impactLayer, Is.Not.Null,
                    "A Host-confirmed cylinder hit must place feedback on the saved Canvas.");
                var damageLabel = impactLayer.GetComponentInChildren<UnityEngine.UI.Text>();
                Assert.That(damageLabel, Is.Not.Null);
                Assert.That(damageLabel.text, Is.EqualTo("-" + network.config.Damage));
                Assert.That(damageLabel.color.r, Is.EqualTo(expectedDamageColor.r).Within(.001f));
                Assert.That(damageLabel.color.g, Is.EqualTo(expectedDamageColor.g).Within(.001f));
                Assert.That(damageLabel.color.b, Is.EqualTo(expectedDamageColor.b).Within(.001f));
                var hanging = network.target.GetComponent<LabHangingTarget>();
                Assert.That(hanging, Is.Not.Null, "The saved scene must expose Pinata tuning.");
                for (int step = 0; step < 10; step++) yield return new WaitForFixedUpdate();
                Assert.That(hanging.SwingAngles.magnitude, Is.GreaterThan(.1f),
                    "A Host-confirmed hit should make the actual target swing.");
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
