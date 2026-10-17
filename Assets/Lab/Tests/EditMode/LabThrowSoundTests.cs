using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabThrowSoundTests
    {
        [Test]
        public void HitOffset_IsZeroAtCenterAndOneAtEdge()
        {
            Vector3 towardTarget = new Vector3(0f, 0f, 1f);
            // 날아온 방향 정면(박 앞쪽 가운데)에 맞으면 0
            Assert.AreEqual(0f, LabThrowSound.HitOffset(new Vector3(0f, .3f, -.5f), towardTarget), 1e-4f);
            // 옆 가장자리를 스치면 1
            Assert.AreEqual(1f, LabThrowSound.HitOffset(new Vector3(.5f, 0f, 0f), towardTarget), 1e-4f);
            Assert.AreEqual(1f, LabThrowSound.HitOffset(new Vector3(-.5f, 0f, 0f), towardTarget), 1e-4f);
            // 절반쯤 벗어난 자리
            Assert.AreEqual(.6f, LabThrowSound.HitOffset(new Vector3(.3f, 0f, -.4f), towardTarget), 1e-4f);
        }

        [Test]
        public void HitOffset_IgnoresHeightAndSpeed()
        {
            Vector3 point = new Vector3(.4f, 0f, -.3f);
            float slow = LabThrowSound.HitOffset(point, new Vector3(0f, -2f, 1f));
            float fast = LabThrowSound.HitOffset(point + Vector3.up * .8f, new Vector3(0f, -20f, 10f));
            Assert.AreEqual(slow, fast, 1e-4f);
            Assert.AreEqual(.8f, slow, 1e-4f);
        }

        [Test]
        public void HitOffset_IsSafeForBadInput()
        {
            Assert.AreEqual(0f, LabThrowSound.HitOffset(Vector3.zero, Vector3.zero));
            Assert.AreEqual(0f, LabThrowSound.HitOffset(new Vector3(float.NaN, 0f, 0f), Vector3.forward));
            Assert.AreEqual(0f, LabThrowSound.HitOffset(new Vector3(.2f, 0f, 0f), Vector3.up));
        }

        [Test]
        public void IsGlancing_UsesThreshold()
        {
            Assert.IsFalse(LabThrowSound.IsGlancing(.69f, .7f));
            Assert.IsTrue(LabThrowSound.IsGlancing(.7f, .7f));
            Assert.IsTrue(LabThrowSound.IsGlancing(1f, .7f));
        }
    }
}
