using System.Collections.Generic;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEngine;

namespace C6.Editor
{
    /// <summary>
    /// 오행 v1: fills OrbArtSet_Placeholder with element sprites from Art/Orbs/Placeholder.
    /// Raw: {element}_yin.png / {element}_yang.png. Combined: Combined/comb_{yinElement}_{yangElement}.png.
    /// </summary>
    public static class OrbElementArtSetup
    {
        private static readonly (OrbElement element, string name)[] Names =
        {
            (OrbElement.Fire, "fire"), (OrbElement.Water, "water"), (OrbElement.Wood, "wood"),
            (OrbElement.Metal, "metal"), (OrbElement.Earth, "earth")
        };

        [MenuItem("C6/Orb Art/Use Placeholder Element Art (오행)")]
        public static void UsePlaceholderElementArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Orb Art", "Stop Play mode first.", "OK");
                return;
            }
            // Create/link the base set (fire yin/yang/comb fallback) first.
            OrbArtSetup.UsePlaceholderFireArt();
            var set = AssetDatabase.LoadAssetAtPath<OrbArtSet>(OrbArtSetup.ArtSetPath);
            if (set == null) return;

            var so = new SerializedObject(set);
            var elements = so.FindProperty("elements");
            elements.ClearArray();
            int raw = 0;
            foreach (var (element, name) in Names)
            {
                var yin = Load(name + "_yin"); var yang = Load(name + "_yang");
                if (yin == null && yang == null) continue;
                elements.InsertArrayElementAtIndex(elements.arraySize);
                var entry = elements.GetArrayElementAtIndex(elements.arraySize - 1);
                entry.FindPropertyRelative("element").enumValueIndex = (int)element;
                entry.FindPropertyRelative("yin").objectReferenceValue = yin;
                entry.FindPropertyRelative("yang").objectReferenceValue = yang;
                raw += (yin != null ? 1 : 0) + (yang != null ? 1 : 0);
            }

            var combined = so.FindProperty("combined");
            combined.ClearArray();
            int pairs = 0;
            foreach (var (yinElement, yinName) in Names)
            foreach (var (yangElement, yangName) in Names)
            {
                var sprite = Load("Combined/comb_" + yinName + "_" + yangName);
                if (sprite == null) continue;
                combined.InsertArrayElementAtIndex(combined.arraySize);
                var entry = combined.GetArrayElementAtIndex(combined.arraySize - 1);
                entry.FindPropertyRelative("yinElement").enumValueIndex = (int)yinElement;
                entry.FindPropertyRelative("yangElement").enumValueIndex = (int)yangElement;
                entry.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                pairs++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Selection.activeObject = set;
            Debug.Log($"[OrbArt] 오행 placeholder art linked: raw sprites={raw}/10, combined pairs={pairs}/25");
        }

        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(OrbArtSetup.PlaceholderFolder + "/" + name + ".png");
    }
}
