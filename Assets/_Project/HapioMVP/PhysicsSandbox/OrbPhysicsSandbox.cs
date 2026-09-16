using System;
using System.Collections.Generic;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace C6.Prototype.PhysicsSandbox
{
    /// <summary>Offline two-screen tuning harness using the game's existing P4 orb physics and transfer rules.</summary>
    [DisallowMultipleComponent]
    public sealed class OrbPhysicsSandbox : MonoBehaviour
    {
        private const float ArtworkRadius = .5f;
        private const float LabelPixels = 12f;
        public ScreenLayoutConfig sourceConfig;
        public Material orbMaterial;
        public Camera sandboxCamera;
        public OrbSandboxSeed[] seeds = Array.Empty<OrbSandboxSeed>();
        public OrbSandboxTuning tuning = new OrbSandboxTuning();
        [SerializeField, HideInInspector] private string savedTuningBaseline;
        public bool portalsEnabled = true;
        public Rect leftWorkspace = new Rect(-7f, -3.5f, 6f, 7f);
        public Rect rightWorkspace = new Rect(1f, -3.5f, 6f, 7f);
        public bool showHelp = true;
        [Tooltip("Matches the current game's lower viewport, safe area and 144-point connected-game footer.")]
        public bool matchGameArea;
        [Range(0, 1)] public int visibleBoard;
        public int TouchSamples { get; private set; }
        public string LastInput { get; private set; } = "Waiting";
        private int layoutWidth, layoutHeight, layoutBoard = -1;
        private bool layoutMatched;
        private Rect layoutSafeArea;
        private float layoutFraction;

        private readonly LocalOrbPhysicsBoard[] boards = new LocalOrbPhysicsBoard[2];
        private readonly Dictionary<string, OrbSandboxSeed> byId = new Dictionary<string, OrbSandboxSeed>();
        private readonly Queue<QueuedCrossing> crossings = new Queue<QueuedCrossing>();
        private Vector3[] startingPositions;
        private OrbSandboxTuning appliedTuning;
        private Rect appliedLeft, appliedRight, appliedPixelRect;
        private float appliedCameraSize;
        private bool appliedPortals;
        private OrbSandboxSeed held;
        private Vector2 pointerOffset;
        private bool touchPointer, focusPaused;
        private int activeTouchId;

        public bool IsReady { get; private set; }
        public int TransferCount { get; private set; }
        public int LastTransferSource { get; private set; } = -1;
        public int LastTransferDestination { get; private set; } = -1;
        public Vector2 LastTransferVelocityBefore { get; private set; }
        public Vector2 LastTransferVelocityAfter { get; private set; }

        private readonly struct QueuedCrossing
        {
            public readonly int Board;
            public readonly OrbEdgeCrossing Crossing;
            public readonly double Time;
            public QueuedCrossing(int board, OrbEdgeCrossing crossing, double time)
            { Board = board; Crossing = crossing; Time = time; }
        }

        public void Configure(ScreenLayoutConfig config, Material material, Camera camera, OrbSandboxSeed[] sceneSeeds)
        {
            if (IsReady) throw new InvalidOperationException("Configure the sandbox before it starts.");
            sourceConfig = config;
            orbMaterial = material;
            sandboxCamera = camera;
            seeds = sceneSeeds ?? Array.Empty<OrbSandboxSeed>();
            if (config != null)
            {
                tuning = OrbSandboxTuning.Capture(config);
                savedTuningBaseline = JsonUtility.ToJson(tuning);
            }
            if (Application.isPlaying && isActiveAndEnabled) Initialize();
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (IsReady || !Application.isPlaying || sandboxCamera == null || seeds == null || seeds.Length == 0) return;
            if (!sandboxCamera.orthographic)
                throw new InvalidOperationException("The sandbox needs an orthographic camera.");
            if (tuning == null) tuning = sourceConfig != null ? OrbSandboxTuning.Capture(sourceConfig) : new OrbSandboxTuning();
            if (sourceConfig != null)
            {
                var saved = OrbSandboxTuning.Capture(sourceConfig);
                // A saved tuning update wins over this scene's older serialized working values.
                // Otherwise preserve deliberate Edit Mode adjustments for the next Play.
                if (JsonUtility.ToJson(saved) != savedTuningBaseline) tuning = saved;
            }
            if (orbMaterial != null) OrbView.SetSharedMaterial(orbMaterial);
            RefreshCameraLayout();
            startingPositions = new Vector3[seeds.Length];
            for (int i = 0; i < 2; i++)
            {
                var boardObject = new GameObject(i == 0 ? "Left Physics Board" : "Right Physics Board");
                boardObject.transform.SetParent(transform, false);
                boards[i] = boardObject.AddComponent<LocalOrbPhysicsBoard>();
            }
            boards[0].EdgeCrossed += QueueLeftCrossing;
            boards[1].EdgeCrossed += QueueRightCrossing;
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed == null) continue;
                startingPositions[i] = seed.transform.position;
                seed.CurrentBoard = Mathf.Clamp(seed.initialBoard, 0, 1);
                if (seed.previewArtwork != null) seed.previewArtwork.gameObject.SetActive(false);
                seed.View = seed.GetComponent<OrbView>();
                if (seed.View == null) seed.View = seed.gameObject.AddComponent<OrbView>();
                seed.View.Configure("sandbox-" + Guid.NewGuid().ToString("N") + "-" + i, OrbKind.Raw, seed.polarity,
                    seed.gameObject.layer, ArtworkRadius);
                seed.View.SetHeldFeedbackEnabled(true);
                byId.Add(seed.View.OrbId, seed);
            }
            IsReady = true;
            ApplyTuning();
            ResetOrbs();
        }

        public LocalOrbPhysicsBoard GetBoard(int index)
        {
            if (index < 0 || index > 1) throw new ArgumentOutOfRangeException(nameof(index));
            return boards[index];
        }

        public bool TryGetVelocity(OrbSandboxSeed seed, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            return IsReady && seed != null && seed.View != null &&
                boards[seed.CurrentBoard].TryGetVelocity(seed.View.OrbId, out velocity);
        }

        public void ApplyTuning()
        {
            if (tuning == null) tuning = new OrbSandboxTuning();
            tuning.Sanitize();
            leftWorkspace = ValidWorkspace(leftWorkspace, new Rect(-7f, -3.5f, 6f, 7f));
            rightWorkspace = ValidWorkspace(rightWorkspace, new Rect(1f, -3.5f, 6f, 7f));
            if (!IsReady)
            {
                foreach (var seed in seeds)
                {
                    if (seed == null) continue;
                    float radius = Radius(Mathf.Clamp(seed.initialBoard, 0, 1));
                    if (seed.previewArtwork != null) seed.previewArtwork.localScale = Vector3.one * (radius / ArtworkRadius);
                    var collider = seed.GetComponent<CircleCollider2D>();
                    if (collider != null) collider.radius = radius;
                }
                return;
            }
            CancelGrab();
            crossings.Clear();
            foreach (var seed in seeds) if (seed != null && seed.View != null) ApplyViewGeometry(seed);
            for (int i = 0; i < 2; i++)
            {
                var workspace = Workspace(i);
                boards[i].ConfigureHorizontalPassage(portalsEnabled, workspace.width);
                boards[i].Configure(CenterBounds(i), Radius(i), tuning.ToPhysics(workspace.width));
            }
            appliedTuning = tuning.Copy();
            appliedLeft = leftWorkspace;
            appliedRight = rightWorkspace;
            appliedPortals = portalsEnabled;
            appliedPixelRect = sandboxCamera.pixelRect;
            appliedCameraSize = sandboxCamera.orthographicSize;
        }

        public void ReloadSavedTuning()
        {
            if (sourceConfig == null) return;
            tuning = OrbSandboxTuning.Capture(sourceConfig);
            savedTuningBaseline = JsonUtility.ToJson(tuning);
            ApplyTuning();
        }

        public void ResetOrbs()
        {
            if (!IsReady) return;
            CancelGrab();
            crossings.Clear();
            boards[0].Clear(); boards[1].Clear();
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed == null || seed.View == null) continue;
                seed.CurrentBoard = Mathf.Clamp(seed.initialBoard, 0, 1);
                seed.transform.position = startingPositions[i];
                ApplyViewGeometry(seed);
                seed.View.SetLocalState(LocalOrbState.Idle);
                boards[seed.CurrentBoard].Register(seed.View);
                boards[seed.CurrentBoard].SetPosition(seed.View.OrbId, startingPositions[i], false, Time.unscaledTimeAsDouble);
            }
            TransferCount = 0;
            LastTransferSource = LastTransferDestination = -1;
            LastTransferVelocityBefore = LastTransferVelocityAfter = Vector2.zero;
        }

        public void StopOrbs()
        {
            if (!IsReady) return;
            CancelGrab();
            crossings.Clear();
            foreach (var board in boards)
            {
                board.SetPaused(true);
                board.SetPaused(focusPaused || !isActiveAndEnabled);
            }
        }

        private void Update()
        {
            if (!IsReady) return;
            RefreshCameraLayout();
            if (tuning == null || !tuning.SameAs(appliedTuning) || leftWorkspace != appliedLeft ||
                rightWorkspace != appliedRight || portalsEnabled != appliedPortals ||
                sandboxCamera.pixelRect != appliedPixelRect || sandboxCamera.orthographicSize != appliedCameraSize)
                ApplyTuning();
            if (focusPaused) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetOrbs();
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) StopOrbs();
            ReadPointer();
        }

        private void QueueLeftCrossing(OrbEdgeCrossing crossing) => QueueCrossing(0, crossing);
        private void QueueRightCrossing(OrbEdgeCrossing crossing) => QueueCrossing(1, crossing);
        private void QueueCrossing(int board, OrbEdgeCrossing crossing) =>
            crossings.Enqueue(new QueuedCrossing(board, crossing, Time.timeAsDouble));

        private void LateUpdate()
        {
            if (!IsReady) return;
            // Both boards finish every fixed step before any ownership switch. A transferred orb
            // therefore cannot receive a second FixedUpdate friction step from its destination.
            while (crossings.Count > 0)
            {
                var pending = crossings.Dequeue();
                var crossing = pending.Crossing;
                if (!byId.TryGetValue(crossing.OrbId, out var seed) || seed == null ||
                    seed.CurrentBoard != pending.Board || !boards[pending.Board].TryGetPendingEdge(crossing.OrbId, out _)) continue;
                if (!portalsEnabled || focusPaused)
                { boards[pending.Board].ResolveRejectedEdge(crossing.OrbId); continue; }
                int destination = 1 - pending.Board;
                var motion = OrbTransferMotion.Decay(new OrbTransferMotion(crossing.VelocityBoardWidthsPerSecond, pending.Time),
                    Math.Max(pending.Time, Time.timeAsDouble), tuning.orbFloorDeceleration, tuning.orbStopSpeed);
                boards[pending.Board].Remove(crossing.OrbId);
                seed.CurrentBoard = destination;
                ApplyViewGeometry(seed);
                boards[destination].Register(seed.View);
                bool resumed = boards[destination].ResumeTransferred(crossing.OrbId, crossing.ToRight,
                    crossing.Height01, motion.Velocity);
                if (!resumed) throw new InvalidOperationException("Sandbox transfer could not resume on its destination.");
                TransferCount++;
                LastTransferSource = pending.Board;
                LastTransferDestination = destination;
                LastTransferVelocityBefore = crossing.VelocityBoardWidthsPerSecond;
                LastTransferVelocityAfter = motion.Velocity;
            }
        }

        private void ReadPointer()
        {
            var screen = Touchscreen.current;
            if (held != null && touchPointer)
            {
                TouchControl touch = null;
                if (screen != null)
                    foreach (var candidate in screen.touches)
                        if (candidate.touchId.ReadValue() == activeTouchId) { touch = candidate; break; }
                if (touch == null || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                { CancelGrab(); return; }
                Vector2 position = touch.position.ReadValue();
                TouchSamples++; LastInput = "Touch / Unity Remote";
                if (!ScreenPointAllowed(position)) { CancelGrab(); return; }
                if (touch.press.wasReleasedThisFrame || touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
                { MoveGrab(position); EndGrab(true); return; }
                if (!touch.press.isPressed) { CancelGrab(); return; }
                MoveGrab(position);
                return;
            }
            if (held == null && screen != null)
                foreach (var touch in screen.touches)
                    if (touch.press.wasPressedThisFrame)
                    {
                        BeginGrab(touch.position.ReadValue());
                        TouchSamples++; LastInput = "Touch / Unity Remote";
                        if (held != null) { touchPointer = true; activeTouchId = touch.touchId.ReadValue(); }
                        return;
                    }
            var mouse = Mouse.current;
            if (mouse == null) { if (held != null) CancelGrab(); return; }
            Vector2 mousePosition = mouse.position.ReadValue();
            if (held != null)
            {
                if (!ScreenPointAllowed(mousePosition)) { CancelGrab(); return; }
                MoveGrab(mousePosition);
                if (mouse.leftButton.wasReleasedThisFrame) EndGrab(true);
                else if (!mouse.leftButton.isPressed) CancelGrab();
            }
            else if (mouse.leftButton.wasPressedThisFrame) { LastInput = "Mouse"; BeginGrab(mousePosition); }
        }

        private void BeginGrab(Vector2 screenPosition)
        {
            if (!ScreenPointAllowed(screenPosition) || OverHelp(screenPosition) || !TryWorld(screenPosition, out var world)) return;
            float closest = float.PositiveInfinity;
            OrbSandboxSeed selected = null;
            foreach (var seed in seeds)
            {
                if (seed == null || seed.View == null || !seed.View.isActiveAndEnabled) continue;
                float distance = Vector2.Distance(world, seed.transform.position);
                if (distance <= Radius(seed.CurrentBoard) && distance < closest) { selected = seed; closest = distance; }
            }
            if (selected == null || !boards[selected.CurrentBoard].Grab(selected.View.OrbId, Time.unscaledTimeAsDouble)) return;
            held = selected;
            touchPointer = false;
            pointerOffset = (Vector2)held.transform.position - world;
            held.View.SetLocalState(LocalOrbState.Dragging);
        }

        private void MoveGrab(Vector2 screenPosition)
        {
            if (held == null || !TryWorld(screenPosition, out var world)) return;
            boards[held.CurrentBoard].SetPosition(held.View.OrbId, world + pointerOffset, true, Time.unscaledTimeAsDouble);
        }

        private void EndGrab(bool fling)
        {
            if (held == null) return;
            boards[held.CurrentBoard].Release(held.View.OrbId, Time.unscaledTimeAsDouble, fling);
            held.View.SetLocalState(LocalOrbState.Idle);
            held = null;
            touchPointer = false;
        }

        private void CancelGrab() => EndGrab(false);
        private bool ScreenPointAllowed(Vector2 point) => sandboxCamera != null && sandboxCamera.pixelRect.Contains(point);
        private bool TryWorld(Vector2 screen, out Vector2 world)
        {
            world = default;
            Ray ray = sandboxCamera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out var distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

        private Rect Workspace(int index) => index == 0 ? leftWorkspace : rightWorkspace;
        private float Radius(int index) => Mathf.Min(Workspace(index).width * tuning.orbRadiusScreenFraction,
            Workspace(index).height * .28f);
        private void ApplyViewGeometry(OrbSandboxSeed seed)
        {
            float scale = Radius(seed.CurrentBoard) / ArtworkRadius;
            Vector3 parentScale = seed.transform.parent == null ? Vector3.one : seed.transform.parent.lossyScale;
            seed.transform.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            seed.View.SetLabelPixelHeight(sandboxCamera, LabelPixels);
        }

        private Rect CenterBounds(int index)
        {
            Rect space = Workspace(index);
            float radius = Radius(index);
            float unitsPerPixel = 2f * sandboxCamera.orthographicSize / Mathf.Max(1f, sandboxCamera.pixelRect.height);
            float horizontal = radius;
            foreach (var seed in seeds)
                if (seed != null && seed.View != null)
                    horizontal = Mathf.Max(horizontal, seed.View.LabelHalfWidthWorld + 2f * unitsPerPixel);
            float bottom = Mathf.Max(radius * 1.8f, radius * 1.72f + (LabelPixels * .5f + 2f) * unitsPerPixel);
            horizontal = Mathf.Min(horizontal, space.width * .45f);
            bottom = Mathf.Min(bottom, space.height * .45f);
            return Rect.MinMaxRect(space.xMin + horizontal, space.yMin + bottom,
                space.xMax - horizontal, space.yMax - radius);
        }

        private static Rect ValidWorkspace(Rect value, Rect fallback)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.width) || !Finite(value.height) ||
                value.width < .25f || value.height < .25f) return fallback;
            return value;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>The same connected-game HUD calculation used by T09Hud: safe lower viewport,
        /// CanvasScaler 390x844 with .5 match, FitContent minimum height 280, footer height 144.</summary>
        public static Rect GameWorkspacePixels(int width, int height, Rect safe, float lowerFraction)
        {
            float scale = Mathf.Sqrt(Mathf.Max(1f, width) / 390f * Mathf.Max(1f, height) / 844f);
            float top = Mathf.Min(safe.yMax, height * lowerFraction);
            float bottom = Mathf.Min(top, safe.yMin);
            float regionHeight = Mathf.Max(1f, top - bottom);
            float fit = Mathf.Max(.01f, Mathf.Min(1f, Mathf.Min(safe.width / scale, 580f) / 320f,
                regionHeight / scale / 280f));
            float footerTop = Mathf.Min(top, bottom + 144f * scale * fit);
            return new Rect(safe.xMin, footerTop, safe.width, Mathf.Max(1f, top - footerTop));
        }

        private void RefreshCameraLayout()
        {
            if (sandboxCamera == null || Screen.width < 1 || Screen.height < 1) return;
            float fraction = sourceConfig != null ? sourceConfig.LowerFraction : .45f;
            if (layoutWidth == Screen.width && layoutHeight == Screen.height && layoutMatched == matchGameArea &&
                layoutBoard == visibleBoard && layoutSafeArea == Screen.safeArea && layoutFraction == fraction) return;
            bool geometryChanged = layoutWidth != Screen.width || layoutHeight != Screen.height ||
                layoutMatched != matchGameArea || layoutSafeArea != Screen.safeArea || layoutFraction != fraction;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutMatched = matchGameArea;
            layoutBoard = visibleBoard; layoutSafeArea = Screen.safeArea; layoutFraction = fraction;
            if (matchGameArea)
            {
                Rect pixels = GameWorkspacePixels(Screen.width, Screen.height, Screen.safeArea, fraction);
                float height = Mathf.Max(.3f, pixels.height / Mathf.Max(1f, pixels.width) * 6f);
                leftWorkspace = new Rect(-7f, -height / 2f, 6f, height);
                rightWorkspace = new Rect(1f, -height / 2f, 6f, height);
                sandboxCamera.pixelRect = pixels;
                sandboxCamera.orthographicSize = height / 2f;
                var center = Workspace(Mathf.Clamp(visibleBoard, 0, 1)).center;
                sandboxCamera.transform.position = new Vector3(center.x, center.y, -10f);
            }
            else
            {
                sandboxCamera.rect = new Rect(0, 0, 1, 1);
                sandboxCamera.orthographicSize = Mathf.Max(5.6f, 8f / Mathf.Max(.1f, sandboxCamera.aspect));
                sandboxCamera.transform.position = new Vector3(0, 0, -10f);
            }
            RefreshBoardArtwork("Board A - visual boundaries", leftWorkspace);
            RefreshBoardArtwork("Board B - visual boundaries", rightWorkspace);
            if (IsReady && geometryChanged) { ApplyTuning(); ResetOrbs(); }
        }

        private void RefreshBoardArtwork(string objectName, Rect bounds)
        {
            var board = transform.Find(objectName);
            if (board == null) return;
            SetPart("Backdrop", bounds.center, bounds.size);
            SetPart("Top wall", new Vector2(bounds.center.x, bounds.yMax), new Vector2(bounds.width, .035f));
            SetPart("Bottom wall", new Vector2(bounds.center.x, bounds.yMin), new Vector2(bounds.width, .035f));
            SetPart("Left portal edge", new Vector2(bounds.xMin, bounds.center.y), new Vector2(.035f, bounds.height));
            SetPart("Right portal edge", new Vector2(bounds.xMax, bounds.center.y), new Vector2(.035f, bounds.height));
            void SetPart(string partName, Vector2 position, Vector2 size)
            {
                var part = board.Find(partName);
                if (part == null) return;
                part.localPosition = position;
                part.localScale = new Vector3(size.x, size.y, 1);
            }
        }

        private Rect HelpRect => new Rect(12f, 12f, Mathf.Min(460f, Mathf.Max(160f, Screen.width - 24f)), 136f);
        private bool OverHelp(Vector2 point) => showHelp && HelpRect.Contains(new Vector2(point.x, Screen.height - point.y));
        private void OnGUI()
        {
            if (!showHelp || !IsReady) return;
            Rect rect = HelpRect;
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 12, rect.y + 7, rect.width - 24, 22), "ORB PHYSICS SANDBOX   |   Transfers: " + TransferCount);
            GUI.Label(new Rect(rect.x + 12, rect.y + 29, rect.width - 24, 22), "Drag & release. Tune in Mac Inspector.");
            GUI.Label(new Rect(rect.x + 12, rect.y + 49, rect.width - 24, 22), portalsEnabled ?
                "Side edges pass to the other board; height and direction persist." : "Portals OFF: side walls bounce.");
            if (GUI.Button(new Rect(rect.x + 12, rect.y + 77, 125, 25), "Reset (R)")) ResetOrbs();
            if (GUI.Button(new Rect(rect.x + 147, rect.y + 77, 125, 25), "Stop (Space)")) StopOrbs();
            GUI.Label(new Rect(rect.x + 12, rect.y + 105, rect.width - 24, 22),
                (matchGameArea ? "Board " + (visibleBoard == 0 ? "A" : "B") + " / " : "Overview / ") + LastInput);
        }

        private void OnApplicationFocus(bool focused)
        {
            focusPaused = !focused;
            if (!IsReady) return;
            StopOrbs();
        }
        private void OnApplicationPause(bool paused)
        {
            focusPaused = paused;
            if (!IsReady) return;
            StopOrbs();
        }
        private void OnDisable()
        {
            if (!IsReady) return;
            StopOrbs();
        }
        private void OnEnable()
        {
            if (!IsReady) return;
            foreach (var board in boards) if (board != null) board.SetPaused(focusPaused);
        }
        private void OnDestroy()
        {
            if (boards[0] != null) boards[0].EdgeCrossed -= QueueLeftCrossing;
            if (boards[1] != null) boards[1].EdgeCrossed -= QueueRightCrossing;
            crossings.Clear();
            IsReady = false;
        }
    }
}
