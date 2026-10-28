using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Connect the saved story to Home without rebuilding any authored menu or cut.</summary>
    public static class GameOpeningAssets
    {
        public const string PrefabPath = "Assets/Hapio/Story/GameOpening.prefab";
        public const string SequencePath = "Assets/Hapio/Story/Opening/Opening.asset";

        [MenuItem("Hapio/Story/Connect Opening To Home")]
        public static void InstallHomeOpening()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before connecting the opening.");
            var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(SequencePath);
            if (sequence == null || !sequence.Validate(out _))
                throw new InvalidOperationException("The approved opening sequence must be valid before connecting Home.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(sequence);
            ValidatePrefab(prefab, sequence);

            Scene previous = SceneManager.GetActiveScene();
            Scene home = SceneManager.GetSceneByPath(GameScenePaths.Home);
            bool owned = !home.IsValid() || !home.isLoaded;
            if (!owned && home.isDirty)
                throw new InvalidOperationException("Save the current Home edits before connecting the opening.");
            if (owned) home = EditorSceneManager.OpenScene(GameScenePaths.Home, OpenSceneMode.Additive);
            try
            {
                var lobby = home.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameLobbyView>(true)).Single();
                var controllers = home.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOpeningController>(true)).ToArray();
                if (controllers.Length > 1) throw new InvalidOperationException("Home has more than one opening controller.");
                if (controllers.Length == 1)
                {
                    var existing = controllers[0];
                    if (existing.player == null || existing.openingRoot == null || existing.lobbyView != lobby)
                        throw new InvalidOperationException("Repair the existing Home opening references instead of replacing its layout.");
                    Debug.Log("HAPIO_HOME_OPENING_ALREADY_CONNECTED");
                    return;
                }

                var ui = (GameObject)PrefabUtility.InstantiatePrefab(prefab, home);
                ui.name = "Game Opening";
                var controllerRoot = new GameObject("Game Opening Controller");
                SceneManager.MoveGameObjectToScene(controllerRoot, home);
                var controller = controllerRoot.AddComponent<GameOpeningController>();
                controller.openingRoot = ui;
                controller.player = ui.GetComponent<StoryPlayer>();
                controller.lobbyView = lobby;
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(home);
                if (!EditorSceneManager.SaveScene(home, GameScenePaths.Home))
                    throw new InvalidOperationException("Could not save the Home opening connection.");
                Debug.Log("HAPIO_HOME_OPENING_CONNECTED: app start -> opening -> original title -> rooms.");
            }
            finally
            {
                if (owned && home.IsValid() && home.isLoaded) EditorSceneManager.CloseScene(home, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static GameObject CreatePrefab(StorySequence sequence)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = SceneManager.GetSceneByPath(StoryWorkspaceBuilder.PreviewScene);
            bool owned = !preview.IsValid() || !preview.isLoaded;
            if (owned) preview = EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene, OpenSceneMode.Additive);
            GameObject copy = null;
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<StoryPlayer>(true)).Single();
                copy = Object.Instantiate(source.gameObject);
                copy.name = "Game Opening";
                SceneManager.MoveGameObjectToScene(copy, preview);
                var player = copy.GetComponent<StoryPlayer>();
                var title = copy.GetComponent<StoryOpeningTitle>();
                if (title != null)
                {
                    if (title.titlePage != null && title.titlePage.transform.IsChildOf(copy.transform))
                        Object.DestroyImmediate(title.titlePage);
                    Object.DestroyImmediate(title);
                }
                var controls = copy.transform.Find("Safe Controls");
                if (controls == null) throw new InvalidOperationException("StoryPreview is missing its saved safe controls.");
                controls.gameObject.SetActive(true);
                var replay = controls.Find("Toolbar/Replay");
                if (replay != null) replay.gameObject.SetActive(false);
                player.sequence = sequence;
                player.playOnStart = false;
                player.onCompleted = new UnityEvent();
                player.onSkipped = new UnityEvent();
                var canvas = copy.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var backdrop = new GameObject("Viewport Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                backdrop.transform.SetParent(copy.transform, false);
                backdrop.transform.SetAsFirstSibling();
                var rect = (RectTransform)backdrop.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                var image = backdrop.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(.12f, .10f, .08f, 1f);
                image.raycastTarget = true;

                // Home intentionally has no listener on its camera. This one exists only while the opening is visible.
                if (copy.GetComponent<AudioListener>() == null) copy.AddComponent<AudioListener>();
                if (player.voiceSource != null)
                {
                    player.voiceSource.playOnAwake = false;
                    player.voiceSource.spatialBlend = 0f;
                }
                copy.SetActive(false);
                var saved = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save the opening prefab.");
                return saved;
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                if (owned && preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void ValidatePrefab(GameObject prefab, StorySequence sequence)
        {
            var player = prefab.GetComponent<StoryPlayer>();
            if (player == null || player.sequence != sequence || player.cutHost == null || player.nextButton == null
                || player.skipButton == null || player.GetComponent<Canvas>() == null
                || player.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                throw new InvalidOperationException("The existing opening prefab is incomplete; repair it without replacing authored layout.");
        }
    }
}
