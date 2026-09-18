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
        private int combinedCycle, rawCycle;
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
        [Tooltip("ON: 실제 게임 영역 대신 기획 시안처럼 화면의 약 절반을 구슬 영역으로 사용합니다. '실제 게임 조합대 영역'이 켜져 있을 때만 적용됩니다.")]
        public bool useHalfScreenArea = true;
        [Tooltip("시안 구슬 영역. 전체 화면 대비 비율이며 아래쪽이 0입니다 (x, y, 너비, 높이).")]
        public Rect halfScreenArea = new Rect(.028f, .058f, .944f, .487f);
        [Tooltip("ON: 게임 코드(T09BattleController.RadiusPixels)와 같은 5x4 그리드 기준 반지름 상한을 적용합니다.")]
        public bool applyGameSizeCap = true;
        [Tooltip("조합대(구슬 영역) 배경색")]
        public Color boardColor = new Color(.18f, .32f, .38f, 1f);
        [Tooltip("조합대 바깥 배경색")]
        public Color outsideColor = new Color(.03f, .045f, .06f, 1f);
        public int CombineCount { get; private set; }
        public string LastCombineResult { get; private set; } = "-";
        [Range(0, 1)] public int visibleBoard;
        public int TouchSamples { get; private set; }
        /// <summary>마지막으로 레이아웃을 계산한 Game 화면 너비(px). Inspector에서는 Screen.width가 Inspector 창 너비라서 이 값을 쓴다.</summary>
        public int LayoutScreenWidth => layoutWidth;
        public string LastInput { get; private set; } = "Waiting";
        private int layoutWidth, layoutHeight, layoutBoard = -1;
        private bool layoutMatched, layoutHalf;
        private Rect layoutHalfArea;
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
            OrbView.SetArtwork(sourceConfig != null ? sourceConfig.OrbArt : null);
            OrbElements.Configure(sourceConfig != null ? sourceConfig.TeamElements : OrbElements.AllElements);
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
                seed.View.Configure("sandbox-" + Guid.NewGuid().ToString("N") + "-" + i, seed.kind,
                    seed.kind == OrbKind.Combined ? OrbPolarity.None : seed.polarity, seed.gameObject.layer, ArtworkRadius);
                seed.View.SetHeldFeedbackEnabled(true);
                byId.Add(seed.View.OrbId, seed);
            }
            IsReady = true;
            HideNonOrbElements();
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
                ApplyAreaColors();
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
            ApplyAreaColors();
            CancelGrab();
            crossings.Clear();
            foreach (var seed in AllSeeds) if (seed != null && seed.View != null) ApplyViewGeometry(seed);
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

        // ---------- 구슬 추가 / 제거 (Play 중) ----------
        public const int MaxOrbs = 20;
        private readonly List<OrbSandboxSeed> addedSeeds = new List<OrbSandboxSeed>();
        private readonly List<OrbSandboxSeed> hiddenSeeds = new List<OrbSandboxSeed>();

        private IEnumerable<OrbSandboxSeed> AllSeeds
        {
            get
            {
                foreach (var seed in seeds) yield return seed;
                foreach (var seed in addedSeeds) yield return seed;
            }
        }

        /// <summary>지금 두 조합대에 있는 구슬 수.</summary>
        public int OrbCount => IsReady ? boards[0].Count + boards[1].Count : 0;

        /// <summary>현재 표시 중인 조합대에 음/양 구슬을 하나 추가한다. 위치는 빈 곳 중 무작위.</summary>
        public bool AddOrb(OrbPolarity polarity) => AddOrb(OrbKind.Raw, polarity);

        public bool AddOrb(OrbKind kind, OrbPolarity polarity)
        {
            if (!IsReady || OrbCount >= MaxOrbs) return false;
            int board = Mathf.Clamp(visibleBoard, 0, 1);
            Rect bounds = CenterBounds(board);
            var position = new Vector3(UnityEngine.Random.Range(bounds.xMin, bounds.xMax),
                UnityEngine.Random.Range(bounds.yMin, bounds.yMax), 0f);
            string name = kind == OrbKind.Combined ? "Added COMB Orb " : polarity == OrbPolarity.Yin ? "Added Yin Orb " : "Added Yang Orb ";
            return CreateOrb(board, kind, kind == OrbKind.Combined ? OrbPolarity.None : polarity, position, name) != null;
        }

        private OrbSandboxSeed CreateOrb(int board, OrbKind kind, OrbPolarity polarity, Vector3 position, string namePrefix)
        {
            OrbSandboxSeed template = null;
            foreach (var seed in seeds) if (seed != null) { template = seed; break; }
            var go = new GameObject(namePrefix + (addedSeeds.Count + 1));
            if (template != null)
            {
                go.layer = template.gameObject.layer;
                go.transform.SetParent(template.transform.parent, false);
            }
            var added = go.AddComponent<OrbSandboxSeed>();
            added.initialBoard = board;
            added.kind = kind;
            added.polarity = polarity;
            added.CurrentBoard = board;
            go.transform.position = position;
            added.View = go.AddComponent<OrbView>();
            added.View.Configure(NewSandboxOrbId(kind), kind, polarity, go.layer, ArtworkRadius);
            added.View.SetHeldFeedbackEnabled(true);
            byId.Add(added.View.OrbId, added);
            addedSeeds.Add(added);
            ApplyViewGeometry(added);
            added.View.SetLocalState(LocalOrbState.Idle);
            boards[board].Register(added.View);
            boards[board].SetPosition(added.View.OrbId, position, false, Time.unscaledTimeAsDouble);
            return added;
        }

        /// <summary>
        /// 오행 확인용 ID. 결합은 (음, 양) 25조합을, 기본은 5속성을 차례로 돌려 버튼만 눌러도
        /// 모든 그림을 순서대로 볼 수 있게 한다. 결합 ID는 실제 판과 같은 인코딩을 쓴다.
        /// </summary>
        private string NewSandboxOrbId(OrbKind kind)
        {
            var all = OrbElements.AllElements;
            if (all.Length == 0) return "sandbox-added-" + Guid.NewGuid().ToString("N");
            if (kind == OrbKind.Combined)
            {
                var yin = all[(combinedCycle / all.Length) % all.Length];
                var yang = all[combinedCycle % all.Length];
                ++combinedCycle;
                return "sandbox-added-" + OrbElements.NewCombinedId(yin, yang);
            }
            var wanted = all[rawCycle % all.Length];
            ++rawCycle;
            for (int attempt = 0; attempt < 512; attempt++)
            {
                string candidate = "sandbox-added-" + Guid.NewGuid().ToString("N");
                if (OrbElements.RawElement(candidate) == wanted) return candidate;
            }
            return "sandbox-added-" + Guid.NewGuid().ToString("N");
        }

        /// <summary>마지막에 추가한 구슬부터 제거. 추가한 구슬이 없으면 씬에 놓인 구슬을 숨긴다(배치 초기화로 복구).</summary>
        public bool RemoveLastOrb()
        {
            if (!IsReady) return false;
            if (addedSeeds.Count > 0)
            {
                var last = addedSeeds[addedSeeds.Count - 1];
                addedSeeds.RemoveAt(addedSeeds.Count - 1);
                DetachOrb(last);
                if (last != null) Destroy(last.gameObject);
                return true;
            }
            for (int i = seeds.Length - 1; i >= 0; i--)
            {
                var seed = seeds[i];
                if (seed == null || seed.View == null || !seed.gameObject.activeSelf) continue;
                DetachOrb(seed);
                seed.gameObject.SetActive(false);
                hiddenSeeds.Add(seed);
                return true;
            }
            return false;
        }

        /// <summary>Play 중 추가한 구슬을 모두 제거한다.</summary>
        public void RemoveAddedOrbs()
        {
            if (!IsReady) return;
            DestroyAddedOrbs();
        }

        private void DestroyAddedOrbs()
        {
            foreach (var added in addedSeeds)
            {
                if (added == null) continue;
                DetachOrb(added);
                Destroy(added.gameObject);
            }
            addedSeeds.Clear();
        }

        private void DetachOrb(OrbSandboxSeed seed)
        {
            if (seed == null || seed.View == null) return;
            if (held == seed) CancelGrab();
            boards[seed.CurrentBoard].Remove(seed.View.OrbId);
            seed.View.SetLocalState(LocalOrbState.Idle);
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
            DestroyAddedOrbs();
            hiddenSeeds.Clear();
            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed == null || seed.View == null) continue;
                if (!seed.gameObject.activeSelf) seed.gameObject.SetActive(true);
                seed.CurrentBoard = Mathf.Clamp(seed.initialBoard, 0, 1);
                seed.transform.position = startingPositions[i];
                ApplyViewGeometry(seed);
                seed.View.SetLocalState(LocalOrbState.Idle);
                boards[seed.CurrentBoard].Register(seed.View);
                boards[seed.CurrentBoard].SetPosition(seed.View.OrbId, startingPositions[i], false, Time.unscaledTimeAsDouble);
            }
            TransferCount = 0;
            CombineCount = 0;
            LastCombineResult = "-";
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
#if ENABLE_LEGACY_INPUT_MANAGER
            // Unity Remote 5는 옛 입력(Input.touches)으로만 터치를 보내는 경우가 많다.
            // Active Input Handling = Both일 때 이 경로로 폰 터치를 받는다.
            if ((held == null || legacyTouch) && ReadLegacyTouch()) return;
