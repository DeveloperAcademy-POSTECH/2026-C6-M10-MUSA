using UnityEngine;

namespace C6Lab
{
    [DefaultExecutionOrder(-100)]
    public sealed class LabSceneController : MonoBehaviour
    {
        public LabConfig config;
        public LabOrbBoard board;
        public Camera orbCamera;

        private void Awake() => ReconfigureBoard();

        /// <summary>Refresh the board only before a round; configuring clears current motion.</summary>
        public void ReconfigureBoard()
        {
            if (config == null || board == null || orbCamera == null) return;
            orbCamera.orthographicSize = config.BoardHeight * .5f;
            float halfHeight = orbCamera.orthographicSize;
            float halfWidth = halfHeight * orbCamera.aspect;
            float margin = config.OrbRadius + 0.05f;
            // The plate follows the visible board when the Inspector or DEV tuning
            // changes its height. It is render-only; the physics plane stays at z=0.
            Transform plate = board.transform.Find("OrbBoardPlate");
            if (plate != null)
                plate.localScale = new Vector3(halfWidth * 2f, halfHeight * 2f, 1f);
            board.Configure(config, orbCamera, new Rect(-halfWidth + margin, -halfHeight + margin, (halfWidth - margin) * 2f, (halfHeight - margin) * 2f));
        }
    }
}
