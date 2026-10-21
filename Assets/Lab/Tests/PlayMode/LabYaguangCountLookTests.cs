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
                Assert.That(new[] { hud.SlotWord(0), hud.SlotWord(1), hud.SlotWord(2) }, Is.EqualTo(new[] { "하나", "둘", "" }));
                Assert.That(hud.QuestionSlotVisible, Is.True, "The fifth slot it cannot count.");

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
                values[185] = 0f;
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

        private static LabSnapshot Snapshot(double now, int progress, bool active) => new LabSnapshot
        {
            sessionId = "yaguang-count", round = 1, phase = LabPhase.Playing, serverTime = now,
            comboCount = 1, comboActive = active, comboColors = Colors, comboProgress = progress,
            comboStartedAt = 34d, comboEndsAt = 49d
        };
    }
}
