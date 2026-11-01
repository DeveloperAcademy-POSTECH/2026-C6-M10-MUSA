using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPlayerCircleTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AutomaticClaimsRequireTheExplicitSmokeTestFlagAndNeverReplaceChoices(int capacity)
        {
            var go = new GameObject("Automatic smoke-test positions");
            try
            {
                var network = go.AddComponent<LabNetwork>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var slots = new LabPlayerSlots(capacity);
                typeof(LabNetwork).GetField("targetParticipantCount", flags).SetValue(network, capacity);
                typeof(LabNetwork).GetField("playerSlots", flags).SetValue(network, slots);
                var assign = typeof(LabNetwork).GetMethod("AssignAutomaticTestSlot", flags);
                assign.Invoke(network, new object[] { 0ul });
                assign.Invoke(network, new object[] { 12ul });
                Assert.That(slots.SelectedCount, Is.Zero,
                    "Normal Host and Client joins must remain unselected.");

                Assert.That(slots.TrySelect(0ul, capacity, out _), Is.True);
                typeof(LabNetwork).GetField("autoStart", flags).SetValue(network, true);
                assign.Invoke(network, new object[] { 0ul });
                assign.Invoke(network, new object[] { 12ul });
                Assert.That(slots.GetSlot(0ul), Is.EqualTo(capacity),
                    "Enabling scripted smoke tests must not renumber an already selected Host.");
                Assert.That(slots.GetSlot(12ul), Is.EqualTo(1));
                assign.Invoke(network, new object[] { 12ul });
                Assert.That(slots.SelectedCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReturningToTheSameLobbyKeepsChosenNumbersButLeavingTheRoomClearsThem()
        {
            var go = new GameObject("Chosen position lifecycle");
            try
            {
                var network = go.AddComponent<LabNetwork>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var order = (List<ulong>)typeof(LabNetwork).GetField("order", flags).GetValue(network);
                order.AddRange(new ulong[] { 50, 2, 90 });
                var slots = new LabPlayerSlots(3);
                Assert.That(slots.TrySelect(50, 3, out _), Is.True);
                Assert.That(slots.TrySelect(2, 1, out _), Is.True);
                Assert.That(slots.TrySelect(90, 2, out _), Is.True);
                typeof(LabNetwork).GetField("playerSlots", flags).SetValue(network, slots);

                typeof(LabNetwork).GetMethod("EndBattleToLobby", flags).Invoke(network, null);
                Assert.That(network.GetSlotOwner(1), Is.EqualTo(2ul));
                Assert.That(network.GetSlotOwner(2), Is.EqualTo(90ul));
                Assert.That(network.GetSlotOwner(3), Is.EqualTo(50ul));
                Assert.That(slots.PlayersInSlotOrder(), Is.EqualTo(new ulong[] { 2, 90, 50 }),
                    "Returning to the same room must not restore arrival order or redraw positions.");
                Assert.That(order, Is.EqualTo(new ulong[] { 50, 2, 90 }));

                typeof(LabNetwork).GetMethod("ResetLocal", flags).Invoke(network, null);
                Assert.That(order, Is.Empty);
                Assert.That(network.GetSlotOwner(1), Is.EqualTo(ulong.MaxValue));
                Assert.That(network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
                Assert.That(network.GetSlotOwner(3), Is.EqualTo(ulong.MaxValue));
                Assert.That(network.CanSelectPlayerSlot, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SavedSceneHasSixCircleAssetsAndFigmaCirclePositions()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity", OpenSceneMode.Additive);
            try
            {
                LabHud hud = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (hud == null) hud = root.GetComponentInChildren<LabHud>(true);
                Assert.That(hud, Is.Not.Null);
                // P1..P5 and P6 for 3:3 territory (#394); the drum minimap draws the same circles.
                Assert.That(hud.playerCircleSprites.Length, Is.EqualTo(LabModel.TerritoryMaximumParticipants));
                foreach (var sprite in hud.playerCircleSprites) Assert.That(sprite, Is.Not.Null);
                Assert.That(hud.playerCircleSprites.Select(sprite => sprite.texture).Distinct().Count(), Is.EqualTo(6));
                LabDrumMinimap map = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (map == null) map = root.GetComponentInChildren<LabDrumMinimap>(true);
                Assert.That(map.playerSprites, Is.EqualTo(hud.playerCircleSprites));
                AssertCircle(hud.localPlayerCircle.rectTransform, 87.5f, 832.5f);
                AssertCircle(hud.leftPlayerCircle.rectTransform, 16.5f, 503.5f);
                AssertCircle(hud.rightPlayerCircle.rectTransform, 387.5f, 503.5f);
                Assert.That(hud.localPlayerCircle.raycastTarget, Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertCircle(RectTransform rect, float x, float y)
        {
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(45, 47)));
            Assert.That(rect.anchorMin.x, Is.EqualTo(x / 402f).Within(.00001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(1f - y / 874f).Within(.00001f));
        }
    }
}
