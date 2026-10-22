using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Saved DEV controls and actual local Solo/Host boundaries; no remote Client ACK claim.</summary>
    public sealed class LabPaintClusterDeveloperTests
    {
        private const int CompactnessField = 138;
        private const int IrregularityField = 139;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabDeveloperMode ui;
        private float[] sourceValues;
        private string sourceJson;

        [UnitySetUp]
        public IEnumerator LoadSavedPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            sourceValues = ui.sourceConfig.CaptureDeveloperValues();
            sourceJson = JsonUtility.ToJson(ui.sourceConfig);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(192));
            Assert.That(ui.valueSliders.Length, Is.EqualTo(192));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (ui != null && ui.network != null)
            {
                ui.network.Stop();
                Object.Destroy(ui.network.gameObject);
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Paint cluster DEV cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator GourdClusterSlidersSynchronizeWithNumbersAndRemainReachable()
        {
            ui.Open();
            foreach (int index in new[] { CompactnessField, IrregularityField })
                Assert.That(ui.valueInputs[index].transform.parent.gameObject.activeSelf, Is.False);
            ui.panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Category Gourd")
                .onClick.Invoke();
            Assert.That(LabConfig.DeveloperFields[CompactnessField].Label, Is.EqualTo("Paint Cluster Compactness"));
            Assert.That(LabConfig.DeveloperFields[IrregularityField].Label, Is.EqualTo("Paint Cluster Irregularity"));
            Assert.That(ui.valueInputs[CompactnessField].transform.parent.GetSiblingIndex(),
                Is.EqualTo(ui.valueInputs[106].transform.parent.GetSiblingIndex() + 1),
                "Cluster controls belong beside the initial/repeat paint area controls.");
            Assert.That(ui.valueInputs[IrregularityField].transform.parent.GetSiblingIndex(),
                Is.EqualTo(ui.valueInputs[CompactnessField].transform.parent.GetSiblingIndex() + 1));

            foreach (int index in new[] { CompactnessField, IrregularityField })
            {
                Assert.That(ui.valueInputs[index].gameObject.activeInHierarchy, Is.True);
                Assert.That(ui.valueInputs[index].interactable && !ui.valueInputs[index].readOnly, Is.True);
                Assert.That(ui.valueSliders[index].interactable, Is.True);
                Assert.That(ui.valueInputs[index].transform.parent.Find("SettingLabel").GetComponent<Text>().text,
                    Is.EqualTo(LabConfig.DeveloperFields[index].DisplayLabel));
                foreach (float value in new[] { 0f, .375f, 1f })
                {
                    ui.valueInputs[index].text = value.ToString(CultureInfo.InvariantCulture);
                    Assert.That(ui.valueSliders[index].value, Is.EqualTo(value).Within(.0001f));
                }
                foreach (float position in new[] { 0f, .625f, 1f })
                {
                    ui.valueSliders[index].value = position;
                    Assert.That(ReadInput(index), Is.EqualTo(position).Within(.0001f));
                }
                yield return ShowAndCheckInputRaycast(index);
            }
            Assert.That(ui.network.config.CaptureDeveloperValues(), Is.EqualTo(sourceValues),
                "Moving sliders and entering numbers only changes the draft.");
            Assert.That(ui.network.Snapshot, Is.Null);
            AssertSourceUnchanged();
            ui.Close(); ui.Open();
            AssertDraft(sourceValues[CompactnessField], sourceValues[IrregularityField]);
        }

        [UnityTest]
        public IEnumerator InvalidClusterInputRejectsTheWholeSoloFormAndKeepsTheAcceptedRound()
        {
            ui.network.port = 19208;
            ui.Open(); SetDraft(.35f, .7f);
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            float[] accepted = ui.network.config.CaptureDeveloperValues();
            string session = ui.network.Snapshot.sessionId;
            foreach (int index in new[] { CompactnessField, IrregularityField })
            foreach (string invalid in new[] { "-0.01", "1.01", "NaN", "not-a-number" })
            {
                ui.Open();
                ui.valueInputs[0].text = "777";
                SetDraft(.9f, .1f);
                ui.valueInputs[index].text = invalid;
                ui.applyStartButton.onClick.Invoke();
                Assert.That(ui.panel.activeSelf, Is.True);
                Assert.That(ui.feedbackText.text, Does.Contain(LabConfig.DeveloperFields[index].Label), invalid);
                Assert.That(ui.network.Snapshot.sessionId, Is.EqualTo(session));
                Assert.That(ui.network.config.CaptureDeveloperValues(), Is.EqualTo(accepted),
                    "A late invalid field cannot apply an earlier field or restart the round.");
                AssertSourceUnchanged();
            }
        }

        [UnityTest]
        public IEnumerator SoloRestartCopiesBothClusterValuesAndDefaultsRemainDraftUntilApplied()
        {
            ui.network.port = 19209;
            ui.Open(); SetDraft(.2f, .8f);
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            AssertRuntime(.2f, .8f);
            LabPaintGrowthSettings firstProfile = ui.network.config.PaintGrowthSettings;
            string firstSession = ui.network.Snapshot.sessionId;
            LabPaintSurface firstSurface = Model().PaintSurface;
            Assert.That(firstSurface.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Fire),
                ui.network.LocalPlayerId, 1), Is.True);
            Assert.That(firstSurface.CoveredCount, Is.GreaterThan(0));

            ui.Open();
            AssertDraft(.2f, .8f);
            SetDraft(1f, 0f);
            AssertRuntime(.2f, .8f);
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
            AssertRuntime(1f, 0f);
            Assert.That(Model().PaintSurface.CoveredCount, Is.Zero);
            Assert.That(Model().PaintSurface.PaintCount, Is.Zero);
            Assert.That(firstProfile.ClusterCompactness, Is.EqualTo(.2f), "Captured round settings stay independent.");
            Assert.That(firstProfile.ClusterIrregularity, Is.EqualTo(.8f));
            string secondSession = ui.network.Snapshot.sessionId;
            Assert.That(ui.network.RestartDeveloperSolo(), Is.True, ui.network.Notice);
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(secondSession));
            AssertRuntime(1f, 0f);

            ui.Open(); ui.defaultsButton.onClick.Invoke();
            AssertDraft(sourceValues[CompactnessField], sourceValues[IrregularityField]);
            AssertRuntime(1f, 0f);
            string beforeDefaultsApply = ui.network.Snapshot.sessionId;
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.Snapshot.sessionId, Is.Not.EqualTo(beforeDefaultsApply));
            AssertRuntime(sourceValues[CompactnessField], sourceValues[IrregularityField]);
            AssertSourceUnchanged();
            ui.endSoloButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.config, Is.SameAs(ui.sourceConfig));
            AssertSourceUnchanged();
        }

        [UnityTest]
        public IEnumerator HostLobbyAppliesAndFingerprintsClusterProfileWithoutSkippingRoomGates()
        {
            ui.network.port = 19210;
            ui.Open(); ui.targetParticipantsSlider.value = 2f;
            SetDraft(.25f, .75f);
            ui.applyHostButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.network.IsHost, Is.True, ui.feedbackText.text);
            Assert.That(ui.network.IsDeveloperSolo, Is.False);
            Assert.That(ui.network.TargetParticipantCount, Is.EqualTo(2));
            Assert.That(ui.network.ParticipantCount, Is.EqualTo(1), "This fixture uses an actual local Host lobby.");
            AssertRuntime(.25f, .75f);
            int revision = NetworkField<int>("configRevision");
            string fingerprint = NetworkField<string>("configFingerprint");
            Assert.That(ui.network.SessionConfigReady, Is.True, "Only the local Host has acknowledged this fixture.");

            ui.Open();
            Assert.That(ui.applyHostLabel.text, Is.EqualTo("APPLY TO ROOM"));
            AssertDraft(.25f, .75f);
            SetDraft(.9f, .1f);
            ui.valueInputs[139].text = "1.01";
            ui.applyHostButton.onClick.Invoke();
            Assert.That(ui.feedbackText.text, Does.Contain("Paint Cluster Irregularity"));
            AssertRuntime(.25f, .75f);
            Assert.That(NetworkField<int>("configRevision"), Is.EqualTo(revision));
            Assert.That(NetworkField<string>("configFingerprint"), Is.EqualTo(fingerprint));
            ui.valueInputs[139].text = "0.1";
            ui.applyHostButton.onClick.Invoke();
            yield return null;
            AssertRuntime(.9f, .1f);
            Assert.That(NetworkField<int>("configRevision"), Is.EqualTo(revision + 1));
            Assert.That(NetworkField<string>("configFingerprint"), Is.Not.EqualTo(fingerprint));
            Assert.That(NetworkField<string>("configFingerprint"), Is.EqualTo(
                LabSessionSettings.Fingerprint(ui.network.config.CaptureDeveloperValues(), 2, 5658)));
            Assert.That(ui.network.CanStartBattle, Is.False);
            ui.network.StartBattle();
            Assert.That(ui.network.Snapshot, Is.Null,
                "Cluster changes cannot bypass missing participants, positions or their settings confirmation.");
            Assert.That(ui.network.CanRestartSharedBattle, Is.False);
            Assert.That(ui.network.RestartBattleWithDeveloperConfig(ui.sourceConfig), Is.False);
            AssertRuntime(.9f, .1f);
            ui.Open(); AssertDraft(.9f, .1f);
            AssertSourceUnchanged();
        }

        private IEnumerator ShowAndCheckInputRaycast(int index)
        {
            ScrollRect scroll = ui.panel.GetComponentsInChildren<ScrollRect>(true).Single();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            var input = (RectTransform)ui.valueInputs[index].transform;
            Vector3 rowCenter = scroll.viewport.InverseTransformPoint(input.TransformPoint(input.rect.center));
            scroll.content.anchoredPosition += Vector2.up * (scroll.viewport.rect.center.y - rowCenter.y);
            scroll.StopMovement();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Vector3 center = scroll.viewport.InverseTransformPoint(input.TransformPoint(input.rect.center));
            Assert.That(scroll.viewport.rect.Contains(new Vector2(center.x, center.y)), Is.True,
                LabConfig.DeveloperFields[index].Label + " must be reachable by scrolling.");
            Canvas canvas = ui.panel.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, input.TransformPoint(input.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform == input || hits[0].gameObject.transform.IsChildOf(input), Is.True);
        }

        private void SetDraft(float compactness, float irregularity)
        {
            ui.valueInputs[CompactnessField].text = compactness.ToString(CultureInfo.InvariantCulture);
            ui.valueInputs[IrregularityField].text = irregularity.ToString(CultureInfo.InvariantCulture);
        }

        private void AssertDraft(float compactness, float irregularity)
        {
            Assert.That(ReadInput(CompactnessField), Is.EqualTo(compactness).Within(.0001f));
            Assert.That(ReadInput(IrregularityField), Is.EqualTo(irregularity).Within(.0001f));
            Assert.That(ui.valueSliders[CompactnessField].value, Is.EqualTo(compactness).Within(.0001f));
            Assert.That(ui.valueSliders[IrregularityField].value, Is.EqualTo(irregularity).Within(.0001f));
        }

        private void AssertRuntime(float compactness, float irregularity)
        {
            Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
            Assert.That(ui.network.config.PaintClusterCompactness, Is.EqualTo(compactness).Within(.0001f));
            Assert.That(ui.network.config.PaintClusterIrregularity, Is.EqualTo(irregularity).Within(.0001f));
            Assert.That(ui.network.config.PaintGrowthSettings.ClusterCompactness, Is.EqualTo(compactness).Within(.0001f));
            Assert.That(ui.network.config.PaintGrowthSettings.ClusterIrregularity, Is.EqualTo(irregularity).Within(.0001f));
            Assert.That(ui.network.config.CaptureDeveloperValues()[CompactnessField], Is.EqualTo(compactness).Within(.0001f));
            Assert.That(ui.network.config.CaptureDeveloperValues()[IrregularityField], Is.EqualTo(irregularity).Within(.0001f));
            Assert.That(ui.sceneController.config, Is.SameAs(ui.network.config));
            Assert.That(ui.seatCamera.config, Is.SameAs(ui.network.config));
        }

        private void AssertSourceUnchanged()
        {
            Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(sourceValues));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }

        private float ReadInput(int index) => float.Parse(ui.valueInputs[index].text, CultureInfo.InvariantCulture);
        private T NetworkField<T>(string name) => (T)typeof(LabNetwork).GetField(name, Private).GetValue(ui.network);
        private LabModel Model() => NetworkField<LabModel>("model");
    }
}
