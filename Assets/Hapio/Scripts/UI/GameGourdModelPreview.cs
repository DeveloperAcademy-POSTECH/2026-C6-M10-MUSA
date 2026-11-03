using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 보여주기용 (#452 preview): 기존 흰 박의 겉모습만 숨기고, 고른 박 모델을 같은 자리에 보여 준다.
    /// - 도색/충돌/히트박스/네트워크는 그대로다. 공은 지금처럼 숨은 흰 박에 맞고 칠해진다(칠은 새 모델에 안 보임).
    /// - 새 모델은 박 계층 밖에 두고 매 프레임 박 자세만 따라간다. 그래서 끝날 때 연출(LabPaintFinishPresentation)이
    ///   새 모델을 복사하지 않고, 그 연출 동안은 새 모델을 숨겨 칠해진 흰 박이 나온다.
    /// - Battle 씬의 GameBattleBindings 옆에 스스로 붙는다(씬/팀원 코드 수정 없음). 설정은 Resources의 GourdModelPreviewSet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameGourdModelPreview : MonoBehaviour
    {
        private GameBattleBindings bindings;
        private GameGourdModelPreviewSet set;
        private GameObject seenVisual, seenPrefab;
        private int seenShow = -1;
        private int inspectorShow = -1;
        private int buttonShow = -1;
        private readonly List<Renderer> hidden = new List<Renderer>();
        private Transform follow;
        private Transform holder;
        private Bounds shellBounds;
        private Vector3 baseLocalPosition, baseLocalScale;
        private Quaternion baseLocalRotation = Quaternion.identity;
        private Animator[] animators = System.Array.Empty<Animator>();
        private float replayAt;
        private GUIStyle buttonStyle;
        private Renderer shell;
        private int seenPaintKey = -1;
        private readonly List<Renderer> overlays = new List<Renderer>();
        private Material overlayMaterial;
        private MaterialPropertyBlock shellBlock, overlayBlock;
        private bool lightingCaptured;
        private int seenLighting = -1;
        private UnityEngine.Rendering.AmbientMode capturedAmbientMode;
        private Color capturedAmbientSky;
        private Light sun;
        private float capturedSunIntensity;
        // #455 이전 Battle 씬 값 (git diff 기준)
        private static readonly Color OldAmbientSky = new Color(.212f, .227f, .259f, 1f);
        private const float OldSunIntensity = 1.35f;
        // 히트박스 배율 (#469): 흰 박 몸통 원점 기준으로 키우고, 떠날 때 원래 크기로 되돌린다.
        private Transform hitbox;
        private Vector3 hitboxBaseScale, hitboxAppliedScale;
        private bool hitboxScaled;
        private const string OverlayShaderPath = "Hapio/GourdPreview/GourdPreviewPaintOverlay";

        public GameObject Instance { get; private set; }
        public int Showing => seenShow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (!scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (GameBattleBindings found in root.GetComponentsInChildren<GameBattleBindings>(true))
                    if (found.GetComponent<GameGourdModelPreview>() == null)
                        found.gameObject.AddComponent<GameGourdModelPreview>();
        }

        private void Awake() => bindings = GetComponent<GameBattleBindings>();
        private void OnEnable() => Application.onBeforeRender += Follow;

        private void OnDisable()
        {
            Application.onBeforeRender -= Follow;
            Release();
        }

        private void OnDestroy()
        {
            Release();
            if (holder != null) Destroy(holder.gameObject);
            if (overlayMaterial != null) Destroy(overlayMaterial);
        }

        private int WantedShow()
        {
            if (set == null) return 0;
            // Inspector에서 숫자를 바꾸면 버튼으로 고른 값보다 Inspector 값을 따른다.
            if (set.show != inspectorShow) { inspectorShow = set.show; buttonShow = -1; }
            int value = buttonShow >= 0 ? buttonShow : set.show;
            return Mathf.Clamp(value, 0, set.Count - 1);
        }

        private bool StageWanted()
        {
            if (set == null) return false;
            if (!set.onlyYaguang) return true;
            GameStageDefinition stage = GameSession.Instance != null ? GameSession.Instance.CurrentStageDefinition : null;
            return stage != null && stage.yokai == LabStageYokai.Yaguang;
        }

        private void LateUpdate()
        {
            if (set == null) set = Resources.Load<GameGourdModelPreviewSet>(GameGourdModelPreviewSet.ResourcesPath);
            ApplyLighting();
            LabTarget target = bindings != null ? bindings.target : null;
            GameObject visual = target != null ? target.MountedVisual : null;
            int show = StageWanted() ? WantedShow() : 0;
            GameGourdModelChoice choice = set != null ? set.ChoiceAt(show) : null;
            GameObject prefab = choice != null ? choice.prefab : null;
            int paintKey = set == null || !set.showPaint ? 0 : set.paintAllParts ? 2 : 1;
            if (visual != seenVisual || prefab != seenPrefab || show != seenShow || paintKey != seenPaintKey)
            {
                Release();
                seenVisual = visual; seenPrefab = prefab; seenShow = show; seenPaintKey = paintKey;
                if (visual != null && prefab != null) Mount(visual, choice);
            }
            if (Instance == null) return;

            // 끝날 때 연출 동안은 새 모델을 숨긴다(연출은 칠해진 흰 박 복사본을 보여 준다).
            var finish = target.GetComponent<LabPaintFinishPresentation>();
            bool finishing = finish != null && (finish.Running || finish.Complete);
            if (Instance.activeSelf == finishing) Instance.SetActive(!finishing);

            Place(choice);
            Follow();
            Replay(choice);
            ApplyHitboxScale(target, choice);
        }

        /// <summary>
        /// 히트박스를 보이는 박과 같은 배율로 맞춘다. LabTarget이 박을 다시 붙이며 원래 자세로 되돌리면
        /// 그 값을 새 기준으로 삼아 다시 키운다. 모든 기기가 같은 스테이지와 설정으로 같은 배율을 쓴다.
        /// </summary>
        private void ApplyHitboxScale(LabTarget target, GameGourdModelChoice choice)
        {
            Transform current = target != null && target.Hitbox != null ? target.Hitbox.transform : null;
            if (current != hitbox) { RestoreHitbox(); hitbox = current; }
            if (hitbox == null) return;
            float factor = set != null && set.matchHitbox && choice != null
                ? Mathf.Max(.01f, choice.scale) * Mathf.Max(.01f, set.gourdScale) : 1f;
            if (!hitboxScaled || hitbox.localScale != hitboxAppliedScale) hitboxBaseScale = hitbox.localScale;
            Vector3 wanted = hitboxBaseScale * factor;
            if (hitbox.localScale != wanted) { hitbox.localScale = wanted; Physics.SyncTransforms(); }
            hitboxAppliedScale = wanted;
            hitboxScaled = true;
        }

        private void RestoreHitbox()
        {
            if (hitbox != null && hitboxScaled && hitbox.localScale == hitboxAppliedScale)
            {
                hitbox.localScale = hitboxBaseScale;
                Physics.SyncTransforms();
            }
            hitboxScaled = false;
        }

        // 조명 비교: 씬에 저장된 새 조명을 처음 한 번 기억해 두고, 끄면 예전 값, 켜면 기억한 값으로 되돌린다.
        private void ApplyLighting()
        {
            if (set == null) return;
            int wanted = set.newLighting ? 1 : 0;
            if (wanted == seenLighting) return;
            if (!lightingCaptured)
            {
                capturedAmbientMode = RenderSettings.ambientMode;
                capturedAmbientSky = RenderSettings.ambientSkyColor;
                sun = FindSun();
                capturedSunIntensity = sun != null ? sun.intensity : 0f;
                lightingCaptured = true;
            }
            if (set.newLighting)
            {
                RenderSettings.ambientMode = capturedAmbientMode;
                RenderSettings.ambientSkyColor = capturedAmbientSky;
                if (sun != null) sun.intensity = capturedSunIntensity;
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientSkyColor = OldAmbientSky;
                if (sun != null) sun.intensity = OldSunIntensity;
            }
            DynamicGI.UpdateEnvironment();
            seenLighting = wanted;
            Debug.Log("[GourdPreview] 조명: " + (set.newLighting ? "새 조명 (#455)" : "예전 조명")
                + (sun != null ? " · 해 " + sun.name + " " + sun.intensity.ToString("0.00") : " · 해를 찾지 못함"));
        }

        private Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            Light best = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional && (best == null || light.intensity > best.intensity)) best = light;
            return best;
        }

        private void Mount(GameObject visual, GameGourdModelChoice choice)
        {
            follow = visual.transform;
            shell = FindShell(follow);
            if (shell == null)
            {
                Debug.LogWarning("[GourdPreview] 'White Gourd' 렌더러를 찾지 못해 기존 박을 그대로 둡니다.");
                return;
            }
            shellBounds = LocalBounds(follow, new[] { shell });

            if (holder == null) holder = new GameObject("GourdPreview · holder").transform;
            SceneManager.MoveGameObjectToScene(holder.gameObject, gameObject.scene);
            holder.gameObject.layer = visual.layer;
            Follow();

            Instance = Instantiate(choice.prefab, holder, false);
            Instance.name = "GourdPreview · " + choice.label;
            Strip(Instance);
            SetLayer(Instance.transform, visual.layer);

            Transform t = Instance.transform;
            Transform source = choice.prefab.transform;
            baseLocalRotation = source.localRotation;
            t.localRotation = baseLocalRotation;
            if (choice.fit == GameGourdModelFit.Authored)
            {
                // 작가가 맞춘 월드 크기를 그대로 쓴다(holder는 박의 크기까지 따라가므로 그만큼 나눈다).
                Vector3 parent = follow.lossyScale;
                Vector3 authored = source.localScale;
                baseLocalScale = new Vector3(authored.x / Safe(parent.x), authored.y / Safe(parent.y), authored.z / Safe(parent.z));
                baseLocalPosition = shellBounds.center;
            }
            else
            {
                t.localPosition = Vector3.zero;
                t.localScale = source.localScale;
                Renderer body = LargestRenderer(Instance);
                if (body == null)
                {
                    Debug.LogWarning("[GourdPreview] 모델에 렌더러가 없어 기존 박을 그대로 둡니다.");
                    Destroy(Instance); Instance = null;
                    return;
                }
                Bounds bodyBounds = LocalBounds(holder, new[] { body });
                baseLocalScale = t.localScale * (shellBounds.size.y / Safe(bodyBounds.size.y));
                t.localScale = baseLocalScale;
                bodyBounds = LocalBounds(holder, new[] { body });
                baseLocalPosition = shellBounds.center - bodyBounds.center;
            }

            // 기존 흰 박(몸통/줄/눈/뚜껑/요괴)은 겉모습만 끈다. 충돌/도색은 그대로 돈다.
            foreach (Renderer renderer in follow.GetComponentsInChildren<Renderer>(true))
                if (!renderer.forceRenderingOff) { renderer.forceRenderingOff = true; hidden.Add(renderer); }

            animators = Instance.GetComponentsInChildren<Animator>(true);
            replayAt = Time.time + choice.replaySeconds;
            Place(choice);

            if (set.showPaint)
            {
                if (set.paintAllParts)
                {
                    foreach (Renderer part in Instance.GetComponentsInChildren<Renderer>(true)) AddOverlay(part);
                }
                else
                {
                    Renderer body = LargestRenderer(Instance);
                    if (body != null) AddOverlay(body);
                    Debug.Log("[GourdPreview] 물감 표시 부품: " + (body != null ? body.name : "없음"));
                }
                UpdateOverlays();
            }
        }

        // 새 모델 부품과 같은 메시를 한 겹 더 그려 물감만 얹는다. 원래 재질은 그대로 둔다.
        private void AddOverlay(Renderer source)
        {
            if (source == null || source is ParticleSystemRenderer) return;
            if (overlayMaterial == null)
            {
                Shader shader = Resources.Load<Shader>(OverlayShaderPath);
                if (shader == null) { Debug.LogWarning("[GourdPreview] 물감 덧그리기 셰이더를 찾지 못했습니다."); return; }
                overlayMaterial = new Material(shader) { name = "GourdPreview · paint overlay" };
            }
            var overlay = new GameObject("GourdPreview · paint overlay");
            overlay.layer = source.gameObject.layer;
            overlay.transform.SetParent(source.transform, false);
            Renderer renderer;
            Mesh mesh;
            if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                var copy = overlay.AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = mesh = skinned.sharedMesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.updateWhenOffscreen = true;
                renderer = copy;
            }
            else if (source is MeshRenderer && source.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            {
                overlay.AddComponent<MeshFilter>().sharedMesh = mesh = filter.sharedMesh;
                renderer = overlay.AddComponent<MeshRenderer>();
            }
            else { Destroy(overlay); return; }
            var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = overlayMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            overlays.Add(renderer);
        }

        // 흰 박이 쓰는 물감 지도와 흰 박의 좌표를 그대로 넘긴다.
        private void UpdateOverlays()
        {
            if (overlays.Count == 0 || shell == null || set == null) return;
            if (shellBlock == null) shellBlock = new MaterialPropertyBlock();
            if (overlayBlock == null) overlayBlock = new MaterialPropertyBlock();
            shell.GetPropertyBlock(shellBlock);
            overlayBlock.Clear();
            bool hasMap = shellBlock.HasTexture("_PaintMap") && shellBlock.GetTexture("_PaintMap") != null;
            if (hasMap) overlayBlock.SetTexture("_PaintMap", shellBlock.GetTexture("_PaintMap"));
            overlayBlock.SetFloat("_PaintMapReady", hasMap && shellBlock.HasFloat("_PaintMapReady") ? shellBlock.GetFloat("_PaintMapReady") : 0f);
            overlayBlock.SetFloat("_PaintStrength", set.paintStrength);
            overlayBlock.SetMatrix("_ShellWorldToObject", shell.transform.worldToLocalMatrix);
            foreach (Renderer overlay in overlays) if (overlay != null) overlay.SetPropertyBlock(overlayBlock);
        }

        private void Place(GameGourdModelChoice choice)
        {
            if (Instance == null || choice == null) return;
            Transform t = Instance.transform;
            float scale = Mathf.Max(.01f, choice.scale) * (set != null ? Mathf.Max(.01f, set.gourdScale) : 1f);
            t.localScale = baseLocalScale * scale;
            t.localRotation = Quaternion.Euler(0f, choice.yaw, 0f) * baseLocalRotation;
            // 배율은 흰 박 가운데를 기준으로 키우고 줄인다.
            t.localPosition = shellBounds.center + (baseLocalPosition - shellBounds.center) * scale + choice.offset;
        }

        // 박은 매달려 흔들리므로 그리기 직전에도 자세를 다시 맞춘다.
        private void Follow()
        {
            if (holder == null || follow == null) return;
            holder.SetPositionAndRotation(follow.position, follow.rotation);
            holder.localScale = follow.lossyScale;
            UpdateOverlays();
        }

        private void Replay(GameGourdModelChoice choice)
        {
            if (choice == null || choice.replaySeconds <= 0f || Time.time < replayAt) return;
            replayAt = Time.time + choice.replaySeconds;
            foreach (Animator animator in animators)
                if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
                    animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
        }

        private void Release()
        {
            RestoreHitbox();
            foreach (Renderer renderer in hidden) if (renderer != null) renderer.forceRenderingOff = false;
            hidden.Clear();
            overlays.Clear();
            shell = null;
            if (Instance != null) { Instance.SetActive(false); Destroy(Instance); }
            Instance = null;
            animators = System.Array.Empty<Animator>();
            follow = null;
            seenVisual = seenPrefab = null;
            seenShow = -1;
            seenPaintKey = -1;
        }

        private void OnGUI()
        {
            if (set == null || !set.showButton || !Debug.isDebugBuild || bindings == null || !StageWanted()) return;
            if (bindings.target == null || bindings.target.MountedVisual == null) return;
            float width = Mathf.Max(160f, Screen.width * .26f);
            float height = Mathf.Max(44f, Screen.height * .045f);
            if (buttonStyle == null) buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = Mathf.RoundToInt(height * .42f);
            Rect safe = Screen.safeArea;
            var rect = new Rect(safe.xMin + 12f, Screen.height * .4f, width, height);
            int show = WantedShow();
            if (GUI.Button(rect, "박: " + set.LabelAt(show) + "  ▶", buttonStyle))
                buttonShow = (show + 1) % set.Count;
        }

        private static Renderer FindShell(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name == "White Gourd") return renderer;
            return null;
        }

        private static Renderer LargestRenderer(GameObject root)
        {
            Renderer best = null;
            float bestVolume = -1f;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Vector3 size = renderer.bounds.size;
                float volume = size.x * size.y * size.z;
                if (volume > bestVolume) { bestVolume = volume; best = renderer; }
            }
            return best;
        }

        private static void Strip(GameObject root)
        {
            // FBX에 딸려 온 카메라/조명/소리 듣기, 물리는 끈다. 애니메이터는 살려 둔다.
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (Light light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; Destroy(body); }
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }

        private static Bounds LocalBounds(Transform space, Renderer[] renderers)
        {
            var result = new Bounds();
            bool any = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                Bounds local = renderer.localBounds;
                Matrix4x4 matrix = space.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                            if (!any) { result = new Bounds(point, Vector3.zero); any = true; }
                            else result.Encapsulate(point);
                        }
            }
            return result;
        }

        private static float Safe(float value) => Mathf.Abs(value) < .00001f ? 1f : value;
    }
}
