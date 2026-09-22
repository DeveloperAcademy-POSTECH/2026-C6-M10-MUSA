using System;
using System.Linq;
using C6.Prototype.Battle;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Editor
{
    /// <summary>Applies the approved flat wooden-board layout to the existing saved battle scene.</summary>
    public static class WornYeotBoardLayoutTools
    {
        private const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";
        private const string SpritePath = "Assets/_Project/HapioMVP/Art/UI/WornYeotBoard.png";

        [MenuItem("C6/UI/Apply Worn Yeot Board Layout")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before changing the saved battle UI.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the clean ContinuousTransferBattle scene first.");

            var huds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T09Hud>(true)).ToArray();
            if (huds.Length != 1)
                throw new InvalidOperationException("Expected exactly one saved battle HUD.");
            string error = string.Empty;
            if (!huds[0].UseSceneHierarchy || !huds[0].ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("Expected one valid saved battle HUD: " + error);
            T09Hud hud = huds[0];
            var overlay = (RectTransform)hud.Canvas.transform;
            var lower = overlay.Find("SafeArea/LowerSafeViewport/LowerHudContent") as RectTransform;
            var divider = overlay.Find("ViewportDivider") as RectTransform;
            var boundary = overlay.Find("OrbWorkspaceBoundary") as RectTransform;
            if (lower == null || divider == null || boundary == null)
                throw new InvalidOperationException("The existing battle UI layout is incomplete.");
            var footer = lower.Find("ResourceControls") as RectTransform;
            var buttons = footer != null ? footer.Find("ResourceButtons") as RectTransform : null;
            var stamina = footer != null ? footer.Find("PersonalStaminaPanel") as RectTransform : null;
            var timeBar = footer != null ? footer.Find("TeamTimeBar") as RectTransform : null;
            if (footer == null || buttons == null || stamina == null || timeBar == null)
                throw new InvalidOperationException("The existing footer controls have changed; no UI was moved.");
            if (hud.Layout == null || hud.Layout.OrbCamera == null ||
                hud.Layout.OrbCamera.transform.Find("OrbWoodenPlate") != null)
                throw new InvalidOperationException("Orb camera missing or wooden plate already applied.");

            Sprite sprite = ImportBoardSprite();
            Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Apply wooden orb board layout");
            footer.sizeDelta = new Vector2(footer.sizeDelta.x, ScreenLayoutConfig.MinimalBattleFooterHeight);
            buttons.anchoredPosition = new Vector2(buttons.anchoredPosition.x, 113f);
            stamina.anchoredPosition = new Vector2(stamina.anchoredPosition.x, 42f);

            // The divider is outside both camera-view masks and already follows their actual split.
            Undo.SetTransformParent(timeBar, divider, "Move team time to viewport divider");
            timeBar.anchorMin = timeBar.anchorMax = new Vector2(.5f, .5f);
            timeBar.pivot = new Vector2(.5f, .5f);
            timeBar.sizeDelta = new Vector2(322f, 43f);
            timeBar.anchoredPosition = Vector2.zero;
            timeBar.localScale = Vector3.one;

            // Retain the editable old guide hierarchy while replacing its visuals with the board.
            foreach (UnityEngine.UI.Graphic graphic in boundary.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.enabled = false;

            var board = new GameObject("OrbWoodenPlate", typeof(SpriteRenderer), typeof(OrbWoodenPlateView));
            Undo.RegisterCreatedObjectUndo(board, "Add wooden orb board");
            board.transform.SetParent(hud.Layout.OrbCamera.transform, false);
            board.transform.localPosition = new Vector3(0f, 0f, 10f);
            int orbLayer = LayerMask.NameToLayer("C6Orbs");
            if (orbLayer < 0) throw new InvalidOperationException("Missing C6Orbs layer.");
            board.layer = orbLayer;
            var renderer = board.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;
            board.GetComponent<OrbWoodenPlateView>().Configure(hud, renderer);

            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!hud.ValidateSceneHierarchy(out error))
                throw new InvalidOperationException("Wooden board broke a saved HUD binding: " + error);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the battle scene.");
            Debug.Log("C6_WORN_YEOT_BOARD_READY: saved time divider, larger workspace, controls and SpriteRenderer plate.");
        }

        private static Sprite ImportBoardSprite()
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing board texture: " + SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false; // The asset is a full-bleed rectangular board.
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null) throw new InvalidOperationException("Could not import the board as a Sprite.");
            return sprite;
        }
    }
}
