using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// Saved DEV controls and actual gourd/HUD/result-viewer binding with fake-clock Host maps.
    /// The map display commit is invoked explicitly; these are not remote-network or collision tests.
    /// </summary>
    public sealed class LabPaintDryingPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LabDeveloperMode developer;
        private LabNetwork network;
        private LabGourdPaint paint;
        private LabHud hud;
        private string sourceJson;
        private LabConfig fixture;

        [UnitySetUp]
        public IEnumerator LoadSavedScene()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            developer = Object.FindFirstObjectByType<LabDeveloperMode>();
            network = developer.network;
            paint = Object.FindFirstObjectByType<LabGourdPaint>();
            hud = Object.FindFirstObjectByType<LabHud>();
            sourceJson = JsonUtility.ToJson(developer.sourceConfig);
            network.port = 19325;
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (network != null)
            {
                network.Stop(); Object.Destroy(network.gameObject);
                yield return null;
            }
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Paint drying cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (fixture != null) Object.Destroy(fixture);
        }

        [UnityTest]
        public IEnumerator GourdToggleAndThreeSlidersApplyToSoloRestartWithoutEditingTheSource()
        {
            Assert.That(developer.valueInputs.Length, Is.EqualTo(253));
            Assert.That(developer.valueSliders.Length, Is.EqualTo(253));
            Assert.That(network.config.PaintDryingEnabled, Is.True, "Fresh game sessions enable drying by default.");
            developer.Open();
            developer.panel.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Category Gourd").onClick.Invoke();
            Transform toggleRow = developer.valueInputs[148].transform.parent;
            Button toggle = toggleRow.Find("SettingSlider/SettingToggle").GetComponent<Button>();
            Assert.That(toggleRow.gameObject.activeInHierarchy, Is.True);
            Assert.That(developer.valueInputs[148].gameObject.activeSelf, Is.False);
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ON"));
            toggle.onClick.Invoke();
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("OFF"));
            Assert.That(network.config.PaintDryingEnabled, Is.True, "Draft OFF must not change the accepted config before APPLY.");
            for (int i = 149; i <= 151; i++)
            {
                Assert.That(developer.valueInputs[i].readOnly, Is.True);
                Assert.That(developer.valueInputs[i].interactable, Is.False);
                Assert.That(developer.valueSliders[i].interactable, Is.False);
            }
            toggle.onClick.Invoke();
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ON"));
            float[] requested = { 7f, .3f, 25f };
            for (int i = 149; i <= 151; i++)
            {
                var field = LabConfig.DeveloperFields[i];
                Assert.That(developer.valueInputs[i].gameObject.activeInHierarchy, Is.True);
                Assert.That(developer.valueSliders[i].interactable, Is.True);
                developer.valueSliders[i].value = .5f;
                Assert.That(float.Parse(developer.valueInputs[i].text, CultureInfo.InvariantCulture),
                    Is.EqualTo(Mathf.Lerp(field.Min, field.Max, .5f)).Within(.0001f));
                developer.valueInputs[i].text = requested[i - 149].ToString(CultureInfo.InvariantCulture);
                Assert.That(developer.valueSliders[i].value,
                    Is.EqualTo(Mathf.InverseLerp(field.Min, field.Max, requested[i - 149])).Within(.0001f));
            }
            Assert.That(network.config.PaintDryingEnabled, Is.True, "Draft values cannot change an accepted room.");
            developer.applyStartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(network.IsDeveloperSolo, Is.True, developer.feedbackText.text);
            Assert.That(network.config, Is.Not.SameAs(developer.sourceConfig));
            Assert.That(network.config.PaintDryingEnabled, Is.True);
            Assert.That(network.config.PaintDryingDelaySeconds, Is.EqualTo(7f));
            Assert.That(network.config.PaintDryingPercentPerSecond, Is.EqualTo(.3f));
            Assert.That(network.config.PaintNearbyHitRadiusDegrees, Is.EqualTo(25f));
            LabModel firstModel = Model();
            Assert.That(firstModel.PaintSurface.RegisterHit(Vector3.forward, Color.red, network.LocalPlayerId, 1), Is.True);
            string previousSession = network.Snapshot.sessionId;
            developer.Open();
            developer.valueInputs[148].text = "0";
            developer.applyStartButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(network.config.PaintDryingEnabled, Is.False);
            Assert.That(network.config.PaintDryingDelaySeconds, Is.EqualTo(7f), "OFF preserves the adjustable profile.");
            Assert.That(network.config.PaintDryingPercentPerSecond, Is.EqualTo(.3f));
            Assert.That(network.config.PaintNearbyHitRadiusDegrees, Is.EqualTo(25f));
            Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(previousSession));
            Assert.That(Model().PaintSurface.CoveredCount, Is.Zero, "APPLY & RESTART clears old wet cells and protection.");
            developer.Open();
            developer.ShowDefaults();
            Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("ON"), "DEFAULTS restores the game asset's ON setting.");
            Assert.That(network.config.PaintDryingEnabled, Is.False, "Loading defaults is not an APPLY operation.");
            Assert.That(developer.valueInputs[149].text, Is.EqualTo("5"));
            Assert.That(developer.valueInputs[150].text, Is.EqualTo("0.2"));
            Assert.That(developer.valueInputs[151].text, Is.EqualTo("20"));
            Assert.That(JsonUtility.ToJson(developer.sourceConfig), Is.EqualTo(sourceJson));
        }

        [UnityTest]
        public IEnumerator AuthoritativeErosionUpdatesSavedColorTexturePainterTextureAndGoalTogether()
        {
            LabModel model = BeginControlledHost(2f);
            Hit(model, "initial", 0d);
            CommitDisplay(model, 0d);
            Color32[] before = paint.PaintTexture.GetPixels32();
            float initialFill = hud.yokaiHpFill.fillAmount;
            model.Tick(6d);
            CommitDisplay(model, 6d);
            Color32[] after = paint.PaintTexture.GetPixels32();
            byte[] owners = Slots(model.PaintSurface);
            Color32[] ownerMap = paint.PainterTexture.GetPixels32();
            int erased = 0;
            for (int i = 0; i < after.Length; i++)
            {
                Assert.That(ownerMap[i].r, Is.EqualTo(owners[i]));
                if (before[i].a == 0 || after[i].a != 0) continue;
                erased++;
                Assert.That(after[i], Is.EqualTo(new Color32(0, 0, 0, 0)));
                Assert.That(ownerMap[i].r, Is.Zero);
            }
            Assert.That(erased, Is.GreaterThan(0));
            Assert.That(after, Is.EqualTo(Pixels(model.PaintSurface)));
            Assert.That(paint.Coverage, Is.EqualTo(network.Snapshot.paintCoverage));
            Assert.That(hud.hpText.text, Is.EqualTo(LabHud.FormatPaintPercentage(network.Snapshot)));
            Assert.That(hud.yokaiHpFill.fillAmount, Is.EqualTo(model.PaintSurface.Coverage).Within(.00001f));
            Assert.That(hud.yokaiHpFill.fillAmount, Is.LessThan(initialFill));
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(JsonUtility.ToJson(developer.sourceConfig), Is.EqualTo(sourceJson));
            yield return null;
            Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(after),
                "The following presentation frame cannot restore obsolete colored cells.");
        }

        [UnityTest]
        public IEnumerator CompletedGourdViewerRetainsTheFrozenOneHundredPercentMapWithDryingEnabled()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required for the completed-gourd preview.");
            LabModel model = BeginControlledHost(100f);
            Hit(model, "complete", 0d);
            CommitDisplay(model, 0d);
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory));
            var viewer = LabGourdResultViewer.Attach(network);
            var finish = network.target.GetComponent<LabPaintFinishPresentation>();
            Assert.That(finish, Is.Not.Null);
            Color32[] frozen = Pixels(model.PaintSurface);
            byte[] owners = Slots(model.PaintSurface);
            uint revision = model.PaintSurface.Revision;
            int hits = network.Snapshot.players.Single().hitTotal;
            float originalTimeScale = Time.timeScale;
            viewer.RefreshState();
            Assert.That(LabGourdResultViewer.CanInspect(network), Is.False,
                "A completed map is committed before its finishing presentation releases the viewer.");
            Assert.That(viewer.OpenViewer(), Is.False);
            Assert.That(viewer.Preview, Is.Null);

            // A terminal victory freezes drying even while the result presentation is still playing.
            model.Tick(500d);
            CommitDisplay(model, 500d);
            Assert.That(model.PaintSurface.Revision, Is.EqualTo(revision));
            Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(frozen));
            Assert.That(Slots(model.PaintSurface), Is.EqualTo(owners));
            double finishDeadline = Time.realtimeSinceStartupAsDouble + 3d;
            while (!finish.Complete && Time.realtimeSinceStartupAsDouble < finishDeadline) yield return null;
            Assert.That(finish.Complete, Is.True, "The real-time completion presentation must finish within its bounded budget.");
            Assert.That(LabPaintFinishPresentation.DelayResult(network, network.Snapshot), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(originalTimeScale));
            Assert.That(network.Snapshot.players.Single().hitTotal, Is.EqualTo(hits));
            Assert.That(model.PaintSurface.Revision, Is.EqualTo(revision));
            Assert.That(Pixels(network.PaintSurface), Is.EqualTo(frozen));
            Assert.That(Slots(model.PaintSurface), Is.EqualTo(owners));
            viewer.RefreshState();
            Assert.That(LabGourdResultViewer.CanInspect(network), Is.True);
            Assert.That(viewer.OpenViewer(), Is.True, viewer.LastError);
            Assert.That(viewer.IsOpen, Is.True);
            Assert.That(viewer.Preview.PaintTexture.GetPixels32(), Is.EqualTo(frozen));
            model.Tick(1000d);
            CommitDisplay(model, 1000d);
            viewer.RefreshState();
            Assert.That(viewer.IsOpen, Is.True);
            Assert.That(model.PaintSurface.Revision, Is.EqualTo(revision));
            Assert.That(viewer.Preview.PaintTexture.GetPixels32(), Is.EqualTo(frozen));
            Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(frozen));
            Assert.That(Slots(model.PaintSurface), Is.EqualTo(owners));
            Assert.That(network.Snapshot.paintCoveredCount, Is.EqualTo(network.Snapshot.paintCellCount));
            Assert.That(hud.hpText.text, Is.EqualTo("100.0%"));
            viewer.CloseViewer();
            yield return null;
        }

        private LabModel BeginControlledHost(float initial)
        {
            Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
            network.enabled = false; // fake-clock integration, no real-time PublishSnapshot can race this fixture
            fixture = Object.Instantiate(network.config);
            float[] values = fixture.CaptureDeveloperValues();
            values[2] = 20f; values[79] = 0f; values[104] = 0f; values[105] = initial;
            values[110] = 1f; values[134] = 0f;
            values[148] = 1f; values[149] = 5f; values[150] = .2f; values[151] = 20f;
            Assert.That(fixture.TryApplyDeveloperValues(values, out string error), Is.True, error);
            network.config = fixture;
            var model = new LabModel(); model.BeginSolo("drying-presentation", network.LocalPlayerId, 0d, fixture);
            LabPaintGrowthSettings settings = fixture.PaintGrowthSettings;
            settings.RepeatPercent = 0f; settings.GrowthPercentPerSecond = 0f;
            model.PaintSurface.Reset(settings, 225u);
            typeof(LabNetwork).GetField("model", Private).SetValue(network, model);
            return model;
        }
        private LabModel Model() => (LabModel)typeof(LabNetwork).GetField("model", Private).GetValue(network);
        private void Hit(LabModel model, string id, double now)
        {
            var orbs = (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model);
            orbs.Add(id, new LabOrbState { id = id, owner = network.LocalPlayerId, kind = LabOrbKind.Combined,
                element = LabOrbElement.Fire, inFlight = true, createdAt = now });
            Assert.That(model.ConfirmHit(id, now, Vector3.forward, out _, out _, out _), Is.True);
        }
        private void CommitDisplay(LabModel model, double now)
        {
            LabSnapshot snapshot = model.Snapshot(now);
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, snapshot);
            typeof(LabNetwork).GetMethod("CopyPaintToDisplay", Private)
                .Invoke(network, new object[] { model.PaintSurface, snapshot });
            paint.RefreshCommittedMap();
            typeof(LabHud).GetMethod("Update", Private).Invoke(hud, null);
        }
        private static Color32[] Pixels(LabPaintSurface surface)
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }
        private static byte[] Slots(LabPaintSurface surface)
        { var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots); return slots; }
    }
}
