using System;
using System.IO;
using System.Linq;
using System.Reflection;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Imports only the authored lobby art, then patches Home/Lobby while preserving their saved DEV panels.</summary>
    public static class GameLobbySceneUpgrade
    {
        public const string PrefabPath = "Assets/Hapio/Art/UI/GameLobby.prefab";
        private const string SourceLab = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("Hapio/Upgrade Figma Game Lobby")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading saved UI.");
            GameObject prefab = EnsurePrefab();
            UpgradeScene(GameScenePaths.Home, prefab);
            UpgradeScene(GameScenePaths.Lobby, prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_FIGMA_LOBBY_READY: Home/Lobby share editable art; Lab, Battle and result Scenes unchanged.");
        }

        private static GameObject EnsurePrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<GameLobbyView>() == null || existing.GetComponent<LabLobbyView>() != null)
                    throw new InvalidOperationException("The saved game lobby prefab has an incompatible binding.");
                return existing; // Existing authored prefab edits are not regenerated from Lab.
            }
            Folder("Assets/Hapio/Art/UI");
            Scene previous = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SourceLab);
            bool owned = !source.IsValid() || !source.isLoaded;
            if (owned) source = EditorSceneManager.OpenScene(SourceLab, OpenSceneMode.Additive);
            if (source.isDirty) throw new InvalidOperationException("Review or save Lab edits before importing its saved lobby art.");
            GameObject copy = null;
            try
            {
                LabLobbyView original = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabLobbyView>(true)).Single();
                copy = UnityEngine.Object.Instantiate(original.gameObject);
                copy.name = "GameLobby";
                LabLobbyView old = copy.GetComponent<LabLobbyView>();
                GameLobbyView view = copy.AddComponent<GameLobbyView>();
                // Read from the cloned component, so all internal widget references already point into the clone.
                foreach (FieldInfo field in typeof(GameLobbyView).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    FieldInfo authored = typeof(LabLobbyView).GetField(field.Name, BindingFlags.Instance | BindingFlags.Public);
                    if (authored != null && authored.FieldType == field.FieldType) field.SetValue(view, authored.GetValue(old));
                }
                UnityEngine.Object.DestroyImmediate(old);
                if (view.emptySlots[0] == view.filledSlots[0]) view.emptySlots[0] = view.emptySlots[1];
                view.developerButton = CloneButton(view.enterButton, view.design, "DeveloperSettings", "DEV", 306, 24, 68, 36, 14);
                view.soloButton = CloneButton(view.enterButton, view.design, "DevelopmentSolo", "DEV · SOLO", 28, 24, 125, 36, 13);
                view.directConnectionPanel.SetActive(false);
                CreateSeatLayouts(view);
                ConfigureScroll(view);
                return PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
            }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                if (owned && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void UpgradeScene(string path, GameObject prefab)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Review or save edits in " + path + " before upgrading UI.");
            try
            {
                GameScreen screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                Canvas canvas = screen.GetComponent<Canvas>();
                if (canvas == null || screen.developerPanel == null || screen.developerInputs.Length != LabConfig.DeveloperFields.Count)
                    throw new InvalidOperationException("The existing Canvas and complete scoped DEV panel must be retained.");
                GameLobbyView[] views = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameLobbyView>(true)).ToArray();
                if (views.Length > 1) throw new InvalidOperationException("A menu must have one shared lobby view.");
                GameLobbyView view;
                if (views.Length == 0)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.transform.SetParent(canvas.transform, false);
                    view = instance.GetComponent<GameLobbyView>();
                }
                else
                {
                    view = views[0];
                    if (PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject) != prefab)
                        throw new InvalidOperationException("The existing menu view is not an instance of the shared authored prefab.");
                }
                Transform developerLayer = screen.developerPanel.transform;
                while (developerLayer.parent != null && developerLayer.parent != canvas.transform) developerLayer = developerLayer.parent;
                // Preserve this SafeArea/DEV branch and its layout. Hide only its legacy navigation siblings.
                HideNavigationSiblings(developerLayer, screen.developerPanel.transform);
                view.transform.SetAsLastSibling();
                developerLayer.SetAsLastSibling();
                view.screen = screen;
                screen.externalNavigation = true;
                screen.developerButton = view.developerButton;
                screen.soloButton = view.soloButton;
                EnsureMenuCamera(scene);
                EditorUtility.SetDirty(screen); EditorUtility.SetDirty(view);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (owned && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void EnsureMenuCamera(Scene scene)
        {
            // Overlay UI does not need a world camera, but the Editor otherwise draws its
            // "No cameras rendering" warning over the authored Home/Lobby presentation.
            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Any()) return;
            var item = new GameObject("Hapio UI Background Camera");
            SceneManager.MoveGameObjectToScene(item, scene);
            item.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = item.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.depth = -100f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
        }

        private static void HideNavigationSiblings(Transform branch, Transform developerPanel)
        {
            if (branch == developerPanel) return;
            foreach (Transform child in branch)
            {
                if (child == developerPanel) continue;
                if (developerPanel.IsChildOf(child)) HideNavigationSiblings(child, developerPanel);
                else child.gameObject.SetActive(false);
            }
        }

        private static void CreateSeatLayouts(GameLobbyView view)
        {
            RectTransform parent = NewRect(view.positionPage.transform, "EditableSeatLayouts"); Stretch(parent);
            var layouts = new RectTransform[4][];
            for (int count = 2; count <= 5; count++)
            {
                RectTransform group = NewRect(parent, "Players" + count); Stretch(group);
                layouts[count - 2] = new RectTransform[count];
                for (int number = 1; number <= count; number++)
                {
                    RectTransform anchor = NewRect(group, "P" + number);
                    Vector2 center = LabLobbyView.SlotCenter(count, number);
                    LabLobbyView.Place(anchor, center.x - 34.41555f, center.y - 33.9867f, 68.8311f, 67.9734f);
                    layouts[count - 2][number - 1] = anchor;
                }
            }
            view.seats2 = layouts[0]; view.seats3 = layouts[1]; view.seats4 = layouts[2]; view.seats5 = layouts[3];
            view.lineLayouts = new RectTransform[3];
            Vector4[] bounds = { new Vector4(96.7f, 417f, 213.643f, 171.635f),
                new Vector4(88.5f, 381.3f, 225.435f, 196.174f), new Vector4(106.8f, 385.8f, 187.975f, 178.949f) };
            for (int i = 0; i < bounds.Length; i++)
            {
                RectTransform anchor = NewRect(parent, "ConnectionLines" + (i + 3));
                Vector4 b = bounds[i]; LabLobbyView.Place(anchor, b.x, b.y, b.z, b.w);
                anchor.localEulerAngles = new Vector3(0, 0, 180); view.lineLayouts[i] = anchor;
            }
        }

        private static void ConfigureScroll(GameLobbyView view)
        {
            UnityEngine.UI.ScrollRect scroll = view.roomContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
            if (scroll == null || scroll.content != view.roomContent)
                throw new InvalidOperationException("The authored room scroll references are incomplete.");
            RectTransform viewport = NewRect(scroll.transform, "Viewport"); Stretch(viewport);
            var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white;
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>(); mask.showMaskGraphic = false;
            view.roomContent.SetParent(viewport, false);
            scroll.viewport = viewport;
            var oldMask = scroll.GetComponent<UnityEngine.UI.RectMask2D>();
            if (oldMask != null) UnityEngine.Object.DestroyImmediate(oldMask);
            scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        }

        private static UnityEngine.UI.Button CloneButton(UnityEngine.UI.Button source, Transform parent, string name,
            string text, float x, float y, float width, float height, int fontSize)
        {
            UnityEngine.UI.Button button = UnityEngine.Object.Instantiate(source, parent, false);
            button.name = name;
            LabLobbyView.Place((RectTransform)button.transform, x, y, width, height);
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>();
            label.text = text; label.fontSize = fontSize;
            LabLobbyView.Place(label.rectTransform, 0, 0, width, height);
            return button;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = rect.anchoredPosition = Vector2.zero; }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
