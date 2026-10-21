using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>야광귀 섬광 (#268): 판 구슬·아이템을 하얗게 덮기(북 제외), 화면 예고 빛·번쩍임.</summary>
    public sealed class LabYaguangFlashLookTests
    {
        [Test]
        public void TheGlareCoversOrbsAndItemsButNeverTheDrum()
        {
            var yin = new GameObject("Glare yin");
            var bomb = new GameObject("Glare bomb");
            var drum = new GameObject("Glare drum");
            try
            {
                var view = yin.AddComponent<LabOrbView>();
                view.Configure("glare-yin", LabOrbKind.Yin, .32f);
                float radius = view.HitSphere.radius;
                view.SetGlare(1f);
                SpriteRenderer cover = Cover(view);
                Assert.That(view.Glare, Is.EqualTo(1f));
                Assert.That(cover, Is.Not.Null);
                Assert.That(cover.gameObject.activeSelf, Is.True);
                Assert.That(cover.color.a, Is.EqualTo(1f), "Fully white: no yin/yang colour shows.");
                Assert.That(cover.transform.localScale.x, Is.GreaterThanOrEqualTo(2f * .32f), "It covers the whole art.");
                Assert.That(view.HitSphere.radius, Is.EqualTo(radius), "Only the picture changes.");
                view.SetGlare(.4f);
                Assert.That(cover.color.a, Is.EqualTo(.4f).Within(1e-5f), "Colour fades back in during recovery.");
                view.SetGlare(0f);
                Assert.That(cover.gameObject.activeSelf, Is.False);
                view.SetGlare(float.NaN);
                Assert.That(view.Glare, Is.Zero);

                var item = bomb.AddComponent<LabOrbView>();
                item.Configure("glare-bomb", LabOrbKind.Bomb, .32f);
                item.SetGlare(1f);
                Assert.That(item.Glare, Is.EqualTo(1f), "Items are whitened too.");

                var drumView = drum.AddComponent<LabOrbView>();
                drumView.Configure("glare-drum", LabOrbKind.Drum, .32f);
                drumView.SetGlare(1f);
                Assert.That(drumView.Glare, Is.Zero, "The drum keeps its colour.");
                Assert.That(Cover(drumView), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(yin);
                Object.DestroyImmediate(bomb);
                Object.DestroyImmediate(drum);
            }
        }

        [Test]
        public void TheBoardKeepsItsGlareBetweenZeroAndOne()
        {
            var host = new GameObject("Glare board");
            try
            {
                var board = host.AddComponent<LabOrbBoard>();
                Assert.That(board.FlashGlare, Is.Zero);
                board.SetFlashGlare(2f);
                Assert.That(board.FlashGlare, Is.EqualTo(1f));
                board.SetFlashGlare(float.NaN);
                Assert.That(board.FlashGlare, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator TheTargetsScreenWarnsThenFlashesWithoutBlockingTouches()
        {
            var root = new GameObject("Flash HUD fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            var config = ScriptableObject.CreateInstance<LabConfig>();
            network.config = config;
            var canvasRoot = new GameObject("Flash canvas", typeof(RectTransform), typeof(Canvas));
            var hud = root.AddComponent<LabYaguangFlashHud>();
            hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                latest.SetValue(network, Snapshot(19.7d, warningAt: 19.5d, flashAt: 21d));
                yield return null;
                Assert.That(hud.EdgeAlpha, Is.Zero, "Early in the warning the eyes are still plain.");
                latest.SetValue(network, Snapshot(20.8d, warningAt: 19.5d, flashAt: 21d));
                yield return null;
                Assert.That(hud.EdgeAlpha, Is.GreaterThan(.5f), "Once the eyes light up, the edge glows.");
                Assert.That(hud.BurstAlpha, Is.Zero);

                LabSnapshot flashed = Snapshot(21.05d, warningAt: -1d, flashAt: -1d);
                flashed.flashCount = 1;
                flashed.flashLastAt = 21d;
                latest.SetValue(network, flashed);
                yield return null;
                Assert.That(hud.BurstAlpha, Is.GreaterThan(.12f), "The screen bursts white.");
                Assert.That(hud.EdgeAlpha, Is.Zero);
                Assert.That(hud.HapticCount, Is.EqualTo(1), "One haptic for the new flash.");

                LabSnapshot dazzled = Snapshot(23d, warningAt: -1d, flashAt: -1d);
                dazzled.flashCount = 1;
                dazzled.flashLastAt = 21d;
                latest.SetValue(network, dazzled);
                yield return null;
                Assert.That(hud.BurstAlpha, Is.EqualTo(.12f).Within(.01f), "Only a faint veil stays while dazzled (7 s).");
                Assert.That(hud.HapticCount, Is.EqualTo(1), "The same flash does not buzz again.");

                foreach (Graphic graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, "The flash never blocks the board or throws.");

                LabSnapshot other = Snapshot(23d, warningAt: -1d, flashAt: -1d);
                other.flashCount = 1;
                other.flashLastAt = 21d;
                other.flashLastTarget = network.LocalPlayerId - 1;
                latest.SetValue(network, other);
                yield return null;
                Assert.That(hud.BurstAlpha, Is.Zero, "ONE: someone else's flash leaves this screen alone.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
                Object.Destroy(config);
            }
        }

        private static SpriteRenderer Cover(LabOrbView view) =>
            view.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(renderer => renderer.name == "Glare Cover");

        private static LabSnapshot Snapshot(double now, double warningAt, double flashAt) => new LabSnapshot
        {
            sessionId = "flash-look", round = 1, phase = LabPhase.Playing, serverTime = now,
            flashWarningAt = warningAt, flashAt = flashAt
        };
    }
}
