using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Targeted, repeatable upgrade. Never rebuilds Home, StoryPreview, cut art or authored layouts.</summary>
    public static class GameOpeningTouchAssets
    {
        [MenuItem("Hapio/Story/Upgrade Opening Touch And Backgrounds")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before upgrading the opening assets.");
            var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(GameOpeningAssets.SequencePath);
            if (sequence == null || !sequence.Validate(out string error))
                throw new InvalidOperationException("The saved opening must be valid before upgrading.");
            // Existing approved views all have a full-page first background. Validate every view first;
            // after this one-time assignment, runtime uses an explicit role, never object names/sizes.
            foreach (var cut in sequence.cuts) ValidateBackground(cut.viewPrefab);
            int changed = 0;
            foreach (var cut in sequence.cuts)
            {
                if (cut.viewPrefab.layers[0].isBackground) continue;
                string path = AssetDatabase.GetAssetPath(cut.viewPrefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.GetComponent<StoryCutView>().layers[0].isBackground = true;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var opening = PrefabUtility.LoadPrefabContents(GameOpeningAssets.PrefabPath);
            try
            {
                ConfigureOpening(opening);
                PrefabUtility.SaveAsPrefabAsset(opening, GameOpeningAssets.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(opening); }
            Debug.Log($"HAPIO_OPENING_TOUCH_READY backgroundsUpdated={changed} cuts={sequence.cuts.Count}");
        }

        private static void ValidateBackground(StoryCutView view)
        {
            if (view == null || view.layers == null || view.layers.Length == 0 || view.layers[0] == null)
                throw new InvalidOperationException("An opening cut has no explicit first background to upgrade.");
            var layer = view.layers[0];
            var placement = layer.transform.parent as RectTransform;
            if (placement == null || placement.rect.width < 400 || placement.rect.height < 870 ||
                layer.GetComponentInChildren<UnityEngine.UI.Image>(true) == null)
                throw new InvalidOperationException("Check this cut's background manually before upgrading: " + view.name);
        }

        internal static void ConfigureOpening(GameObject root)
        {
            var player = root.GetComponent<StoryPlayer>();
            var controls = root.transform.Find("Safe Controls");
            if (player == null || controls == null)
                throw new InvalidOperationException("Repair the existing opening controls; do not replace the layout.");
            player.touchOnly = true;
            if (player.autoButton != null) player.autoButton.gameObject.SetActive(false);
            Transform surface = root.transform.Find("Tap To Continue");
            if (surface == null)
            {
                surface = new GameObject("Tap To Continue", typeof(RectTransform),
                    typeof(UnityEngine.UI.Image), typeof(StoryTapAdvance)).transform;
                surface.SetParent(root.transform, false);
            }
            var rect = (RectTransform)surface;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = surface.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            surface.GetComponent<StoryTapAdvance>().player = player;
            int index = controls.GetSiblingIndex();
            if (surface.GetSiblingIndex() < index) index--;
            surface.SetSiblingIndex(index); // Above art/dialogue, below the existing toolbar.
        }
    }
}
