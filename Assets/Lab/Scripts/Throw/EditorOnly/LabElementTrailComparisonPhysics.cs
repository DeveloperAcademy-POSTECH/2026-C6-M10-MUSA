#if UNITY_EDITOR
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Runs only after an explicit editor comparison opens its private physics scene.
    /// The normal projectile FixedUpdate supplies the force before this scene advances.
    /// This type is omitted entirely from player builds.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class LabElementTrailComparisonPhysics : MonoBehaviour
    {
        private PhysicsScene physicsScene;
        private bool initialized;

        public void Initialize(PhysicsScene value)
        {
            physicsScene = value;
            initialized = value.IsValid() && value != Physics.defaultPhysicsScene;
        }

        private void FixedUpdate()
        {
            if (initialized && physicsScene.IsValid())
                physicsScene.Simulate(Time.fixedDeltaTime);
        }
    }
}
#endif
