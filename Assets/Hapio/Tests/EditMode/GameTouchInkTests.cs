using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Hapio.Tests
{
    /// <summary>터치 먹물 번짐 (#587): 나오는 화면, 사라지는 방식, 터치마다 달라지는 모양, 저장값.</summary>
    public sealed class GameTouchInkTests
    {
        [Test]
        public void ShowsOnEveryScreenButTheBattle()
        {
            Assert.That(GameTouchInk.ShowsOn(GameScenePaths.Battle), Is.False, "The battle, its tutorial and its pause menu.");
            foreach (string path in new[] { GameScenePaths.Home, GameScenePaths.Lobby, GameScenePaths.StageResult, GameScenePaths.GameOver })
                Assert.That(GameTouchInk.ShowsOn(path), Is.True, path);
        }

        [Test]
        public void DrawsOverTheMenusAndUnderTheFadeAndLoadingScreen()
        {
            Assert.That(GameTouchInk.SortingOrder, Is.GreaterThan(5000), "Over the DEV button and the opening.");
            Assert.That(GameTouchInk.SortingOrder, Is.LessThan(GameSceneFade.SortingOrder));
            Assert.That(GameTouchInk.SortingOrder, Is.LessThan(GameLoadingView.SortingOrder));
        }

        [Test]
        public void StartsAtItsStrongestAndFadesAtOnceWithNoHold()
        {
            const float seconds = .6f, strength = .35f;
            Assert.That(GameTouchInk.MarkAlpha(0f, seconds, strength), Is.EqualTo(strength).Within(1e-4f), "Strongest as it lands.");
            Assert.That(GameTouchInk.MarkAlpha(.01f, seconds, strength), Is.LessThan(strength), "Already fading.");
            Assert.That(GameTouchInk.MarkAlpha(seconds * .5f, seconds, strength), Is.EqualTo(strength * .5f).Within(1e-4f));
            Assert.That(GameTouchInk.MarkAlpha(seconds, seconds, strength), Is.Zero);
            Assert.That(GameTouchInk.MarkAlpha(seconds * 2f, seconds, strength), Is.Zero);
        }

        [Test]
        public void SpreadsWhileItFades()
        {
            const float seconds = .6f, start = .85f, end = 1.1f;
            float[] sizes = Enumerable.Range(0, 7).Select(step => GameTouchInk.MarkScale(step * .1f, seconds, start, end)).ToArray();
            Assert.That(sizes[0], Is.EqualTo(start).Within(1e-4f));
            Assert.That(sizes[6], Is.EqualTo(end).Within(1e-4f));
            Assert.That(sizes, Is.Ordered.Ascending);
            Assert.That(sizes[1] - sizes[0], Is.GreaterThan(sizes[6] - sizes[5]), "Quickly at first, then slowing.");
        }

        [Test]
        public void EveryTouchRollsADifferentShapeInsideTheArtsLimits()
        {
            GameTouchInkArt art = GameTouchInkArt.Load();
            var random = new System.Random(11);
            GameTouchInkPlan[] plans = Enumerable.Range(0, 300).Select(_ => GameTouchInk.RollMark(art, random)).ToArray();
            Assert.That(plans.Select(plan => plan.Stain).Distinct().OrderBy(index => index), Is.EqualTo(Enumerable.Range(0, art.stains.Length)), "Every stain comes up.");
            Assert.That(plans.Select(plan => plan.Droplets.Length).Distinct().OrderBy(count => count),
                Is.EqualTo(Enumerable.Range(art.dropletsMin, art.dropletsMax - art.dropletsMin + 1)), "Every droplet count comes up.");
            Assert.That(plans.Select(plan => plan.StainRotation).Distinct().Count(), Is.GreaterThan(290), "Turned differently each time.");
            foreach (GameTouchInkPlan plan in plans)
            {
                Assert.That(plan.StainSize, Is.InRange(art.stainSize * (1f - art.stainSizeVariation), art.stainSize * (1f + art.stainSizeVariation)));
                foreach (GameTouchInkDroplet droplet in plan.Droplets)
                {
                    Assert.That(droplet.Picture, Is.InRange(0, art.droplets.Length - 1));
                    Assert.That(droplet.Size, Is.InRange(art.dropletSizeMin, art.dropletSizeMax));
                    Assert.That(droplet.Offset.magnitude, Is.InRange(art.dropletDistanceMin - .01f, art.dropletDistanceMax + .01f));
                }
            }
        }

        [Test]
        public void SavedArtHoldsTheInkPicturesAndUsableValues()
        {
            GameTouchInkArt art = GameTouchInkArt.Load();
            Assert.That(art, Is.Not.Null, GameTouchInkArt.ResourcePath);
            Assert.That(art.stains.Length, Is.EqualTo(8));
            Assert.That(art.droplets.Length, Is.EqualTo(2));
            Assert.That(art.stains.Concat(art.droplets), Has.None.Null);
            // The numbers themselves are tuned in the Inspector; only what would break the effect is pinned here.
            Assert.That(art.inkStrength, Is.GreaterThan(0f), "Visible at all.");
            Assert.That(art.fadeSeconds, Is.GreaterThan(0f));
            Assert.That(art.dropletsMax, Is.GreaterThanOrEqualTo(art.dropletsMin));
            Assert.That(art.maxMarks, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void EachTapPicksOneOfTheThreeInkSoundsNeverTheSameTwiceInARow()
        {
            // #629: three recorded ink drops at full volume; consecutive taps never repeat the same one.
            GameTouchInkArt art = GameTouchInkArt.Load();
            Assert.That(art.tapSounds, Has.Length.EqualTo(3));
            Assert.That(art.tapSounds, Has.None.Null);
            Assert.That(art.soundVolume, Is.EqualTo(1f));
            int previous = -1;
            for (int i = 0; i < 60; i++)
            {
                AudioClip clip = art.PickTapSound(previous, out int picked);
                Assert.That(clip, Is.Not.Null);
                Assert.That(picked, Is.InRange(0, 2));
                Assert.That(picked, Is.Not.EqualTo(previous), "Not the same sound twice in a row.");
                previous = picked;
            }
        }
    }
}
