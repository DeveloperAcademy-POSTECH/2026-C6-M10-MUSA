using System;
using System.IO;
using System.Linq;
using C6.Prototype.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Editor
{
    /// <summary>Adds editable visual guides inside the saved, unchanged defense input zones.</summary>
    public static class DefenseTouchMarkerTools
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string RingPath = "Assets/_Project/HapioMVP/Art/UI/DefenseRing.png";
        private const string HandPath = "Assets/_Project/HapioMVP/Art/UI/DefenseHand.png";

        [MenuItem("C6/UI/Apply Defense Touch Markers")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before editing the battle scene.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the clean ContinuousTransferBattle scene first.");

            var huds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T09Hud>(true)).ToArray();
            var warnings = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonsterAttackWarning>(true)).ToArray();
            if (huds.Length != 1 || warnings.Length != 1)
                throw new InvalidOperationException("Expected one saved battle HUD and one attack warning.");
            T09Hud hud = huds[0];
            string error = string.Empty;
            if (!hud.UseSceneHierarchy || !hud.ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("Saved battle HUD is invalid: " + error);
            var upper = hud.Canvas.transform.Find("SafeArea/UpperSafeViewport/UpperHudContent");
            var left = upper != null ? upper.Find("DefenseZoneLeft") as RectTransform : null;
            var right = upper != null ? upper.Find("DefenseZoneRight") as RectTransform : null;
            if (left == null || right == null || left.GetComponents<Component>().Length != 1 ||
                right.GetComponents<Component>().Length != 1 || left.childCount != 0 || right.childCount != 0)
                throw new InvalidOperationException("Expected the original empty RectTransform defense zones.");

            Sprite ring = EnsureRingSprite();
            Sprite hand = ImportSprite(HandPath, 256);
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Add defense touch markers");
            DefenseTouchMarker leftMarker = CreateMarker(left, "DefenseTouchLeft", ring, hand);
            DefenseTouchMarker rightMarker = CreateMarker(right, "DefenseTouchRight", ring, hand);

            var warning = new SerializedObject(warnings[0]);
            var markers = warning.FindProperty("defenseMarkers");
            markers.arraySize = 2;
            markers.GetArrayElementAtIndex(0).objectReferenceValue = leftMarker;
            markers.GetArrayElementAtIndex(1).objectReferenceValue = rightMarker;
            warning.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!hud.ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("Defense markers broke a HUD binding: " + error);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the battle scene.");
            Debug.Log("C6_DEFENSE_TOUCH_MARKERS_READY: two non-blocking visual guides bound to the existing warning.");
        }

        private static DefenseTouchMarker CreateMarker(RectTransform zone, string name, Sprite ring, Sprite hand)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(DefenseTouchMarker));
            Undo.RegisterCreatedObjectUndo(root, "Add " + name);
            root.transform.SetParent(zone, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = Vector2.zero;

            Image outline = CreateImage(rect, "Outline", ring, 72f);
            Image progress = CreateImage(rect, "HoldProgress", ring, 72f);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Radial360;
            progress.fillOrigin = (int)Image.Origin360.Top;
            progress.fillClockwise = true;
            progress.fillAmount = 0f;
            Image icon = CreateImage(rect, "HandIcon", hand, 40f);
            icon.preserveAspect = true;

            var marker = root.GetComponent<DefenseTouchMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("outline").objectReferenceValue = outline;
            serialized.FindProperty("holdProgress").objectReferenceValue = progress;
            serialized.FindProperty("handIcon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            marker.Hide();
            return marker;
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, float size)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(child, "Add defense " + name);
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            var image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static Sprite EnsureRingSprite()
        {
            if (!File.Exists(RingPath))
            {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f),
                            new Vector2(size * .5f, size * .5f));
                        float coverage = Mathf.Clamp01(122.5f - distance) * Mathf.Clamp01(distance - 113.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * coverage));
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(RingPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(RingPath);
            }
            return ImportSprite(RingPath, 256);
        }

        private static Sprite ImportSprite(string path, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing defense artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Could not import defense artwork: " + path);
            return sprite;
        }
    }
}
