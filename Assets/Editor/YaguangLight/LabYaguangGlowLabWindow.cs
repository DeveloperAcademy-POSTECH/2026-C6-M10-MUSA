using System;
using C6Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    public sealed class LabYaguangGlowLabWindow : EditorWindow
    {
        private enum Mode { 발광_계속보기, 예고_3, 예고_2, 예고_1, 번쩍임, 잔광, 전체_흐름 }
        [SerializeField] private LabYaguangFlashTuning tuning;
        [SerializeField] private Mode mode;
        [SerializeField] private Color backdrop = new Color(.035f, .055f, .065f);
        [SerializeField] private bool overlay, loop;
        [SerializeField] private float position;
        [SerializeField] private int previewShakes;
        private GameObject root;
        private Camera previewCamera;
        private LabYaguangFlashLight light;
        private LabYaguangShakeCue cue;
        private Image countImage;
        private RenderTexture output;
        private bool playing;
        private double started, lastFrame;
        private double previewFlashAt = double.NegativeInfinity;
        private string notice = "실험값입니다. 게임에는 저장 버튼을 눌러 적용합니다.";
        public RenderTexture PreviewTexture => output;

        [MenuItem("C6 Lab/Experiments/Yagwanggwi Glow Lab")]
        public static void Open()
        {
            var window = GetWindow<LabYaguangGlowLabWindow>();
            window.titleContent = new GUIContent("야광귀 발광 실험실");
            window.minSize = new Vector2(850, 680);
            window.Show();
        }
        private void OnEnable()
        {
            if (tuning == null) { tuning = LabYaguangFlashLightProfile.GameDefaults(); tuning.sourceArea = .18f; }
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Release;
            EditorApplication.playModeStateChanged -= PlayChanged;
            Release();
        }
        private void PlayChanged(PlayModeStateChange state) { Release(); }
        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.K && !EditorGUIUtility.editingTextField)
            { PreviewShake(); Event.current.Use(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(380)))
                {
                    GUILayout.Label("발광 강도 · 색 실험", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(notice, MessageType.Info);
                    EditorGUI.BeginChangeCheck();
                    mode = (Mode)EditorGUILayout.EnumPopup("미리보기", mode);
                    if (mode == Mode.번쩍임 || mode == Mode.전체_흐름)
                    {
                        position = EditorGUILayout.Slider("재생 위치", position, 0f, mode == Mode.전체_흐름 ? 4.2f : 1f);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(playing ? "멈춤" : "재생"))
                            { playing = !playing; started = EditorApplication.timeSinceStartup; position = 0; }
                            loop = GUILayout.Toggle(loop, "반복");
                        }
                    }
                    GUILayout.Space(8);
                    tuning.flashRadiance = EditorGUILayout.Slider("순간 발광 · HDR", tuning.flashRadiance, 0f, 100f);
                    tuning.sourceArea = EditorGUILayout.Slider("발광 면적", tuning.sourceArea, .02f, 1f);
                    tuning.coreColor = EditorGUILayout.ColorField(new GUIContent("중심 빛 색"), tuning.coreColor, true, false, true);
                    tuning.glowColor = EditorGUILayout.ColorField(new GUIContent("주변 광채 색"), tuning.glowColor, true, false, true);
                    tuning.bloomColor = EditorGUILayout.ColorField(new GUIContent("Bloom 색"), tuning.bloomColor, true, false, true);
                    GUILayout.Space(8);
                    tuning.flashBloom = EditorGUILayout.Slider("순간 Bloom", tuning.flashBloom, 0f, 10f);
                    tuning.otherBloom = EditorGUILayout.Slider("예고 · 잔광 Bloom", tuning.otherBloom, 0f, 10f);
                    tuning.scatter = EditorGUILayout.Slider("빛 번짐", tuning.scatter, 0f, 1f);
                    tuning.threshold = EditorGUILayout.Slider("Bloom 시작 밝기", tuning.threshold, 0f, 3f);
                    tuning.warningRadiance = EditorGUILayout.Slider("예고 발광 · HDR", tuning.warningRadiance, 0f, 10f);
                    tuning.warningGain = EditorGUILayout.Slider("예고 강도 배율", tuning.warningGain, 0f, 5f);
                    tuning.afterglowGain = EditorGUILayout.Slider("잔광 강도 배율", tuning.afterglowGain, 0f, 5f);
                    backdrop = EditorGUILayout.ColorField("비교 배경", backdrop);
                    overlay = EditorGUILayout.Toggle("게임 안내 함께 보기", overlay);
                    if (mode == Mode.잔광 || mode == Mode.전체_흐름)
                        previewShakes = EditorGUILayout.IntSlider("인식한 흔들기 / 10", previewShakes, 0, 10);
                    if (EditorGUI.EndChangeCheck()) Render();
                    if (mode == Mode.잔광 || mode == Mode.전체_흐름)
                        if (GUILayout.Button("K · 흔들기 1회")) PreviewShake();
                    GUILayout.Space(10);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("현재 게임값")) { tuning = LabYaguangFlashLightProfile.GameDefaults(); Render(); }
                        if (GUILayout.Button("작은 발광체 예시"))
                        {
                            tuning = new LabYaguangFlashTuning { sourceArea = .14f, flashRadiance = 24f, flashBloom = 3f,
                                coreColor = Color.white, glowColor = new Color(.05f, 1f, .65f), bloomColor = new Color(.2f, 1f, .75f) };
                            mode = Mode.발광_계속보기; Render();
                        }
                    }
                    if (GUILayout.Button("게임 기본값으로 저장")) SaveGame();
                    GUILayout.Label("면적을 줄이고 HDR 밝기를 높이면 작은 광원에 빛이 집중됩니다.", EditorStyles.wordWrappedMiniLabel);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    var rect = GUILayoutUtility.GetRect(340, 620, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    if (output != null) GUI.DrawTexture(rect, output, ScaleMode.ScaleToFit, false);
                }
            }
        }

        private void PreviewShake()
        {
            mode = Mode.잔광;
            if (previewShakes >= 10) return;
            previewShakes++;
            previewFlashAt = EditorApplication.timeSinceStartup;
            Render(); Repaint();
        }
        private void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup - lastFrame < 1d / 30d) return;
            lastFrame = EditorApplication.timeSinceStartup;
            if (playing)
            {
                float duration = mode == Mode.전체_흐름 ? 4.2f : LabYaguangFlashHud.BurstSeconds;
                float elapsed = (float)(EditorApplication.timeSinceStartup - started);
                if (elapsed >= duration)
                {
                    if (loop) { started = EditorApplication.timeSinceStartup; elapsed = 0; }
                    else { playing = false; elapsed = duration; }
                }
                position = mode == Mode.전체_흐름 ? elapsed : elapsed / duration;
            }
            Render(); Repaint(); EditorApplication.QueuePlayerLoopUpdate();
        }
        private void Build()
        {
            if (root != null) return;
            root = new GameObject("Glow Lab Preview") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Glow Lab Output"); cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(10000, 10000, -10);
            previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.cullingMask = 1 << 29;
            output = new RenderTexture(350, 766, 24) { hideFlags = HideFlags.HideAndDontSave, name = "Glow Lab Preview" };
            output.Create(); previewCamera.targetTexture = output;
            var canvasObject = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = previewCamera; canvas.planeDistance = 1f;
            var background = Picture("Backdrop", canvas.transform); background.color = backdrop;
            var lightObject = FullRect("Preview Light", canvas.transform);
            light = lightObject.gameObject.AddComponent<LabYaguangFlashLight>();
            Canvas.ForceUpdateCanvases(); light.Configure(tuning); light.Build();
            if (!light.Ready) return;
            // Layer30 is reserved for this preview; game light rigs use31.
            foreach (var part in light.PrivateRig.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 30;
            var native = light.PrivateRig.GetComponentInChildren<Camera>(); native.cullingMask = 1 << 30;
            native.GetComponent<UniversalAdditionalCameraData>().volumeLayerMask = 1 << 30;
            countImage = Picture("Countdown", canvas.transform); countImage.preserveAspect = true;
            var cueRoot = FullRect("Cue", canvas.transform); cue = cueRoot.gameObject.AddComponent<LabYaguangShakeCue>(); cue.Build();
        }
        private void Render()
        {
            try
            {
                Build(); if (light == null || !light.Ready) { notice = "셰이더 준비 후 다시 열어주세요."; return; }
                root.GetComponentInChildren<Canvas>().transform.Find("Backdrop").GetComponent<Image>().color = backdrop;
                Canvas.ForceUpdateCanvases();
                int number = mode == Mode.예고_3 ? 3 : mode == Mode.예고_2 ? 2 : mode == Mode.예고_1 ? 1 : 0;
                float pulse = mode == Mode.발광_계속보기 ? 0f : mode == Mode.번쩍임 && position < 1 ? position : -1;
                float veil = mode == Mode.잔광 ? .2f : 0;
                if (mode == Mode.잔광) pulse = LabYaguangFlashHud.FlashProgressAt((float)(EditorApplication.timeSinceStartup - previewFlashAt));
                if (mode == Mode.전체_흐름)
                {
                    number = position < 3 ? 3 - Mathf.FloorToInt(position) : 0;
                    float after = position - 3;
                    pulse = LabYaguangFlashHud.FlashProgressAt(after);
                    veil = after >= 0 ? .2f : 0;
                }
                light.Configure(tuning);
                light.Present(LabYaguangFlashHud.WarningStageStrength(number) * .12f, pulse, veil, number, mode == Mode.전체_흐름 || mode == Mode.번쩍임 || mode == Mode.잔광, (mode == Mode.잔광 || mode == Mode.전체_흐름 && number == 0) ? 1f - previewShakes / 10f : 1f);
                var native = light.PrivateRig.GetComponentInChildren<Camera>(); native.enabled = false;
                native.Render();
                countImage.gameObject.SetActive(overlay && number > 0);
                if (number > 0)
                {
                    countImage.sprite = Resources.Load<Sprite>(LabYaguangShakeCue.ArtPath + "Countdown" + number);
                    var rect = countImage.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.sizeDelta = countImage.sprite.rect.size * .5f; rect.anchoredPosition = Vector2.zero;
                }
                cue.gameObject.SetActive(overlay && number == 0);
                if (overlay && number == 0) cue.Present(previewShakes, 10, (float)EditorApplication.timeSinceStartup, 5f);
                foreach (var part in root.GetComponentsInChildren<RectTransform>(true))
                    if (!part.IsChildOf(light.PrivateRig.transform)) part.gameObject.layer = 29;
                Canvas.ForceUpdateCanvases(); previewCamera.Render();
            }
            catch (Exception error) { notice = error.Message; }
        }
        private void SaveGame()
        {
            const string path = "Assets/Lab/Resources/Yaguang/Light/Tuning.asset";
            var saved = AssetDatabase.LoadAssetAtPath<LabYaguangFlashLightProfile>(path);
            if (saved == null)
            { saved = CreateInstance<LabYaguangFlashLightProfile>(); AssetDatabase.CreateAsset(saved, path); }
            Undo.RecordObject(saved, "Save glow tuning"); saved.tuning = tuning.Copy(); EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved);
            foreach (var target in Object.FindObjectsByType<LabYaguangFlashLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (target != light) target.Configure(tuning);
            notice = "게임 기본값을 저장했습니다. 실행 중인 빛에도 반영했습니다.";
        }
        private static RectTransform FullRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        private static Image Picture(string name, Transform parent)
        { var image = FullRect(name, parent).gameObject.AddComponent<Image>(); image.raycastTarget = false; return image; }
        private void Release()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (root != null) DestroyImmediate(root);
            if (output != null) { output.Release(); DestroyImmediate(output); }
            root = null; light = null; output = null;
        }
    }
}
