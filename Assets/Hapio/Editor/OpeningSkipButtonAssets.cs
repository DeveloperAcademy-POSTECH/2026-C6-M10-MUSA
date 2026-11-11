using System;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Apply the authored Figma control without rebuilding the story or Home.</summary>
    public static class OpeningSkipButtonAssets
    {
        public const string SpritePath = "Assets/Hapio/Art/Story/Opening/OpeningSkip.png";

        [MenuItem("Hapio/Story/Upgrade Opening Skip Button")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before updating the opening button.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var root = PrefabUtility.LoadPrefabContents(GameOpeningAssets.PrefabPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, GameOpeningAssets.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void Configure(GameObject root)
        {
            var player = root.GetComponent<StoryPlayer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (player == null || player.skipButton == null || sprite == null)
                throw new InvalidOperationException("Opening player, skip button and authored sprite are required.");
            // A separate fitted page stays above the full-screen tap catcher and follows the artwork.
            var overlay = root.transform.Find("Opening Skip Overlay");
            if (overlay == null)
            {
                overlay = new GameObject("Opening Skip Overlay", typeof(RectTransform), typeof(StoryPageFit)).transform;
                overlay.SetParent(root.transform, false);
            }
            overlay.SetAsLastSibling();
            overlay.GetComponent<StoryPageFit>().Apply();
            var button = player.skipButton;
            var rect = (RectTransform)button.transform;
            rect.SetParent(overlay, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(7, -53);
            rect.sizeDelta = new Vector2(115, 38);
            rect.localScale = Vector3.one;
            var image = button.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.color = Color.white; // Cloud 80%, text/arrow 100% are baked separately in the Figma export.
            image.preserveAspect = true;
            image.raycastTarget = true;
            foreach (var label in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                label.gameObject.SetActive(false);
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
            button.colors = colors;
            player.hideSkipDuringCinematics = true;
        }
    }
}
