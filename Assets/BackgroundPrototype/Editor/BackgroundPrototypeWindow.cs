using System.IO;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MUSA.BackgroundPrototype
{
    // Editor-only visual experiment: no battle, network or build-setting changes.
    public sealed class BackgroundPrototypeWindow : EditorWindow
    {
        const string Root = "Assets/BackgroundPrototype";
        const string ScenePath = Root + "/Generated/BackgroundPrototype.unity";
        static readonly Vector3 Target = new Vector3(0, 1.35f, 0);
        int count = 3;
        int seat;
        bool grid = true;
        Vector2 scroll;
        RenderTexture[] previews;

        [MenuItem("C6 Lab/Background Prototype")]
        public static void Open() => GetWindow<BackgroundPrototypeWindow>("Background Views");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("2D 배경 × 3D 구미호 시점 비교. Play 없이 사용합니다. 배경은 Hierarchy에서 이동할 수 있습니다.", MessageType.Info);
            if (GUILayout.Button("실험 장면 열기 / 최초 생성"))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
                else Build();
            }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) return;
            int next = EditorGUILayout.IntSlider("플레이어 수", count, 3, 5);
            if (next != count) { count = next; Arrange(count); }
            grid = EditorGUILayout.Toggle("모든 시점 함께 보기", grid);
            seat = EditorGUILayout.IntSlider("크게 볼 플레이어", seat, 0, count - 1);
            if (GUILayout.Button("현재 배치의 3·4·5인 화면 저장")) { CaptureAll(); Arrange(count); }
            var cams = Cameras();
            if (cams.Length == 0) return;
            EnsurePreviews(cams.Length);
            int columns = grid ? Mathf.Min(3, cams.Length) : 1;
            float width = Mathf.Min((position.width - 20) / columns, grid ? 240 : 330);
            float height = width * 16 / 9;
            int rows = grid ? Mathf.CeilToInt(cams.Length / (float)columns) : 1;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Rect area = GUILayoutUtility.GetRect(position.width - 20, rows * (height + 24));
            for (int i = 0; i < cams.Length; i++)
            {
                if (!grid && i != seat) continue;
                cams[i].targetTexture = previews[i];
                cams[i].Render();
                cams[i].targetTexture = null;
                int index = grid ? i : 0;
                Rect r = new Rect(area.x + index % columns * width, area.y + index / columns * (height + 24), width - 6, height);
                GUI.DrawTexture(r, previews[i], ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x, r.yMax, r.width, 22), "Player " + (i + 1));
            }
            EditorGUILayout.EndScrollView();
        }
        void OnInspectorUpdate() => Repaint();
        void OnDisable() => ReleasePreviews();
        void ReleasePreviews()
        {
            if (previews != null) foreach (var rt in previews) { rt.Release(); DestroyImmediate(rt); }
            previews = null;
        }
        void EnsurePreviews(int n)
        {
            if (previews != null && previews.Length == n) return;
            ReleasePreviews();
            previews = Enumerable.Range(0, n).Select(_ => new RenderTexture(360, 640, 24)).ToArray();
        }
        static Camera[] Cameras() => Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Where(c => c.name.StartsWith("BG Player ")).OrderBy(c => c.name).ToArray();

        static Material Material(string name, Color color, Texture texture = null, bool cutout = false)
        {
            string path = Root + "/Generated/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Cull", 0);
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .18f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }
        static void Card(string file, float angle, float radius, float width, Transform parent)
        {
            string path = Root + "/Art/" + file + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float height = width * tex.height / tex.width;
            Vector3 radial = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var obj = Shape(file, PrimitiveType.Quad, radial * radius + Vector3.up * height / 2,
                new Vector3(width, height, 1), Material(file, Color.white, tex, true));
            obj.transform.rotation = Quaternion.LookRotation(radial);
            obj.transform.SetParent(parent, true);
        }
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cards = new GameObject("Painted Background Cards").transform;
            for (int i = 0; i < 8; i++) Card("03-mountains", i * 45, 22, 20, cards);
            Card("02-school-gate", 210, 15, 12, cards);
            Card("04-tiled-house", 70, 16, 9, cards);
            Card("05-thatched-house", 110, 16, 8, cards);
            Card("01-pine", 275, 12, 18, cards);
            for (int i = 0; i < 8; i++)
            {
                Card(i % 2 == 0 ? "06-rock" : "07-shrub", i * 45 + 15, 13, 4, cards);
                Card("08-grass", i * 45 + 35, 12, 2, cards);
            }
            Shape("Clearing", PrimitiveType.Cylinder, new Vector3(0, -.15f, 0), new Vector3(60, .15f, 60),
                Material("Ground", new Color(.83f, .74f, .55f)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Model/ninetailedfox.fbx");
            var fox = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            fox.name = "Gumiho — visual reference only";
            var renderers = fox.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            fox.transform.localScale *= 4f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            fox.transform.position += new Vector3(0, 3.35f, 0) - bounds.center;
            var foxMat = Material("Gumiho", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Model/Gumiho_Material.png"));
            foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(foxMat, r.sharedMaterials.Length).ToArray();
            var rope = Material("Rope", new Color(.33f, .22f, .12f));
            Shape("Temporary overhead support", PrimitiveType.Cube, new Vector3(0, 6.6f, 0), new Vector3(10, .18f, .18f), rope);
            Shape("Suspension cord", PrimitiveType.Cylinder, new Vector3(0, 5.3f, 0), new Vector3(.04f, 1.2f, .04f), rope);
            Arrange(3);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }
        static void Arrange(int count)
        {
            foreach (var c in Cameras()) DestroyImmediate(c.gameObject);
            var config = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset");
            for (int i = 0; i < count; i++)
            {
                var cam = new GameObject("BG Player " + (i + 1)).AddComponent<Camera>();
                cam.transform.position = LabThrowMath.SeatPosition(i, count, Target, config.CameraRadius, config.CameraHeightOffset);
                cam.transform.LookAt(Target + Vector3.up * 2);
                cam.fieldOfView = 52;
                cam.nearClipPlane = .1f;
                cam.farClipPlane = 100;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.94f, .89f, .76f);
                cam.enabled = i == 0;
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        public static void CaptureAll()
        {
            string output = "Library/BackgroundPrototypeCaptures";
            Directory.CreateDirectory(output);
            for (int n = 3; n <= 5; n++)
            {
                Arrange(n);
                foreach (var cam in Cameras())
                {
                    var rt = new RenderTexture(540, 960, 24);
                    var previous = RenderTexture.active;
                    var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
                    try
                    {
                        cam.targetTexture = rt;
                        cam.Render();
                        RenderTexture.active = rt;
                        image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(output + "/" + n + "P-" + cam.name + ".png", image.EncodeToPNG());
                    }
                    finally
                    {
                        cam.targetTexture = null;
                        RenderTexture.active = previous;
                        rt.Release();
                        DestroyImmediate(rt);
                        DestroyImmediate(image);
                    }
                }
            }
            Arrange(3);
            Debug.Log("BACKGROUND_CAPTURE_OK: " + output);
        }
        public static void BuildAndCapture() { Build(); CaptureAll(); }
        public static void CaptureSaved() { EditorSceneManager.OpenScene(ScenePath); CaptureAll(); }
    }
}
