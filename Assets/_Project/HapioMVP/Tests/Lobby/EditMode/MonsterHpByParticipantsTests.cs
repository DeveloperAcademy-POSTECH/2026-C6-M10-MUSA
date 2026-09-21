using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Lobby.Tests
{
    /// <summary>#29: the frozen multiplayer roster size picks the monster HP on every device.</summary>
    public sealed class MonsterHpByParticipantsTests
    {
        [TestCase(2, 800)]
        [TestCase(3, 1000)]
        [TestCase(4, 1200)]
        [TestCase(5, 1400)]
        public void RosterSizePicksTheTableHp(int players, int hp)
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try { Assert.That(config.MonsterMaxHpFor(players), Is.EqualTo(hp)); }
            finally { Object.DestroyImmediate(config); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void WithoutAFrozenRosterTheLegacyHpStays(int players)
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try { Assert.That(config.MonsterMaxHpFor(players), Is.EqualTo(config.MonsterMaxHp)); }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void HostConfigCarriesTheSameTableToClients()
        {
            var config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            try
            {
                var host = LobbyHostConfig.Capture(config);
                for (int players = 0; players <= 6; players++)
                    Assert.That(host.MonsterHpFor(players), Is.EqualTo(config.MonsterMaxHpFor(players)), players + " players");
                Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(host), out _), Is.True, "the new table keys pass the Host config schema");
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
