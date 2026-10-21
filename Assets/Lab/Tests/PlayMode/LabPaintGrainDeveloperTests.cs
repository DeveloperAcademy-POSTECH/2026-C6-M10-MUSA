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
    /// <summary>Saved Scene UI and local runtime-copy flow; no claim of a remote Client ACK.</summary>
    public sealed class LabPaintGrainDeveloperTests
    {
        private LabDeveloperMode ui;
        private string sourceJson;

        [UnitySetUp]
        public IEnumerator LoadSavedPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            ui.allowOfflineEditorTuning = true; // Offline Editor fixture.
            sourceJson = JsonUtility.ToJson(ui.sourceConfig);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(166), "The live DEV panel includes all appended settings, including nearby-hit drying.");
            Assert.That(ui.valueSliders.Length, Is.EqualTo(166), "The live DEV panel includes all appended settings, including nearby-hit drying.");
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (ui != null && ui.network != null)
            {
                ui.network.Stop();
                Object.Destroy(ui.network.gameObject);
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Paint grain DEV cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator GourdToggleAndThreeSlidersAreAccessibleWithoutChangingTheSourceAsset()
        {
            ui.Open();
            Assert.That(ui.panel.activeSelf, Is.True);
            Button category = ui.panel.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Category Gourd");
            for (int i = 128; i <= 131; i++)
                Assert.That(ui.valueInputs[i].transform.parent.gameObject.activeSelf, Is.False);
            category.onClick.Invoke();
            Transform toggleRow = ui.valueInputs[128].transform.parent;
            Button toggle = toggleRow.Find("SettingSlider/SettingToggle").GetComponent<Button>();
            Assert.That(toggleRow.gameObject.activeInHierarchy, Is.True);
            Assert.That(ui.valueInputs[128].gameObject.activeSelf, Is.False,
                "The enable field must appear as a real ON/OFF control, not a numeric slider.");
            Assert.That(toggle.interactable, Is.True);
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("OFF"));
            toggle.onClick.Invoke();
            Assert.That(ui.valueInputs[128].text, Is.EqualTo("1"));
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ON"));

            float[] profile = { 2f, .6f, .4f };
            for (int i = 129; i <= 131; i++)
            {
                var field = LabConfig.DeveloperFields[i];
                Assert.That(ui.valueInputs[i].gameObject.activeInHierarchy, Is.True);
                Assert.That(ui.valueInputs[i].interactable && !ui.valueInputs[i].readOnly, Is.True);
                Assert.That(ui.valueSliders[i].interactable, Is.True);
                ui.valueInputs[i].text = profile[i - 129].ToString(CultureInfo.InvariantCulture);
                Assert.That(ui.valueSliders[i].value,
                    Is.EqualTo(Mathf.InverseLerp(field.Min, field.Max, profile[i - 129])).Within(.0001f));
                ui.valueSliders[i].value = .75f;
                Assert.That(float.Parse(ui.valueInputs[i].text, CultureInfo.InvariantCulture),
                    Is.EqualTo(Mathf.Lerp(field.Min, field.Max, .75f)).Within(.0001f));
            }

            ScrollRect scroll = ui.panel.GetComponentsInChildren<ScrollRect>(true).Single();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 0f;
            scroll.StopMovement();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var input = (RectTransform)ui.valueInputs[131].transform;
            // More Gourd settings can follow the grain group; scroll to this input rather
            // than assuming that the grain group remains the content's final rows.
            Vector3 rowCenter = scroll.viewport.InverseTransformPoint(input.TransformPoint(input.rect.center));
            scroll.content.anchoredPosition += Vector2.up * (scroll.viewport.rect.center.y - rowCenter.y);
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            Vector3 center = scroll.viewport.InverseTransformPoint(input.TransformPoint(input.rect.center));
            Assert.That(scroll.viewport.rect.Contains(new Vector2(center.x, center.y)), Is.True,
                "The final grain slider and input must be reachable after scrolling.");
            Canvas canvas = ui.panel.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, input.TransformPoint(input.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform == input || hits[0].gameObject.transform.IsChildOf(input), Is.True);
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            Assert.That(ui.network.config.PaintGrainEnabled, Is.False, "A draft is not the accepted runtime config.");
            ui.Close();
            ui.Open();
            yield return null;
            Assert.That(ui.valueInputs[128].text, Is.EqualTo("0"));
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("OFF"));
        }

        [UnityTest]
        public IEnumerator SoloRestartAppliesAndDisablesTheAcceptedGrainProfileWithoutSavingTheAsset()
        {
            ui.network.port = 19196;
            ui.Open();
            ui.valueInputs[128].text = "1";
            ui.valueInputs[129].text = "2";
            ui.valueInputs[130].text = "0.6";
            ui.valueInputs[131].text = "0.4";
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(ui.network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            Assert.That(ui.network.config, Is.Not.SameAs(ui.sourceConfig));
            Assert.That(ui.network.config.PaintGrainEnabled, Is.True);
            Assert.That(ui.network.config.PaintGrainSize, Is.EqualTo(2f));
            Assert.That(ui.network.config.PaintGrainStrength, Is.EqualTo(.6f));
            Assert.That(ui.network.config.PaintMottleStrength, Is.EqualTo(.4f));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
            ui.Open();
            ui.valueInputs[128].text = "0";
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(ui.network.config.PaintGrainEnabled, Is.False);
            Assert.That(ui.network.config.PaintGrainSize, Is.EqualTo(2f), "Disabling preserves the adjustable profile.");
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(sourceJson));
        }
    }
}
