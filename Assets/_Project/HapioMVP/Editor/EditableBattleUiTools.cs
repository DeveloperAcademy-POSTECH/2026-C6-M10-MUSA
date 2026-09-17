using System;
using System.IO;
using System.Linq;
using C6.Prototype.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    /// <summary>Creates the current battle UI once through Unity's object/scene serialization APIs.</summary>
    public static class EditableBattleUiTools
    {
        public const string ScenePath = "Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity";

        [MenuItem("C6/UI/Prepare Editable Battle UI")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing the battle UI.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("The battle scene has unsaved changes. Save it before preparing UI; no changes were discarded.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var hud = FindHud(scene);
                if (hud.UseSceneHierarchy)
                {
                    Validate(hud);
                    WriteReceipt("ALREADY_PREPARED", hud);
                    Selection.activeGameObject = hud.Canvas.gameObject;
                    return;
                }
                Undo.RecordObject(hud, "Prepare editable battle UI");
                hud.PrepareSceneHierarchy();
                Undo.RegisterCreatedObjectUndo(hud.Canvas.gameObject, "Create battle Canvas");
                var events = hud.GetComponentsInChildren<EventSystem>(true);
                foreach (var item in events) Undo.RegisterCreatedObjectUndo(item.gameObject, "Create battle event system");
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                Validate(hud);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save battle UI scene.");
                WriteReceipt("PREPARED", hud);
                Selection.activeGameObject = hud.Canvas.gameObject;
                Debug.Log("C6_EDITABLE_UI_READY: edit T09Overlay children in the saved ContinuousTransferBattle scene.");
            }
            catch
            {
                // Leave this loaded scene and its Undo history intact for inspection. Never force-save or discard on failure.
                throw;
            }
        }

        [MenuItem("C6/UI/Select Battle Canvas")]
        public static void SelectCanvas()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var hud = FindHud(scene);
            Validate(hud);
            Selection.activeGameObject = hud.Canvas.gameObject;
            EditorGUIUtility.PingObject(hud.Canvas);
        }

        [MenuItem("C6/UI/Validate Battle UI Connections")]
        public static void ValidateCurrent()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var hud = FindHud(scene);
                Validate(hud);
                WriteReceipt("VALIDATED", hud);
                Debug.Log("C6_EDITABLE_UI_CONNECTIONS_VALID: serialized references and existing controller links are present.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static T09Hud FindHud(Scene scene)
        {
            var huds = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T09Hud>(true)).ToArray();
            if (huds.Length != 1) throw new InvalidOperationException("Expected exactly one battle HUD in the current game scene.");
            return huds[0];
        }

        static void Validate(T09Hud hud)
        {
            if (!hud.UseSceneHierarchy) throw new InvalidOperationException("Run Prepare Editable Battle UI first.");
            if (!hud.ValidateSceneHierarchy(out var error)) throw new InvalidOperationException(error);
            if (hud.GetComponent<T09BattleController>() == null || hud.GetComponent<T09BattleController>().Hud != hud)
                throw new InvalidOperationException("Keep the existing battle controller and HUD connection on the game root.");
            if (hud.Canvas.gameObject.scene != hud.gameObject.scene)
                throw new InvalidOperationException("Canvas must be saved in the same scene as the battle controller.");
            if (hud.GetComponentsInChildren<Canvas>(true).Count(canvas => canvas == hud.Canvas) != 1)
                throw new InvalidOperationException("Keep the referenced battle Canvas under the existing game root.");
            foreach (var item in hud.Canvas.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                    throw new InvalidOperationException("Missing script on " + item.name);
        }

        static void WriteReceipt(string status, T09Hud hud)
        {
            string path = Path.Combine("Library", "EditableBattleUi", "authoring-receipt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new Receipt {
                status = status, scene = ScenePath, utc = DateTime.UtcNow.ToString("O"),
                children = hud.Canvas.GetComponentsInChildren<Transform>(true).Length,
                useSceneHierarchy = hud.UseSceneHierarchy
            }, true));
        }
        [Serializable] sealed class Receipt
        {
            public string status, scene, utc;
            public int children;
            public bool useSceneHierarchy;
        }
    }
}
