using System;
using System.IO;
using System.Linq;
using C6.Prototype.Orbs;
using C6.Prototype.PhysicsSandbox;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    /// <summary>Creates an independent tuning scene without changing game scenes or build settings.</summary>
    public static class OrbPhysicsSandboxSetup
    {
        public const string RootPath = "Assets/_Project/HapioMVP/PhysicsSandbox";
        public const string ScenePath = RootPath + "/OrbPhysicsSandbox.unity";
        public const string YinPrefabPath = RootPath + "/Prefabs/YinSandboxOrb.prefab";
        public const string YangPrefabPath = RootPath + "/Prefabs/YangSandboxOrb.prefab";
        public const string CirclePath = RootPath + "/Art/Circle.asset";
        public const string SquarePath = RootPath + "/Art/Square.asset";

        static readonly Color Navy = new Color(.055f, .10f, .15f);
        static readonly Color Board = new Color(.085f, .16f, .21f);
        static readonly Color Cream = new Color(.94f, .94f, .84f);
        static readonly Color Teal = new Color(.38f, .82f, .75f);
        static readonly Color Gold = new Color(.89f, .74f, .44f);
        static readonly Rect LeftBoard = new Rect(-7f, -3.5f, 6f, 7f);
        static readonly Rect RightBoard = new Rect(1f, -3.5f, 6f, 7f);

        // The installer can request this one asset-generation operation. It never selects,
        // saves, closes or starts Play on an existing user scene. No arbitrary code is read.
        [InitializeOnLoadMethod]
        static void CheckInstallRequest()
        {
            EditorApplication.delayCall += PrepareRequestedAssets;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += PrepareRequestedAssets;
        }

        static void PrepareRequestedAssets()
        {
            const string request = "Temp/C6OrbPhysicsSandbox.prepare";
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || !File.Exists(request)) return;
            if (File.ReadAllText(request).Trim() != "prepare-new-sandbox-assets") return;
            try
            {
                Prepare();
                File.Delete(request);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/C6OrbPhysicsSandbox-prepared.txt", ScenePath);
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("C6/Physics Sandbox/Unity Remote Settings", false, 20)]
        public static void OpenRemoteSettings() => SettingsService.OpenProjectSettings("Project/Editor");

        [MenuItem("C6/Physics Sandbox/Open Tuning Scene", false, 0)]
        public static void OpenTuningScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Play를 정지한 뒤 물리 튜닝 씬을 열어 주세요.");
                return;
            }

            Prepare();
            // Preparing is additive and preserves open scenes. Opening Single is a separate,
            // user-driven operation and must honor Unity's save/discard/cancel choice.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sandbox = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<OrbPhysicsSandbox>(true)).FirstOrDefault();
            if (sandbox == null) return;
            Selection.activeGameObject = sandbox.gameObject;
            EditorGUIUtility.PingObject(sandbox.gameObject);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = true;
                SceneView.lastActiveSceneView.Frame(new Bounds(Vector3.zero, new Vector3(16f, 11.2f, 1f)), false);
            }
        }

        [MenuItem("C6/Physics Sandbox/Prepare Tuning Assets", false, 1)]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing the physics sandbox.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("C6_ORB_SANDBOX_READY existing scene preserved: " + ScenePath);
                return;
            }
            RequireUnoccupiedPath(ScenePath);

            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(BattleLoopBuild.ConfigPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(BattleLoopBuild.SpriteMaterialPath);
            if (config == null) throw new InvalidOperationException("The existing ScreenLayoutConfig asset is required.");
            if (material == null) throw new InvalidOperationException("The installed URP Sprite Unlit material is required.");

            EnsureFolder(RootPath + "/Art");
            EnsureFolder(RootPath + "/Prefabs");
            var circle = EnsureSprite(CirclePath, true);
            var square = EnsureSprite(SquarePath, false);
            var originalActive = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                // Even batch setup uses Additive: no existing scene or user object is discarded.
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var yin = EnsureOrbPrefab(YinPrefabPath, OrbPolarity.Yin, circle, material, config);
                var yang = EnsureOrbPrefab(YangPrefabPath, OrbPolarity.Yang, circle, material, config);

                var root = new GameObject("Orb Physics Tuning");
                var sandbox = root.AddComponent<OrbPhysicsSandbox>();
                sandbox.leftWorkspace = LeftBoard;
                sandbox.rightWorkspace = RightBoard;
                sandbox.portalsEnabled = false;
                sandbox.showHelp = true;
                sandbox.matchGameArea = true;

                var background = new GameObject("Background Camera", typeof(Camera));
                background.transform.SetParent(root.transform, false);
                var backgroundCamera = background.GetComponent<Camera>();
                backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                backgroundCamera.backgroundColor = Navy;
                backgroundCamera.cullingMask = 0;
                backgroundCamera.depth = -10;

                var cameraObject = new GameObject("Sandbox Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.depth = 0;
                camera.orthographic = true;
                camera.orthographicSize = 5.6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Navy;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 50f;

                CreateBoard(root.transform, "Board A", LeftBoard, square, material, Teal);
                CreateBoard(root.transform, "Board B", RightBoard, square, material, Gold);
                CreateLabel(root.transform, "Sandbox heading", "ORB PHYSICS LAB", new Vector3(0f, 5f, 0f), .32f, Cream);
                CreateLabel(root.transform, "Board A title", "A  /  COMBINATION BOARD", new Vector3(-4f, 4.05f, 0f), .25f, Teal);
                CreateLabel(root.transform, "Board B title", "B  /  COMBINATION BOARD", new Vector3(4f, 4.05f, 0f), .25f, Gold);
                CreateLabel(root.transform, "Portal route", "A <-> B", Vector3.zero, .20f, Cream);
                CreateLabel(root.transform, "Scene instruction", "PLAY  /  DRAG & RELEASE  /  TUNE IN INSPECTOR",
                    new Vector3(0f, -4.05f, 0f), .23f, Cream);

                var seeds = new[]
                {
                    PlaceOrb(yin, root.transform, 0, new Vector2(-5.3f, .65f)),
                    PlaceOrb(yang, root.transform, 0, new Vector2(-2.8f, -.70f)),
                    PlaceOrb(yin, root.transform, 1, new Vector2(2.8f, .65f)),
                    PlaceOrb(yang, root.transform, 1, new Vector2(5.3f, -.70f))
                };
                sandbox.Configure(config, material, camera, seeds);
                sandbox.ApplyTuning();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Could not save the new physics tuning scene.");
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("C6_ORB_SANDBOX_PREPARED scene=" + ScenePath +
                " config=" + BattleLoopBuild.ConfigPath + " (existing scenes and build settings preserved)");
        }

        static OrbSandboxSeed PlaceOrb(GameObject prefab, Transform parent, int board, Vector2 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var seed = instance.GetComponent<OrbSandboxSeed>();
            seed.initialBoard = board;
            instance.name = (board == 0 ? "A - " : "B - ") + (seed.polarity == OrbPolarity.Yin ? "Yin Orb" : "Yang Orb");
            instance.transform.position = new Vector3(position.x, position.y, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(seed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return seed;
        }

        static GameObject EnsureOrbPrefab(string path, OrbPolarity polarity, Sprite circle, Material material, ScreenLayoutConfig config)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (existing.GetComponent<OrbSandboxSeed>() == null)
                    throw new InvalidOperationException("The existing prefab is not a sandbox orb; preserving it: " + path);
                return existing;
            }
            RequireUnoccupiedPath(path);
            var orb = new GameObject(polarity == OrbPolarity.Yin ? "Yin Sandbox Orb" : "Yang Sandbox Orb");
            try
            {
                float radius = LeftBoard.width * config.OrbRadiusScreenFraction;
                var seed = orb.AddComponent<OrbSandboxSeed>();
                seed.polarity = polarity;
                seed.initialBoard = 0;
                var rigidbody = orb.AddComponent<Rigidbody2D>();
                rigidbody.gravityScale = 0f;
                rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
                rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var collider = orb.AddComponent<CircleCollider2D>();
                collider.radius = radius;
                collider.isTrigger = false;

                var artwork = new GameObject("Preview Artwork (visible before Play)").transform;
                artwork.SetParent(orb.transform, false);
                artwork.localScale = Vector3.one * (radius / .5f);
                seed.previewArtwork = artwork;
                bool yin = polarity == OrbPolarity.Yin;
                SpritePart(artwork, "Ring", circle, material, Vector2.zero, Vector2.one, yin ? Cream : Gold, 40);
                SpritePart(artwork, "Core", circle, material, Vector2.zero, Vector2.one * .82f, yin ? Board : Cream, 41);
                SpritePart(artwork, "Dot", circle, material, new Vector2(0f, .04f), Vector2.one * .19f, yin ? Cream : Board, 42);
                CreateLabel(artwork, "Polarity label", yin ? "YIN" : "YANG", new Vector3(0f, -.65f, 0f), .28f, Cream);

                var saved = PrefabUtility.SaveAsPrefabAsset(orb, path);
                if (saved == null) throw new InvalidOperationException("Could not save sandbox orb prefab: " + path);
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(orb); }
        }

        static void CreateBoard(Transform parent, string name, Rect bounds, Sprite square, Material material, Color accent)
        {
            var board = new GameObject(name + " - visual boundaries").transform;
            board.SetParent(parent, false);
            SpritePart(board, "Backdrop", square, material, bounds.center, bounds.size, Board, -20);
            const float line = .035f;
            SpritePart(board, "Top wall", square, material, new Vector2(bounds.center.x, bounds.yMax), new Vector2(bounds.width, line), Cream, -10);
            SpritePart(board, "Bottom wall", square, material, new Vector2(bounds.center.x, bounds.yMin), new Vector2(bounds.width, line), Cream, -10);
            SpritePart(board, "Left portal edge", square, material, new Vector2(bounds.xMin, bounds.center.y), new Vector2(line, bounds.height), accent, -10);
            SpritePart(board, "Right portal edge", square, material, new Vector2(bounds.xMax, bounds.center.y), new Vector2(line, bounds.height), accent, -10);
            // Actual collision/portal bounds are supplied to the shared OrbPhysicsWorld by the manager.
            // These objects deliberately carry no independent colliders.
        }

        static void SpritePart(Transform parent, string name, Sprite sprite, Material material, Vector2 position,
            Vector2 scale, Color color, int order)
        {
            var part = new GameObject(name, typeof(SpriteRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(position.x, position.y, 0f);
            part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = part.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        static void CreateLabel(Transform parent, string name, string text, Vector3 position, float height, Color color)
        {
            var labelObject = new GameObject(name, typeof(TextMesh));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            var label = labelObject.GetComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.fontSize = 64;
            font.RequestCharactersInTexture(text, label.fontSize);
            float glyphHeight = 64f;
            if (font.GetCharacterInfo('M', out var glyph, label.fontSize)) glyphHeight = Mathf.Max(1f, glyph.maxY - glyph.minY);
            label.characterSize = height * 10f / glyphHeight;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.text = text;
            var renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 45;
        }

        static Sprite EnsureSprite(string path, bool circle)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                if (existing == null) throw new InvalidOperationException("The existing art asset has no sprite; preserving it: " + path);
                return existing;
            }
            RequireUnoccupiedPath(path);
            int size = circle ? 128 : 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = circle ? "Sandbox Circle Texture" : "Sandbox Square Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size * .5f, size * .5f));
                    float alpha = circle ? Mathf.Clamp01(size * .5f - distance) : 1f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size,
                0, SpriteMeshType.FullRect);
            sprite.name = circle ? "Sandbox Circle" : "Sandbox Square";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            return sprite;
        }

        static void RequireUnoccupiedPath(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("Asset path is already occupied; preserving it: " + path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Cannot create asset folder: " + path);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new InvalidOperationException("Could not create asset folder: " + path);
        }
    }
}
