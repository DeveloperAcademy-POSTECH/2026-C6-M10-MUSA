using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>Private HDR emitter + native URP Bloom, additively composited below legible UI.</summary>
    public sealed class LabYaguangFlashLight : MonoBehaviour
    {
        private const int PrivateLayer = 31;
        private GameObject rig;
        private Camera lightCamera;
        private Material emission, composite;
        private RawImage display;
        private RenderTexture lightTexture;
        private Volume volume;
        private VolumeProfile profile;
        private Bloom bloom;
        private Transform emitter;
        private float warning, flash;
        private LabYaguangFlashTuning tuning;
        public bool Ready => lightCamera != null && lightTexture != null;
        public float WarningAlpha => isActiveAndEnabled ? warning : 0f;
        public float FlashAlpha => isActiveAndEnabled ? flash : 0f;
        public RenderTexture LightTexture => lightTexture;
        public GameObject PrivateRig => rig;

        public void Build()
        {
            if (rig != null) return;
            var source = Resources.Load<Shader>("Yaguang/Light/Emission");
            var combine = Resources.Load<Shader>("Yaguang/Light/Composite");
            if (source == null || combine == null || !source.isSupported || !combine.isSupported) return;
            if (tuning == null) tuning = LabYaguangFlashLightProfile.GameDefaults();
            emission = new Material(source) { hideFlags = HideFlags.DontSave };
            composite = new Material(combine) { hideFlags = HideFlags.DontSave };
            display = gameObject.AddComponent<RawImage>();
            display.raycastTarget = false;
            display.material = composite;
            rig = new GameObject("Yaguang HDR light - private") { hideFlags = HideFlags.DontSave, layer = PrivateLayer };
            // Ownership in the hierarchy also cleans up a never-enabled preview or a domain reload.
            rig.transform.SetParent(transform, false);
            PositionRig();
            var cameraObject = new GameObject("HDR Bloom Camera") { layer = PrivateLayer };
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            lightCamera = cameraObject.AddComponent<Camera>();
            lightCamera.orthographic = true;
            lightCamera.orthographicSize = 1f;
            lightCamera.clearFlags = CameraClearFlags.SolidColor;
            lightCamera.backgroundColor = Color.black;
            lightCamera.allowHDR = true;
            lightCamera.allowMSAA = false;
            lightCamera.cullingMask = 1 << PrivateLayer;
            lightCamera.useOcclusionCulling = false;
            lightCamera.depth = -1000;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.renderShadows = false;
            cameraData.volumeLayerMask = 1 << PrivateLayer;
            cameraData.volumeTrigger = cameraObject.transform;
            volume = rig.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(.85f);
            bloom.intensity.Override(.35f);
            bloom.scatter.Override(.75f);
            bloom.maxIterations.Override(5);
            bloom.tint.Override(new Color(1f, .985f, .94f));
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            volume.sharedProfile = profile;
            volume.weight = 0f;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HDR Flash Emitter";
            quad.layer = PrivateLayer;
            quad.transform.SetParent(rig.transform, false);
            var collider = quad.GetComponent<Collider>();
            collider.enabled = false;
            Release(collider);
            quad.GetComponent<MeshRenderer>().sharedMaterial = emission;
            emitter = quad.transform;
            lightCamera.enabled = false;
            Resize();
        }

        private void PositionRig()
        {
            rig.transform.SetPositionAndRotation(new Vector3(0, -30000, 0), Quaternion.identity);
            Vector3 scale = transform.lossyScale;
            rig.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(scale.x), .00001f),
                1f / Mathf.Max(Mathf.Abs(scale.y), .00001f),
                1f / Mathf.Max(Mathf.Abs(scale.z), .00001f));
        }

        private void Resize()
        {
            var rect = ((RectTransform)transform).rect.size;
            if (rect.x <= 0 || rect.y <= 0) return;
            var canvas = GetComponentInParent<Canvas>();
            float pixelScale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            float factor = Mathf.Min(1f, 960f / Mathf.Max(rect.x * pixelScale, rect.y * pixelScale));
            int width = Mathf.Max(32, Mathf.CeilToInt(rect.x * pixelScale * factor));
            int height = Mathf.Max(32, Mathf.CeilToInt(rect.y * pixelScale * factor));
            if (lightTexture != null && lightTexture.width == width && lightTexture.height == height) return;
            lightCamera.targetTexture = null;
            if (lightTexture != null) { lightTexture.Release(); Release(lightTexture); }
            lightTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { name = "Yaguang HDR Bloom", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            lightTexture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = lightTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            lightCamera.targetTexture = lightTexture;
            lightCamera.aspect = rect.x / rect.y;
            emitter.localScale = new Vector3(2f * lightCamera.aspect, 2f, 1f);
            emission.SetFloat("_Aspect", lightCamera.aspect);
            composite.SetFloat("_Aspect", lightCamera.aspect);
            display.texture = lightTexture;
        }

        public void Present(float warningAmount, float waveProgress, float veilAmount, int countdown = 0, bool fullScreenPulse = false, float visibility = 1f)
        {
            if (rig == null) Build();
            if (rig == null) return;
            PositionRig();
            Resize();
            warning = Mathf.Clamp01(warningAmount);
            visibility = Mathf.Clamp01(visibility);
            flash = (waveProgress >= 0 ? 1f : Mathf.Clamp01(veilAmount)) * visibility;
            bool active = visibility > 0f && (warning > 0 || flash > 0);
            composite.SetFloat("_Visibility", visibility);
            float pulse = fullScreenPulse && waveProgress >= 0 ? LabYaguangFlashHud.FlashPulseStrength(waveProgress) : 0f;
            emission.SetColor("_WhiteColor", Color.Lerp(tuning.coreColor, Color.white, pulse * .9f));
            emission.SetColor("_GlowColor", tuning.glowColor);
            emission.SetFloat("_FlashRadiance", Mathf.Max(0f, tuning.flashRadiance));
            emission.SetFloat("_WarningRadiance", Mathf.Max(0f, tuning.warningRadiance));
            emission.SetFloat("_SourceArea", fullScreenPulse && waveProgress >= 0 ? 1f : Mathf.Clamp(tuning.sourceArea, .02f, 1f));
            emission.SetFloat("_Warning", warning * Mathf.Max(0f, tuning.warningGain));
            emission.SetFloat("_WarningStage", countdown);
            emission.SetFloat("_Wave", fullScreenPulse && waveProgress >= 0 ? 1f - Mathf.Pow(pulse, 1f / 3f) : waveProgress);
            // Keep the translucent veil under the brief whole-screen pulse and its decay.
            emission.SetFloat("_Veil", Mathf.Clamp01(veilAmount) * Mathf.Max(0f, tuning.afterglowGain) * .85f);
            composite.SetFloat("_LightLimit", fullScreenPulse && waveProgress >= 0 ? .22f + .2f * pulse : veilAmount > 0 ? .22f : 1f);
            float decay = fullScreenPulse && waveProgress >= 0 ? 1f - pulse : 0f;
            bloom.intensity.value = Mathf.Max(0f, waveProgress >= 0
                ? Mathf.Lerp(tuning.flashBloom, tuning.otherBloom, decay) : tuning.otherBloom);
            bloom.scatter.value = Mathf.Clamp01(tuning.scatter);
            bloom.threshold.value = Mathf.Max(0f, tuning.threshold);
            bloom.tint.value = tuning.bloomColor;
            display.enabled = active;
            volume.weight = active ? 1f : 0f;
            lightCamera.enabled = active && isActiveAndEnabled;
        }

        public void Configure(LabYaguangFlashTuning values)
        {
            tuning = values != null ? values.Copy() : LabYaguangFlashLightProfile.GameDefaults();
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDisable()
        {
            warning = flash = 0;
            if (lightCamera != null) lightCamera.enabled = false;
            if (volume != null) volume.weight = 0;
        }
        private void OnDestroy()
        {
            if (lightCamera != null) lightCamera.targetTexture = null;
            if (lightTexture != null) { lightTexture.Release(); Release(lightTexture); }
            if (rig != null) Release(rig);
            if (profile != null)
            {
                foreach (var component in profile.components) if (component != null) Release(component);
                Release(profile);
            }
            if (emission != null) Release(emission);
            if (composite != null) Release(composite);
        }
    }
}
