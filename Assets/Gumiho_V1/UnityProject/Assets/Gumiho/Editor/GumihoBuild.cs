using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ADA.Gumiho;

public static class GumihoBuild
{
    private const string Root = "Assets/Gumiho";
    private const string Model = Root + "/Models/Gumiho.fbx";
    private static readonly string Delivery = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

    [MenuItem("ADA C6/Gumiho/Rebuild Prefab And Preview")]
    public static void Build()
    {
        foreach (var folder in new[] { "Materials", "Prefabs", "Scenes", "Settings", "Animations" })
            Directory.CreateDirectory(Root + "/" + folder);
        AssetDatabase.Refresh();
        ConfigureTextures();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        if (importer == null) throw new InvalidOperationException("Missing Gumiho.fbx");
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = clip.name.Split('|').Last();
            clip.loopTime = clip.name == "Idle" || clip.name == "Walk_InPlace" || clip.name == "Run_InPlace";
            clip.loopPose = clip.loopTime;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Install Universal RP before rebuilding.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Gumiho_PBR.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Root + "/Materials/Gumiho_PBR.mat");
        }
        material.shader = shader;
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Gumiho_BaseColor.png"));
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Gumiho_MetallicSmoothness.png"));
        material.SetFloat("_Metallic", 1);
        material.SetFloat("_Smoothness", 1);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);

        var root = new GameObject("Gumiho");
        var orientation = new GameObject("Model Orientation");
        orientation.transform.SetParent(root.transform, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        model.transform.SetParent(orientation.transform, false);
        var all = model.GetComponentsInChildren<Transform>();
        var head = all.First(t => t.name == "Head");
        var pelvis = all.First(t => t.name == "Pelvis");
        var front = Vector3.ProjectOnPlane(head.position - pelvis.position, Vector3.up);
        orientation.transform.rotation = Quaternion.FromToRotation(front.normalized, Vector3.forward);
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.sharedMaterials = new[] { material };
            renderer.updateWhenOffscreen = false;
            renderer.quality = SkinQuality.Bone4;
        }
        var animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        var controllerPath = Root + "/Animations/Gumiho.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        foreach (var state in sm.states) sm.RemoveState(state.state);
        var importedClips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__")).ToArray();
        foreach (var clip in importedClips)
        {
            var state = sm.AddState(clip.name);
            state.motion = clip;
            if (clip.name == "Idle") sm.defaultState = state;
        }
        animator.runtimeAnimatorController = controller;
        FitAnimatedBounds(model, importedClips);

        var body = root.AddComponent<CapsuleCollider>();
        body.center = new Vector3(0, 1.0f, 0);
        body.radius = 0.45f;
        body.height = 1.9f;
        body.direction = 2;
        AddWeakPoint(root.transform, pelvis, "TailBack", new Vector3(0, 1.18f, -1.16f), Vector3.back, true);
        AddWeakPoint(root.transform, all.First(t => t.name == "Tassel_L"), "LeftOrnament", new Vector3(-.40f, 1.45f, 1.0f), Vector3.left, true);
        AddWeakPoint(root.transform, all.First(t => t.name == "Tassel_R"), "RightOrnament", new Vector3(.40f, 1.45f, 1.0f), Vector3.right, true);
        AddWeakPoint(root.transform, pelvis, "Underside", new Vector3(0, .05f, 0), Vector3.down, false);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Gumiho.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        BuildScene(saved);
        Validate(saved, importedClips);
        Export();
        Debug.Log("GUMIHO_BUILD_OK " + Delivery);
    }

    public static void Export()
    {
        AssetDatabase.ExportPackage(Root, Path.Combine(Delivery, "Gumiho_URP.unitypackage"), ExportPackageOptions.Recurse);
        Debug.Log("GUMIHO_PACKAGE_OK");
    }

    private static void ConfigureTextures()
    {
        foreach (var path in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) continue;
            importer.sRGBTexture = path.Contains("BaseColor");
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }
    }

    private static void FitAnimatedBounds(GameObject model, AnimationClip[] clips)
    {
        var transforms = model.GetComponentsInChildren<Transform>();
        var positions = transforms.Select(t => t.localPosition).ToArray();
        var rotations = transforms.Select(t => t.localRotation).ToArray();
        var scales = transforms.Select(t => t.localScale).ToArray();
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        var bounds = skin.localBounds;
        foreach (var clip in clips)
        {
            for (int step = 0; step <= 8; step++)
            {
                clip.SampleAnimation(model, clip.length * step / 8f);
                skin.BakeMesh(baked);
                foreach (var vertex in baked.vertices)
                {
                    if (float.IsNaN(vertex.sqrMagnitude) || float.IsInfinity(vertex.sqrMagnitude) || vertex.sqrMagnitude > 1000)
                        throw new Exception("Invalid animated vertex in " + clip.name);
                }
                bounds.Encapsulate(baked.bounds);
            }
        }
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].localPosition = positions[i];
            transforms[i].localRotation = rotations[i];
            transforms[i].localScale = scales[i];
        }
        bounds.Expand(.10f);
        skin.localBounds = bounds;
        UnityEngine.Object.DestroyImmediate(baked);
    }

    private static void AddWeakPoint(Transform root, Transform parent, string name, Vector3 position, Vector3 direction, bool horizontal)
    {
        var child = new GameObject("WeakPoint_" + name);
        child.transform.SetParent(parent, false);
        child.transform.position = root.TransformPoint(position);
        child.AddComponent<SphereCollider>().radius = .10f;
        child.GetComponent<SphereCollider>().isTrigger = true;
        child.AddComponent<GumihoWeakPoint>().Configure(child.transform.InverseTransformDirection(root.TransformDirection(direction)), 50, horizontal);
    }

    private static void BuildScene(GameObject prefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pipelinePath = Root + "/Settings/Gumiho_URP.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, Root + "/Settings/Gumiho_Renderer.asset");
            pipeline = UniversalRenderPipelineAsset.Create(data);
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 15;
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        pipeline.mainLightShadowmapResolution = 2048;
        var pipelineSettings = new SerializedObject(pipeline);
        pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
        pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        QualitySettings.skinWeights = SkinWeights.FourBones;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.56f, .58f, .62f);
        RenderSettings.ambientEquatorColor = new Color(.35f, .35f, .35f);
        RenderSettings.ambientGroundColor = new Color(.22f, .22f, .24f);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.AddComponent<GumihoAnimationDemo>();
        var light = new GameObject("Studio Key").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = .85f;
        light.color = new Color(1, .96f, .92f);
        light.transform.rotation = Quaternion.Euler(48, 145, 0);
        light.shadowBias = .05f;
        light.shadowNormalBias = .5f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = .65f;
        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        var fill = new GameObject("Studio Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = .45f;
        fill.color = new Color(.87f, .92f, 1);
        fill.transform.rotation = Quaternion.Euler(22, -40, 0);
        fill.gameObject.AddComponent<UniversalAdditionalLightData>();
        var frontFill = new GameObject("Face Fill").AddComponent<Light>();
        frontFill.type = LightType.Directional;
        frontFill.intensity = .30f;
        frontFill.transform.rotation = Quaternion.Euler(18, -145, 0);
        frontFill.gameObject.AddComponent<UniversalAdditionalLightData>();
        var volume = new GameObject("Studio Color").AddComponent<Volume>();
        volume.isGlobal = true;
        var profilePath = Root + "/Settings/StudioColor.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tone, profile);
            EditorUtility.SetDirty(profile);
        }
        volume.sharedProfile = profile;
        var preview = new GameObject("Player Views").AddComponent<GumihoSeatPreview>();
        preview.subject = instance.transform;
        preview.cameras = new Camera[5];
        for (int i = 0; i < 5; i++)
        {
            var camera = new GameObject("Player " + (i + 1)).AddComponent<Camera>();
            camera.transform.SetParent(preview.transform, false);
            camera.orthographic = true;
            camera.orthographicSize = 2.12f;
            camera.backgroundColor = new Color(.16f, .16f, .17f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 50f;
            var cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            preview.cameras[i] = camera;
        }
        preview.SendMessage("OnValidate");
        preview.cameras[0].gameObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, Root + "/Scenes/Gumiho_Preview.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene.path, true) };
        AssetDatabase.SaveAssets();
    }

    private static void Validate(GameObject prefab, AnimationClip[] clips)
    {
        var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh == null) throw new Exception("No skinned mesh imported.");
        if (skin.bones.Length != 60) throw new Exception("Unexpected skeleton size: " + skin.bones.Length);
        if (skin.sharedMaterials.Any(m => m == null || m.shader == null)) throw new Exception("Missing material.");
        var requiredClips = new[] { "Idle", "Walk_InPlace", "Run_InPlace", "Howl_Threat", "Pounce_Attack", "Attack_Preview", "Hit_Preview" };
        foreach (var required in requiredClips)
            if (!clips.Any(c => c.name == required)) throw new Exception("Missing authored animation clip: " + required);
        int tails = prefab.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Tail_") && t.name.EndsWith("_1"));
        if (tails != 9) throw new Exception("Expected nine tails, found " + tails);
        var test = new GameObject("WeakPointTest");
        var wp = test.AddComponent<GumihoWeakPoint>();
        wp.Configure(Vector3.back, 50, true);
        if (!wp.IsVisibleFrom(Vector3.back * 5) || wp.IsVisibleFrom(Vector3.forward * 5)) throw new Exception("Directional visibility failed.");
        test.transform.rotation = Quaternion.Euler(0, 90, 0);
        if (!wp.IsVisibleFrom(Vector3.left * 5) || wp.IsVisibleFrom(Vector3.right * 5)) throw new Exception("Rotated visibility failed.");
        UnityEngine.Object.DestroyImmediate(test);
        var bounds = skin.bounds;
        string report = "Unity " + Application.unityVersion + "\n" +
            "Import: PASS\nPrefab: PASS\nMaterials: PASS\nDirectional query: PASS\nAnimation pose bounds (9 samples per clip): PASS\n" +
            "Vertices (split normals / UV seams): " + skin.sharedMesh.vertexCount + "\n" +
            "Triangles: " + (skin.sharedMesh.GetIndexCount(0) / 3) + "\n" +
            "Bones: " + skin.bones.Length + "\nTail chains: " + tails + "\n" +
            "Clips: " + string.Join(", ", clips.Select(c => c.name)) + "\n" +
            "Animated culling bounds: " + bounds.size + "\n" +
            "Reference appearance: requires art approval; not an exact source mesh reconstruction.\n" +
            "iOS device performance: not measured.\n";
        File.WriteAllText(Path.Combine(Delivery, "Unity_Validation.txt"), report);
    }

    public static void RenderPreview()
    {
        EditorSceneManager.OpenScene(Root + "/Scenes/Gumiho_Preview.unity");
        var camera = UnityEngine.Object.FindAnyObjectByType<GumihoSeatPreview>().cameras[0];
        camera.transform.position = new Vector3(2.15f, 4.3f, 8);
        camera.transform.LookAt(new Vector3(0, 1.6f, 0));
        camera.aspect = 1;
        var skin = UnityEngine.Object.FindAnyObjectByType<SkinnedMeshRenderer>();
        var baked = new Mesh();
        skin.BakeMesh(baked);
        var points = baked.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        var right = camera.transform.right;
        var up = camera.transform.up;
        float minX = points.Min(v => Vector3.Dot(v, right));
        float maxX = points.Max(v => Vector3.Dot(v, right));
        float minY = points.Min(v => Vector3.Dot(v, up));
        float maxY = points.Max(v => Vector3.Dot(v, up));
        camera.transform.position += right * ((minX + maxX) * .5f - Vector3.Dot(camera.transform.position, right));
        camera.transform.position += up * ((minY + maxY) * .5f - Vector3.Dot(camera.transform.position, up));
        camera.orthographicSize = Mathf.Max(maxX - minX, maxY - minY) * .56f;
        UnityEngine.Object.DestroyImmediate(baked);
        var rt = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        RenderPipeline.SubmitRenderRequest(camera, request);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(Delivery, "Preview/Unity_URP.png"), image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        Debug.Log("GUMIHO_RENDER_OK");
    }
}
