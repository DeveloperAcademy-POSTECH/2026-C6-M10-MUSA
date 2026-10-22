using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace C6Lab.Tests
{
    /// <summary>
    /// 콤보 SMALL/BIG 표시 (#257): 판 구슬 그림 크기(물리 그대로), 숫자 없는 SMALL 배지, BIG 대신 쏟아지는 별.
    /// </summary>
    public sealed class LabComboEffectLookTests
    {
        [Test]
        public void TheComboScaleSizesTheArtOnTopOfOtherAnimationsAndLeavesPhysicsAlone()
        {
            var orb = new GameObject("Combo scale orb");
            try
            {
                var view = orb.AddComponent<LabOrbView>();
                view.Configure("combo-scale", LabOrbKind.Yin, .32f);
                float radius = view.HitSphere.radius;
                view.SetComboScaleTarget(.6f);
                for (int i = 0; i < 60; i++) view.StepComboScale(.02f, .25f);
                Assert.That(view.ComboScale, Is.EqualTo(.6f).Within(.002f), "SMALL: the art shrinks smoothly to 0.6.");
                Assert.That(view.Visual.localScale.x, Is.EqualTo(.6f).Within(.002f));
                Assert.That(view.HitSphere.radius, Is.EqualTo(radius), "Bumping, grabbing and combining stay the same.");

                // A spawn/drop/combine animation writes its own size; the combo scale goes on top, never compounding.
                view.Visual.localScale = new Vector3(1.2f, .8f, 1f);
                view.StepComboScale(.02f, .25f);
                Assert.That(view.Visual.localScale.x, Is.EqualTo(1.2f * .6f).Within(.002f));
                Assert.That(view.Visual.localScale.y, Is.EqualTo(.8f * .6f).Within(.002f));
                view.StepComboScale(.02f, .25f);
                Assert.That(view.Visual.localScale.x, Is.EqualTo(1.2f * .6f).Within(.002f));

                view.SetComboScaleTarget(1f);
                for (int i = 0; i < 60; i++) view.StepComboScale(.02f, .25f);
                Assert.That(view.Visual.localScale.x, Is.EqualTo(1.2f).Within(.002f), "Over: back to the animation's own size.");
                view.SetComboScaleTarget(float.NaN);
                Assert.That(view.ComboScaleTarget, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(orb);
            }
        }

        [Test]
        public void TheBoardMapsSmallAndBigToItsMilderArtScales()
        {
            var host = new GameObject("Combo look board");
            try
            {
                var board = host.AddComponent<LabOrbBoard>();
                Assert.That(board.ComboLookScale, Is.EqualTo(1f));
                board.SetComboLook(-1);
                Assert.That(board.ComboLook, Is.EqualTo(-1));
                Assert.That(board.ComboLookScale, Is.EqualTo(.6f), "Milder than a thrown orb's 0.1x.");
                board.SetComboLook(5);
                Assert.That(board.ComboLook, Is.EqualTo(1));
                Assert.That(board.ComboLookScale, Is.EqualTo(1.4f), "Milder than a thrown orb's 2.5x.");
                board.SetComboLook(0);
                Assert.That(board.ComboLookScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TheRewardRainsTheBujeokArtInsteadOfStars()
        {
            Assert.That(Resources.Load<Sprite>(LabComboRewardStars.BujeokPath), Is.Not.Null, "The talisman art (#268) is in Resources.");
            var parent = new GameObject("Reward rain parent", typeof(RectTransform));
            try
            {
                var rain = parent.AddComponent<LabComboRewardStars>();
                rain.Initialize((RectTransform)parent.transform, 1f);
                Assert.That(rain.UsesBujeok, Is.True);
                Image first = rain.Area.GetComponentInChildren<Image>(true);
                Assert.That(first.sprite.texture.name, Is.EqualTo("Bujeok"));
                Assert.That(first.color, Is.EqualTo(Color.white), "Its own colours, not tinted yellow.");
                Assert.That(first.raycastTarget, Is.False);
                Assert.That(LabComboRewardStars.BujeokSizeScale, Is.EqualTo(2.7f), "1.5x the first talisman size.");
                Assert.That(LabComboRewardStars.BujeokCountScale, Is.EqualTo(.5f), "Half as many as the stars.");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [UnityTest]
        public IEnumerator SmallShowsABarWithoutSecondsAndBigRainsStarsInstead()
        {
            var root = new GameObject("Combo look HUD fixture");
            var network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            var canvasRoot = new GameObject("Combo look canvas", typeof(RectTransform), typeof(Canvas));
            var hud = root.AddComponent<LabGourdComboHud>();
            hud.Bind(network, canvasRoot.GetComponent<Canvas>());
            FieldInfo latest = typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(latest, Is.Not.Null);
            try
            {
                latest.SetValue(network, Snapshot(network, 50d, penaltyUntil: 55d));
                yield return null;
                yield return null;
                Assert.That(hud.EffectVisible, Is.True);
                Assert.That(hud.EffectLabel, Is.EqualTo(LabGourdComboHud.PenaltyLabel), "SMALL with its bar, no seconds.");
                Assert.That(hud.EffectLabel, Does.Not.Match("[0-9]"));
                Assert.That(hud.StarsEmitting, Is.False);
                Assert.That(hud.BoardLook, Is.EqualTo(-1), "The board is told to shrink its orbs.");

                latest.SetValue(network, Snapshot(network, 51d, rewardUntil: 59d));
                yield return null;
                yield return null;
                Assert.That(hud.EffectVisible, Is.False, "No BIG badge any more.");
                Assert.That(hud.StarsEmitting, Is.True, "Stars rain instead.");
                Assert.That(hud.StarCount, Is.GreaterThan(0), "A burst as the reward starts.");
                Assert.That(hud.BoardLook, Is.EqualTo(1));
                float early = hud.StarsPerSecond;

                latest.SetValue(network, Snapshot(network, 58.5d, rewardUntil: 59d));
                yield return null;
                Assert.That(hud.StarsPerSecond, Is.LessThan(early), "Fewer stars as the reward runs out.");
                Assert.That(hud.StarsEmitting, Is.True);

                foreach (Graphic graphic in canvasRoot.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, "Stars cannot steal throw/board input.");

                latest.SetValue(network, Snapshot(network, 60d));
                yield return null;
                Assert.That(hud.StarsEmitting, Is.False, "Reward over: no new stars.");
                Assert.That(hud.BoardLook, Is.Zero, "The board's orbs go back to their size.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(canvasRoot);
            }
        }

        private static LabSnapshot Snapshot(LabNetwork network, double now, double penaltyUntil = 0d, double rewardUntil = 0d)
        {
            return new LabSnapshot
            {
                sessionId = "combo-look", round = 1, phase = LabPhase.Playing, serverTime = now,
                comboCount = 1, comboActive = false,
                comboColors = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Earth },
                comboResultAt = 49d,
                players = new[]
                {
                    new LabPlayerState { id = network.LocalPlayerId, comboPenaltyUntil = penaltyUntil, comboRewardUntil = rewardUntil }
                }
            };
        }
    }
}
