using System;

namespace C6.Prototype.Presentation
{
    public static class ParticipantViewAngle
    {
        public static float CalculateYaw(int playerNumber, int participantCount)
            => CalculateSeatYaw(playerNumber, participantCount);

        /// <summary>The one-based actual round seat, never the player's admission P number.</summary>
        public static float CalculateSeatYaw(int seatNumber, int participantCount)
        {
            if (participantCount < 2 || participantCount > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(participantCount),
                    participantCount,
                    "참가 인원은 2명 이상 5명 이하여야 합니다."
                );
            }

            if (seatNumber < 1 || seatNumber > participantCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seatNumber),
                    seatNumber,
                    "실제 자리는 1부터 참가 인원수 사이여야 합니다."
                );
            }

            float angleStep = 360f / participantCount;
            float playerYaw = (seatNumber - 1) * angleStep;

            return playerYaw;
        }
    }
}
