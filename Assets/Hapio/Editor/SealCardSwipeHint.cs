using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>The seal card's swipe hint uses the approved Figma line on all three stage cards.</summary>
    public static class SealCardSwipeHint
    {
        internal const string Text = "좌우로 밀어 부적 뒤집기";
        internal const string RetiredText = "밀어서 뒤집기";

        [MenuItem("Hapio/Story/Restore Seal Card Swipe Hint")]
        public static void Restore()
        {
            foreach (string name in new[] { "Gumiho", "Yagwanggui", "Jigwi" })
            {
                string path = StageStoryAssets.Root + "/Views/" + name + "_SealCard.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var flip = root.GetComponentInChildren<StoryCardFlip>(true);
                    var hint = flip != null && flip.swipeHint != null ? flip.swipeHint.GetComponentInChildren<UnityEngine.UI.Text>(true) : null;
                    if (hint == null) { Debug.LogError("Seal card swipe hint missing: " + path); continue; }
                    hint.text = Text;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
