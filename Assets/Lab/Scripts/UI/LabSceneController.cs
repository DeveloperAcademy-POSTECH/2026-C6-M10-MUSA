using UnityEngine;

namespace C6Lab
{
    [DefaultExecutionOrder(-100)]
    public sealed class LabSceneController : MonoBehaviour
    {
        public LabConfig config;
        public LabOrbBoard board;
        public Camera orbCamera;
        [Header("Saved combination area (Canvas screen coordinates)")]
        public RectTransform orbInteractionArea;
        public RectTransform lowerHudBoundary;
        [Min(0f)] public float lowerHudGap = 4f;

        private readonly Vector3[] areaCorners = new Vector3[4];
        private bool boardConfigured;
        private Rect appliedScreenArea;
        private string lastAreaError;

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
            Canvas.ForceUpdateCanvases();
            if (!TryBoardBounds(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out Rect screenArea, out Rect centreBounds, out string error))
            {
                ReportAreaError(error);
                return;
            }
            FitPlateToCamera();
            board.Configure(config, orbCamera, centreBounds);
            board.SetInputScreenRect(screenArea);
            appliedScreenArea = screenArea;
            boardConfigured = true;
            lastAreaError = null;
        }

        // Layout changes resize the physical region without resetting moving or pending orbs.
        // A held gesture finishes using its old region, then the next LateUpdate applies the resize.
        private void LateUpdate()
        {
            if (!boardConfigured || config == null || board == null || orbCamera == null) return;
            if (!TryBoardBounds(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out Rect screenArea, out Rect centreBounds, out string error))
            {
                ReportAreaError(error);
                return;
            }
            if (Approximately(centreBounds, board.CenterBounds) && Approximately(screenArea, appliedScreenArea))
                return;
            if (!board.TryResizeBounds(centreBounds)) return;
            board.SetInputScreenRect(screenArea);
            appliedScreenArea = screenArea;
            FitPlateToCamera();
            lastAreaError = null;
        }

        public bool ValidateBoardTuning(float boardHeight, float orbRadius, float drumRadius, out string error)
        {
            Canvas.ForceUpdateCanvases();
            return TryBoardBounds(boardHeight, orbRadius, drumRadius, out _, out _, out error);
        }

        private bool TryBoardBounds(float boardHeight, float orbRadius, float drumRadius,
            out Rect screenArea, out Rect centreBounds, out string error)
        {
            screenArea = default;
            centreBounds = default;
            error = "The orb camera is not connected in the saved Scene.";
            if (orbCamera == null) return false;
            screenArea = orbCamera.pixelRect;
            // Small test fixtures and legacy scene generators can still use the complete viewport.
            // The saved Lab Scene explicitly supplies the editable area and HUD references.
            if (orbInteractionArea != null)
            {
                Canvas canvas = orbInteractionArea.GetComponentInParent<Canvas>(true);
                if (canvas == null) { error = "The orb interaction area must belong to a Canvas."; return false; }
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                screenArea = LabOrbAreaGeometry.ScreenRect(orbInteractionArea, uiCamera, areaCorners);
                if (lowerHudBoundary != null)
                {
                    Rect hud = LabOrbAreaGeometry.ScreenRect(lowerHudBoundary, uiCamera, areaCorners);
                    if (hud.xMin < screenArea.xMax && hud.xMax > screenArea.xMin)
                        screenArea.yMin = Mathf.Max(screenArea.yMin,
                            hud.yMax + Mathf.Max(0f, lowerHudGap) * canvas.scaleFactor);
                }
            }
            Rect viewport = orbCamera.pixelRect;
            screenArea = Rect.MinMaxRect(Mathf.Max(screenArea.xMin, viewport.xMin),
                Mathf.Max(screenArea.yMin, viewport.yMin), Mathf.Min(screenArea.xMax, viewport.xMax),
                Mathf.Min(screenArea.yMax, viewport.yMax));
            return LabOrbAreaGeometry.TryCenterBounds(orbCamera, screenArea, boardHeight,
                orbRadius, drumRadius, out centreBounds, out error);
        }

        private void FitPlateToCamera()
        {
            float halfHeight = orbCamera.orthographicSize;
            float halfWidth = halfHeight * orbCamera.aspect;
            // The plate follows the visible board when the Inspector or DEV tuning
            // changes its height. It is render-only; the physics plane stays at z=0.
            Transform plate = board.transform.Find("OrbBoardPlate");
            if (plate != null)
                plate.localScale = new Vector3(halfWidth * 2f, halfHeight * 2f, 1f);
        }

        private void ReportAreaError(string error)
        {
            if (lastAreaError == error) return;
            lastAreaError = error;
            Debug.LogWarning("C6_LAB_AREA_INVALID " + error, this);
        }

        private static bool Approximately(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) < .0001f && Mathf.Abs(a.xMax - b.xMax) < .0001f
            && Mathf.Abs(a.yMin - b.yMin) < .0001f && Mathf.Abs(a.yMax - b.yMax) < .0001f;
    }
}
