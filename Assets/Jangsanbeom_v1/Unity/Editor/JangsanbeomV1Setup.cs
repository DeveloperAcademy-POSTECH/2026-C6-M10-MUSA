using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Copy the Jangsanbeom folder beneath Assets, then run this menu once.
public class JangsanbeomV1Importer : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (!assetPath.Contains("/Jangsanbeom_v1/") || !assetPath.EndsWith(".fbx")) return;
        var m = (ModelImporter)assetImporter;
        m.animationType = ModelImporterAnimationType.Generic;
        m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        m.importAnimation = true;
        m.importCameras = false; m.importLights = false;
        m.importBlendShapes = false;
        m.isReadable = false;
        m.animationCompression = ModelImporterAnimationCompression.Off;
        m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    void OnPreprocessAnimation()
    {
        if (!assetPath.Contains("/Jangsanbeom_v1/") || !assetPath.EndsWith(".fbx")) return;
        var m = (ModelImporter)assetImporter;
        var clips = m.defaultClipAnimations;
        foreach (var c in clips)
        {
            var f = Path.GetFileNameWithoutExtension(assetPath);
            c.name = f.Contains("@") ? f.Split('@').Last() : c.name.Split('|').Last();
            c.loopTime = c.name == "Idle" || c.name == "Move" || c.name == "Stunned";
            c.loopPose = c.loopTime;
            c.keepOriginalPositionXZ = true; c.keepOriginalPositionY = true;
            c.keepOriginalOrientation = true;
            c.lockRootPositionXZ = true; c.lockRootHeightY = true; c.lockRootRotation = true;
        }
        m.clipAnimations = clips;
    }
}

