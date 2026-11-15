using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>
    /// VFX 녹화용 실험실 (#610, #673에서 1~3라운드 모든 기믹으로 확장). 혼자 플레이로는 녹화하기 어려운 장면을 실제 게임 화면 그대로 바로 띄운다.
    /// 라운드를 골라 Home → 혼자 시작으로 열고, 전투 중 버튼으로 장면을 부른다. 빌드에는 들어가지 않고 게임 설정도 바꾸지 않는다
    /// (#724: 지귀의 연기만 / 부적만 모드도 LabConfig.asset을 건드리지 않고 이 에디터 세션 값으로만 켠다. 창을 닫으면 기본 시간표로 돌아간다).
    /// </summary>
    public sealed class LabVfxLabWindow : EditorWindow
    {
        private const string AutoSoloKey = "C6Lab.VfxLab.AutoSolo";
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

        private void OnDisable()
        {
            LabJigwiScreenPresentation.VfxLabFakeSeats = 0;
            SessionState.SetInt(LabConfig.VfxLabJigwiModeKey, 0); // #724: a closed lab leaves the real timetable
        }

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
            // #741: 도화선을 기다리지 않고 폭발 연출 보기
            using (new EditorGUI.DisabledScope(!ready || !network.VfxLabHasBomb))
                if (GUILayout.Button("박 폭탄 바로 터뜨리기 (내 판, 잠긴 폭탄)", GUILayout.Height(26)))
                    Report(network.VfxLabDetonateBomb(), "도화선이 다 탄 것으로 했다. 곧 터진다.", "내 판에 잠긴 박 폭탄이 없음 (먼저 박 폭탄을 떨어뜨린다)");
            if (EditorApplication.isPlaying && !ready) EditorGUILayout.LabelField("전투 중에 켜진다.", EditorStyles.wordWrappedMiniLabel);
            DrawTimeLeft(network, ready);
            EditorGUILayout.Space();
        }

        /// <summary>#734: 남은 30초 알림 ("30초 남았소!" 말풍선, 시간 숫자 깜빡임, 가장자리 번쩍임, 경고음, 진동).</summary>
        private void DrawTimeLeft(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("남은 30초 알림 (#734)", EditorStyles.miniBoldLabel);
            LabThirtySecondsLeftHud hud = ready ? Object.FindFirstObjectByType<LabThirtySecondsLeftHud>() : null;
            using (new EditorGUI.DisabledScope(hud == null))
            {
                if (GUILayout.Button("30초 알림 지금 보기 (화면/소리/진동만, 시간은 그대로)", GUILayout.Height(28)))
                    Report(hud.PreviewNow(), "\"30초 남았소!\"를 띄웠다. 몇 번이든 다시 누를 수 있다. 실제 알림은 남은 30초에 따로 뜬다.",
                        "알림을 그릴 화면을 찾지 못함");
                EditorGUILayout.LabelField("알림 화면과 함께 소리 골라 듣기 (LabConfig는 그대로)", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("알림 + 징", GUILayout.Height(26)))
                        Report(hud.PreviewNow(LabThirtySecondsLeftSound.Gong), "알림과 징 소리를 띄웠다.", "알림을 그릴 화면을 찾지 못함");
                    if (GUILayout.Button("알림 + 북", GUILayout.Height(26)))
                        Report(hud.PreviewNow(LabThirtySecondsLeftSound.Drum), "알림과 북 소리를 띄웠다.", "알림을 그릴 화면을 찾지 못함");
                    if (GUILayout.Button("알림 + 징 다음 북", GUILayout.Height(26)))
                        Report(hud.PreviewNow(LabThirtySecondsLeftSound.GongThenDrum), "알림과 징 → 북 소리를 띄웠다 (간격은 LabConfig).",
                            "알림을 그릴 화면을 찾지 못함");
                }
            }
            if (ready && network.config != null)
                EditorGUILayout.LabelField("실제 게임에서 쓰는 소리: " + (network.config.ThirtySecondsLeftSound == LabThirtySecondsLeftSound.Drum ? "북"
                    : network.config.ThirtySecondsLeftSound == LabThirtySecondsLeftSound.GongThenDrum ? "징 다음 북" : "징")
                    + " (LabConfig \"남은 30초 알림\"에서 바꾼다)", EditorStyles.wordWrappedMiniLabel);
            if (ready && network.Snapshot != null && network.config != null)
            {
                double until = network.Snapshot.remaining - network.config.ThirtySecondsLeftAt;
                EditorGUILayout.LabelField(until > 0d ? "실제 알림까지 " + Mathf.CeilToInt((float)until) + "초 (튜토리얼/2:2에서는 안 뜸)"
                    : "실제 알림 시각이 지났다.", EditorStyles.wordWrappedMiniLabel);
                Repaint();
            }
            else if (ready && hud == null) EditorGUILayout.LabelField("이 화면에는 알림이 붙어 있지 않다.", EditorStyles.wordWrappedMiniLabel);
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

        // ───────── #741: 기믹마다 묶고, 기믹 안에서 예보 / 진행 / 파훼 / 보상 / 처벌 줄로 나눈다 ─────────
        private static void Gimmick(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        /// <summary>한 줄: 왼쪽에 단계 이름, 오른쪽에 그 단계 버튼들.</summary>
        private static EditorGUILayout.HorizontalScope Phase(string name)
        {
            var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(name, EditorStyles.miniBoldLabel, GUILayout.Width(34));
            return row;
        }

        private static bool Button(string label, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(label, GUILayout.Height(24));
        }

        private static void Note(string text) => EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);

        private void DrawYaguang(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("2. 야광귀", EditorStyles.boldLabel);
            bool timeline = ready && network.VfxLabYaguangFeverReady;
            bool running = ready && network.VfxLabComboRunning, active = ready && network.VfxLabComboActive;

            Gimmick("Y1 섬광");
            using (Phase("예보"))
            {
                if (Button("눈 뜸 → 3 2 1부터", timeline))
                    Report(network.VfxLabStartFlash(), "섬광 예고를 시작했다 (눈 뜸 → 3 2 1 → 번쩍).", "지금은 시작할 수 없음 (섬광/콤보/보상/벌칙 진행 중, 또는 피버 뒤)");
            }
            using (Phase("진행"))
            {
                if (Button("예고 없이 바로 번쩍", timeline))
                    Report(network.VfxLabStartFlash(true), "바로 번쩍했다. 화면이 하얗다.", "지금은 시작할 수 없음 (섬광/콤보/보상/벌칙 진행 중, 또는 피버 뒤)");
            }
            using (Phase("파훼"))
            {
                if (Button("다 흔든 것으로 해제", ready && network.VfxLabFlashGlared))
                {
                    LabYaguangFlashHud flashHud = Object.FindFirstObjectByType<LabYaguangFlashHud>();
                    Report(flashHud != null && flashHud.VfxLabShakeAll(), "흔들기를 다 채웠다. 섬광이 풀리고 \"으악 어지러워~\" 뒤 오색구슬이 나온다.", "지금 눈부시지 않음 (섬광이 걸려 있을 때만)");
                }
            }
            using (Phase("보상"))
            {
                fiveColorCount = EditorGUILayout.IntSlider(fiveColorCount, 1, 3, GUILayout.Width(120));
                if (Button("오색구슬 받기 (흔들기 없이)", ready))
                {
                    int dropped = network.VfxLabGiveFiveColorOrbs(fiveColorCount);
                    Report(dropped > 0, "오색구슬 " + dropped + "개를 놓았다. 박에 던지면 다섯 색으로 칠한다.", "판이 가득 찼음");
                }
            }
            using (Phase("처벌")) Note("따로 없음: 못 풀면 하얀 화면이 다음 섬광 / 피버까지 남는다 (진행 버튼 뒤 흔들지 않고 둔다).");

            Gimmick("Y2 콤보 / Y3 BIG · SMALL");
            using (Phase("예보"))
            {
                if (Button("넷 셋 둘 하나 + 색 미리 보기부터", timeline && !running))
                    Report(network.VfxLabStartCombo(), "콤보 예고를 시작했다. 넷 셋 둘 하나에 맞춰 칸이 실제 색으로 채워진다.", "지금은 시작할 수 없음 (섬광/콤보/보상/벌칙 진행 중, 또는 피버 뒤)");
            }
            using (Phase("진행"))
            {
                if (Button("예고 없이 바로 4칸", timeline && !running))
                    Report(network.VfxLabStartComboNow(), "콤보 4칸이 바로 시작됐다 (하트 3개).", "지금은 시작할 수 없음 (섬광/콤보/보상/벌칙 진행 중, 또는 피버 뒤)");
                if (Button("다음 칸 색 구슬 받기", active))
                    Report(network.VfxLabGiveComboOrb(out LabOrbElement color), "내 판에 " + Name(color) + " 구슬을 놓았다. 박에 던져 맞히면 칸이 체크된다.", "판이 가득 찼음");
                if (Button("다른 색 구슬 받기", active))
                    Report(network.VfxLabGiveOtherColorOrb(out LabOrbElement other), "내 판에 " + Name(other) + " 구슬을 놓았다. 빛 테두리가 붙지 않아야 한다.", "판이 가득 찼음");
            }
            using (Phase("파훼"))
            {
                if (Button("한 칸 맞히기", active))
                    Report(network.VfxLabHitComboSlot(), "한 칸을 맞혔다. 도장 → 강조가 다음 칸으로.", "맞힐 수 없음");
                if (Button("남은 칸 다 맞히기 → 성공", active))
                    Report(network.VfxLabCompleteCombo(), "콤보 성공. BIG 보상이 걸렸다.", "성공 처리를 못 함");
            }
            using (Phase("보상"))
            {
                Note("BIG: 파훼에서 \"남은 칸 다 맞히기\".");
                if (Button("BIG / SMALL 바로 끝내기", ready && network.VfxLabComboEffectOn))
                    Report(network.VfxLabEndComboEffect(), "BIG / SMALL을 끝냈다. 끝 연출과 소리가 나온다.", "걸려 있는 BIG / SMALL이 없음");
            }
            using (Phase("처벌"))
            {
                if (Button("틀린 색 한 번 (봐주기 → 하트 깨짐)", active))
                    Report(network.VfxLabWrongCombo(), "틀린 색을 맞혔다. 첫 칸 전이면 봐주고, 아니면 하트가 하나 깨진다 (마지막 하트면 실패).", "콤보 4칸이 진행 중이 아님");
                if (Button("바로 실패 → SMALL", active))
                    Report(network.VfxLabFailCombo(), "콤보 실패. SMALL 벌칙이 걸렸다.", "실패 처리를 못 함");
            }
            if (ready && running && !active) Note("예고 중. 4칸이 나오면 진행 / 파훼 / 처벌 버튼이 켜진다.");

            Gimmick("Y5 야광귀 피버");
            using (Phase("진행"))
            {
                if (Button("피버 바로 시작 (100초를 지금으로)", timeline))
                    Report(network.VfxLabStartYaguangFever(), "피버를 시작했다. 이 판의 콤보/섬광은 더 나오지 않는다.", "피버를 시작할 수 없음");
            }
            if (ready && !timeline) Note("이미 피버 중. 다시 보려면 Play를 멈추고 다시 연다.");
        }

        private void DrawJigwi(LabNetwork network, bool ready)
        {
            EditorGUILayout.LabelField("3. 지귀", EditorStyles.boldLabel);
            LabJigwiABKind? mission = ready ? network.VfxLabJigwiMission : null;
            bool on = ready && network.VfxLabJigwiOn, free = on && mission == null;
            bool smoke = on && mission == LabJigwiABKind.GroupFire, talisman = on && mission == LabJigwiABKind.PersonalBurn;
            Note("지금: " + (!ready ? "전투 전" : !on ? "피버 (미션 없음)" : smoke ? "연기 진행 중" : talisman ? "부적 진행 중" : "미션 없음"));

            Gimmick("J1 연기 → 검은 구슬");
            using (Phase("예보"))
            {
                if (Button("지귀 도발 예고부터", free))
                    Report(network.VfxLabStartJigwiMission(LabJigwiABKind.GroupFire, false), "연기 예고를 시작했다 (2초 뒤 연기가 덮는다).", "지금은 시작할 수 없음 (다른 미션 진행 중이거나 남은 차례가 없음)");
            }
            using (Phase("진행"))
            {
                if (Button("예고 없이 바로 연기", free))
                    Report(network.VfxLabStartJigwiMission(LabJigwiABKind.GroupFire, true), "연기가 바로 덮었다. 문질러 닦아 본다 (멈추면 다시 번짐).", "지금은 시작할 수 없음");
            }
            using (Phase("파훼"))
            {
                if (Button("다 닦은 것으로 → 확 걷힘", smoke))
                {
                    LabJigwiBucketHandleView view = Object.FindFirstObjectByType<LabJigwiBucketHandleView>();
                    Report(view != null && view.VfxLabWipeAll(), "연기를 다 닦았다. 확 걷히고 파편 4개 + 가운데 빈 구슬이 나온다.", "내 화면에 연기가 없음 (연기가 덮은 뒤에)");
                }
            }
            using (Phase("보상"))
            {
                if (Button("파편 4개 받기", ready))
                    Report(network.VfxLabGiveSootShards(), "검은 파편 4개를 놓았다. 탭하거나 끌어다 가운데 빈 구슬에 넣는다.", "놓을 수 없음");
                if (Button("검정 구슬 받기", ready))
                    Report(network.VfxLabGiveSootOrb(), "검정 구슬을 놓았다 (파편이 모여드는 장면 → 던져서 명중).", "판이 가득 찼음");
            }
            using (Phase("처벌"))
            {
                if (Button("시간 끝 (파편 없이 사라짐)", smoke))
                    Report(network.VfxLabFinishJigwiMission(false), "연기 시간이 다 됐다. 파편 없이 끝난다.", "연기가 진행 중이 아님");
            }

            Gimmick("J2 부적 → J4 물 폭포 / J3 실패");
            using (Phase("예보"))
            {
                if (Button("지귀 도발 예고부터", free))
                    Report(network.VfxLabStartJigwiMission(LabJigwiABKind.PersonalBurn, false), "부적 예고를 시작했다 (2초 뒤 부적이 뜬다).", "지금은 시작할 수 없음 (다른 미션 진행 중이거나 남은 차례가 없음)");
            }
            using (Phase("진행"))
            {
                if (Button("예고 없이 바로 부적", free))
                    Report(network.VfxLabStartJigwiMission(LabJigwiABKind.PersonalBurn, true), "부적이 바로 떴다. 획을 순서대로 따라 쓴다.", "지금은 시작할 수 없음");
                fakeSeats = EditorGUILayout.IntSlider(fakeSeats, 0, 4, GUILayout.Width(150));
                // 지귀 전투를 혼자 Host로 할 때만 화면에 더한다. Test Runner를 돌릴 때는 이 창을 닫는다.
                LabJigwiScreenPresentation.VfxLabFakeSeats = ready ? fakeSeats : 0;
            }
            Note("가짜 자리 수: 부적이 시작되면 가짜 자리가 0.6초 간격으로 다 쓴 것처럼 켜진다 (화면만).");
            using (Phase("파훼"))
            {
                if (Button("모두 다 쓴 것으로", talisman))
                    Report(network.VfxLabFinishJigwiMission(true), "부적 완성. 2.5초 뒤 부적이 빛나고 물 폭포가 쏟아진다.", "부적이 진행 중이 아님 (예고 중이면 잠시 뒤)");
            }
            using (Phase("보상")) Note("물 폭포: 파훼 2.5초 뒤 저절로 (박에 물 칠).");
            using (Phase("처벌"))
            {
                if (Button("시간 끝 → 구슬 탐 + 북 미끄러움", talisman))
                    Report(network.VfxLabFinishJigwiMission(false), "부적 실패. 대사 뒤 판의 구슬이 타고 북이 미끄러워진다.", "부적이 진행 중이 아님");
            }

            Gimmick("지귀 피버");
            using (Phase("진행"))
            {
                if (Button("피버 바로 시작 (100초를 지금으로)", on))
                    Report(network.VfxLabStartJigwiFever(), "지귀 피버를 시작했다. 진행 중이던 미션은 여기서 끝난다.", "피버를 시작할 수 없음 (이미 피버 중)");
            }
            if (ready && !on) Note("이미 피버 중. 다시 보려면 Play를 멈추고 다시 연다.");
            EditorGUILayout.LabelField("어떤 기믹을 띄울까 (Play 전에 고른다)", EditorStyles.miniBoldLabel);
            {
                // #724: the mode lives only in this editor session (SessionState), never in LabConfig.asset.
                int mode = Mathf.Clamp(SessionState.GetInt(LabConfig.VfxLabJigwiModeKey, 0), 0, JigwiModes.Length - 1);
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    mode = GUILayout.Toolbar(mode, JigwiModes);
                if (EditorGUI.EndChangeCheck()) SessionState.SetInt(LabConfig.VfxLabJigwiModeKey, mode);
                EditorGUILayout.LabelField(JigwiModeHelp[mode], EditorStyles.wordWrappedMiniLabel);
                if (mode != 0)
                    EditorGUILayout.HelpBox("실험실에서만 켜졌다 (LabConfig.asset은 그대로). 이 창을 닫으면 기본 시간표로 돌아간다.", MessageType.Info);
            }
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
