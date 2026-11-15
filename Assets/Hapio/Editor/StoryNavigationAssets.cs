using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Editor
{
    public static class StoryNavigationAssets
    {
        [MenuItem("Hapio/Story/Update Story Touch Navigation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop playback first.");
            foreach (var guid in AssetDatabase.FindAssets("t:StorySequence", new[] { "Assets/Hapio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/Hapio/Story/Opening/") &&
                    !path.EndsWith("/GameEnding.asset") && path != "Assets/Hapio/Story/StageStories/Ending.asset") continue;
                var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(path);
                sequence.sideTapNavigation = true;
                EditorUtility.SetDirty(sequence);
                AssetDatabase.SaveAssetIfDirty(sequence);
            }
            const string cutPath = "Assets/Hapio/Story/Opening/Views/OP_003.prefab";
            var root = PrefabUtility.LoadPrefabContents(cutPath);
            try
            {
                var text = root.GetComponentsInChildren<Text>(true).Single(t => t.name == "Text [4553:4317]");
                MatchLineHeight(text, 33 * 4);
                PrefabUtility.SaveAsPrefabAsset(root, cutPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Unity's mixed-size rich text adds a font-size offset to line spacing.
        // Measure the actual authored text rather than a pair of plain sample glyphs.
        public static void MatchLineHeight(Text text, float height)
        {
            using (var generator = new TextGenerator())
            {
                var settings = text.GetGenerationSettings(new Vector2(4000, 4000));
                float low = 0, high = 3;
                for (int i = 0; i < 20; i++)
                {
                    settings.lineSpacing = (low + high) * .5f;
                    generator.Populate(text.text, settings);
                    if (generator.lines.Count != 2) throw new InvalidOperationException("Expected two authored lines.");
                    // Both line starts carry the base-size run (including the closing size tag).
                    // UILineInfo.topY instead follows the largest glyph and adds extra leading.
                    float actual = generator.characters[generator.lines[0].startCharIdx].cursorPos.y
                        - generator.characters[generator.lines[1].startCharIdx].cursorPos.y;
                    if (Mathf.Abs(actual - height) < .1f) break;
                    if (actual < height) low = settings.lineSpacing; else high = settings.lineSpacing;
                }
                text.lineSpacing = settings.lineSpacing;
            }
        }
    }
}
