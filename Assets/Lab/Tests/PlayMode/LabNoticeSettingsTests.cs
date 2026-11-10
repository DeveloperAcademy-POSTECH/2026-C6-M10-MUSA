using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>Saved HUD/DEV binding coverage. Release compile-gating requires a separate player build.</summary>
    public sealed class LabNoticeSettingsTests
    {
        private readonly LabSavedSceneNetworkFixture fixture = new LabSavedSceneNetworkFixture();
        private LabHud hud;
        private LabDeveloperMode developer;
        private bool hadPreference;
        private int previousPreference;

        [UnitySetUp]
        public IEnumerator LoadSavedHudWithNoPreference()
        {
            hadPreference = PlayerPrefs.HasKey(LabNoticeSettings.PreferenceKey);
            previousPreference = PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey);
            PlayerPrefs.DeleteKey(LabNoticeSettings.PreferenceKey);
            PlayerPrefs.Save();
            yield return fixture.Load();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            hud = roots.SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
            developer = roots.SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            yield return null; // Finish the saved HUD's one-shot automatic-search startup before emitting a notice.
        }

        [UnityTearDown]
        public IEnumerator ReleaseSceneAndRestoreLocalPreference()
        {
            try
            {
                Time.timeScale = 1f;
                yield return fixture.Cleanup();
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetInt(LabNoticeSettings.PreferenceKey, previousPreference);
                else PlayerPrefs.DeleteKey(LabNoticeSettings.PreferenceKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator MissingPreferenceHidesNoticeWithoutDiscardingCurrentNetworkMessage()
        {
            Assert.That(LabNoticeSettings.Enabled, Is.False);
            Assert.That(hud.noticeText, Is.Not.Null, "Keep the saved NoticeText reference.");
            Assert.That(hud.noticeText.gameObject.activeSelf, Is.False);

            // A real rejected setup action supplies a notice without starting transport or altering config.
            Assert.That(hud.network.ConfigureHostSession(developer.sourceConfig, 0), Is.False);
            string notice = hud.network.Notice;
            Assert.That(notice, Is.Not.Empty);
            yield return null;

            Assert.That(hud.noticeText.text, Is.EqualTo(notice), "Diagnostic text still receives network updates.");
            Assert.That(hud.noticeText.gameObject.activeSelf, Is.False, "Populating a notice must not show it.");
            Assert.That(PlayerPrefs.HasKey(LabNoticeSettings.PreferenceKey), Is.False,
                "Reading the default must not create a saved preference.");
        }

        [UnityTest]
        public IEnumerator LocalDevButtonTogglesCurrentNoticeImmediatelyAndReopensWithoutChangingSharedSettings()
        {
            Assert.That(LabNoticeSettings.AvailableInThisBuild, Is.True,
                "This test exercises Editor/Development UI; it does not certify a Release player.");
            string savedConfig = JsonUtility.ToJson(developer.sourceConfig);
            string sessionConfig = JsonUtility.ToJson(hud.network.config);
            float[] sharedValues = developer.sourceConfig.CaptureDeveloperValues();
            Assert.That(sharedValues.Length, Is.EqualTo(259));
            developer.Open();
            var row = developer.panel.GetComponentsInChildren<LabNoticeSettingsRow>(true).Single();
            Assert.That(row.name, Is.EqualTo(LabNoticeSettingsRow.RowName));
            Assert.That(row.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(row.ToggleButton.interactable, Is.True);
            Assert.That(row.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("OFF"));

            Assert.That(hud.network.ConfigureHostSession(developer.sourceConfig, 0), Is.False);
            string notice = hud.network.Notice;
            row.ToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(LabNoticeSettings.Enabled, Is.True);
            Assert.That(PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey), Is.EqualTo(1));
            Assert.That(hud.noticeText.gameObject.activeSelf, Is.True);
            Assert.That(hud.noticeText.text, Is.EqualTo(notice));
            Assert.That(row.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("ON"));

            developer.Close();
            developer.Open();
            Assert.That(row.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("ON"));
            row.ToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(LabNoticeSettings.Enabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey), Is.Zero);
            Assert.That(hud.noticeText.gameObject.activeSelf, Is.False);
            Assert.That(hud.noticeText.text, Is.EqualTo(notice));
            developer.Close();
            developer.Open();
            Assert.That(row.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("OFF"));

            Assert.That(developer.valueInputs.Length, Is.EqualTo(259));
            Assert.That(developer.valueSliders.Length, Is.EqualTo(259));
            Assert.That(developer.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(sharedValues));
            Assert.That(JsonUtility.ToJson(developer.sourceConfig), Is.EqualTo(savedConfig));
            Assert.That(JsonUtility.ToJson(hud.network.config), Is.EqualTo(sessionConfig),
                "Local display changes must not edit a Host/shared config or require APPLY.");
        }
    }
}
