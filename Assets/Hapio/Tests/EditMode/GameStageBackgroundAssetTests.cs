using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        // #504: when the Editor first reads a URP material's properties, URP rewrites its hidden _MainTex/_Color from
        // _BaseMap/_BaseColor. A different texture copy dirties the material, so the next save changes the .mat; a different
        // color copy is written with the next save for any other reason. A material read this session may already show
        // the rewritten values, so read the saved file itself.
        [TestCase(GameScenePaths.Battle)]
        [TestCase("Assets/Lab/Scenes/Lab.unity", Category = "LabOnly")]
        public void EveryStudioMaterialIsAppliedAndSavedWithTheCopiesTheEditorRewrites(string path)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                LabBackgroundStudio[] studios = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabBackgroundStudio>(true)).ToArray();
                Assert.That(studios, Has.Length.EqualTo(path == GameScenePaths.Battle ? 3 : 1));
                foreach (LabBackgroundStudio studio in studios)
                {
                    Assert.That(studio.UnappliedImages(), Is.Empty, studio.name);
                    List<Material> materials = studio.ImageMaterials();
                    Assert.That(materials, Has.Count.EqualTo(3), studio.name + ": ground, surround and board backing");
                    foreach (Material material in materials)
                    {
                        string file = AssetDatabase.GetAssetPath(material);
                        Assert.That(material.HasProperty("_MainTex") && material.HasProperty("_Color"), Is.True,
                            file + " keeps the URP Lit/Unlit hidden copies");
                        string text = File.ReadAllText(file).Replace("\r\n", "\n");
                        Assert.That(SavedTexture(text, "_MainTex", file), Is.EqualTo(SavedTexture(text, "_BaseMap", file)), file);
                        Assert.That(SavedColor(text, "_Color", file), Is.EqualTo(UrpColorCopy(material.shader, SavedColor(text, "_BaseColor", file))),
                            file + " _Color");
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void ApplyImageWritesTheHiddenCopiesSoUrpsOwnCopyChangesNothing()
        {
            var png = new Texture2D(2, 2);
            // GetColor returns the first two a float step away from what SetColor stored; the last comes back exactly.
            Color[] colors = { new Color(.55f, .29f, .81f, 1f), new Color(.17923617f, .4977885f, .5250866f, 1f), new Color(.8f, .7f, .6f, 1f) };
            try
            {
                foreach (string shaderName in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" })
                    foreach (Color color in colors)
                    {
                        var material = new Material(Shader.Find(shaderName));
                        string context = shaderName + " " + color.ToString("F7");
                        try
                        {
                            // Stale copies, the way the image button used to leave them.
                            material.SetTexture("_MainTex", Texture2D.blackTexture);
                            material.SetTextureScale("_MainTex", Vector2.one);
                            material.SetColor("_Color", Color.red);
                            LabBackgroundStudio.ApplyImage(material, png, color, new Vector2(5, 4), new Vector2(.25f, .5f));
                            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                            {
                                Assert.That(material.GetTexture(property), Is.SameAs(png), context + " " + property);
                                Assert.That(material.GetTextureScale(property), Is.EqualTo(new Vector2(5, 4)), context + " " + property);
                                Assert.That(material.GetTextureOffset(property), Is.EqualTo(new Vector2(.25f, .5f)), context + " " + property);
                            }
                            Color before = material.GetColor("_Color");
                            Assert.That(before.r, Is.EqualTo(color.r).Within(1e-5f), context);
                            // The copy URP's BaseShaderGUI.SetMaterialKeywords makes when the Editor first reads the material.
                            // Had ApplyImage written `color` itself, this would change the drifting colors and dirty the asset.
                            material.SetColor("_Color", material.GetColor("_BaseColor"));
                            Assert.That(Bits(material.GetColor("_Color")), Is.EqualTo(Bits(before)), context);
                        }
                        finally { Object.DestroyImmediate(material); }
                    }
            }
            finally { Object.DestroyImmediate(png); }
        }

        [Test]
        public void TheStudioListsOnlyTheSlotsWhoseSavedMaterialDiffers()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(GameScenePaths.Battle, OpenSceneMode.Additive);
            try
            {
                LabBackgroundStudio studio = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameStageBackgrounds>(true)).Single().Resolve(2).studio;
                Assert.That(studio.UnappliedImages(), Is.Empty);
                Texture2D ground = studio.groundPng, backing = studio.boardBackgroundPng;
                Vector2 paperTiling = studio.paperTiling;
                Color backingColor = studio.boardBackgroundColor;
                // #487's case: the studio names a floor that the saved ground material does not show.
                studio.groundPng = studio.paperPng;
                Assert.That(studio.UnappliedImages(), Is.EqualTo(new[] { LabBackgroundStudio.GroundSlot }));
                studio.groundPng = ground;
                studio.paperTiling = paperTiling + Vector2.one;
                Assert.That(studio.UnappliedImages(), Is.EqualTo(new[] { LabBackgroundStudio.SurroundSlot }));
                studio.paperTiling = paperTiling;
                studio.boardBackgroundColor = Color.red;
                Assert.That(studio.UnappliedImages(), Is.EqualTo(new[] { LabBackgroundStudio.BoardBackingSlot }));
                studio.boardBackgroundColor = backingColor;
                // Without its own PNG the backing falls back to the ground PNG, exactly as the image button does.
                Assert.That(backing, Is.SameAs(ground));
                studio.boardBackgroundPng = null;
                Assert.That(studio.UnappliedImages(), Is.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static string SavedTexture(string text, string name, string file)
        {
            Match match = Regex.Match(text, "\n    - " + name
                + ":\n        m_Texture: (\\{[^}]*\\})\n        m_Scale: (\\{[^}]*\\})\n        m_Offset: (\\{[^}]*\\})\n");
            Assert.That(match.Success, Is.True, file + " saves " + name);
            return match.Groups[1].Value + " " + match.Groups[2].Value + " " + match.Groups[3].Value;
        }

        /// <summary>The `_Color` URP copies from this saved `_BaseColor`: what GetColor returns after SetColor (#504).</summary>
        private static float[] UrpColorCopy(Shader shader, float[] baseColor)
        {
            var probe = new Material(shader);
            try
            {
                probe.SetColor("_BaseColor", new Color(baseColor[0], baseColor[1], baseColor[2], baseColor[3]));
                Color copy = probe.GetColor("_BaseColor");
                return new[] { copy.r, copy.g, copy.b, copy.a };
            }
            finally { Object.DestroyImmediate(probe); }
        }

        private static int[] Bits(Color color) => new[] { color.r, color.g, color.b, color.a }
            .Select(channel => System.BitConverter.ToInt32(System.BitConverter.GetBytes(channel), 0)).ToArray();

        private static float[] SavedColor(string text, string name, string file)
        {
            Match match = Regex.Match(text, "\n    - " + name + ": \\{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\\}\n");
            Assert.That(match.Success, Is.True, file + " saves " + name);
            return Enumerable.Range(1, 4).Select(group => float.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
