using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// One-time art-only migration for the saved Lab Scene. The Gumiho prefab remains an
    /// ordinary, editable child of Visual Mount; combat still hits the original cylinder.
    /// Re-running never overwrites an existing prefab or a designer's visual placement.
    /// </summary>
    public static class LabGumihoVisualUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ModelPath = "Assets/Lab/Art/Monster/ninetailedfox.fbx";
        private const string MaterialPath = "Assets/Lab/Art/Monster/Gumiho_Mat.Mat";
        private const string PrefabPath = "Assets/Lab/Art/Monster/GumihoVisual.prefab";
        private const string VisualName = "GumihoVisual";
        // Match the legacy gameplay cylinder's 1.8 world-unit height after Awake.
        // The model silhouette is still approximate; the cylinder remains the hitbox.
        private const float DisplayHeight = 1.8f;

        [MenuItem("C6 Lab/Upgrade Saved Gumiho Target Visual")]
        public static void Apply()
        {
            RequireFile(ScenePath);
            RequireFile(ModelPath);
            RequireFile(MaterialPath);

            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (sourceModel == null || material == null || material.shader == null)
                throw new InvalidOperationException("Imported Gumiho FBX or its explicit material is missing.");
            if (!material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") == null)
                throw new InvalidOperationException("Gumiho material has no Base Map texture.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                if (File.Exists(PrefabPath))
                    throw new InvalidOperationException("A non-prefab asset already occupies " + PrefabPath);
                prefab = CreateArtOnlyPrefab(sourceModel, material);
            }
            ValidateArtOnly(prefab, material);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabTarget[] targets = UnityEngine.Object.FindObjectsByType<LabTarget>(FindObjectsSortMode.None);
            if (targets.Length != 1 || targets[0].gameObject.scene != scene)
                throw new InvalidOperationException("Expected exactly one saved LabTarget in the Lab Scene.");
            LabTarget target = targets[0];
            MeshCollider hitbox = target.Hitbox;
            Transform mount = target.VisualMount;
            if (hitbox == null || mount == null || hitbox.sharedMesh == null ||
                hitbox.GetComponent<MeshFilter>() == null ||
                hitbox.GetComponent<MeshRenderer>() == null)
                throw new InvalidOperationException("Saved cylinder hitbox or Visual Mount references are incomplete.");
            Mesh beforeMesh = hitbox.sharedMesh;
            Transform beforeParent = hitbox.transform.parent;
            Vector3 beforeScale = hitbox.transform.localScale;
            bool beforeEnabled = hitbox.enabled;

            Transform existing = mount.Find(VisualName);
            GameObject instance;
            if (existing == null)
            {
                if (mount.childCount != 0)
                    throw new InvalidOperationException("Visual Mount already contains art. Inspect it before migration.");
                instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate Gumiho prefab.");
                instance.transform.SetParent(mount, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }
            else
            {
                instance = existing.gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
                    throw new InvalidOperationException("Existing Gumiho Visual is not linked to the expected prefab.");
            }

            ValidateArtOnly(instance, material);
            MeshRenderer cylinderRenderer = hitbox.GetComponent<MeshRenderer>();
            cylinderRenderer.enabled = false;
            if (target.Hitbox != hitbox || hitbox.sharedMesh != beforeMesh ||
                hitbox.transform.parent != beforeParent ||
                hitbox.transform.localScale != beforeScale || hitbox.enabled != beforeEnabled ||
                hitbox.isTrigger || !hitbox.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Gumiho art migration unexpectedly changed the target hitbox.");

            EditorUtility.SetDirty(cylinderRenderer);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Gumiho Scene integration.");
            AssetDatabase.SaveAssets();
            LogMeshStats(instance, material);
            Debug.Log("C6_GUMIHO_VISUAL_SAVED scene=" + ScenePath +
                " prefab=" + PrefabPath + " hitbox=original-cylinder renderer=hidden");
        }

        private static GameObject CreateArtOnlyPrefab(GameObject sourceModel, Material material)
        {
            GameObject root = new GameObject(VisualName);
            try
            {
                GameObject model = UnityEngine.Object.Instantiate(sourceModel, root.transform, false);
                model.name = "Model Offset";
                if (PrefabUtility.IsPartOfPrefabInstance(model))
                    PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                StripNonVisualComponents(model);
                MeshRenderer[] meshRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
                SkinnedMeshRenderer[] skinnedRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshRenderers.Length + skinnedRenderers.Length == 0)
                    throw new InvalidOperationException("Imported Gumiho FBX contains no renderable mesh.");

                foreach (MeshRenderer renderer in meshRenderers)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        throw new InvalidOperationException("MeshRenderer is missing its mesh: " + renderer.name);
                    AssignMaterial(renderer, material);
                }
                foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
                {
                    if (renderer.sharedMesh == null)
                        throw new InvalidOperationException("SkinnedMeshRenderer is missing its mesh: " + renderer.name);
                    AssignMaterial(renderer, material);
                }

                Bounds bounds = RenderBounds(root);
                if (bounds.size.y <= .0001f)
                    throw new InvalidOperationException("Imported Gumiho mesh has invalid vertical bounds.");
                float scale = DisplayHeight / bounds.size.y;
                model.transform.localScale *= scale;
                bounds = RenderBounds(root);
                model.transform.localPosition -= bounds.center;
                ValidateArtOnly(root, material);

                bool saved;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                if (!saved || prefab == null) throw new IOException("Could not save Gumiho visual prefab.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void StripNonVisualComponents(GameObject root)
        {
            // Preserve all Transforms, including bones, and the mesh renderer/filter pairs.
            // Remove URP camera/light companion scripts first; RequireComponent otherwise
            // prevents Unity from removing the imported Camera and Light components.
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                UnityEngine.Object.DestroyImmediate(behaviour);
            // Source FBX cameras, lights, colliders, and animators never reach the prefab.
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is MeshFilter ||
                    component is MeshRenderer || component is SkinnedMeshRenderer) continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static void AssignMaterial(Renderer renderer, Material material)
        {
            int count = Math.Max(1, renderer.sharedMaterials.Length);
            var materials = new Material[count];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.enabled = true;
        }

        private static void ValidateArtOnly(GameObject visual, Material expectedMaterial)
        {
            foreach (Component component in visual.GetComponentsInChildren<Component>(true))
            {
                if (component is Camera || component is Light || component is Collider ||
                    component is Rigidbody || component is Collider2D ||
                    component is Rigidbody2D || component is Animator)
                    throw new InvalidOperationException("Gumiho visual must not carry " +
                        component.GetType().Name + " on " + component.gameObject.name + ".");
            }
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Gumiho visual has no renderer.");
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    throw new InvalidOperationException("Gumiho prefab contains an unexpected renderer.");
                foreach (Material assigned in renderer.sharedMaterials)
                    if (assigned != expectedMaterial)
                        throw new InvalidOperationException("Gumiho mesh material is not " + MaterialPath);
            }
        }

        private static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No Gumiho mesh bounds.");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void LogMeshStats(GameObject visual, Material material)
        {
            long vertices = 0, triangles = 0;
            int meshCount = 0;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) throw new InvalidOperationException("Renderer lost its mesh: " + renderer.name);
                meshCount++;
                vertices += mesh.vertexCount;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    if (mesh.GetTopology(submesh) == MeshTopology.Triangles)
                        triangles += (long)mesh.GetIndexCount(submesh) / 3;
                Debug.Log("C6_GUMIHO_MESH name=" + renderer.name + " vertices=" + mesh.vertexCount +
                    " submeshes=" + mesh.subMeshCount + " material=" + material.name);
            }
            Bounds bounds = RenderBounds(visual);
            Debug.Log("C6_GUMIHO_STATS renderers=" + meshCount + " vertices=" + vertices +
                " triangles=" + triangles + " bounds=" + bounds.size +
                " center=" + bounds.center + " material=" + material.name);
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required Lab asset is missing.", path);
        }
    }
}
