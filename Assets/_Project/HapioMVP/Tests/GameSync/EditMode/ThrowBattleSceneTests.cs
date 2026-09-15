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
    public sealed class ThrowBattleSceneTests
    {
        const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ThrowBattle.unity";
        const string PreviousScene = "Assets/_Project/HapioMVP/Scenes/PhysicsBattle.unity";
        const string PrefabPath = "Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab";
        Scene scene;
        [SetUp] public void OpenPreparedP2Scene()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null,
                "Run C6/Next Phase/P2/Prepare Release Throw Battle first.");
            scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        }
        [TearDown] public void CloseOnlyThisPreview()
        { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }

        [Test] public void ReleaseModeRetainsOneTwoParticipantPipelineAndSharedConfig()
        {
            var layout = Components<SplitScreenLayout>(scene).Single();
            var controller = Components<T09BattleController>(scene).Single();
            Assert.That(controller.OrbPhysicsEnabled && controller.ReleaseThrowsEnabled, Is.True);
            Assert.That(Components<T10GameSession>(scene).Single().BuildIdentifier, Is.EqualTo("22"));
            Assert.That(Components<T10LobbySession>(scene).Single().BuildIdentifier, Is.EqualTo("22"));
            Assert.That(Components<DirectConnectionSession>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<OrbPointerInput>(scene), Has.Length.EqualTo(1));
            Assert.That(layout.Config, Is.SameAs(AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(
                "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset")));
            Assert.That(layout.Config.UpperFraction, Is.EqualTo(.55f));
            Assert.That(Components<Camera>(scene), Has.Length.EqualTo(2));
        }

        [Test] public void PreservedMonsterPrefabAndNewFloorRemainDifferentCollisionKinds()
        {
            var monster = Components<BenchmarkMonster>(scene).Single();
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(monster.gameObject), Is.EqualTo(PrefabPath));
            Assert.That(Components<MonsterHitTarget>(scene), Has.Length.EqualTo(1));
            var floor = Components<ThrowBattleFloor>(scene).Single();
            var config = Components<SplitScreenLayout>(scene).Single().Config;
            Assert.That(floor.Hitbox.isTrigger, Is.False);
            Assert.That(floor.Hitbox.attachedRigidbody, Is.Null);
            Assert.That(floor.Hitbox.GetComponentInParent<MonsterHitTarget>(), Is.Null);
            Assert.That(floor.Hitbox.size, Is.EqualTo(config.ThrowFloorSize));
            Assert.That(floor.transform.position.y + floor.Hitbox.size.y * .5f,
                Is.EqualTo(config.ThrowFloorY).Within(.0001f));
            Assert.That(floor.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("C6Battle")));
            Assert.That(floor.Hitbox.sharedMaterial, Is.Null,
                "The contact material is runtime-owned and must not become a second saved tuning asset.");
        }

        [Test] public void FramingPreservesSavedPerspectivePoseBaselineAndBothViewportRoles()
        {
            var framing = Components<ThrowBattleFraming>(scene).Single();
            var layout = Components<SplitScreenLayout>(scene).Single();
            Assert.That(framing.BattleCamera, Is.SameAs(layout.BattleCamera));
            Assert.That(layout.BattleCamera.orthographic, Is.False);
            Assert.That(layout.OrbCamera.orthographic, Is.True);
            var previous = EditorSceneManager.OpenPreviewScene(PreviousScene);
            try
            {
                var oldLayout = Components<SplitScreenLayout>(previous).Single();
                Assert.That(framing.BaselinePosition, Is.EqualTo(oldLayout.BattleCamera.transform.position));
                Assert.That(framing.BaselineRotation, Is.EqualTo(oldLayout.BattleCamera.transform.rotation));
                Assert.That(framing.BaselineFieldOfView, Is.EqualTo(oldLayout.BattleCamera.fieldOfView));
                Assert.That(Components<T09BattleController>(previous).Single().ReleaseThrowsEnabled, Is.False);
                Assert.That(Components<T10GameSession>(previous).Single().BuildIdentifier, Is.EqualTo("21"));
                Assert.That(Components<ThrowBattleFraming>(previous), Is.Empty);
                Assert.That(Components<ThrowBattleFloor>(previous), Is.Empty);
            }
            finally { if (previous.IsValid()) EditorSceneManager.ClosePreviewScene(previous); }
        }

        [Test] public void AllSceneReferencesResolveInsideNewSceneIncludingPrefabOverrides()
        {
            foreach (var component in Components<Component>(scene))
            {
                Assert.That(component, Is.Not.Null);
                using (var serialized = new SerializedObject(component))
                {
                    var p = serialized.GetIterator();
                    while (p.Next(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var referenced = p.objectReferenceValue is Component c ? c.gameObject : p.objectReferenceValue as GameObject;
                        if (referenced != null && referenced.scene.IsValid())
                            Assert.That(referenced.scene, Is.EqualTo(scene), component.name + "." + p.propertyPath);
                    }
                }
            }
        }

        [TestCase(390f, 844f)]
        [TestCase(560f, 746f)]
        [TestCase(1206f, 2622f)]
        [TestCase(1488f, 2266f)]
        public void PerspectiveFitPlacesEveryTargetCornerInsideReservedHudFreeRegion(float width, float height)
        {
            var framing = Components<ThrowBattleFraming>(scene).Single();
            // Unequal top/bottom reservations expose incorrect center-only fits.
            var reserved = new Rect(.04f, .14f, .92f, .37f);
            float aspect = width / (height * .55f);
            var bounds = new Bounds(new Vector3(0f, 1.4f, 0f), new Vector3(1.75f, 2.6f, .75f));
            Assert.That(ThrowBattleFraming.TrySolvePosition(bounds, framing.BaselineRotation,
                framing.BaselineFieldOfView, aspect, reserved, framing.BaselinePosition, out var position), Is.True);
            var rotation = Quaternion.Inverse(framing.BaselineRotation);
            float tangent = Mathf.Tan(framing.BaselineFieldOfView * Mathf.Deg2Rad * .5f);
            for (int i = 0; i < 8; i++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var cameraPoint = rotation * (point - position);
                Assert.That(cameraPoint.z, Is.GreaterThan(.3f));
                float x = .5f + cameraPoint.x / (2f * cameraPoint.z * tangent * aspect);
                float y = .5f + cameraPoint.y / (2f * cameraPoint.z * tangent);
                Assert.That(x, Is.InRange(reserved.xMin - .00001f, reserved.xMax + .00001f));
                Assert.That(y, Is.InRange(reserved.yMin - .00001f, reserved.yMax + .00001f));
            }
        }

        [TestCase(0f, .2f, .9f, 0f)]
        [TestCase(-.1f, .2f, .9f, .3f)]
        [TestCase(.2f, .2f, 1f, .3f)]
        public void InvalidHudSpaceCannotProduceACameraFit(float x, float y, float width, float height)
        {
            Assert.That(ThrowBattleFraming.TrySolvePosition(new Bounds(Vector3.zero, Vector3.one), Quaternion.identity,
                42f, 1f, new Rect(x, y, width, height), new Vector3(0, 0, -9), out _), Is.False);
        }
        static T[] Components<T>(Scene value) where T : Component => value.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
