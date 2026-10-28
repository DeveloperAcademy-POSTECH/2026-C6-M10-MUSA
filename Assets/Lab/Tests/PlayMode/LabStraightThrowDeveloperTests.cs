using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    public sealed class LabStraightThrowDeveloperTests
    {
        private LabNetwork testedNetwork;

        [UnityTearDown]
        public IEnumerator CleanUpSession()
        {
            if (testedNetwork != null)
            {
                if (testedNetwork.IsConnected) testedNetwork.Stop();
                Object.Destroy(testedNetwork.gameObject);
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Straight developer test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedDevToggleEnablesStraightControlsBeforeApplyAndDefaultsRestoreArc()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode ui = SavedDeveloperUi();
            testedNetwork = ui.network;
            float[] original = ui.sourceConfig.CaptureDeveloperValues();
            Assert.That(ui.valueInputs.Length, Is.EqualTo(251), "The live DEV panel includes all appended settings, including nearby-hit drying.");
            Assert.That(ui.valueSliders.Length, Is.EqualTo(251), "The live DEV panel includes all appended settings, including nearby-hit drying.");
            Assert.That(ui.sourceConfig.ThrowForm, Is.EqualTo(LabThrowForm.Arc));
            ui.Open();
            Assert.That(ui.panel.activeSelf, Is.True);
            Button category = ui.panel.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Category Throw form");
            category.onClick.Invoke();
            Button toggle = ThrowToggle(ui);
            Assert.That(toggle.gameObject.activeInHierarchy, Is.True);
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ARC"));
            AssertControlsEnabled(ui, false);
            toggle.onClick.Invoke();
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("STRAIGHT"));
            Assert.That(ui.valueInputs[111].text, Is.EqualTo("1"));
            AssertControlsEnabled(ui, true);
            Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(original),
                "Editing the form must not write the saved Inspector asset.");
            Assert.That(ui.network.config.ThrowForm, Is.EqualTo(LabThrowForm.Arc),
                "A draft toggle must not change the running configuration before APPLY.");
            ui.valueSliders[112].value = 1f;
            Assert.That(ui.valueInputs[112].text, Is.EqualTo("5"));
            ui.valueInputs[112].text = "-2.5";
            Assert.That(ui.valueSliders[112].value, Is.EqualTo(.25f).Within(.0001f));
            ui.defaultsButton.onClick.Invoke();
            Assert.That(ui.valueInputs[111].text, Is.EqualTo("0"));
            AssertControlsEnabled(ui, false);
            Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(original));
            ui.Close();
        }

        [UnityTest]
        public IEnumerator DevSoloAppliesRuntimeStraightThenRestoresArcOnRestartWithoutSavingTheAsset()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabDeveloperMode ui = SavedDeveloperUi();
            testedNetwork = ui.network;
            LabNetwork network = ui.network;
            float[] original = ui.sourceConfig.CaptureDeveloperValues();
            network.port = 19190;
            try
            {
                ui.Open();
                ThrowToggle(ui).onClick.Invoke();
                ui.valueInputs[112].text = "0.75";
                ui.valueInputs[113].text = "0.1";
                ui.valueInputs[114].text = "-0.2";
                ui.valueInputs[115].text = "5";
                ui.valueInputs[116].text = "0.2";
                ui.valueInputs[117].text = "0";
                ui.ApplyAndStart();
                Assert.That(network.IsDeveloperSolo, Is.True, ui.feedbackText.text);
                yield return null;
                Assert.That(network.config, Is.Not.SameAs(ui.sourceConfig));
                Assert.That(network.config.ThrowForm, Is.EqualTo(LabThrowForm.Straight));
                Assert.That(network.config.StraightOriginOffset, Is.EqualTo(new Vector3(.75f, .1f, -.2f)));
                Assert.That(network.config.StraightPitchOffsetDegrees, Is.EqualTo(5f));
                Assert.That(network.config.StraightLoftScale, Is.EqualTo(.2f));
                Assert.That(network.config.EffectiveThrowGravity, Is.Zero);
                Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(original));
                string firstSession = network.Snapshot.sessionId;
                ui.Open();
                Assert.That(ui.valueInputs[111].text, Is.EqualTo("1"));
                AssertControlsEnabled(ui, true);
                ThrowToggle(ui).onClick.Invoke();
                AssertControlsEnabled(ui, false);
                ui.ApplyAndStart();
                yield return null;
                Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
                Assert.That(network.config.ThrowForm, Is.EqualTo(LabThrowForm.Arc));
                Assert.That(network.config.EffectiveThrowGravity, Is.EqualTo(network.config.ThrowGravity));
                Assert.That(network.config.StraightOriginOffset, Is.EqualTo(new Vector3(.75f, .1f, -.2f)),
                    "Switching form preserves the draft Straight profile for the next comparison.");
                Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(original));
            }
            finally { if (network.IsConnected) network.Stop(); }
        }

        private static LabDeveloperMode SavedDeveloperUi() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();

        private static Button ThrowToggle(LabDeveloperMode ui) =>
            ui.valueSliders[111].transform.Find("SettingToggle").GetComponent<Button>();

        private static void AssertControlsEnabled(LabDeveloperMode ui, bool enabled)
        {
            for (int i = 112; i <= 117; i++)
            {
                Assert.That(ui.valueInputs[i].readOnly, Is.EqualTo(!enabled), "ReadOnly index " + i);
                Assert.That(ui.valueInputs[i].interactable, Is.EqualTo(enabled), "Input index " + i);
                Assert.That(ui.valueSliders[i].interactable, Is.EqualTo(enabled), "Slider index " + i);
            }
        }
    }
}
