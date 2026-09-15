using System;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class LaunchMappingTests
    {
        private static readonly ProjectileLaunchBasis Basis = new ProjectileLaunchBasis(
            new Vector3(0f, .55f, -4.5f), Vector3.right, 3.5f, new Vector3(0f, 1.4f, 0f));

        [TestCase(0f, -1.75f)]
        [TestCase(.5f, 0f)]
        [TestCase(1f, 1.75f)]
        public void HorizontalEntryMapsToExplicitBasisWithoutCamera(float x, float expectedX)
        {
            var pose = LaunchMapping.Calculate(new Vector2(x, .82f), Basis);
            Assert.That(pose.Position.x, Is.EqualTo(expectedX).Within(.00001f));
            Assert.That(pose.Position.y, Is.EqualTo(.55f));
            Assert.That(pose.Position.z, Is.EqualTo(-4.5f));
            Assert.That(Vector3.Angle(pose.Direction, Basis.AimPoint - pose.Position), Is.LessThan(.01f));
            Assert.That(pose.Direction.magnitude, Is.EqualTo(1f).Within(.00001f));
        }

        [Test]
        public void RotatedTranslatedBasisDefinesWorldLaunch()
        {
            var basis = new ProjectileLaunchBasis(new Vector3(8f, 2f, 10f), Vector3.forward * 7f,
                4f, new Vector3(20f, 3f, 10f));
            var pose = LaunchMapping.Calculate(new Vector2(1f, 1f), basis);
            Assert.That(pose.Position, Is.EqualTo(new Vector3(8f, 2f, 12f)));
            Assert.That(Vector3.Angle(pose.Direction, new Vector3(12f, 1f, -2f)), Is.LessThan(.01f));
        }

        [Test]
        public void EntryHeightDoesNotAddASecondThrowRuleOrChangePower()
        {
            var first = LaunchMapping.Calculate(new Vector2(.3f, .82f), Basis);
            var second = LaunchMapping.Calculate(new Vector2(.3f, 1f), Basis);
            Assert.That(first.Position, Is.EqualTo(second.Position));
            Assert.That(first.Direction, Is.EqualTo(second.Direction));
        }

        [TestCase(-.001f, .8f)]
        [TestCase(1.001f, .8f)]
        [TestCase(.5f, -.001f)]
        [TestCase(.5f, 1.001f)]
        [TestCase(float.NaN, .8f)]
        [TestCase(.5f, float.PositiveInfinity)]
        public void InvalidCoordinatesAreRejectedInsteadOfSilentlyReaimed(float x, float y) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => LaunchMapping.Calculate(new Vector2(x, y), Basis));

        [Test]
        public void InvalidBasisOrDirectionCannotCreateLaunch()
        {
            Assert.Throws<ArgumentException>(() => new ProjectileLaunchBasis(Vector3.zero, Vector3.zero, 1f, Vector3.forward));
            Assert.Throws<ArgumentException>(() => new ProjectileLaunchBasis(Vector3.zero, Vector3.right, 0f, Vector3.forward));
            Assert.Throws<ArgumentException>(() => new ProjectileLaunchPose(Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentException>(() => LaunchMapping.Calculate(Vector2.one * .5f, default));
            var pointEqualsStart = new ProjectileLaunchBasis(Vector3.zero, Vector3.right, 1f, Vector3.zero);
            Assert.Throws<ArgumentException>(() => LaunchMapping.Calculate(Vector2.one * .5f, pointEqualsStart));
        }

        [Test]
        public void PhysicsTuningRejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => new ProjectilePhysicsTuning(0f, 3f, .2f));
            Assert.Throws<ArgumentException>(() => new ProjectilePhysicsTuning(12f, -1f, .2f));
            Assert.Throws<ArgumentException>(() => new ProjectilePhysicsTuning(12f, 3f, float.NaN));
        }
    }
}
