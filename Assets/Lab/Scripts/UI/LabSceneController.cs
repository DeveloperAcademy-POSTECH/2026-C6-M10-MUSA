using UnityEngine;

namespace C6Lab
{
    [DefaultExecutionOrder(-100)]
    public sealed class LabSceneController : MonoBehaviour
    {
        public LabConfig config;
        public LabOrbBoard board;
        public Camera orbCamera;

        // 소리 크기와 햅틱 설정은 LabConfig(Inspector: Resources/LabConfig, DEV: Sound and haptics)에 있다.

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
            // 소리·햅틱은 장면 수정 없이 여기에 붙인다. 크기·햅틱 설정은 모두 boardNetwork.config(LabConfig)에서 읽는다.
            // 구슬 생성 효과음 (내 구슬이 떠오를 때)
            LabGenerateSound generateSound = GetComponent<LabGenerateSound>();
            if (board != null && generateSound == null)
            {
                generateSound = gameObject.AddComponent<LabGenerateSound>();
                generateSound.Bind(board);
            }
            if (generateSound != null) generateSound.BindConfig(boardNetwork);
            // 전투 BGM (전투 상태를 읽을 네트워크 연결)
            if (boardNetwork != null && GetComponent<LabBattleBgm>() == null)
                gameObject.AddComponent<LabBattleBgm>().Bind(boardNetwork);
            // 던진 구슬의 결과 소리 (맞음·빗나감, 던진 사람 기기만)
            if (boardNetwork != null && GetComponent<LabThrowSound>() == null)
                gameObject.AddComponent<LabThrowSound>().Bind(boardNetwork);
            // 햅틱 (내 구슬 생성, 내가 던진 구슬 타격)
            if (boardNetwork != null && GetComponent<LabHaptics>() == null)
                gameObject.AddComponent<LabHaptics>().Bind(boardNetwork, board);
            // 일시정지 메뉴에 BGM·효과음 크기 슬라이더를 붙인다. 값은 기기마다 따로 저장된다.
            LabHud hud = FindFirstObjectByType<LabHud>();
            if (hud != null && hud.pauseMenu != null && GetComponent<LabAudioSettingsPanel>() == null)
                gameObject.AddComponent<LabAudioSettingsPanel>().Bind(hud.pauseMenu, generateSound);
            // 승리/패배 결과 화면 위에 이번 라운드 기록·MVP 대시보드를 붙인다.
            if (hud != null && hud.resultControls != null && boardNetwork != null
                && GetComponent<LabResultDashboard>() == null)
                gameObject.AddComponent<LabResultDashboard>().Bind(boardNetwork, hud.resultControls,
                    hud.resultStatusText != null ? hud.resultStatusText.font : null);
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
