using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// VFX 녹화용 실험실 (#610, #673에서 1~3라운드 모든 기믹으로 확장). 혼자 플레이로는 녹화하기 어려운 장면을 실제 게임 화면 그대로 바로 띄운다.
    /// 라운드를 골라 Home → 혼자 시작으로 열고, 전투 중 버튼으로 장면을 부른다. 빌드에는 들어가지 않고 게임 설정도 바꾸지 않는다
    /// (지귀의 B/A Test Only만 LabConfig 값을 직접 켜고 끈다. 시험이 끝나면 기본 시간표로 돌린다).
    /// </summary>
    public sealed class LabVfxLabWindow : EditorWindow
    {
        private const string AutoSoloKey = "C6Lab.VfxLab.AutoSolo";
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";
        private static readonly string[] Rounds = { "1R 구미호", "2R 야광귀", "3R 지귀" };
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

        private void OnEnable() => round = Mathf.Clamp(EditorPrefs.GetInt(GameSession.DeveloperSoloStartStageKey, 2) - 1, 0, 2);

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
            DrawCommon(network, ready);
            // #673: only the chosen round's gimmicks show (in a battle, the round actually being played).
            LabStageYokai shown = stage != LabStageYokai.None ? stage
                : round == 0 ? LabStageYokai.Gumiho : round == 1 ? LabStageYokai.Yaguang : LabStageYokai.Jigwi;
            if (shown == LabStageYokai.Gumiho) DrawGumiho(network, ready && stage == LabStageYokai.Gumiho);
            if (shown == LabStageYokai.Yaguang) DrawYaguang(network, ready && stage == LabStageYokai.Yaguang);
            if (shown == LabStageYokai.Jigwi) DrawJigwi(network, ready && stage == LabStageYokai.Jigwi);
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
                    EditorPrefs.SetInt(GameSession.DeveloperSoloStartStageKey, round + 1);
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

        /// <summary>#673: 세 라운드 어디서나 부를 수 있는 공통 기믹.</summary>
        private void DrawCommon(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("공통 기믹 (모든 라운드)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!ready))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("박 폭탄 (내 판)", GUILayout.Height(26)))
                    Report(network.VfxLabCastGimmick(LabSabotageKind.GourdBomb), "내 판에 박 폭탄이 떨어졌다. 옆으로 넘기지 않으면 터진다.", "박 폭탄을 놓을 수 없음");
                if (GUILayout.Button("넘기기 막기 (나)", GUILayout.Height(26)))
                    Report(network.VfxLabCastGimmick(LabSabotageKind.SwipeBlock), "내 한쪽 넘기기가 막혔다.", "이미 넘기기 막기가 걸려 있음");
            }
            if (EditorApplication.isPlaying && !ready) EditorGUILayout.LabelField("전투 중에 켜진다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
        }

        /// <summary>#673: 1R 구미호 기믹.</summary>
        private void DrawGumiho(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("1. 구미호", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!ready))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("여우불 (모두의 판)", GUILayout.Height(26)))
                        Report(network.VfxLabCastGimmick(LabSabotageKind.FoxFire), "모두의 판에 여우 구슬이 떨어졌다. 두 번 눌러 없앤다.", "여우불을 떨어뜨릴 수 없음");
                    if (GUILayout.Button("매혹 (나)", GUILayout.Height(26)))
                        Report(network.VfxLabCastGimmick(LabSabotageKind.Charm), "매혹 예고 → 나에게 매혹이 걸린다.", "이미 매혹이 걸려 있거나 예고 중");
                }
                if (GUILayout.Button("구미호 피버 바로 시작 (남은 20초를 지금으로)", GUILayout.Height(30)))
                    Report(network.VfxLabStartGumihoFever(), "구미호 피버를 시작했다. 이 판의 방해는 더 나오지 않는다.", "피버를 시작할 수 없음 (이미 피버 중)");
            }
            if (EditorApplication.isPlaying && !ready) EditorGUILayout.LabelField("구미호 전투에서만 보인다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
        }

        private void DrawYaguang(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("2. 야광귀", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Y1(섬광)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!ready || !network.VfxLabYaguangFeverReady))
                if (GUILayout.Button("섬광 바로 시작 (눈 뜸 예고부터)", GUILayout.Height(28)))
                    Report(network.VfxLabStartFlash(), "섬광 예고를 시작했다. 번쩍인 뒤 폰을 흔들어 푼다.", "지금은 시작할 수 없음 (섬광/콤보 진행 중이거나 섬광이 꺼져 있음, 또는 피버 뒤)");
            bool running = ready && network.VfxLabComboRunning, active = ready && network.VfxLabComboActive;
            EditorGUILayout.LabelField("Y2(콤보) / Y3(BIG 보상)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!ready || running || !network.VfxLabYaguangFeverReady))
                if (GUILayout.Button("콤보 바로 시작 (넷 셋 둘 하나 + 색 미리 보기부터)", GUILayout.Height(30)))
                    Report(network.VfxLabStartCombo(), "콤보 예고를 시작했다. 넷 셋 둘 하나에 맞춰 칸이 실제 색으로 하나씩 채워진다.", "지금은 시작할 수 없음 (섬광/콤보 진행 중이거나 보상/벌칙이 남아 있음, 또는 피버 뒤)");
            using (new EditorGUI.DisabledScope(!active))
            {
                if (GUILayout.Button("다음 칸 색 구슬 받기 (직접 던져 맞히기)", GUILayout.Height(26)))
                    Report(network.VfxLabGiveComboOrb(out LabOrbElement color), "내 판에 " + Name(color) + " 속성 구슬을 놓았다. 박에 던져 맞히면 칸이 체크된다.",
                        "판이 가득 찼음 (구슬을 하나 던지거나 버린 뒤 다시)");
                // #719: 콤보 가이딩을 한 단계씩 본다.
                if (GUILayout.Button("다른 색 구슬 받기 (빛 테두리가 안 붙는지)", GUILayout.Height(26)))
                    Report(network.VfxLabGiveOtherColorOrb(out LabOrbElement other), "내 판에 " + Name(other) + " 속성 구슬을 놓았다. 다음 칸 색이 아니라 빛나지 않아야 한다.",
                        "판이 가득 찼음 (구슬을 하나 던지거나 버린 뒤 다시)");
                if (GUILayout.Button("다음 칸 하나만 맞히기 (도장 → 강조가 다음 칸으로)", GUILayout.Height(26)))
                    Report(network.VfxLabHitComboSlot(), "한 칸을 맞혔다. 도장이 찍히고 강조와 빛 테두리가 다음 색으로 옮겨 간다.", "맞힐 수 없음 (4칸이 끝났거나 시간이 지남)");
                if (GUILayout.Button("틀린 색 → 봐주기 (첫 칸 전, 금옥 \"다시 해 보자\")", GUILayout.Height(26)))
                    Report(network.VfxLabForgiveWrongCombo(), "틀린 색을 봐줬다. 콤보는 계속된다.", "봐줄 수 없음 (첫 칸을 이미 맞혔음 → 아래 실패 버튼)");
                if (GUILayout.Button("남은 칸 바로 맞히기 → 성공 + BIG 보상", GUILayout.Height(26)))
                    Report(network.VfxLabCompleteCombo(), "콤보 성공. BIG 보상이 걸렸다.", "성공 처리를 못 함 (4칸이 끝났거나 시간이 지남)");
                if (GUILayout.Button("틀린 색 맞히기 → 실패 + SMALL 벌칙", GUILayout.Height(26)))
                    Report(network.VfxLabFailCombo(), "콤보 실패. SMALL 벌칙이 걸렸다.", "실패 처리를 못 함 (4칸이 끝났거나 시간이 지남)");
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

        private void DrawJigwi(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("3. 지귀", EditorStyles.boldLabel);
            // #673: 시간표를 기다리지 않고 미션을 지금 부르고 끝낸다.
            LabJigwiABKind? mission = ready ? network.VfxLabJigwiMission : null;
            bool on = ready && network.VfxLabJigwiOn;
            EditorGUILayout.LabelField("지금 부르기 (시간표의 다음 미션)", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!on || mission != null))
                if (GUILayout.Button("다음 미션 바로 시작 (연기 또는 부적, 예고부터)", GUILayout.Height(28)))
                    Report(network.VfxLabStartNextJigwiMission(), "다음 미션 예고를 시작했다.", "지금은 시작할 수 없음 (남은 미션이 없거나 피버 뒤)");
            using (new EditorGUI.DisabledScope(!on || mission == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                string name = mission == LabJigwiABKind.PersonalBurn ? "부적" : mission == LabJigwiABKind.GroupFire ? "연기" : "미션";
                if (GUILayout.Button(name + " 성공", GUILayout.Height(26)))
                    Report(network.VfxLabFinishJigwiMission(true), name + " 성공 처리했다 (부적이면 모두 다 쓴 것으로 → 완성 연출).", "아직 시작 전(예고 중)이거나 끝났음");
                if (GUILayout.Button(name + " 실패 (시간 끝)", GUILayout.Height(26)))
                    Report(network.VfxLabFinishJigwiMission(false), name + "의 시간이 다 된 것으로 끝냈다 (부적이면 불덩이 → 판이 탐).", "아직 시작 전(예고 중)이거나 끝났음");
            }
            // #696/#719: 연기 파편과 검정 구슬 연출을 바로 본다.
            EditorGUILayout.LabelField("연기 파편 / 검정 구슬", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(!ready))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("파편 4개 받기", GUILayout.Height(26)))
                    Report(network.VfxLabGiveSootShards(), "검은 파편 4개(북/서/남/동)를 놓았다. 이웃끼리 끌어다 붙여 본다.", "놓을 수 없음");
                if (GUILayout.Button("검정 구슬 받기", GUILayout.Height(26)))
                    Report(network.VfxLabGiveSootOrb(), "검정 구슬을 놓았다. 기운 소리/연기를 보고, 던져서 명중 효과를 본다.", "판이 가득 찼음");
            }
            using (new EditorGUI.DisabledScope(!on))
                if (GUILayout.Button("지귀 피버 바로 시작 (100초를 지금으로)", GUILayout.Height(30)))
                    Report(network.VfxLabStartJigwiFever(), "지귀 피버를 시작했다. 진행 중이던 미션은 여기서 끝난다.", "피버를 시작할 수 없음 (이미 피버 중)");
            if (ready && !on) EditorGUILayout.LabelField("이미 피버 중. 다시 보려면 Play를 멈추고 다시 연다.", EditorStyles.wordWrappedMiniLabel);
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
