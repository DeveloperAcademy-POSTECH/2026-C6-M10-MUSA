using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>
    /// #757 엔딩 SFX 창: 엔딩 이야기를 컷 단위로 바로 띄우고, 컷마다 배경음악 / 대사 음성 / 그림 효과음 / 덧붙인 효과음(StoryCutSound)을
    /// 바꿔 끼워 바로 듣는다. 고친 값은 컷 에셋과 View 프리팹에 바로 들어가고 "저장"으로 디스크에 쓴다(실제 게임 엔딩도 같은 에셋을 쓴다).
    /// </summary>
    public sealed class StorySfxLabWindow : EditorWindow
    {
        private const string EndingPath = "Assets/Hapio/Story/StageStories/Ending.asset";
        private int selected;
        private Vector2 scroll;

        [MenuItem("Hapio/엔딩 SFX")]
        public static void Open() => GetWindow<StorySfxLabWindow>("엔딩 SFX");

        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        private void OnGUI()
        {
            var ending = AssetDatabase.LoadAssetAtPath<StorySequence>(EndingPath);
            if (ending == null) { EditorGUILayout.HelpBox("엔딩 목록(" + EndingPath + ")을 찾지 못했다.", MessageType.Error); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPlayer(ending);
            EditorGUILayout.Space();
            DrawCutTabs(ending);
            EditorGUILayout.Space();
            if (selected >= 0 && selected < ending.cuts.Count) DrawCut(ending, ending.cuts[selected]);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("저장 (에셋에 쓰기)", GUILayout.Height(30)))
                {
                    AssetDatabase.SaveAssets();
                    ShowNotification(new GUIContent("저장했다"));
                }
            }
            EditorGUILayout.LabelField("고친 값은 바로 에셋에 들어간다. 다시 들으려면 \"이 컷 다시 보기\". 되돌리기는 Ctrl/⌘+Z 또는 GitHub Desktop Discard.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ───────── 재생 ─────────
        private void DrawPlayer(StorySequence ending)
        {
            EditorGUILayout.LabelField("엔딩 재생", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                if (GUILayout.Button("엔딩 미리보기 열기 (Play)", GUILayout.Height(32)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    StageStoryAssets.OpenPreview();
                    EditorApplication.isPlaying = true;
                }
                EditorGUILayout.LabelField("미리보기 장면을 열고 Play한다. 소리 끼워 넣기 / 저장은 Play 전에도 된다.", EditorStyles.wordWrappedMiniLabel);
                return;
            }
            var player = Object.FindFirstObjectByType<StoryPlayer>();
            if (player == null) { EditorGUILayout.HelpBox("이 장면에 StoryPlayer가 없다. \"엔딩 미리보기 열기\"로 연다.", MessageType.Info); return; }
            bool playingEnding = player.IsPlaying && player.sequence == ending;
            string now = playingEnding && player.CurrentCutIndex >= 0 ? ending.cuts[player.CurrentCutIndex].cutId + " · " + (player.CurrentPageIndex + 1) + "쪽" : "엔딩이 재생 중이 아님";
            EditorGUILayout.LabelField("지금: " + now);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("엔딩 처음부터", GUILayout.Height(26))) Jump(player, ending, 0);
                using (new EditorGUI.DisabledScope(!playingEnding))
                {
                    if (GUILayout.Button("◀ 이전", GUILayout.Height(26))) player.Previous();
                    if (GUILayout.Button("다음 ▶", GUILayout.Height(26))) player.Next();
                }
            }
        }

        private static void Jump(StoryPlayer player, StorySequence ending, int index)
        {
            StoryCutSoundDriver.Ensure(player);
            if (!player.Play(ending) || index <= 0) return;
            typeof(StoryPlayer).GetMethod("ShowCut", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, new object[] { index });
        }

        // ───────── 컷 고르기 ─────────
        private void DrawCutTabs(StorySequence ending)
        {
            EditorGUILayout.LabelField("컷", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
                for (int i = 0; i < ending.cuts.Count; i++)
                {
                    var cut = ending.cuts[i];
                    string label = cut != null ? cut.cutId.Replace("Ending_", "") : "?";
                    bool on = GUILayout.Toggle(selected == i, label, "Button", GUILayout.Height(24));
                    if (on && selected != i) selected = i;
                }
        }

        // ───────── 컷 하나 ─────────
        private void DrawCut(StorySequence ending, StoryCutDefinition cut)
        {
            if (cut == null || cut.viewPrefab == null) { EditorGUILayout.HelpBox("컷 또는 View가 비어 있다.", MessageType.Warning); return; }
            EditorGUILayout.LabelField(cut.cutId, EditorStyles.largeLabel);
            if (!string.IsNullOrEmpty(cut.direction)) EditorGUILayout.LabelField(cut.direction, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if (GUILayout.Button("이 컷 다시 보기 (Play 중)", GUILayout.Height(26)))
                {
                    var player = Object.FindFirstObjectByType<StoryPlayer>();
                    if (player != null) Jump(player, ending, selected);
                }

            // 대사와 음성
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("대사 음성 (쪽마다)", EditorStyles.miniBoldLabel);
            var cutSo = new SerializedObject(cut);
            var pages = cutSo.FindProperty("pages");
            if (pages == null || pages.arraySize == 0) EditorGUILayout.LabelField("대사 없음", EditorStyles.miniLabel);
            else
                for (int i = 0; i < pages.arraySize; i++)
                {
                    var page = pages.GetArrayElementAtIndex(i);
                    string speaker = page.FindPropertyRelative("speaker").stringValue, line = page.FindPropertyRelative("dialogue").stringValue;
                    EditorGUILayout.LabelField((i + 1) + "쪽 " + (string.IsNullOrEmpty(speaker) ? "" : speaker + ": ") + (line ?? "").Replace("\n", " "),
                        EditorStyles.wordWrappedMiniLabel);
                    ClipRow(page.FindPropertyRelative("voice"), null, "음성");
                }
            cutSo.ApplyModifiedProperties();

            // 배경음악
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("배경음악 (이 컷이 시작될 때 바뀐다. 비우면 서서히 사라짐)", EditorStyles.miniBoldLabel);
            var viewSo = new SerializedObject(cut.viewPrefab);
            ClipRow(viewSo.FindProperty("music"), viewSo.FindProperty("musicVolume"), "곡");
            if (viewSo.ApplyModifiedProperties()) EditorUtility.SetDirty(cut.viewPrefab);

            // 그림 효과음
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("그림 효과음 (그림 움직임에 붙은 소리)", EditorStyles.miniBoldLabel);
            var beats = cut.viewPrefab.GetComponentsInChildren<StoryVisualBeat>(true);
            if (beats.Length == 0) EditorGUILayout.LabelField("없음", EditorStyles.miniLabel);
            foreach (var beat in beats)
            {
                var so = new SerializedObject(beat);
                EditorGUILayout.LabelField(beat.name + " · " + beat.motion + " · " + beat.delay.ToString("0.00") + "초", EditorStyles.miniLabel);
                ClipRow(so.FindProperty("sound"), so.FindProperty("volume"), "소리");
                if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(beat);
            }

            // 덧붙인 효과음
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("덧붙인 효과음 (컷 시작 뒤 N초, #757)", EditorStyles.miniBoldLabel);
            var extra = cut.viewPrefab.GetComponent<StoryCutSound>();
            if (extra == null)
            {
                if (GUILayout.Button("이 컷에 효과음 칸 만들기")) AddCutSound(cut.viewPrefab);
                return;
            }
            var extraSo = new SerializedObject(extra);
            var entries = extraSo.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    ClipRow(e.FindPropertyRelative("clip"), e.FindPropertyRelative("volume"), (i + 1) + "번");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("delay"), new GUIContent("나오는 시각(초)"));
                        if (GUILayout.Button("빼기", GUILayout.Width(48))) { entries.DeleteArrayElementAtIndex(i); break; }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("stopMusic"), new GUIContent("이때 음악 끊기 (테이프 멈춤)"));
                        if (e.FindPropertyRelative("stopMusic").boolValue)
                            EditorGUILayout.PropertyField(e.FindPropertyRelative("stopSeconds"), new GUIContent("멈추는 초"));
                    }
                }
            }
            if (GUILayout.Button("+ 효과음 하나 더")) { entries.InsertArrayElementAtIndex(entries.arraySize); var n = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                n.FindPropertyRelative("clip").objectReferenceValue = null; n.FindPropertyRelative("delay").floatValue = 0f;
                n.FindPropertyRelative("volume").floatValue = .8f; n.FindPropertyRelative("stopMusic").boolValue = false; n.FindPropertyRelative("stopSeconds").floatValue = .7f; }
            if (extraSo.ApplyModifiedProperties()) EditorUtility.SetDirty(extra);
        }

        private static void ClipRow(SerializedProperty clip, SerializedProperty volume, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(clip, new GUIContent(label));
                if (volume != null) EditorGUILayout.PropertyField(volume, GUIContent.none, GUILayout.Width(140));
                using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                    if (GUILayout.Button("▶", GUILayout.Width(28)))
                        Audition(clip.objectReferenceValue as AudioClip, volume != null ? volume.floatValue : 1f);
            }
        }

        private static void AddCutSound(StoryCutView prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<StoryCutSound>() == null) root.AddComponent<StoryCutSound>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>파일만 바로 들어 본다 (Play 중이면 게임 소리로, 아니면 에디터 미리 듣기로).</summary>
        private static void Audition(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (EditorApplication.isPlaying)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("SFX Lab Audition", HideFlags.HideAndDontSave, typeof(AudioSource));
                var source = go.GetComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.PlayOneShot(clip, Mathf.Clamp01(volume));
                Object.Destroy(go, clip.length + .3f);
                return;
            }
            var util = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            util?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
            var play = util?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            play?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
