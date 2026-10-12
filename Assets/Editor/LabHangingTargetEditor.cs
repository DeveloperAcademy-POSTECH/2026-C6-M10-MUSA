using UnityEditor;
using UnityEngine;

namespace C6Lab.Editor
{
    [CustomEditor(typeof(LabHangingTarget))]
    public sealed class LabHangingTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("피냐타 흔들림 실험", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("impactStrength"),
                new GUIContent("밀리는 힘", "같은 명중 속도에서의 충격 배율. 0~20, 기본 2.4"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swingDamping"),
                new GUIContent("흔들림 감쇠", "높을수록 빨리 멈춥니다. 0~20, 기본 1.15"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumAngle"),
                new GUIContent("최대 흔들림 각도", "수직에서 벗어날 수 있는 각도. 1~75도, 기본 24"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("returnStrength"),
                new GUIContent("원래 위치로 돌아오는 힘", "높을수록 복원력이 커집니다. 0~100, 기본 13"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ropeLength"),
                new GUIContent("줄 길이", "천장 고정점부터 표적 상단까지의 길이. 0.1~8, 기본 0.85. 표적의 정지 중심은 유지됩니다."));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("위아래 탄성 움직임", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceStrength"),
                new GUIContent("위아래 튕기는 힘", "명중 속도에 따른 아래 방향 충격 배율. 0~2, 기본 0.18"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceReturnStrength"),
                new GUIContent("위아래 복원력", "줄의 기본 길이로 돌아오려는 힘. 0~100, 기본 45"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bounceDamping"),
                new GUIContent("위아래 감쇠", "높을수록 위아래 진동이 빨리 멈춥니다. 0~20, 기본 3"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumExtension"),
                new GUIContent("최대 늘어나는 거리", "줄이 추가로 늘어나는 월드 거리. 0~3, 기본 0.5. 0이면 위아래 움직임을 끕니다."));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "실행 중 변경은 즉시 반영되며, Play를 멈추면 되돌아갑니다. 기록한 값을 멈춘 뒤 다시 입력하고 Scene을 저장하세요."
                : "값을 직접 입력한 후 Scene을 저장하세요. 실험할 때는 밀리는 힘만 바꾸고 나머지 세 값은 유지하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("같은 충격으로 반응만 확인"))
                {
                    var hanging = (LabHangingTarget)target;
                    hanging.ResetSwing();
                    // A fixed target-local impulse makes repeated tuning comparable.
                    hanging.Kick(hanging.transform.TransformDirection(new Vector3(6f, 0f, -9f)));
                }
                if (GUILayout.Button("흔들림 초기화")) ((LabHangingTarget)target).ResetSwing();
            }
            EditorGUILayout.LabelField("미리보기는 HP·스태미나를 바꾸지 않는 로컬 반응 검사입니다.", EditorStyles.wordWrappedMiniLabel);
        }

        [MenuItem("C6 Lab/Pinata/Select Target")]
        public static void SelectTarget()
        {
            var hanging = Object.FindAnyObjectByType<LabHangingTarget>();
            if (hanging == null) { Debug.LogWarning("Open Assets/Lab/Scenes/Lab.unity first."); return; }
            Selection.activeGameObject = hanging.gameObject;
            EditorGUIUtility.PingObject(hanging.gameObject);
        }
    }
}
