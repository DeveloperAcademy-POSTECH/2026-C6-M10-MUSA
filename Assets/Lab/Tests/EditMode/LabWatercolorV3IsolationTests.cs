using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Comparison isolation and data contracts, not an aesthetic approval.</summary>
    public sealed class LabWatercolorV3IsolationTests
    {
        const string ProfilePath = "Assets/Editor/WatercolorPaint/V3/Data/V3StudyProfile.asset";
        static Type EditorType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("C6Lab.Experiments.Editor." + name, false))
            .FirstOrDefault(type => type != null) ?? throw new InvalidOperationException("V3 Editor study type missing: " + name);
        static object Snapshot(Object profile) => profile.GetType().GetMethod("Snapshot", Type.EmptyTypes).Invoke(profile, null);
        static T Property<T>(object source, string name) => (T)source.GetType().GetProperty(name).GetValue(source);

        [Test]
        public void V3IsEditorOnlyAndDoesNotBecomeTheGameDefault()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath(ProfilePath);
            Assert.That(profile, Is.Not.Null, "Bake the explicitly versioned comparison data before running this repository check.");
            Assert.That(AssetDatabase.GetAssetPath(profile), Does.StartWith("Assets/Editor/"));
            Assert.That(LabWatercolorStampSettings.StyleVersion, Is.EqualTo("watercolor-stamps-v2"));
            var live = LabWatercolorStampSettings.CaptureDefault();
            Assert.That(live.Version, Does.StartWith(LabClayStickerSettings.StyleVersion + "/"));
            Assert.That(live.UsesClayStickers, Is.True, "The approved clay look is the live default; V3 remains Editor-only.");
            Assert.That(live.density, Is.EqualTo(.9f));
            Assert.That(live.overlap, Is.EqualTo(.78f));
            object captured = Snapshot(profile);
            Assert.That(Property<string>(captured, "Version"), Does.StartWith("watercolor-stamps-v3-study-"));
            foreach (var brush in Property<Texture2D[]>(captured, "Brushes"))
                Assert.That(AssetDatabase.GetAssetPath(brush), Does.StartWith("Assets/Editor/WatercolorPaint/V3/Data/"));
            Assert.That(AssetDatabase.GetAssetPath(Property<Texture2D>(captured, "PaperTexture")), Does.StartWith("Assets/Editor/"));
        }

        [Test]
        public void CapturedV3SettingsAndCopiesCannotFollowLaterInspectorChanges()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(ProfilePath);
            Assert.That(asset, Is.Not.Null);
            string original = EditorJsonUtility.ToJson(asset);
            Object clone = Object.Instantiate(asset);
            try
            {
                clone.GetType().GetField("layerOpacityCap").SetValue(clone, .75f);
                object captured = Snapshot(clone);
                string version = Property<string>(captured, "Version");
                int fingerprint = Property<int>(captured, "Fingerprint");
                clone.GetType().GetField("layerOpacityCap").SetValue(clone, .85f);
                clone.GetType().GetField("pigmentDepth").SetValue(clone, 2.3f);
                Assert.That(Property<string>(Snapshot(clone), "Version"), Is.Not.EqualTo(version));
                Assert.That(Property<string>(captured, "Version"), Is.EqualTo(version));
                Assert.That(Property<int>(captured, "Fingerprint"), Is.EqualTo(fingerprint));
                var brushes = Property<Texture2D[]>(captured, "Brushes"); brushes[0] = null;
                Assert.That(Property<Texture2D[]>(captured, "Brushes")[0], Is.Not.Null);
                var settings = Property<LabWatercolorStampSettings>(captured, "BaseStampSettings");
                float density = settings.density; settings.density = .05f;
                Assert.That(Property<LabWatercolorStampSettings>(captured, "BaseStampSettings").density, Is.EqualTo(density));
                object alternate = captured.GetType().GetMethod("WithLayerOpacityCap").Invoke(captured, new object[] { .85f });
                Assert.That(Property<float>(alternate, "LayerOpacityCap"), Is.EqualTo(.85f));
                Assert.That(Property<string>(alternate, "Version"), Is.Not.EqualTo(version));
                var block = new MaterialPropertyBlock(); alternate.GetType().GetMethod("ApplyStamp").Invoke(alternate, new object[] { block });
                Assert.That(block.GetFloat("_V3LayerOpacityCap"), Is.EqualTo(.85f));
                Assert.That(Property<string>(captured, "Version"), Is.EqualTo(version));
            }
            finally { Object.DestroyImmediate(clone); }
            Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(original), "The inspection study must never rewrite its authoring asset.");
        }

        [Test]
        public void V3WhiteBrushRemovesTheDyeFloorWhileKeepingV2GeometryAndPadding()
        {
            Color32[] input = Enumerable.Repeat(new Color32(255, 255, 255, 255), 16).ToArray();
            object[] args = { input, 4, 4 };
            var oldData = (Color32[])EditorType("LabWatercolorBrushBaker").GetMethod("Bake").Invoke(null, args);
            var newData = (Color32[])EditorType("LabWatercolorV3Baker").GetMethod("Bake").Invoke(null, args);
            Assert.That(newData, Has.Length.EqualTo(oldData.Length));
            Assert.That(newData.All(pixel => pixel.r == 0), Is.True, "Opaque white must not receive a hidden .28 pigment floor.");
            Assert.That(oldData.Any(pixel => pixel.r > 0), Is.True, "This fixture must distinguish the preserved V2 interpretation.");
            for (int i = 0; i < oldData.Length; i++)
                if (newData[i].g != oldData[i].g || newData[i].b != oldData[i].b || newData[i].a != oldData[i].a)
                    Assert.Fail("V3 may change pigment density only, not brush support/distance/padding at texel " + i);
            Assert.That(input.All(pixel => pixel.Equals(new Color32(255, 255, 255, 255))), Is.True);
        }
    }
}
