using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    [CustomEditor(typeof(StoryCutDefinition))]
    public sealed class StoryCutDefinitionEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            var pages = serializedObject.FindProperty("pages");
            pages.isExpanded = true;
            for (int i = 0; i < pages.arraySize; i++) pages.GetArrayElementAtIndex(i).isExpanded = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cutId"), new GUIContent("컷 ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("viewPrefab"), new GUIContent("그림 프리팹"));
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("한 항목이 대사 한 페이지입니다. +로 추가하고 순서를 바꾸세요. 마지막 페이지 이후 다음 그림으로 넘어갑니다.", MessageType.Info);
            var pages = serializedObject.FindProperty("pages");
            EditorGUILayout.PropertyField(pages, new GUIContent("대사 페이지"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("direction"), new GUIContent("연출 메모"));
            serializedObject.ApplyModifiedProperties();
            var cut = (StoryCutDefinition)target;
            if (pages.arraySize == 0 && GUILayout.Button("기존 대사를 첫 페이지로 가져오기"))
            {
                Undo.RecordObject(cut, "Convert story dialogue to pages");
                cut.pages.Add(cut.GetPage(0));
                EditorUtility.SetDirty(cut);
            }
        }

        [MenuItem("Hapio/Story/Edit Opening Text")]
        public static void SelectOpeningText()
        {
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(
                "Assets/Hapio/Story/Opening/Cuts/OP_001.asset");
            Selection.activeObject = cut;
            EditorGUIUtility.PingObject(cut);
        }

        [MenuItem("Hapio/Story/Upgrade Legacy Opening Dialogue To Pages")]
        public static void UpgradeOpening()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:StoryCutDefinition", new[] { "Assets/Hapio/Story/Opening/Cuts" }))
            {
                var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (cut.pages.Count > 0) continue;
                Undo.RecordObject(cut, "Convert story dialogue to pages");
                cut.pages.Add(cut.GetPage(0));
                EditorUtility.SetDirty(cut);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
