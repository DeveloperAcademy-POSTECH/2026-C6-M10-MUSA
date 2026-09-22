using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Lobby.Tests
{
    /// <summary>#28: attack and defense timing travels with the Host config and stays inside the warning.</summary>
    public sealed class MonsterAttackConfigTests
    {
        [Test]
        public void DefaultsMatchTheRuleBookTrial()
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try
            {
                Assert.That(config.MonsterAttackFirstDelaySeconds, Is.EqualTo(20f));
                Assert.That(config.MonsterAttackIntervalSeconds, Is.EqualTo(15f));
                Assert.That(config.MonsterAttackWarningSeconds, Is.EqualTo(3f));
                Assert.That(config.DefenseHoldSeconds, Is.EqualTo(1.2f), "defense stance after 1.2 s");
                Assert.That(config.DefenseFailPenaltySeconds, Is.EqualTo(20f));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void HostConfigCarriesAttackTimingAndRejectsAHoldLongerThanTheWarning()
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try
            {
                var host = LobbyHostConfig.Capture(config);
                Assert.That(host.monsterAttackWarning, Is.EqualTo(config.MonsterAttackWarningSeconds));
                Assert.That(host.defenseHold, Is.EqualTo(config.DefenseHoldSeconds));
                Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(host), out _), Is.True);
                host.defenseHold = host.monsterAttackWarning + 1f;
                Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(host), out _), Is.False);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}