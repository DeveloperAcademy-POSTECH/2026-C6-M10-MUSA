using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Host reason codes stay out of the game's battle notice; the Lab still shows them.</summary>
    public sealed class LabRejectionNoticeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("INSUFFICIENT_STAMINA", true)]
        [TestCase("JIGWI_B_WAIT", true)]
        [TestCase("", true)]
        [TestCase(null, true)]
        [TestCase("Restoring board state. Try again in a moment.", false)]
        [TestCase("불에 막힘", false)]
        public void ReasonCodesAreToldApartFromSentences(string reason, bool code) =>
            Assert.That(LabNetwork.IsInternalReasonCode(reason), Is.EqualTo(code));

        [TestCase(true, "INSUFFICIENT_STAMINA", "before")]
        [TestCase(false, "INSUFFICIENT_STAMINA", "INSUFFICIENT_STAMINA")]
        [TestCase(true, "Restoring board state. Try again in a moment.", "Restoring board state. Try again in a moment.")]
        [TestCase(false, null, "Request rejected.")]
        public void OnlyTheGameHidesCodes(bool game, string reason, string expected)
        {
            var host = new GameObject("Rejection notice");
            host.SetActive(false);
            try
            {
                LabNetwork network = host.AddComponent<LabNetwork>();
                typeof(LabNetwork).GetField("gameFlowEnabled", Private).SetValue(network, game);
                typeof(LabNetwork).GetMethod("SetNotice", Private).Invoke(network, new object[] { "before" });
                typeof(LabNetwork).GetMethod("ShowRejection", Private).Invoke(network, new object[] { reason });
                Assert.That(network.Notice, Is.EqualTo(expected));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
