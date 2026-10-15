using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Bakes the existing paper PNG into editable Scene curves; changes no texture import settings.</summary>
    public static class LabCurvedOrbAreaUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string TexturePath = "Assets/Lab/Art/UI/BoardPlate.png";
        private const string HudTexturePath = "Assets/Lab/Art/BottomHud/BottomHudBackground.png";
        private const int SampleCount = 65;
        private const float AlphaThreshold = .5f;

        [MenuItem("C6 Lab/Fit Curved Orb Area")]
        public static void Apply()
        {
            Scene scene = RequireScene();
            if (scene.isDirty)
                throw new InvalidOperationException("Save the open Lab Scene before baking its curved paper area.");
            LabSceneController controller = RequireSingle<LabSceneController>(scene);
            if (controller.board == null || controller.orbCamera == null || controller.config == null)
                throw new InvalidOperationException("The saved Lab controller needs its board, orb camera and Config.");
            LabHud hud = RequireSingle<LabHud>(scene);
            RectTransform battle = hud.battleUi != null ? hud.battleUi.GetComponent<RectTransform>() : null;
            if (battle == null || battle.gameObject.scene != scene)
                throw new InvalidOperationException("LabHud must reference this Scene's existing FigmaBattleUi.");
            RectTransform area = battle.Find("OrbInteractionArea") as RectTransform;
            RectTransform lowerHud = battle.Find("BottomHudBackground") as RectTransform;
            Transform plate = controller.board.transform.Find("OrbBoardPlate");
            if (area == null || lowerHud == null || plate == null)
                throw new InvalidOperationException("OrbInteractionArea, BottomHudBackground and OrbBoardPlate must already exist.");
            RequireVisualPlate(plate);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (source == null || plate.GetComponent<Renderer>().sharedMaterial.mainTexture != source)
                throw new InvalidOperationException("The saved OrbBoardPlate must display the existing BoardPlate.png.");
            UnityEngine.UI.Image hudImage = lowerHud.GetComponent<UnityEngine.UI.Image>();
            Texture2D hudSource = AssetDatabase.LoadAssetAtPath<Texture2D>(HudTexturePath);
            if (hudSource == null || hudImage == null || hudImage.sprite == null
                || hudImage.sprite.texture != hudSource || hudImage.type != UnityEngine.UI.Image.Type.Simple
                || hudImage.preserveAspect)
                throw new InvalidOperationException("BottomHudBackground must keep its existing full-rect Simple sprite mapping.");
            LabOrbAreaProfile profile = plate.GetComponent<LabOrbAreaProfile>();
            bool bake = profile == null || profile.SourceTexture != source
                || profile.LowerBoundary.Count < 2 || profile.UpperBoundary.Count < 2;
            Vector2[] lower = null, upper = null;
            if (bake) Bake(source, out lower, out upper);
            bool bakeHud = profile == null || profile.LowerOccluderTexture != hudSource
                || profile.LowerOccluderBoundary.Count < 2;
            Vector2[] hudTop = bakeHud ? BakeOpaqueTop(hudSource) : null;

            const string label = "Fit Curved Orb Area";
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            if (profile == null) profile = Undo.AddComponent<LabOrbAreaProfile>(plate.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { controller, area, profile, plate, controller.orbCamera }, label);
            // Re-fitting layout preserves later Scene-handle/Inspector edits. The source is
            // sampled only when this Scene has no profile yet or a different texture is assigned.
            if (bake) profile.SetBakedBoundary(source, AlphaThreshold, lower, upper);
            if (bakeHud) profile.SetBakedOccluder(hudSource, AlphaThreshold, hudTop);
            controller.curvedAreaProfile = profile;
            controller.requireCurvedAreaProfile = true;
            controller.orbInteractionArea = area;
            controller.lowerHudBoundary = lowerHud;
            controller.lowerHudGap = 0f;
            area.anchorMin = controller.orbCamera.rect.min;
            area.anchorMax = controller.orbCamera.rect.max;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.pivot = new Vector2(.5f, .5f);
            area.localScale = Vector3.one;
            area.localRotation = Quaternion.identity;
            controller.orbCamera.orthographicSize = controller.config.BoardHeight * .5f;
            // Restore the exact saved #154 art transform. Runtime fits its width to the
            // camera aspect just as #154 did; neither path moves the paper above the HUD.
            plate.localPosition = new Vector3(0f, 0f, .55f);
            plate.localScale = new Vector3(10f, 5f, 1f);
            Canvas.ForceUpdateCanvases();
            ValidateBindings(controller, source);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(area);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(plate);
            EditorUtility.SetDirty(controller.orbCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the curved paper area.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("C6_LAB_CURVED_AREA_SAVED: #154 paper transform restored; physics follows the paper silhouette and actual lower-HUD opaque contour without moving artwork.");
        }

        [MenuItem("C6 Lab/Validate Curved Orb Area")]
        public static void ValidateCurrentScene()
        {
            LabSceneController controller = RequireSingle<LabSceneController>(RequireScene());
            ValidateBindings(controller, AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            Debug.Log("C6_LAB_CURVED_AREA_VALID: #154 saved paper transform, paper/HUD curve sources and controller references agree.");
        }

        private static void Bake(Texture2D source, out Vector2[] lower, out Vector2[] upper)
        {
            // Decode a temporary CPU copy rather than enabling Read/Write on the imported texture.
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                string path = AssetDatabase.GetAssetPath(source);
                if (!decoded.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidOperationException("Unity could not decode the paper PNG for curve baking.");
                Color32[] pixels = decoded.GetPixels32();
                lower = new Vector2[SampleCount];
                upper = new Vector2[SampleCount];
                int alpha = Mathf.RoundToInt(AlphaThreshold * 255f);
                for (int sample = 0; sample < SampleCount; sample++)
                {
                    float u = sample / (float)(SampleCount - 1);
                    int x = Mathf.RoundToInt(u * (decoded.width - 1));
                    int bestStart = 0, bestLength = 0, runStart = 0, runLength = 0;
                    for (int y = 0; y <= decoded.height; y++)
                    {
                        bool opaque = y < decoded.height && pixels[y * decoded.width + x].a >= alpha;
                        if (opaque) { if (runLength == 0) runStart = y; runLength++; }
                        if (opaque) continue;
                        if (runLength > bestLength) { bestStart = runStart; bestLength = runLength; }
                        runLength = 0;
                    }
                    if (bestLength <= 1)
                        throw new InvalidOperationException("The paper PNG has no continuous playable surface at sample " + sample + ".");
                    lower[sample] = new Vector2(u, bestStart / (float)decoded.height);
                    upper[sample] = new Vector2(u, (bestStart + bestLength) / (float)decoded.height);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private static Vector2[] BakeOpaqueTop(Texture2D source)
        {
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)), false))
                    throw new InvalidOperationException("Unity could not decode the lower HUD PNG.");
                Color32[] pixels = decoded.GetPixels32();
                var points = new Vector2[SampleCount];
                int alpha = Mathf.RoundToInt(AlphaThreshold * 255f);
                for (int sample = 0; sample < SampleCount; sample++)
                {
                    float u = sample / (float)(SampleCount - 1);
                    int x = Mathf.RoundToInt(u * (decoded.width - 1));
                    int top = decoded.height - 1;
                    while (top >= 0 && pixels[top * decoded.width + x].a < alpha) top--;
                    if (top < 0) throw new InvalidOperationException("The lower HUD PNG has no opaque surface at sample " + sample + ".");
                    points[sample] = new Vector2(u, (top + 1) / (float)decoded.height);
                }
                return points;
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private static void ValidateBindings(LabSceneController controller, Texture2D source)
        {
            LabOrbAreaProfile profile = controller.curvedAreaProfile;
            if (!controller.requireCurvedAreaProfile || profile == null || profile.SourceTexture != source
                || profile.transform.parent != controller.board.transform
                || profile.gameObject.name != "OrbBoardPlate")
                throw new InvalidOperationException("The saved Lab Scene must require and reference its BoardPlate curve profile.");
            if (controller.orbInteractionArea == null || controller.lowerHudBoundary == null
                || controller.orbInteractionArea.GetComponent<UnityEngine.UI.Graphic>() != null
                || controller.orbInteractionArea.GetComponent<CanvasRenderer>() != null)
                throw new InvalidOperationException("The invisible plate reference and existing lower HUD must be connected.");
            RequireVisualPlate(profile.transform);
            UnityEngine.UI.Image hud = controller.lowerHudBoundary.GetComponent<UnityEngine.UI.Image>();
            if (hud == null || hud.sprite == null || hud.sprite.texture != profile.LowerOccluderTexture
                || profile.LowerOccluderTexture != AssetDatabase.LoadAssetAtPath<Texture2D>(HudTexturePath)
                || profile.LowerOccluderBoundary.Count < 2 || controller.lowerHudGap != 0f)
                throw new InvalidOperationException("The lower HUD's actual opaque contour must be baked without a rectangular gap.");
            if (!controller.ValidateBoardTuning(controller.config.BoardHeight,
                controller.config.OrbRadius, controller.config.DrumRadius, out string error))
                throw new InvalidOperationException(error);
            if (Vector3.Distance(profile.transform.localPosition, new Vector3(0f, 0f, .55f)) > .0001f
                || Vector3.Distance(profile.transform.localScale, new Vector3(10f, 5f, 1f)) > .0001f)
                throw new InvalidOperationException("The saved paper Quad must retain #154's local position (0,0,.55) and scale (10,5,1). Runtime alone fits the camera aspect.");
            if ((controller.orbInteractionArea.anchorMin - controller.orbCamera.rect.min).sqrMagnitude > .000001f
                || (controller.orbInteractionArea.anchorMax - controller.orbCamera.rect.max).sqrMagnitude > .000001f
                || controller.orbInteractionArea.offsetMin.sqrMagnitude > .000001f
                || controller.orbInteractionArea.offsetMax.sqrMagnitude > .000001f)
                throw new InvalidOperationException("The invisible reference must cover the complete orb camera viewport.");
        }

        private static void RequireVisualPlate(Transform plate)
        {
            Renderer renderer = plate.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null || plate.GetComponent<Collider>() != null
                || Vector3.Dot(plate.right, Vector3.right) < .9999f
                || Vector3.Dot(plate.up, Vector3.up) < .9999f)
                throw new InvalidOperationException("OrbBoardPlate must remain an axis-aligned visual Quad without a Collider.");
        }

        private static Scene RequireScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before baking or validating the saved paper curves.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open Assets/Lab/Scenes/Lab.unity first.");
            return scene;
        }

        private static T RequireSingle<T>(Scene scene) where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T candidate in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null) throw new InvalidOperationException("More than one " + typeof(T).Name + " exists.");
                    result = candidate;
                }
            if (result == null) throw new InvalidOperationException("The saved Scene has no " + typeof(T).Name + ".");
            return result;
        }
    }

    [CustomEditor(typeof(LabOrbAreaProfile))]
    public sealed class LabOrbAreaProfileEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var profile = (LabOrbAreaProfile)target;
            DrawHandles(profile, false, profile.LowerBoundary, new Color(1f, .7f, .15f));
            DrawHandles(profile, true, profile.UpperBoundary, new Color(.2f, 1f, .8f));
        }

        private static void DrawHandles(LabOrbAreaProfile profile, bool upper,
            System.Collections.Generic.IReadOnlyList<Vector2> points, Color colour)
        {
            if (points == null) return;
            Handles.color = colour;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 world = profile.transform.TransformPoint(profile.LocalPoint(points[i]));
                EditorGUI.BeginChangeCheck();
                Vector3 changed = Handles.Slider(world, Vector3.up,
                    HandleUtility.GetHandleSize(world) * .025f, Handles.DotHandleCap, 0f);
                if (!EditorGUI.EndChangeCheck()) continue;
                Undo.RecordObject(profile, "Edit Paper Curve Point");
                Vector3 local = profile.transform.InverseTransformPoint(changed);
                profile.SetControlPoint(upper, i, new Vector2(points[i].x, local.y + .5f));
                EditorUtility.SetDirty(profile);
                EditorSceneManager.MarkSceneDirty(profile.gameObject.scene);
            }
        }
    }
}
