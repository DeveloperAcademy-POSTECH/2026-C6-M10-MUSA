using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Pure recovery decisions (#476); the native recovery paths run in the macOS PlayMode checks.</summary>
    public sealed class LabDiscoveryBackoffTests
    {
        [Test]
        public void RetriesWaitOneToThirtySecondsAndStopAtTheirLimits()
        {
            double[] expected = { 1, 2, 4, 8, 16, 30, 30, 30 };
            for (int attempt = 0; attempt < expected.Length; attempt++)
                Assert.That(LabDiscoveryBackoff.Delay(attempt), Is.EqualTo(expected[attempt]), "attempt " + attempt);
            Assert.That(LabDiscoveryBackoff.Delay(-3), Is.EqualTo(1d), "Never faster than a second.");
            Assert.That(LabDiscoveryBackoff.Delay(int.MaxValue), Is.EqualTo(30d));
            Assert.That(LabDiscoveryBackoff.CanRetry(0, LabDiscoveryBackoff.MaximumAttempts), Is.True);
            Assert.That(LabDiscoveryBackoff.CanRetry(LabDiscoveryBackoff.MaximumAttempts - 1, LabDiscoveryBackoff.MaximumAttempts), Is.True);
            Assert.That(LabDiscoveryBackoff.CanRetry(LabDiscoveryBackoff.MaximumAttempts, LabDiscoveryBackoff.MaximumAttempts), Is.False);
            Assert.That(LabDiscoveryBackoff.CanRetry(LabDiscoveryBackoff.ServiceRetryLimit, LabDiscoveryBackoff.ServiceRetryLimit), Is.False);
            Assert.That(LabDiscoveryBackoff.CanRetry(-1, LabDiscoveryBackoff.MaximumAttempts), Is.False);
        }

        [Test]
        public void AnExpiredAddressIsAskedForOnceBeforeItIsRemoved()
        {
            Assert.That(LabDiscoveryBackoff.NextAddressState(10d, false, 9.9d), Is.EqualTo(LabAddressExpiry.Keep));
            Assert.That(LabDiscoveryBackoff.NextAddressState(10d, false, 10d), Is.EqualTo(LabAddressExpiry.Renew));
            Assert.That(LabDiscoveryBackoff.NextAddressState(18d, true, 12d), Is.EqualTo(LabAddressExpiry.Keep),
                "A renewing record stays listed until its renewal deadline.");
            Assert.That(LabDiscoveryBackoff.NextAddressState(18d, true, 18d), Is.EqualTo(LabAddressExpiry.Remove));
        }

        [Test]
        public void OnlyAnIphoneWithAnOpenOrSoughtRoomStaysAwake()
        {
            Assert.That(LabMobilePresentationPolicy.NetworkKeepsAwake(false, false, false, false), Is.False);
            Assert.That(LabMobilePresentationPolicy.NetworkKeepsAwake(true, false, false, false), Is.True);
            Assert.That(LabMobilePresentationPolicy.NetworkKeepsAwake(false, true, false, false), Is.True);
            Assert.That(LabMobilePresentationPolicy.NetworkKeepsAwake(false, false, true, false), Is.True);
            Assert.That(LabMobilePresentationPolicy.NetworkKeepsAwake(false, false, false, true), Is.True);
            Assert.That(LabMobilePresentationPolicy.SleepTimeoutFor(RuntimePlatform.IPhonePlayer, true), Is.EqualTo(SleepTimeout.NeverSleep));
            Assert.That(LabMobilePresentationPolicy.SleepTimeoutFor(RuntimePlatform.IPhonePlayer, false), Is.EqualTo(SleepTimeout.SystemSetting));
            foreach (RuntimePlatform kept in new[] { RuntimePlatform.OSXEditor, RuntimePlatform.OSXPlayer, RuntimePlatform.WindowsEditor })
            foreach (bool awake in new[] { false, true })
                Assert.That(LabMobilePresentationPolicy.SleepTimeoutFor(kept, awake), Is.EqualTo(LabMobilePresentationPolicy.KeepSleepTimeout), kept.ToString());
        }
    }
}
