using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// 아트팀 폭탄 실험실(#584). 라운드(1 구미호 / 2 야광귀 / 3 지귀)를 골라 실제 게임 화면과 지금 들어 있는 어셋으로 연다.
    /// Play 중 버튼으로 내 판에 폭탄을 떨어뜨리고, 터지는 시간은 실험실에서만 바꿀 수 있다(게임 설정은 그대로).
    /// </summary>
    public sealed class LabBombArtLabWindow : EditorWindow
    {
        private const string AutoSoloKey = "C6Lab.BombArtLab.AutoSolo";
        private const string AppearancePath = "Assets/Lab/Art/Orbs/LabOrbAppearance.asset";
        private static readonly string[] Rounds = { "1R 구미호", "2R 야광귀", "3R 지귀" };
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
        private int round;
        private int lockSwipes = 1;
        private bool randomSpot = true, gameFuse = true;
        private float fuseSeconds = 5f;
        private Vector2 spot = new Vector2(.5f, .6f);
        private SerializedObject appearance;
        private Vector2 scroll;

        [MenuItem("C6 Lab/Art/폭탄 실험실")]
        private static void Open() => GetWindow<LabBombArtLabWindow>("폭탄 실험실");

        private void OnEnable() => round = Mathf.Clamp(EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 1) - 1, 0, 2);

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawRound();
            EditorGUILayout.Space();
            DrawArt();
            EditorGUILayout.Space();
            DrawDrop();
            EditorGUILayout.EndScrollView();
        }

        private void DrawRound()
        {
            EditorGUILayout.LabelField("1. 라운드 고르기", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                round = GUILayout.Toolbar(round, Rounds);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("고른 라운드를 실제 게임 화면(Home → 혼자 시작)과 지금 들어 있는 어셋 그대로 연다.", MessageType.Info);
                if (GUILayout.Button(Rounds[round] + " 게임 화면 열기", GUILayout.Height(32)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, round + 1);
                    SessionState.SetBool(AutoSoloKey, true);
                    EditorSceneManager.OpenScene(GameScenePaths.Home);
                    EditorApplication.isPlaying = true;
                }
                return;
            }
            GameSession session = GameSession.Instance;
            string now = session != null && session.State != null && session.State.stageIndex > 0
                ? "지금 " + Rounds[Mathf.Clamp(session.State.stageIndex - 1, 0, 2)] : "게임 화면을 여는 중";
            EditorGUILayout.HelpBox(now + ". 다른 라운드는 Play를 멈추고 다시 고른다. 2R/3R은 그 라운드 기믹도 같이 나온다.", MessageType.None);
        }

        private void DrawArt()
        {
            EditorGUILayout.LabelField("2. 폭탄 그림 (지금 게임에 들어 있는 어셋)", EditorStyles.boldLabel);
            if (appearance == null || appearance.targetObject == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(AppearancePath);
                appearance = asset != null ? new SerializedObject(asset) : null;
            }
            if (appearance == null) { EditorGUILayout.HelpBox(AppearancePath + " 이 없음", MessageType.Warning); return; }
            appearance.Update();
            foreach (string name in ArtProperties)
            {
                SerializedProperty property = appearance.FindProperty(name);
                if (property != null) EditorGUILayout.PropertyField(property);
            }
            appearance.ApplyModifiedProperties();
            if (GUILayout.Button("LabOrbAppearance 선택 (나머지 값)")) Ping(AppearancePath);
            EditorGUILayout.LabelField("그림 파일 (같은 이름으로 덮어쓰면 바로 바뀜)", EditorStyles.miniBoldLabel);
            foreach (string path in ArtFiles)
                if (GUILayout.Button(path, EditorStyles.linkLabel)) Ping(path);
        }

        private void DrawDrop()
        {
            EditorGUILayout.LabelField("3. 폭탄 떨어뜨리기", EditorStyles.boldLabel);
            LabNetwork network = EditorApplication.isPlaying ? Network() : null;
            bool ready = network != null && network.ArtLabReady;
            float gameSeconds = ready ? network.ArtLabGameFuseSeconds : 0f;
            lockSwipes = EditorGUILayout.IntSlider("잠금 횟수 (넘겨야 풀림)", lockSwipes, 1, 5);
            gameFuse = EditorGUILayout.Toggle(gameSeconds > 0f ? "게임 도화선 그대로 (" + gameSeconds.ToString("0.#") + "초)" : "게임 도화선 그대로", gameFuse);
            if (!gameFuse)
            {
                float max = gameSeconds > 0f ? gameSeconds : 15f;
                fuseSeconds = EditorGUILayout.Slider("터지는 시간 (초, 실험실에서만)", Mathf.Min(fuseSeconds, max), .5f, max);
                EditorGUILayout.LabelField("이 창에서 떨어뜨린 폭탄만 이 시간에 터진다. 게임 설정은 바뀌지 않는다.", EditorStyles.wordWrappedMiniLabel);
            }
            randomSpot = EditorGUILayout.Toggle("떨어질 자리 무작위", randomSpot);
            if (!randomSpot) spot = EditorGUILayout.Vector2Field("떨어질 자리 (0~1)", spot);
            if (EditorApplication.isPlaying && !ready)
                EditorGUILayout.HelpBox("전투가 시작되면 버튼이 켜진다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("내 판에 폭탄 떨어뜨리기", GUILayout.Height(40))) Drop(network, 1);
                if (GUILayout.Button("한꺼번에 3개", GUILayout.Height(28))) Drop(network, 3);
            }
        }

        private static LabNetwork Network()
        {
            GameSession session = GameSession.Instance;
            return session != null && session.Network != null ? session.Network : Object.FindFirstObjectByType<LabNetwork>();
        }

        private void Drop(LabNetwork network, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 at = randomSpot ? new Vector2(Random.Range(.2f, .8f), .6f) // 게임과 같은 떨어질 자리
                    : new Vector2(Mathf.Clamp01(spot.x), Mathf.Clamp01(spot.y));
                if (!network.ArtLabDropGourdBomb(at, lockSwipes, gameFuse ? -1f : fuseSeconds))
                    Debug.LogWarning("폭탄 실험실: 지금은 떨어뜨릴 수 없음 (전투 중 Host인지 확인)");
            }
        }

        private static void Ping(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void OnInspectorUpdate()
        {
            // Play가 켜지고 Home이 준비되면 고른 라운드로 혼자 시작한다(한 번만).
            if (EditorApplication.isPlaying && SessionState.GetBool(AutoSoloKey, false))
            {
                GameSession session = GameSession.Instance;
                if (session != null && !session.IsLoadingScene && session.State != null && session.State.phase == GameFlowPhase.Home)
                {
                    SessionState.SetBool(AutoSoloKey, false);
                    session.StartSolo();
                }
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.SetBool(AutoSoloKey, false);
            Repaint();
        }
    }
}
