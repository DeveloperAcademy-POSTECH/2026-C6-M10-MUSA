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
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();

        [UnityTearDown]
        public IEnumerator UnloadSavedScene() => fixture.Cleanup();

        [UnityTest]
        public IEnumerator SavedHudAndDeveloperSliderShowLocalPlayerHpAcrossSoloRestart()
        {
            yield return fixture.Load();

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
            Assert.That(developerUi.valueSliders[118], Is.Not.Null,
                "The saved DEV panel extends its Gourd rows with the rope motion slider.");
            Assert.That(developerUi.valueSliders[125], Is.Not.Null,
                "The saved DEV panel extends its Gourd rows with the swing-angle slider.");
            Assert.That(hud.playerHpText.text, Is.EqualTo("—"));

            LabNetwork network = hud.network;
            fixture.UseAvailablePort(network);
            try
            {
                developerUi.Open();
                developerUi.valueInputs[48].text = "125";
                developerUi.valueInputs[118].text = "0.7";
                developerUi.valueInputs[125].text = "45";
                Assert.That(developerUi.valueSliders[48].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[48], 300f, 125f))
                        .Within(.0001f));
                Assert.That(developerUi.valueSliders[118].value,
                    Is.EqualTo(LabDeveloperSliderScale.Normalize(LabConfig.DeveloperFields[118], .45f, .7f))
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
            yield return fixture.Load();

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
            hud.port.text = fixture.UseAvailablePort(network).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
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
        public IEnumerator SavedDeveloperPanelPicksDrumCountFromAutoAndOneToFiveThreeToARow()
        {
            yield return fixture.Load();
            LabDeveloperMode developerUi = SavedDeveloperUi();
            developerUi.Open();
            developerUi.panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "Category Drum").onClick.Invoke();
            Transform row = developerUi.valueInputs[74].transform.parent;
            Assert.That(row.gameObject.activeInHierarchy, Is.True);
            Assert.That(developerUi.valueInputs[74].gameObject.activeSelf, Is.False, "Drum Count is picked with buttons (#236).");
            UnityEngine.UI.Button[] choices = Enumerable.Range(0, 6).Select(option =>
                row.Find("SettingSlider/SettingChoices/Choice" + option).GetComponent<UnityEngine.UI.Button>()).ToArray();
            Assert.That(choices.Select(button => button.GetComponentInChildren<UnityEngine.UI.Text>().text),
                Is.EqualTo(new[] { "AUTO", "1", "2", "3", "4", "5" }));
            Assert.That(developerUi.valueInputs[74].text, Is.EqualTo("0"), "The saved config starts on AUTO.");
            for (int option = 0; option < choices.Length; option++)
            {
                var rect = (RectTransform)choices[option].transform;
                Assert.That(rect.anchorMin.x, Is.EqualTo(option % 3 / 3f).Within(1e-4f), "Three to a row.");
                Assert.That(rect.anchorMax.x, Is.EqualTo((option % 3 + 1) / 3f).Within(1e-4f));
                Assert.That(rect.anchorMin.y, Is.EqualTo(option < 3 ? .5f : 0f).Within(1e-4f), "Two rows.");
            }
            int savedCount = developerUi.network.config.DrumCount;
            for (int option = choices.Length - 1; option >= 0; option--)
            {
                choices[option].onClick.Invoke();
                Assert.That(developerUi.valueInputs[74].text, Is.EqualTo(option.ToString()));
                Assert.That(developerUi.network.config.DrumCount, Is.EqualTo(savedCount), "A button selection is not APPLY.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedRawCorrectionToggleAndSlidersApplyToSoloRuntimeCopies()
        {
            yield return fixture.Load();
            LabDeveloperMode ui = SavedDeveloperUi();
            fixture.UseAvailablePort(ui.network);
            string sourceJson = JsonUtility.ToJson(ui.sourceConfig);
            ui.Open();
            Assert.That(ui.valueInputs, Has.Length.EqualTo(228));
            Assert.That(ui.valueSliders, Has.Length.EqualTo(228));
            ui.panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(button => button.name == "Category Generation chances").onClick.Invoke();
            Transform row = ui.valueInputs[225].transform.parent;
            var toggle = row.Find("SettingSlider/SettingToggle").GetComponent<UnityEngine.UI.Button>();
            Assert.That(row.gameObject.activeInHierarchy, Is.True);
            Assert.That(ui.valueInputs[225].gameObject.activeSelf, Is.False);
            Assert.That(toggle.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("ON"));
            toggle.onClick.Invoke();
            Assert.That(ui.valueInputs[225].text, Is.EqualTo("0"));
            Assert.That(toggle.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("OFF"));
            foreach (int index in new[] { 226, 227 })
            {
                Assert.That(ui.valueInputs[index].gameObject.activeInHierarchy, Is.True);
                Assert.That(ui.valueSliders[index].interactable, Is.True);
                ui.valueSliders[index].value = 0f;
                Assert.That(ui.valueInputs[index].text, Is.EqualTo(index == 226 ? "0" : "1"));
                ui.valueSliders[index].value = 1f;
                Assert.That(ui.valueInputs[index].text, Is.EqualTo(index == 226 ? "100" : "10"));
            }
            Assert.That(ui.network.config.RawStreakCorrectionEnabled, Is.True, "Unapplied edits are only draft settings.");
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
            Assert.That(ui.network.config.RawStreakCorrectionEnabled, Is.False);
            Assert.That(ui.network.config.RawStreakOppositeChancePercent, Is.EqualTo(100));
            Assert.That(ui.network.config.RawStreakMaximum, Is.EqualTo(10));
            string firstSession = ui.network.Snapshot.sessionId;
            ui.Open();
            Assert.That(new[] { ui.valueInputs[225].text, ui.valueInputs[226].text, ui.valueInputs[227].text },
                Is.EqualTo(new[] { "0", "100", "10" }));
            toggle.onClick.Invoke();
            ui.valueInputs[226].text = "0";
            ui.valueInputs[227].text = "1";
            Assert.That(ui.valueSliders[226].value, Is.EqualTo(0f));
            Assert.That(ui.valueSliders[227].value, Is.EqualTo(0f));
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
            Assert.That(ui.network.config.RawStreakCorrectionEnabled, Is.True);
            Assert.That(ui.network.config.RawStreakOppositeChancePercent, Is.EqualTo(0));
            Assert.That(ui.network.config.RawStreakMaximum, Is.EqualTo(1));
            ui.Open();
            Assert.That(new[] { ui.valueInputs[225].text, ui.valueInputs[226].text, ui.valueInputs[227].text },
                Is.EqualTo(new[] { "1", "0", "1" }));
            Assert.That(toggle.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("ON"));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }

        [UnityTest]
        public IEnumerator SavedDeveloperPanelCreatesAndRetunesTwoPlayerHostLobby()
        {
            yield return fixture.Load();
            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabSceneController sceneController = developerUi.sceneController;
            LabSeatCamera seatCamera = developerUi.seatCamera;
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
            Assert.That(developerUi.applyHostButton, Is.Not.Null);
            Assert.That(developerUi.targetParticipantsSlider, Is.Not.Null);
            fixture.UseAvailablePort(network);
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
            yield return fixture.Load();
            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            Assert.That(network, Is.Not.Null);
            Assert.That(developerUi, Is.Not.Null);
            Assert.That(developerUi.valueInputs.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.valueSliders.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(developerUi.applyHostButton, Is.Not.Null);
            Assert.That(developerUi.targetParticipantsSlider.value, Is.EqualTo(3f));
            fixture.UseAvailablePort(network);
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
        public IEnumerator SavedSceneSoloCanGenerateCombineAndPhysicallyPaintTheGourd()
        {
            yield return fixture.Load();

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
            var composition = developerUi.GetComponent<LabOrbHudComposition>();
            Assert.That(composition, Is.Not.Null, "The saved HUD must attach the foreground composition at runtime.");
            Assert.That(boardCamera.targetTexture, Is.Null, "Input coordinates must remain on the original screen camera.");
            int savedBoardMask = boardCamera.cullingMask;
            Assert.That((battleCamera.cullingMask & (1 << board.gameObject.layer)) == 0, Is.True,
                "The new 3D board spheres must not appear in the battle camera.");
            Assert.That(boardCamera.depth, Is.GreaterThan(battleCamera.depth),
                "The board camera must render after the battle camera on mobile devices.");

            // Each solo round has a random session seed, so use
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
            tuning[79] = 0f; // Paint-only reserved slot; check approved paint and particles below.
            tuning[205] = 0f; // no common gimmicks (#311): this test counts the orbs on the board
            Assert.That(rawOnly.TryApplyDeveloperValues(tuning, out string tuningError), Is.True, tuningError);
            network.config = rawOnly;
            // The board reads contact combining from its own config, so it needs the same tuning.
            LabSceneController sceneController = developerUi.sceneController;
            LabConfig savedBoardConfig = sceneController.config;
            sceneController.config = rawOnly;
            sceneController.ReconfigureBoard();

            // Separate this real Host from other Editor/test processes on the machine.
            fixture.UseAvailablePort(network);
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True);
                Assert.That(network.IsHost, Is.True);
                Assert.That(network.ParticipantCount, Is.EqualTo(1));
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(network.Snapshot.players.Length, Is.EqualTo(1));
                yield return WaitForComposition(composition);
                Assert.That((composition.ForegroundCamera.cullingMask & (1 << board.gameObject.layer)) != 0, Is.True);
                Assert.That((boardCamera.cullingMask & (1 << board.gameObject.layer)) == 0, Is.True);
                Assert.That(boardCamera.targetTexture, Is.Null);
                composition.enabled = false;
                Assert.That(boardCamera.cullingMask, Is.EqualTo(savedBoardMask), "Disabling composition must restore direct board rendering.");
                composition.enabled = true;
                yield return WaitForComposition(composition);
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
                Assert.That(yinView.Visual.GetComponentInChildren<SpriteRenderer>().sprite,
                    Is.SameAs(appearance.SpriteFor(LabOrbKind.Yin)));

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
                Assert.That(combinedView.Visual.GetComponentInChildren<SpriteRenderer>().sprite,
                    Is.SameAs(appearance.SpriteFor(LabOrbKind.Combined, combined.element)));

                // A centred upward release produces the same seat-zero arc as manual Solo play.
                // Keep the orb held while moving it into position, pause, then flick up.
                // Releasing and immediately re-grabbing would race the next physics step.
                Vector2 combinedWorld = ToWorld(bounds, combined01);
                Vector2 launchFrom = ToWorld(bounds, new Vector2(.5f, .25f));
                Assert.That(board.TryBeginDragAtWorld(combinedWorld, 2d), Is.True);
                // Arrive before the release window (.12 s) opens: otherwise the window starts partway along
                // the move from wherever the combine happened, and that random leg bends and weakens the flick.
                Assert.That(board.DragToWorld(launchFrom, 2.5d), Is.True);
                Assert.That(board.DragToWorld(launchFrom, 3d), Is.True);
                Vector2 release = launchFrom + Vector2.up * (bounds.width * .2f);
                Assert.That(board.EndDragAtWorld(release, 3.04d), Is.True);
                Assert.That(network.Snapshot.orbs.Any(o => o.id == combined.id && o.inFlight), Is.True,
                    "ThrowRequested must cause a Host-approved launch.");
                LabProjectile projectile = UnityEngine.Object.FindFirstObjectByType<LabProjectile>();
                Assert.That(projectile, Is.Not.Null);
                SphereCollider projectileCollider = projectile.GetComponent<SphereCollider>();
                Assert.That(projectileCollider, Is.Not.Null);
                Assert.That(projectileCollider.isTrigger, Is.False, "The real Host owns physical hit resolution.");
                Assert.That(projectile.ProjectileId, Is.EqualTo(combined.id));
                Assert.That(projectile.OwnerPlayerId, Is.EqualTo(network.LocalPlayerId));
                float blastScale = combined.small ? network.config.BlastShrinkScale : 1f;
                Assert.That(projectile.SizeScale, Is.EqualTo(blastScale));
                float projectileRadius = network.config.ThrowRadius * blastScale * projectile.RadiusScale;
                Assert.That(projectileCollider.radius, Is.EqualTo(projectileRadius).Within(.0001f));
                Assert.That(projectile.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
                Assert.That(projectile.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(1));
                Assert.That(projectile.VisualMount.GetComponentsInChildren<LabOrbSpriteArt>(), Is.Empty);
                Transform sphere = projectile.VisualMount.Find("Replacement Visual/Sphere");
                Assert.That(sphere, Is.Not.Null, "The saved appearance supplies the flight sphere prefab.");
                MeshRenderer sphereRenderer = sphere.GetComponent<MeshRenderer>();
                Assert.That(sphereRenderer != null && sphereRenderer.gameObject.activeInHierarchy, Is.True);
                MeshFilter sphereMesh = sphere.GetComponent<MeshFilter>();
                Assert.That(sphereMesh != null && sphereMesh.sharedMesh != null, Is.True);
                Assert.That(sphereMesh.sharedMesh.bounds.size.z, Is.GreaterThan(.1f));
                foreach (float axis in new[] { sphereRenderer.bounds.size.x, sphereRenderer.bounds.size.y, sphereRenderer.bounds.size.z })
                    Assert.That(axis, Is.EqualTo(2f * projectileRadius).Within(.001f),
                        "The flight sphere keeps the gameplay scale, including blast and combo effects.");
                Assert.That(sphereRenderer.sharedMaterial,
                    Is.SameAs(appearance.MaterialFor(LabOrbKind.Combined, combined.element)));
                var sphereProperties = new MaterialPropertyBlock();
                sphereRenderer.GetPropertyBlock(sphereProperties);
                Color expectedElementColor = appearance.ElementColorFor(combined.element);
                foreach (string colorProperty in new[] { "_BaseColor", "_Color" })
                {
                    Color actualElementColor = sphereProperties.GetColor(colorProperty);
                    Assert.That(actualElementColor.r, Is.EqualTo(expectedElementColor.r).Within(.0001f));
                    Assert.That(actualElementColor.g, Is.EqualTo(expectedElementColor.g).Within(.0001f));
                    Assert.That(actualElementColor.b, Is.EqualTo(expectedElementColor.b).Within(.0001f));
                }
                Assert.That(projectile.ElementFlightTrail, Is.Not.Null);
                Assert.That(projectile.ElementFlightTrail.IsEmitting, Is.True);
                Assert.That(projectile.ElementFlightTrail.Element, Is.EqualTo(combined.element));
                Assert.That(projectile.ElementFlightTrail.Profile,
                    Is.SameAs(appearance.ElementTrailFor(combined.element)));
                Assert.That(projectile.FlightTrail, Is.Null,
                    "Configured elemental effects replace, rather than overlap, the legacy trail.");
                Color expectedFeedbackColor = (Color32)LabOrbAppearance.DefaultElementColor(combined.element);

                for (int step = 0; step < 150 && network.Snapshot.hp == initialHp; step++)
                    yield return new WaitForFixedUpdate();
                Assert.That(network.Snapshot.hp, Is.EqualTo(initialHp - network.config.Damage),
                    "Only a real Host-resolved gourd collision should lower diagnostic HP.");
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing),
                    "One partial paint hit must not end the round.");
                Assert.That(network.Snapshot.paintCount, Is.EqualTo(1));
                Assert.That(network.PaintSurface.CoveredCount, Is.GreaterThan(0).And.LessThan(network.PaintSurface.CellCount),
                    "The real mesh collision must commit paint as well as nominal hit credit.");
                Assert.That(network.Snapshot.players.Single().hitTotal, Is.EqualTo(1));
                Assert.That(network.Snapshot.players.Single().damageTotal, Is.EqualTo(network.config.Damage));
                var particles = UnityEngine.Object.FindFirstObjectByType<LabImpactParticleVfx>();
                Assert.That(particles, Is.Not.Null, "The saved target must bind its approved world-space impact effect.");
                Assert.That(particles.ActiveBurstCount, Is.EqualTo(1));
                LabImpactParticleBurst activeBurst = particles.GetComponentsInChildren<LabImpactParticleBurst>()
                    .Single(burst => burst.gameObject.activeInHierarchy);
                ParticleSystem[] systems = activeBurst.GetComponentsInChildren<ParticleSystem>();
                Assert.That(systems.Length, Is.GreaterThanOrEqualTo(3), "Core, sparks and ring must remain connected.");
                foreach (ParticleSystem system in systems)
                {
                    Color actualColor = (Color32)system.main.startColor.color;
                    Assert.That(actualColor.r, Is.EqualTo(expectedFeedbackColor.r).Within(.001f));
                    Assert.That(actualColor.g, Is.EqualTo(expectedFeedbackColor.g).Within(.001f));
                    Assert.That(actualColor.b, Is.EqualTo(expectedFeedbackColor.b).Within(.001f));
                }
                Transform impactLayer = network.impactFeedback.transform.Find("Impact Feedback");
                Assert.That(impactLayer == null || impactLayer.GetComponentInChildren<UnityEngine.UI.Text>() == null, Is.True,
                    "Paint mode retains its approved hit effect without restoring the retired HP damage labels.");
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
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig gourd = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = gourd.CaptureDeveloperValues();
            tuning[205] = 1f; // the common group (#311) every second...
            tuning[208] = 0f; // ...with only the gourd bomb
            Assert.That(gourd.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = gourd;
            fixture.UseAvailablePort(network);
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
        public IEnumerator SavedSceneSoloFoxFireDropsGlowingFoxOrbsOntoTheOnePlayersBoard()
        {
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig foxy = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = foxy.CaptureDeveloperValues();
            tuning[166] = 1f; // the Lab borrows the fox's own gimmicks (#311), every second
            tuning[205] = 0f;
            tuning[206] = 1f;
            tuning[171] = 100f; // only the fox fire
            tuning[172] = 0f;
            Assert.That(foxy.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = foxy;
            fixture.UseAvailablePort(network);
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                float until = Time.realtimeSinceStartup + 3f;
                LabOrbState[] foxes = System.Array.Empty<LabOrbState>();
                while (foxes.Length == 0 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    foxes = (network.Snapshot?.orbs ?? System.Array.Empty<LabOrbState>())
                        .Where(orb => orb.kind == LabOrbKind.FoxFire).ToArray();
                }
                yield return null; // the board takes the snapshot that holds them
                Assert.That(foxes.Length, Is.InRange(2, 5), "The fox fire drops two to five fox orbs.");
                Assert.That(foxes.All(orb => orb.owner == network.LocalPlayerId), Is.True, "Solo, on the one player.");
                Assert.That(foxes.Any(orb => board.IsDropping(orb.id)), Is.True,
                    "The drop signal reaches the board before the snapshot holding them, so they fall in.");
                Assert.That(network.Notice, Does.Contain("Fox fire"));
                var alert = UnityEngine.Object.FindAnyObjectByType<LabGourdAlert>();
                Assert.That(alert, Is.Not.Null);
                Assert.That(alert.Active, Is.True, "Every player sees the gourd's \"!\" bubble.");
                LabOrbView view = board.GetComponentsInChildren<LabOrbView>().First(candidate => candidate.OrbId == foxes[0].id);
                Assert.That(view.FoxGlowVisible, Is.True);
                Assert.That(view.GetComponentInChildren<LabOrbSpriteArt>(), Is.Not.Null, "It shows the fox orb art.");
            }
            finally
            {
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(foxy);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloCharmShowsOnTheOnePlayersScreenAndWearsOff()
        {
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig charming = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = charming.CaptureDeveloperValues();
            tuning[166] = 1f; // the Lab borrows the fox's own gimmicks (#311), every two seconds
            tuning[205] = 0f;
            tuning[206] = 2f;
            tuning[171] = 0f; // only the charm
            tuning[172] = 100f;
            tuning[176] = 1f; // for one second
            tuning[204] = 1f; // after a one-second warning (#305)
            Assert.That(charming.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = charming;
            fixture.UseAvailablePort(network);
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                var hud = UnityEngine.Object.FindAnyObjectByType<LabCharmHud>();
                Assert.That(hud, Is.Not.Null, "The saved scene adds the charmed player's own display.");
                float until = Time.realtimeSinceStartup + 4f;
                while (!hud.WarningShowing && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(hud.WarningShowing, Is.True, "A warning comes first (#305)...");
                Assert.That(hud.WarningShown, Is.EqualTo(LabCharmHud.WarningText("나")), "...naming me on my own screen.");
                Assert.That(board.Charmed, Is.False, "Not charmed during the warning.");
                until = Time.realtimeSinceStartup + 3f;
                while (!board.Charmed && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(board.Charmed, Is.True, "Solo, to try it out, the charm falls on the one player.");
                Assert.That(board.CharmEdge.Visible, Is.True);
                var alert = UnityEngine.Object.FindAnyObjectByType<LabGourdAlert>();
                Assert.That(alert, Is.Not.Null);
                // The heart comes as the charm itself starts (#305), seen in the alert's own frame.
                float heartBy = Time.realtimeSinceStartup + .5f;
                while (!alert.ShowingHeart && Time.realtimeSinceStartup < heartBy) yield return null;
                Assert.That(alert.ShowingHeart, Is.True, "The gourd's bubble shows a heart to the charmed player.");
                Assert.That(alert.Label, Is.EqualTo("♥"));
                yield return null;
                Assert.That(hud.WarningShowing, Is.False, "The warning goes when the charm starts.");
                Assert.That(hud.WaitShowing, Is.False, "Solo no one beside can wake them, so they are not told to wait (#328).");
                Assert.That(hud.GaugeVisible, Is.True, "The time left shows on the gauge.");
                Assert.That(hud.GaugeFill, Is.InRange(.01f, 1f));
                var minimap = UnityEngine.Object.FindAnyObjectByType<LabDrumMinimap>();
                if (minimap != null && minimap.IsShowing)
                {
                    var gaugeCorners = new Vector3[4];
                    var minimapCorners = new Vector3[4];
                    hud.Gauge.GetWorldCorners(gaugeCorners);
                    minimap.Panel.GetWorldCorners(minimapCorners);
                    Assert.That(gaugeCorners[0].y, Is.GreaterThanOrEqualTo(minimapCorners[1].y),
                        "The gauge stands just above the drum minimap.");
                }

                until = Time.realtimeSinceStartup + 2f;
                while (board.Charmed && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(board.Charmed, Is.False, "Solo there is no one to wake them: it wears off after its second.");
                yield return null;
                Assert.That(hud.AwakeShowing, Is.True, "\"정신 차림!\" as it ends.");
                Assert.That(hud.GaugeVisible, Is.False);
            }
            finally
            {
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(charming);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloGimmickGroupAnnouncesEachCastOnTheOnePlayer()
        {
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabConfig savedConfig = network.config;
            LabConfig mixing = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = mixing.CaptureDeveloperValues();
            tuning[205] = 1f; // the common group (#311), every second...
            tuning[207] = 0f; // ...with only the swipe block
            Assert.That(mixing.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = mixing;
            fixture.UseAvailablePort(network);
            // Record only: an assertion thrown inside the event would interrupt the Host's snapshot.
            var kinds = new List<LabSabotageKind>();
            var targets = new List<ulong>();
            var blocks = new List<bool>();
            System.Action<LabSabotageKind, ulong> onCast = (kind, target) =>
            {
                kinds.Add(kind);
                targets.Add(target);
            };
            System.Action<bool, float> onBlock = (right, seconds) => blocks.Add(right);
            network.SabotageCast += onCast;
            network.SwipeBlockStarted += onBlock;
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                float until = Time.realtimeSinceStartup + 3f;
                while ((kinds.Count == 0 || blocks.Count == 0) && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(kinds, Is.Not.Empty, "The group casts after its interval.");
                Assert.That(kinds, Is.All.EqualTo(LabSabotageKind.SwipeBlock));
                Assert.That(targets, Is.All.EqualTo(network.LocalPlayerId), "Solo, it falls on the one player.");
                Assert.That(blocks, Is.Not.Empty, "What it cast is the gourd's ordinary swipe block.");
                Assert.That(network.Snapshot.SwipeBlockOn(network.LocalPlayerId), Is.True);

                until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.That(kinds.Count, Is.EqualTo(1),
                    "Each cast is announced once, and no second block comes while the first still holds.");
            }
            finally
            {
                network.SabotageCast -= onCast;
                network.SwipeBlockStarted -= onBlock;
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(mixing);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSoloSwipeBlockBouncesAndShowsOnTheBlockedPlayersOwnScreen()
        {
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabOrbBoard board = network.board;
            LabConfig savedConfig = network.config;
            LabConfig blocking = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = blocking.CaptureDeveloperValues();
            tuning[205] = 1f; // the common group (#311) one second in, with only the swipe block
            tuning[207] = 0f;
            tuning[153] = 3f;
            Assert.That(blocking.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            network.config = blocking;
            fixture.UseAvailablePort(network);
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

        [UnityTest]
        public IEnumerator SavedSceneSoloRecordsEachFinishedRoundsGimmickStatsOnceWhenTurnedOn()
        {
            yield return fixture.Load();

            LabDeveloperMode developerUi = SavedDeveloperUi();
            LabNetwork network = developerUi.network;
            LabConfig savedConfig = network.config;
            LabConfig recording = UnityEngine.Object.Instantiate(savedConfig);
            float[] tuning = recording.CaptureDeveloperValues();
            tuning[2] = 3f;   // a three-second round
            tuning[205] = 1f; // the common group (#311) every second...
            tuning[208] = 0f; // ...with only the gourd bomb
            Assert.That(recording.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
            JsonUtility.FromJsonOverwrite("{\"recordGimmickStats\":true}", recording); // the Inspector-only UT switch
            network.config = recording;
            fixture.UseAvailablePort(network);
            // The test writes to its own file, never to the player's gimmick-stats.jsonl.
            string path = System.IO.Path.Combine(Application.temporaryCachePath,
                "gimmick-stats-" + System.Guid.NewGuid().ToString("N") + ".jsonl");
            var pathField = typeof(LabNetwork).GetField("gimmickStatsPath",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(pathField, Is.Not.Null);
            pathField.SetValue(network, path);
            try
            {
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                float until = Time.realtimeSinceStartup + 8f;
                while ((network.Snapshot == null || network.Snapshot.phase == LabPhase.Playing) && Time.realtimeSinceStartup < until)
                    yield return null;
                Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Defeat), "The three-second round runs out.");
                until = Time.realtimeSinceStartup + .4f;
                while (Time.realtimeSinceStartup < until) yield return null; // several more snapshots go out meanwhile

                Assert.That(System.IO.File.Exists(path), Is.True, "The Host writes the finished round's record.");
                string[] lines = System.IO.File.ReadAllLines(path);
                Assert.That(lines.Length, Is.EqualTo(1), "One line per finished round, however many snapshots follow it.");
                var stats = JsonUtility.FromJson<LabGimmickStats>(lines[0]);
                Assert.That((stats.result, stats.players, stats.round, stats.stage), Is.EqualTo(("Defeat", 1, 1u, "None")));
                Assert.That((stats.total, stats.gourdBomb, stats.simultaneous), Is.EqualTo((3, 3, 0)),
                    "A bomb at 1, 2 and 3 s of battle time, however the frames fell.");
                Assert.That(stats.seconds, Is.EqualTo(3d).Within(1e-6d));
                Assert.That(stats.commonInterval, Is.EqualTo(1f));
            }
            finally
            {
                network.Stop();
                network.config = savedConfig;
                UnityEngine.Object.Destroy(recording);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator SavedHostFixtureUsesAnAvailablePortAndDisposesItsPersistentNetwork()
        {
            yield return fixture.Load();
            LabNetwork network = SavedDeveloperUi().network;
            ushort occupiedPort = fixture.UseAvailablePort(network);
            using (var occupied = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
            {
                occupied.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, occupiedPort));
                ushort hostPort = fixture.UseAvailablePort(network);
                Assert.That(hostPort, Is.Not.EqualTo(occupiedPort),
                    "An external listener must not make this fixture reuse an occupied port.");
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                Assert.That(network.IsHost, Is.True);
                // Do not Stop here: cleanup must also handle a test that exits while hosting.
                yield return fixture.Cleanup();
                Assert.That(network == null, Is.True,
                    "Scene unload alone cannot release an NGO object in DontDestroyOnLoad.");
                using (var released = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp))
                    Assert.DoesNotThrow(() => released.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, hostPort)),
                        "Disposing the owned manager must release the Host socket before the next fixture.");
            }
        }

        private static IEnumerator WaitForComposition(LabOrbHudComposition composition)
        {
            // The test coroutine can resume before HUD.Update and composition.LateUpdate.
            // Wait only for that bounded presentation hand-off; a real renderer error fails at once.
            for (int frame = 0; frame < 8 && !composition.IsCompositing; frame++)
            {
                Assert.That(composition.InitializationError, Is.Null, composition.InitializationError);
                yield return null;
            }
            Assert.That(composition.InitializationError, Is.Null, composition.InitializationError);
            Assert.That(composition.GetComponent<LabHud>().battleUi.activeInHierarchy, Is.True);
            Assert.That(composition.IsCompositing, Is.True, "The active battle HUD did not acquire its foreground renderer within eight frames.");
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
