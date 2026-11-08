using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Imports the Figma component art without rewriting any Scene or physics profile.</summary>
    public static class GameBattleUiUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/Battle/";
        private const string Data = "Assets/Hapio/Resources/Hapio/UI/";
        [MenuItem("Hapio/Upgrade Battle UI Components")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = Path.GetFileName(path).StartsWith("BoardStage") ? TextureImporterType.Default : TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100;
                importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            string appearancePath = Data + "BattleOrbAppearance.asset";
            if (AssetDatabase.LoadAssetAtPath<LabOrbAppearance>(appearancePath) == null)
                AssetDatabase.CopyAsset("Assets/Lab/Art/Orbs/LabOrbAppearance.asset", appearancePath);
            var appearance = AssetDatabase.LoadAssetAtPath<LabOrbAppearance>(appearancePath);
            var serialized = new SerializedObject(appearance);
            string[] names = { "metalSprite", "waterSprite", "fireSprite", "woodSprite", "earthSprite", "drumSprite" };
            string[] files = { "e3ff0", "1b7bc", "ca403", "02a2c", "31348", "GenerateDrum" };
            for (int i=0;i<names.Length;i++) serialized.FindProperty(names[i]).objectReferenceValue = Sprite(files[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(appearance);
            var theme = AssetDatabase.LoadAssetAtPath<GameBattleUiTheme>(Data + "BattleUiTheme.asset");
            if (theme == null) { theme = ScriptableObject.CreateInstance<GameBattleUiTheme>(); AssetDatabase.CreateAsset(theme,Data+"BattleUiTheme.asset"); }
            theme.orbAppearance = appearance;
            theme.boardTextures = new[] { Texture("BoardStage1"), Texture("BoardStage2"), Texture("BoardStage3") };
            theme.paper = AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Source/04f1b.png");
            // Import the original paper as a sprite too (kept as the unmodified Figma source).
            var paper = (TextureImporter)AssetImporter.GetAtPath(Art+"Source/04f1b.png");
            paper.textureType=TextureImporterType.Sprite; paper.npotScale=TextureImporterNPOTScale.None; paper.mipmapEnabled=false; paper.SaveAndReimport();
            theme.paper = AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Source/04f1b.png");
            theme.underlineDark=Sprite("21af4"); theme.underlineLight=Sprite("34af9"); theme.pauseStroke=Sprite("4cf20"); theme.staminaCells=Sprite("StaminaCells");
            theme.playerBadges = new[] { Sprite("Player1"),Sprite("Player2"),Sprite("Player3"),Sprite("Player4"),Sprite("Player5"),Sprite("Mini6") };
            theme.minimapBadges = new[] { Sprite("Mini1"),Sprite("Mini2"),Sprite("Mini3"),Sprite("Mini4"),Sprite("Mini5"),Sprite("Mini6") };
            theme.teamPositionArtwork = new[] { Sprite("Position2v2Red"),Sprite("Position2v2Blue"),Sprite("Position3v3Red"),Sprite("Position3v3Blue") };
            theme.teamBlueMarker=Sprite("TeamBlue"); theme.teamRedMarker=Sprite("TeamRed"); theme.drumIcon=Sprite("DrumIcon");
            theme.teamPairConnector = Sprite("07b57");
            theme.teamTrioConnector = Sprite("a49d1");
            theme.blueFlagDark=Sprite("50224"); theme.redFlagDark=Sprite("3aff5"); theme.blueFlagLight=Sprite("4da2d"); theme.redFlagLight=Sprite("a794d");
            theme.tickDark=Sprite("ead0b"); theme.tickLight=Sprite("57d73"); theme.tickCenter=Sprite("0e7c5");
            theme.medium=AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
            theme.bold=AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            theme.staminaBorders=new[] { GameBattleUiColors.Stamina1,GameBattleUiColors.Stamina2,GameBattleUiColors.Stamina3 };
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            Debug.Log("Hapio Battle UI components imported. Scenes, backgrounds and physics profiles retained.");
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png");
        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Art+name+".png");
    }
}
