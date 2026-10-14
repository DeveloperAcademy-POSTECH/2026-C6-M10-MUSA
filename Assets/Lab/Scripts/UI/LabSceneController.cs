using UnityEngine;

namespace C6Lab
{
    [DefaultExecutionOrder(-100)]
    public sealed class LabSceneController : MonoBehaviour
    {
        public LabConfig config;
        public LabOrbBoard board;
        public Camera orbCamera;

        private void Awake()
        {
            LabNetwork boardNetwork = null;
            foreach (var network in FindObjectsByType<LabNetwork>(FindObjectsSortMode.None))
            {
                if (network.board == board) boardNetwork = network;
                if (network.board != board || network.target == null
                    || network.target.GetComponent<LabHangingTarget>() == null) continue;
                if (network.battleFloor != null)
                    network.battleFloor.transform.position += Vector3.down * .7f;
                break;
            }
            // 장면에 소리를 듣는 귀(AudioListener)가 없으면 어떤 소리도 들리지 않는다.
            // 게임 소리는 모두 화면 기준(2D)이라 위치와 상관없이 하나만 있으면 된다.
            if (FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            // 구슬 생성 효과음은 장면 수정 없이 보드 옆에 붙여 둔다.
            LabGenerateSound generateSound = GetComponent<LabGenerateSound>();
            if (board != null && generateSound == null)
            {
                generateSound = gameObject.AddComponent<LabGenerateSound>();
                generateSound.Bind(board);
            }
            // 전투 BGM도 같은 방식으로 붙이고, 전투 상태를 읽을 네트워크를 연결한다.
            if (boardNetwork != null && GetComponent<LabBattleBgm>() == null)
                gameObject.AddComponent<LabBattleBgm>().Bind(boardNetwork);
            // 일시정지 메뉴에 BGM·효과음 크기 슬라이더를 붙인다. 값은 기기마다 따로 저장된다.
            LabHud hud = FindFirstObjectByType<LabHud>();
            if (hud != null && hud.pauseMenu != null && GetComponent<LabAudioSettingsPanel>() == null)
                gameObject.AddComponent<LabAudioSettingsPanel>().Bind(hud.pauseMenu, generateSound);
            ReconfigureBoard();
        }

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
