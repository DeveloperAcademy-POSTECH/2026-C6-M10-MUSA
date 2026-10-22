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
                other.flashLastTargetAll = false;
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

        [UnityTest]
        public IEnumerator PauseResumeRestoresWarningEyesAndDisablingTheHudClearsItsGlare()
        {
            var root = new GameObject("Flash lifecycle fixture");
            var network = root.AddComponent<LabNetwork>(); network.enabled = false;
            var config = ScriptableObject.CreateInstance<LabConfig>(); network.config = config;
            var boardRoot = new GameObject("Glare board"); boardRoot.transform.SetParent(root.transform, false);
            network.board = boardRoot.AddComponent<LabOrbBoard>();
            var cameraRoot = new GameObject("Battle camera"); cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.position = Vector3.back * 10f;
            Camera camera = cameraRoot.AddComponent<Camera>();
            network.target = LabTarget.CreateCylinder(root.transform, Vector3.zero, .8f, 2f);
            var canvasRoot = new GameObject("Flash lifecycle canvas", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            LabGourdAlert alert = root.AddComponent<LabGourdAlert>(); alert.Bind(network, canvas, camera);
            LabYaguangFlashHud hud = root.AddComponent<LabYaguangFlashHud>(); hud.Bind(network, canvas);
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                latest.SetValue(network, Snapshot(20.8d, 19.5d, 21d));
                yield return null;
                Assert.That(alert.ShowingEyes, Is.True);
                LabSnapshot paused = Snapshot(20.8d, 19.5d, 21d); paused.paused = true;
                latest.SetValue(network, paused);
                yield return new WaitForSeconds(.35f); // Longer than the warning's remaining 0.2 s.
                Assert.That(alert.ShowingEyes, Is.False, "The existing alert hides while the pause menu is open.");
                latest.SetValue(network, Snapshot(20.8d, 19.5d, 21d));
                yield return null;
                Assert.That(alert.ShowingEyes, Is.True, "The same warning resumes with its remaining time.");
                Assert.That(alert.EyeGlow, Is.GreaterThan(.9f));

                LabSnapshot flashed = Snapshot(21.05d, -1d, -1d);
                flashed.flashCount = 1; flashed.flashLastAt = 21d;
                latest.SetValue(network, flashed);
                yield return null;
                Assert.That(network.board.FlashGlare, Is.EqualTo(1f));
                hud.enabled = false;
                Assert.That(network.board.FlashGlare, Is.Zero, "Disabling the effect cannot leave the board white.");
                Assert.That(hud.BurstAlpha, Is.Zero);

                yield return new WaitForSeconds(.35f);
                LabSnapshot freshRound = Snapshot(21.05d, -1d, -1d);
                freshRound.round = 2;
                freshRound.flashCount = 1; freshRound.flashLastAt = 21.05d;
                latest.SetValue(network, freshRound);
                hud.enabled = true;
                yield return null;
                Assert.That(hud.BurstAlpha, Is.GreaterThan(.9f),
                    "An identical numeric server time in a new round must not inherit the previous round's extrapolation.");
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(canvasRoot); Object.Destroy(config);
            }
        }

        [UnityTest]
        public IEnumerator PreviousStageHudCannotUpdateOrClearTheNextStagesBoard()
        {
            var sessionRoot = new GameObject("Persistent flash session");
            var network = sessionRoot.AddComponent<LabNetwork>(); network.enabled = false;
            var config = ScriptableObject.CreateInstance<LabConfig>(); network.config = config;
            var previousStage = new GameObject("Previous flash stage");
            var nextStage = new GameObject("Next flash stage");
            var previousCanvas = new GameObject("Previous flash canvas", typeof(RectTransform), typeof(Canvas));
            var nextCanvas = new GameObject("Next flash canvas", typeof(RectTransform), typeof(Canvas));
            LabOrbBoard previousBoard = previousStage.AddComponent<LabOrbBoard>();
            LabOrbBoard nextBoard = nextStage.AddComponent<LabOrbBoard>();
            network.board = previousBoard;
            LabYaguangFlashHud previousHud = previousStage.AddComponent<LabYaguangFlashHud>();
            previousHud.Bind(network, previousCanvas.GetComponent<Canvas>());
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                LabSnapshot flashed = Snapshot(21.05d, -1d, -1d);
                flashed.flashCount = 1; flashed.flashLastAt = 21d;
                flashed.paused = true; // Keep the effect at an exact authoritative time across both frames.
                latest.SetValue(network, flashed);
                yield return null;
                Assert.That(previousBoard.FlashGlare, Is.EqualTo(1f));
                Assert.That(previousHud.BurstAlpha, Is.GreaterThan(.12f));

                network.board = nextBoard;
                LabYaguangFlashHud nextHud = nextStage.AddComponent<LabYaguangFlashHud>();
                nextHud.Bind(network, nextCanvas.GetComponent<Canvas>());
                yield return null;
                Assert.That(previousBoard.FlashGlare, Is.Zero, "A detached screen clears only its own board.");
                Assert.That(previousHud.BurstAlpha, Is.Zero, "The previous screen must stop drawing the persistent session.");
                Assert.That(nextBoard.FlashGlare, Is.EqualTo(1f));
                Assert.That(nextHud.BurstAlpha, Is.GreaterThan(.12f));

                previousHud.enabled = false;
                Assert.That(nextBoard.FlashGlare, Is.EqualTo(1f), "Old-stage OnDisable cannot clear the new board.");
                Object.DestroyImmediate(previousHud);
                Assert.That(nextBoard.FlashGlare, Is.EqualTo(1f), "Old-stage OnDestroy cannot clear the new board.");
                Assert.That(nextHud.BurstAlpha, Is.GreaterThan(.12f));
            }
            finally
            {
                Object.Destroy(previousStage); Object.Destroy(nextStage); Object.Destroy(sessionRoot);
                Object.Destroy(previousCanvas); Object.Destroy(nextCanvas); Object.Destroy(config);
            }
        }

        private static SpriteRenderer Cover(LabOrbView view) =>
            view.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(renderer => renderer.name == "Glare Cover");

        private static LabSnapshot Snapshot(double now, double warningAt, double flashAt) => new LabSnapshot
        {
            sessionId = "flash-look", round = 1, phase = LabPhase.Playing, serverTime = now,
            flashWarningAt = warningAt, flashAt = flashAt,
            flashTargetAll = true, flashLastTargetAll = true
        };
    }
}
