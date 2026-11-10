using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace C6Lab.Editor
{
    public static class LabFiveElementsDemoBuilder
    {
        public const string Root = "Assets/Experiments/FiveElements";
        public const string ScenePath = Root + "/Scenes/FiveElementsComparison.unity";
        public const string ProfilePath = "Assets/Lab/Resources/FiveElements/Profile.asset";
        private const string Source = "Assets/Lab/Scripts/Experiments/FiveElements/";
        private const string FontPath = "Assets/Lab/Art/Fonts/Shilla_Culture(M).otf";

        [MenuItem("C6 Lab/Experiments/Five Elements/Open Comparison")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath) && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Experiments/ClayPreview/Scenes/ClayPreview.unity");
            Prepare();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Safe to repeat: create missing assets without replacing an artist's settings or scene.
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before preparing the comparison.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Experiments/ClayPreview/Scenes/ClayPreview.unity");
            Folder(Root + "/Scenes");
            Folder("Assets/Lab/Resources/FiveElements");
            if (!File.Exists(ProfilePath))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LabFiveElementProfile>(), ProfilePath);
            Require<LabFiveElementProfile>(ProfilePath);
            if (!File.Exists(ScenePath)) CreateScene();
            Require<SceneAsset>(ScenePath);
            Debug.Log("FIVE_ELEMENTS_SCENE_READY " + ScenePath);
        }

        private static void CreateScene()
        {
            var font = Require<Font>(FontPath);
            var hud = Require<VisualTreeAsset>(Source + "FiveElementsHud.uxml");
            var style = Require<StyleSheet>(Source + "FiveElementsHud.uss");
            // Reuse the project's experiment panel (screen-size scaling and local runtime text theme).
            var panel = Require<PanelSettings>("Assets/Experiments/ClayPreview/ClayPreviewPanelSettings.asset");
            string materialPath = Root + "/SampleSurface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) throw new InvalidOperationException("The project's URP Lit shader is missing.");
                material = new Material(lit) { name = "Sample Surface" };
                material.SetFloat("_Smoothness", .3f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Scene previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("오색흙 · 오행 비교");
                SceneManager.MoveGameObjectToScene(root, scene);
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = hud;
                var demo = root.AddComponent<LabFiveElementsDemo>();
                demo.hudStyle = style;
                demo.hudFont = font;
                demo.stageMaterial = material;
                var config = Resources.Load<LabConfig>("LabConfig");
                if (config != null) demo.diameter = config.ThrowRadius * 2f;
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("The comparison scene could not be saved.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path);
            return value;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            Folder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(parent.Length + 1));
        }
    }
}
