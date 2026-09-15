using System;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Battle;
using C6.Prototype.Lobby;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Prototype.GameSync.Tests
{
    // These assertions inspect saved wiring. They do not claim transport, rendering, or device execution.
    public sealed class InterruptionSceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity";
        private const string PriorScenePath = "Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity";
        private const string TransferScenePath = "Assets/_Project/HapioMVP/Scenes/OrbTransferBattle.unity";
        private const string BattleLoopScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLoop.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private Scene scene;

        [SetUp]
        public void OpenSavedInterruptionSceneIndependently()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "Run C6/T13/Prepare Interruption Battle before verifying its saved scene.");
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        }

        [TearDown]
        public void CloseOnlyThePreviewOpenedByThisTest()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test]
        public void InterruptionModeRetainsTheSingleRootTransferPipelineAndCanonicalRules()
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
            Assert.That(game.ReachableEdgeTransferDistance, Is.True);
            Assert.That(game.InterruptionHandlingEnabled, Is.True);
            Assert.That(game.BuildIdentifier, Is.EqualTo("20"));
            Assert.That(root.GetComponent<T10LobbySession>().BuildIdentifier, Is.EqualTo("20"));
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
            var transfer = EditorSceneManager.OpenPreviewScene(TransferScenePath);
            var single = EditorSceneManager.OpenPreviewScene(BattleLoopScenePath);
            try
            {
                var oldGame = Components<T10GameSession>(previous).Single();
                Assert.That(oldGame.TransfersEnabled, Is.True);
                Assert.That(oldGame.ReachableEdgeTransferDistance, Is.True);
                Assert.That(oldGame.InterruptionHandlingEnabled, Is.False);
                Assert.That(oldGame.BuildIdentifier, Is.EqualTo("19"));
                var olderGame = Components<T10GameSession>(transfer).Single();
                Assert.That(olderGame.TransfersEnabled, Is.True);
                Assert.That(olderGame.InterruptionHandlingEnabled, Is.False);
                Assert.That(olderGame.ReachableEdgeTransferDistance, Is.False);
                Assert.That(olderGame.BuildIdentifier, Is.EqualTo("16"));
                Assert.That(Components<T10GameSession>(single), Is.Empty);
                Assert.That(Components<T09BattleController>(single), Has.Length.EqualTo(1));
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(PriorScenePath)));
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(BattleLoopScenePath)));
            }
            finally
            {
                if (single.IsValid()) EditorSceneManager.ClosePreviewScene(single);
                if (transfer.IsValid()) EditorSceneManager.ClosePreviewScene(transfer);
                if (previous.IsValid()) EditorSceneManager.ClosePreviewScene(previous);
            }
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
        private static void AssertReference(UnityEngine.Object owner, string field, UnityEngine.Object expected)
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
