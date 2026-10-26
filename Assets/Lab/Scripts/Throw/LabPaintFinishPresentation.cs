using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>Render-only completion recoil: 1.2 real seconds at .2 presentation speed. Never changes global time or colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class LabPaintFinishPresentation : MonoBehaviour
    {
        public const float Duration = 1.2f;
        public const float PresentationSpeed = .2f;
        public LabNetwork Network { get; private set; }
        public bool ExternallyCoordinated { get; set; }
        public bool ResultReleased { get; set; }
        public bool Complete { get; private set; }
        public bool Running { get; private set; }
        public float Progress { get; private set; }
        public GameObject PresentationRoot => copy;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly List<bool> previousHidden = new List<bool>();
        GameObject copy;
        Transform slowBody;
        ParticleSystem celebration;
        Material celebrationMaterial;
        string roundKey;
        double localBeganAt = -1d, localWaitAt = -1d;
        Vector3 restScale;
        Quaternion restRotation;
        float yawVelocity;

        public static LabPaintFinishPresentation Attach(LabNetwork network)
        {
            if (network == null || network.target == null) return null;
            var value = network.target.GetComponent<LabPaintFinishPresentation>();
            if (value == null) value = network.target.gameObject.AddComponent<LabPaintFinishPresentation>();
            value.Network = network;
            return value;
        }
        public static bool IsCompletedSurface(LabNetwork network) => network != null && network.IsConnected
            && network.Snapshot != null && !network.Snapshot.teamTerritory
            && LabGourdResultViewer.IsEligible(network.Snapshot, network.PaintSurface);

        public static bool DelayResult(LabNetwork network, LabSnapshot snapshot)
        {
            if (network == null || snapshot == null || snapshot.phase != LabPhase.Victory || snapshot.teamTerritory) return false;
            var value = network.target != null ? network.target.GetComponent<LabPaintFinishPresentation>() : null;
            return value != null && value.isActiveAndEnabled
                && (value.ExternallyCoordinated ? !value.ResultReleased : !value.Complete);
        }
        public bool Prepare()
        {
            if (!isActiveAndEnabled || !IsCompletedSurface(Network)) return false;
            string current = Network.Snapshot.sessionId + "/" + Network.Snapshot.round;
            if (current != roundKey) { ResetPresentation(); roundKey = current; }
            var paint = GetComponent<LabGourdPaint>();
            if (paint == null || !paint.IsReady) return false;
            paint.RefreshCommittedMap();
            return paint.CompletionVisualReady;
        }
        public bool Begin()
        {
            if (Running || Complete) return Running;
            if (!Prepare()) return false;
            var paint = GetComponent<LabGourdPaint>();
            Transform source = Network.target.transform;
            copy = new GameObject("Completed paint · presentation only");
            copy.transform.SetPositionAndRotation(source.position, source.rotation);
            copy.transform.localScale = source.lossyScale;
            CopyMeshes(source, copy.transform);
            if (copy.GetComponentsInChildren<MeshRenderer>().Length == 0) { ResetPresentation(); return false; }
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                hidden.Add(renderer); previousHidden.Add(renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
            if (slowBody == null) slowBody = copy.transform;
            restRotation = slowBody.localRotation;
            restScale = slowBody.localScale;
            var hanging = GetComponent<LabHangingTarget>();
            yawVelocity = hanging != null ? Mathf.Clamp(hanging.YawSpeedDegreesPerSecond, -90f, 90f) : 18f;
            BuildCelebration();
            Running = true;
            Progress = 0f;
            return true;
        }
        public void Sample(double realElapsed)
        {
            if (!Running) return;
            float elapsed = Mathf.Clamp((float)realElapsed, 0f, Duration);
            Progress = Mathf.Max(Progress, elapsed / Duration);
            float slowed = elapsed * PresentationSpeed;
            // A copied mesh takes the final approved recoil. Live hanging physics continues untouched.
            if (slowBody != null)
            {
                slowBody.localRotation = restRotation * Quaternion.Euler(0f, yawVelocity * slowed, 0f);
                slowBody.localScale = restScale * (1f + .015f * Mathf.Sin(Progress * Mathf.PI));
            }
            if (Progress >= 1f) { Complete = true; Running = false; }
        }
        public void Skip() { ResetPresentation(); Complete = true; Progress = 1f; }
        public void ResetPresentation()
        {
            for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].forceRenderingOff = previousHidden[i];
            hidden.Clear(); previousHidden.Clear();
            if (copy != null) { copy.SetActive(false); Destroy(copy); }
            if (celebration != null) { celebration.gameObject.SetActive(false); Destroy(celebration.gameObject); }
            if (celebrationMaterial != null) Destroy(celebrationMaterial);
            copy = null; slowBody = null; celebration = null; celebrationMaterial = null;
            Complete = Running = ResultReleased = false; Progress = 0f; localBeganAt = localWaitAt = -1d;
        }
        void LateUpdate()
        {
            var snapshot = Network != null ? Network.Snapshot : null;
            string current = snapshot != null ? snapshot.sessionId + "/" + snapshot.round : null;
            if (current != roundKey || snapshot == null || snapshot.phase != LabPhase.Victory)
            {
                ResetPresentation(); roundKey = current;
                return;
            }
            if (ExternallyCoordinated || Complete || snapshot.teamTerritory) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (localWaitAt < 0d) localWaitAt = now;
            if (!Running && Prepare() && Begin()) localBeganAt = now;
            if (Running) Sample(now - localBeganAt);
            else if (!Complete && now - localWaitAt >= 12d) Skip();
        }
        void CopyMeshes(Transform source, Transform destination)
        {
            if (Network != null && Network.target != null && source == Network.target.VisualMount) slowBody = destination;
            destination.gameObject.layer = source.gameObject.layer;
            MeshFilter mesh = source.GetComponent<MeshFilter>();
            MeshRenderer renderer = source.GetComponent<MeshRenderer>();
            if (mesh != null && mesh.sharedMesh != null && renderer != null && renderer.enabled)
            {
                destination.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var visual = destination.gameObject.AddComponent<MeshRenderer>();
                visual.sharedMaterials = renderer.sharedMaterials;
                visual.shadowCastingMode = ShadowCastingMode.Off;
                visual.receiveShadows = false;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block); visual.SetPropertyBlock(block);
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                { renderer.GetPropertyBlock(block, i); if (!block.isEmpty) visual.SetPropertyBlock(block, i); }
            }
            foreach (Transform child in source)
            {
                if (!child.gameObject.activeSelf) continue;
                var next = new GameObject(child.name + " · finish mesh").transform;
                next.SetParent(destination, false);
                next.localPosition = child.localPosition; next.localRotation = child.localRotation; next.localScale = child.localScale;
                CopyMeshes(child, next);
            }
        }
        void BuildCelebration()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;
            celebrationMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            celebrationMaterial.SetColor("_BaseColor", new Color(1f, .9f, .55f, 1f));
            var root = new GameObject("Paint complete · slow sparks");
            root.transform.position = Network.target.Hitbox.bounds.center;
            celebration = root.AddComponent<ParticleSystem>();
            celebration.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = celebration.main;
            main.loop = false; main.playOnAwake = false; main.useUnscaledTime = true;
            main.simulationSpeed = PresentationSpeed; main.startLifetime = Duration * PresentationSpeed;
            main.startSpeed = 2f; main.startSize = .05f; main.maxParticles = 24;
            main.startColor = new Color(1f, .88f, .45f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = celebration.emission; emission.enabled = false;
            var shape = celebration.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .65f;
            var size = celebration.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = celebrationMaterial;
            celebration.Play(); celebration.Emit(24);
        }
        void OnDisable() => ResetPresentation();
        void OnDestroy() => ResetPresentation();
    }
}
