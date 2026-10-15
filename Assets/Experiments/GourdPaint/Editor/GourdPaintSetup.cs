using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MUSA.GourdPaint.EditorTools
{
    /// <summary>
    /// 박 페인트 실험 장면 열기·다시 만들기·아이폰 빌드 메뉴.
    /// 기본 빌드(C6 Lab → Export iOS Xcode Project)와 Build Settings 장면 목록은 건드리지 않는다.
    /// </summary>
    public static class GourdPaintSetup
    {
        const string Root = "Assets/Experiments/GourdPaint";
        public const string ScenePath = Root + "/GourdPaintPrototype.unity";
        const string LabScenePath = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Gourd Paint Experiment/Open Scene")]
        [MenuItem("C6 Lab/Open Gourd Paint Experiment")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
        }

        /// <summary>최신 Lab.unity를 복사해 실험 장면을 새로 만든다. main의 Lab이 바뀌었을 때 쓴다.</summary>
        [MenuItem("C6 Lab/Gourd Paint Experiment/Rebuild Scene From Latest Lab")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Rebuild Gourd Paint scene",
                    "Lab.unity를 복사해 실험 장면을 새로 만듭니다. 실험 장면에서 손으로 바꾼 내용은 사라집니다.",
                    "Rebuild", "Cancel")) return;
            Create();
        }

        /// <summary>실험 장면만 넣은 아이폰용 Xcode 프로젝트를 Builds/GourdPaint 아래에 만든다.</summary>
        [MenuItem("C6 Lab/Gourd Paint Experiment/Export iOS Xcode Project")]
        public static void ExportIos()
        {
            if (!File.Exists(ScenePath))
                throw new InvalidOperationException("Open or rebuild the Gourd Paint scene first.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(root, "Builds", "GourdPaint",
                DateTime.Now.ToString("yyyyMMdd-HHmmss"), "iOS");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("GOURD_PAINT_BUILD_FAILED result="
                    + (report == null ? "NoReport" : report.summary.result.ToString()));
            Debug.Log("GOURD_PAINT_BUILD_OK output=" + output);
            EditorUtility.RevealInFinder(output);
        }

        static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        public static void Create()
        {
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Root + "/WhiteGourd.mat");
            if (paint == null)
            {
                paint = new Material(Shader.Find("MUSA/GourdPaint"));
                AssetDatabase.CreateAsset(paint, Root + "/WhiteGourd.mat");
            }
            var stem = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Stem.mat");
            if (stem == null)
            {
                stem = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                stem.SetColor("_BaseColor", new Color(.5f, .3f, .12f));
                AssetDatabase.CreateAsset(stem, Root + "/Stem.mat");
            }

            var root = new GameObject("Paint Gourd Visual");
            Part("White Gourd", PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 1.8f, paint);
            Part("Stem", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .96f, 0f), new Vector3(.14f, .12f, .14f), stem);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/WhiteGourd.prefab");
            Object.DestroyImmediate(root);

            // 원본 Lab.unity를 열어 실험 컴포넌트를 붙인 뒤 다른 이름으로 저장한다. 원본은 저장하지 않는다.
            EditorSceneManager.OpenScene(LabScenePath);
            foreach (var network in Object.FindObjectsByType<LabNetwork>(FindObjectsSortMode.None))
            {
                if (network.target == null) continue;
                var target = network.target.gameObject.GetComponent<GourdPaintTarget>();
                if (target == null) target = network.target.gameObject.AddComponent<GourdPaintTarget>();
                target.target = network.target;
                target.network = network;
                target.paintMaterial = paint;
                target.visualPrefab = prefab;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }
    }
}
