using System;
using C6.Prototype.Battle;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Editor
{
    /// <summary>Applies the Figma front artwork to the saved battle scene without replacing gameplay objects.</summary>
    public static class FigmaFrontUiTools
    {
        private const string Art = "Assets/_Project/HapioMVP/Art/FigmaFront/";
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";

        [MenuItem("C6/UI/Apply Figma Front Artwork")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before editing the saved battle UI.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the clean ContinuousTransferBattle scene before applying front artwork.");

            T09Hud hud = null;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                hud = sceneRoot.GetComponentInChildren<T09Hud>(true);
                if (hud != null) break;
            }
            string error = string.Empty;
            if (hud == null || !hud.UseSceneHierarchy || !hud.ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("The saved battle HUD is missing or invalid: " + error);
            if (hud.Layout != null && hud.Layout.OrbCamera != null &&
                hud.Layout.OrbCamera.transform.Find("OrbWoodenPlate") != null)
                throw new InvalidOperationException(
                    "The wooden board layout is scene-authored. Reapplying the old Figma preset would reset its control positions.");

            Texture2D background = ImportTexture("FrontBackdrop.png", false);
            Texture2D hudAtlas = ImportTexture("HudAtlasTransparent.png", true);
            Material backgroundMaterial = EnsureBackgroundMaterial(background);

            var root = hud.transform;
            var upper = Required(root, "T09Overlay/SafeArea/UpperSafeViewport/UpperHudContent");
            var lower = Required(root, "T09Overlay/SafeArea/LowerSafeViewport/LowerHudContent");
            var stats = Required(upper, "BattleStats");
            var hpPanel = Required(stats, "MonsterHpPanel");
            var hpFill = hud.MonsterHpFill;
            var footer = Required(lower, "ResourceControls");
            var buttons = Required(footer, "ResourceButtons");
            var staminaPanel = Required(footer, "PersonalStaminaPanel");
            var staminaTrack = Required(staminaPanel, "ContinuousStaminaTrack");
            var timeBar = (upper.Find("TeamTimeBar") ?? footer.Find("TeamTimeBar")) as RectTransform;
            if (timeBar == null) throw new InvalidOperationException("Saved team time bar is missing.");

            // The footer height is also the orb physics exclusion zone; the matching config
            // constant keeps moving orbs out from beneath the controls.
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Apply Figma front artwork");
            SetRect(stats, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(.5f, 1f), new Vector2(359f, 67f), new Vector2(0f, -8f));
            ApplyAtlas(hpPanel, hudAtlas, new Rect(5f, 1085f, 1196f, 214f));
            var hpTrack = EnsureRect(hpPanel, "FigmaMonsterHpTrack");
            SetRect(hpTrack, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(233f, 10f), new Vector2(87f, -32f));
            ReparentFill(hpFill, hpTrack, new Color(1f, .16f, .25f, 1f));
            var hpCaption = stats.Find("MonsterHpCaption");
            if (hpCaption != null) hpCaption.gameObject.SetActive(false);
            hud.HpLabel.gameObject.SetActive(false);

            SetHeight(footer, ScreenLayoutConfig.MinimalBattleFooterHeight);
            ClearImage(footer);
            SetRect(buttons, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(.5f, .5f), new Vector2(194f, 79f), new Vector2(0f, 149f));
            var generate = (RectTransform)hud.GenerateButton.transform;
            Stretch(generate);
            var buttonArt = ApplyAtlas(generate, hudAtlas, new Rect(273f, 210f, 653f, 266f));
            buttonArt.raycastTarget = true;
            hud.GenerateButton.targetGraphic = buttonArt;
            var generateText = hud.GenerateButton.GetComponentInChildren<Text>(true);
            generateText.color = new Color(.19f, .11f, .08f, 1f);
            generateText.fontSize = 15;

            SetRect(staminaPanel, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(.5f, .5f), new Vector2(223f, 42f), new Vector2(0f, 85f));
            ClearImage(staminaPanel);
            Stretch(staminaTrack);
            ApplyAtlas(staminaTrack, hudAtlas, new Rect(209f, 50f, 784f, 127f));
            ClearImage(hud.StaminaFill);
            foreach (var image in staminaTrack.GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("TwentyMarker", StringComparison.Ordinal)) image.enabled = false;
            var staminaValue = staminaPanel.Find("StaminaValue");
            if (staminaValue != null) staminaValue.gameObject.SetActive(false);
            var staminaInterior = EnsureRect(staminaTrack, "FigmaStaminaInterior");
            SetRect(staminaInterior, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(165f, 20f), new Vector2(28f, 11f));
            SetSolid(staminaInterior, new Color(.039f, .043f, .059f, 1f));
            staminaInterior.SetSiblingIndex(1);
            var gems = new RectTransform[5];
            for (int i = 0; i < gems.Length; i++)
            {
                var slot = EnsureRect(staminaTrack, "FigmaStaminaGem" + (i + 1));
                SetRect(slot, Vector2.zero, Vector2.zero, Vector2.zero,
                    new Vector2(29f, 19f), new Vector2(33f + i * 31.7f, 11f));
                var clip = EnsureRect(slot, "FillClip");
                Stretch(clip);
                if (clip.GetComponent<RectMask2D>() == null) Undo.AddComponent<RectMask2D>(clip.gameObject);
                var art = EnsureRect(clip, "Artwork");
                SetRect(art, Vector2.zero, Vector2.zero, Vector2.zero,
                    new Vector2(29f, 19f), Vector2.zero);
                ApplyAtlas(art, hudAtlas, new Rect(298f, 80f, 107f, 70f));
                slot.SetAsLastSibling();
                gems[i] = clip;
            }
            using (var serialized = new SerializedObject(hud))
            {
                var segments = serialized.FindProperty("staminaSegments");
                segments.arraySize = gems.Length;
                for (int i = 0; i < gems.Length; i++)
                    segments.GetArrayElementAtIndex(i).objectReferenceValue = gems[i];
                serialized.ApplyModifiedProperties();
            }

            timeBar.SetParent(footer, false);
            SetRect(timeBar, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(.5f, .5f), new Vector2(322f, 43f), new Vector2(0f, 42f));
            ApplyAtlas(timeBar, hudAtlas, new Rect(132f, 959f, 932f, 123f));
            var timeTrack = EnsureRect(timeBar, "FigmaTeamTimeTrack");
            SetRect(timeTrack, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(237f, 10f), new Vector2(47f, -17f));
            ReparentFill(hud.TeamTimeFill, timeTrack, new Color(.94f, .23f, .53f, 1f));
            hud.TeamTimeValue.gameObject.SetActive(false);
            var divider = Required(root, "T09Overlay/ViewportDivider");
            ClearImage(divider);

            EnsureBackdrop(hud.Layout.BattleCamera, "Figma Front Upper Backdrop", 10,
                background, backgroundMaterial);
            EnsureBackdrop(hud.Layout.OrbCamera, "Figma Front Lower Backdrop", 9,
                background, backgroundMaterial);
            HideLegacyStageRenderers(scene);

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!hud.ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("Artwork changed a required HUD reference: " + error);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save front artwork scene.");
            Debug.Log("C6_FIGMA_FRONT_APPLIED: saved art, backdrops, five dynamic stamina gems and existing HUD bindings.");
        }

        private static Texture2D ImportTexture(string file, bool transparent)
        {
            string path = Art + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing artwork: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = transparent;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material EnsureBackgroundMaterial(Texture texture)
        {
            string path = Art + "FigmaFrontBackdrop.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("URP Unlit shader is unavailable.");
                material = new Material(shader) { name = "FigmaFrontBackdrop" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static RectTransform Required(Transform parent, string path)
        {
            var child = parent.Find(path) as RectTransform;
            if (child == null) throw new InvalidOperationException("Missing saved UI object: " + path);
            return child;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var child = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(child, "Create Figma UI art");
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetHeight(RectTransform rect, float height)
        {
            var size = rect.sizeDelta;
            size.y = height;
            rect.sizeDelta = size;
        }

        private static RawImage ApplyAtlas(RectTransform rect, Texture2D atlas, Rect pixels)
        {
            var original = rect.GetComponent<Image>();
            if (original != null)
            {
                original.sprite = null;
                original.enabled = false;
            }
            var art = EnsureRect(rect, "FigmaAtlasArt");
            Stretch(art);
            art.SetAsFirstSibling();
            var raw = art.GetComponent<RawImage>();
            if (raw == null) raw = Undo.AddComponent<RawImage>(art.gameObject);
            raw.texture = atlas;
            raw.uvRect = new Rect(pixels.x / atlas.width, pixels.y / atlas.height,
                pixels.width / atlas.width, pixels.height / atlas.height);
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        private static Image SetSolid(RectTransform rect, Color color)
        {
            var image = rect.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(rect.gameObject);
            image.enabled = true;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void ClearImage(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image == null) return;
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        private static void ReparentFill(RectTransform fill, RectTransform track, Color color)
        {
            fill.SetParent(track, false);
            Stretch(fill);
            SetSolid(track, new Color(.039f, .027f, .043f, 1f));
            SetSolid(fill, color);
        }

        private static void EnsureBackdrop(Camera camera, string name, int layer,
            Texture2D texture, Material material)
        {
            if (camera == null) throw new InvalidOperationException("A split-screen camera is missing.");
            var child = camera.transform.Find(name);
            GameObject quad;
            if (child == null)
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = name;
                Undo.RegisterCreatedObjectUndo(quad, "Create Figma camera backdrop");
                quad.transform.SetParent(camera.transform, false);
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            }
            else quad = child.gameObject;
            quad.layer = layer;
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var backdrop = quad.GetComponent<FigmaViewportBackdrop>();
            if (backdrop == null) backdrop = Undo.AddComponent<FigmaViewportBackdrop>(quad);
            backdrop.Configure(camera, texture);
        }

        private static void HideLegacyStageRenderers(Scene scene)
        {
            string[] names = { "Left Backdrop Pillar", "Right Backdrop Pillar", "Training Platform", "Floor" };
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool legacyStage = Array.Exists(names, name => renderer.gameObject.name == name);
                bool gridGuide = renderer.gameObject.name.StartsWith("Grid Vertical ", StringComparison.Ordinal)
                    || renderer.gameObject.name.StartsWith("Grid Horizontal ", StringComparison.Ordinal);
                if (legacyStage || gridGuide) renderer.enabled = false;
            }
        }
    }
}
