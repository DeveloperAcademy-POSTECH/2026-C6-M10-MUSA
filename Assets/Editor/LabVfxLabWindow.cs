using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// VFX 녹화용 실험실 (#610). 혼자 플레이로는 녹화하기 어려운 야광귀/지귀 장면을 실제 게임 화면 그대로 바로 띄운다.
    /// 라운드를 골라 Home → 혼자 시작으로 열고, 전투 중 버튼으로 장면을 부른다. 빌드에는 들어가지 않고 게임 설정도 바꾸지 않는다
    /// (지귀의 B/A Test Only만 LabConfig 값을 직접 켜고 끈다. 시험이 끝나면 기본 시간표로 돌린다).
    /// </summary>
    public sealed class LabVfxLabWindow : EditorWindow
    {
        private const string AutoSoloKey = "C6Lab.VfxLab.AutoSolo";
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";
        private static readonly string[] Rounds = { "2R 야광귀", "3R 지귀" };
        private static readonly string[] JigwiModes = { "기본 시간표", "J1 연기만", "J2~J4 부적만" };
        private static readonly string[] JigwiModeHelp =
        {
            "실제 게임 그대로: 연기 10초 / 부적 45초 / 연기 66초 / 부적 83초.",
            "J1(연기 B)만 4초부터 17초마다 (B Test Only). 연기가 판을 덮고 손잡이 링 게이지를 문질러 걷는다.",
            "J2(부적 쓰기)/J3(실패)/J4(공용 완성)/J5(스와이프 금지)만 4초부터 17초마다 (A Test Only).",
        };
        private int round;
        private int fiveColorCount = 1;
        private int fakeSeats = 4;
        private string last = "";
        private Vector2 scroll;

        [MenuItem("Hapio/VFX 실험실")]
        private static void Open() => GetWindow<LabVfxLabWindow>("VFX 실험실");

        private void OnEnable() => round = EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 2) >= 3 ? 1 : 0;

        private void OnDisable() => LabJigwiScreenPresentation.VfxLabFakeSeats = 0;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawRound();
            EditorGUILayout.Space();
            LabNetwork network = EditorApplication.isPlaying ? Network() : null;
            bool ready = network != null && network.VfxLabReady;
            if (EditorApplication.isPlaying && !ready)
                EditorGUILayout.HelpBox("전투가 시작되면 버튼이 켜진다 (혼자 Host일 때만).", MessageType.Info);
            LabStageYokai stage = ready ? network.VfxLabStage : LabStageYokai.None;
            if (!EditorApplication.isPlaying || stage == LabStageYokai.Yaguang) DrawYaguang(network, ready && stage == LabStageYokai.Yaguang);
            if (!EditorApplication.isPlaying || stage == LabStageYokai.Jigwi) DrawJigwi(ready && stage == LabStageYokai.Jigwi);
            if (!string.IsNullOrEmpty(last))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(last, MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRound()
        {
            EditorGUILayout.LabelField("1. 라운드 고르기", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                round = GUILayout.Toolbar(round, Rounds);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("고른 라운드를 실제 게임 화면(Home → 혼자 시작)과 지금 들어 있는 어셋 그대로 연다. 녹화는 Game 창을 찍는다.", MessageType.Info);
                if (GUILayout.Button(Rounds[round] + " 게임 화면 열기", GUILayout.Height(32)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, round + 2);
                    SessionState.SetBool(AutoSoloKey, true);
                    last = "";
                    EditorSceneManager.OpenScene(GameScenePaths.Home);
                    EditorApplication.isPlaying = true;
                }
                return;
            }
            GameSession session = GameSession.Instance;
            string now = session != null && session.State != null && session.State.stageIndex > 0
                ? "지금 " + session.State.stageIndex + "R" : "게임 화면을 여는 중";
            EditorGUILayout.HelpBox(now + ". 다른 라운드는 Play를 멈추고 다시 고른다.", MessageType.None);
        }

        private void DrawYaguang(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("2. 야광귀", EditorStyles.boldLabel);
            bool running = ready && network.VfxLabComboRunning, active = ready && network.VfxLabComboActive;
            EditorGUILayout.LabelField("Y2(콤보) / Y3(BIG 보상)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!ready || running || !network.VfxLabYaguangFeverReady))
                if (GUILayout.Button("콤보 바로 시작 (COMBO 3/2/1부터)", GUILayout.Height(30)))
                    Report(network.VfxLabStartCombo(), "콤보 예고를 시작했다.", "지금은 시작할 수 없음 (섬광/콤보 진행 중이거나 보상/벌칙이 남아 있음, 또는 피버 뒤)");
            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("다음 칸 색 구슬 받기 (직접 던져 맞히기)", GUILayout.Height(26)))
                    Report(network.VfxLabGiveComboOrb(out LabOrbElement color), "내 판에 " + Name(color) + " 속성 구슬을 놓았다. 박에 던져 맞히면 칸이 체크된다.",
                        "판이 가득 찼음 (구슬을 하나 던지거나 버린 뒤 다시)");
                if (GUILayout.Button("남은 칸 바로 맞히기 → 성공 + BIG 보상", GUILayout.Height(26)))
                    Report(network.VfxLabCompleteCombo(), "콤보 성공. BIG 보상이 걸렸다.", "성공 처리를 못 함 (4칸이 끝났거나 시간이 지남)");
            }
            if (ready && running && !active) EditorGUILayout.LabelField("예고 중. 4칸이 나오면 아래 두 버튼이 켜진다.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.LabelField("Y4(오색구슬)", EditorStyles.miniBoldLabel);
            fiveColorCount = EditorGUILayout.IntSlider("받을 개수", fiveColorCount, 1, 3);
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("오색구슬 받기 (흔들기 없이)", GUILayout.Height(26)))
                {
                    int dropped = network.VfxLabGiveFiveColorOrbs(fiveColorCount);
                    Report(dropped > 0, "오색구슬 " + dropped + "개를 놓았다. 박에 던지면 다섯 색으로 칠한다.", "판이 가득 찼음");
                }

            EditorGUILayout.LabelField("Y5(야광귀 피버)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!ready || !network.VfxLabYaguangFeverReady))
                if (GUILayout.Button("피버 바로 시작 (100초를 지금으로)", GUILayout.Height(30)))
                    Report(network.VfxLabStartYaguangFever(), "피버를 시작했다. 이 판의 콤보/섬광은 더 나오지 않는다.", "피버를 시작할 수 없음");
            if (ready && !network.VfxLabYaguangFeverReady) EditorGUILayout.LabelField("이미 피버 중. 다시 보려면 Play를 멈추고 다시 연다.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawJigwi(bool ready)
        {
            EditorGUILayout.LabelField("3. 지귀", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("어떤 기믹을 띄울까 (Play 전에 고른다)", EditorStyles.miniBoldLabel);
            var config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            if (config != null)
            {
                var serialized = new SerializedObject(config);
                SerializedProperty bOnly = serialized.FindProperty("jigwiBTestOnly"), aOnly = serialized.FindProperty("jigwiATestOnly");
                if (bOnly != null && aOnly != null)
                {
                    int mode = bOnly.boolValue ? 1 : aOnly.boolValue ? 2 : 0;
                    // #663: write only when the toolbar is clicked. Writing every repaint put A Test Only back on whenever
                    // it was turned off in the Inspector (and saved it into LabConfig.asset while this window was open).
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        mode = GUILayout.Toolbar(mode, JigwiModes);
                    if (EditorGUI.EndChangeCheck())
                    {
                        bOnly.boolValue = mode == 1; aOnly.boolValue = mode == 2;
                        serialized.ApplyModifiedProperties();
                    }
                    EditorGUILayout.LabelField(JigwiModeHelp[mode], EditorStyles.wordWrappedMiniLabel);
                    if (mode != 0)
                        EditorGUILayout.HelpBox("LabConfig 값(B/A Test Only)을 켰다. 녹화가 끝나면 \"기본 시간표\"로 돌리고, 이 값은 커밋하지 않는다.", MessageType.Warning);
                }
            }
            EditorGUILayout.LabelField("J4(공용 부적 완성)", EditorStyles.miniBoldLabel);
            fakeSeats = EditorGUILayout.IntSlider("가짜 자리 수 (나 말고)", fakeSeats, 0, 4);
            // 지귀 전투를 혼자 Host로 할 때만 화면에 더한다. Test Runner를 돌릴 때는 이 창을 닫는다.
            LabJigwiScreenPresentation.VfxLabFakeSeats = ready ? fakeSeats : 0;
            EditorGUILayout.LabelField("부적이 시작되면 가짜 자리가 0.6초 간격으로 다 쓴 것처럼 켜진다. 내가 마지막으로 다 쓰면 실제 게임처럼 완성(빛남 → 물 폭포)된다. 화면만 바뀌고 판정은 그대로다.",
                EditorStyles.wordWrappedMiniLabel);
            if (EditorApplication.isPlaying && !ready) EditorGUILayout.LabelField("지귀 전투에서만 보인다.", EditorStyles.wordWrappedMiniLabel);
        }

        private void Report(bool ok, string success, string failure)
        {
            last = ok ? success : failure;
            if (!ok) Debug.LogWarning("VFX 실험실: " + failure);
        }

        private static string Name(LabOrbElement element)
        {
            switch (element)
            {
                case LabOrbElement.Fire: return "화";
                case LabOrbElement.Water: return "수";
                case LabOrbElement.Wood: return "목";
                case LabOrbElement.Metal: return "금";
                case LabOrbElement.Earth: return "토";
                default: return element.ToString();
            }
        }

        private static LabNetwork Network()
        {
            GameSession session = GameSession.Instance;
            return session != null && session.Network != null ? session.Network : Object.FindFirstObjectByType<LabNetwork>();
        }

        private void OnInspectorUpdate()
        {
            // Play가 켜지고 Home이 준비되면 고른 라운드로 혼자 시작한다(한 번만). 폭탄 실험실(#584)과 같은 방식.
            if (EditorApplication.isPlaying && SessionState.GetBool(AutoSoloKey, false))
            {
                GameSession session = GameSession.Instance;
                if (session != null && !session.IsLoadingScene && session.State != null && session.State.phase == GameFlowPhase.Home)
                {
                    SessionState.SetBool(AutoSoloKey, false);
                    session.StartSolo();
                }
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(AutoSoloKey, false);
                LabJigwiScreenPresentation.VfxLabFakeSeats = 0;
            }
            Repaint();
        }
    }
}
