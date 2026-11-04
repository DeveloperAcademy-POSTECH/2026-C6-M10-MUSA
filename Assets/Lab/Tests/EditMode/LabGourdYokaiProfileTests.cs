using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>#507: the white gourd's timeline moved into a profile without changing a single value.</summary>
    public sealed class LabGourdYokaiProfileTests
    {
        [Test]
        public void TheLegacyProfileIsExactlyTheOldWhiteGourdConstants()
        {
            LabGourdYokaiProfile legacy = LabGourdYokaiProfile.Legacy;
            Assert.That(legacy.lidOpensAt, Is.EqualTo(LabGourdYokai.LidOpensAt));
            Assert.That(legacy.risenAt, Is.EqualTo(LabGourdYokai.RisenAt));
            Assert.That(legacy.lookFrom, Is.EqualTo(LabGourdYokai.LookFrom));
            Assert.That(legacy.lookTo, Is.EqualTo(LabGourdYokai.LookTo));
            Assert.That(legacy.eyesBackAt, Is.EqualTo(LabGourdYokai.EyesBackAt));
            Assert.That(legacy.sinkFrom, Is.EqualTo(LabGourdYokai.SinkFrom));
            Assert.That(legacy.sunkAt, Is.EqualTo(LabGourdYokai.SunkAt));
            Assert.That(legacy.lidClosedAt, Is.EqualTo(LabGourdYokai.LidClosedAt));
            Assert.That(legacy.clipLength, Is.EqualTo(LabGourdYokai.ClipLength));
            Assert.That(legacy.eyeAngle, Is.EqualTo(24f));
            Assert.That(legacy.bodyPath, Is.EqualTo("YokaiRoot"));
            Assert.That(legacy.hopAxis, Is.EqualTo(Vector3.forward));
            Assert.That(legacy.hop, Is.EqualTo(.0006f));
            Assert.That(legacy.idleLoop, Is.True, "The white gourd keeps its ordinary 6-second loop.");
            Assert.That(legacy.IsValid(out string reason), Is.True, reason);
            Assert.That(LabGourdYokaiProfile.Legacy, Is.SameAs(legacy), "One shared runtime instance.");
        }

        [Test]
        public void AnOutOfOrderOrIncompleteTimelineIsRejected()
        {
            var profile = ScriptableObject.CreateInstance<LabGourdYokaiProfile>();
            try
            {
                Assert.That(profile.IsValid(out _), Is.True, "A new profile starts from the legacy values.");
                profile.sinkFrom = profile.lookFrom - .1f;
                Assert.That(profile.IsValid(out string reason), Is.False);
                Assert.That(reason, Does.Contain("order"));
                profile.sinkFrom = LabGourdYokai.SinkFrom;
                profile.lookTo = profile.lookFrom;
                Assert.That(profile.IsValid(out _), Is.False, "The look-around window needs a length.");
                profile.lookTo = LabGourdYokai.LookTo;
                profile.risenAt = float.NaN;
                Assert.That(profile.IsValid(out _), Is.False);
                profile.risenAt = LabGourdYokai.RisenAt;
                profile.bodyPath = "";
                Assert.That(profile.IsValid(out _), Is.False);
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
