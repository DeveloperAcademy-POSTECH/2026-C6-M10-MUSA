using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Exercise the saved DEV panel and real local Host paths. The lobby test has no Client:
    /// it does not claim remote ACK, shared paused restart, or multi-device synchronization coverage.
    /// </summary>
    public sealed class LabGourdRopeDeveloperTests
    {
        private const int First = 118;
        private const int Count = 10;
        private static readonly float[] ProfileA = { .8f, 1.6f, .5f, 30f, 1.2f, 1.1f, 9f, 50f, 12f, .8f };
        private static readonly float[] ProfileB = { .2f, 4f, .15f, 65f, 5f, .3f, 4f, 25f, 25f, 3f };
        private LabDeveloperMode ui;
        private LabNetwork testedNetwork;
        private float[] savedValues;
        private string savedJson;

        [UnitySetUp]
        public IEnumerator LoadSavedPanel()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            ui = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabDeveloperMode>(true)).Single();
            testedNetwork = ui.network;
            Assert.That(testedNetwork, Is.Not.Null);
            Assert.That(ui.valueInputs.Length, Is.EqualTo(228), "The live DEV panel includes all appended settings, including nearby-hit drying.");
            Assert.That(ui.valueSliders.Length, Is.EqualTo(228), "The live DEV panel includes all appended settings, including nearby-hit drying.");
            savedValues = ui.sourceConfig.CaptureDeveloperValues();
            savedJson = JsonUtility.ToJson(ui.sourceConfig);
        }

        [UnityTearDown]
        public IEnumerator CleanUpSession()
        {
            if (testedNetwork != null)
            {
                testedNetwork.Stop();
                Object.Destroy(testedNetwork.gameObject);
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            Scene cleanup = SceneManager.CreateScene("Gourd DEV test cleanup");
            SceneManager.SetActiveScene(cleanup);
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator AllTenGourdRowsHaveTwoWayControlsAndTheLastInputIsReachableByScrolling()
        {
            ui.toggleButton.onClick.Invoke();
            Assert.That(ui.panel.activeSelf, Is.True);
            UnityEngine.UI.Button category = GourdCategory();
            for (int i = First; i < First + Count; i++)
                Assert.That(ui.valueInputs[i].transform.parent.gameObject.activeSelf, Is.False,
                    "New Gourd settings must follow the category's initial collapsed state.");
            category.onClick.Invoke();
            for (int offset = 0; offset < Count; offset++)
            {
                int index = First + offset;
                var field = LabConfig.DeveloperFields[index];
                Transform row = ui.valueInputs[index].transform.parent;
                Assert.That(row.gameObject.activeInHierarchy, Is.True, field.Label);
                Assert.That(row.GetSiblingIndex(), Is.GreaterThan(category.transform.GetSiblingIndex()));
                Assert.That(row.Find("SettingLabel").GetComponent<UnityEngine.UI.Text>().text,
                    Is.EqualTo(field.Label));
                Assert.That(ui.valueInputs[index].interactable, Is.True, field.Label);
                Assert.That(ui.valueInputs[index].readOnly, Is.False, field.Label);
                Assert.That(ui.valueSliders[index].interactable, Is.True, field.Label);

                ui.valueInputs[index].text = ProfileA[offset].ToString(CultureInfo.InvariantCulture);
                // All ten ranges are linear. Verify the listener, independently of the slider helper.
                Assert.That(ui.valueSliders[index].value,
                    Is.EqualTo((ProfileA[offset] - field.Min) / (field.Max - field.Min)).Within(.0001f), field.Label);
                ui.valueSliders[index].value = .25f;
                Assert.That(ReadInput(index), Is.EqualTo(field.Min + (field.Max - field.Min) * .25f)
                    .Within(.0001f), field.Label);
            }

            UnityEngine.UI.ScrollRect scroll = ui.panel.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Single();
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(ui.valueInputs[127].transform.parent.parent));
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            Rebuild(scroll);
            scroll.verticalNormalizedPosition = 1f;
            scroll.StopMovement();
            if (scroll.content.rect.height > scroll.viewport.rect.height)
            {
                Assert.That(RowCenterIsVisible(scroll, 127), Is.False,
                    "This layout needs scrolling to reach the final new setting.");
                Assert.That(EventSystem.current, Is.Not.Null);
                scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1000f) });
                yield return null;
                scroll.StopMovement();
            }
            Rebuild(scroll);
            // The rope group is followed by newer Gourd controls. Bring the tested rope
            // input into view explicitly instead of relying on it being the final row.
            var lastRopeInput = (RectTransform)ui.valueInputs[127].transform;
            Vector3 ropeCenter = scroll.viewport.InverseTransformPoint(lastRopeInput.TransformPoint(lastRopeInput.rect.center));
            scroll.content.anchoredPosition += Vector2.up * (scroll.viewport.rect.center.y - ropeCenter.y);
            scroll.StopMovement();
            Rebuild(scroll);
            Assert.That(RowCenterIsVisible(scroll, 127), Is.True,
                "The Gourd expansion must resize Content so the last row can enter the masked viewport.");
            AssertInputReceivesRaycast(127);
            AssertSourceUnchanged();
            Assert.That(testedNetwork.config.CaptureDeveloperValues(), Is.EqualTo(savedValues),
                "Moving controls only edits the form until APPLY.");

            category.onClick.Invoke();
            for (int i = First; i < First + Count; i++)
                Assert.That(ui.valueInputs[i].transform.parent.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingReopeningAndDefaultsReloadApprovedValuesWithoutDuplicatingOrSavingRows()
        {
            ui.Open();
            GourdCategory().onClick.Invoke();
            Transform[] rows = ui.valueInputs.Select(input => input.transform.parent).ToArray();
            int childCount = rows[0].parent.childCount;
            SetForm(ProfileA);
            ui.closeButton.onClick.Invoke();
            Assert.That(ui.panel.activeSelf, Is.False);
            Assert.That(testedNetwork.config.CaptureDeveloperValues(), Is.EqualTo(savedValues));
            ui.toggleButton.onClick.Invoke();
            Assert.That(ui.panel.activeSelf, Is.True);
            AssertForm(savedValues.Skip(First).Take(Count).ToArray());
            Assert.That(rows[0].parent.childCount, Is.EqualTo(childCount),
                "Reopening must not create another category or another set of setting rows.");
            for (int i = 0; i < rows.Length; i++)
                Assert.That(ui.valueInputs[i].transform.parent, Is.SameAs(rows[i]));

            SetForm(ProfileB);
            ui.defaultsButton.onClick.Invoke();
            AssertForm(savedValues.Skip(First).Take(Count).ToArray());
            for (int i = First; i < First + Count; i++)
            {
                var field = LabConfig.DeveloperFields[i];
                Assert.That(ui.valueSliders[i].value,
                    Is.EqualTo((savedValues[i] - field.Min) / (field.Max - field.Min)).Within(.0001f), field.Label);
            }
            Assert.That(testedNetwork.config.CaptureDeveloperValues(), Is.EqualTo(savedValues),
                "DEFAULTS fills the form; it does not apply settings or begin a session.");
            Assert.That(testedNetwork.Snapshot, Is.Null);
            AssertSourceUnchanged();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoloButtonsApplyAllTenValuesAndRestartWithANewProfileWithoutEditingTheSource()
        {
            testedNetwork.port = 19194;
            ui.Open();
            SetForm(ProfileA);
            ui.applyStartButton.onClick.Invoke();
            Assert.That(testedNetwork.IsDeveloperSolo, Is.True, ui.feedbackText.text);
            Assert.That(ui.panel.activeSelf, Is.False);
            yield return null;
            AssertRuntime(ProfileA);
            AssertSourceUnchanged();
            string firstSession = testedNetwork.Snapshot.sessionId;

            ui.Open();
            Assert.That(ui.applyStartLabel.text, Is.EqualTo("RESTART SOLO"));
            AssertForm(ProfileA);
            SetForm(ProfileB);
            AssertRuntime(ProfileA);
            Assert.That(testedNetwork.Snapshot.sessionId, Is.EqualTo(firstSession));
            ui.applyStartButton.onClick.Invoke();
            yield return null;
            Assert.That(testedNetwork.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
            Assert.That(testedNetwork.Snapshot.round, Is.EqualTo(1u));
            AssertRuntime(ProfileB);
            AssertSourceUnchanged();

            ui.Open();
            AssertForm(ProfileB);
            ui.endSoloButton.onClick.Invoke();
            yield return null;
            Assert.That(testedNetwork.IsDeveloperSolo, Is.False);
            Assert.That(testedNetwork.config, Is.SameAs(ui.sourceConfig));
            AssertSourceUnchanged();
        }

        [UnityTest]
        public IEnumerator HostLobbyButtonsRetuneAllTenValuesButCannotBypassParticipantsOrPauseRequirements()
        {
            testedNetwork.port = 19195;
            ui.Open();
            ui.targetParticipantsSlider.value = 2f;
            SetForm(ProfileA);
            ui.applyHostButton.onClick.Invoke();
            Assert.That(testedNetwork.IsHost, Is.True, ui.feedbackText.text);
            Assert.That(testedNetwork.IsDeveloperSolo, Is.False);
            yield return null;
            Assert.That(testedNetwork.TargetParticipantCount, Is.EqualTo(2));
            Assert.That(testedNetwork.ParticipantCount, Is.EqualTo(1),
                "This uses the actual local Host lobby; it does not create a synthetic Client.");
            Assert.That(testedNetwork.Snapshot, Is.Null);
            AssertRuntime(ProfileA);

            ui.Open();
            Assert.That(ui.applyHostLabel.text, Is.EqualTo("APPLY TO ROOM"));
            AssertForm(ProfileA);
            SetForm(ProfileB);
            ui.valueInputs[127].text = "21";
            ui.applyHostButton.onClick.Invoke();
            Assert.That(ui.panel.activeSelf, Is.True);
            Assert.That(ui.feedbackText.text, Does.Contain("Gourd Swing Damping"));
            AssertRuntime(ProfileA);
            ui.valueInputs[127].text = ProfileB[9].ToString(CultureInfo.InvariantCulture);
            ui.applyHostButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.panel.activeSelf, Is.False);
            AssertRuntime(ProfileB);
            Assert.That(testedNetwork.CanStartBattle, Is.False);
            testedNetwork.StartBattle();
            Assert.That(testedNetwork.Snapshot, Is.Null,
                "Retuning cannot skip the missing participant and current settings confirmation gates.");
            Assert.That(testedNetwork.CanRestartSharedBattle, Is.False);
            Assert.That(testedNetwork.RestartBattleWithDeveloperConfig(ui.sourceConfig), Is.False,
                "The shared APPLY & RESTART path requires an actual paused battle.");
            AssertRuntime(ProfileB);
            AssertSourceUnchanged();
        }

        private UnityEngine.UI.Button GourdCategory() => ui.panel.GetComponentsInChildren<UnityEngine.UI.Button>(true)
            .Single(button => button.name == "Category Gourd");

        private void SetForm(float[] values)
        {
            for (int i = 0; i < Count; i++)
                ui.valueInputs[First + i].text = values[i].ToString(CultureInfo.InvariantCulture);
        }

        private float ReadInput(int index) => float.Parse(ui.valueInputs[index].text, CultureInfo.InvariantCulture);

        private void AssertForm(float[] expected)
        {
            for (int i = 0; i < Count; i++)
                Assert.That(ReadInput(First + i), Is.EqualTo(expected[i]).Within(.0001f), LabConfig.DeveloperFields[First + i].Label);
        }

        private void AssertRuntime(float[] expected)
        {
            Assert.That(testedNetwork.config, Is.Not.SameAs(ui.sourceConfig));
            float[] actual = testedNetwork.config.CaptureDeveloperValues();
            for (int i = 0; i < Count; i++)
                Assert.That(actual[First + i], Is.EqualTo(expected[i]).Within(.0001f), LabConfig.DeveloperFields[First + i].Label);
            LabHangingTarget target = testedNetwork.target.GetComponent<LabHangingTarget>();
            Assert.That(target, Is.Not.Null);
            float[] targetValues = { target.CycleAmplitude, target.CyclePeriod, target.BounceStrength,
                target.BounceReturnStrength, target.BounceDamping, target.MaximumExtension,
                target.ImpactStrength, target.MaximumAngle, target.ReturnStrength, target.SwingDamping };
            for (int i = 0; i < Count; i++)
                Assert.That(targetValues[i], Is.EqualTo(expected[i]).Within(.0001f), "Target " + LabConfig.DeveloperFields[First + i].Label);
            Assert.That(ui.sceneController.config, Is.SameAs(testedNetwork.config));
            Assert.That(ui.seatCamera.config, Is.SameAs(testedNetwork.config));
        }

        private void AssertSourceUnchanged()
        {
            Assert.That(ui.sourceConfig.CaptureDeveloperValues(), Is.EqualTo(savedValues));
            Assert.That(JsonUtility.ToJson(ui.sourceConfig), Is.EqualTo(savedJson),
                "Neither draft edits, DEFAULTS nor an applied runtime copy may write any source field.");
        }

        private static void Rebuild(UnityEngine.UI.ScrollRect scroll)
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
        }

        private bool RowCenterIsVisible(UnityEngine.UI.ScrollRect scroll, int index)
        {
            var row = (RectTransform)ui.valueInputs[index].transform.parent;
            Vector3 point = scroll.viewport.InverseTransformPoint(row.TransformPoint(row.rect.center));
            return scroll.viewport.rect.Contains(new Vector2(point.x, point.y));
        }

        private void AssertInputReceivesRaycast(int index)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            var input = (RectTransform)ui.valueInputs[index].transform;
            Canvas canvas = ui.panel.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, input.TransformPoint(input.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Transform firstHit = hits[0].gameObject.transform;
            Assert.That(firstHit == input || firstHit.IsChildOf(input), Is.True,
                "After scrolling, the last numeric input must be reachable through the real Canvas raycaster.");
        }
    }
}
