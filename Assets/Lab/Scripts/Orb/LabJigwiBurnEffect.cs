using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>A disposable presentation; the board retains the real body for a rejected Host action.</summary>
    public sealed class LabJigwiBurnEffect : MonoBehaviour
    {
        private Transform coal;
        private readonly Transform[] flames = new Transform[3];
        private MeshRenderer coalRenderer;
        private float age, radius;
        private LabOrbAppearance appearance;
        private static Material sharedMaterial;
        public const float Duration = .65f;

        public static LabJigwiBurnEffect Play(Transform parent, Vector3 position, float radius,
            LabOrbAppearance appearance = null)
        {
            var root = new GameObject("Jigwi orb burns to ash");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.layer = parent != null ? parent.gameObject.layer : 0;
            var effect = root.AddComponent<LabJigwiBurnEffect>();
            effect.radius = Mathf.Max(.01f, radius);
            effect.appearance = appearance;
            effect.coal = effect.MakeSphere("Burning orb", Color.yellow);
            effect.coalRenderer = effect.coal.GetComponent<MeshRenderer>();
            for (int i = 0; i < effect.flames.Length; i++)
                effect.flames[i] = effect.MakeSphere("Flame " + i,
                    i == 1 ? new Color(1f, .76f, .1f) : new Color(1f, .22f, .025f));
            effect.Pose(0f);
            return effect;
        }

        private Transform MakeSphere(string name, Color color)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.layer = gameObject.layer;
            child.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Tint(renderer, color);
            return child.transform;
        }

        private void Update()
        {
            age += Time.deltaTime;
            Pose(Mathf.Clamp01(age / Duration));
            if (age < Duration) return;
            LabOrbVanishSmoke.Play(transform.parent, transform.position, radius, appearance,
                new Color(.19f, .17f, .16f, .8f));
            Destroy(gameObject);
        }

        private void Pose(float progress)
        {
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, progress);
            coal.localScale = Vector3.one * (radius * 2f * shrink);
            Tint(coalRenderer, Color.Lerp(new Color(1f, .45f, .035f), new Color(.035f, .025f, .02f),
                Mathf.Clamp01(progress * 2.2f)));
            for (int i = 0; i < flames.Length; i++)
            {
                float flicker = .8f + .2f * Mathf.Sin(age * 33f + i * 2f);
                flames[i].localPosition = new Vector3((i - 1) * radius * .5f, radius * .5f, -radius * .4f);
                flames[i].localScale = new Vector3(.65f, 1.6f * flicker, .25f) * (radius * shrink);
                flames[i].localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -22f);
            }
        }

        private static void Tint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        internal static Material Material
        {
            get
            {
                if (sharedMaterial != null) return sharedMaterial;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");
                sharedMaterial = new Material(shader) { name = "Jigwi primitive flame", hideFlags = HideFlags.DontSave };
                return sharedMaterial;
            }
        }
    }
}
