using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540 부적 구슬(물) 발사: 구슬 하나가 넓은 파도를 타고 간다. 파도 폭 안에는 보이지 않는 옆 갈래 둘이 함께 날아가
    /// (모든 기기가 같은 자리·속도로 계산) Host에서는 셋 모두 진짜 투사체라 각각 박에 맞으면 각각 칠한다. 옆 갈래는 피해·다른 보상이 없다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>
        /// 가운데 구슬(main)에 물기둥·물줄기 연출을 붙이고, 양옆 물구슬 둘을 띄운다. authoritative면 Host 판정용 투사체,
        /// 아니면 보기만 하는 복사본이다.
        /// </summary>
        private void LaunchTalismanVolley(LabProjectile main, string orbId, ulong owner, Vector3 position, Vector3 velocity,
            float sizeScale, LabOrbElement element, bool authoritative)
        {
            if (main == null || config == null || target == null || string.IsNullOrEmpty(orbId)) return;
            float radius = config.ThrowRadius * Mathf.Max(.1f, sizeScale);
            LabJigwiWaterVolley.Launch(position, velocity, radius, main.gameObject.layer);
            LabJigwiWaveRide.Follow(main.transform, radius, velocity, main.gameObject.layer); // the orb rides a wave
            for (int k = 1; k < LabModel.JigwiTalismanVolleyCount; k++)
            {
                string id = LabModel.JigwiTalismanShardId(orbId, k);
                Vector3 at = LabJigwiWaterVolley.ShardPosition(position, velocity, k, radius);
                Vector3 shardVelocity = LabJigwiWaterVolley.ShardVelocity(velocity, k);
                LabProjectile shard = LabProjectile.Create(at, config, projectileRoot, element, 1f);
                shard.name = "Projectile " + id;
                shard.SetRadiusScale(sizeScale);
                shard.SetMissSurface(battleFloor);
                shard.SetFireSurfaces(JigwiFireSurfacesFor(owner));
                if (authoritative) shard.Launch(id, owner, shardVelocity, config, target, OnProjectileResolved);
                else shard.LaunchVisual(shardVelocity, config, target);
                shard.gameObject.AddComponent<LabJigwiHiddenShard>(); // only the wave shows where it flies
            }
        }

        /// <summary>Host: 옆 물구슬 하나가 내려앉았다. 박이면 물로 칠하고 모두에게 알리고, 아니면 잊는다.</summary>
        private void ResolveTalismanShard(LabProjectileResult result, double now)
        {
            string id = result.ProjectileId;
            if (!result.Hit || result.Kind != LabProjectileHitKind.Monster || target == null)
            {
                model.DropJigwiTalismanShard(id);
                return;
            }
            Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
            Vector3 local = hitbox.InverseTransformPoint(result.HitPoint);
            Vector3 paintDirection = local.normalized;
            if (model.PaintSurface.WouldBlockHit(result.OwnerPlayerId, paintDirection))
            {
                model.DropJigwiTalismanShard(id);
                jigwiStagePresentation?.PlayBlocked(local);
                return;
            }
            if (!model.ConfirmJigwiTalismanShardHit(id, now, paintDirection,
                LabPaintImpact.Normalize(result.ImpactVelocity, result.HitNormal))) return;
            float spin = target.GetComponent<LabHangingTarget>()?.ApplyHostImpact(result.HitPoint, result.ImpactVelocity) ?? 0f;
            target.NotifyApprovedHit(local, LabOrbElement.Water);
            impactFeedback?.PlayHit(target, local, LabOrbElement.Water, 1, result.ImpactVelocity, false);
            PresentTalismanHit(local, result.OwnerPlayerId);
            Color paint = model.PaintColorFor(LabOrbElement.Water);
            var impact = new LabNetworkPacket { type = "targetImpact", sessionId = sessionId,
                round = model.Round, firstId = id, owner = result.OwnerPlayerId,
                element = (int)LabOrbElement.Water, damage = 0, impactValue = 1, paintBlocked = false,
                paintX = paintDirection.x, paintY = paintDirection.y, paintZ = paintDirection.z,
                paintR = Mathf.Clamp01(paint.r), paintG = Mathf.Clamp01(paint.g), paintB = Mathf.Clamp01(paint.b), paintA = Mathf.Clamp01(paint.a),
                x = local.x, y = local.y, z = local.z,
                wx = result.ImpactVelocity.x, wy = result.ImpactVelocity.y, wz = result.ImpactVelocity.z,
                spinImpulse = spin, talisman = true, sizeScale = 1f };
            foreach (ulong player in order) if (player != LocalPlayerId) Send(player, impact);
        }
    }
}
