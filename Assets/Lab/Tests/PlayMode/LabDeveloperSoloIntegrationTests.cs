using System.Collections;
using System.Collections.Generic;
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

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabHud hud = developerUi.GetComponent<LabHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.network, Is.Not.Null);
            Assert.That(hud.playerHpText, Is.Not.Null, "The saved Canvas must bind its player HP text to LabHud.");
            Assert.That(hud.battleUi, Is.Not.Null, "The Figma battle UI must be saved in the Scene.");
            Assert.That(hud.yokaiHpFill, Is.Not.Null);
            Assert.That(hud.playerHpText.transform.parent.name, Is.EqualTo("FigmaPlayerHpBar"));
            Assert.That(hud.playerHpFill, Is.Not.Null,
                "The saved Figma HP bar must bind to the player's live health value.");
            Assert.That(hud.timeText.transform.parent.name, Is.EqualTo("FigmaTimeFrame"));
            Transform oldGenerateButton = hud.battleUi.transform.Find("FigmaGenerateButton");
            Assert.That(oldGenerateButton == null || !oldGenerateButton.gameObject.activeSelf, Is.True,
                "The drum on the board replaces the bottom generate button, which stays hidden.");
            Assert.That(hud.staminaFill, Is.Not.Null);
            Assert.That(hud.staminaCells, Is.Empty);
            Assert.That(hud.staminaFill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
            Assert.That(hud.staminaFill.fillOrigin, Is.EqualTo((int)UnityEngine.UI.Image.OriginHorizontal.Left));
            Assert.That(hud.playerHpFill.fillOrigin, Is.EqualTo((int)UnityEngine.UI.Image.OriginHorizontal.Right));
            Assert.That(hud.leftNeighborText, Is.Not.Null);
            Assert.That(hud.rightNeighborText, Is.Not.Null);
            Assert.That(hud.pauseButton, Is.Not.Null);
            Assert.That(hud.pauseMenu, Is.Not.Null);
            Assert.That(hud.resultControls, Is.Not.Null);
            Assert.That(hud.resultRestartButton, Is.Not.Null);
            Assert.That(hud.resultReturnLobbyButton, Is.Not.Null);
            Assert.That(hud.resultLeaveButton, Is.Not.Null);
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.valueSliders.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.valueSliders[119], Is.Not.Null,
                "The saved DEV panel extends its Gourd rows with the rope motion slider.");
            Assert.That(developerUi.valueSliders[126], Is.Not.Null,
                "The saved DEV panel extends its Gourd rows with the swing-angle slider.");
            Assert.That(hud.playerHpText.text, Is.EqualTo("—"));

            LabNetwork network = hud.network;
            network.port = 17776;
            try
            {
                developerUi.Open();
                developerUi.valueInputs[48].text = "125";
                developerUi.valueInputs[119].text = "0.7";
                developerUi.valueInputs[126].text = "45";
                Assert.That(developerUi.valueSliders[48].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[48], 300f, 125f))
                        .Within(.0001f));
                Assert.That(developerUi.valueSliders[119].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[119], .45f, .7f))
                        .Within(.0001f));
                developerUi.ApplyAndStart();
                Assert.That(network.IsDeveloperSolo, Is.True, developerUi.feedbackText.text);
                Assert.That(network.config.GourdRopeCycleAmplitude, Is.EqualTo(.7f).Within(.0001f));
                Assert.That(network.config.GourdSwingMaximumAngle, Is.EqualTo(45f));
                Assert.That(network.target.GetComponent<LabHangingTarget>().CycleAmplitude,
                    Is.EqualTo(.7f).Within(.0001f));
                Assert.That(network.target.GetComponent<LabHangingTarget>().MaximumAngle,
                    Is.EqualTo(45f));
                yield return null;
                Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(125));
                Assert.That(hud.playerHpText.text, Is.EqualTo("125 / 125"));
                Assert.That(hud.playerHpFill.fillAmount, Is.EqualTo(1f).Within(.001f));
                Assert.That(hud.localPlayerCircle.sprite, Is.SameAs(hud.playerCircleSprites[0]));
                Assert.That(hud.localPlayerCircle.enabled, Is.True);
                Assert.That(hud.leftNeighborBadge.activeSelf, Is.False);
                Assert.That(hud.rightNeighborBadge.activeSelf, Is.False);
                Assert.That(hud.staminaFill.fillAmount,
                    Is.EqualTo(network.Snapshot.players.Single().stamina / network.config.StaminaMax).Within(.001f));
                string firstSession = network.Snapshot.sessionId;

                developerUi.Open();
                developerUi.valueInputs[48].text = "80";
                developerUi.ApplyAndStart();
                yield return null;
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(firstSession),
                    "DEV Solo restart creates a fresh battle session.");
                Assert.That(network.Snapshot.round, Is.EqualTo(1u));
                Assert.That(network.Snapshot.players.Single().personalHp, Is.EqualTo(80));
                Assert.That(hud.playerHpText.text, Is.EqualTo("80 / 80"));
            }
            finally
            {
                if (network.IsConnected) network.Stop();
            }
            yield return null;
            Assert.That(hud.playerHpText.text, Is.EqualTo("—"));
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
            Assert.That(hud.playerSlotPanel, Is.Not.Null,
                "The saved lobby Canvas needs an editable P-slot panel.");
            Assert.That(hud.playerSlotButtons, Is.Not.Null);
            Assert.That(hud.playerSlotButtons.Length, Is.EqualTo(LabModel.MaximumParticipants));
            Assert.That(hud.playerSlotStatusText, Is.Not.Null);
            Assert.That(hud.playerSlotPanel.transform.parent, Is.EqualTo(hud.transform),
                "Slot controls should remain saved scene objects on LabCanvas.");
            foreach (UnityEngine.UI.Button button in hud.playerSlotButtons)
            {
                Assert.That(button, Is.Not.Null);
                Assert.That(button.GetComponentInChildren<UnityEngine.UI.Text>(true), Is.Not.Null);
            }
            Assert.That(hud.playerSlotPanel.activeSelf, Is.False,
                "The slot chooser should appear only after joining a room.");
            RectTransform title = hud.roomListPanel.transform.Find("RoomListTitle") as RectTransform;
            Assert.That(title, Is.Not.Null);
            Assert.That(title.anchorMin.y, Is.EqualTo(1f),
                "The room heading must stay above the scrolling rows, including on tall screens.");
            Assert.That(hud.roomSearchStatusText.rectTransform.anchorMin.y, Is.EqualTo(1f),
                "The room-search hint must remain visible above the scrolling rows.");
            Assert.That(hud.roomListPanel.activeSelf, Is.True,
                "The quick room browser appears automatically while disconnected.");
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
                Assert.That(network.LocalSlotNumber, Is.Zero,
                    "Normal rooms start with an unselected Host, just like joining participants.");
                Assert.That(network.CanStartBattle, Is.False,
                    "The room still needs its remaining participants and their selections.");
                Assert.That(hud.playerSlotPanel.activeSelf, Is.True);
                Assert.That(network.CanSelectPlayerSlot, Is.True);
                Assert.That(hud.playerSlotStatusText.text, Does.Contain("POSITIONS 0 / 4"));
                for (int i = 0; i < hud.playerSlotButtons.Length; i++)
                {
                    Assert.That(hud.playerSlotButtons[i].gameObject.activeSelf, Is.EqualTo(i < 4),
                        "Only P1 through the configured room size should be offered.");
                    if (i < 4) Assert.That(hud.playerSlotButtons[i].interactable, Is.True);
                }

                // Use the saved Canvas button: the Host role does not reserve P1.
                hud.playerSlotButtons[2].onClick.Invoke();
                yield return null;
                Assert.That(network.LocalSlotNumber, Is.EqualTo(3));
                Assert.That(network.IsHost, Is.True);
                Assert.That(hud.seatText.text, Does.Contain("P3"));
                Assert.That(hud.playerSlotButtons[2].GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("YOU/CLEAR"));
                Assert.That(hud.playerSlotStatusText.text, Does.Contain("POSITIONS 1 / 4"));

                developerUi.Open();
                yield return null;
                Assert.That(hud.playerSlotPanel.activeSelf, Is.False,
                    "The chooser must not show through the developer settings panel.");
                developerUi.Close();
                yield return null;
                Assert.That(hud.playerSlotPanel.activeSelf, Is.True);

                hud.playerSlotButtons[2].onClick.Invoke();
                yield return null;
                Assert.That(network.LocalSlotNumber, Is.Zero,
                    "Clicking the selected position clears only that choice.");
                Assert.That(network.GetSlotOwner(3), Is.EqualTo(ulong.MaxValue));
                Assert.That(hud.playerSlotButtons[2].GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Does.Contain("OPEN"));
                Assert.That(network.RequestPlayerSlot(5), Is.False,
                    "A four-player room cannot select P5 through a direct request either.");
                Assert.That(network.CanStartBattle, Is.False);
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
            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabSceneController sceneController = developerUi.sceneController;
            LabSeatCamera seatCamera = developerUi.seatCamera;
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
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
            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
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

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network == null ? null : network.board;
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
            foreach (LabOrbElement element in new[] { LabOrbElement.Fire, LabOrbElement.Water,
                LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth })
                Assert.That(appearance.ElementTrailFor(element), Is.Not.Null,
                    "The saved orb appearance must bind an Inspector-editable profile for " + element + ".");
            Assert.That(appearance.DrumSprite, Is.Not.Null,
                "The saved orb appearance must provide the generate drum art.");
            Assert.That(network.impactFeedback, Is.Not.Null,
                "The saved LabCanvas must be connected to the Host-approved impact feedback.");
            Assert.That(network.impactFeedback.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count),
                "New throw tuning rows must be usable in the saved Scene's DEV panel.");
            Assert.That(network.board, Is.SameAs(board), "The Host must receive this board's gesture events.");
            Assert.That(network.target != null && network.target.Hitbox != null, Is.True);
            Assert.That(network.battleFloor, Is.Not.Null);
            Camera boardCamera = developerUi.sceneController?.orbCamera;
            Camera battleCamera = developerUi.seatCamera?.battleCamera;
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
            // This checks the generate-combine-throw-hit path, not throw feel. Pin a throw that
            // reaches the target so tuning the saved defaults cannot break the path check.
            tuning[21] = 4f;    // throw origin radius
            tuning[22] = -.5f;  // throw origin height offset
            tuning[28] = 2f;    // minimum upward speed
            tuning[29] = 2.5f;  // lateral gain
            tuning[30] = 2.1f;  // upward gain
            tuning[31] = 4.5f;  // forward gain
            tuning[32] = 18f;   // maximum world speed
            tuning[33] = 9.81f; // gravity
            tuning[36] = 1f;    // power exponent
            tuning[37] = 0f;    // loft offset
            tuning[38] = 0f;    // air damping
            // Orbs scatter from the drum in every direction. Turn off contact combining so orbs
            // that bump while scattering cannot merge before the test drags Yin onto Yang.
            tuning[51] = 0f;
            tuning[79] = 1f; // Crack keeps the damage-number presentation checked below.
            tuning[88] = 0f; // no gourd bombs: this test counts the orbs on the board
            tuning[89] = 0f;
            Assert.That(rawOnly.TryApplyDeveloperValues(tuning, out string tuningError), Is.True, tuningError);
            network.config = rawOnly;
            // The board reads contact combining from its own config, so it needs the same tuning.
            LabSceneController sceneController = developerUi.sceneController;
            LabConfig savedBoardConfig = sceneController.config;
            sceneController.config = rawOnly;
            sceneController.ReconfigureBoard();

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

                LabOrbState drum = network.Snapshot.drums.Single();
                Assert.That(drum.owner, Is.EqualTo(network.LocalPlayerId), "The solo player holds the drum.");
                LabOrbView drumView = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == drum.id);
                Assert.That(drumView.DrumArtVisible, Is.True);
                Assert.That(drumView.HitSphere.radius, Is.EqualTo(network.config.DrumRadius).Within(.0001f));

                // Tap the drum on the board, the same gesture a player uses.
                LabOrbState yin = null;
                LabOrbState yang = null;
                for (int i = 0; i < 20 && (yin == null || yang == null); i++)
                {
                    int before = network.Snapshot.orbs.Length;
                    Assert.That(board.TryGetMotion(drum.id, out Vector2 drum01, out _), Is.True);
                    Vector2 drumWorld = ToWorld(board.CenterBounds, drum01);
                    Assert.That(board.TryBeginDragAtWorld(drumWorld, 10d + i), Is.True);
                    Assert.That(board.EndDragAtWorld(drumWorld, 10.05d + i), Is.True);
                    Assert.That(network.Snapshot.orbs.Length, Is.EqualTo(before + 1));
                    yin = network.Snapshot.orbs.FirstOrDefault(o => o.kind == LabOrbKind.Yin);
                    yang = network.Snapshot.orbs.FirstOrDefault(o => o.kind == LabOrbKind.Yang);
                }
                Assert.That(yin, Is.Not.Null);
                Assert.That(yang, Is.Not.Null);
                LabOrbView yinView = board.GetComponentsInChildren<LabOrbView>().Single(view => view.OrbId == yin.id);
                Assert.That(yinView, Is.Not.Null);
                Assert.That(yinView.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(yinView.GetComponent<SphereCollider>(), Is.Not.Null);
                Assert.That(yinView.Visual.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);

                // Generated orbs pop out of the drum, fly off at once and slide to a stop before
                // the drag. Use the same public board gesture methods the pointer path calls.
                float slideSeconds = network.config.SpawnMaxSpeed / Mathf.Max(.01f, network.config.OrbFloorDeceleration);
                yield return new WaitForSeconds(network.config.SpawnPopDuration + slideSeconds + .3f);
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
                Assert.That(projectile.ElementFlightTrail, Is.Not.Null);
                Assert.That(projectile.ElementFlightTrail.IsEmitting, Is.True);
                Assert.That(projectile.ElementFlightTrail.Element, Is.EqualTo(combined.element));
                Assert.That(projectile.ElementFlightTrail.Profile,
                    Is.SameAs(appearance.ElementTrailFor(combined.element)));
                Assert.That(projectile.FlightTrail, Is.Null,
                    "Configured elemental effects replace, rather than overlap, the legacy trail.");
                Color expectedDamageColor = (Color32)LabOrbAppearance.DefaultElementColor(combined.element);

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
                // Trail art has its own palette; approved damage labels retain the gameplay palette.
                Color damageColor = (Color32)damageLabel.color;
                Assert.That(damageColor.r, Is.EqualTo(expectedDamageColor.r).Within(.001f));
                Assert.That(damageColor.g, Is.EqualTo(expectedDamageColor.g).Within(.001f));
                Assert.That(damageColor.b, Is.EqualTo(expectedDamageColor.b).Within(.001f));
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
                sceneController.config = savedBoardConfig;
                sceneController.ReconfigureBoard();
                UnityEngine.Object.Destroy(rawOnly);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloGourdBombFallsStraightOntoTheLocalBoard()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null; // Let Awake/Start bind the saved Scene references.

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig gourd = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = gourd.CaptureDeveloperValues();
            tuning[88] = 1f; // a gourd bomb every second
            tuning[89] = 1f;
            Assert.That(gourd.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = gourd;
            network.port = 17791;
            // Record only: an assertion thrown inside the event would interrupt the Host's snapshot.
            var drops = new List<string>();
            var targets = new List<ulong>();
            System.Action<ulong, string, Vector2> onDrop = (target, id, at) =>
            {
                targets.Add(target);
                drops.Add(id);
            };
            network.GourdBombDropped += onDrop;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                float until = Time.realtimeSinceStartup + 3f;
                while (drops.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(drops, Is.Not.Empty, "The gourd drops a bomb after the interval.");
                Assert.That(targets, Is.All.EqualTo(network.LocalPlayerId), "Solo, every bomb falls on the one player.");
                string bombId = drops[0];
                LabOrbState bomb = network.Snapshot.orbs.Single(orb => orb.id == bombId);
                Assert.That(bomb.origin, Is.EqualTo(LabOrbOrigin.Gourd));
                Assert.That(bomb.owner, Is.EqualTo(network.LocalPlayerId));
                Assert.That(board.IsDropping(bombId), Is.True,
                    "The drop signal reaches the board before the snapshot that holds the bomb.");
                LabOrbView falling = board.GetComponentsInChildren<LabOrbView>().Single(candidate => candidate.OrbId == bombId);
                Assert.That(falling.Visual.localPosition.y, Is.GreaterThan(0f),
                    "It falls in from above at once, with no landing warning (#212).");
                Assert.That(bomb.lockSwipes, Is.InRange(2, 4), "A gourd bomb comes locked.");
                Assert.That(network.Notice, Does.Contain("gourd"));
                var alert = UnityEngine.Object.FindFirstObjectByType<LabGourdAlert>();
                Assert.That(alert, Is.Not.Null, "The saved scene shows the gourd's alert.");
                Assert.That(alert.Active, Is.True, "Every player sees the gourd's \"!\" bubble.");

                LabOrbAppearance look = network.config.OrbAppearance;
                until = Time.realtimeSinceStartup + look.DropSeconds + 1.5f;
                while (board.IsDropping(bombId) && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(board.IsDropping(bombId), Is.False, "It lands and becomes an ordinary bomb.");
                LabOrbView view = board.GetComponentsInChildren<LabOrbView>().Single(candidate => candidate.OrbId == bombId);
                Assert.That(view.BombArtVisible, Is.True, "It shows the cartoon bomb.");
                Assert.That(view.ItemMarker.BombCountText, Is.Not.Empty, "With its unlock handoffs left in red.");
            }
            finally
            {
                network.GourdBombDropped -= onDrop;
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(gourd);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloSwipeBlockBouncesAndShowsOnTheBlockedPlayersOwnScreen()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null; // Let Awake/Start bind the saved Scene references.

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig blocking = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = blocking.CaptureDeveloperValues();
            tuning[88] = 0f; // no gourd bombs
            tuning[89] = 0f;
            tuning[153] = 1f; // a swipe block one second in
            tuning[154] = 3f;
            tuning[155] = 0f;
            Assert.That(blocking.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = blocking;
            network.port = 17792;
            // Record only: an assertion thrown inside the event would interrupt the Host's snapshot.
            var started = new List<bool>();
            System.Action<bool, float> onBlock = (right, seconds) => started.Add(right);
            network.SwipeBlockStarted += onBlock;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                float until = Time.realtimeSinceStartup + 3f;
                while (started.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(started, Is.Not.Empty, "The gourd blocks a way after the interval (#215).");
                bool right = started[0];
                Assert.That(network.Snapshot.SwipeBlockOn(network.LocalPlayerId), Is.True, "Solo, it falls on the one player.");
                Assert.That(board.BlockedSide, Is.EqualTo(right ? 1 : -1), "This board holds that edge shut.");
                Assert.That(board.EdgeBlock.Visible, Is.True, "It glows red...");
                Assert.That(board.EdgeBlock.SecondsText, Is.Not.Empty, "...with the seconds left.");
                Renderer paper = board.transform.Find("OrbBoardPlate").GetComponent<Renderer>();
                Assert.That(paper.sortingOrder, Is.LessThan(board.EdgeBlock.GlowSortingOrder),
                    "The transparent paper plate is drawn first, so it never covers the red glow.");
                Assert.That(paper.sortingOrder, Is.EqualTo(LabSceneController.PlateSortingOrder));
                var alert = UnityEngine.Object.FindFirstObjectByType<LabGourdAlert>();
                Assert.That(alert, Is.Not.Null);
                Assert.That(alert.ShowingBlock, Is.True, "The gourd's bubble shows the blocked way.");
                Assert.That(alert.BlockRight, Is.EqualTo(right));

                until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.That(started.Count, Is.EqualTo(1), "Each block is announced once, however many snapshots repeat it.");
            }
            finally
            {
                network.SwipeBlockStarted -= onBlock;
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(blocking);
            }
        }

        private static LabDeveloperMode SavedDeveloperUi() =>
            SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true))
                .Single();

        private static Vector2 ToWorld(Rect bounds, Vector2 position01) => new Vector2(
            Mathf.Lerp(bounds.xMin, bounds.xMax, position01.x),
            Mathf.Lerp(bounds.yMin, bounds.yMax, position01.y));
    }
}
