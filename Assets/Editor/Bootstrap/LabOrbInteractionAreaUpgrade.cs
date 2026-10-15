using System;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Binds one editable, invisible rectangle to the saved Lab board layout.</summary>
    public static class LabOrbInteractionAreaUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string AreaName = "OrbInteractionArea";
        private const float InitialBottom = .0881007f;
        private const float CurveSafeTop = .40f;
        private const float LowerHudGap = 4f;

        [MenuItem("C6 Lab/Fit Orb Interaction Area")]
        public static void Apply()
        {
            Scene scene = RequireCurrentScene();
            if (scene.isDirty)
                throw new InvalidOperationException("Save the open Lab Scene before fitting the orb interaction area.");

            LabSceneController controller = RequireSingleComponent<LabSceneController>(scene);
            LabHud hud = RequireSingleComponent<LabHud>(scene);
            if (controller.board == null || controller.orbCamera == null || controller.config == null)
                throw new InvalidOperationException("The saved LabSceneController board, camera and config references are required.");
            RectTransform battle = RequireBattleUi(hud, scene);
            RectTransform lowerHud = RequireRect(battle.Find("BottomHudBackground"), "BottomHudBackground");
            Transform existing = battle.Find(AreaName);
            RectTransform area = existing == null ? null : RequireRect(existing, AreaName);
            if (area != null && (area.GetComponent<UnityEngine.UI.Graphic>() != null
                || area.GetComponent<CanvasRenderer>() != null))
                throw new InvalidOperationException("OrbInteractionArea must remain an invisible RectTransform without a Graphic.");
            if (lowerHud.anchorMax.y > InitialBottom + .00001f)
                throw new InvalidOperationException("The lower HUD was moved above the planned interaction-area bottom. Review the new layout before fitting it.");

            const string undoLabel = "Fit Orb Interaction Area";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoLabel);
            if (area == null)
            {
                var gameObject = new GameObject(AreaName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(gameObject, undoLabel);
                area = gameObject.GetComponent<RectTransform>();
                area.SetParent(battle, false);
            }
            Undo.RecordObject(area, undoLabel);
            area.anchorMin = new Vector2(0f, InitialBottom);
            area.anchorMax = new Vector2(1f, CurveSafeTop);
            area.pivot = new Vector2(.5f, .5f);
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.localRotation = Quaternion.identity;
            area.localScale = Vector3.one;
            Undo.RecordObject(controller, undoLabel);
            controller.orbInteractionArea = area;
            controller.lowerHudBoundary = lowerHud;
            controller.lowerHudGap = LowerHudGap;

            ValidateBindings(scene, controller, battle, lowerHud, area);
            EditorUtility.SetDirty(area);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the fitted Lab interaction area.");
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("C6_LAB_ORB_INTERACTION_AREA_SAVED: editable rectangle is bound above the existing lower HUD; no HUD positions were changed.");
        }

        [MenuItem("C6 Lab/Validate Orb Interaction Area")]
        public static void ValidateCurrentScene()
        {
            Scene scene = RequireCurrentScene();
            LabSceneController controller = RequireSingleComponent<LabSceneController>(scene);
            LabHud hud = RequireSingleComponent<LabHud>(scene);
            RectTransform battle = RequireBattleUi(hud, scene);
            RectTransform lowerHud = RequireRect(battle.Find("BottomHudBackground"), "BottomHudBackground");
            RectTransform area = RequireRect(battle.Find(AreaName), AreaName);
            ValidateBindings(scene, controller, battle, lowerHud, area);
            Debug.Log("C6_LAB_ORB_INTERACTION_AREA_VALID: saved rectangle, controller references, curve-safe top and lower HUD gap are connected.");
        }

        private static Scene RequireCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before fitting or validating the saved orb interaction area.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open the saved Assets/Lab/Scenes/Lab.unity Scene before fitting or validating its interaction area.");
            return scene;
        }

        private static T RequireSingleComponent<T>(Scene scene) where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T candidate in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null)
                        throw new InvalidOperationException("The Lab Scene has more than one " + typeof(T).Name + ".");
                    result = candidate;
                }
            }
            if (result == null)
                throw new InvalidOperationException("The saved Lab Scene has no " + typeof(T).Name + ".");
            return result;
        }

        private static RectTransform RequireBattleUi(LabHud hud, Scene scene)
        {
            if (hud.battleUi == null || hud.battleUi.scene != scene)
                throw new InvalidOperationException("LabHud must reference FigmaBattleUi in the current saved Scene.");
            return RequireRect(hud.battleUi.transform, "FigmaBattleUi");
        }

        private static RectTransform RequireRect(Transform transform, string name)
        {
            if (!(transform is RectTransform rect))
                throw new InvalidOperationException(name + " is missing or has no RectTransform.");
            return rect;
        }

        private static void ValidateBindings(Scene scene, LabSceneController controller,
            RectTransform battle, RectTransform lowerHud, RectTransform area)
        {
            if (controller.orbInteractionArea != area || controller.lowerHudBoundary != lowerHud
                || !Mathf.Approximately(controller.lowerHudGap, LowerHudGap))
                throw new InvalidOperationException("LabSceneController is not bound to the interaction area, lower HUD and four-unit gap.");
            if (area.gameObject.scene != scene || area.parent != battle || lowerHud.parent != battle)
                throw new InvalidOperationException("The interaction area and lower HUD must belong to the existing FigmaBattleUi.");
            if (area.GetComponent<UnityEngine.UI.Graphic>() != null
                || area.GetComponent<CanvasRenderer>() != null)
                throw new InvalidOperationException("OrbInteractionArea must have no Graphic or CanvasRenderer; its rectangle is a layout guide only.");
            if (!Mathf.Approximately(area.anchorMin.x, 0f)
                || !Mathf.Approximately(area.anchorMax.x, 1f)
                || area.anchorMin.y < lowerHud.anchorMax.y - .00001f
                || area.anchorMax.y > CurveSafeTop + .00001f
                || area.anchorMin.y >= area.anchorMax.y
                || area.offsetMin.sqrMagnitude > .00001f || area.offsetMax.sqrMagnitude > .00001f)
                throw new InvalidOperationException("OrbInteractionArea must span the screen width above the lower HUD and below the paper's curve-safe top.");
        }
    }
}
