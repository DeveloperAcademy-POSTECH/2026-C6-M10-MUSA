using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>저장 Scene DEV 패널의 야광귀 묶음 (#268): 한꺼번에 켜기 버튼과 콤보·섬광 구분선.</summary>
    public sealed class LabYaguangDeveloperTests
    {
        private LabDeveloperMode ui;

        [UnitySetUp]
        public IEnumerator LoadSavedPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            Assert.That(ui.valueInputs.Length, Is.EqualTo(204));
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
            Scene cleanup = SceneManager.CreateScene("Yaguang DEV cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator OneSwitchTurnsTheComboAndTheFlashOnAndOffTogether()
        {
            ui.Open();
            Button header = ui.panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Category 야광귀");
            Button switchButton = header.transform.Find("CategorySwitch").GetComponent<Button>();
            Text label = switchButton.GetComponentInChildren<Text>();
            Assert.That(switchButton.gameObject.activeInHierarchy, Is.True, "Shown on the folded header.");
            Assert.That(new[] { ui.valueInputs[155].text, ui.valueInputs[184].text }, Is.EqualTo(new[] { "0", "0" }));
            Assert.That(label.text, Is.EqualTo("끔"));

            switchButton.onClick.Invoke();
            Assert.That(new[] { ui.valueInputs[155].text, ui.valueInputs[184].text }, Is.EqualTo(new[] { "1", "1" }));
            Assert.That(label.text, Is.EqualTo("켬"));

            ui.valueInputs[184].text = "0";
            yield return null;
            Assert.That(label.text, Is.EqualTo("일부"), "Only the combo is on.");
            switchButton.onClick.Invoke();
            Assert.That(new[] { ui.valueInputs[155].text, ui.valueInputs[184].text }, Is.EqualTo(new[] { "1", "1" }),
                "From a mix, the switch turns both on.");
            switchButton.onClick.Invoke();
            Assert.That(new[] { ui.valueInputs[155].text, ui.valueInputs[184].text }, Is.EqualTo(new[] { "0", "0" }));

            Transform content = header.transform.parent;
            Transform combo = content.Find("Divider ── 콤보 ──");
            Transform flash = content.Find("Divider ── 섬광 ──");
            Assert.That(combo, Is.Not.Null);
            Assert.That(flash, Is.Not.Null);
            Assert.That(combo.gameObject.activeSelf, Is.False, "Folded with the group.");
            header.onClick.Invoke();
            Assert.That(new[] { combo.gameObject.activeSelf, flash.gameObject.activeSelf }, Is.All.True);
            Transform comboRow = ui.valueInputs[155].transform.parent;
            Transform flashRow = ui.valueInputs[184].transform.parent;
            Assert.That(combo.GetSiblingIndex(), Is.EqualTo(header.transform.GetSiblingIndex() + 1));
            Assert.That(comboRow.GetSiblingIndex(), Is.EqualTo(combo.GetSiblingIndex() + 1));
            Assert.That(flashRow.GetSiblingIndex(), Is.EqualTo(flash.GetSiblingIndex() + 1));
            Assert.That(combo.GetComponent<Text>().raycastTarget, Is.False);
        }
    }
}
