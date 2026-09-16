using System;

namespace C6.Prototype.Presentation
{
    public static class ParticipantViewAngle
    {
        public static float CalculateYaw(int playerNumber, int participantCount)
        {
            if (participantCount < 2 || participantCount > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(participantCount),
                    participantCount,
                    "참가 인원은 2명 이상 5명 이하여야 합니다."
                );
            }

            if (playerNumber < 1 || playerNumber > participantCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerNumber),
                    playerNumber,
                    "플레이어 번호는 1부터 참가 인원수 사이여야 합니다."
                );
            }

            float angleStep = 360f / participantCount;
            float playerYaw = (playerNumber - 1) * angleStep;

            return playerYaw;
        }
    }
}
