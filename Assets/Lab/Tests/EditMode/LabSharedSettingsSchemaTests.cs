using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Shared array shape is a transport contract, independent of any individual DEV feature.</summary>
    public sealed class LabSharedSettingsSchemaTests
    {
        private LabConfig config;
        private float[] baseline;
        private static ushort Protocol => (ushort)typeof(LabNetwork)
            .GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            baseline = config.CaptureDeveloperValues();
        }

        [TearDown] public void Dispose() => Object.DestroyImmediate(config);

        private static IEnumerable<TestCaseData> IncompatibleLengths()
        {
            int current = LabConfig.DeveloperFields.Count;
            yield return new TestCaseData(0, 3).SetName("SharedSchema_EmptyArrayRejectedAtomically");
            yield return new TestCaseData(1, 3).SetName("SharedSchema_ShortArrayRejectedAtomically");
            yield return new TestCaseData(211, 3).SetName("SharedSchema_SeparateGroupBuildRejectedAtomically");
            yield return new TestCaseData(220, 3).SetName("SharedSchema_SeparateTimelineBuildRejectedAtomically");
            yield return new TestCaseData(225, 3).SetName("SharedSchema_BeforeRawStreakCorrectionRejectedAtomically");
            yield return new TestCaseData(228, 3).SetName("SharedSchema_BeforeJigwiSurfaceFireRejectedAtomically");
            yield return new TestCaseData(238, 3).SetName("SharedSchema_BeforeFeverTimeRejectedAtomically");
            yield return new TestCaseData(240, 3).SetName("SharedSchema_BeforeJigwiScreenSettingsRejectedAtomically");
            yield return new TestCaseData(240, 2).SetName("SharedSchema_TwoPlayerBeforeJigwiScreenSettingsRejectedAtomically");
            yield return new TestCaseData(249, 3).SetName("SharedSchema_UnmergedJigwiScreenPayloadRejectedAtomically");
            yield return new TestCaseData(current - 1, 3).SetName("SharedSchema_MissingTailRejectedAtomically");
            yield return new TestCaseData(current + 1, 3).SetName("SharedSchema_UnknownTailRejectedAtomically");
        }

        [TestCaseSource(nameof(IncompatibleLengths))]
        public void IncompatibleLengthCannotInstallAnySettingOrCreateFingerprint(int length, int participants)
        {
            var requested = new float[length];
            Array.Copy(baseline, requested, Math.Min(baseline.Length, length));
            if (length > 0) requested[0] = 777f;
            Assert.That(config.TryApplyDeveloperValues(requested, out string error), Is.False);
            Assert.That(error, Does.Contain("incomplete"));
            Assert.That(config.CaptureDeveloperValues(), Is.EqualTo(baseline),
                "Array shape is validated before any otherwise valid leading change can be applied.");
            Assert.Throws<ArgumentException>(() => LabSessionSettings.Fingerprint(requested, participants, Protocol));
        }

        [Test]
        public void CurrentSchemaHasTheCurrentProtocolAndCanBeAppliedAndFingerprinted()
        {
            Assert.That(Protocol, Is.EqualTo(5677));
            Assert.That(baseline.Length, Is.EqualTo(251));
            Assert.That(baseline.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
            Assert.That(config.TryApplyDeveloperValues(baseline, out string error), Is.True, error);
            Assert.That(LabSessionSettings.Fingerprint(baseline, 3, Protocol), Is.Not.Empty);
        }
    }
}
