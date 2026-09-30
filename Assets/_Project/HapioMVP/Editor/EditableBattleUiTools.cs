using System;
using System.IO;
using System.Linq;
using C6.Prototype.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        [MenuItem("C6/UI/Prepare Monster Interference UI")]
        public static void PrepareMonsterInterferenceUi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing monster interference UI.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException(
                    "The battle scene has unsaved changes. Save it before preparing monster interference UI; no changes were discarded.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var hud = FindHud(scene);
            Validate(hud);
            var controller = hud.GetComponent<T09BattleController>();
            var canvas = hud.Canvas.transform;
            var existing = canvas.GetComponentInChildren<MonsterInterferenceOverlay>(true);
            var overlay = existing != null
                ? existing
                : CreateInterferenceOverlay(canvas, hud.ActionLabel != null ? hud.ActionLabel.font : null);

            var serializedOverlay = new SerializedObject(overlay);
            var left = overlay.transform.Find("BlockedLeftEdge")?.GetComponent<Graphic>();
            var right = overlay.transform.Find("BlockedRightEdge")?.GetComponent<Graphic>();
            var message = overlay.transform.Find("InterferenceMessage")?.GetComponent<Text>();
            serializedOverlay.FindProperty("leftEdge").objectReferenceValue = left;
            serializedOverlay.FindProperty("rightEdge").objectReferenceValue = right;
            serializedOverlay.FindProperty("message").objectReferenceValue = message;
            serializedOverlay.ApplyModifiedPropertiesWithoutUndo();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("interferenceOverlay").objectReferenceValue = overlay;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            if (hud.ResultOverlay != null && hud.ResultOverlay.transform.parent == canvas)
                overlay.transform.SetSiblingIndex(hud.ResultOverlay.transform.GetSiblingIndex());
            else
                overlay.transform.SetAsLastSibling();

            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save monster interference UI to the battle scene.");

            Selection.activeGameObject = overlay.gameObject;
            EditorGUIUtility.PingObject(overlay.gameObject);
            Debug.Log("C6_MONSTER_INTERFERENCE_UI_READY: red board-edge block and centered monster message are connected.");
        }

        static MonsterInterferenceOverlay CreateInterferenceOverlay(Transform canvas, Font font)
        {
            var rootObject = new GameObject("MonsterInterferenceOverlay", typeof(RectTransform),
                typeof(MonsterInterferenceOverlay));
            Undo.RegisterCreatedObjectUndo(rootObject, "Create monster interference UI");
            var root = (RectTransform)rootObject.transform;
            root.SetParent(canvas, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            Image sourceLeft = null;
            Image sourceRight = null;
            var attackWarning = canvas.GetComponentInChildren<MonsterAttackWarning>(true);
            if (attackWarning != null)
            {
                sourceLeft = attackWarning.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(item => item.name == "LeftEdge");
                sourceRight = attackWarning.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(item => item.name == "RightEdge");
            }

            CreateInterferenceEdge("BlockedLeftEdge", root, sourceLeft);
            CreateInterferenceEdge("BlockedRightEdge", root, sourceRight);

            var messageObject = new GameObject("InterferenceMessage", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            Undo.RegisterCreatedObjectUndo(messageObject, "Create monster interference message");
            var messageRect = (RectTransform)messageObject.transform;
            messageRect.SetParent(root, false);
            var message = messageObject.GetComponent<Text>();
            message.font = font;
            message.text = MonsterInterferenceOverlay.DefaultMessage;
            message.fontSize = 20;
            message.fontStyle = FontStyle.Bold;
            message.alignment = TextAnchor.MiddleCenter;
            message.color = new Color(1f, .80f, .39f, 1f);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Overflow;
            message.resizeTextForBestFit = true;
            message.resizeTextMinSize = 14;
            message.resizeTextMaxSize = 22;
            message.raycastTarget = false;
            message.enabled = false;
            var outline = messageObject.GetComponent<Outline>();
            outline.effectColor = new Color(.15f, .06f, .01f, .9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            return rootObject.GetComponent<MonsterInterferenceOverlay>();
        }

        static Image CreateInterferenceEdge(string name, Transform parent, Image source)
        {
            var edgeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(edgeObject, "Create monster interference edge");
            edgeObject.transform.SetParent(parent, false);
            var image = edgeObject.GetComponent<Image>();
            if (source != null)
            {
                image.sprite = source.sprite;
                image.type = source.type;
                image.preserveAspect = source.preserveAspect;
                image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                edgeObject.transform.localScale = source.transform.localScale;
            }
            image.color = new Color(1f, .08f, .05f, .88f);
            image.raycastTarget = false;
            image.maskable = false;
            image.enabled = false;
            return image;
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
