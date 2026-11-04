using System;
using System.Collections.Generic;
using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class BattleLightingStudio : EditorWindow
{
    [Serializable] sealed class LightState
    {
        public Light light;
        public bool enabled;
        public Color color;
        public float intensity;
        public Quaternion rotation;
    }
    readonly List<LightState> baseline = new List<LightState>();
    Vector2 scroll;
    Scene boundScene;
    AmbientMode ambientMode;
    Color ambientLight, sky, equator, ground;
    float ambientIntensity, reflectionIntensity;
    Material skybox;

    [MenuItem("Hapio/배틀 조명 작업실")]
    public static void Open()
    {
        var window = GetWindow<BattleLightingStudio>("배틀 조명 작업실");
        window.minSize = new Vector2(360, 500);
        window.Show();
    }
    void OnEnable() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
    void OnDisable() { EditorApplication.playModeStateChanged -= OnPlayModeChanged; }
    void OnPlayModeChanged(PlayModeStateChange state) { baseline.Clear(); Repaint(); }
    bool IsBattle => SceneManager.GetActiveScene().path == "Assets/Hapio/Scenes/Battle.unity";
    void Capture()
    {
        boundScene = SceneManager.GetActiveScene();
        baseline.Clear();
        foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (light.gameObject.scene == boundScene)
                baseline.Add(new LightState { light = light, enabled = light.enabled,
                    color = light.color, intensity = light.intensity, rotation = light.transform.localRotation });
        ambientMode = RenderSettings.ambientMode;
        ambientLight = RenderSettings.ambientLight;
        sky = RenderSettings.ambientSkyColor;
        equator = RenderSettings.ambientEquatorColor;
        ground = RenderSettings.ambientGroundColor;
        ambientIntensity = RenderSettings.ambientIntensity;
        reflectionIntensity = RenderSettings.reflectionIntensity;
        skybox = RenderSettings.skybox;
    }
    void RecordEnvironment()
    {
        if (!Application.isPlaying)
        {
            var settings = Unsupported.GetRenderSettings();
            Undo.RecordObject(settings, "Adjust battle environment light");
        }
    }
    void Changed()
    {
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(boundScene);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
    void Restore()
    {
        foreach (var state in baseline)
        {
            if (state.light == null) continue;
            Undo.RecordObjects(new UnityEngine.Object[] { state.light, state.light.transform }, "Restore battle lighting");
            state.light.enabled = state.enabled;
            state.light.color = state.color;
            state.light.intensity = state.intensity;
            state.light.transform.localRotation = state.rotation;
        }
        RecordEnvironment();
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.skybox = skybox;
        Changed();
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("배틀 조명 작업실", EditorStyles.boldLabel);
        if (!IsBattle)
        {
            EditorGUILayout.HelpBox("Battle 씬에서 사용하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Battle 씬 열기") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene("Assets/Hapio/Scenes/Battle.unity");
            return;
        }
        if (boundScene != SceneManager.GetActiveScene() || baseline.Count == 0) Capture();
        EditorGUILayout.HelpBox(Application.isPlaying
            ? "플레이 중: 조절은 임시 미리보기입니다. 종료하면 씬 설정으로 돌아갑니다."
            : "실시간으로 반영됩니다. 저장 버튼으로 Battle 씬에 저장하세요. Ctrl/Cmd+Z로 되돌릴 수 있습니다.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var state in baseline)
        {
            var light = state.light;
            if (light == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(light.name + " · " + light.type, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("조명 켜기", light.enabled);
            Color color = EditorGUILayout.ColorField("빛 색상", light.color);
            float intensity = Mathf.Max(0, EditorGUILayout.FloatField("빛 강도", light.intensity));
            Vector3 rotation = EditorGUILayout.Vector3Field("방향 (회전)", light.transform.localEulerAngles);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new UnityEngine.Object[] { light, light.transform }, "Adjust battle light");
                light.enabled = enabled; light.color = color; light.intensity = intensity;
                light.transform.localEulerAngles = rotation; Changed();
            }
            EditorGUILayout.LabelField("영향 레이어", light.cullingMask.ToString());
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("환경광 · 반사", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var mode = (AmbientMode)EditorGUILayout.EnumPopup("환경광 방식", RenderSettings.ambientMode);
        var box = (Material)EditorGUILayout.ObjectField("환경광 Skybox", RenderSettings.skybox, typeof(Material), false);
        float ambient = EditorGUILayout.Slider("환경광 강도", RenderSettings.ambientIntensity, 0, 4);
        Color flat = RenderSettings.ambientLight, top = RenderSettings.ambientSkyColor,
            middle = RenderSettings.ambientEquatorColor, bottom = RenderSettings.ambientGroundColor;
        if (mode == AmbientMode.Flat) flat = EditorGUILayout.ColorField("환경광 색상", flat);
        if (mode == AmbientMode.Trilight)
        {
            top = EditorGUILayout.ColorField("위쪽", top);
            middle = EditorGUILayout.ColorField("수평", middle);
            bottom = EditorGUILayout.ColorField("아래쪽", bottom);
        }
        float reflection = EditorGUILayout.Slider("반사 강도", RenderSettings.reflectionIntensity, 0, 1);
        if (EditorGUI.EndChangeCheck())
        {
            RecordEnvironment(); RenderSettings.ambientMode = mode; RenderSettings.skybox = box;
            RenderSettings.ambientIntensity = ambient; RenderSettings.reflectionIntensity = reflection;
            if (mode == AmbientMode.Flat) RenderSettings.ambientLight = flat;
            if (mode == AmbientMode.Trilight) { RenderSettings.ambientSkyColor = top;
                RenderSettings.ambientEquatorColor = middle; RenderSettings.ambientGroundColor = bottom; }
            Changed();
        }
        EditorGUILayout.HelpBox("하늘 PNG와 환경광 Skybox는 별개입니다. 바닥은 Lit 재질이라 조명의 영향을 받습니다. 이 설정은 세 스테이지가 공유합니다.", MessageType.None);
        var backgrounds = FindObjectsByType<GameStageBackgrounds>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 1; i <= 3; i++)
                if (GUILayout.Button("Stage " + i) && backgrounds.Length > 0)
                {
                    var b = backgrounds[0];
                    Undo.RegisterFullObjectHierarchyUndo(b.transform.root.gameObject, "Preview stage lighting");
                    b.ApplyStage(i); Changed();
                }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("작업실을 열었을 때 상태로 복원")) Restore();
        if (GUILayout.Button("현재 상태를 복원 기준으로 지정")) Capture();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Battle 씬 저장", GUILayout.Height(30))) EditorSceneManager.SaveScene(boundScene);
    }
}
