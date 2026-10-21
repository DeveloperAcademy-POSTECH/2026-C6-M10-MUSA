using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// 요괴 방해공작 그림 (#251): Resources/Sabotage의 PNG는 코드가 이름으로 불러 쓰는 Sprite로 가져온다.
    /// Inspector에서 하나씩 바꾸지 않아도 된다.
    /// </summary>
    public sealed class LabSabotageArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Lab/Resources/Sabotage/";
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";

        /// <summary>Moves only the saved minimap above the new neighbour charm bubble; retains the existing Scene.</summary>
        [MenuItem("C6 Lab/Upgrade Saved Charm Minimap Spacing")]
        public static void UpgradeSavedCharmMinimapSpacing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before upgrading the saved charm spacing.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save or review the Lab Scene changes before upgrading charm spacing.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                LabDrumMinimap minimap = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (LabSceneController controller in root.GetComponentsInChildren<LabSceneController>(true))
                    {
                        LabDrumMinimap saved = controller.GetComponent<LabDrumMinimap>();
                        if (saved == null) continue;
                        if (minimap != null)
                            throw new InvalidOperationException("The saved Lab Scene must contain one controller minimap.");
                        minimap = saved;
                    }
                if (minimap == null)
                    throw new InvalidOperationException("The saved controller's drum minimap is missing.");

                var serialized = new SerializedObject(minimap);
                serialized.FindProperty(nameof(LabDrumMinimap.gapAboveBadge)).floatValue = 120f;
                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Unity could not save the charm minimap spacing.");
                }
                Debug.Log("C6_LAB_CHARM_MINIMAP_SPACING_SAVED gap=120");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal)
                || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
