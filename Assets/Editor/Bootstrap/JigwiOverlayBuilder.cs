using System;
using C6Lab;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>Creates only the new editable overlay prefab. Never opens, saves or replaces a Scene.</summary>
    public static class JigwiOverlayBuilder
    {
        public const string PrefabPath = "Assets/Lab/Resources/Jigwi/ScreenOverlay.prefab";
        [MenuItem("C6 Lab/Jigwi/Create or Update Screen Overlay Prefab")]
        public static void Build() => CreateOrUpdate();
        public static GameObject CreateOrUpdate()
        {
            EnsureFolder("Assets/Lab/Resources", "Jigwi");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("JigwiScreenOverlay", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var view = root.GetComponent<LabJigwiScreenPresentation>() ?? root.AddComponent<LabJigwiScreenPresentation>();
                if (view.groupFireCurtain != null)
                    for (int i = view.groupFireCurtain.childCount - 1; i >= 0; i--)
                    {
                        Transform child = view.groupFireCurtain.GetChild(i);
                        if (child.name.StartsWith("OuterFlame", StringComparison.Ordinal)
                            && int.TryParse(child.name.Substring("OuterFlame".Length), out int stamp) && stamp >= 1 && stamp <= 9)
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                view.EnsureWidgets();
                Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                if (font != null) view.SetFont(font);
                foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
                view.Hide();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save the Jigwi screen overlay prefab.");
                Debug.Log("C6_JIGWI_SCREEN_OVERLAY_PREFAB_READY: existing battle Scenes and layouts preserved.");
                return saved;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }
        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
