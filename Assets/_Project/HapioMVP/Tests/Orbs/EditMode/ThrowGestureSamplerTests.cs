using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class ThrowGestureSamplerTests
    {
        [Test]
        public void RecentLinearInputInterpolatesTheWindowBoundary()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(new Vector2(.1f, .2f), 10);
            sampler.Add(new Vector2(.2f, .4f), 10.1);
            sampler.Add(new Vector2(.3f, .6f), 10.2);
            Assert.That(sampler.TryRelease(new Vector2(.4f, .8f), 10.3, .12f, .02f, out var input), Is.True);
            Assert.That(input.Duration, Is.EqualTo(.12f).Within(.000001f));
            Assert.That(input.Delta.x, Is.EqualTo(.12f).Within(.000001f));
            Assert.That(input.Delta.y, Is.EqualTo(.24f).Within(.000001f));
            Assert.That(sampler.Active, Is.False);
        }

        [Test]
        public void FullDisplayWidthUnitsGiveIdenticalInputAtDifferentPixelSizes()
        {
            OrbThrowInput Sample(float width)
            {
                var sampler = new ThrowGestureSampler();
                sampler.Begin(new Vector2(width * .5f, width * 1.2f) / width, 1);
                Assert.That(sampler.TryRelease(new Vector2(width * .6f, width * 1.325f) / width,
                    1.1, .12f, .02f, out var input), Is.True);
                return input;
            }
            var a = Sample(390);
            var b = Sample(1024);
            Assert.That(Vector2.Distance(a.Delta, b.Delta), Is.LessThan(.000001f));
            Assert.That(a.Delta.y, Is.EqualTo(.125f).Within(.000001f), "Y is also divided by width, even above 1 width.");
            Assert.That(a.Duration, Is.EqualTo(b.Duration));
        }

        [Test]
        public void StationaryReleaseIncludesTheFinalHoldAndDoesNotReuseFastMotion()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            sampler.Add(Vector2.up, 10.05);
            Assert.That(sampler.TryRelease(Vector2.up, 11, .12f, .02f, out var input), Is.True);
            Assert.That(input.Delta, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ShortHoldRejectsAndConsumesTheGesture()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            Assert.That(sampler.TryRelease(Vector2.up, 10.01, .12f, .02f, out _), Is.False);
            Assert.That(sampler.Active, Is.False);
            Assert.That(sampler.TryRelease(Vector2.up, 10.1, .12f, .02f, out _), Is.False);
        }

        [Test]
        public void NormalReleaseAndExplicitCancelCannotBeReused()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            Assert.That(sampler.TryRelease(Vector2.up * .1f, 10.1, .12f, .02f, out _), Is.True);
            Assert.That(sampler.TryRelease(Vector2.up, 10.2, .12f, .02f, out _), Is.False);
            sampler.Begin(Vector2.zero, 11);
            sampler.Clear();
            Assert.That(sampler.Add(Vector2.up, 11.1), Is.False);
            Assert.That(sampler.TryRelease(Vector2.up, 11.2, .12f, .02f, out _), Is.False);
        }

        [Test]
        public void NonmonotonicOrInvalidInputInvalidatesTheWholeGesture()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            sampler.Add(Vector2.up, 10.1);
            Assert.That(sampler.Add(Vector2.zero, 10.05), Is.False);
            Assert.That(sampler.TryRelease(Vector2.up, 10.2, .12f, .02f, out _), Is.False);
            Assert.That(sampler.Begin(Vector2.zero, double.NaN), Is.False);
            sampler.Begin(Vector2.zero, 20);
            Assert.That(sampler.Add(new Vector2(float.PositiveInfinity, 0), 20.1), Is.False);
            Assert.That(sampler.Active, Is.False);
        }

        [Test]
        public void SameTimestampSampleReplacesItsPositionWithoutDivisionByZero()
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            sampler.Add(Vector2.up * .05f, 10);
            Assert.That(sampler.TryRelease(Vector2.up * .15f, 10.1, .12f, .02f, out var input), Is.True);
            Assert.That(input.Delta.y, Is.EqualTo(.1f).Within(.000001f));
        }

        [TestCase(0f, .02f)]
        [TestCase(.12f, 0f)]
        [TestCase(.01f, .02f)]
        [TestCase(float.NaN, .02f)]
        public void InvalidWindowConsumesWithoutProducingInput(float window, float minimum)
        {
            var sampler = new ThrowGestureSampler();
            sampler.Begin(Vector2.zero, 10);
            Assert.That(sampler.TryRelease(Vector2.up, 10.1, window, minimum, out _), Is.False);
            Assert.That(sampler.Active, Is.False);
        }
    }
}
