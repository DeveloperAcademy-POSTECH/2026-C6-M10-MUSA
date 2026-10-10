using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Updates the saved scene in place; it never rebuilds the designer's Canvas.</summary>
    public static class LabOrb3DSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const int BoardLayer = 8;
        private const string BoardLayerName = "LabOrbBoard";
        private const string ArtFolder = "Assets/Lab/Art/Orbs";
        private const string AppearancePath = ArtFolder + "/LabOrbAppearance.asset";
        private const string VisualPath = ArtFolder + "/OrbVisual.prefab";
        private const string ShadowTexturePath = ArtFolder + "/OrbContactShadowTexture.asset";
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";

        [MenuItem("C6 Lab/Upgrade Saved 3D Orb Board")]
        public static void UpgradeSavedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject board = GameObject.Find("OrbBoard");
            Camera orbCamera = GameObject.Find("OrbCamera")?.GetComponent<Camera>();
            Camera battleCamera = GameObject.Find("BattleCamera")?.GetComponent<Camera>();
            if (board == null || orbCamera == null || battleCamera == null)
                throw new InvalidOperationException("The saved orb board or its cameras are missing.");
            Configure(board, orbCamera, battleCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the 3D orb board scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_3D_ORB_SCENE_SAVED layer=" + BoardLayerName);
        }

        public static void Configure(GameObject board, Camera orbCamera, Camera battleCamera)
        {
            if (board == null || orbCamera == null || battleCamera == null)
                throw new ArgumentNullException("Board and both cameras are required.");
            EnsureBoardLayer();
            board.layer = BoardLayer;
            // The lower camera sees board art only. The upper camera never renders it.
            orbCamera.cullingMask = 1 << BoardLayer;
            battleCamera.cullingMask &= ~(1 << BoardLayer);
            // Both are Base cameras with separate viewports. Give the board an
            // explicit later render order so mobile backends cannot clear it away
            // when two cameras share the same default depth.
            if (orbCamera.depth <= battleCamera.depth)
                orbCamera.depth = battleCamera.depth + 1f;
            LabOrbAppearance appearance = EnsureAppearanceAssets();
            BindAppearanceToConfig(appearance);
            EnsureBoardPlate(board.transform);
            EnsureBoardLight(board.transform);
            ExcludeBoardFromBattleLight(board.scene);
            DisableOverlayTint(board.scene);
            EditorUtility.SetDirty(board);
            EditorUtility.SetDirty(orbCamera);
            EditorUtility.SetDirty(battleCamera);
        }

        private static LabOrbAppearance EnsureAppearanceAssets()
        {
            EnsureFolder("Assets/Lab", "Art");
            EnsureFolder("Assets/Lab/Art", "Orbs");

            Material yin = EnsureLitMaterial("OrbYin.mat", new Color(.11f, .42f, .85f), .55f, .08f);
            Material yang = EnsureLitMaterial("OrbYang.mat", new Color(.95f, .35f, .12f), .55f, .08f);
            Material combined = EnsureLitMaterial("OrbCombined.mat", new Color(.55f, .29f, .81f), .65f, .12f);
            Material marker = EnsureLitMaterial("OrbMarker.mat", new Color(.95f, .94f, .85f), .72f, .04f);
            Material shadow = EnsureShadowMaterial(EnsureShadowTexture());
            GameObject visual = EnsureVisualPrefab(yin, marker);

            LabOrbAppearance appearance = LoadExpectedAsset<LabOrbAppearance>(AppearancePath);
            if (appearance == null)
            {
                appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
                AssetDatabase.CreateAsset(appearance, AppearancePath);
            }
            var serialized = new SerializedObject(appearance);
            AssignObjectIfNull(serialized, "visualPrefab", visual);
            AssignObjectIfNull(serialized, "yinMaterial", yin);
            AssignObjectIfNull(serialized, "yangMaterial", yang);
            AssignObjectIfNull(serialized, "combinedMaterial", combined);
            AssignObjectIfNull(serialized, "shadowMaterial", shadow);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return appearance;
        }

        private static Material EnsureLitMaterial(string fileName, Color color, float smoothness, float metallic)
        {
            string path = ArtFolder + "/" + fileName;
            Material material = LoadExpectedAsset<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No Lit shader is available for orb art.");
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(fileName) };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D EnsureShadowTexture()
        {
            Texture2D texture = LoadExpectedAsset<Texture2D>(ShadowTexturePath);
            if (texture != null) return texture;
            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "OrbContactShadowTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f);
                float dy = (y + .5f - size * .5f) / (size * .5f);
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, falloff * falloff);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, ShadowTexturePath);
            return texture;
        }

        private static Material EnsureShadowMaterial(Texture2D texture)
        {
            string path = ArtFolder + "/OrbContactShadow.mat";
            Material material = LoadExpectedAsset<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) throw new InvalidOperationException("No transparent shader is available for orb shadows.");
            material = new Material(shader) { name = "OrbContactShadow" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject EnsureVisualPrefab(Material yin, Material markerMaterial)
        {
            GameObject existing = LoadExpectedAsset<GameObject>(VisualPath);
            if (existing != null) return existing;

            var root = new GameObject("OrbVisual");
            try
            {
                GameObject sphere = CreateArtSphere("Sphere", root.transform, Vector3.zero, 1f, yin);
                CreateArtSphere("Roll Marker", sphere.transform,
                    new Vector3(.18f, .17f, -.43f), .12f, markerMaterial);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
                if (prefab == null) throw new IOException("Could not save OrbVisual prefab.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateArtSphere(string name, Transform parent,
            Vector3 localPosition, float localScale, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = Vector3.one * localScale;
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            return sphere;
        }

        private static void BindAppearanceToConfig(LabOrbAppearance appearance)
        {
            LabConfig config = LoadExpectedAsset<LabConfig>(ConfigPath);
            if (config == null) throw new FileNotFoundException("Lab Config is missing.", ConfigPath);
            var serialized = new SerializedObject(config);
            SerializedProperty field = serialized.FindProperty("orbAppearance");
            if (field == null) throw new InvalidOperationException("LabConfig.orbAppearance is missing.");
            if (field.objectReferenceValue != null) return; // Preserve an Inspector-assigned alternative.
            field.objectReferenceValue = appearance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureBoardPlate(Transform board)
        {
            Transform existing = board.Find("OrbBoardPlate");
            if (existing != null)
            {
                existing.gameObject.layer = BoardLayer;
                if (existing.GetComponent<Collider>() != null)
                    throw new InvalidOperationException("OrbBoardPlate must remain visual-only; remove its Collider in the Inspector.");
                // The first generated plate faced away from this project's -Z board camera.
                // Correct only that exact generated rotation; preserve later designer edits.
                if (Quaternion.Angle(existing.localRotation, Quaternion.Euler(0f, 180f, 0f)) < .1f)
                    existing.localRotation = Quaternion.identity;
                return; // Keep designer material and placement on later runs.
            }

            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "OrbBoardPlate";
            plate.transform.SetParent(board, false);
            plate.transform.localPosition = new Vector3(0f, 0f, .55f);
            // Unity's built-in Quad normal is -Z, toward OrbCamera at z=-10.
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(10f, 5f, 1f);
            plate.layer = BoardLayer;
            Collider collider = plate.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Material material = EnsureLitMaterial("OrbBoardPlate.mat", new Color(.16f, .22f, .26f), .12f, 0f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void EnsureBoardLight(Transform board)
        {
            Transform existing = board.Find("OrbBoardLight");
            Light light;
            if (existing == null)
            {
                var lightObject = new GameObject("OrbBoardLight", typeof(Light));
                lightObject.transform.SetParent(board, false);
                lightObject.transform.localPosition = new Vector3(-3f, 4f, -6f);
                light = lightObject.GetComponent<Light>();
                light.type = LightType.Point;
                light.range = 40f;
                light.intensity = 70f;
                light.color = new Color(.90f, .95f, 1f);
                light.shadows = LightShadows.None;
            }
            else
            {
                light = existing.GetComponent<Light>();
                if (light == null) throw new InvalidOperationException("OrbBoardLight has no Light component.");
            }
            light.cullingMask = 1 << BoardLayer;
            EditorUtility.SetDirty(light);
        }

        private static void ExcludeBoardFromBattleLight(Scene scene)
        {
            Light light = FindInScene(scene, "BattleLight")?.GetComponent<Light>();
            if (light == null) return;
            light.cullingMask &= ~(1 << BoardLayer);
            EditorUtility.SetDirty(light);
        }

        private static void DisableOverlayTint(Scene scene)
        {
            GameObject tint = FindInScene(scene, "BoardAreaTint");
            UnityEngine.UI.Image image = tint?.GetComponent<UnityEngine.UI.Image>();
            if (image == null || !image.enabled) return;
            image.enabled = false;
            EditorUtility.SetDirty(image);
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child.gameObject;
            return null;
        }

        private static T LoadExpectedAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Another asset type already occupies " + path);
            return asset;
        }

        private static void AssignObjectIfNull(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty field = serialized.FindProperty(propertyName);
            if (field == null) throw new InvalidOperationException("LabOrbAppearance." + propertyName + " is missing.");
            if (field.objectReferenceValue == null) field.objectReferenceValue = value;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (AssetDatabase.IsValidFolder(path)) return;
            if (!AssetDatabase.IsValidFolder(parent)) throw new DirectoryNotFoundException(parent);
            AssetDatabase.CreateFolder(parent, child);
        }

        private static void EnsureBoardLayer()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) throw new InvalidOperationException("Unity TagManager is unavailable.");
            var tags = new SerializedObject(assets[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            if (layers == null || layers.arraySize <= BoardLayer)
                throw new InvalidOperationException("Unity layers are unavailable.");
            SerializedProperty slot = layers.GetArrayElementAtIndex(BoardLayer);
            if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != BoardLayerName)
                throw new InvalidOperationException("Layer 8 is already used: " + slot.stringValue);
            if (slot.stringValue == BoardLayerName) return;
            slot.stringValue = BoardLayerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