#endif
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

#if ENABLE_LEGACY_INPUT_MANAGER
        private bool legacyTouch;

        /// <summary>옛 입력 시스템 터치 처리. 터치를 처리했으면 true(마우스 경로는 건너뜀).</summary>
        private bool ReadLegacyTouch()
        {
            int count = UnityEngine.Input.touchCount;
            if (!legacyTouch)
            {
                if (count == 0) return false;
                for (int i = 0; i < count; i++)
                {
                    var began = UnityEngine.Input.GetTouch(i);
                    if (began.phase != UnityEngine.TouchPhase.Began) continue;
                    TouchSamples++; LastInput = "Touch / Unity Remote (legacy)";
                    BeginGrab(began.position);
                    if (held != null) { legacyTouch = true; activeTouchId = began.fingerId; }
                    return true;
                }
                return true; // 빈 곳을 누르고 있는 중: 마우스 경로로 넘기지 않음
            }
            for (int i = 0; i < count; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.fingerId != activeTouchId) continue;
                TouchSamples++; LastInput = "Touch / Unity Remote (legacy)";
                if (touch.phase == UnityEngine.TouchPhase.Canceled || !ScreenPointAllowed(touch.position)) { CancelGrab(); return true; }
                MoveGrab(touch.position);
                if (touch.phase == UnityEngine.TouchPhase.Ended) EndGrab(true);
                return true;
            }
            CancelGrab(); // 손가락이 사라짐
            return true;
        }
