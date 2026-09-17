using UnityEngine;

namespace ADA.Gumiho
{
    public sealed class GumihoWeakPoint : MonoBehaviour
    {
        [SerializeField] private Vector3 localFacing = Vector3.back;
        [SerializeField, Range(1, 180)] private float halfAngle = 50f;
        [SerializeField] private bool horizontalOnly = true;

        public void Configure(Vector3 facing, float angle, bool horizontal)
        {
            localFacing = facing.normalized;
            halfAngle = Mathf.Clamp(angle, 1, 180);
            horizontalOnly = horizontal;
        }

        // Evaluate independently per player's camera; do not toggle shared visibility.
        public bool IsVisibleFrom(Vector3 observerPosition)
        {
            var facing = transform.TransformDirection(localFacing);
            var direction = observerPosition - transform.position;
            if (horizontalOnly)
            {
                facing = Vector3.ProjectOnPlane(facing, transform.root.up);
                direction = Vector3.ProjectOnPlane(direction, transform.root.up);
            }
            if (direction.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f)
                return false;
            return Vector3.Dot(direction.normalized, facing.normalized) >=
                   Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.09f);
            Gizmos.DrawRay(transform.position, transform.TransformDirection(localFacing) * 0.3f);
        }
    }
}
