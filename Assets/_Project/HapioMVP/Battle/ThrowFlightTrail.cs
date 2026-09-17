using C6.Prototype.Attack;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Presentation-only trail behind the local player's own thrown orb. It follows the visible flight
    /// (the Host physics body or the client display proxy) and fades out after that view ends.
    /// It has no collider and never affects contact, damage, ownership, or the snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowFlightTrail : MonoBehaviour
    {
        public const float FadeSeconds = 0.4f; // 꼬리가 얼마나 남는지를 조정할 수 있음. 
        // A first sample later than this may already include a bounce, so it cannot be rewound.
        private const float MaxRewindSeconds = .25f;
        private static readonly Color HeadColor = new Color(.35f, 1f, .88f, .85f);
        private static readonly Color TailColor = new Color(.35f, 1f, .88f, .3f);
        private Transform source;
        private TrailRenderer trail;
        private float fadeElapsed = -1f;
        public bool Fading => fadeElapsed >= 0f;

        public static ThrowFlightTrail Create(Transform parent, Transform followed, ProjectileWire wire, Material material)
        {
            var root = new GameObject("Throw Flight Trail " + wire.id) { layer = followed.gameObject.layer };
            root.transform.position = followed.position;
            root.transform.SetParent(parent, true);
            var view = root.AddComponent<ThrowFlightTrail>();
            view.source = followed;
            var trail = view.trail = root.AddComponent<TrailRenderer>();
             // Keep only the most recent part of the flight; the fade below removes the rest after the view ends.
            trail.time = 0.4f; trail.minVertexDistance = .04f; trail.autodestruct = false;
            trail.widthMultiplier = Mathf.Max(.01f, wire.radius * 1.4f);
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .35f));
            trail.numCapVertices = 4; trail.numCornerVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.sharedMaterial = material;
            view.ApplyAlpha(1f);
            trail.Clear();
            if (TryLaunchOrigin(wire, out var origin)) trail.AddPosition(origin);
            return view;
        }

        /// <summary>Rewinds an early ballistic sample to its launch point so a late attach still starts at the release.</summary>
        public static bool TryLaunchOrigin(ProjectileWire wire, out Vector3 origin)
        {
            origin = default;
            if (wire == null || !wire.ballistic || !Finite(wire.elapsed) || wire.elapsed <= 0f || wire.elapsed > MaxRewindSeconds) return false;
            float t = wire.elapsed;
            origin = wire.position - wire.velocity * t + wire.gravity * (.5f * t * t);
            return Finite(origin.x) && Finite(origin.y) && Finite(origin.z);
        }

        public static float FadeAlpha(float elapsed, float duration) =>
            !Finite(elapsed) || !Finite(duration) || duration <= 0f ? 0f : Mathf.Clamp01(1f - elapsed / duration);

        private void LateUpdate()
        {
            if (trail == null) { Destroy(gameObject); return; }
            if (!Fading)
            {
                if (source != null && source.gameObject.activeInHierarchy) { transform.position = source.position; return; }
                fadeElapsed = 0f; trail.emitting = false;
            }
            fadeElapsed += Time.unscaledDeltaTime;
            float alpha = FadeAlpha(fadeElapsed, FadeSeconds);
            ApplyAlpha(alpha);
            if (alpha <= 0f) Destroy(gameObject);
        }

        private void ApplyAlpha(float alpha)
        {
            trail.startColor = new Color(HeadColor.r, HeadColor.g, HeadColor.b, HeadColor.a * alpha);
            trail.endColor = new Color(TailColor.r, TailColor.g, TailColor.b, TailColor.a * alpha);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
