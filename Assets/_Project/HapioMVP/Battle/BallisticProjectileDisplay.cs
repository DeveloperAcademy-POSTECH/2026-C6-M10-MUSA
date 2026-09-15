using C6.Prototype.Attack;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>Short, bounded presentation prediction from approved Host samples. No collider or gameplay.</summary>
    public sealed class BallisticProjectileDisplay : MonoBehaviour
    {
        private Vector3 position, velocity, gravity;
        private double receivedAt;
        private bool initialized;
        public void Accept(ProjectileWire sample)
        {
            position = sample.position; velocity = sample.velocity; gravity = sample.gravity;
            receivedAt = Time.unscaledTimeAsDouble;
            if (!initialized) { transform.position = position; initialized = true; }
        }
        private void Update()
        {
            if (!initialized) return;
            float time = Mathf.Clamp((float)(Time.unscaledTimeAsDouble - receivedAt), 0f, .1f);
            Vector3 goal = position + velocity * time + gravity * (.5f * time * time);
            transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
        }
    }
}
