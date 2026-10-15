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
        [Header("Paper silhouette (baked from the visible plate)")]
        public LabOrbAreaProfile curvedAreaProfile;
        public bool requireCurvedAreaProfile;

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
            if (!TryBoardArea(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out Rect screenArea, out Rect plateRect, out LabOrbAreaShape shape, out string error))
            {
                ReportAreaError(error);
                return;
            }
            FitPlate(plateRect);
            board.ConfigureArea(config, orbCamera, shape);
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
            if (!TryBoardArea(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out Rect screenArea, out Rect plateRect, out LabOrbAreaShape shape, out string error))
            {
                ReportAreaError(error);
                return;
            }
            if (shape.ApproximatelyEquals(board.AreaShape) && Approximately(screenArea, appliedScreenArea))
                return;
            if (!board.TryResizeArea(shape)) return;
            board.SetInputScreenRect(screenArea);
            appliedScreenArea = screenArea;
            FitPlate(plateRect);
            lastAreaError = null;
        }

        public bool ValidateBoardTuning(float boardHeight, float orbRadius, float drumRadius, out string error)
        {
            Canvas.ForceUpdateCanvases();
            return TryBoardArea(boardHeight, orbRadius, drumRadius, out _, out _, out _, out error);
        }

        /// <summary>Editor layout helper: moves the existing paper Quad without creating physics objects.</summary>
        public bool FitPaperPlate(out string error)
        {
            if (config == null) { error = "The Lab Config is missing."; return false; }
            Canvas.ForceUpdateCanvases();
            if (!TryBoardArea(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out _, out Rect plateRect, out _, out error)) return false;
            FitPlate(plateRect);
            return true;
        }

        private bool TryBoardArea(float boardHeight, float orbRadius, float drumRadius,
            out Rect screenArea, out Rect plateRect, out LabOrbAreaShape shape, out string error)
        {
            screenArea = default;
            plateRect = default;
            shape = null;
            error = "The orb camera is not connected in the saved Scene.";
            if (orbCamera == null) return false;
            if (requireCurvedAreaProfile && curvedAreaProfile == null)
            {
                error = "The saved Lab Scene requires its baked paper curve profile. Run C6 Lab/Fit Curved Orb Area.";
                return false;
            }
            if (curvedAreaProfile != null && (board == null || curvedAreaProfile.transform.parent != board.transform))
            {
                error = "The curved area profile must be attached to this board's existing OrbBoardPlate.";
                return false;
            }
            screenArea = orbCamera.pixelRect;
            // Legacy test fixtures keep their full rectangular viewport. The saved Lab explicitly
            // requires the paper profile and uses this RectTransform only as the Quad's layout box.
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
            else if (curvedAreaProfile != null)
            {
                error = "The saved curved paper area requires its Canvas layout guide.";
                return false;
            }
            Rect viewport = orbCamera.pixelRect;
            screenArea = Rect.MinMaxRect(Mathf.Max(screenArea.xMin, viewport.xMin),
                Mathf.Max(screenArea.yMin, viewport.yMin), Mathf.Min(screenArea.xMax, viewport.xMax),
                Mathf.Min(screenArea.yMax, viewport.yMax));
            if (!TryWorldRect(screenArea, boardHeight, out plateRect))
            {
                error = "The paper layout must have a positive size inside the orthographic orb camera.";
                return false;
            }
            if (curvedAreaProfile != null)
                return curvedAreaProfile.TryBuildShape(plateRect, orbRadius, drumRadius, out shape, out error);
            if (!LabOrbAreaGeometry.TryCenterBounds(orbCamera, screenArea, boardHeight,
                orbRadius, drumRadius, out Rect centreBounds, out error)) return false;
            shape = LabOrbAreaShape.FromCenterBounds(centreBounds, orbRadius);
            return true;
        }

        private bool TryWorldRect(Rect screenArea, float boardHeight, out Rect rect)
        {
            rect = default;
            if (!orbCamera.orthographic || !LabOrbAreaGeometry.Valid(screenArea)
                || boardHeight <= 0f || orbCamera.orthographicSize <= 0f) return false;
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!Project(plane, screenArea.min, out Vector2 lower)
                || !Project(plane, screenArea.max, out Vector2 upper)
                || !Project(plane, orbCamera.pixelRect.center, out Vector2 centre)) return false;
            // DEV validates a candidate height without changing the camera or saved Config.
            float ratio = boardHeight / (orbCamera.orthographicSize * 2f);
            lower = centre + (lower - centre) * ratio;
            upper = centre + (upper - centre) * ratio;
            rect = Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            return LabOrbAreaGeometry.Valid(rect);
        }

        private bool Project(Plane plane, Vector2 screen, out Vector2 world)
        {
            world = default;
            Ray ray = orbCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance) || distance < 0f) return false;
            Vector3 point = ray.GetPoint(distance);
            world = new Vector2(point.x, point.y);
            return !float.IsNaN(world.x) && !float.IsInfinity(world.x)
                && !float.IsNaN(world.y) && !float.IsInfinity(world.y);
        }

        private void FitPlate(Rect plateRect)
        {
            Transform plate = curvedAreaProfile != null ? curvedAreaProfile.transform
                : board.transform.Find("OrbBoardPlate");
            if (plate == null) return;
            // Quad and the physics profile share exactly the same UV-to-world mapping.
            // Only its Y position/height change for the lower HUD; X extent and camera stay fixed.
            Vector3 position = plate.position;
            plate.position = new Vector3(plateRect.center.x, plateRect.center.y, position.z);
            Vector3 parentScale = plate.parent != null ? plate.parent.lossyScale : Vector3.one;
            plate.localScale = new Vector3(plateRect.width / Mathf.Abs(parentScale.x),
                plateRect.height / Mathf.Abs(parentScale.y), plate.localScale.z);
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
