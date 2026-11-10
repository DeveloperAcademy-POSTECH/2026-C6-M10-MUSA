using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    public static class GameStrokeDesignUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/Strokes/";
        private const string Library = "Assets/Hapio/Resources/Hapio/UI/FigmaStrokeLibrary.asset";
        [Serializable] private sealed class Manifest { public Entry[] surfaces; }
        [Serializable] private sealed class Entry
        {
            public string name, source;
            public float width,height,exportWidth,exportHeight;
            public Vector2 offset;
        }

        [MenuItem("Hapio/Upgrade Figma UI Strokes")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Art+"Source/manifest.json"));
            var library = AssetDatabase.LoadAssetAtPath<GameFigmaStrokeLibrary>(Library);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<GameFigmaStrokeLibrary>();
                AssetDatabase.CreateAsset(library,Library);
            }
            library.surfaces = Array.ConvertAll(manifest.surfaces, entry =>
            {
                string path = Art+entry.name+".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 300;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                return new GameFigmaStrokeLibrary.Surface { name=entry.name, source=entry.source,
                    sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path), size=new Vector2(entry.width,entry.height),
                    offset=entry.offset,exportSize=new Vector2(entry.exportWidth,entry.exportHeight) };
            });
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            var root = PrefabUtility.LoadPrefabContents(GameLobbySceneUpgrade.PrefabPath);
            try
            {
                GameFigmaButton.Apply(root.GetComponent<GameLobbyView>());
                PrefabUtility.SaveAsPrefabAsset(root,GameLobbySceneUpgrade.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var theme = Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            theme.staminaCells = AssetDatabase.LoadAssetAtPath<Sprite>(Art+"StaminaCells.png");
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            Debug.Log("#615: 18 original Figma surfaces imported; lobby and battle HUD updated. Game over/results excluded.");
        }
    }
}
