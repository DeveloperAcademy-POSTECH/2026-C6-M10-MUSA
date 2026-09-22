using System;
using System.Linq;
using C6.Prototype.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6.Editor
{
    /// <summary>Adds the orb-area guide to the saved Canvas without rebuilding the Figma HUD.</summary>
    public static class OrbWorkspaceBoundaryTools
    {
        private const string ScenePath = EditableBattleUiTools.ScenePath;
        private const string BoundaryName = "OrbWorkspaceBoundary";

        [MenuItem("C6/UI/Add Orb Workspace Boundary")]
        public static void Add()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before editing the orb boundary.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing battle scene first; no scene was replaced.");

            var huds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T09Hud>(true)).ToArray();
            if (huds.Length != 1 || huds[0].Canvas == null)
                throw new InvalidOperationException("Expected one battle HUD with its saved Canvas.");
            T09Hud hud = huds[0];
            if (!hud.ValidateSceneHierarchy(out string error))
                throw new InvalidOperationException(error);

            RectTransform canvas = hud.Canvas.transform as RectTransform;
            if (canvas.Find(BoundaryName) != null)
                throw new InvalidOperationException("OrbWorkspaceBoundary already exists; its authored style was left untouched.");

            var rootObject = new GameObject(BoundaryName, typeof(RectTransform), typeof(OrbWorkspaceBoundaryView));
            Undo.RegisterCreatedObjectUndo(rootObject, "Add orb workspace boundary");
            RectTransform frame = rootObject.GetComponent<RectTransform>();
            frame.SetParent(canvas, false);
            frame.SetAsFirstSibling();
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(320f, 170f); // Edit-mode preview; live dimensions come from the HUD.

            Color shadow = new Color(0.035f, 0.055f, 0.09f, 0.82f);
            Color edge = new Color(0.48f, 0.77f, 0.76f, 0.74f);
            Color portal = new Color(0.61f, 0.88f, 0.84f, 0.58f);
            AddTint(frame);
            Horizontal(frame, "UpperEdgeShadow", 1f, 5f, shadow);
            Horizontal(frame, "UpperEdgeHighlight", 1f, 2f, edge);
            Horizontal(frame, "LowerEdgeShadow", 0f, 5f, shadow);
            Horizontal(frame, "LowerEdgeHighlight", 0f, 2f, edge);
            Corner(frame, "LeftUpperOpening", 0f, 1f, 1f, -3f, 2f, 22f, portal);
            Corner(frame, "LeftLowerOpening", 0f, 0f, 1f, 3f, 2f, 22f, portal);
            Corner(frame, "RightUpperOpening", 1f, 1f, -1f, -3f, 2f, 22f, portal);
            Corner(frame, "RightLowerOpening", 1f, 0f, -1f, 3f, 2f, 22f, portal);

            rootObject.GetComponent<OrbWorkspaceBoundaryView>().SetReferences(hud, frame);
            EditorUtility.SetDirty(rootObject.GetComponent<OrbWorkspaceBoundaryView>());
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = rootObject;
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the battle scene after adding its boundary.");
            Debug.Log("C6_ORB_WORKSPACE_BOUNDARY_READY: scene artwork is editable; its rectangle follows the live orb workspace.");
        }

        [MenuItem("C6/UI/Add Orb Workspace Tint")]
        public static void AddTintToExisting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before editing the orb boundary.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing battle scene first; no scene was replaced.");
            var huds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T09Hud>(true)).ToArray();
            if (huds.Length != 1 || huds[0].Canvas == null)
                throw new InvalidOperationException("Expected one saved battle Canvas.");
            var frame = huds[0].Canvas.transform.Find(BoundaryName) as RectTransform;
            if (frame == null)
                throw new InvalidOperationException("Add the orb workspace boundary before adding its tint.");
            if (frame.Find("OrbAreaTint") != null)
                throw new InvalidOperationException("OrbAreaTint already exists; its authored color was left untouched.");
            AddTint(frame);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the orb area tint.");
        }

        private static void AddTint(RectTransform frame)
        {
            RectTransform panel = Image(frame, "OrbAreaTint", new Color(0.035f, 0.13f, 0.20f, 0.42f));
            panel.SetAsFirstSibling();
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
        }

        private static void Horizontal(RectTransform parent, string name, float yAnchor, float height, Color color)
        {
            RectTransform line = Image(parent, name, color);
            line.anchorMin = new Vector2(0f, yAnchor);
            line.anchorMax = new Vector2(1f, yAnchor);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = Vector2.zero;
            line.sizeDelta = new Vector2(0f, height);
        }

        private static void Corner(RectTransform parent, string name, float xAnchor, float yAnchor,
            float insetX, float insetY, float width, float height, Color color)
        {
            RectTransform line = Image(parent, name, color);
            line.anchorMin = line.anchorMax = new Vector2(xAnchor, yAnchor);
            line.pivot = new Vector2(0.5f, yAnchor);
            line.anchoredPosition = new Vector2(insetX, insetY);
            line.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform Image(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
