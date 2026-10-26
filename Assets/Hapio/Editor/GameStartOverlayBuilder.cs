using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Produces only the new owned start overlay; never saves or rewrites a game Scene.</summary>
    public static class GameStartOverlayBuilder
    {
        public const string PrefabPath = "Assets/Hapio/Resources/Hapio/GameStartOverlay.prefab";

        [MenuItem("Hapio/Create Missing Battle Start Overlay")]
        public static void CreateMissing()
        {
            if (File.Exists(PrefabPath))
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<GameStartOverlay>() == null)
                    throw new InvalidOperationException("The existing start overlay is not a valid prefab.");
                return; // Preserve later Inspector/layout edits.
            }
            EnsureFolder("Assets/Hapio/Resources/Hapio");
            Font font = AssetDatabase.LoadAssetAtPath<Font>(GameSceneBuilder.SharedFontPath)
                ?? throw new InvalidOperationException("The existing Shilla font is missing.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("GameStartOverlay", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390f, 844f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f; group.interactable = group.blocksRaycasts = false;

                var backdrop = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                backdrop.transform.SetParent(root.transform, false);
                Stretch((RectTransform)backdrop.transform);
                var image = backdrop.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(.045f, .07f, .095f, 1f);
                image.raycastTarget = false;
                var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(GameSafeArea));
                safe.transform.SetParent(root.transform, false);
                Stretch((RectTransform)safe.transform);
                var label = new GameObject("Caption", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                label.transform.SetParent(safe.transform, false);
                var rect = (RectTransform)label.transform;
                rect.anchorMin = new Vector2(.06f, .34f);
                rect.anchorMax = new Vector2(.94f, .66f);
                rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
                var text = label.GetComponent<UnityEngine.UI.Text>();
                text.font = font;
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.98f, .96f, .9f, 1f);
                text.raycastTarget = false;
                text.supportRichText = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.text = string.Empty;

                var view = root.AddComponent<GameStartOverlay>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("visibility").objectReferenceValue = group;
                serialized.FindProperty("background").objectReferenceValue = image;
                serialized.FindProperty("caption").objectReferenceValue = text;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("The start overlay prefab could not be saved.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