#endif

        private void BeginGrab(Vector2 screenPosition)
        {
            if (!ScreenPointAllowed(screenPosition) || OverHelp(screenPosition) || !TryWorld(screenPosition, out var world)) return;
            float closest = float.PositiveInfinity;
            OrbSandboxSeed selected = null;
            foreach (var seed in AllSeeds)
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
            var source = held;
            // 게임과 동일: 정상적으로 놓았을 때 가까운 구슬이 있으면 결합 시도(관성 없음), 없으면 관성 유지.
            var target = fling ? NearestDropTarget(source) : null;
            boards[source.CurrentBoard].Release(source.View.OrbId, Time.unscaledTimeAsDouble, fling && target == null);
            source.View.SetLocalState(LocalOrbState.Idle);
            held = null;
            touchPointer = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            legacyTouch = false;
#endif
            if (target != null) TryCombine(source, target);
        }

        // ---------- 결합 (게임 규칙: 음 + 양 Raw만, 놓은 위치에서 화면 너비의 CombinationRadiusFraction 이내) ----------
        private float CombinationRadiusFraction => sourceConfig != null ? sourceConfig.CombinationRadiusFraction : .08f;

        private OrbSandboxSeed NearestDropTarget(OrbSandboxSeed source)
        {
            Vector2 center = sandboxCamera.WorldToScreenPoint(source.transform.position);
            float limit = CombinationRadiusFraction * Mathf.Max(1f, Screen.width);
            OrbSandboxSeed best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var other in AllSeeds)
            {
                if (other == null || other == source || other.View == null || !other.gameObject.activeInHierarchy
                    || other.CurrentBoard != source.CurrentBoard) continue;
                Vector2 position = sandboxCamera.WorldToScreenPoint(other.transform.position);
                float distance = Vector2.Distance(position, center);
                if (distance > limit + .01f || distance >= bestDistance) continue;
                best = other; bestDistance = distance;
            }
            return best;
        }

        private void TryCombine(OrbSandboxSeed source, OrbSandboxSeed target)
        {
            if (source.kind != OrbKind.Raw || target.kind != OrbKind.Raw)
            { LastCombineResult = "거절: 결합 구슬은 다시 결합할 수 없음"; return; }
            if (source.polarity == target.polarity)
            { LastCombineResult = "거절: 같은 극끼리는 결합 불가"; return; }
            int board = target.CurrentBoard;
            Vector3 middle = (source.transform.position + target.transform.position) * .5f;
            middle.z = 0f;
            RemoveOrb(source);
            RemoveOrb(target);
            CreateOrb(board, OrbKind.Combined, OrbPolarity.None, middle, "Combined Orb ");
            CombineCount++;
            LastCombineResult = "결합 성공 (음 + 양 → COMB)";
        }

        private void RemoveOrb(OrbSandboxSeed seed)
        {
            if (seed == null) return;
            DetachOrb(seed);
            if (addedSeeds.Remove(seed)) Destroy(seed.gameObject);
            else { seed.gameObject.SetActive(false); hiddenSeeds.Add(seed); }
        }

        // ---------- Play 화면 정리 / 영역 색 ----------
        private static readonly string[] NonOrbElements =
            { "Sandbox heading", "Board A title", "Board B title", "Portal route", "Scene instruction" };
        private static readonly string[] BoardDecorations =
            { "Top wall", "Bottom wall", "Left portal edge", "Right portal edge" };

        private void HideNonOrbElements()
        {
            showHelp = false;
            foreach (string name in NonOrbElements)
            {
                var child = transform.Find(name);
                if (child != null) child.gameObject.SetActive(false);
            }
            foreach (string boardName in new[] { "Board A - visual boundaries", "Board B - visual boundaries" })
            {
                var board = transform.Find(boardName);
                if (board == null) continue;
                foreach (string part in BoardDecorations)
                {
                    var child = board.Find(part);
                    if (child != null) child.gameObject.SetActive(false);
                }
            }
        }

        public void ApplyAreaColors()
        {
            var background = transform.Find("Background Camera");
            var backgroundCamera = background != null ? background.GetComponent<Camera>() : null;
            if (backgroundCamera != null) backgroundCamera.backgroundColor = outsideColor;
            if (sandboxCamera != null) sandboxCamera.backgroundColor = matchGameArea ? boardColor : outsideColor;
            foreach (string boardName in new[] { "Board A - visual boundaries", "Board B - visual boundaries" })
            {
                var board = transform.Find(boardName);
                var backdrop = board != null ? board.Find("Backdrop") : null;
                var renderer = backdrop != null ? backdrop.GetComponent<SpriteRenderer>() : null;
                if (renderer != null) renderer.color = boardColor;
            }
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
        private float Radius(int index)
        {
            Rect space = Workspace(index);
            float requested = space.width * tuning.orbRadiusScreenFraction;
            return applyGameSizeCap ? Mathf.Min(requested, GameSizeCap(space)) : Mathf.Min(requested, space.height * .28f);
        }

        /// <summary>게임 코드와 같은 상한: min(그리드 너비/5*.35, 그리드 높이/4*.28). (게임의 4pt 여백은 무시)</summary>
        private float GameSizeCap(Rect space)
        {
            float scale = tuning != null ? tuning.orbRadiusCapScale : 1f;
            return Mathf.Min(space.width / 5f * .35f, space.height / 4f * .28f) * scale;
        }

        /// <summary>Inspector 표시용 크기 정보. 비율은 조합대 너비 기준.</summary>
        public void GetSizeInfo(out float requestedFraction, out float capFraction, out float usedFraction)
        {
            Rect space = Workspace(Mathf.Clamp(visibleBoard, 0, 1));
            float width = Mathf.Max(.0001f, space.width);
            float fraction = tuning != null ? tuning.orbRadiusScreenFraction : .055f;
            requestedFraction = fraction;
            capFraction = GameSizeCap(space) / width;
            usedFraction = Radius(Mathf.Clamp(visibleBoard, 0, 1)) / width;
        }
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
            foreach (var seed in AllSeeds)
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

        /// <summary>기획 시안(화면 절반 구슬 영역)을 픽셀 영역으로 변환. Safe Area 밖으로는 나가지 않는다.</summary>
        public static Rect HalfScreenPixels(int width, int height, Rect safe, Rect normalized)
        {
            float w = Mathf.Max(1f, width), h = Mathf.Max(1f, height);
            float xMin = Mathf.Clamp01(normalized.xMin) * w, xMax = Mathf.Clamp01(normalized.xMax) * w;
            float yMin = Mathf.Clamp01(normalized.yMin) * h, yMax = Mathf.Clamp01(normalized.yMax) * h;
            xMin = Mathf.Max(xMin, safe.xMin); xMax = Mathf.Min(xMax, safe.xMax);
            yMin = Mathf.Max(yMin, safe.yMin); yMax = Mathf.Min(yMax, safe.yMax);
            if (xMax - xMin < 1f) xMax = xMin + 1f;
            if (yMax - yMin < 1f) yMax = yMin + 1f;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void RefreshCameraLayout()
        {
            if (sandboxCamera == null || Screen.width < 1 || Screen.height < 1) return;
            float fraction = sourceConfig != null ? sourceConfig.LowerFraction : .45f;
            if (layoutWidth == Screen.width && layoutHeight == Screen.height && layoutMatched == matchGameArea &&
                layoutBoard == visibleBoard && layoutSafeArea == Screen.safeArea && layoutFraction == fraction &&
                layoutHalf == useHalfScreenArea && layoutHalfArea == halfScreenArea) return;
            bool geometryChanged = layoutWidth != Screen.width || layoutHeight != Screen.height ||
                layoutMatched != matchGameArea || layoutSafeArea != Screen.safeArea || layoutFraction != fraction ||
                layoutHalf != useHalfScreenArea || layoutHalfArea != halfScreenArea;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutMatched = matchGameArea;
            layoutBoard = visibleBoard; layoutSafeArea = Screen.safeArea; layoutFraction = fraction;
            layoutHalf = useHalfScreenArea; layoutHalfArea = halfScreenArea;
            if (matchGameArea)
            {
                Rect pixels = useHalfScreenArea
                    ? HalfScreenPixels(Screen.width, Screen.height, Screen.safeArea, halfScreenArea)
                    : GameWorkspacePixels(Screen.width, Screen.height, Screen.safeArea, fraction);
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
            ApplyAreaColors();
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

        private Rect HelpRect => new Rect(12f, 12f, Mathf.Min(460f, Mathf.Max(160f, Screen.width - 24f)), 166f);
        private bool OverHelp(Vector2 point) => showHelp && HelpRect.Contains(new Vector2(point.x, Screen.height - point.y));
        private void OnGUIRemoved()
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
                (matchGameArea ? "Board " + (visibleBoard == 0 ? "A" : "B") + " / " : "Overview / ") + LastInput + "  |  Orbs " + OrbCount);
            float third = (rect.width - 32f) / 3f;
            if (GUI.Button(new Rect(rect.x + 12, rect.y + 131, third - 4f, 25), "+ Yin")) AddOrb(OrbPolarity.Yin);
            if (GUI.Button(new Rect(rect.x + 12 + third, rect.y + 131, third - 4f, 25), "+ Yang")) AddOrb(OrbPolarity.Yang);
            if (GUI.Button(new Rect(rect.x + 12 + third * 2f, rect.y + 131, third - 4f, 25), "- Remove")) RemoveLastOrb();
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
            OrbView.SetArtwork(null);
            IsReady = false;
        }
    }
}