public static class JangsanbeomV1Setup
{
    public static void ValidateAndRender() { ValidateBatch(); RenderPreview(); CreatePreviewScene(); string p=Environment.GetEnvironmentVariable("JANGSAN_PACKAGE"); if(!string.IsNullOrEmpty(p)) AssetDatabase.ExportPackage("Assets/Jangsanbeom_v1",p,ExportPackageOptions.Recurse); }
    [MenuItem("Tools/Jangsanbeom v1/Create Materials and Prefab")]
    public static void Build()
    {
        string main = "Assets/Jangsanbeom_v1/Jangsanbeom.fbx";
        string root = Path.GetDirectoryName(main).Replace('\\', '/');
        string gen = root + "/Unity/Generated";
        Directory.CreateDirectory(gen); AssetDatabase.Refresh();
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader shader = pipeline != null && pipeline.GetType().Name.Contains("Universal") ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (shader == null) throw new Exception("Standard or URP Lit shader required.");
        Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(root + "/Textures/Jangsanbeom_Palette.png");
        string[] names = {"Ivory","Ivory_Light","Ivory_Shadow","Inner_Charcoal","Face_Bone","Crimson"};
        var materials = new Dictionary<string, Material>();
        foreach (string n in names)
        {
            string path = gen + "/" + n + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {mat = new Material(shader); AssetDatabase.CreateAsset(mat, path);}
            mat.shader = shader;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", n == "Crimson" ? .58f : .12f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", n == "Crimson" ? .58f : .12f);
            materials[n] = mat; EditorUtility.SetDirty(mat);
        }
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(main);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        go.name = "Jangsanbeom";
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => materials.ContainsKey(m.name) ? materials[m.name] : materials["Ivory"]).ToArray();
            // 9/17(목) 업데이트:  Root 본의 FBX 배율(100)과 상관없이 실제 뼈 위치로 표시 범위를 계산
            r.updateWhenOffscreen = true;
        }
        string controllerPath = gen + "/Jangsanbeom.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        foreach (var s in sm.states) sm.RemoveState(s.state);
        var clips = AssetDatabase.LoadAllAssetsAtPath(main).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        for (int i=0;i<clips.Length;i++)
        {
            var state = sm.AddState(clips[i].name, new Vector3(260*(i%3),100*(i/3),0));
            state.motion = clips[i];
            if(clips[i].name=="Idle") sm.defaultState=state;
        }
        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        PrefabUtility.SaveAsPrefabAsset(go, gen + "/Jangsanbeom.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Debug.Log("Jangsanbeom: materials, Generic Animator, 10 states and prefab created.");
    }

    public static void ValidateBatch()
    {
        Build();
        string path = "Assets/Jangsanbeom_v1/Jangsanbeom.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        string[] expected = {"Idle","Call_Lure","Move","Ambush","Claw_Attack","Grab","Vanish","Hit","Stunned","Defeated"};
        if (clips.Length != 10 || expected.Any(n=>!clips.Any(c=>c.name==n))) throw new Exception("Missing animation clips.");
        var avatar = model.GetComponent<Animator>().avatar;
        if (avatar == null || !avatar.isValid || avatar.isHuman) throw new Exception("Invalid Generic avatar.");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var report = new List<string> {"Unity " + Application.unityVersion, "Generic avatar: valid; non-humanoid", "Animation clips: 10"};
        foreach (var clip in clips)
        {
            clip.SampleAnimation(go, clip.length * .5f);
            foreach(var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh(); sk.BakeMesh(baked);
                if (baked.vertices.Any(v => float.IsNaN(v.x) || float.IsInfinity(v.x))) throw new Exception("Invalid deformed vertices.");
                UnityEngine.Object.DestroyImmediate(baked);
            }
            report.Add(clip.name + " | " + clip.length.ToString("F3") + " sec | curves=" + AnimationUtility.GetCurveBindings(clip).Length);
        }
        UnityEngine.Object.DestroyImmediate(go);
        string reportPath = Environment.GetEnvironmentVariable("JANGSAN_REPORT");
        if (!string.IsNullOrEmpty(reportPath)) File.WriteAllLines(reportPath,report);
        string packagePath = Environment.GetEnvironmentVariable("JANGSAN_PACKAGE");
        if (!string.IsNullOrEmpty(packagePath)) AssetDatabase.ExportPackage("Assets/Jangsanbeom_v1",packagePath,ExportPackageOptions.Recurse);
        Debug.Log("JANGSANBEOM_VALIDATION_PASSED\n"+string.Join("\n",report));
    }

    public static void RenderPreview()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jangsanbeom_v1/Unity/Generated/Jangsanbeom.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var a = go.GetComponent<Animator>(); a.enabled = false;
        var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Jangsanbeom_v1/Jangsanbeom.fbx").OfType<AnimationClip>().First(c=>c.name=="Idle");
        idle.SampleAnimation(go,0);
        var cameraGo = new GameObject("Preview Camera"); var cam = cameraGo.AddComponent<Camera>();
        cameraGo.transform.position = new Vector3(4,3.0f,7);
        cameraGo.transform.LookAt(new Vector3(0,1.4f,0));
        cam.orthographic=true;cam.orthographicSize=1.7f;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.12f,.15f);
        RenderSettings.ambientLight=new Color(.60f,.60f,.60f);
        var lightGo = new GameObject("Preview Light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;lightGo.transform.rotation=Quaternion.Euler(35,-40,0);
        var rt = new RenderTexture(680,800,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var tex=new Texture2D(680,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,680,800),0,0);tex.Apply();
        File.WriteAllBytes(Environment.GetEnvironmentVariable("JANGSAN_PREVIEW"),tex.EncodeToPNG());
        cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cameraGo);UnityEngine.Object.DestroyImmediate(lightGo);
        Debug.Log("JANGSANBEOM_PREVIEW_RENDERED");
    }

    [MenuItem("Tools/Jangsanbeom v1/Create Preview Scene")]
    public static void CreatePreviewScene()
    {
        if(!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jangsanbeom_v1/Unity/Generated/Jangsanbeom.prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var control=new GameObject("Animation Buttons").AddComponent<JangsanbeomV1Preview>();control.character=go.GetComponent<Animator>();
        var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=new Vector3(4,3.0f,7);cam.transform.LookAt(new Vector3(0,1.35f,0));cam.orthographic=true;cam.orthographicSize=1.85f;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.12f,.15f);
        cam.gameObject.AddComponent<AudioListener>();
        var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=new Color(1,.89f,.79f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(40,-40,0);
        var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.color=new Color(.68f,.78f,1);fill.transform.rotation=Quaternion.Euler(30,135,0);
        RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Preview Floor";floor.transform.position=new Vector3(0,-.04f,0);
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var shader=pipeline!=null && pipeline.GetType().Name.Contains("Universal")?Shader.Find("Universal Render Pipeline/Lit"):Shader.Find("Standard");
        string matPath="Assets/Jangsanbeom_v1/Unity/Generated/PreviewFloor.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}mat.shader=shader;mat.color=new Color(.15f,.17f,.20f);floor.GetComponent<Renderer>().sharedMaterial=mat;
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Assets/Jangsanbeom_v1/Unity/Generated/Jangsanbeom_Preview.unity");
        AssetDatabase.SaveAssets();Debug.Log("JANGSANBEOM_PREVIEW_SCENE_READY");
    }
}
