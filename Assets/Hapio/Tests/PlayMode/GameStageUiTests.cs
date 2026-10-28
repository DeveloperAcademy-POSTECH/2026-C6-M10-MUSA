using System.Collections;
using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hapio.Tests
{
    /// <summary>Saved Home controls exercise the real per-scope settings API, without claiming a device/transport test.</summary>
    public sealed class GameStageUiTests
    {
        private GameSession session;
        [UnitySetUp]
        public IEnumerator OpenHome()
        {
            Assert.That(GameSession.Instance, Is.Null);
            yield return SceneManager.LoadSceneAsync(GameScenePaths.Home, LoadSceneMode.Single);
            for (int i = 0; i < 120 && (GameSession.Instance == null || GameSession.Instance.IsLoadingScene); i++) yield return null;
            session = GameSession.Instance;
            Assert.That(session, Is.Not.Null);
            Assert.That(session.IsLoadingScene, Is.False);
            // DEV controls are available on the initial title without playing the opening.
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (session != null)
            {
                session.Network.Stop(); Object.Destroy(session.gameObject);
                yield return null;
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene cleanup = SceneManager.CreateScene("Stage UI cleanup"); SceneManager.SetActiveScene(cleanup);
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedDevTabsApplyOnlyTheirScopeAndKeepStageOffValuesAfterReopening()
        {
            GameScreen screen = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
            screen.developerButton.onClick.Invoke();
            Assert.That(screen.developerPanel.activeSelf, Is.True);
            AssertScope(screen, 0);
            float[] initialYaguang = session.CaptureDeveloperSettings(2);
            int duration = Field("Battle Duration (seconds)");
            screen.developerScopeButtons[1].onClick.Invoke();
            AssertScope(screen, 1);
            screen.developerInputs[209].text = "0"; // The fox's actual unique gimmick switches (#311).
            screen.developerInputs[210].text = "0";
            screen.applyDeveloperButton.onClick.Invoke();
            Assert.That(new[] { session.CaptureDeveloperSettings(1)[209], session.CaptureDeveloperSettings(1)[210] },
                Is.All.EqualTo(0f));
            Assert.That(session.CaptureDeveloperSettings(2), Is.EqualTo(initialYaguang));

            screen.developerScopeButtons[3].onClick.Invoke();
            AssertScope(screen, 3);
            screen.developerInputs[178].text = "2"; // Fire target count is local to Jigwi.
            screen.applyDeveloperButton.onClick.Invoke();
            Assert.That(session.CaptureDeveloperSettings(3)[178], Is.EqualTo(2f));
            float[][] beforeTimeline = Enumerable.Range(0, GameStageSettings.TerritoryScope + 1)
                .Select(session.CaptureDeveloperSettings).ToArray();
            screen.developerScopeButtons[2].onClick.Invoke();
            AssertScope(screen, 2);
            Assert.That(screen.developerSliders, Has.Length.EqualTo(251));
            // Its own battle time (211) is retired (#328); the schedule's end (212) is the first timetable value.
            Assert.That(screen.developerInputs[211].transform.parent.gameObject.activeSelf, Is.False);
            Assert.That(screen.developerSliders[212].minValue, Is.EqualTo(0f));
            Assert.That(screen.developerSliders[212].maxValue, Is.EqualTo(180f));
            foreach (int index in new[] { 205, 169, 170 })
                Assert.That(screen.developerInputs[index].transform.parent.gameObject.activeSelf, Is.False,
                    "The scheduled profile does not expose unused periodic fields: " + index);
            foreach (int index in new[] { 207, 208 })
                Assert.That(screen.developerInputs[index].transform.parent.gameObject.activeSelf, Is.True,
                    "Common-kind switches still control the Yaguang schedule: " + index);
            Assert.That(screen.developerFeedback.text, Does.Contain("시간표"));
            Assert.That(screen.developerFeedback.text, Does.Contain("간격·확률은 사용하지 않습니다"));
            screen.developerSliders[212].value = 110f;
            Assert.That(screen.developerInputs[212].text, Is.EqualTo("110"),
                "The saved slider must drive its actual numeric input.");
            screen.applyDeveloperButton.onClick.Invoke();
            Assert.That(session.CaptureDeveloperSettings(2)[212], Is.EqualTo(110f));
            foreach (int scope in new[] { 0, 1, 3, GameStageSettings.TerritoryScope })
                Assert.That(session.CaptureDeveloperSettings(scope), Is.EqualTo(beforeTimeline[scope]),
                    "Applying the Yaguang timeline cannot change scope " + scope);

            screen.developerScopeButtons[0].onClick.Invoke();
            AssertScope(screen, 0);
            screen.developerInputs[duration].text = "420";
            screen.developerInputs[209].text = screen.developerInputs[210].text = "1";
            screen.developerInputs[212].text = "70"; // Hidden values must never be applied through Common.
            screen.applyDeveloperButton.onClick.Invoke();
            for (int scope = 0; scope <= 3; scope++)
                Assert.That(session.CaptureDeveloperSettings(scope)[duration], Is.EqualTo(420f));
            Assert.That(new[] { session.CaptureDeveloperSettings(1)[209], session.CaptureDeveloperSettings(1)[210] },
                Is.All.EqualTo(0f));
            Assert.That(session.CaptureDeveloperSettings(2)[212], Is.EqualTo(110f));
            screen.closeDeveloperButton.onClick.Invoke(); screen.developerButton.onClick.Invoke();
            screen.developerScopeButtons[1].onClick.Invoke();
            Assert.That(new[] { screen.developerInputs[209].text, screen.developerInputs[210].text }, Is.All.EqualTo("0"));
            screen.developerScopeButtons[2].onClick.Invoke();
            AssertScope(screen, 2);
            Assert.That(screen.developerInputs[212].text, Is.EqualTo("110"));
            Assert.That(screen.developerSliders[212].value, Is.EqualTo(110f));
            screen.developerScopeButtons[3].onClick.Invoke();
            Assert.That(screen.developerInputs[178].text, Is.EqualTo("2"));
            Assert.That(screen.developerScopeLabel.text, Does.Contain(GameStageCatalog.RequireDefault().Get(3).displayName));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedRawCorrectionControlsApplyReopenAndRemainCommonAcrossScopes()
        {
            GameScreen screen = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            string sourceJson = JsonUtility.ToJson(source);
            screen.developerButton.onClick.Invoke();
            AssertScope(screen, 0);
            foreach (int index in new[] { 225, 226, 227 })
            {
                Assert.That(screen.developerInputs[index].gameObject.activeInHierarchy, Is.True);
                Assert.That(screen.developerSliders[index].gameObject.activeInHierarchy, Is.True);
                Assert.That(screen.developerSliders[index].wholeNumbers, Is.True);
                Assert.That(screen.developerSliders[index].minValue, Is.EqualTo(LabConfig.DeveloperFields[index].Min));
                Assert.That(screen.developerSliders[index].maxValue, Is.EqualTo(LabConfig.DeveloperFields[index].Max));
            }
            Assert.That(screen.developerInputs[225].text, Is.EqualTo("1"));
            // Hapio's saved form uses a whole-number 0/1 slider for ON/OFF.
            screen.developerSliders[225].value = 0f;
            screen.developerSliders[226].value = 0f;
            screen.developerSliders[227].value = 1f;
            Assert.That(new[] { screen.developerInputs[225].text, screen.developerInputs[226].text,
                screen.developerInputs[227].text }, Is.EqualTo(new[] { "0", "0", "1" }));
            screen.developerSliders[226].value = 100f;
            screen.developerSliders[227].value = 10f;
            Assert.That(new[] { screen.developerInputs[226].text, screen.developerInputs[227].text },
                Is.EqualTo(new[] { "100", "10" }));
            Assert.That(session.CaptureDeveloperSettings(0)[225], Is.EqualTo(1f), "Moving controls only edits the draft.");
            screen.applyDeveloperButton.onClick.Invoke();
            for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                Assert.That(session.CaptureDeveloperSettings(scope).Skip(225).Take(3).ToArray(), Is.EqualTo(new[] { 0f, 100f, 10f }),
                    "Common generation policy reaches scope " + scope);
            screen.closeDeveloperButton.onClick.Invoke();
            screen.developerButton.onClick.Invoke();
            Assert.That(new[] { screen.developerInputs[225].text, screen.developerInputs[226].text,
                screen.developerInputs[227].text }, Is.EqualTo(new[] { "0", "100", "10" }));
            foreach (int scope in new[] { 1, 2, 3, GameStageSettings.TerritoryScope })
            {
                screen.developerScopeButtons[scope].onClick.Invoke();
                foreach (int index in new[] { 225, 226, 227 })
                    Assert.That(screen.developerInputs[index].transform.parent.gameObject.activeSelf, Is.False,
                        "Common raw policy is hidden in scope " + scope);
                screen.developerInputs[225].text = "1";
                screen.developerInputs[226].text = "1";
                screen.developerInputs[227].text = "1";
                screen.applyDeveloperButton.onClick.Invoke();
                Assert.That(session.CaptureDeveloperSettings(scope).Skip(225).Take(3).ToArray(), Is.EqualTo(new[] { 0f, 100f, 10f }),
                    "A stage form cannot apply hidden raw-policy values.");
            }
            screen.developerScopeButtons[0].onClick.Invoke();
            screen.developerSliders[225].value = 1f;
            screen.developerInputs[226].text = "0";
            screen.developerInputs[227].text = "1";
            Assert.That(screen.developerSliders[226].value, Is.EqualTo(0f));
            Assert.That(screen.developerSliders[227].value, Is.EqualTo(1f));
            screen.applyDeveloperButton.onClick.Invoke();
            screen.closeDeveloperButton.onClick.Invoke();
            screen.developerButton.onClick.Invoke();
            Assert.That(new[] { screen.developerInputs[225].text, screen.developerInputs[226].text,
                screen.developerInputs[227].text }, Is.EqualTo(new[] { "1", "0", "1" }));
            for (int scope = 0; scope <= GameStageSettings.TerritoryScope; scope++)
                Assert.That(session.CaptureDeveloperSettings(scope).Skip(225).Take(3).ToArray(), Is.EqualTo(new[] { 1f, 0f, 1f }));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(sourceJson));
            yield return null;
        }

        private static int Field(string label) => Enumerable.Range(0, LabConfig.DeveloperFields.Count)
            .Single(i => LabConfig.DeveloperFields[i].Label == label);
        private static void AssertScope(GameScreen screen, int scope)
        {
            Assert.That(screen.DeveloperScope, Is.EqualTo(scope));
            Assert.That(screen.developerInputs, Has.Length.EqualTo(251));
            for (int i = 0; i < screen.developerInputs.Length; i++)
                Assert.That(screen.developerInputs[i].transform.parent.gameObject.activeSelf,
                    Is.EqualTo(GameStageSettings.IsFieldVisible(scope, i)), "Field " + i + " in scope " + scope);
        }
    }
}
