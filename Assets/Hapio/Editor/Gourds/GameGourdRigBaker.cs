using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>
    /// #507 PR-2: bake the Stage 2 Yagwanggwi art prefab into a battle gourd that follows the gourd model contract
    /// (<see cref="LabGourdVisualRig"/>) and point Stage 2 at it. The art prefab is never changed. Running it again
    /// rewrites the same assets in place (same GUIDs). When the lighter art re-export arrives, run only this again.
    /// Batch: -executeMethod Hapio.Editor.GameGourdRigBaker.BakeFromCommandLine
    /// </summary>
    public static class GameGourdRigBaker
    {
        public const string GeneratedFolder = "Assets/Hapio/Art/Gourds/Stage02_Yagwanggwi/Generated";
        public const string PrefabPath = "Assets/Hapio/Resources/Hapio/Gourds/Stage02_Yagwanggwi_Battle.prefab";
        public const string Resource = "Hapio/Gourds/Stage02_Yagwanggwi_Battle";
        public const string StagePath = "Assets/Hapio/Resources/Hapio/Stages/Stage02_yaguang.asset";
        public const string PaintShellMesh = GeneratedFolder + "/Stage02_PaintShell.asset";
        public const string LidPaintMesh = GeneratedFolder + "/Stage02_LidPaint.asset";
        public const string HitShellMesh = GeneratedFolder + "/Stage02_HitShell.asset";
        public const string ClipPath = GeneratedFolder + "/Stage02_YokaiPeek.anim";
        public const string ProfilePath = GeneratedFolder + "/Stage02_YokaiProfile.asset";
        public const string BodyMaterialPath = GeneratedFolder + "/Stage02_BodyPaint.mat";
        public const string LidMaterialPath = GeneratedFolder + "/Stage02_LidPaint.mat";
        public const string BodyArt = "Assets/Hapio/Art/Gourds/Stage02_Yagwanggwi/Textures/model_part0_BaseColor.png";
        public const string LidArt = "Assets/Hapio/Art/Gourds/Stage02_Yagwanggwi/Textures/Image_0.png";
        public const string LogPath = GeneratedFolder + "/BakeReport.txt";

        [MenuItem("Hapio/Gourds/Bake Stage 2 Yagwanggwi Gourd")]
        public static void Bake()
        {
            GameGourdRigBake.Result result = null;
            GameObject art = null, battle = null;
            try
            {
                Folder(GeneratedFolder);
                Folder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(GameGourdRigBake.SourcePrefab);
                art = Object.Instantiate(source);
                art.name = source.name;
                result = GameGourdRigBake.Compute(art);

                Mesh paintShell = Save(result.PaintShell, PaintShellMesh);
                Mesh lidPaint = Save(result.LidPaint, LidPaintMesh);
                Mesh hitShell = Save(result.HitShell, HitShellMesh);
                AnimationClip clip = Save(result.Clip, ClipPath);
                LabGourdYokaiProfile profile = SaveProfile(result);
                Material bodyMaterial = SaveMaterial(BodyMaterialPath, "Stage02 Yagwanggwi body paint", BodyArt);
                Material lidMaterial = SaveMaterial(LidMaterialPath, "Stage02 Yagwanggwi lid paint", LidArt);

                battle = Assemble(art, result, paintShell, lidPaint, hitShell, clip, profile, bodyMaterial, lidMaterial);
                art = null; // moved into the battle root
                var rig = battle.GetComponent<LabGourdVisualRig>();
                if (!rig.Validate(out string reason)) throw new InvalidOperationException("The baked rig is invalid: " + reason);
                PrefabUtility.SaveAsPrefabAsset(battle, PrefabPath);
                LinkStage();
                string report = Report(battle, result);
                File.WriteAllText(LogPath, report);
                AssetDatabase.ImportAsset(LogPath);
                AssetDatabase.SaveAssets();
                Debug.Log("HAPIO_GOURD_BAKED " + report.Replace('\n', ' '));
            }
            finally
            {
                if (battle != null) Object.DestroyImmediate(battle);
                if (art != null) Object.DestroyImmediate(art);
                if (result != null) result.Destroy();
            }
        }

        public static void BakeFromCommandLine()
        {
            try { Bake(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static GameObject Assemble(GameObject art, GameGourdRigBake.Result result, Mesh paintShell, Mesh lidPaint,
            Mesh hitShell, AnimationClip clip, LabGourdYokaiProfile profile, Material bodyMaterial, Material lidMaterial)
        {
            var battle = new GameObject("Stage02_Yagwanggwi_Battle");
            Transform model = art.transform.Find(GameGourdRigBake.ModelPath);
            Transform hinge = model.Find(GameGourdRigBake.LidHingePath);

            // The scaled root moves into the model so the battle root stays at scale 1.
            model.SetParent(battle.transform, true);
            Object.DestroyImmediate(art);

            Object.DestroyImmediate(model.Find(GameGourdRigBake.BodyPath).gameObject);
            Object.DestroyImmediate(model.Find(GameGourdRigBake.LidPath).gameObject);
            foreach (string part in GameGourdRigBake.ModelRopeParts)
            {
                Transform rope = model.Find(part);
                if (rope != null) Object.DestroyImmediate(rope.gameObject);
            }
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

        private static void LinkStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<GameStageDefinition>(StagePath);
            if (stage == null) throw new InvalidOperationException("Missing " + StagePath);
            var serialized = new SerializedObject(stage);
            SerializedProperty property = serialized.FindProperty(nameof(GameStageDefinition.gourdVisualResource));
            if (property.stringValue == Resource) return;
            property.stringValue = Resource;
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
            profile.bodyPath = GameGourdRigBake.YokaiBodyPath;
            profile.hopAxis = Vector3.forward; // the eye control's local Z points out of the opening
            profile.hop = .00005f;             // about 0.1 world units under the model's 120 x 17 scale
            profile.idleLoop = false;
            if (!profile.IsValid(out string reason)) throw new InvalidOperationException("Extracted timeline invalid: " + reason);
            LabGourdYokaiProfile saved = Save(profile, ProfilePath);
            Object.DestroyImmediate(profile);
            return saved;
        }

        private static Material SaveMaterial(string path, string name, string artPath)
        {
            var white = Resources.Load<Material>("GourdPaint/WhiteGourd");
            var material = new Material(white) { name = name };
            material.SetTexture("_UnderpaintTex", AssetDatabase.LoadAssetAtPath<Texture2D>(artPath));
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
            foreach (MeshRenderer renderer in battle.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) triangles += (int)mesh.GetIndexCount(s) / 3;
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
