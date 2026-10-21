using System;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// 요괴 방해공작 그림 (#251): Resources/Sabotage의 PNG는 코드가 이름으로 불러 쓰는 Sprite로 가져온다.
    /// Inspector에서 하나씩 바꾸지 않아도 된다.
    /// </summary>
    public sealed class LabSabotageArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Lab/Resources/Sabotage/";

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
