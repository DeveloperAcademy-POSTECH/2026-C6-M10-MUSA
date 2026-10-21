using UnityEditor;
using UnityEngine;
using C6Lab;

[CustomEditor(typeof(LabBackgroundStudio))]
public sealed class LabBackgroundStudioEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("이미지·색·Opacity·두께·바람을 바꾼 뒤 ‘이미지만 적용’을 누르세요. 기존 오브젝트와 Transform은 유지됩니다. 원형 배치를 바꾸려면 별도 배치 버튼을 사용하세요. 두께는 이미지 층/교차면 표현이며 실제 조각형 3D 모델은 아닙니다.",MessageType.Info);
        DrawDefaultInspector();
        var studio=(LabBackgroundStudio)target;
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("이미지만 적용 / Preserve Layout",GUILayout.Height(32)))
            {
                Undo.RegisterCompleteObjectUndo(studio,"Apply background settings");
                studio.ApplyBackground();
            }
            if(GUILayout.Button("원형 배치 재계산 / Relayout"))
            {
                if(EditorUtility.DisplayDialog("배치 재계산", "장식의 위치·회전·크기를 목록의 원형 배치값으로 변경합니다. 개별 Transform을 유지하려면 취소하세요.", "배치 변경", "취소"))
                {Undo.RegisterCompleteObjectUndo(studio,"Relayout background");studio.RelayoutBackground();}
            }
            if(GUILayout.Button("장식 PNG 추가 / Add Decoration"))
            {
                Undo.RecordObject(studio,"Add decoration");studio.decorations.Add(new LabBackgroundStudio.Decoration());EditorUtility.SetDirty(studio);
            }
        }
        if(GUILayout.Button("전체 보기 / Frame Environment") && SceneView.lastActiveSceneView!=null)
            SceneView.lastActiveSceneView.LookAt(studio.Center,Quaternion.Euler(35,30,0),32);
        EditorGUILayout.LabelField("좌석 시점 확인 / Scene View",EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for(int i=0;i<5;i++)if(GUILayout.Button("P"+(i+1)))
        {
            float a=-2*Mathf.PI*i/5;var pos=studio.Center+new Vector3(Mathf.Sin(a)*8,.65f,Mathf.Cos(a)*8);
            var look=studio.Center+Vector3.up*.8f;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(look,Quaternion.LookRotation(look-pos),Vector3.Distance(pos,look),false,true);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("P1~P5는 Scene 뷰의 방향 참고용입니다. 실제 휴대폰의 화면 비율·HUD까지 재현하는 Game 뷰는 아닙니다. 배경 변경 후 Scene 저장(Cmd+S)을 해주세요.",MessageType.None);
    }
}

[CustomPropertyDrawer(typeof(LabBackgroundStudio.Decoration))]
public sealed class LabBackgroundDecorationDrawer:PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property,GUIContent label)
    {
        if(!property.isExpanded)return EditorGUIUtility.singleLineHeight;
        float height=EditorGUIUtility.singleLineHeight+4;
        var iterator=property.Copy();var end=iterator.GetEndProperty();bool enter=true;
        while(iterator.NextVisible(enter)&&!SerializedProperty.EqualContents(iterator,end)){enter=false;height+=EditorGUI.GetPropertyHeight(iterator,true)+3;}
        return height;
    }
    public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
    {
        var title=property.FindPropertyRelative("title");var category=property.FindPropertyRelative("category");
        var header=new GUIContent((category!=null?category.stringValue+" · ":"")+(title!=null?title.stringValue:label.text));
        position.height=EditorGUIUtility.singleLineHeight;property.isExpanded=EditorGUI.Foldout(position,property.isExpanded,header,true);
        if(!property.isExpanded)return;
        EditorGUI.indentLevel++;position.y+=EditorGUIUtility.singleLineHeight+4;
        var iterator=property.Copy();var end=iterator.GetEndProperty();bool enter=true;
        while(iterator.NextVisible(enter)&&!SerializedProperty.EqualContents(iterator,end)){enter=false;position.height=EditorGUI.GetPropertyHeight(iterator,true);EditorGUI.PropertyField(position,iterator,true);position.y+=position.height+3;}
        EditorGUI.indentLevel--;
    }
}
