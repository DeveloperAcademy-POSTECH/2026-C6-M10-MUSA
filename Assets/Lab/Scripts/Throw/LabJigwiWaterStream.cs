using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>#540: 날아가는 부적 물구슬을 따라가는 굵은 물줄기 꼬리 + 흩날리는 물방울·물안개. 구슬이 사라지면 꼬리를 남겨 흐려진다.</summary>
    public sealed class LabJigwiWaterStream : MonoBehaviour
    {
        private Transform source;
        private float radius, dropDebt, mistDebt, fadeUntil = -1f;
        private Vector3 last, velocity;
        private TrailRenderer trail;

        public void Begin(Transform follow, float orbRadius, Material material)
        {
            source = follow; radius = Mathf.Max(.01f, orbRadius);
            last = follow != null ? follow.position : transform.position;
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = .32f;
            trail.minVertexDistance = radius * .2f;
            trail.widthMultiplier = radius * 2.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.35f, .7f), new Keyframe(1f, 0f));
            trail.numCapVertices = 4;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.sharedMaterial = material != null ? material : LabJigwiWaterFx.LineMaterial;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.92f, 1f, 1f), 0f), new GradientColorKey(new Color(.4f, .78f, 1f), .35f),
                    new GradientColorKey(new Color(.12f, .45f, .95f), 1f) },
                new[] { new GradientAlphaKey(.95f, 0f), new GradientAlphaKey(.7f, .4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sortingOrder = 18;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (source != null && source.gameObject.activeInHierarchy)
            {
                Vector3 now = source.position;
                velocity = (now - last) / dt; last = now;
                transform.position = now;
                LabJigwiWaterFx fx = LabJigwiWaterFx.Get(gameObject.layer);
                dropDebt += dt * 70f; mistDebt += dt * 14f;
                while (dropDebt >= 1f)
                {
                    dropDebt -= 1f;
                    Vector3 shed = -velocity * Random.Range(.05f, .18f) + Random.insideUnitSphere * radius * 6f + Vector3.up * radius * 2f;
                    fx.Drop(now + Random.insideUnitSphere * radius * .7f, shed, radius * Random.Range(.16f, .34f), Random.Range(.3f, .55f));
                }
                while (mistDebt >= 1f)
                {
                    mistDebt -= 1f;
                    fx.Mist(now - velocity * dt, -velocity * .05f + Random.insideUnitSphere * radius,
                        radius * .9f, radius * 2.6f, Random.Range(.3f, .45f));
                }
                return;
            }
            // The orb hit or missed: leave the stream where it ended and let it fade.
            if (fadeUntil < 0f) { fadeUntil = Time.time + (trail != null ? trail.time : 0f) + .05f; if (trail != null) trail.emitting = false; }
            if (Time.time >= fadeUntil) Destroy(gameObject);
        }
    }
}
