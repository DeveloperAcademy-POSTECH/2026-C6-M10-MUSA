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
        private bool hasInitialPose;

        /// <summary>Re-evaluate a seat, or restore the lobby view after leaving a solo session.</summary>
        public void RefreshPose()
        {
            activeSeat = -1;
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
                }
                return;
            }

            if (target == null || config == null || snapshot.players == null) return;
            ulong localId = network.LocalPlayerId;
            foreach (LabPlayerState player in snapshot.players)
            {
                if (player == null || player.id != localId) continue;
                if (player.seat < 0 || player.seat >= 3 || player.seat == activeSeat) return;

                Vector3 center = target.transform.position;
                battleCamera.transform.position = LabThrowMath.SeatPosition(
                    player.seat, 3, center, config.CameraRadius, config.CameraHeightOffset);
                battleCamera.transform.LookAt(center);
                activeSeat = player.seat;
                Debug.Log("C6_LAB_CAMERA seat=" + activeSeat + " position=" +
                    battleCamera.transform.position.ToString("F2"));
                return;
            }
        }
    }
}
