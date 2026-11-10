using UnityEngine;
using UnityEngine.Rendering;

namespace Hapio
{
    /// <summary>Projects the private result meshes onto the viewer's horizontal floor.</summary>
    internal sealed class GameGourdFloorShadow : MonoBehaviour
    {
        private const int MaskWidth = 256;
        private const int MaskHeight = 128;
        private static readonly int ObjectToFloorClip = Shader.PropertyToID("_ObjectToFloorClip");

        private MeshFilter[] casters;
        private Material material;
        private CommandBuffer commands;
        private MaterialPropertyBlock properties;
        private RenderTexture mask;
        private RenderTexture shadow;
        private Quaternion renderedRotation;
        private bool rendered;
        private bool unavailable;

        private void Awake()
        {
            casters = GetComponentsInChildren<MeshFilter>();
        }

        internal RenderTexture Render()
        {
            if (unavailable) return null;
            if (material == null && !Initialize()) return null;

            Quaternion rotation = transform.localRotation;
            if (rendered && rotation.Equals(renderedRotation) && mask.IsCreated() && shadow.IsCreated())
                return shadow;
            if ((!mask.IsCreated() && !mask.Create()) || (!shadow.IsCreated() && !shadow.Create()))
            {
                unavailable = true;
                return null;
            }

            // Project along a fixed light direction in the preview rig. The second row maps
            // the floor's depth to texture Y, which the UI foreshortens against the backdrop.
            Matrix4x4 projection = Matrix4x4.zero;
            projection.m00 = 1f / 2.1f;
            projection.m01 = .80f / 2.1f;
            projection.m11 = .62f / 2.1f;
            projection.m12 = 1f / 2.1f;
            projection.m33 = 1f;
            Matrix4x4 worldToFloor = projection * transform.parent.worldToLocalMatrix;

            commands.Clear();
            commands.SetRenderTarget(mask);
            commands.ClearRenderTarget(false, true, Color.clear);
            foreach (MeshFilter caster in casters)
            {
                Mesh mesh = caster.sharedMesh;
                if (mesh == null) continue;
                properties.SetMatrix(ObjectToFloorClip, worldToFloor * caster.transform.localToWorldMatrix);
                for (int i = 0; i < mesh.subMeshCount; i++)
                    commands.DrawMesh(mesh, Matrix4x4.identity, material, i, 0, properties);
            }
            commands.Blit(mask, shadow, material, 1);
            RenderTexture previous = RenderTexture.active;
            try { Graphics.ExecuteCommandBuffer(commands); }
            finally { RenderTexture.active = previous; }
            renderedRotation = rotation;
            rendered = true;
            return shadow;
        }

        private bool Initialize()
        {
            Shader shader = Resources.Load<Shader>("Hapio/UI/GourdFloorShadow");
            if (shader == null || !shader.isSupported)
            {
                unavailable = true;
                return false;
            }

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            commands = new CommandBuffer { name = "Sealed gourd floor shadow" };
            properties = new MaterialPropertyBlock();
            mask = CreateTexture("Gourd floor shadow mask");
            shadow = CreateTexture("Gourd floor shadow");
            return true;
        }

        private static RenderTexture CreateTexture(string name)
        {
            return new RenderTexture(MaskWidth, MaskHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void OnDestroy()
        {
            commands?.Release();
            Release(mask);
            Release(shadow);
            if (material != null) Destroy(material);
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }
    }
}
