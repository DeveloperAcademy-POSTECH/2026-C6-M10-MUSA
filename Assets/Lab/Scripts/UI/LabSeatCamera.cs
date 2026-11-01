using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Places the local battle camera at its assigned seat around the target. Seat assignment
    /// comes from the host snapshot; the camera does not guess a seat before battle begins.
    /// </summary>
    public sealed class LabSeatCamera : MonoBehaviour
    {
        public LabNetwork network;
        public LabTarget target;
        public Camera battleCamera;
        public LabConfig config;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private int activeSeat = -1;
        private int activeSeatCount = -1;
        private bool hasInitialPose;

        /// <summary>Re-evaluate a seat, or restore the lobby view after leaving a solo session.</summary>
        public void RefreshPose()
        {
            activeSeat = -1;
            activeSeatCount = -1;
            if (battleCamera != null && hasInitialPose && (network == null || network.Snapshot == null))
                battleCamera.transform.SetPositionAndRotation(initialPosition, initialRotation);
        }

        private void Awake()
        {
            if (battleCamera == null) return;
            initialPosition = battleCamera.transform.position;
            initialRotation = battleCamera.transform.rotation;
            hasInitialPose = true;
        }

        private void LateUpdate()
        {
            if (battleCamera == null || network == null) return;

            LabSnapshot snapshot = network.Snapshot;
            if (snapshot == null)
            {
                if (activeSeat >= 0 && hasInitialPose)
                {
                    battleCamera.transform.SetPositionAndRotation(initialPosition, initialRotation);
                    activeSeat = -1;
                    activeSeatCount = -1;
                }
                return;
            }

            if (target == null || config == null || snapshot.players == null) return;
            int seatCount = snapshot.players.Length;
            if (seatCount < 1 || seatCount > LabModel.TerritoryMaximumParticipants) return;
            ulong localId = network.LocalPlayerId;
            foreach (LabPlayerState player in snapshot.players)
            {
                if (player == null || player.id != localId) continue;
                // 땅따먹기: 2:2는 청·홍이 엇갈리고(#281), 1:1은 마주 보고, 3:3은 팀끼리 앞뒤로 앉는다(#394).
                int ring = LabTeams.RingSeatFor(snapshot, player.seat);
                if (ring < 0 || ring >= seatCount
                    || (ring == activeSeat && seatCount == activeSeatCount)) return;

                Vector3 center = target.transform.position;
                battleCamera.transform.position = LabThrowMath.SeatPosition(
                    ring, seatCount, center, config.CameraRadius, config.CameraHeightOffset,
                    !LabTeams.TeamSeats(snapshot));
                battleCamera.transform.LookAt(center + Vector3.up * config.CameraLookHeightOffset);
                activeSeat = ring;
                activeSeatCount = seatCount;
                Debug.Log("C6_LAB_CAMERA seat=" + activeSeat + " position=" +
                    battleCamera.transform.position.ToString("F2"));
                return;
            }
        }
    }
}
