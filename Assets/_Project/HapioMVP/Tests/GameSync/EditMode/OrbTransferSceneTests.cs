using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Prototype.GameSync.Tests
{
    // These assertions inspect saved wiring. They do not claim transport, rendering, or device execution.
    public sealed class OrbTransferSceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/OrbTransferBattle.unity";
        private const string PriorScenePath = "Assets/_Project/HapioMVP/Scenes/TwoPlayerBattle.unity";
        private const string BattleLoopScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private Scene scene;

        [SetUp]
        public void OpenSavedTransferSceneIndependently()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "Run C6/T11/Prepare Orb Transfer Battle before verifying its saved scene.");
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        }

        [TearDown]
        public void CloseOnlyThePreviewOpenedByThisTest()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test]
        public void TransferModeUsesOneExistingGameAndLobbyRootWithCanonicalConfig()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            var root = layout.gameObject;
            Assert.That(layout.Config, Is.SameAs(AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath)));
            Assert.That(layout.Config.StaminaStart, Is.EqualTo(100));
            Assert.That(layout.Config.GenerateCost, Is.EqualTo(20));
            Assert.That(layout.Config.StaminaHitRecovery, Is.EqualTo(5));
            AssertSingleRoot<GameRuntimeConfig>(root);
            AssertSingleRoot<DirectConnectionSession>(root);
            AssertSingleRoot<T09BattleController>(root);
            AssertSingleRoot<T09Hud>(root);
            AssertSingleRoot<OrbPointerInput>(root);
            AssertSingleRoot<T10LobbySession>(root);
            AssertSingleRoot<T10LobbyHud>(root);
            AssertSingleRoot<T10LobbyController>(root);
            var game = AssertSingleRoot<T10GameSession>(root);
            Assert.That(game.TransfersEnabled, Is.True);
            Assert.That(game.BuildIdentifier, Is.EqualTo("16"));
            Assert.That(root.GetComponent<T10LobbySession>().BuildIdentifier, Is.EqualTo("16"));
        }

        [Test]
        public void CopiedControllerInputCamerasAndTargetReferencesBelongToTheNewScene()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            var controller = Components<T09BattleController>(scene).Single();
            var cameras = Components<Camera>(scene);
            Assert.That(cameras, Has.Length.EqualTo(2));
            Assert.That(cameras, Does.Contain(layout.BattleCamera));
            Assert.That(cameras, Does.Contain(layout.OrbCamera));
            Assert.That(layout.BattleCamera, Is.Not.SameAs(layout.OrbCamera));
            Assert.That(layout.BattleCamera.orthographic, Is.False);
            Assert.That(layout.OrbCamera.orthographic, Is.True);
            Assert.That(Components<MonsterHitTarget>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<MonsterHitTarget>(scene)[0].GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(Components<AttackLaunchFrame>(scene), Has.Length.EqualTo(1));
            AssertReference(controller, "layout", layout);
            AssertReference(controller, "connection", Components<DirectConnectionSession>(scene).Single());
            AssertReference(controller, "hud", Components<T09Hud>(scene).Single());
            AssertReference(controller, "launchFrame", Components<AttackLaunchFrame>(scene).Single());
            AssertReference(controller, "target", Components<MonsterHitTarget>(scene).Single());
            AssertReference(Components<OrbPointerInput>(scene).Single(), "controller", controller);
            AssertReference(Components<T10LobbyController>(scene).Single(), "session", Components<T10LobbySession>(scene).Single());
            AssertReference(Components<T10LobbyController>(scene).Single(), "hud", Components<T10LobbyHud>(scene).Single());
            foreach (var component in Components<Component>(scene))
            {
                Assert.That(component, Is.Not.Null, "No copied scene script may be missing.");
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        var referencedObject = reference is Component value ? value.gameObject : reference as GameObject;
                        if (referencedObject != null && referencedObject.scene.IsValid())
                            Assert.That(referencedObject.scene, Is.EqualTo(scene), component.name + "." + property.propertyPath);
                    }
                }
            }
        }

        [Test]
        public void PreviousSavedGameScenesRetainTheirOwnModeAndIdentity()
        {
            var previous = EditorSceneManager.OpenPreviewScene(PriorScenePath);
            var single = EditorSceneManager.OpenPreviewScene(BattleLoopScenePath);
            try
            {
                var oldGame = Components<T10GameSession>(previous).Single();
                Assert.That(oldGame.TransfersEnabled, Is.False);
                Assert.That(oldGame.BuildIdentifier, Is.EqualTo("15"));
                Assert.That(Components<T10GameSession>(single), Is.Empty);
                Assert.That(Components<T09BattleController>(single), Has.Length.EqualTo(1));
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(PriorScenePath)));
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(BattleLoopScenePath)));
            }
            finally
            {
                if (single.IsValid()) EditorSceneManager.ClosePreviewScene(single);
                if (previous.IsValid()) EditorSceneManager.ClosePreviewScene(previous);
            }
        }

        [Test]
        public void PreparedBuildSelectsTransferSceneAndRetainsEarlierScenesInPortraitUniversalProject()
        {
            // A later Task may select its own transfer-enabled scene. Preserve and inspect T11
            // above; here validate the selected Task's build identity instead of forcing a rollback to T11.
            const string integratedPath = "Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity";
            const string interruptionPath = "Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity";
            const string physicsPath = "Assets/_Project/HapioMVP/Scenes/PhysicsBattle.unity";
            const string throwPath = "Assets/_Project/HapioMVP/Scenes/ThrowBattle.unity";
            const string fivePath = "Assets/_Project/HapioMVP/Scenes/FivePlayerBattle.unity";
            const string continuousPath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
            var enabled = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray();
            Assert.That(enabled, Has.Length.EqualTo(1));
            Assert.That(enabled[0], Is.EqualTo(ScenePath).Or.EqualTo(integratedPath).Or.EqualTo(interruptionPath).Or.EqualTo(physicsPath).Or.EqualTo(throwPath).Or.EqualTo(fivePath).Or.EqualTo(continuousPath));
            bool integrated = enabled[0] == integratedPath;
            bool interruption = enabled[0] == interruptionPath;
            bool physics = enabled[0] == physicsPath;
            bool releaseThrow = enabled[0] == throwPath;
            bool fivePlayer = enabled[0] == fivePath;
            bool continuous = enabled[0] == continuousPath;
            string expectedBuild = continuous ? "25" : fivePlayer ? "23" : releaseThrow ? "22" : physics ? "21" : interruption ? "20" : integrated ? "19" : "16";
            string expectedDesktop = continuous ? "com.wolfuraark.c6prototype.p4.desktop" : fivePlayer ? "com.wolfuraark.c6prototype.p3.desktop" : releaseThrow ? "com.wolfuraark.c6prototype.p2.desktop" : physics ? "com.wolfuraark.c6prototype.p1.desktop" : interruption ? "com.wolfuraark.c6prototype.t13.desktop" :
                integrated ? "com.wolfuraark.c6prototype.t12.desktop" : "com.wolfuraark.c6prototype.t11.desktop";
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path), Does.Contain(ScenePath));
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path), Does.Contain(PriorScenePath));
            Assert.That(EditorBuildSettings.scenes.Select(item => item.path), Does.Contain(BattleLoopScenePath));
            var selected = EditorSceneManager.OpenPreviewScene(enabled[0]);
            try
            {
                var game = Components<T10GameSession>(selected).Single();
                Assert.That(game.TransfersEnabled, Is.True);
                Assert.That(game.InterruptionHandlingEnabled, Is.EqualTo(interruption || physics || releaseThrow || fivePlayer || continuous));
                Assert.That(game.BuildIdentifier, Is.EqualTo(expectedBuild));
                Assert.That(Components<T10LobbySession>(selected).Single().BuildIdentifier, Is.EqualTo(expectedBuild));
            }
            finally { if (selected.IsValid()) EditorSceneManager.ClosePreviewScene(selected); }
            Assert.That(PlayerSettings.iOS.buildNumber, Is.EqualTo(expectedBuild));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
            Assert.That(PlayerSettings.iOS.targetDevice, Is.EqualTo(iOSTargetDevice.iPhoneAndiPad));
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS), Is.EqualTo("com.wolfuraark.c6prototype"));
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone), Is.EqualTo(expectedDesktop));
        }

        private T AssertSingleRoot<T>(GameObject root) where T : Component
        {
            var found = Components<T>(scene);
            Assert.That(found, Has.Length.EqualTo(1), typeof(T).Name);
            Assert.That(found[0].gameObject, Is.SameAs(root), typeof(T).Name);
            return found[0];
        }
        private static T[] Components<T>(Scene value) where T : Component => value.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static void AssertReference(Object owner, string field, Object expected)
        {
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.FindProperty(field);
                Assert.That(property, Is.Not.Null, field);
                Assert.That(property.objectReferenceValue, Is.SameAs(expected), owner.name + "." + field);
            }
        }
    }
}
