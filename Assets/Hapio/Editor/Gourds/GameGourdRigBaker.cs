using System;
using System.Collections.Generic;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>
    /// #507: bake each stage's art gourd into a battle gourd that follows the gourd model contract
    /// (<see cref="LabGourdVisualRig"/>) and point that stage at it. The art prefabs are never changed. Running it again
    /// rewrites the same assets in place (same GUIDs). When a lighter art re-export arrives, run only this again.
    /// Batch: -executeMethod Hapio.Editor.GameGourdRigBaker.BakeAllFromCommandLine
    /// </summary>
    public static class GameGourdRigBaker
    {
        [MenuItem("Hapio/Gourds/Bake Stage 1 Gumiho Gourd")]
        public static void BakeStage1() => Bake(GameGourdRigBake.Stage1);

        [MenuItem("Hapio/Gourds/Bake Stage 2 Yagwanggwi Gourd")]
        public static void BakeStage2() => Bake(GameGourdRigBake.Stage2);

        [MenuItem("Hapio/Gourds/Bake Stage 3 Jigwi Gourd")]
        public static void BakeStage3() => Bake(GameGourdRigBake.Stage3);

        [MenuItem("Hapio/Gourds/Bake All Stage Gourds")]
        public static void BakeAll()
        {
            foreach (GameGourdRigBake.Spec spec in GameGourdRigBake.All) Bake(spec);
        }

        public static void BakeAllFromCommandLine()
        {
            try { BakeAll(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Bake(GameGourdRigBake.Spec spec)
        {
            GameGourdRigBake.Result result = null;
            GameObject art = null, battle = null;
            try
            {
                Folder(spec.GeneratedFolder);
                Folder(Path.GetDirectoryName(spec.PrefabPath).Replace('\\', '/'));
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.SourcePrefab);
                if (source == null) throw new InvalidOperationException("Missing art prefab " + spec.SourcePrefab);
                art = Object.Instantiate(source);
                art.name = source.name;
                result = GameGourdRigBake.Compute(spec, art);

                Mesh paintShell = Save(result.PaintShell, spec.Generated("PaintShell.asset"));
                Mesh lidPaint = Save(result.LidPaint, spec.Generated("LidPaint.asset"));
                Mesh hitShell = Save(result.HitShell, spec.Generated("HitShell.asset"));
                AnimationClip clip = Save(result.Clip, spec.Generated("YokaiPeek.anim"));
                LabGourdYokaiProfile profile = SaveProfile(result);
                Material bodyMaterial = SaveMaterial(spec.Generated("BodyPaint.mat"), spec.Label + " body paint", result.BodyArt);
                Material lidMaterial = SaveMaterial(spec.Generated("LidPaint.mat"), spec.Label + " lid paint", result.LidArt);

                battle = Assemble(art, result, paintShell, lidPaint, hitShell, clip, profile, bodyMaterial, lidMaterial);
                art = null; // moved into the battle root
                var rig = battle.GetComponent<LabGourdVisualRig>();
                if (!rig.Validate(out string reason)) throw new InvalidOperationException("The baked rig is invalid: " + reason);
                PrefabUtility.SaveAsPrefabAsset(battle, spec.PrefabPath);
                LinkStage(spec);
                string report = Report(battle, result);
                string reportPath = spec.GeneratedFolder + "/BakeReport.txt";
                File.WriteAllText(reportPath, report);
                AssetDatabase.ImportAsset(reportPath);
                AssetDatabase.SaveAssets();
                Debug.Log("HAPIO_GOURD_BAKED stage=" + spec.Stage + " " + report.Replace('\n', ' '));
            }
            finally
            {
                if (battle != null) Object.DestroyImmediate(battle);
                if (art != null) Object.DestroyImmediate(art);
                if (result != null) result.Destroy();
            }
        }

        private static GameObject Assemble(GameObject art, GameGourdRigBake.Result result, Mesh paintShell, Mesh lidPaint,
            Mesh hitShell, AnimationClip clip, LabGourdYokaiProfile profile, Material bodyMaterial, Material lidMaterial)
        {
            GameGourdRigBake.Spec spec = result.Spec;
            var battle = new GameObject(spec.Id + "_Battle");
            Transform model = art.transform.Find(spec.ModelName);
            Transform hinge = model.Find(result.LidHingePath);

            // The scaled root moves into the model so the battle root stays at scale 1.
            model.SetParent(battle.transform, true);
            Object.DestroyImmediate(art);

            Object.DestroyImmediate(model.Find(result.BodyPath).gameObject);
            Object.DestroyImmediate(model.Find(result.LidPath).gameObject);
            foreach (Transform rope in RopeParts(model)) Object.DestroyImmediate(rope.gameObject);
            Object.DestroyImmediate(model.GetComponent<Animator>());
            var animation = model.gameObject.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;

            var shell = new GameObject("Paint Shell", typeof(MeshFilter), typeof(MeshRenderer));
            shell.transform.SetParent(battle.transform, false);
            shell.transform.SetPositionAndRotation(result.PaintOrigin, result.PaintRotation);
            shell.GetComponent<MeshFilter>().sharedMesh = paintShell;
            var shellRenderer = shell.GetComponent<MeshRenderer>();
            shellRenderer.sharedMaterial = bodyMaterial;

            var hit = new GameObject("Hit Shell", typeof(MeshFilter));
            hit.transform.SetParent(shell.transform, false);
            hit.GetComponent<MeshFilter>().sharedMesh = hitShell;

            // The lid's paint keeps paint space while closed and opens with the hinge.
            var lid = new GameObject("Lid Paint", typeof(MeshFilter), typeof(MeshRenderer));
            lid.transform.SetParent(hinge, false);
            lid.transform.SetPositionAndRotation(result.PaintOrigin, result.PaintRotation);
            lid.transform.localScale = Vector3.one / hinge.lossyScale.x;
            lid.GetComponent<MeshFilter>().sharedMesh = lidPaint;
            var lidRenderer = lid.GetComponent<MeshRenderer>();
            lidRenderer.sharedMaterial = lidMaterial;

            var socket = new GameObject("Rope Socket").transform;
            socket.SetParent(battle.transform, false);
            socket.position = result.RopeSocket;
            var anchor = new GameObject("Size Anchor").transform;
            anchor.SetParent(battle.transform, false);
            anchor.position = result.SizeAnchor;

            var rig = battle.AddComponent<LabGourdVisualRig>();
            rig.paintShell = shellRenderer;
            rig.paintExtras = new Renderer[] { lidRenderer };
            rig.hitShell = hit.GetComponent<MeshFilter>();
            rig.paintMaterial = bodyMaterial;
            rig.ropeSocket = socket;
            rig.referenceVisualSize = GameGourdRigBake.ReferenceVisualSize;
            rig.sizeAnchor = anchor;
            rig.yokaiAnimation = animation;
            rig.yokaiProfile = profile;
            return battle;
        }

        /// <summary>The model's own hanging rope parts, whatever their duplicate suffix.</summary>
        private static List<Transform> RopeParts(Transform model)
        {
            var parts = new List<Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(GameGourdRigBake.ModelRopeParts, GameGourdRigBake.BaseName(t.name)) >= 0) parts.Add(t);
            return parts;
        }

        private static void LinkStage(GameGourdRigBake.Spec spec)
        {
            var stage = AssetDatabase.LoadAssetAtPath<GameStageDefinition>(spec.StageAsset);
            if (stage == null) throw new InvalidOperationException("Missing " + spec.StageAsset);
            var serialized = new SerializedObject(stage);
            SerializedProperty property = serialized.FindProperty(nameof(GameStageDefinition.gourdVisualResource));
            if (property.stringValue == spec.Resource) return;
            property.stringValue = spec.Resource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(stage);
        }

        private static LabGourdYokaiProfile SaveProfile(GameGourdRigBake.Result result)
        {
            var profile = ScriptableObject.CreateInstance<LabGourdYokaiProfile>();
            profile.lidOpensAt = result.LidOpensAt; profile.risenAt = result.RisenAt;
            profile.lookFrom = result.LookFrom; profile.lookTo = result.LookTo;
            profile.eyesBackAt = result.EyesBackAt; profile.sinkFrom = result.SinkFrom;
            profile.sunkAt = result.SunkAt; profile.lidClosedAt = result.LidClosedAt;
            profile.clipLength = result.ClipLength;
            profile.bodyPath = result.YokaiBodyPath;
            profile.hopAxis = Vector3.forward; // the eye control's local Z points out of the opening
            profile.hop = .00005f;             // about 0.1 world units under the model's 120 x 17 scale
            profile.idleLoop = false;
            if (!profile.IsValid(out string reason)) throw new InvalidOperationException("Extracted timeline invalid: " + reason);
            LabGourdYokaiProfile saved = Save(profile, result.Spec.Generated("YokaiProfile.asset"));
            Object.DestroyImmediate(profile);
            return saved;
        }

        private static Material SaveMaterial(string path, string name, Texture art)
        {
            var white = Resources.Load<Material>("GourdPaint/WhiteGourd");
            var material = new Material(white) { name = name };
            material.SetTexture("_UnderpaintTex", art);
            material.SetFloat("_UnderpaintMap", 1f);
            material.EnableKeyword("_UNDERPAINT_MAP");
            Material saved = Save(material, path);
            Object.DestroyImmediate(material);
            return saved;
        }

        /// <summary>Create the asset, or overwrite the existing one in place so its GUID and references stay.</summary>
        private static T Save<T>(T built, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(Object.Instantiate(built), path);
                existing = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            else
            {
                string name = existing.name;
                EditorUtility.CopySerialized(built, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
            }
            existing.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        private static string Report(GameObject battle, GameGourdRigBake.Result result)
        {
            int renderers = 0, triangles = 0;
            foreach (Renderer renderer in battle.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                renderers++;
                for (int s = 0; s < mesh.subMeshCount; s++) triangles += (int)mesh.GetIndexCount(s) / 3;
            }
            bool animationAboveShell = false;
            var rig = battle.GetComponent<LabGourdVisualRig>();
            for (Transform t = rig.hitShell.transform; t != null; t = t.parent)
                if (t.GetComponent<Animation>() != null || t.GetComponent<Animator>() != null) animationAboveShell = true;
            return result.Log
                + "renderers=" + renderers + " triangles=" + triangles + "\n"
                + "animationAboveHitShell=" + animationAboveShell + "\n";
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
