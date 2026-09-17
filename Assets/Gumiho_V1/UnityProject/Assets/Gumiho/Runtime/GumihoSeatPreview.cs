using UnityEngine;

namespace ADA.Gumiho
{
    [ExecuteAlways]
    public sealed class GumihoSeatPreview : MonoBehaviour
    {
        [Range(3, 5)] public int playerCount = 5;
        [Range(0, 4)] public int playerIndex;
        public Camera[] cameras;
        public Transform subject;

        private void OnEnable() { Refresh(); }
        private void OnValidate() { Refresh(); }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.Q))
            {
                playerIndex = (playerIndex + playerCount - 1) % playerCount;
                Refresh();
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                playerIndex = (playerIndex + 1) % playerCount;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (cameras == null || subject == null) return;
            int selected = Mathf.Clamp(playerIndex, 0, playerCount - 1);
            var center = subject.position + Vector3.up * 1.55f;
            for (int i = 0; i < cameras.Length; ++i)
            {
                if (cameras[i] == null) continue;
                cameras[i].enabled = i == selected;
                float angle = i * 360f / playerCount;
                cameras[i].transform.position = center +
                    Quaternion.Euler(0, angle, 0) * new Vector3(0, 1.0f, 6f);
                cameras[i].transform.LookAt(center);
                var listener = cameras[i].GetComponent<AudioListener>();
                if (listener != null) listener.enabled = i == selected;
            }
        }
    }
}
