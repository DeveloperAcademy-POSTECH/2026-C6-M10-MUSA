using System.Collections.Generic;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPlayerCircleTests
    {
        [Test]
        public void ArrivalOrderNotClientIdDeterminesNumbersAndRejoinGoesLast()
        {
            var go = new GameObject("Arrival test");
            try
            {
                var network = go.AddComponent<LabNetwork>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var order = (List<ulong>)typeof(LabNetwork).GetField("order", flags).GetValue(network);
                order.AddRange(new ulong[] { 50, 2, 90 });
                var sync = typeof(LabNetwork).GetMethod("SyncArrivalSlots", flags);
                sync.Invoke(network, null);
                var slots = (LabPlayerSlots)typeof(LabNetwork).GetField("playerSlots", flags).GetValue(network);
                Assert.That(slots.PlayersInSlotOrder(), Is.EqualTo(new ulong[] { 50, 2, 90 }));
                order.Remove(2);
                order.Add(100);
                sync.Invoke(network, null);
                slots = (LabPlayerSlots)typeof(LabNetwork).GetField("playerSlots", flags).GetValue(network);
                Assert.That(slots.PlayersInSlotOrder(), Is.EqualTo(new ulong[] { 50, 90, 100 }));
                Assert.That(network.CanSelectPlayerSlot, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SavedSceneHasFiveAssetsAndFigmaCirclePositions()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity", OpenSceneMode.Additive);
            try
            {
                LabHud hud = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (hud == null) hud = root.GetComponentInChildren<LabHud>(true);
                Assert.That(hud, Is.Not.Null);
                Assert.That(hud.playerCircleSprites.Length, Is.EqualTo(5));
                foreach (var sprite in hud.playerCircleSprites) Assert.That(sprite, Is.Not.Null);
                AssertCircle(hud.localPlayerCircle.rectTransform, 201, 833);
                AssertCircle(hud.leftPlayerCircle.rectTransform, 17, 461);
                AssertCircle(hud.rightPlayerCircle.rectTransform, 384, 461);
                Assert.That(hud.localPlayerCircle.raycastTarget, Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertCircle(RectTransform rect, float x, float y)
        {
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(48, 48)));
            Assert.That(rect.anchorMin.x, Is.EqualTo(x / 402f).Within(.00001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(1f - y / 874f).Within(.00001f));
        }
    }
}
