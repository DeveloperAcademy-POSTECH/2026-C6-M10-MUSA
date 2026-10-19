using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Saved DEV controls and local round application; no remote Client ACK claim.</summary>
    public sealed class LabPaintSpreadFlowDeveloperTests
    {
        private LabDeveloperMode ui;
        private string sourceJson;

        [UnitySetUp]
        public IEnumerator LoadPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            sourceJson = JsonUtility.ToJson(ui.sourceConfig);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(157), "The saved DEV panel includes both appended cluster controls.");
            Assert.That(ui.valueSliders.Length, Is.EqualTo(157), "The saved DEV panel includes both appended cluster controls.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (ui != null && ui.network != null)
            {
                ui.network.Stop(); Object.Destroy(ui.network.gameObject); yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Spread flow DEV cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator GourdStyleToggleAndFiveSlidersAreReachableAndDefaultsOnlyChangeTheDraft()
        {
            ui.Open();
            Button category = ui.panel.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Category Gourd");
            for (int index = 133; index <= 138; index++)
                Assert.That(ui.valueInputs[index].transform.parent.gameObject.activeSelf, Is.False);
            category.onClick.Invoke();
            ui.valueInputs[139].text = "0"; // Explicitly test the preserved LEGACY submode.
            Button toggle = ui.valueInputs[133].transform.parent.Find("SettingSlider/SettingToggle").GetComponent<Button>();
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ORIGINAL"));
            Assert.That(ui.valueInputs[133].gameObject.activeSelf, Is.False);
            toggle.onClick.Invoke();
            Assert.That(ui.valueInputs[133].text, Is.EqualTo("1"));
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("SPREAD + FLOW"));
            float[] profile = { .8f, .6f, 5f, 100f, 6f };
            for (int index = 134; index <= 138; index++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[index];
                Assert.That(ui.valueInputs[index].gameObject.activeInHierarchy, Is.True);
                Assert.That(ui.valueInputs[index].interactable && !ui.valueInputs[index].readOnly, Is.True);
                Assert.That(ui.valueSliders[index].interactable, Is.True);
                ui.valueInputs[index].text = profile[index - 134].ToString(CultureInfo.InvariantCulture);
                Assert.That(ui.valueSliders[index].value,
                    Is.EqualTo(Mathf.InverseLerp(field.Min, field.Max, profile[index - 134])).Within(.0001f));
                ui.valueSliders[index].value = .75f;
                Assert.That(float.Parse(ui.valueInputs[index].text, CultureInfo.InvariantCulture),
                    Is.EqualTo(Mathf.Lerp(field.Min, field.Max, .75f)).Within(.0001f));
            }
            ScrollRect scroll = ui.panel.GetComponentsInChildren<ScrollRect>(true).Single();
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 0f; scroll.StopMovement();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var input = (RectTransform)ui.valueInputs[138].transform;
            Vector3 center = scroll.viewport.InverseTransformPoint(input.TransformPoint(input.rect.center));
            Assert.That(scroll.viewport.rect.Contains(new Vector2(center.x, center.y)), Is.True);
            Canvas canvas = ui.panel.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, input.TransformPoint(input.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform == input || hits[0].gameObject.transform.IsChildOf(input), Is.True);
            Assert.That(ui.network.config.PaintGrowthStyle, Is.Zero, "A draft does not alter the accepted round.");
            ui.defaultsButton.onClick.Invoke();
            float[] defaults = ui.sourceConfig.CaptureDeveloperValues();
            for (int index = 133; index <= 138; index++)
                Assert.That(float.Parse(ui.valueInputs[index].text, CultureInfo.InvariantCulture), Is.EqualTo(defaults[index]));
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ORIGINAL"));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            Assert.That(ui.network.config.PaintGrowthStyle, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SoloRestartAppliesSpreadFlowThenReturnsToOriginalWithoutChangingTheSourceAsset()
        {
            ui.network.port = 19198;
            ui.Open(); ui.valueInputs[139].text = "0";
            string[] profile = { "1", "0.8", "0.6", "5", "100", "6" };
            for (int index = 133; index <= 138; index++) ui.valueInputs[index].text = profile[index - 133];
            ui.applyStartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
            Assert.That(ui.network.config.PaintGrowthStyle, Is.EqualTo(1));
            LabPaintGrowthSettings settings = ui.network.config.PaintGrowthSettings;
            Assert.That(settings.Style, Is.EqualTo(1));
            Assert.That(settings.Raggedness, Is.EqualTo(.8f));
            Assert.That(settings.FlowRatio, Is.EqualTo(.6f));
            Assert.That(settings.DripWidthDegrees, Is.EqualTo(5f));
            Assert.That(settings.DripLengthDegrees, Is.EqualTo(100f));
            Assert.That(settings.GravityWeight, Is.EqualTo(6f));
            Assert.That(ui.network.config.PaintGrainEnabled, Is.False, "Growth style is independent of the grain experiment.");
            Assert.That(ui.network.Snapshot.round, Is.EqualTo(1));
            ui.Open(); ui.valueInputs[133].text = "0";
            ui.applyStartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(ui.network.config.PaintGrowthStyle, Is.Zero);
            Assert.That(ui.network.config.PaintEdgeRaggedness, Is.EqualTo(.8f), "ORIGINAL preserves the optional profile for later use.");
            Assert.That(ui.network.config.PaintFlowRatio, Is.EqualTo(.6f));
            Assert.That(ui.network.config.PaintGrowthSettings.Style, Is.Zero);
            Assert.That(ui.network.config.PaintGrainEnabled, Is.False);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }
    }
}
