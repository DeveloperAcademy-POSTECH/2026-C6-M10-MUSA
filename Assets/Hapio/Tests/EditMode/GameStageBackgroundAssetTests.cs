using System.Linq;
using C6Lab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Tests
{
    public sealed class GameStageBackgroundAssetTests
    {
        [Test]
        public void StageTwoUsesIsolatedArtworkAndRestoresTheDefaultForOtherStages()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(GameScenePaths.Battle, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameStageBackgrounds>(true)).Single();
                var baseline = controller.defaultBackground;
                var night = controller.Resolve(2);
                // #447: stage 3 has its own sunset set too; stage 1 and unknown stages keep the default.
                var sunset = controller.Resolve(3);
                Assert.That(night, Is.Not.SameAs(baseline));
                Assert.That(sunset, Is.Not.SameAs(baseline));
                Assert.That(sunset, Is.Not.SameAs(night));
                Assert.That(controller.Resolve(1), Is.SameAs(baseline));
                Assert.That(controller.Resolve(99), Is.SameAs(baseline));
                Assert.That(sunset.studio.target, Is.SameAs(baseline.studio.target));
                Assert.That(sunset.studio.collisionFloor, Is.SameAs(baseline.studio.collisionFloor));
                Assert.That(sunset.studio.boardCamera, Is.SameAs(baseline.studio.boardCamera));
                Assert.That(night.studio.target, Is.SameAs(baseline.studio.target));
                Assert.That(night.studio.collisionFloor, Is.SameAs(baseline.studio.collisionFloor));
                Assert.That(night.studio.boardCamera, Is.SameAs(baseline.studio.boardCamera));
                foreach (int stage in new[] { 1, 2, 3, 2, 1 })
                {
                    controller.ApplyStage(stage);
                    Assert.That(baseline.studio.gameObject.activeSelf, Is.EqualTo(stage == 1));
                    Assert.That(baseline.ground.activeSelf, Is.EqualTo(stage == 1));
                    Assert.That(baseline.boardBacking.activeSelf, Is.EqualTo(stage == 1));
                    Assert.That(night.studio.gameObject.activeSelf, Is.EqualTo(stage == 2));
                    Assert.That(night.ground.activeSelf, Is.EqualTo(stage == 2));
                    Assert.That(night.boardBacking.activeSelf, Is.EqualTo(stage == 2));
                    Assert.That(sunset.studio.gameObject.activeSelf, Is.EqualTo(stage == 3));
                    Assert.That(sunset.ground.activeSelf, Is.EqualTo(stage == 3));
                    Assert.That(sunset.boardBacking.activeSelf, Is.EqualTo(stage == 3));
                }
                var baselineMaterials = baseline.studio.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).ToArray();
                foreach (var renderer in night.studio.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, renderer.name);
                        Assert.That(baselineMaterials, Has.None.SameAs(material), renderer.name);
                        Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Hapio/Art/Environment/Stage02/"));
                    }
                Assert.That(night.ground.GetComponent<Renderer>().sharedMaterial,
                    Is.Not.SameAs(baseline.ground.GetComponent<Renderer>().sharedMaterial));
                Assert.That(night.boardBacking.GetComponent<Renderer>().sharedMaterial,
                    Is.Not.SameAs(baseline.boardBacking.GetComponent<Renderer>().sharedMaterial));
                var nightMaterials = night.studio.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).ToArray();
                foreach (var renderer in sunset.studio.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, renderer.name);
                        Assert.That(baselineMaterials, Has.None.SameAs(material), renderer.name);
                        Assert.That(nightMaterials, Has.None.SameAs(material), renderer.name);
                    }
                Assert.That(sunset.ground.GetComponent<Renderer>().sharedMaterial,
                    Is.Not.SameAs(baseline.ground.GetComponent<Renderer>().sharedMaterial));
                Assert.That(sunset.boardBacking.GetComponent<Renderer>().sharedMaterial,
                    Is.Not.SameAs(baseline.boardBacking.GetComponent<Renderer>().sharedMaterial));
                Assert.That(sunset.ground.transform.parent, Is.SameAs(baseline.ground.transform.parent));
                Assert.That(sunset.boardBacking.transform.parent, Is.SameAs(baseline.boardBacking.transform.parent));
                Assert.That(night.ground.transform.parent, Is.SameAs(baseline.ground.transform.parent));
                Assert.That(night.boardBacking.transform.parent, Is.SameAs(baseline.boardBacking.transform.parent));
                foreach (string componentName in new[] { "Camera", "Canvas", "Rigidbody", "Collider" })
                    Assert.That(night.studio.GetComponentsInChildren<Component>(true)
                        .Any(component => component != null && component.GetType().Name == componentName), Is.False, componentName);
                Assert.That(night.studio.GetComponentsInChildren<Collider>(true), Is.Empty);
                foreach (string name in new[] { "DistantMountains", "PineGrove", "RockAndPines", "Rocks", "Grass", "GateAndWalls" })
                {
                    var group = night.studio.decorations.Single(d => d.png != null && d.png.name == name);
                    Assert.That(AssetDatabase.GetAssetPath(group.png), Does.StartWith("Assets/Hapio/Art/Environment/Stage02/Textures/"));
                    foreach (var card in group.cards.Where(card => card != null))
                        {
                        var artwork = card.GetComponent<LabEnvironmentCard>();
                        if (artwork == null) Assert.That(card.gameObject.activeSelf, Is.False, "Retained unused card");
                        else Assert.That(artwork.png, Is.SameAs(group.png));
                        }
                }
                Assert.That(night.studio.paperPng.name, Is.EqualTo("Sky_Hanji"));
                Assert.That(night.studio.groundPng.name, Is.EqualTo("Stage02_Ground_Yagwanggwi"), "#487: stage 2's blue-grey ground.");
                Assert.That(sunset.studio.groundPng.name, Is.EqualTo("Stage03_Ground_Jigwi"));
                // A player draws the saved material, not the studio field: both must name the same texture (#487).
                foreach (var set in new[] { baseline, night, sunset })
                {
                    Texture backing = set.studio.boardBackgroundPng != null ? set.studio.boardBackgroundPng : set.studio.groundPng;
                    Assert.That(set.ground.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(set.studio.groundPng),
                        set.studio.name + " ground");
                    Assert.That(set.boardBacking.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(backing),
                        set.studio.name + " board backing");
                }
                Assert.That(night.studio.boardBackgroundPng, Is.SameAs(night.studio.groundPng));
                Assert.That(night.studio.decorations.Single(d => d.png != null && d.png.name == "Moon").count, Is.EqualTo(1));
                Assert.That(night.studio.decorations.Single(d => d.png != null && d.png.name == "RockAndPines").visible, Is.False);
                Assert.That(night.studio.decorations.Single(d => d.png != null && d.png.name == "Lantern_Post_Yellow").count, Is.EqualTo(2));
                Assert.That(night.studio.decorations.Single(d => d.png != null && d.png.name == "Hanok_Shoes").count, Is.EqualTo(2));
                Assert.That(night.studio.decorations.Single(d => d.png != null && d.png.name == "GhostFlame").count, Is.EqualTo(5));
                Assert.That(night.studio.transform.Find("Generated Background").Cast<Transform>().Single(child => child.name == "Butterflies / 배경 나비").gameObject.activeSelf, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
