using System;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        private LabJigwiStagePresentation jigwiStagePresentation;
        private LabSnapshot jigwiStageAppliedSnapshot;
        private bool jigwiStageAppliedPaused;

        // Only the Host changes anchors. All peers render the same mesh-local directions.
        private bool CalibrateJigwiStageGeometry(LabSnapshot snapshot, double now)
        {
            if (!IsHost || model == null || !model.JigwiStageEnabled || target == null
                || target.Hitbox == null || snapshot?.jigwiStage == null
                || snapshot.phase != LabPhase.Playing) return false;
            bool changed = false;
            foreach (var group in snapshot.jigwiStage.groups)
            {
                if (group.resolution != LabJigwiStageResolution.None || now >= group.expiresAt) continue;
                var helper = snapshot.players.FirstOrDefault(p => p.id == group.helperPlayer);
                var affected = snapshot.players.FirstOrDefault(p => p.id == group.affectedPlayer);
                if (helper == null || affected == null) continue;
                var siblings = snapshot.jigwiStage.groups.Where(g => g.slot == group.slot
                    && g.affectedPlayer == group.affectedPlayer).OrderBy(g => g.id, StringComparer.Ordinal).ToArray();
                int sibling = Array.FindIndex(siblings, g => g.id == group.id);
                float offset = siblings.Length > 1 ? (sibling == 0 ? -.32f : .32f) : 0f;
                // Preserve the planned position variants when replacing provisional model anchors.
                float rootX = offset, rootY = .08f, fireY = 0f;
                if (siblings.Length == 1 && group.slot == 4) { rootX = .26f; rootY = fireY = .24f; }
                else if (siblings.Length == 1 && group.slot == 5) { rootX = -.26f; rootY = fireY = -.18f; }
                Vector3 root;
                if (!TryJigwiSurfaceDirection(helper, snapshot.players.Length, rootX, rootY, out root))
                {
                    changed |= model.RejectJigwiStageGeometry(group.id, now);
                    continue;
                }
                if (!group.geometryConfigured)
                {
                    Vector3 center;
                    if (!TryMeaningfulJigwiPatch(affected, snapshot.players.Length, rootX, fireY, out center))
                    {
                        changed |= model.RejectJigwiStageGeometry(group.id, now);
                        continue;
                    }
                    var patches = new Vector3[group.patchDirections.Length];
                    patches[0] = center;
                    Vector3 axis = Vector3.Cross(Vector3.up, center).normalized;
                    if (axis.sqrMagnitude < .01f) axis = Vector3.right;
                    float sign = group.slot == 4 ? -1f : 1f;
                    for (int i = 1; i < patches.Length; i++)
                        patches[i] = (Quaternion.AngleAxis(sign * 15f * i, i % 2 == 0 ? Vector3.up : axis) * center).normalized;
                    changed |= model.SetJigwiStageGeometry(group.id, root, patches, now);
                }
                else if (!JigwiRootFacesHelper(group.rootDirection, helper, snapshot.players.Length))
                {
                    // Re-anchor only the root if rotation hides it. The finite fire patches stay put.
                    changed |= model.SetJigwiStageGeometry(group.id, root, group.patchDirections, now);
                }
            }
            return changed;
        }

        private bool TryMeaningfulJigwiPatch(LabPlayerState player, int count, float offset, float preferredY, out Vector3 direction)
        {
            direction = Vector3.zero;
            foreach (float dx in new[] { offset, offset - .28f, offset + .28f })
                foreach (float dy in new[] { preferredY, Mathf.Clamp(preferredY + .26f, -.5f, .5f), Mathf.Clamp(preferredY - .26f, -.5f, .5f) })
                    if (TryJigwiSurfaceDirection(player, count, dx, dy, out var candidate)
                        && !model.PaintSurface.IsPaintedAt(candidate))
                    { direction = candidate; return true; }
            return false;
        }

        private Vector3 JigwiEye(LabPlayerState player, int count) => LabThrowMath.SeatPosition(
            player.seat, count, target.transform.position, config.CameraRadius, config.CameraHeightOffset, true);

        private bool TryJigwiSurfaceDirection(LabPlayerState player, int count, float x, float y, out Vector3 direction)
        {
            direction = Vector3.zero;
            Bounds shell = target.Hitbox.bounds;
            Vector3 eye = JigwiEye(player, count);
            Vector3 front = (eye - shell.center).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -front).normalized;
            float width = Mathf.Max(shell.extents.x, shell.extents.z);
            Vector3 aim = shell.center + right * width * x + Vector3.up * shell.extents.y * y;
            if (!target.Hitbox.Raycast(new Ray(eye, (aim - eye).normalized), out var hit,
                Vector3.Distance(eye, aim) + shell.size.magnitude)) return false;
            direction = target.Hitbox.transform.InverseTransformPoint(hit.point).normalized;
            return LabJigwiStageNetworkValidation.Direction(direction, false);
        }

        private bool JigwiRootFacesHelper(Vector3 direction, LabPlayerState helper, int count)
        {
            Vector3 front = (JigwiEye(helper, count) - target.Hitbox.bounds.center).normalized;
            Vector3 normal = target.Hitbox.transform.TransformVector(direction).normalized;
            return Vector3.Dot(front, normal) >= .55f;
        }

        private void SyncJigwiStagePresentation()
        {
            bool held = latest == null || latest.paused || GameStartHeld;
            if (ReferenceEquals(jigwiStageAppliedSnapshot, latest) && jigwiStageAppliedPaused == held) return;
            jigwiStageAppliedSnapshot = latest;
            jigwiStageAppliedPaused = held;
            if (latest?.jigwiStage == null || !latest.jigwiStage.enabled || target == null)
            {
                if (jigwiStagePresentation != null)
                    jigwiStagePresentation.Apply(null, target, latest, LocalPlayerId, latest?.serverTime ?? 0d, true);
                return;
            }
            if (jigwiStagePresentation == null)
                jigwiStagePresentation = gameObject.GetComponent<LabJigwiStagePresentation>()
                    ?? gameObject.AddComponent<LabJigwiStagePresentation>();
            jigwiStagePresentation.Apply(latest.jigwiStage, target, latest, LocalPlayerId,
                latest.serverTime, latest.paused || GameStartHeld);
        }
    }
}
