using System;
using C6.Prototype.Presentation;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>One P2-only ground surface. A ground contact can bounce but can never identify a monster hit.</summary>
    [DefaultExecutionOrder(-150), DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class ThrowBattleFloor : MonoBehaviour
    {
        [SerializeField] SplitScreenLayout layout;
        PhysicsMaterial runtimeMaterial;
        Vector3 appliedSize;
        float appliedY = float.NaN, appliedFriction = float.NaN;
        public PhysicsMaterial RuntimeMaterial => runtimeMaterial;
        public BoxCollider Hitbox => GetComponent<BoxCollider>();
        public void Configure(SplitScreenLayout source)
        {
            if (source == null || source.Config == null) throw new ArgumentNullException(nameof(source));
            layout = source;
            ApplyConfiguration();
        }
        void Awake() => ApplyConfiguration();
        void LateUpdate()
        {
            if (layout == null || layout.Config == null) return;
            var config = layout.Config;
            if (appliedSize != config.ThrowFloorSize || appliedY != config.ThrowFloorY
                || appliedFriction != config.ThrowFloorFriction) ApplyConfiguration();
        }
        public void ApplyConfiguration()
        {
            if (layout == null || layout.Config == null) return;
            var config = layout.Config;
            var box = Hitbox;
            transform.position = new Vector3(0f, config.ThrowFloorY - config.ThrowFloorSize.y * .5f, 0f);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            box.center = Vector3.zero;
            box.size = config.ThrowFloorSize;
            box.isTrigger = false;
            if (Application.isPlaying)
            {
                if (runtimeMaterial == null)
                    runtimeMaterial = new PhysicsMaterial("P2 ground contact") { hideFlags = HideFlags.DontSave };
                runtimeMaterial.bounciness = 0f;
                runtimeMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
                runtimeMaterial.dynamicFriction = config.ThrowFloorFriction;
                runtimeMaterial.staticFriction = config.ThrowFloorFriction;
                runtimeMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
                box.sharedMaterial = runtimeMaterial;
            }
            appliedSize = config.ThrowFloorSize;
            appliedY = config.ThrowFloorY;
            appliedFriction = config.ThrowFloorFriction;
        }
        void OnDestroy()
        {
            if (runtimeMaterial == null) return;
            var box = Hitbox;
            if (box != null && box.sharedMaterial == runtimeMaterial) box.sharedMaterial = null;
            if (Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
