using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Lobby.Tests
{
    public sealed class LobbyDeveloperSettingsTests
    {
        private ScreenLayoutConfig asset;
        private LobbyHostConfig defaults;
        private LobbyDeveloperSettings settings;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
            JsonUtility.FromJsonOverwrite("{\"orbRadiusScreenFraction\":0.075,\"orbRadiusCapScale\":2.2,"
                + "\"monsterMaxHp2Players\":800,\"monsterMaxHp3Players\":1000,\"monsterMaxHp4Players\":1200,"
                + "\"monsterMaxHp5Players\":1400,\"baseDamage\":20,\"orbStorageLimit\":20}", asset);
            defaults = LobbyHostConfig.Capture(asset);
            settings = new LobbyDeveloperSettings();
            settings.LoadDefaults(defaults);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(asset);

        [Test]
        public void DisabledModeReturnsAnIndependentCopyOfEveryProjectDefault()
        {
            LobbyHostConfig room = settings.BuildRoomConfig();
            Assert.That(JsonUtility.ToJson(room), Is.EqualTo(JsonUtility.ToJson(defaults)));
            room.monsterHp2 = 1;
            Assert.That(settings.BuildRoomConfig().monsterHp2, Is.EqualTo(800));
            Assert.That(settings.Enabled, Is.False);
            settings.SetEnabled(true);
            Assert.That(JsonUtility.ToJson(settings.BuildRoomConfig()), Is.EqualTo(JsonUtility.ToJson(defaults)),
                "Enabling developer mode alone must not change any gameplay value.");
        }

        [Test]
        public void EnabledModeBuildsOneValidatedHostConfigWithoutEditingTheAsset()
        {
            string before = JsonUtility.ToJson(asset);
            settings.SetEnabled(true);
            settings.SetValue(LobbyDeveloperSetting.MonsterHp2, 600);
            settings.SetValue(LobbyDeveloperSetting.MonsterHp3, 700);
            settings.SetValue(LobbyDeveloperSetting.MonsterHp4, 800);
            settings.SetValue(LobbyDeveloperSetting.MonsterHp5, 900);
            settings.SetValue(LobbyDeveloperSetting.Damage, 35);
            settings.SetValue(LobbyDeveloperSetting.OrbStorageLimit, 12);
            settings.SetValue(LobbyDeveloperSetting.OrbSizePercent, 120);
            settings.SetValue(LobbyDeveloperSetting.BattleDuration, 90);
            settings.SetValue(LobbyDeveloperSetting.GenerateCost, 15);
            settings.SetValue(LobbyDeveloperSetting.RecoveryAmount, 10);
            settings.SetValue(LobbyDeveloperSetting.RecoverySeconds, 1.5);
            settings.SetValue(LobbyDeveloperSetting.HitRecovery, 8);

            LobbyHostConfig room = settings.BuildRoomConfig();

            Assert.That(room.monsterHp2, Is.EqualTo(600));
            Assert.That(room.monsterHp3, Is.EqualTo(700));
            Assert.That(room.monsterHp4, Is.EqualTo(800));
            Assert.That(room.monsterHp5, Is.EqualTo(900));
            Assert.That(room.monsterHp, Is.EqualTo(defaults.monsterHp));
            Assert.That(room.damage, Is.EqualTo(35));
            Assert.That(room.storageLimit, Is.EqualTo(12));
            Assert.That(room.orbRadiusScreenFraction, Is.EqualTo(.09f).Within(.0001f));
            Assert.That(room.orbRadiusCapScale, Is.EqualTo(2.64f).Within(.0001f));
            Assert.That(room.duration, Is.EqualTo(90));
            Assert.That(room.generateCost, Is.EqualTo(15));
            Assert.That(room.recoveryAmount, Is.EqualTo(10));
            Assert.That(room.recoverySeconds, Is.EqualTo(1.5));
            Assert.That(room.hitRecovery, Is.EqualTo(8));
            Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(room), out _), Is.True);
            Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(before));
        }

        [Test]
        public void OrbPhysicsBounceAndExpirySettingsReachTheRoomConfig()
        {
            settings.SetEnabled(true);
            settings.SetValue(LobbyDeveloperSetting.OrbRestitution, .5);
            settings.SetValue(LobbyDeveloperSetting.OrbContactFriction, .2);
            settings.SetValue(LobbyDeveloperSetting.OrbFloorDeceleration, 3);
            settings.SetValue(LobbyDeveloperSetting.OrbMaxReleaseSpeed, 4);
            settings.SetValue(LobbyDeveloperSetting.OrbStopSpeed, .05);
            settings.SetValue(LobbyDeveloperSetting.OrbReleaseSampleWindow, .1);
            settings.SetValue(LobbyDeveloperSetting.RejectedCombinationBounce, 1.5);
            settings.SetValue(LobbyDeveloperSetting.CombinedOrbLifetime, 10);
            settings.SetValue(LobbyDeveloperSetting.CombinedOrbWarning, 3);

            LobbyHostConfig room = settings.BuildRoomConfig();

            Assert.That(room.orbRestitution, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(room.orbContactFriction, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(room.orbFloorDeceleration, Is.EqualTo(3f).Within(.0001f));
            Assert.That(room.orbMaxReleaseSpeed, Is.EqualTo(4f).Within(.0001f));
            Assert.That(room.orbStopSpeed, Is.EqualTo(.05f).Within(.0001f));
            Assert.That(room.orbReleaseSampleWindow, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(room.rejectedCombinationBounceSpeed, Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(room.combinedOrbLifetimeSeconds, Is.EqualTo(10f).Within(.0001f));
            Assert.That(room.combinedOrbWarningSeconds, Is.EqualTo(3f).Within(.0001f));
            Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(room), out _), Is.True);
        }

        [Test]
        public void ExpiryBlinkNeverOutlastsTheLifetime()
        {
            settings.SetEnabled(true);
            settings.SetValue(LobbyDeveloperSetting.CombinedOrbLifetime, 10);
            settings.SetValue(LobbyDeveloperSetting.CombinedOrbWarning, 5);
            settings.SetValue(LobbyDeveloperSetting.CombinedOrbLifetime, 3);
            Assert.That(settings.Value(LobbyDeveloperSetting.CombinedOrbWarning), Is.EqualTo(3));
            settings.Adjust(LobbyDeveloperSetting.CombinedOrbWarning, 1);
            Assert.That(settings.Value(LobbyDeveloperSetting.CombinedOrbWarning), Is.EqualTo(3));
            Assert.That(LobbyHostConfig.TryRead(JsonUtility.ToJson(settings.BuildRoomConfig()), out _), Is.True);
        }

        [Test]
        public void AdjustmentsClampAtTheSupportedHostRangesAndResetToDefaults()
        {
            settings.SetValue(LobbyDeveloperSetting.OrbStorageLimit, 999);
            settings.SetValue(LobbyDeveloperSetting.OrbSizePercent, -10);
            settings.SetValue(LobbyDeveloperSetting.RecoverySeconds, 0);
            Assert.That(settings.Value(LobbyDeveloperSetting.OrbStorageLimit), Is.EqualTo(20));
            Assert.That(settings.Value(LobbyDeveloperSetting.OrbSizePercent), Is.EqualTo(50));
            Assert.That(settings.Value(LobbyDeveloperSetting.RecoverySeconds), Is.EqualTo(.5));
            settings.ResetValues();
            Assert.That(settings.Value(LobbyDeveloperSetting.OrbSizePercent), Is.EqualTo(100));
            Assert.That(settings.Value(LobbyDeveloperSetting.MonsterHp5), Is.EqualTo(1400));
        }
    }
}
