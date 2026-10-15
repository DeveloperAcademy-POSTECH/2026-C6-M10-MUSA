using UnityEngine;

namespace C6Lab
{
    [DefaultExecutionOrder(-100)]
    public sealed class LabSceneController : MonoBehaviour
    {
        public LabConfig config;
        public LabOrbBoard board;
        public Camera orbCamera;
        [Header("Nonvisual combination-area reference (paper keeps the camera viewport)")]
        public RectTransform orbInteractionArea;
        public RectTransform lowerHudBoundary;
        [Tooltip("Legacy rectangular fixtures only; the saved curved Lab follows the actual HUD alpha with no extra gap.")]
        [Min(0f)] public float lowerHudGap = 0f;
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
            // 박 페인트 (#153): 요괴 자리에 흰 박을 붙이고 승인된 타격마다 속성 색을 칠한다.
            // 다른 컴포넌트의 Start보다 먼저 붙여야 크랙 박(#163)이 박 모델을 다시 붙이지 않는다.
            if (LabGourdPaint.EnabledInMainScene) LabGourdPaint.Attach(boardNetwork);
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
            FitPlateToCamera();
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
            FitPlateToCamera();
            lastAreaError = null;
        }

        public bool ValidateBoardTuning(float boardHeight, float orbRadius, float drumRadius, out string error)
        {
            Canvas.ForceUpdateCanvases();
            return TryBoardArea(boardHeight, orbRadius, drumRadius, out _, out _, out _, out error);
        }

        /// <summary>Fits the existing Quad exactly as #154 did; never moves it to make room for HUD artwork.</summary>
        public bool FitPaperPlate(out string error)
        {
            if (config == null) { error = "The Lab Config is missing."; return false; }
            Canvas.ForceUpdateCanvases();
            if (!TryBoardArea(config.BoardHeight, config.OrbRadius, config.DrumRadius,
                out _, out Rect plateRect, out _, out error)) return false;
            FitPlateToCamera();
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
            if (curvedAreaProfile != null)
            {
                // The paper remains the full lower-camera Quad. Only the physics shape is
                // intersected with the already-rendered lower HUD's actual opaque contour.
                if (orbInteractionArea == null || lowerHudBoundary == null)
                {
                    error = "The saved curved area requires its nonvisual guide and lower HUD references.";
                    return false;
                }
                Canvas canvas = lowerHudBoundary.GetComponentInParent<Canvas>(true);
                UnityEngine.UI.Image hudImage = lowerHudBoundary.GetComponent<UnityEngine.UI.Image>();
                if (canvas == null || hudImage == null || hudImage.sprite == null
                    || hudImage.sprite.texture != curvedAreaProfile.LowerOccluderTexture
                    || hudImage.type != UnityEngine.UI.Image.Type.Simple || hudImage.preserveAspect)
                {
                    error = "The lower HUD must use its baked, non-aspect-preserving Simple Image sprite.";
                    return false;
                }
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Rect hudScreen = LabOrbAreaGeometry.ScreenRect(lowerHudBoundary, uiCamera, areaCorners);
                if (!TryPaperWorldRect(boardHeight, out plateRect)
                    || !TryWorldRect(hudScreen, boardHeight, out Rect hudWorld))
                {
                    error = "The paper and lower HUD require valid orthographic screen-to-world rectangles.";
                    return false;
                }
                return curvedAreaProfile.TryBuildShape(plateRect, hudWorld, orbRadius, drumRadius,
                    out shape, out error);
            }
            // Legacy test fixtures keep their previous rectangle semantics. The saved Lab
            // requires the curve profile above and can never silently enter this fallback.
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
            if (!TryWorldRect(screenArea, boardHeight, out plateRect))
            {
                error = "The legacy paper layout must have a positive size inside the orthographic orb camera.";
                return false;
            }
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

        private bool TryPaperWorldRect(float boardHeight, out Rect rect)
        {
            rect = default;
            if (board == null || !orbCamera.orthographic || boardHeight <= 0f) return false;
            float halfHeight = boardHeight * .5f;
            float halfWidth = halfHeight * orbCamera.aspect;
            // Use the same Quad transform as #154. Camera.pixelRect can contain fractional
            // pixels while screen projection uses integer pixel dimensions; projecting that
            // outer rectangle back would slightly stretch physics away from the drawn paper.
            Vector3 lower = board.transform.TransformPoint(new Vector3(-halfWidth, -halfHeight, 0f));
            Vector3 upper = board.transform.TransformPoint(new Vector3(halfWidth, halfHeight, 0f));
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

        private void FitPlateToCamera()
        {
            Transform plate = curvedAreaProfile != null ? curvedAreaProfile.transform
                : board.transform.Find("OrbBoardPlate");
            if (plate == null) return;
            float halfHeight = orbCamera.orthographicSize;
            float halfWidth = halfHeight * orbCamera.aspect;
            // #154 composition: full lower-camera Quad at the board origin, beneath the HUD.
            // A curved physics boundary must follow that composition, never resize the artwork.
            plate.localPosition = new Vector3(0f, 0f, plate.localPosition.z);
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
