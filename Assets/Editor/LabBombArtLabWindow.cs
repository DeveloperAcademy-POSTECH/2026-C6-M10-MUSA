using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// 아트팀 폭탄 실험실. 폭탄 그림을 바꾸고 Play에서 버튼으로 내 판에 폭탄을 떨어뜨려 게임 화면 그대로 본다.
    /// </summary>
    public sealed class LabBombArtLabWindow : EditorWindow
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string AppearancePath = "Assets/Lab/Art/Orbs/LabOrbAppearance.asset";
        private static readonly string[] ArtProperties =
        {
            "bombArt", "bombFuseArt", "bombBodySize", "bombFuseCanvasSize", "bombCapAngle",
            "bombRopeLength", "bombRopeColor", "bombRopeThickness", "bombCountColor", "bombWarningColor"
        };
        private static readonly string[] ArtFiles =
        {
            "Assets/Lab/Art/Items/ItemBomb.png", "Assets/Lab/Art/Items/ItemBombBody.png",
            "Assets/Lab/Art/Items/ItemBombFuse.png", "Assets/Lab/Resources/Bomb"
        };
        private int lockSwipes = 1;
        private bool randomSpot = true;
        private Vector2 spot = new Vector2(.5f, .6f);
        private SerializedObject appearance;
        private Vector2 scroll;

        [MenuItem("C6 Lab/Art/폭탄 실험실")]
        private static void Open() => GetWindow<LabBombArtLabWindow>("폭탄 실험실");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("1. 폭탄 그림", EditorStyles.boldLabel);
            if (appearance == null || appearance.targetObject == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(AppearancePath);
                appearance = asset != null ? new SerializedObject(asset) : null;
            }
            if (appearance == null) EditorGUILayout.HelpBox(AppearancePath + " 이 없음", MessageType.Warning);
            else
            {
                appearance.Update();
                foreach (string name in ArtProperties)
                {
                    SerializedProperty property = appearance.FindProperty(name);
                    if (property != null) EditorGUILayout.PropertyField(property);
                }
                appearance.ApplyModifiedProperties();
                if (GUILayout.Button("LabOrbAppearance 선택 (나머지 값)")) Ping(AppearancePath);
            }
            EditorGUILayout.LabelField("그림 파일 (같은 이름으로 덮어쓰면 바로 바뀜)", EditorStyles.miniBoldLabel);
            foreach (string path in ArtFiles)
                if (GUILayout.Button(path, EditorStyles.linkLabel)) Ping(path);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. 게임 화면에서 보기", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Lab 씬을 열고 Play → 오른쪽 위 DEV → START SOLO로 혼자 전투를 시작한 뒤 아래 버튼을 누름", MessageType.Info);
                if (GUILayout.Button("Lab 씬 열고 Play", GUILayout.Height(32)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath);
                    EditorApplication.isPlaying = true;
                }
                EditorGUILayout.EndScrollView();
                return;
            }
            LabNetwork network = Object.FindFirstObjectByType<LabNetwork>();
            bool ready = network != null && network.ArtLabReady;
            if (!ready) EditorGUILayout.HelpBox("게임 화면 오른쪽 위 DEV → START SOLO로 전투를 시작하면 버튼이 켜짐", MessageType.Info);
            lockSwipes = EditorGUILayout.IntSlider("잠금 횟수 (넘겨야 풀림)", lockSwipes, 1, 5);
            randomSpot = EditorGUILayout.Toggle("떨어질 자리 무작위", randomSpot);
            if (!randomSpot) spot = EditorGUILayout.Vector2Field("떨어질 자리 (0~1)", spot);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("내 판에 폭탄 떨어뜨리기", GUILayout.Height(40))) Drop(network, 1);
                if (GUILayout.Button("한꺼번에 3개", GUILayout.Height(28))) Drop(network, 3);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Drop(LabNetwork network, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 at = randomSpot ? new Vector2(Random.Range(.2f, .8f), .6f) // 게임과 같은 떨어질 자리
                    : new Vector2(Mathf.Clamp01(spot.x), Mathf.Clamp01(spot.y));
                if (!network.ArtLabDropGourdBomb(at, lockSwipes)) Debug.LogWarning("폭탄 실험실: 지금은 떨어뜨릴 수 없음 (전투 중 Host인지 확인)");
            }
        }

        private static void Ping(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
