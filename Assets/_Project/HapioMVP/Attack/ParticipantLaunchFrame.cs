using C6.Prototype.Presentation;
using UnityEngine;

//몬스터 중심에서 기존 투척 위치까지의 거리 계산
//→ 플레이어 각도만큼 회전
//→ 몬스터 중심 반대편의 실제 투척 위치 계산
//→ 화면 좌우 방향도 같은 각도로 회전

namespace C6.Prototype.Attack
{
    public static class ParticipantLaunchFrame
    {
        public static ProjectileLaunchBasis Calculate(
            ProjectileLaunchBasis baseline,
            int playerNumber,
            int participantCount
        )
        {
            float yaw = ParticipantViewAngle.CalculateYaw(
                playerNumber,
                participantCount
            );

            Quaternion rotation = Quaternion.AngleAxis(
                yaw,
                Vector3.up
            );

            Vector3 originOffset =
                baseline.Origin - baseline.AimPoint;

            Vector3 rotatedOrigin =
                baseline.AimPoint
                + rotation * originOffset;

            Vector3 rotatedHorizontalAxis =
                rotation * baseline.HorizontalAxis;

            return new ProjectileLaunchBasis(
                rotatedOrigin,
                rotatedHorizontalAxis,
                baseline.Width,
                baseline.AimPoint
            );
        }
    }
}
