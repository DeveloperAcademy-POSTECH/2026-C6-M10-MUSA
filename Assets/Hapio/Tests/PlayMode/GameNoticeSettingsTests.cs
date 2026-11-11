using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>Uses saved Home/Lobby and the real persistent session; no remote peer or Release-player claim.</summary>
    [Category("Slow")]
    public sealed class GameNoticeSettingsTests
    {
        private GameSession session;
        private bool hadPreference;
        private int previousPreference;
#if UNITY_EDITOR
        private bool soloStagePreferenceOverridden;
        private bool hadSoloStagePreference;
        private int previousSoloStagePreference;
#endif

        [UnitySetUp]
        public IEnumerator LoadSavedHomeWithNoPreference()
        {
            hadPreference = PlayerPrefs.HasKey(LabNoticeSettings.PreferenceKey);
            previousPreference = PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey);
            PlayerPrefs.DeleteKey(LabNoticeSettings.PreferenceKey);
            PlayerPrefs.Save();
            Assert.That(GameSession.Instance, Is.Null, "Release the previous persistent session first.");
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            yield return Until(() =>
            {
                // Own the persistent object before the wait can fail, so teardown can release it.
                if (session == null) session = GameSession.Instance;
                return session != null && !session.IsLoadingScene;
            }, "Saved Home must finish its session bootstrap.");
            session.Network.port = AvailablePort();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedSessionAndRestoreLocalPreference()
        {
            try
            {
                Time.timeScale = 1f;
                if (session != null)
                {
                    session.Network.Stop();
                    Object.Destroy(session.gameObject);
                    yield return null;
                }
                Scene previous = SceneManager.GetActiveScene();
                Scene cleanup = SceneManager.CreateScene("Notice setting game fixture cleanup");
                SceneManager.SetActiveScene(cleanup);
                if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
                session = null;
                yield return null;
            }
            finally
            {
#if UNITY_EDITOR
                if (soloStagePreferenceOverridden)
                {
                    if (hadSoloStagePreference)
                        UnityEditor.EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, previousSoloStagePreference);
                    else UnityEditor.EditorPrefs.DeleteKey(GameSession.DeveloperSoloStartStageKey);
                    soloStagePreferenceOverridden = false;
                }
#endif
                if (hadPreference) PlayerPrefs.SetInt(LabNoticeSettings.PreferenceKey, previousPreference);
                else PlayerPrefs.DeleteKey(LabNoticeSettings.PreferenceKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator CommonDevLocalNoticePreferenceSurvivesPanelAndHomeToLobbyTransition()
        {
            var home = Object.FindFirstObjectByType<GameScreen>();
            Assert.That(home, Is.Not.Null);
            home.OpenDeveloper();
            Assert.That(home.developerPanel.activeInHierarchy, Is.True);
            var homeRow = home.developerPanel.GetComponentsInChildren<LabNoticeSettingsRow>(true).Single();
            Assert.That(homeRow.gameObject.activeInHierarchy, Is.True);
            Assert.That(homeRow.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(homeRow.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("OFF"));
            Assert.That(home.developerInputs.Length, Is.EqualTo(261));
            float[][] sharedBefore = CaptureSharedSettings();

            homeRow.ToggleButton.onClick.Invoke();
            Assert.That(LabNoticeSettings.Enabled, Is.True);
            Assert.That(PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey), Is.EqualTo(1));
            home.closeDeveloperButton.onClick.Invoke();
            home.OpenDeveloper();
            Assert.That(homeRow.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("ON"));
            home.SelectDeveloperScope(1);
            Assert.That(homeRow.gameObject.activeSelf, Is.False, "Local preferences belong to the Common tab only.");
            home.SelectDeveloperScope(0);
            Assert.That(homeRow.gameObject.activeInHierarchy, Is.True);
            AssertSharedSettingsEqual(sharedBefore);
            home.closeDeveloperButton.onClick.Invoke();

            session.Host();
            yield return Until(() => session.State.phase == GameFlowPhase.Lobby && !session.IsLoadingScene
                && SceneManager.GetActiveScene().path == GameScenePaths.Lobby && session.Network.IsHost,
                "The real Host flow must load the saved Lobby.");
            yield return null;
            Assert.That(homeRow == null, Is.True, "The Home row must leave with its Scene.");
            var lobby = Object.FindFirstObjectByType<GameScreen>();
            lobby.OpenDeveloper();
            Assert.That(lobby.developerPanel.activeInHierarchy, Is.True);
            var lobbyRow = lobby.developerPanel.GetComponentsInChildren<LabNoticeSettingsRow>(true).Single();
            Assert.That(lobbyRow.gameObject.activeInHierarchy, Is.True);
            Assert.That(lobbyRow.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("ON"),
                "A new Scene's DEV row reads the saved local preference.");
            Assert.That(lobby.developerInputs.Length, Is.EqualTo(261));
            Assert.That(lobby.developerSliders.Length, Is.EqualTo(261));
            float[][] lobbySharedBefore = CaptureSharedSettings();
            lobbyRow.ToggleButton.onClick.Invoke();
            Assert.That(LabNoticeSettings.Enabled, Is.False);
            Assert.That(PlayerPrefs.GetInt(LabNoticeSettings.PreferenceKey), Is.Zero);
            AssertSharedSettingsEqual(lobbySharedBefore);
        }

        [UnityTest]
        public IEnumerator HomeLocalNoticePreferenceReachesSoloBattleAndBattleDevCanHideCurrentNotice()
        {
#if UNITY_EDITOR
            hadSoloStagePreference = UnityEditor.EditorPrefs.HasKey(GameSession.DeveloperSoloStartStageKey);
            previousSoloStagePreference = UnityEditor.EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 1);
            soloStagePreferenceOverridden = true;
            UnityEditor.EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, 1);
#endif
            GameTestStartTiming.UseShort(); // Keep the real preparation/countdown path, with the assembly's normal test durations.
            var home = Object.FindFirstObjectByType<GameScreen>();
            home.OpenDeveloper();
            var homeRow = home.developerPanel.GetComponentsInChildren<LabNoticeSettingsRow>(true).Single();
            homeRow.ToggleButton.onClick.Invoke();
            Assert.That(LabNoticeSettings.Enabled, Is.True);
            float[][] sharedBefore = CaptureSharedSettings();
            GameSession originalSession = session;
            LabNetwork originalNetwork = session.Network;

            session.StartSolo(GameMode.Story);
            yield return Until(() => session.State.phase == GameFlowPhase.Playing && !session.IsLoadingScene
                && !session.IsLoadingScreenUp && !session.IsPreparingBattle && !session.Network.GameStartHeld
                && SceneManager.GetActiveScene().path == GameScenePaths.Battle,
                "SOLO must finish real preparation and countdown in the saved Battle.");
            yield return null;
            Assert.That(GameSession.Instance, Is.SameAs(originalSession));
            Assert.That(session.Network, Is.SameAs(originalNetwork));
            Assert.That(homeRow == null, Is.True, "The Home row does not survive as a stray overlay.");
            var bindings = Object.FindFirstObjectByType<GameBattleBindings>();
            Assert.That(bindings, Is.Not.Null);
            LabHud hud = bindings.hud;
            Assert.That(hud.network, Is.SameAs(originalNetwork));
            Assert.That(bindings.developerMode.network, Is.SameAs(originalNetwork));
            Assert.That(hud.noticeText.gameObject.activeInHierarchy, Is.True);

            // A real rejected request populates diagnostics without changing the running battle.
            Assert.That(originalNetwork.ConfigureHostSession(originalNetwork.config, 0), Is.False);
            string notice = originalNetwork.Notice;
            Assert.That(notice, Is.Not.Empty);
            yield return null;
            Assert.That(hud.noticeText.text, Is.EqualTo(notice));

            var target = bindings.GetComponent<GameDeveloperTapTarget>();
            Assert.That(target, Is.Not.Null, "Use Battle's installed hidden DEV entry, not a synthetic panel.");
            for (int tap = 0; tap < 3; tap++)
                ExecuteEvents.Execute(target.TapArea, new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(originalNetwork.IsBattlePaused, Is.True);
            Assert.That(bindings.developerMode.panel.activeInHierarchy, Is.True);
            var battleRow = bindings.developerMode.panel.GetComponentsInChildren<LabNoticeSettingsRow>(true).Single();
            Assert.That(battleRow.ToggleButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("ON"));
            string acceptedConfig = JsonUtility.ToJson(originalNetwork.config);
            Assert.That(originalNetwork.ConfigureHostSession(originalNetwork.config, 0), Is.False);
            notice = originalNetwork.Notice;
            battleRow.ToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(LabNoticeSettings.Enabled, Is.False);
            Assert.That(hud.noticeText.gameObject.activeSelf, Is.False);
            Assert.That(hud.noticeText.text, Is.EqualTo(notice), "Hiding the rendered notice must not discard its text.");
            Assert.That(originalNetwork.Notice, Is.EqualTo(notice));
            Assert.That(JsonUtility.ToJson(originalNetwork.config), Is.EqualTo(acceptedConfig));
            AssertSharedSettingsEqual(sharedBefore);
        }

        private float[][] CaptureSharedSettings() => Enumerable.Range(0, GameStageSettings.TerritoryScope + 1)
            .Select(scope => session.CaptureDeveloperSettings(scope)).ToArray();

        private void AssertSharedSettingsEqual(float[][] expected)
        {
            for (int scope = 0; scope < expected.Length; scope++)
                Assert.That(session.CaptureDeveloperSettings(scope), Is.EqualTo(expected[scope]),
                    "Toggling local NoticeText must not edit shared DEV scope " + scope + ".");
        }

        private static IEnumerator Until(Func<bool> predicate, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private static ushort AvailablePort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            {
                                ipv6.DualMode = false;
                                ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
                            }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        // Retry only the free-port probe; production Host gets one real bind attempt.
                    }
                }
            }
            throw new InvalidOperationException("No free IPv4/IPv6 UDP test port was found.");
        }
    }
}
