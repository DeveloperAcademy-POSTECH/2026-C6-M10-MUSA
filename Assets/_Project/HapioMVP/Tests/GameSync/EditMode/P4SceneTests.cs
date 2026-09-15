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
    // Saved wiring only. These checks do not claim network execution, live rendering, or device input.
    public sealed class P4SceneTests
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string PreviousPath = "Assets/_Project/HapioMVP/Scenes/FivePlayerBattle.unity";
        private const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        private const string MonsterPath = "Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab";
        private Scene scene;
        [SetUp] public void OpenSavedP4Scene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "Prepare P4 ContinuousTransferBattle before verifying saved wiring.");
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        }
        [TearDown] public void CloseOnlyThisPreview()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test]
        public void EveryP4ServiceUsesOneSharedRootAndOptInCapacityFive()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            var root = layout.gameObject;
            var game = SingleOnRoot<T10GameSession>(root);
            var lobby = SingleOnRoot<T10LobbySession>(root);
            var controller = SingleOnRoot<T09BattleController>(root);
            SingleOnRoot<DirectConnectionSession>(root);
            SingleOnRoot<GameRuntimeConfig>(root);
            SingleOnRoot<T09Hud>(root);
            SingleOnRoot<OrbPointerInput>(root);
            SingleOnRoot<T10LobbyHud>(root);
            SingleOnRoot<T10LobbyController>(root);
            SingleOnRoot<ThrowBattleFraming>(root);
            Assert.That(game.MaximumParticipants, Is.EqualTo(5));
            Assert.That(lobby.MaximumParticipants, Is.EqualTo(5));
            Assert.That(lobby.ProtocolVersion, Is.EqualTo(24));
            Assert.That(controller.MaximumParticipants, Is.EqualTo(5));
            Assert.That(game.BuildIdentifier, Is.EqualTo("24"));
            Assert.That(lobby.BuildIdentifier, Is.EqualTo("24"));
            Assert.That(game.TransfersEnabled && game.ReachableEdgeTransferDistance && game.InterruptionHandlingEnabled, Is.True);
            Assert.That(controller.OrbPhysicsEnabled && controller.ReleaseThrowsEnabled, Is.True);
            Assert.That(game.ContinuousTransfersEnabled && lobby.ContinuousTransfersEnabled && controller.ContinuousTransfersEnabled, Is.True);
            Assert.That(layout.Config, Is.SameAs(AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath)));
            Assert.That(layout.Config.StaminaStart, Is.EqualTo(100));
            Assert.That(layout.Config.GenerateCost, Is.EqualTo(20));
            Assert.That(layout.Config.StaminaHitRecovery, Is.EqualTo(5));
            Assert.That(Components<P4ContinuousTransferProbe>(scene).Single().gameObject, Is.SameAs(root));
            Assert.That(Components<P3MultiplayerProbe>(scene), Is.Empty);
        }
        [Test]
        public void CopiedP4CamerasMonsterFloorAndEverySceneReferenceAreLocal()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            var cameras = Components<Camera>(scene);
            Assert.That(cameras, Has.Length.EqualTo(2));
            Assert.That(cameras, Does.Contain(layout.BattleCamera));
            Assert.That(cameras, Does.Contain(layout.OrbCamera));
            Assert.That(layout.BattleCamera.orthographic, Is.False);
            Assert.That(layout.OrbCamera.orthographic, Is.True);
            var monster = Components<BenchmarkMonster>(scene).Single();
            Assert.That(Components<MonsterHitTarget>(scene).Single().gameObject, Is.SameAs(monster.gameObject));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(monster.gameObject),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath)));
            Assert.That(monster.SavedConfig, Is.SameAs(layout.Config));
            Assert.That(monster.Hitbox.isTrigger, Is.False);
            Assert.That(monster.Visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            var floor = Components<ThrowBattleFloor>(scene).Single();
            Assert.That(floor.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(floor.GetComponent<BoxCollider>().isTrigger, Is.False);
            Assert.That(floor.GetComponent<MonsterHitTarget>(), Is.Null);
            Assert.That(Components<AttackLaunchFrame>(scene), Has.Length.EqualTo(1));
            foreach (var component in Components<Component>(scene))
            {
                Assert.That(component, Is.Not.Null, "No copied script may be missing.");
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        var referenced = reference is Component item ? item.gameObject : reference as GameObject;
                        if (referenced != null && referenced.scene.IsValid())
                            Assert.That(referenced.scene, Is.EqualTo(scene), component.name + "." + property.propertyPath);
                    }
                }
            }
        }
        [Test]
        public void PreviousP3SceneKeepsItsManualTransfersAndFivePlayerIdentity()
        {
            var prior = EditorSceneManager.OpenPreviewScene(PreviousPath);
            try
            {
                var game = Components<T10GameSession>(prior).Single();
                var lobby = Components<T10LobbySession>(prior).Single();
                var controller = Components<T09BattleController>(prior).Single();
                Assert.That(game.BuildIdentifier, Is.EqualTo("23"));
                Assert.That(lobby.BuildIdentifier, Is.EqualTo("23"));
                Assert.That(game.MaximumParticipants, Is.EqualTo(5));
                Assert.That(lobby.MaximumParticipants, Is.EqualTo(5));
                Assert.That(lobby.ProtocolVersion, Is.EqualTo(23));
                Assert.That(controller.MaximumParticipants, Is.EqualTo(5));
                Assert.That(controller.OrbPhysicsEnabled && controller.ReleaseThrowsEnabled, Is.True);
                Assert.That(game.ContinuousTransfersEnabled || lobby.ContinuousTransfersEnabled || controller.ContinuousTransfersEnabled, Is.False);
                Assert.That(Components<P3MultiplayerProbe>(prior), Has.Length.EqualTo(1));
                Assert.That(Components<P4ContinuousTransferProbe>(prior), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(PreviousPath)));
                Assert.That(Components<SplitScreenLayout>(prior).Single().Config,
                    Is.SameAs(Components<SplitScreenLayout>(scene).Single().Config));
            }
            finally { if (prior.IsValid()) EditorSceneManager.ClosePreviewScene(prior); }
        }
        private T SingleOnRoot<T>(GameObject root) where T : Component
        {
            var value = Components<T>(scene);
            Assert.That(value, Has.Length.EqualTo(1), typeof(T).Name);
            Assert.That(value[0].gameObject, Is.SameAs(root), typeof(T).Name);
            return value[0];
        }
        private static T[] Components<T>(Scene value) where T : Component => value.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
