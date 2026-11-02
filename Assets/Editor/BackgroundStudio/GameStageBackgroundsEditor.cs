using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GameStageBackgrounds))]
public sealed class GameStageBackgroundsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("스테이지 버튼으로 배경을 미리 봅니다. 실행할 때는 게임의 스테이지가 자동 선택됩니다. 각 Studio에서 PNG를 교체한 뒤 ‘이미지만 적용’을 누르세요. 기본 배경은 Stage 1·3 및 별도 세트가 없는 스테이지에 사용됩니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 1; i <= 3; i++)
                if (GUILayout.Button("Stage " + i + " 미리보기")) Preview(i);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void Preview(int stage)
    {
        var backgrounds = (GameStageBackgrounds)target;
        if (backgrounds.defaultBackground.studio != null) Undo.RecordObject(backgrounds.defaultBackground.studio.gameObject, "Preview background");
        if (backgrounds.defaultBackground.ground != null) Undo.RecordObject(backgrounds.defaultBackground.ground, "Preview ground");
        if (backgrounds.defaultBackground.boardBacking != null) Undo.RecordObject(backgrounds.defaultBackground.boardBacking, "Preview backing");
        foreach (var entry in backgrounds.stageOverrides)
        {
            if (entry?.background?.studio != null) Undo.RecordObject(entry.background.studio.gameObject, "Preview background");
            if (entry?.background?.ground != null) Undo.RecordObject(entry.background.ground, "Preview ground");
            if (entry?.background?.boardBacking != null) Undo.RecordObject(entry.background.boardBacking, "Preview backing");
        }
        backgrounds.ApplyStage(stage);
        EditorSceneManager.MarkSceneDirty(backgrounds.gameObject.scene);
        SceneView.RepaintAll();
    }
}
