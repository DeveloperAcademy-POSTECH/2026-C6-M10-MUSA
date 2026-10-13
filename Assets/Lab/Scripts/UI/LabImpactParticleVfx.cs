using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Pooled world-space visuals for a Host-approved target impact. It reads the existing
    /// target hitbox but never tests collisions, changes damage, or sends network messages.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabImpactParticleVfx : MonoBehaviour
    {
        [SerializeField] private LabImpactParticleBurst effectPrefab;
        [SerializeField] private Camera battleCamera;
        [SerializeField, Range(0, 8)] private int prewarmCount = 8;
        [SerializeField, Range(1, 8)] private int maxPoolSize = 8;
        [SerializeField, Range(0f, .1f)] private float surfaceOffset = .04f;

        private readonly Queue<LabImpactParticleBurst> available = new Queue<LabImpactParticleBurst>(8);
        private readonly List<ActiveBurst> active = new List<ActiveBurst>(8);
        private int createdCount;

        public int ActiveBurstCount => active.Count;

        private struct ActiveBurst
        {
            public LabImpactParticleBurst Burst;
            public Transform Hitbox;
            public Vector3 LocalPoint;
            public float StartedAt;
        }

        private void Awake()
        {
            Prewarm();
        }

        /// <summary>
        /// The local point is relative to the Host-approved MeshCollider, so a moving target
        /// carries the core/ring while already-emitted world-space sparks can fly outward.
        /// </summary>
        public bool Play(LabTarget target, Vector3 targetLocalPoint,
            LabOrbElement element, Vector3 impactVelocity)
        {
            if (!isActiveAndEnabled || effectPrefab == null || target == null
                || target.Hitbox == null || !Finite(targetLocalPoint)
                || !Finite(impactVelocity) || Time.timeScale <= 0f)
                return false;

            if (battleCamera == null) battleCamera = Camera.main;
            if (battleCamera == null) return false;

            LabImpactParticleBurst burst = Acquire();
            if (burst == null) return false;

            Transform hitbox = target.Hitbox.transform;
            burst.transform.position = VisiblePoint(hitbox.TransformPoint(targetLocalPoint));
            burst.transform.rotation = Quaternion.identity;
            burst.gameObject.SetActive(true);
            if (!burst.Begin(LabOrbVisualFactory.TemporaryElementColor(element),
                    impactVelocity, battleCamera))
            {
                ReturnToPool(burst);
                return false;
            }

            active.Add(new ActiveBurst
            {
                Burst = burst,
                Hitbox = hitbox,
                LocalPoint = targetLocalPoint,
                StartedAt = Time.time
            });
            return true;
        }

        /// <summary>Stop all current effects immediately on round reset, lobby return or disable.</summary>
        public void Clear()
        {
            for (int i = 0; i < active.Count; i++)
                ReturnToPool(active[i].Burst);
            active.Clear();
        }

        private void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveBurst entry = active[i];
                LabImpactParticleBurst burst = entry.Burst;
                float age = Time.time - entry.StartedAt;
                if (burst == null || entry.Hitbox == null
                    || age >= burst.MaxLifetimeSeconds
                    || (age > .04f && !burst.IsAlive()))
                {
                    ReturnToPool(burst);
                    active.RemoveAt(i);
                    continue;
                }

                burst.transform.position = VisiblePoint(
                    entry.Hitbox.TransformPoint(entry.LocalPoint));
                burst.FaceCamera(battleCamera);
            }
        }

        private Vector3 VisiblePoint(Vector3 hitPoint)
        {
            // Keep the Host-approved point unchanged. This tiny visual-only offset prevents
            // the billboard particles from being depth-tested behind the Cylinder surface.
            Vector3 cameraDirection = battleCamera.orthographic
                ? -battleCamera.transform.forward
                : battleCamera.transform.position - hitPoint;
            return cameraDirection.sqrMagnitude > .0001f
                ? hitPoint + cameraDirection.normalized * surfaceOffset : hitPoint;
        }

        private void Prewarm()
        {
            if (effectPrefab == null) return;
            int count = Mathf.Min(prewarmCount, maxPoolSize);
            for (int i = createdCount; i < count; i++)
            {
                LabImpactParticleBurst burst = Instantiate(effectPrefab, transform);
                burst.gameObject.SetActive(false);
                available.Enqueue(burst);
                createdCount++;
            }
        }

        private LabImpactParticleBurst Acquire()
        {
            while (available.Count > 0)
            {
                LabImpactParticleBurst pooled = available.Dequeue();
                if (pooled != null) return pooled;
                createdCount = Mathf.Max(0, createdCount - 1);
            }
            if (createdCount < maxPoolSize)
            {
                createdCount++;
                return Instantiate(effectPrefab, transform);
            }
            if (active.Count == 0) return null;

            // Keep a hard cap during many simultaneous hits. The oldest effect gives way to
            // the newest approved hit rather than allocating or accumulating visual garbage.
            LabImpactParticleBurst oldest = active[0].Burst;
            active.RemoveAt(0);
            oldest.End();
            return oldest;
        }

        private void ReturnToPool(LabImpactParticleBurst burst)
        {
            if (burst == null) return;
            burst.End();
            available.Enqueue(burst);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            maxPoolSize = Mathf.Clamp(maxPoolSize, 1, 8);
            prewarmCount = Mathf.Clamp(prewarmCount, 0, maxPoolSize);
            surfaceOffset = Mathf.Clamp(surfaceOffset, 0f, .1f);
        }
    }
}
