using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>One owner-specific extinguishing target. Trigger geometry never deflects other owners' throws.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class LabJigwiFireSurface : MonoBehaviour
    {
        private Collider hitbox;
        private SphereCollider pointHitbox;
        private BoxCollider curtainHitbox;
        private Transform visual;
        private readonly Transform[] tongues = new Transform[3];
        private float radius;
        public ulong OwnerPlayerId { get; private set; }
        public string FireId { get; private set; }
        public string FireTargetId { get; private set; }
        public bool IsCurtain { get; private set; }
        public Collider Hitbox => hitbox;
        public bool Active => isActiveAndEnabled && hitbox != null && hitbox.enabled && gameObject.activeInHierarchy;

        public static LabJigwiFireSurface Create(Transform parent, ulong ownerPlayerId, string fireId,
            string targetId, Vector3 center, float radius, bool showVisual = true)
        {
            var root = new GameObject("Jigwi Fire " + ownerPlayerId + " / " + targetId);
            root.transform.SetParent(parent, false);
            var surface = root.AddComponent<LabJigwiFireSurface>();
            surface.OwnerPlayerId = ownerPlayerId;
            surface.FireId = fireId;
            surface.FireTargetId = targetId;
            surface.Configure(center, radius, true, showVisual);
            return surface;
        }

        /// <summary>Invisible owner-scoped wall. Its full rectangle remains solid while any point is lit.</summary>
        public static LabJigwiFireSurface CreateCurtain(Transform parent, ulong ownerPlayerId, string fireId,
            string targetId, Vector3 center, Quaternion rotation, Vector2 halfSize, bool active)
        {
            var root = new GameObject("Jigwi Curtain " + ownerPlayerId);
            root.transform.SetParent(parent, false);
            var surface = root.AddComponent<LabJigwiFireSurface>();
            surface.OwnerPlayerId = ownerPlayerId; surface.FireId = fireId; surface.FireTargetId = targetId;
            surface.ConfigureCurtain(center, rotation, halfSize, active);
            return surface;
        }

        private void Awake()
        {
            pointHitbox = GetComponent<SphereCollider>();
            pointHitbox.isTrigger = true;
            hitbox = pointHitbox;
            var art = new GameObject("Fire visual");
            art.transform.SetParent(transform, false);
            visual = art.transform;
            for (int i = 0; i < tongues.Length; i++)
            {
                var tongue = new GameObject("Flame tongue " + i);
                tongue.transform.SetParent(visual, false);
                tongue.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var renderer = tongue.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = LabJigwiBurnEffect.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var block = new MaterialPropertyBlock();
                Color color = i == 1 ? new Color(1f, .78f, .12f) : new Color(1f, .22f, .02f);
                block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
                tongues[i] = tongue.transform;
            }
        }

        public void Configure(Vector3 center, float targetRadius, bool active, bool showVisual)
        {
            IsCurtain = false;
            if (curtainHitbox != null) curtainHitbox.enabled = false;
            hitbox = pointHitbox;
            transform.position = center;
            radius = Mathf.Max(.05f, targetRadius);
            pointHitbox.radius = radius;
            pointHitbox.enabled = active;
            visual.gameObject.SetActive(active && showVisual);
            // Collision stays spherical, while the simplified flame points up.
            Pose();
        }

        public void ConfigureCurtain(Vector3 center, Quaternion rotation, Vector2 halfSize, bool active)
        {
            IsCurtain = true;
            pointHitbox.enabled = false;
            if (curtainHitbox == null)
            {
                curtainHitbox = gameObject.AddComponent<BoxCollider>();
                curtainHitbox.isTrigger = true;
            }
            hitbox = curtainHitbox;
            transform.SetPositionAndRotation(center, rotation);
            curtainHitbox.size = new Vector3(Mathf.Max(.05f, halfSize.x * 2f), Mathf.Max(.05f, halfSize.y * 2f), .08f);
            curtainHitbox.enabled = active;
            visual.gameObject.SetActive(false);
        }

        private void Update() { if (visual.gameObject.activeSelf) Pose(); }
        private void Pose()
        {
            for (int i = 0; i < tongues.Length; i++)
            {
                tongues[i].localPosition = new Vector3((i - 1) * radius * .42f, radius * .12f, 0f);
                tongues[i].localScale = new Vector3(.85f, 1.5f + .2f * Mathf.Sin(Time.time * 9f + i), .85f) * radius;
                tongues[i].localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -18f);
            }
        }

        /// <summary>Shared seat frame: normalized fire positions are in front of the real boss, not on the UI.</summary>
        public static Vector3 WorldPosition(LabTarget target, int seatIndex, int seatCount,
            float x01, float y01, LabConfig config)
        {
            Vector3 center = target.Hitbox.bounds.center;
            Bounds shell = target.Hitbox.bounds;
            Vector3 seat = LabThrowMath.SeatPosition(seatIndex, seatCount, center,
                config.ThrowOriginRadius, config.ThrowOriginHeightOffset,
                !(config.TeamTerritoryEnabled && seatCount == LabTeams.Players));
            Vector3 towardSeat = Vector3.ProjectOnPlane(seat - center, Vector3.up).normalized;
            Vector3 screenRight = Vector3.Cross(Vector3.up, -towardSeat);
            float halfWidth = Mathf.Max(.65f, Mathf.Max(shell.extents.x, shell.extents.z));
            float halfHeight = Mathf.Max(.65f, shell.extents.y);
            return center + towardSeat * (halfWidth + .65f)
                + screenRight * ((Mathf.Clamp01(x01) * 2f - 1f) * halfWidth * .75f)
                + Vector3.up * ((Mathf.Clamp01(y01) * 2f - 1f) * halfHeight * .7f);
        }
    }
}
