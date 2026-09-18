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
    // Saved wiring / Config behaviour only. Root-owned PlayMode and player runs verify actual collisions.
    public sealed class PhysicsBattleMonsterTests
    {
        const string ScenePath = "Assets/_Project/HapioMVP/Scenes/PhysicsBattle.unity";
        const string PreviousScene = "Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity";
        const string PrefabPath = "Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab";
        const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        Scene scene;

        [SetUp] public void OpenPreparedPhysicsScene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "Run C6/Next Phase/P1/Prepare Physics Battle before these saved-scene checks.");
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        }
        [TearDown] public void CloseOnlyThisPreview()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test] public void NewSceneOptsInToPhysicsWithoutDuplicatingTransportOrGameplayRoots()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            Assert.That(layout.Config, Is.SameAs(AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath)));
            Assert.That(Components<T09BattleController>(scene).Single().OrbPhysicsEnabled, Is.True);
            var game = Components<T10GameSession>(scene).Single();
            Assert.That(game.BuildIdentifier, Is.EqualTo("21"));
            Assert.That(game.TransfersEnabled && game.ReachableEdgeTransferDistance && game.InterruptionHandlingEnabled, Is.True);
            Assert.That(Components<T10LobbySession>(scene).Single().BuildIdentifier, Is.EqualTo("21"));
            Assert.That(Components<DirectConnectionSession>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<GameRuntimeConfig>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<OrbPointerInput>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<Camera>(scene), Has.Length.EqualTo(2));
            Assert.That(Components<T09BattleController>(scene).Single().gameObject, Is.SameAs(layout.gameObject));
        }

        [Test] public void OneConnectedPrefabReplacesBothTheOldDummyVisualAndOldStandaloneTarget()
        {
            var monster = Components<BenchmarkMonster>(scene).Single();
            Assert.That(Components<MonsterHitTarget>(scene).Single(), Is.SameAs(monster.Target));
            Assert.That(Components<Transform>(scene).Any(t => t.name == "T04 Training Dummy - Visual Only"), Is.False);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(monster.gameObject), Is.EqualTo(PrefabPath));
            AssertStructure(monster);
            using (var controller = new SerializedObject(Components<T09BattleController>(scene).Single()))
                Assert.That(controller.FindProperty("target").objectReferenceValue, Is.SameAs(monster.Target));
            using (var serialized = new SerializedObject(monster))
                Assert.That(serialized.FindProperty("runtimeLayout").objectReferenceValue,
                    Is.SameAs(Components<SplitScreenLayout>(scene).Single()));
        }

        [Test] public void PrefabHasSeparateVisualAndSingleStaticHitboxWithNoLocalHpService()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var monster = prefab.GetComponent<BenchmarkMonster>();
            AssertStructure(monster);
            Assert.That(prefab.GetComponents<Component>().Select(c => c.GetType()).ToArray(),
                Is.EquivalentTo(new[] { typeof(Transform), typeof(MonsterHitTarget), typeof(BenchmarkMonster) }));
            Assert.That(monster.SavedConfig, Is.SameAs(AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath)));
            using (var serialized = new SerializedObject(monster))
                Assert.That(serialized.FindProperty("runtimeLayout").objectReferenceValue, Is.Null,
                    "The reusable prefab may not store a reference to another saved scene.");
        }

        [Test] public void BaselineGeometryMatchesConfigAndTheFormerHostTargetSpecification()
        {
            var monster = Components<BenchmarkMonster>(scene).Single();
            var config = monster.Config;
            Assert.That(monster.Target.TargetId, Is.EqualTo(config.MonsterTargetId));
            Assert.That(monster.transform.position, Is.EqualTo(config.MonsterPosition));
            Assert.That(((BoxCollider)monster.Hitbox).center, Is.EqualTo(config.MonsterHitboxCenter));
            // The saved P1 box keeps the former target specification. BenchmarkMonster applies the shared Config
            // size when it runs (RuntimeCopyChangesGeometry... covers that path).
            Assert.That(((BoxCollider)monster.Hitbox).size, Is.EqualTo(new Vector3(1.2f, 2.6f, .65f)));
            Assert.That(config.MonsterTargetId, Is.EqualTo("dev-training-dummy"));
            Assert.That(config.MonsterHitboxCenter, Is.EqualTo(new Vector3(0f, 1.4f, 0f)));
            // #16: the shared Config is sized for Jangsanbeom's capsule (X = diameter, Y = height).
            Assert.That(config.MonsterHitboxSize, Is.EqualTo(new Vector3(1.9f, 2.8f, 1.9f)));
        }

        [Test] public void RuntimeCopyChangesGeometryWithoutEditingTheSavedConfigOrAddingHp()
        {
            var original = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            string before = JsonUtility.ToJson(original);
            var copy = Object.Instantiate(original);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"monsterTargetId\":\"p1-test-target\",\"monsterPosition\":{\"x\":0.5,\"y\":0.1,\"z\":1},"
                    + "\"monsterHitboxCenter\":{\"x\":0,\"y\":1.2,\"z\":0.1},\"monsterHitboxSize\":{\"x\":1.6,\"y\":2.1,\"z\":0.8}}", copy);
                var layout = Components<SplitScreenLayout>(scene).Single();
                layout.Configure(copy, layout.BattleCamera, layout.OrbCamera);
                var monster = Components<BenchmarkMonster>(scene).Single();
                monster.ApplyConfiguration();
                Assert.That(monster.Config, Is.SameAs(copy));
                Assert.That(monster.Target.TargetId, Is.EqualTo("p1-test-target"));
                Assert.That(monster.transform.position, Is.EqualTo(copy.MonsterPosition));
                Assert.That(((BoxCollider)monster.Hitbox).center, Is.EqualTo(copy.MonsterHitboxCenter));
                Assert.That(((BoxCollider)monster.Hitbox).size, Is.EqualTo(copy.MonsterHitboxSize));
                Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
                Assert.That(monster.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test] public void PreviousSavedSceneRetainsBuild20OldColliderAndDisabledOrbPhysics()
        {
            var previous = EditorSceneManager.OpenPreviewScene(PreviousScene);
            try
            {
                Assert.That(Components<T10GameSession>(previous).Single().BuildIdentifier, Is.EqualTo("20"));
                Assert.That(Components<T09BattleController>(previous).Single().OrbPhysicsEnabled, Is.False);
                Assert.That(Components<BenchmarkMonster>(previous), Is.Empty);
                Assert.That(Components<MonsterHitTarget>(previous).Single().GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(Components<Transform>(previous).Count(t => t.name == "T04 Training Dummy - Visual Only"), Is.EqualTo(1));
            }
            finally { if (previous.IsValid()) EditorSceneManager.ClosePreviewScene(previous); }
        }

        [Test] public void EveryCopiedSceneReferenceStaysInsideTheNewScene()
        {
            foreach (var component in Components<Component>(scene))
            {
                Assert.That(component, Is.Not.Null);
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        var gameObject = reference is Component c ? c.gameObject : reference as GameObject;
                        if (gameObject != null && gameObject.scene.IsValid())
                            Assert.That(gameObject.scene, Is.EqualTo(scene), component.name + "." + property.propertyPath);
                    }
                }
            }
        }

        static void AssertStructure(BenchmarkMonster monster)
        {
            Assert.That(monster, Is.Not.Null);
            Assert.That(monster.Visual.name, Is.EqualTo("Visual"));
            Assert.That(monster.Visual.parent, Is.SameAs(monster.transform));
            Assert.That(monster.Hitbox.gameObject.name, Is.EqualTo("Hitbox"));
            Assert.That(monster.Hitbox.transform.parent, Is.SameAs(monster.transform));
            Assert.That(monster.Hitbox.isTrigger, Is.False);
            Assert.That(monster.GetComponentsInChildren<Collider>(true), Is.EqualTo(new Collider[] { monster.Hitbox }));
            Assert.That(monster.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(monster.Visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(monster.Visual.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
            Assert.That(monster.Hitbox.GetComponentInParent<MonsterHitTarget>(true), Is.SameAs(monster.Target));
            Assert.That(monster.Hitbox.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("C6Battle")));
        }
        static T[] Components<T>(Scene value) where T : Component => value.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
