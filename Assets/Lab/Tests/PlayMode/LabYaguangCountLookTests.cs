using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>야광귀 셈법 콤보 화면 (#271): 패널 예고 없이 말풍선만 세기, 칸 위의 수, ? 칸, 성공은 그대로 CLEAR!.</summary>
    public sealed class LabYaguangCountLookTests
    {
        private static readonly LabOrbElement[] Colors =
            { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Earth };

        [UnityTest]
        public IEnumerator TheYaguangCountsTheSlotsAndAClearStaysPlain()
        {
            var root = new GameObject("Yaguang count fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            var config = ScriptableObject.CreateInstance<LabConfig>();
            network.config = config;
            var canvasRoot = new GameObject("Yaguang count canvas", typeof(RectTransform), typeof(Canvas));
            var hud = root.AddComponent<LabGourdComboHud>();
            hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                Assert.That(hud.YaguangCount, Is.True);
                var warning = Snapshot(31d, progress: 0, active: false);
                warning.comboWarningAt = 30d;
                warning.comboNextAt = 34d;
                latest.SetValue(network, warning);
                yield return null;
                Assert.That(hud.WarningCounting, Is.True);
                Assert.That(hud.WarningVisible, Is.False, "No COMBO! panel: only the gourd's bubble counts.");

                latest.SetValue(network, Snapshot(35d, progress: 2, active: true));
                yield return null;
                Assert.That(new[] { hud.SlotWord(0), hud.SlotWord(1), hud.SlotWord(2) }, Is.EqualTo(new[] { "", "", "" }));
                Assert.That(hud.QuestionSlotVisible, Is.False, "The authored four-slot panel has no extra question orb.");
                var panel = canvasRoot.transform.Find("Gourd Combo/Gourd Combo Panel").GetComponent<RectTransform>();
                Assert.That(panel.GetComponent<UnityEngine.UI.Image>().sprite, Is.EqualTo(LabYaguangComboArt.Load("Panel")));
                float fit = LabYaguangComboArt.Fit(canvasRoot.GetComponent<RectTransform>().rect.size);
                Assert.That(panel.sizeDelta.x, Is.EqualTo(207f * fit).Within(.01f));
                Assert.That(panel.sizeDelta.y, Is.EqualTo(51f * fit).Within(.01f));
                var layer = panel.parent;
                Assert.That(layer.Find("Combo Count Pop").GetComponent<RectTransform>().anchorMin, Is.EqualTo(panel.anchorMin));
                Assert.That(layer.Find("Combo Result").GetComponent<RectTransform>().anchorMin, Is.EqualTo(panel.anchorMin));
                for (int i = 0; i < Colors.Length; i++)
                {
                    var slot = panel.Find("Combo Slot " + (i + 1));
                    Assert.That(slot.GetComponent<UnityEngine.UI.Image>().sprite, Is.EqualTo(LabYaguangComboArt.Orb(Colors[i])));
                    Assert.That(hud.SlotChecked(i), Is.EqualTo(i < 2));
                    Assert.That(slot.Find("Check").GetComponent<UnityEngine.UI.Image>().sprite, Is.EqualTo(LabYaguangComboArt.Load("Check")));
                }

                var cleared = Snapshot(40d, progress: 4, active: false);
                cleared.comboResult = LabComboResult.Success;
                cleared.comboResultCount = 1;
                cleared.comboResultAt = 40d;
                latest.SetValue(network, cleared);
                yield return null;
                Assert.That(hud.ResultLabel, Is.EqualTo(LabGourdComboHud.ClearText), "No counting riddle on a clear.");
                yield return new WaitForSecondsRealtime(.9f);
                Assert.That(hud.ResultLabel, Is.EqualTo(LabGourdComboHud.ClearText));

                // Off: the old look.
                float[] values = config.CaptureDeveloperValues();
                values[191] = 0f;
                Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var again = Snapshot(100d, progress: 2, active: true);
                again.comboCount = 2;
                again.comboResultCount = 1;
                latest.SetValue(network, again);
                yield return new WaitForSecondsRealtime(.8f);
                yield return null;
                Assert.That(hud.SlotWord(0), Is.Empty);
                Assert.That(hud.QuestionSlotVisible, Is.False);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
                Object.Destroy(config);
            }
        }

        [UnityTest]
        public IEnumerator ComboReplacesTheGaugeFadesInAndRestoresItOnPauseAndDisable()
        {
            var root = new GameObject("Combo gauge replacement");
            var network = root.AddComponent<LabNetwork>(); network.enabled = false;
            var config = ScriptableObject.CreateInstance<LabConfig>(); network.config = config;
            var canvasRoot = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var progress = new GameObject("Paint Progress", typeof(RectTransform), typeof(CanvasGroup));
            progress.transform.SetParent(canvasRoot.transform, false);
            var group = progress.GetComponent<CanvasGroup>(); group.alpha = .65f;
            var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(progress.transform, false); frame.sizeDelta = new Vector2(242, 27);
            frame.anchoredPosition = new Vector2(-33, 200);
            var hud = root.AddComponent<LabGourdComboHud>(); hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            var latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            var snapshot = Snapshot(35, 1, true); latest.SetValue(network, snapshot);
            try
            {
                Canvas.ForceUpdateCanvases(); yield return null;
                var panel = canvasRoot.transform.Find("Gourd Combo/Gourd Combo Panel").GetComponent<RectTransform>();
                var fade = panel.GetComponent<CanvasGroup>();
                Assert.That(group.alpha, Is.Zero, "Gauge and percentage are hidden before the panel becomes opaque.");
                Assert.That(fade.alpha, Is.LessThan(1f), "The panel fades in rather than appearing instantly.");
                Assert.That(panel.position.x, Is.EqualTo(frame.position.x).Within(.01f));
                Assert.That(panel.position.y, Is.EqualTo(frame.position.y).Within(.01f));
                Assert.That(panel.sizeDelta.x, Is.EqualTo(242f * .98f).Within(.01f));
                yield return new WaitForSecondsRealtime(.26f);
                Assert.That(fade.alpha, Is.EqualTo(1f).Within(.001f));
                snapshot.paused = true; latest.SetValue(network, snapshot); yield return null;
                Assert.That(group.alpha, Is.EqualTo(.65f), "Pause restores the gauge's previous alpha.");
                snapshot.paused = false; latest.SetValue(network, snapshot); yield return null;
                Assert.That(group.alpha, Is.Zero);
                hud.enabled = false;
                Assert.That(group.alpha, Is.EqualTo(.65f), "Disabling the HUD never leaves the gauge hidden.");
                snapshot.comboActive = false; hud.enabled = true; latest.SetValue(network, snapshot); yield return null;
                Assert.That(group.alpha, Is.EqualTo(.65f), "The gauge returns when the combo overlay ends.");
            }
            finally { Object.Destroy(root); Object.Destroy(canvasRoot); Object.Destroy(config); }
        }

        private static LabSnapshot Snapshot(double now, int progress, bool active) => new LabSnapshot
        {
            sessionId = "yaguang-count", round = 1, phase = LabPhase.Playing, serverTime = now,
            comboCount = 1, comboActive = active, comboColors = Colors, comboProgress = progress,
            comboStartedAt = 34d, comboEndsAt = 49d
        };
    }
}
