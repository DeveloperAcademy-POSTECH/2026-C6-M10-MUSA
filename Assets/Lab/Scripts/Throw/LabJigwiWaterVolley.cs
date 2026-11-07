using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540 부적 구슬(물) 3D 연출. 던지면 가운데와 양옆, 3갈래 물구슬로 갈라져 날아가 각각 박을 칠한다.
    /// 떠날 때 물기둥이 소용돌이치며 솟고, 날아가는 동안 굵은 물줄기 꼬리와 흩날리는 물방울·물안개가 따라가고,
    /// 박에 맞으면 큰 물보라(물방울 왕관, 퍼지는 물결 고리, 물안개)가 터진다. 모두 그림만이고 판정에는 손대지 않는다.
    /// </summary>
    public static class LabJigwiWaterVolley
    {
        /// <summary>판 위와 날아갈 때 부적 구슬 크기 배율.</summary>
        public const float SizeScale = 1.5f;
        /// <summary>양옆 물구슬이 바깥으로 벌어지는 각도(도).</summary>
        public const float SpreadDegrees = 4f;
        /// <summary>양옆 물구슬이 출발하는 자리 간격 (구슬 반지름 배).</summary>
        public const float SideOffsetRadii = 2.6f;

        public static float Side(int k) => k == 1 ? -1f : 1f;

        private static Vector3 SideAxis(Vector3 velocity)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            return flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
        }

        /// <summary>k번째(1, 2) 옆 물구슬의 출발 자리. 모든 기기가 같은 값을 낸다.</summary>
        public static Vector3 ShardPosition(Vector3 position, Vector3 velocity, int k, float radius)
            => position + SideAxis(velocity) * Side(k) * radius * SideOffsetRadii;

        /// <summary>k번째(1, 2) 옆 물구슬의 출발 속도 (가운데 구슬 속도를 위축으로 조금 돌린 것).</summary>
        public static Vector3 ShardVelocity(Vector3 velocity, int k)
            => Quaternion.AngleAxis(Side(k) * SpreadDegrees, Vector3.up) * velocity;

        /// <summary>날아가는 구슬에 물줄기 꼬리와 흩날리는 물방울을 붙인다.</summary>
        public static void Follow(LabProjectile projectile, float radius)
        {
            if (projectile == null) return;
            var go = new GameObject("Jigwi Water Stream");
            go.layer = projectile.gameObject.layer;
            go.transform.position = projectile.transform.position;
            go.AddComponent<LabJigwiWaterStream>().Begin(projectile.transform, radius,
                projectile.FlightTrail != null ? projectile.FlightTrail.sharedMaterial : null);
        }

        /// <summary>떠나는 자리에서 물기둥이 소용돌이치며 솟는다.</summary>
        public static void Launch(Vector3 position, Vector3 velocity, float radius, int layer)
        {
            LabJigwiWaterFx fx = LabJigwiWaterFx.Get(layer);
            float r = Mathf.Max(.01f, radius);
            Vector3 forward = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            const int spiral = 34;
            for (int i = 0; i < spiral; i++)
            {
                float angle = i * 2.39996f; // golden angle: evenly round the column
                float delay = i / (float)spiral * .18f;
                fx.Swirl(position, angle, r * Random.Range(.6f, 1.3f), r * Random.Range(6f, 10f), Random.Range(7f, 11f),
                    r * Random.Range(.22f, .42f), Random.Range(.45f, .7f), delay);
            }
            for (int i = 0; i < 16; i++)
            {
                Vector3 spray = (Vector3.up * 1.4f + forward * .6f + Random.insideUnitSphere * .9f).normalized;
                fx.Drop(position + Random.insideUnitSphere * r * .5f, spray * r * Random.Range(9f, 15f),
                    r * Random.Range(.18f, .34f), Random.Range(.4f, .65f));
            }
            for (int i = 0; i < 2; i++) fx.Ring(position, r * 1.2f, r * (5.5f + i * 2.5f), .45f + i * .12f, i * .07f);
            for (int i = 0; i < 6; i++)
                fx.Mist(position + Random.insideUnitSphere * r, Vector3.up * r * Random.Range(1.5f, 3f) + forward * r * 1.5f,
                    r * Random.Range(1.2f, 1.8f), r * Random.Range(3.5f, 5f), Random.Range(.5f, .75f));
        }

        /// <summary>박에 맞은 자리에서 큰 물보라가 터진다. normal은 박 바깥쪽.</summary>
        public static void Splash(Vector3 point, Vector3 normal, float radius, int layer)
        {
            LabJigwiWaterFx fx = LabJigwiWaterFx.Get(layer);
            float r = Mathf.Max(.01f, radius);
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.back;
            fx.Flash(point + n * r * .3f, r * 5f, .18f);
            const int crown = 48;
            for (int i = 0; i < crown; i++)
            {
                Vector3 dir = (n * 1.1f + Random.insideUnitSphere + Vector3.up * .5f).normalized;
                fx.Drop(point + n * r * .3f, dir * r * Random.Range(10f, 22f), r * Random.Range(.18f, .45f),
                    Random.Range(.5f, .85f));
            }
            for (int i = 0; i < 3; i++) fx.Ring(point + n * r * .2f, r * 1.5f, r * (7f + i * 3f), .4f + i * .1f, i * .06f);
            // #540: the fire spirit is doused — thick white steam boils up from where it hit, the view shakes and
            // flashes pale blue.
            for (int i = 0; i < 16; i++)
                fx.Steam(point + n * r * .6f + Random.insideUnitSphere * r,
                    Vector3.up * r * Random.Range(4f, 8f) + n * r * Random.Range(1f, 3f) + Random.insideUnitSphere * r * 1.5f,
                    r * Random.Range(1.5f, 2.5f), r * Random.Range(7f, 12f), Random.Range(1.3f, 2.1f), i * .03f);
            fx.Flash(point, r * 70f, .3f, new Color(.55f, .85f, 1f, .5f));
            LabJigwiWaterFx.Shake(r * .9f, .35f);
            for (int i = 0; i < 9; i++)
                fx.Mist(point + n * r * .5f + Random.insideUnitSphere * r * 1.2f,
                    (n + Random.insideUnitSphere * .6f).normalized * r * Random.Range(2f, 5f) + Vector3.up * r * 1.5f,
                    r * Random.Range(1.6f, 2.6f), r * Random.Range(5f, 8f), Random.Range(.6f, .95f));
        }
    }
}
