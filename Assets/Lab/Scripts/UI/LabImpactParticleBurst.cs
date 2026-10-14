using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// One short, presentation-only hit burst. Particle emission and lifetime are authored in
    /// the prefab; this component only applies the orb's element color and reuses the systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabImpactParticleBurst : MonoBehaviour
    {
        [SerializeField] private ParticleSystem core;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem ring;
        [SerializeField, Min(.05f)] private float maxLifetimeSeconds = .65f;

        public float MaxLifetimeSeconds => Mathf.Max(.05f, maxLifetimeSeconds);

        internal bool Begin(Color elementColor, Vector3 incomingVelocity, Camera battleCamera)
        {
            if (core == null && sparks == null && ring == null) return false;

            if (sparks != null && incomingVelocity.sqrMagnitude > .0001f)
            {
                Vector3 outward = -incomingVelocity.normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(outward, Vector3.up)) > .98f
                    ? Vector3.forward : Vector3.up;
                sparks.transform.rotation = Quaternion.LookRotation(outward, up);
            }
            FaceCamera(battleCamera);

            // The prefab's size/alpha-over-lifetime curves remain in control of the silhouette.
            // Only its tint changes for the approved hit's element. Set pose and tint before
            // emission so the first frame cannot show particles in a stale pooled orientation.
            StartSystem(core, elementColor);
            StartSystem(sparks, elementColor);
            StartSystem(ring, elementColor);
            return true;
        }

        internal void FaceCamera(Camera battleCamera)
        {
            if (battleCamera == null) return;
            Face(core, battleCamera);
            Face(ring, battleCamera);
        }

        internal bool IsAlive()
        {
            return (core != null && core.IsAlive(false))
                || (sparks != null && sparks.IsAlive(false))
                || (ring != null && ring.IsAlive(false));
        }

        internal void End()
        {
            StopSystem(core);
            StopSystem(sparks);
            StopSystem(ring);
            gameObject.SetActive(false);
        }

        private static void StartSystem(ParticleSystem system, Color color)
        {
            if (system == null) return;
            var main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(false);
        }

        private static void StopSystem(ParticleSystem system)
        {
            if (system != null)
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void Face(ParticleSystem system, Camera battleCamera)
        {
            if (system == null) return;
            Vector3 towardCamera = battleCamera.orthographic
                ? -battleCamera.transform.forward
                : battleCamera.transform.position - system.transform.position;
            if (towardCamera.sqrMagnitude > .0001f)
                system.transform.rotation = Quaternion.LookRotation(
                    towardCamera, battleCamera.transform.up);
        }

        private void OnValidate()
        {
            maxLifetimeSeconds = Mathf.Max(.05f, maxLifetimeSeconds);
        }
    }
}
