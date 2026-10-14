using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// Installs the hit-only particle artwork into the saved Lab scene. Existing artwork and
    /// Inspector assignments are left alone on subsequent runs; this is not a scene rebuild.
    /// </summary>
    public static class LabImpactParticleVfxUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ArtFolder = "Assets/Lab/Art/Effects";
        private const string PrefabPath = ArtFolder + "/ImpactParticleBurst.prefab";
        private const string DiscPath = ArtFolder + "/ImpactSoftDisc.asset";
        private const string RingPath = ArtFolder + "/ImpactSoftRing.asset";
        private const string DiscMaterialPath = ArtFolder + "/ImpactSoftDisc.mat";
        private const string RingMaterialPath = ArtFolder + "/ImpactSoftRing.mat";

        [MenuItem("C6 Lab/Install Impact Particle VFX")]
        public static void Apply()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Saved Lab Scene is missing.", ScenePath);
            EnsureFolder("Assets/Lab/Art", "Effects");

            Texture2D disc = EnsureTexture(DiscPath, false);
            Texture2D ring = EnsureTexture(RingPath, true);
            Material discMaterial = EnsureMaterial(DiscMaterialPath, disc);
            Material ringMaterial = EnsureMaterial(RingMaterialPath, ring);
            EnsurePrefab(discMaterial, ringMaterial);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Opening a scene may unload the prefab object created moments earlier. Resolve
            // the persistent asset again immediately before serializing its reference.
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            LabImpactParticleBurst burst = prefabAsset == null
                ? null : prefabAsset.GetComponent<LabImpactParticleBurst>();
            if (burst == null) throw new InvalidOperationException("Saved impact prefab is missing its Burst component.");
            Camera battleCamera = FindInScene(scene, "BattleCamera")?.GetComponent<Camera>();
            LabImpactFeedback feedback = FindInScene(scene, "LabCanvas")?.GetComponent<LabImpactFeedback>();
            if (battleCamera == null || feedback == null)
                throw new InvalidOperationException("The saved BattleCamera or LabCanvas hit feedback is missing.");

            GameObject root = FindInScene(scene, "Impact Particle VFX");
            if (root == null) root = new GameObject("Impact Particle VFX");
            LabImpactParticleVfx manager = root.GetComponent<LabImpactParticleVfx>();
            if (manager == null) manager = root.AddComponent<LabImpactParticleVfx>();
            // No collider, UI raycaster, or Rigidbody is added. The visual never affects hit
            // authority or orb input in either of the scene's camera viewports.
            AssignIfNull(manager, "effectPrefab", burst);
            AssignIfNull(manager, "battleCamera", battleCamera);
            AssignIfNull(feedback, "particleVfx", manager);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(feedback);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the Lab impact scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_IMPACT_PARTICLE_VFX_APPLIED scene=" + ScenePath + " prefab=" + PrefabPath);
        }

        private static LabImpactParticleBurst EnsurePrefab(Material disc, Material ring)
        {
            GameObject existing = LoadExpectedAsset<GameObject>(PrefabPath);
            if (existing != null)
            {
                LabImpactParticleBurst savedBurst = existing.GetComponent<LabImpactParticleBurst>();
                if (savedBurst == null)
                    throw new InvalidOperationException("Existing impact prefab has no LabImpactParticleBurst component.");
                return savedBurst;
            }

            var root = new GameObject("ImpactParticleBurst", typeof(LabImpactParticleBurst));
            try
            {
                LabImpactParticleBurst component = root.GetComponent<LabImpactParticleBurst>();
                ParticleSystem core = AddParticle(root.transform, "Core Flash", disc,
                    1, .16f, .62f, 0f, false, false);
                ParticleSystem sparks = AddParticle(root.transform, "Element Sparks", disc,
                    12, .30f, .14f, 2.35f, true, false);
                ParticleSystem shockwave = AddParticle(root.transform, "Expanding Shockwave", ring,
                    1, .34f, 1.45f, 0f, false, true);
                AssignIfNull(component, "core", core);
                AssignIfNull(component, "sparks", sparks);
                AssignIfNull(component, "ring", shockwave);

                // Deliberately no Collider or Rigidbody anywhere in this prefab.
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new IOException("Could not save the impact particle prefab.");
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
                GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (persistent == null) throw new IOException("Could not reload the saved impact prefab.");
                return persistent.GetComponent<LabImpactParticleBurst>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem AddParticle(Transform parent, string name, Material material,
            short count, float lifetime, float size, float speed, bool sparks, bool shockwave)
        {
            var child = new GameObject(name, typeof(ParticleSystem));
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.GetComponent<ParticleSystem>();
            var main = system.main;
            main.duration = .45f;
            main.loop = false;
            main.playOnAwake = false;
            main.startDelay = 0f;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startSpeed = speed;
            main.startColor = Color.white;
            main.simulationSpace = sparks ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = system.shape;
            shape.enabled = sparks;
            if (sparks)
            {
                // Burst rotates this cone away from the incoming projectile, so the
                // approved impact velocity changes the visible spark direction.
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 42f;
                shape.radius = .045f;
            }

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(sparks ? .9f : .75f, .2f),
                    new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                shockwave ? new AnimationCurve(new Keyframe(0f, .12f), new Keyframe(.28f, .68f),
                    new Keyframe(1f, 1.12f))
                    : sparks ? new AnimationCurve(new Keyframe(0f, .7f), new Keyframe(.22f, 1f),
                        new Keyframe(1f, 0f))
                    : new AnimationCurve(new Keyframe(0f, .4f), new Keyframe(.16f, 1f),
                        new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = sparks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (sparks)
            {
                renderer.velocityScale = .24f;
                renderer.lengthScale = 1.8f;
            }
            renderer.sortingOrder = shockwave ? 0 : sparks ? 1 : 2;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static Texture2D EnsureTexture(string path, bool ring)
        {
            Texture2D existing = LoadExpectedAsset<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(path),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f - size * .5f) / (size * .5f);
                float dy = (y + .5f - size * .5f) / (size * .5f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = ring
                    ? Mathf.Clamp01((radius - .55f) / .08f) * Mathf.Clamp01((.82f - radius) / .13f)
                    : Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Material EnsureMaterial(string path, Texture2D texture)
        {
            Material existing = LoadExpectedAsset<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                throw new InvalidOperationException("URP Particles/Unlit shader is required for impact VFX.");
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (AssetDatabase.IsValidFolder(path)) return;
            if (!AssetDatabase.IsValidFolder(parent)) throw new DirectoryNotFoundException(parent);
            AssetDatabase.CreateFolder(parent, child);
        }

        private static T LoadExpectedAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Another asset type already occupies " + path);
            return asset;
        }

        private static void AssignIfNull(UnityEngine.Object owner, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty field = serialized.FindProperty(property);
            if (field == null)
                throw new InvalidOperationException(owner.GetType().Name + "." + property + " is missing.");
            if (field.objectReferenceValue != null) return;
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }
    }
}
