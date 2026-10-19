using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Saved Canvas and real local Host boundaries; no synthetic remote ACK or device claim.</summary>
    public sealed class LabPaintPatternDeveloperTests
    {
        private const int ViewerField = 111;
        private const int PlayerPatternsField = 142;
        private LabDeveloperMode ui;
        private string sourceJson;

        [UnitySetUp]
        public IEnumerator LoadSavedPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            sourceJson = JsonUtility.ToJson(ui.sourceConfig);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(149), "The saved DEV panel includes both appended cluster controls.");
            Assert.That(ui.valueSliders.Length, Is.EqualTo(149), "Every DEV field keeps its corresponding control slot.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (ui != null && ui.network != null)
            {
                ui.network.Stop(); Object.Destroy(ui.network.gameObject); yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Paint pattern DEV cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator FourNamedButtonsSwitchOnlyTheDraftAndDefaultsRestoresSplatter()
        {
            ui.Open();
            var category = ui.panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Category Gourd");
            Transform row = ui.valueInputs[139].transform.parent;
            Assert.That(row.gameObject.activeSelf, Is.False);
            category.onClick.Invoke();
            Assert.That(row.gameObject.activeInHierarchy, Is.True);
            Assert.That(ui.valueInputs[139].gameObject.activeSelf, Is.False);
            Assert.That(ui.valueSliders[139].interactable, Is.False);
            Button[] choices = Choices();
            Assert.That(choices.Select(button => button.GetComponentInChildren<Text>().text),
                Is.EqualTo(new[] { "LEGACY", "BROAD", "SPLATTER", "FLOW" }));
            Assert.That(choices.All(button => button.interactable), Is.True);
            Assert.That(ui.valueInputs[139].text, Is.EqualTo("2"));
            Button legacyStyle = ui.valueInputs[133].transform.parent.Find("SettingSlider/SettingToggle").GetComponent<Button>();
            Assert.That(legacyStyle.interactable, Is.False);
            for (int pattern = 0; pattern < 4; pattern++)
            {
                choices[pattern].onClick.Invoke();
                Assert.That(ui.valueInputs[139].text, Is.EqualTo(pattern.ToString()));
                Assert.That(legacyStyle.interactable, Is.EqualTo(pattern == 0));
                Assert.That(ui.network.config.PaintPattern, Is.EqualTo(LabPaintPattern.Splatter), "A button selection is not APPLY.");
                Color selected = ((Image)choices[pattern].targetGraphic).color;
                Assert.That(choices.Where((button, index) => index != pattern)
                    .All(button => ((Image)button.targetGraphic).color != selected), Is.True,
                    "The accepted draft choice must be visually distinguishable.");
            }
            ui.defaultsButton.onClick.Invoke();
            Assert.That(ui.valueInputs[139].text, Is.EqualTo("2"));
            Assert.That(legacyStyle.interactable, Is.False);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            Assert.That(ui.network.Snapshot, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllFourPatternsApplyToSoloRoundCopiesAndRestartResetsPaint()
        {
            ui.network.port = 19200;
            foreach (int pattern in new[] { 2, 0, 1, 3 })
            {
                ui.Open();
                Choices()[pattern].onClick.Invoke();
                LabConfig previous = ui.network.config;
                ui.applyStartButton.onClick.Invoke();
                yield return null; yield return null;
                Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
                Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
                Assert.That(ui.network.config.PaintPattern, Is.EqualTo((LabPaintPattern)pattern));
                Assert.That(ui.network.config.PaintGrowthSettings.Pattern, Is.EqualTo((LabPaintPattern)pattern));
                LabModel model = Model();
                Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
                Assert.That(model.PaintSurface.PaintCount, Is.Zero);
                Assert.That(model.PaintSurface.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Fire), 1), Is.True);
                Assert.That(model.PaintSurface.CoveredCount, Is.GreaterThan(0));
                Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            }
        }

        [UnityTest]
        public IEnumerator HostLobbyAppliesPatternButCannotSkipParticipantOrPausedRestartGates()
        {
            ui.network.port = 19201;
            ui.Open(); ui.targetParticipantsSlider.value = 2f;
            Choices()[1].onClick.Invoke();
            ui.applyHostButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsHost, Is.True, ui.feedbackText.text);
            Assert.That(ui.network.IsDeveloperSolo, Is.False);
            Assert.That(ui.network.TargetParticipantCount, Is.EqualTo(2));
            Assert.That(ui.network.ParticipantCount, Is.EqualTo(1));
            Assert.That(ui.network.config.PaintPattern, Is.EqualTo(LabPaintPattern.Broad));
            Assert.That(ui.network.Snapshot, Is.Null);
            ui.Open(); Choices()[3].onClick.Invoke();
            Assert.That(ui.network.config.PaintPattern, Is.EqualTo(LabPaintPattern.Broad));
            ui.valueInputs[139].text = "4";
            ui.applyHostButton.onClick.Invoke();
            Assert.That(ui.feedbackText.text, Does.Contain("Paint Pattern"));
            Assert.That(ui.network.config.PaintPattern, Is.EqualTo(LabPaintPattern.Broad));
            Choices()[3].onClick.Invoke(); ui.applyHostButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.config.PaintPattern, Is.EqualTo(LabPaintPattern.Flow));
            Assert.That(ui.network.CanStartBattle, Is.False);
            ui.network.StartBattle();
            Assert.That(ui.network.Snapshot, Is.Null, "Changing pattern cannot bypass a missing participant or settings ACK.");
            Assert.That(ui.network.CanRestartSharedBattle, Is.False);
            Assert.That(ui.network.RestartBattleWithDeveloperConfig(ui.sourceConfig), Is.False);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }

        [UnityTest]
        public IEnumerator ClosingAndReopeningDoesNotDuplicateChoicesOrKeepUnappliedDrafts()
        {
            ui.Open();
            Transform choiceParent = Choices()[0].transform.parent;
            int count = choiceParent.childCount;
            Choices()[0].onClick.Invoke();
            ui.closeButton.onClick.Invoke();
            ui.Open();
            yield return null;
            Assert.That(ui.valueInputs[139].text, Is.EqualTo("2"));
            Assert.That(Choices()[0].transform.parent, Is.SameAs(choiceParent));
            Assert.That(choiceParent.childCount, Is.EqualTo(count));
            Assert.That(count, Is.EqualTo(4));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }

        [UnityTest]
        public IEnumerator IndependentFeatureButtonsChangeOnlyTheDraftAndDefaultsRestoreBothOn()
        {
            ui.Open();
            ui.panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Category Gourd")
                .onClick.Invoke();
            Assert.That(LabConfig.DeveloperFields[ViewerField].Label, Is.EqualTo("Completed Gourd Viewer (0 off, 1 on)"));
            Assert.That(LabConfig.DeveloperFields[PlayerPatternsField].Label, Is.EqualTo("Player Paint Patterns (0 off, 1 on)"));
            AssertFeatureConfig(ui.sourceConfig, true, true);
            AssertFeatureDraft(true, true);
            Button viewerToggle = FeatureToggle(ViewerField), patternsToggle = FeatureToggle(PlayerPatternsField);
            Assert.That(viewerToggle.gameObject.activeInHierarchy, Is.True);
            Assert.That(patternsToggle.gameObject.activeInHierarchy, Is.True);
            viewerToggle.onClick.Invoke();
            AssertFeatureDraft(false, true);
            patternsToggle.onClick.Invoke();
            AssertFeatureDraft(false, false);
            viewerToggle.onClick.Invoke();
            AssertFeatureDraft(true, false);
            AssertFeatureConfig(ui.network.config, true, true);
            Assert.That(ui.network.Snapshot, Is.Null, "Changing feature buttons does not start a round.");
            ui.defaultsButton.onClick.Invoke();
            AssertFeatureDraft(true, true);
            AssertFeatureConfig(ui.network.config, true, true);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloRestartAppliesIndependentFeatureSettingsAndDefaultsStayDraftUntilApply()
        {
            ui.network.port = 19202;
            ui.Open(); SetFeatureDraft(false, true);
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            AssertFeatureConfig(ui.network.config, false, true);
            string firstSession = ui.network.Snapshot.sessionId;

            ui.Open();
            AssertFeatureDraft(false, true);
            SetFeatureDraft(true, false);
            AssertFeatureConfig(ui.network.config, false, true);
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
            Assert.That(ui.network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
            AssertFeatureConfig(ui.network.config, true, false);
            string secondSession = ui.network.Snapshot.sessionId;
            Assert.That(ui.network.RestartDeveloperSolo(), Is.True, ui.network.Notice);
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(secondSession));
            AssertFeatureConfig(ui.network.config, true, false);

            ui.Open(); ui.defaultsButton.onClick.Invoke();
            AssertFeatureDraft(true, true);
            AssertFeatureConfig(ui.network.config, true, false);
            string beforeDefaultsApply = ui.network.Snapshot.sessionId;
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(beforeDefaultsApply));
            AssertFeatureConfig(ui.network.config, true, true);
            Assert.That(ui.network.PaintSurface.CoveredCount, Is.Zero);
            Assert.That(ui.network.PaintSurface.PaintCount, Is.Zero);
            ui.endSoloButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.config, Is.SameAs(ui.sourceConfig));
            AssertFeatureConfig(ui.network.config, true, true);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }

        [UnityTest]
        public IEnumerator HostLobbySharesBothIndependentFeaturesWithoutSkippingRoomStartOrRestartGates()
        {
            ui.network.port = 19203;
            for (int combination = 0; combination < 4; combination++)
            {
                bool viewerEnabled = (combination & 2) != 0;
                bool patternsEnabled = (combination & 1) != 0;
                ui.Open();
                if (combination == 0) ui.targetParticipantsSlider.value = 2f;
                SetFeatureDraft(viewerEnabled, patternsEnabled);
                ui.applyHostButton.onClick.Invoke();
                yield return null;
                Assert.That(ui.network.IsHost, Is.True, ui.feedbackText.text);
                Assert.That(ui.network.IsDeveloperSolo, Is.False);
                Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
                AssertFeatureConfig(ui.network.config, viewerEnabled, patternsEnabled);
                Assert.That(ui.sceneController.config, Is.SameAs(ui.network.config));
                Assert.That(ui.seatCamera.config, Is.SameAs(ui.network.config));
                Assert.That(ui.network.config.CaptureDeveloperValues()[ViewerField], Is.EqualTo(viewerEnabled ? 1f : 0f));
                Assert.That(ui.network.config.CaptureDeveloperValues()[PlayerPatternsField], Is.EqualTo(patternsEnabled ? 1f : 0f));
                Assert.That(ui.network.TargetParticipantCount, Is.EqualTo(2));
                Assert.That(ui.network.ParticipantCount, Is.EqualTo(1), "This fixture exercises the actual local Host lobby.");
                Assert.That(ui.network.CanStartBattle, Is.False);
                ui.network.StartBattle();
                Assert.That(ui.network.Snapshot, Is.Null, "Feature changes cannot skip missing participants or current settings confirmation.");
                Assert.That(ui.network.CanRestartSharedBattle, Is.False);
                Assert.That(ui.network.RestartBattleWithDeveloperConfig(ui.sourceConfig), Is.False,
                    "Feature settings do not bypass the shared paused-battle restart gate.");
                AssertFeatureConfig(ui.network.config, viewerEnabled, patternsEnabled);
                ui.Open();
                AssertFeatureDraft(viewerEnabled, patternsEnabled);
                ui.Close();
                Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            }
            ui.network.Stop();
            yield return null;
            Assert.That(ui.network.config, Is.SameAs(ui.sourceConfig));
            AssertFeatureConfig(ui.network.config, true, true);
        }

        private Button FeatureToggle(int field) => ui.valueInputs[field].transform.parent
            .Find("SettingSlider/SettingToggle").GetComponent<Button>();

        private void SetFeatureDraft(bool viewerEnabled, bool patternsEnabled)
        {
            ui.valueInputs[ViewerField].text = viewerEnabled ? "1" : "0";
            ui.valueInputs[PlayerPatternsField].text = patternsEnabled ? "1" : "0";
        }

        private void AssertFeatureDraft(bool viewerEnabled, bool patternsEnabled)
        {
            Assert.That(ui.valueInputs[ViewerField].text, Is.EqualTo(viewerEnabled ? "1" : "0"));
            Assert.That(ui.valueInputs[PlayerPatternsField].text, Is.EqualTo(patternsEnabled ? "1" : "0"));
            Assert.That(FeatureToggle(ViewerField).GetComponentInChildren<Text>().text, Is.EqualTo(viewerEnabled ? "ON" : "OFF"));
            Assert.That(FeatureToggle(PlayerPatternsField).GetComponentInChildren<Text>().text, Is.EqualTo(patternsEnabled ? "ON" : "OFF"));
        }

        private static void AssertFeatureConfig(LabConfig config, bool viewerEnabled, bool patternsEnabled)
        {
            Assert.That(config.CompletedGourdViewerEnabled, Is.EqualTo(viewerEnabled));
            Assert.That(config.PlayerPaintPatternsEnabled, Is.EqualTo(patternsEnabled));
        }

        private Button[] Choices() => Enumerable.Range(0, 4).Select(option => ui.valueInputs[139].transform.parent
            .Find("SettingSlider/SettingChoices/Choice" + option).GetComponent<Button>()).ToArray();
        private LabModel Model() => (LabModel)typeof(LabNetwork).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui.network);
    }
}
