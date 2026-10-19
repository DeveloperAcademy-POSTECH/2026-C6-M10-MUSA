using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab.Editor
{
    public static class LabTemporaryBackdropUpgrade
    {
        [MenuItem("C6 Lab/Apply Temporary Battle Background")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the background.");
            const string path = "Assets/Lab/Art/UI/TemporaryBattleBackground.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var view = GameObject.Find("BattleCamera").GetComponent<Camera>();
            var existing = view.transform.Find("Temporary Battle Background");
            var root = existing != null ? existing.gameObject : new GameObject("Temporary Battle Background");
            root.transform.SetParent(view.transform, false);
            var renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            renderer.color = Color.white;
            renderer.sortingOrder = -1000;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var backdrop = root.GetComponent<LabTemporaryBackdrop>();
            if (backdrop == null) backdrop = root.AddComponent<LabTemporaryBackdrop>();
            backdrop.FitToCamera();

            // Continue the same artwork behind the paper's transparent curved edge.
            // The orb camera clears its own viewport, so the battle camera alone cannot fill it.
            var orbView = GameObject.Find("OrbCamera").GetComponent<Camera>();
            var continuation = orbView.transform.Find("Temporary Board Background");
            var boardBackground = continuation != null ? continuation.gameObject
                : new GameObject("Temporary Board Background");
            boardBackground.layer = LayerMask.NameToLayer("LabOrbBoard");
            boardBackground.transform.SetParent(orbView.transform, false);
            var boardRenderer = boardBackground.GetComponent<SpriteRenderer>();
            if (boardRenderer == null) boardRenderer = boardBackground.AddComponent<SpriteRenderer>();
            boardRenderer.sprite = renderer.sprite;
            boardRenderer.color = Color.white;
            boardRenderer.sortingOrder = -1000;
            boardRenderer.shadowCastingMode = ShadowCastingMode.Off;
            boardRenderer.receiveShadows = false;
            var boardBackdrop = boardBackground.GetComponent<LabTemporaryBackdrop>();
            if (boardBackdrop == null) boardBackdrop = boardBackground.AddComponent<LabTemporaryBackdrop>();
            boardBackdrop.alignToScreenTop = true;
            boardBackdrop.compositionCamera = view;
            boardBackdrop.FitToCamera();

            // Keep the miss-surface collider while hiding the prototype's grey floor.
            GameObject.Find("BattleFloor").GetComponent<MeshRenderer>().enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
