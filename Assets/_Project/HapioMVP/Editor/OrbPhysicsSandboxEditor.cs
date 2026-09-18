using System;
using C6.Prototype.PhysicsSandbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace C6.Editor
{
    [CustomEditor(typeof(OrbPhysicsSandbox))]
    public sealed class OrbPhysicsSandboxEditor : UnityEditor.Editor
    {
        static readonly string[] TuningFields =
        {
            "orbRadiusScreenFraction", "orbRadiusCapScale", "orbRestitution", "orbContactFriction", "orbFloorDeceleration",
            "orbStopSpeed", "orbMaxReleaseSpeed", "orbReleaseSampleWindow"
        };

        bool referencesExpanded;

        public override void OnInspectorGUI()
        {
            var sandbox = (OrbPhysicsSandbox)target;
            EditorGUILayout.LabelField("구슬 물리 튜닝", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Game 화면에서 구슬을 드래그한 뒤 놓으세요. 아래 값을 바꾸면 즉시 반영됩니다. 마음에 드는 값은 Play를 끄기 전에 '튜닝값 저장'을 누르세요."
                : "음·양 구슬은 Play 전에도 보입니다. Scene에서 시작 위치를 옮긴 뒤 Play를 눌러 마우스로 밀고 던져 보세요. 아래 값은 이 테스트 씬의 작업값입니다.", MessageType.Info);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            var tuning = serializedObject.FindProperty("tuning");
            DrawTuning(tuning, "orbRadiusScreenFraction", "구슬 반지름 / 화면 너비", "각 조합대 너비에 대한 반지름 비율입니다. 0.055는 너비의 5.5% 반지름이며, 표시 크기와 충돌·잡기 판정에 함께 적용됩니다.");
            DrawTuning(tuning, "orbRadiusCapScale", "크기 상한 배율", "게임 코드의 5x4 그리드 반지름 상한에 곱하는 값입니다. 1은 기존 게임과 같고, 올릴수록 구슬을 더 크게 만들 수 있습니다.");
            DrawTuning(tuning, "orbRestitution", "반발력", "0은 덜 튀고 1은 강하게 튑니다. 구슬 간 충돌과 상하 경계의 반발에 적용됩니다.");
            DrawTuning(tuning, "orbContactFriction", "접촉 마찰", "구슬끼리 접촉할 때의 마찰입니다. 혼자 미끄러지는 구슬의 감속은 아래 '바닥 감속'으로 조절하세요.");
            DrawTuning(tuning, "orbFloorDeceleration", "바닥 감속", "초당 감소하는 속도입니다. 단위는 조합대 너비/초²입니다. 작을수록 오래 미끄러지고 클수록 빨리 멈춥니다.");
            DrawTuning(tuning, "orbStopSpeed", "완전 정지 기준 속도", "이 속도보다 느려지면 정지합니다. 단위는 조합대 너비/초입니다.");
            DrawTuning(tuning, "orbMaxReleaseSpeed", "놓을 때 최대 속도", "드래그 후 놓을 때 허용하는 최대 속도입니다. 단위는 조합대 너비/초입니다. 예: 2는 1초에 화면 너비 2배입니다.");
            DrawTuning(tuning, "orbReleaseSampleWindow", "놓기 속도 측정 구간 (초)", "놓기 직전 움직임을 평균 내는 시간입니다. 짧으면 마지막 손동작에 민감하고 길면 부드러워집니다.");
            DrawSizeInfo(sandbox);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("matchGameArea"),
                new GUIContent("실제 게임 조합대 영역", "Play 시 게임의 하단 비율·Safe Area·하단 버튼 영역을 반영합니다. 끄면 A/B를 함께 보는 전체 화면입니다."));
            using (new EditorGUI.DisabledScope(!sandbox.matchGameArea))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useHalfScreenArea"),
                    new GUIContent("화면 절반 영역 (기획 시안)", "켜면 현재 게임 영역 대신 시안처럼 화면의 약 49%를 구슬 영역으로 씁니다."));
                if (sandbox.useHalfScreenArea)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("halfScreenArea"),
                        new GUIContent("시안 영역 비율", "전체 화면 대비 비율, 아래쪽이 0. X/Y = 왼쪽/아래 시작, W/H = 너비/높이."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyGameSizeCap"),
                new GUIContent("실제 게임 크기 제한", "켜면 게임 코드와 같은 5x4 그리드 기준 반지름 상한을 적용합니다. 끄면 상한 없이 미리보기만 합니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("visibleBoard"),
                new GUIContent("표시할 화면 (0=A, 1=B)", "실제 영역 모드에서 포탈로 전달된 구슬을 받으려면 상대 화면으로 전환하세요."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("portalsEnabled"),
                new GUIContent("좌우 포탈 통과", "켜면 A↔B 좌우 경계를 이동합니다. 양쪽 바깥 경계도 이웃 화면으로 이어집니다. 끄면 좌우 벽에서 반발합니다. 이 스위치는 테스트 씬 전용입니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boardColor"), new GUIContent("조합대 색", "구슬이 움직이는 영역의 배경색"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("outsideColor"), new GUIContent("바깥 영역 색", "조합대 밖 배경색"));
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                sandbox.ApplyTuning();
                if (!Application.isPlaying && sandbox.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(sandbox.gameObject.scene);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !sandbox.IsReady))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("배치 초기화")) sandbox.ResetOrbs();
                    if (GUILayout.Button("구슬 멈추기")) sandbox.StopOrbs();
                }
                EditorGUILayout.LabelField("구슬 추가 / 제거", "현재 " + (Application.isPlaying ? sandbox.OrbCount.ToString() : "-") + " / " + OrbPhysicsSandbox.MaxOrbs + "개");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ 음(Yin)")) sandbox.AddOrb(C6.Prototype.Orbs.OrbPolarity.Yin);
                    if (GUILayout.Button("+ 양(Yang)")) sandbox.AddOrb(C6.Prototype.Orbs.OrbPolarity.Yang);
                    if (GUILayout.Button("+ 결합(COMB)")) sandbox.AddOrb(C6.Prototype.Orbs.OrbKind.Combined, C6.Prototype.Orbs.OrbPolarity.None);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("마지막 구슬 제거")) sandbox.RemoveLastOrb();
                    if (GUILayout.Button("추가한 구슬 모두 제거")) sandbox.RemoveAddedOrbs();
                }
                EditorGUILayout.HelpBox("결합: 음 구슬을 양 구슬 위(화면 너비 8% 이내)에 놓으면 두 구슬 사이에 COMB가 생깁니다. 같은 극·COMB끼리는 결합되지 않고, 가까이 놓으면 관성 없이 멈춥니다(게임과 동일).", MessageType.None);
                EditorGUILayout.HelpBox("추가한 구슬은 지금 보고 있는 조합대에 생깁니다. '배치 초기화'를 누르면 추가한 구슬은 사라지고 처음 배치로 돌아갑니다.", MessageType.None);
            }
            using (new EditorGUI.DisabledScope(sandbox.sourceConfig == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("저장된 값 불러오기"))
                    {
                        Undo.RecordObject(sandbox, "Reload saved orb tuning");
                        sandbox.ReloadSavedTuning();
                        if (!Application.isPlaying) EditorUtility.SetDirty(sandbox);
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("튜닝값 저장"))
                    {
                        SaveTuning(sandbox);
                        ShowNotification("8개 크기·물리 설정을 저장했습니다.");
                    }
                }
            }
            EditorGUILayout.HelpBox("'튜닝값 저장'은 공용 ScreenLayoutConfig의 위 8개 항목만 저장합니다. 저장한 값은 실제 게임을 다음에 시작할 때 사용됩니다. Play 중 변경한 값은 이 버튼을 누르지 않으면 되돌아갑니다.", MessageType.None);
            EditorGUILayout.HelpBox("이 씬은 같은 물리 코드로 두 조합대 사이 이동과 음·양 결합을 로컬에서 시험합니다. 몬스터 공격은 제외하며, 기기 간 네트워크와 휴대폰 터치 감각은 별도 확인이 필요합니다.", MessageType.None);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("이번 실행의 포탈 통과", sandbox.TransferCount.ToString());
                EditorGUILayout.LabelField("최근 입력", sandbox.LastInput);
                EditorGUILayout.LabelField("이번 실행의 결합", sandbox.CombineCount.ToString());
                EditorGUILayout.LabelField("최근 결합 결과", sandbox.LastCombineResult);
                EditorGUILayout.LabelField("터치 입력 수", sandbox.TouchSamples.ToString());
                Repaint();
            }
            referencesExpanded = EditorGUILayout.Foldout(referencesExpanded, "연결된 에셋과 구슬", true);
            if (referencesExpanded)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("공용 설정", sandbox.sourceConfig, typeof(C6.Prototype.Presentation.ScreenLayoutConfig), false);
                    EditorGUILayout.ObjectField("구슬 재질", sandbox.orbMaterial, typeof(Material), false);
                    EditorGUILayout.ObjectField("테스트 카메라", sandbox.sandboxCamera, typeof(Camera), true);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("seeds"), new GUIContent("미리 배치한 구슬"), true);
                }
            }
        }

        const float IPhone17WidthMm = 66.6f; // 402pt x 3px @ 460ppi

        static void DrawSizeInfo(OrbPhysicsSandbox sandbox)
        {
            sandbox.GetSizeInfo(out float requested, out float cap, out float used);
            string mm = string.Empty;
            if (Application.isPlaying && sandbox.matchGameArea && sandbox.sandboxCamera != null && sandbox.LayoutScreenWidth > 0)
            {
                float boardMm = sandbox.sandboxCamera.pixelRect.width / sandbox.LayoutScreenWidth * IPhone17WidthMm;
                mm = string.Format(" (iPhone 17 기준 지름 약 {0:0.0}mm)", used * 2f * boardMm);
            }
            string message = string.Format("실제 적용 반지름: 너비의 {0:0.0}%{1}\n요청값 {2:0.0}% / 게임 상한 {3:0.0}%", used * 100f, mm, requested * 100f, cap * 100f);
            if (requested > cap + .0001f)
                message += sandbox.applyGameSizeCap
                    ? "\n요청값이 게임 상한보다 커서 상한까지만 커집니다. 더 키우려면 '크기 상한 배율'을 올리세요."
                    : "\n게임 상한을 넘었습니다: 이 크기는 미리보기 전용이며 실제 게임에서는 작게 보입니다.";
            EditorGUILayout.HelpBox(message, requested > cap + .0001f ? MessageType.Warning : MessageType.Info);
        }

        static void DrawTuning(SerializedProperty tuning, string name, string label, string tooltip)
        {
            var property = tuning?.FindPropertyRelative(name);
            if (property == null) EditorGUILayout.HelpBox("튜닝 필드를 찾지 못했습니다: " + name, MessageType.Error);
            else EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }

        /// <summary>Copies only the eight size/physics fields; preserves every unrelated configuration value.</summary>
        public static void SaveTuning(OrbPhysicsSandbox sandbox)
        {
            if (sandbox == null) throw new ArgumentNullException(nameof(sandbox));
            if (sandbox.sourceConfig == null || !AssetDatabase.Contains(sandbox.sourceConfig))
                throw new InvalidOperationException("A saved ScreenLayoutConfig asset must be connected before saving tuning.");

            sandbox.ApplyTuning();
            var edited = new SerializedObject(sandbox);
            var tuning = edited.FindProperty("tuning");
            var config = new SerializedObject(sandbox.sourceConfig);
            // Validate the complete mapping before changing any asset field.
            foreach (string name in TuningFields)
                if (tuning?.FindPropertyRelative(name) == null || config.FindProperty(name) == null)
                    throw new InvalidOperationException("Missing orb tuning field: " + name);

            Undo.RecordObject(sandbox.sourceConfig, "Save orb physics tuning");
            foreach (string name in TuningFields)
                config.FindProperty(name).floatValue = tuning.FindPropertyRelative(name).floatValue;
            config.ApplyModifiedProperties();
            EditorUtility.SetDirty(sandbox.sourceConfig);
            AssetDatabase.SaveAssetIfDirty(sandbox.sourceConfig);
            Debug.Log("C6_ORB_SANDBOX_TUNING_SAVED " + AssetDatabase.GetAssetPath(sandbox.sourceConfig), sandbox.sourceConfig);
        }

        static void ShowNotification(string message)
        {
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.ShowNotification(new GUIContent(message));
        }
    }

    [CustomEditor(typeof(OrbSandboxSeed))]
    public sealed class OrbSandboxSeedEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var seed = (OrbSandboxSeed)target;
            var sandbox = seed.GetComponentInParent<OrbPhysicsSandbox>();
            EditorGUILayout.HelpBox("Scene에서 구슬을 선택하고 W 키로 시작 위치를 옮길 수 있습니다. 크기·마찰·반발 등 공통 값은 'Orb Physics Tuning'에서 바꿉니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(sandbox == null))
                if (GUILayout.Button("공통 물리 설정 선택")) Selection.activeGameObject = sandbox.gameObject;
            using (new EditorGUI.DisabledScope(Application.isPlaying)) DrawDefaultInspector();
        }
    }
}
