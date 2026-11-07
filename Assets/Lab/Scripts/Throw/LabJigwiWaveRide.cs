using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// #540 부적 구슬이 파도를 타고 간다. 구슬 아래로 넓은 물결 판(세 갈래 칠하는 폭을 덮는 너비)이 궤적을 따라 밀려가고,
    /// 그 앞머리에는 물벽이 솟아 하얀 물보라가 말려 넘어가며 흩날린다. 구슬이 박에 닿거나 사라지면 파도가 그 자리에서
    /// 크게 부서지고, 남은 물결은 흐려진다. 그림만이고 판정에는 손대지 않는다.
    /// </summary>
    public sealed class LabJigwiWaveRide : MonoBehaviour
    {
        private Transform source;
        private float radius, halfWidth, sprayDebt, fadeUntil = -1f;
        private Vector3 last, velocity, side = Vector3.right, startedAt;
        private Transform sheetRoot;
        private TrailRenderer sheet;
        private LineRenderer crest;
        private readonly Vector3[] crestPoints = new Vector3[11];
        private bool crashed;

        /// <summary>Wave half width at the start, in orb radii (covers the two side lanes and a little more).</summary>
        public const float HalfWidthRadii = 3.6f;

        public static LabJigwiWaveRide Follow(Transform orb, float orbRadius, Vector3 launchVelocity, int layer)
        {
            if (orb == null) return null;
            var go = new GameObject("Jigwi Talisman Wave");
            go.layer = layer;
            go.transform.position = orb.position;
            var ride = go.AddComponent<LabJigwiWaveRide>();
            ride.Begin(orb, orbRadius, launchVelocity);
            return ride;
        }

        private void Begin(Transform orb, float orbRadius, Vector3 launchVelocity)
        {
            source = orb; radius = Mathf.Max(.01f, orbRadius);
            last = startedAt = orb.position; velocity = launchVelocity;
            var flat = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
            side = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            halfWidth = radius * HalfWidthRadii;
            sheet = Ribbon("Wave Body", out sheetRoot, .5f, new[]
            {
                new GradientColorKey(new Color(.75f, .97f, 1f), 0f), new GradientColorKey(new Color(.25f, .65f, 1f), .3f),
                new GradientColorKey(new Color(.05f, .3f, .85f), 1f)
            }, new[] { new GradientAlphaKey(.85f, 0f), new GradientAlphaKey(.55f, .45f), new GradientAlphaKey(0f, 1f) });
            // Crest: a band of white water across the whole width at the wave's front, facing the camera.
            var crestObject = new GameObject("Wave Crest");
            crestObject.layer = gameObject.layer;
            crestObject.transform.SetParent(transform, false);
            crest = crestObject.AddComponent<LineRenderer>();
            crest.useWorldSpace = true; crest.positionCount = crestPoints.Length;
            crest.alignment = LineAlignment.View; crest.numCapVertices = 4; crest.numCornerVertices = 2;
            crest.shadowCastingMode = ShadowCastingMode.Off; crest.receiveShadows = false;
            crest.sharedMaterial = LabJigwiWaterFx.LineMaterial;
            crest.widthCurve = new AnimationCurve(new Keyframe(0f, .3f), new Keyframe(.5f, 1f), new Keyframe(1f, .3f));
            var crestColors = new Gradient();
            crestColors.SetKeys(
                new[] { new GradientColorKey(new Color(.6f, .9f, 1f), 0f), new GradientColorKey(Color.white, .5f), new GradientColorKey(new Color(.6f, .9f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.85f, .25f), new GradientAlphaKey(.95f, .5f), new GradientAlphaKey(.85f, .75f), new GradientAlphaKey(0f, 1f) });
            crest.colorGradient = crestColors;
            crest.sortingOrder = 19;
            Pose(orb.position);
            sheet.Clear();
        }

        private TrailRenderer Ribbon(string label, out Transform root, float seconds, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var go = new GameObject(label);
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            root = go.transform;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = seconds;
            trail.minVertexDistance = radius * .25f;
            trail.alignment = LineAlignment.TransformZ; // lies in the plane facing this child's Z (set in Pose)
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.sharedMaterial = LabJigwiWaterFx.LineMaterial;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.6f, .8f), new Keyframe(1f, .4f));
            var gradient = new Gradient(); gradient.SetKeys(colors, alphas);
            trail.colorGradient = gradient;
            trail.sortingOrder = 17;
            return trail;
        }

        private void Pose(Vector3 at)
        {
            // The flight spreads a little (the side lanes open outward), so the wave widens with the distance flown.
            Vector3 flown = at - startedAt; flown.y = 0f;
            float width = 2f * (halfWidth + flown.magnitude * Mathf.Tan(LabJigwiWaterVolley.SpreadDegrees * Mathf.Deg2Rad));
            // Body: a wide sheet lying flat just under the orb (its plane faces up).
            sheetRoot.position = at - Vector3.up * radius * .8f;
            sheetRoot.rotation = Quaternion.LookRotation(Vector3.up, side);
            sheet.widthMultiplier = width;
            // Crest: across the width at the orb, bulging up in the middle and rolling (a gentle wobble along it).
            float roll = Time.time * 9f;
            for (int i = 0; i < crestPoints.Length; i++)
            {
                float across = i / (crestPoints.Length - 1f) - .5f;
                float lift = (1f - 4f * across * across) * radius * .6f + Mathf.Sin(roll + across * 9f) * radius * .2f;
                crestPoints[i] = at + side * across * width + Vector3.up * (lift - radius * .3f);
            }
            if (crest != null) { crest.SetPositions(crestPoints); crest.widthMultiplier = radius * 2.4f; }
            Width = width;
        }

        /// <summary>Current wave width (world units).</summary>
        public float Width { get; private set; }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            LabJigwiWaterFx fx = LabJigwiWaterFx.Get(gameObject.layer);
            if (source != null && source.gameObject.activeInHierarchy)
            {
                Vector3 now = source.position;
                velocity = (now - last) / dt; last = now;
                transform.position = now;
                Pose(now);
                Vector3 forward = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
                // Spray curling over the crest all along its width.
                sprayDebt += dt * 260f;
                while (sprayDebt >= 1f)
                {
                    sprayDebt -= 1f;
                    float across = Random.Range(-.5f, .5f) * Width;
                    Vector3 at = now + side * across + Vector3.up * radius * Random.Range(.6f, 1.4f);
                    Vector3 curl = forward * velocity.magnitude * Random.Range(.1f, .3f) + Vector3.up * radius * Random.Range(6f, 12f)
                        + Random.insideUnitSphere * radius * 3f;
                    fx.Drop(at, curl, radius * Random.Range(.15f, .32f), Random.Range(.35f, .6f));
                }
                if (Random.value < dt * 30f)
                    fx.Mist(now + side * Random.Range(-.5f, .5f) * Width, -velocity * .08f + Vector3.up * radius * 2f,
                        radius * 1.2f, radius * 3.5f, Random.Range(.35f, .5f));
                return;
            }
            if (!crashed)
            {
                // The orb is gone (hit or missed): the wave breaks right there, all along its width.
                crashed = true;
                Vector3 forward = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
                for (int i = 0; i < 70; i++)
                {
                    float across = Random.Range(-.5f, .5f) * Width;
                    Vector3 burst = (forward * .8f + Vector3.up * Random.Range(.6f, 1.4f) + Random.insideUnitSphere * .6f).normalized;
                    fx.Drop(last + side * across, burst * radius * Random.Range(10f, 20f), radius * Random.Range(.2f, .45f),
                        Random.Range(.5f, .9f));
                }
                for (int i = 0; i < 8; i++)
                    fx.Mist(last + side * Random.Range(-.5f, .5f) * Width, Vector3.up * radius * 3f + forward * radius * 2f,
                        radius * 1.8f, radius * 6f, Random.Range(.6f, .9f));
                fadeUntil = Time.time + sheet.time + .05f;
                sheet.emitting = false;
                if (crest != null) crest.enabled = false;
            }
            if (Time.time >= fadeUntil) Destroy(gameObject);
        }
    }
}
