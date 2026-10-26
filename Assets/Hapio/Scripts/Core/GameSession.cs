using System;
using System.Collections;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// Persistent room/run data. Every screen and Battle presentation is disposable; neither
    /// scene names nor the Lab battle round number decide a stage transition.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed partial class GameSession : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/GameSession";
        /// <summary>The single 2:2 team territory battle (#281) has no yokai stage definition.</summary>
        public const string TerritoryStageId = "territory";
        [SerializeField] private LabNetwork network;
        [SerializeField] private GameStageCatalog stageCatalog;
        private GameStageSettings stageSettings;
        private int expectedSettingsRevision;
        private string expectedSettingsFingerprint;
        [SerializeField, Min(5f)] private float loadingTimeoutSeconds = 30f;
        private readonly GameRunFlow hostFlow = new GameRunFlow();
        private readonly GameFlowReplica replica = new GameFlowReplica();
        private readonly Queue<Control> controls = new Queue<Control>();
        private readonly HashSet<ulong> resultReady = new HashSet<ulong>();
        private readonly GamePendingRoomRegistry pendingRooms = new GamePendingRoomRegistry();
        private bool roomBrowserVisible;
        private GameFlowState state = new GameFlowState();
        private Coroutine sceneRoutine;
        private string wantedScene;
        private bool forceBattleReload, goingHome, battleReadyPending, localBattleReady;
        private bool restartPending;
        private double loadStartedAt, nextStateAt, nextReadyAt, nextResultReadyAt;
        private LabGourdResultPreview preview;
        // 2:2 (#281): the Host's room mode, and the mode a client expects from the room card it joined.
        private GameMode hostMode, joiningMode;
        // 전체 게임 대시보드용: 이번 판의 스테이지별 결과. 같은 스테이지를 다시 하면 마지막 기록으로 덮어쓴다.
        private readonly SortedDictionary<int, LabResultStage> stageResults = new SortedDictionary<int, LabResultStage>();
        private string stageResultsRunId;

        [Serializable]
        private sealed class Control
        {
            public string kind;
            public GameFlowState state;
            public string runId;
            public int stageIndex;
            public uint revision;
            public string stageId;
            public LabStageYokai yokai;
            public int settingsRevision;
            public string settingsFingerprint;
            public int readyCount;
            public int participantCount;
            [NonSerialized] public ulong sender;
        }

        public static GameSession Instance { get; private set; }
        public LabNetwork Network => network;
        public GameFlowState State => state;
        /// <summary>2:2 team territory room (#281): one battle, then back to this room's lobby.</summary>
        public bool IsTerritory => state.mode == GameMode.Territory;
        public GameStageDefinition CurrentStageDefinition => state.mode == GameMode.Story
            && state.stageIndex >= 1 && state.stageIndex <= 3 ? stageCatalog.Get(state.stageIndex) : null;
        public LabSnapshot ResultSnapshot { get; private set; }
        /// <summary>이번 판에서 깬 스테이지들의 결과 (스테이지 순서). ALL STAGES CLEAR 화면의 전체 대시보드가 쓴다.</summary>
        public IReadOnlyList<LabResultStage> StageResults => new List<LabResultStage>(stageResults.Values);
        public Texture PreviewTexture => preview != null && !preview.IsDisposed ? preview.Texture : null;
        public bool IsViewerOpen { get; private set; }
        public string UiNotice { get; private set; } = string.Empty;
        public bool RoomBrowserVisible => roomBrowserVisible;
        public IReadOnlyList<GamePendingRoomInfo> PendingRooms => pendingRooms.Rooms;
        public bool IsLoadingScene => sceneRoutine != null || IsPreparingBattle;
        public bool PendingSettingsApply => network != null && network.IsConnected && !network.SessionConfigReady;
        public bool CanAdvanceStage => network != null && network.IsHost && !IsLoadingScene && AllResultsReady
            && (!IsTerritory || !network.IsDeveloperSolo);
        // The run's own stage count: 2:2 (#281) plays one battle, so it has no next stage to skip to.
        public bool CanDeveloperAdvanceStage => network != null && network.DeveloperBuildAvailable
            && network.IsConnected && network.IsHost && !goingHome && !IsLoadingScene && !restartPending
            && state.phase == GameFlowPhase.Playing && state.stageIndex >= 1
            && state.stageIndex < state.totalStages && network.IsBattlePaused
            && network.Snapshot != null && network.Snapshot.phase == LabPhase.Playing
            && MatchesBattle(network.Snapshot);
        public bool CanReturnHome => state.phase == GameFlowPhase.GameOver
            || state.phase == GameFlowPhase.Home || state.phase == GameFlowPhase.Lobby
            || (state.phase == GameFlowPhase.Completed && (!network.IsHost || AllResultsReady));
        private bool AllResultsReady
        {
            get
            {
                foreach (ulong id in network.ConnectedPlayerIds) if (!resultReady.Contains(id)) return false;
                return resultReady.Count > 0;
            }
        }
        public event Action Changed;

        public static GameSession EnsureExists()
        {
            if (Instance != null) return Instance;
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException("The saved Hapio GameSession prefab is missing.");
            GameObject created = Instantiate(prefab);
            GameSession session = created.GetComponent<GameSession>();
            if (session == null) { Destroy(created); throw new InvalidOperationException("The game session prefab has no controller."); }
            return session;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (network == null) network = GetComponent<LabNetwork>();
            if (network == null || !network.SetGameFlowEnabled(true))
                throw new InvalidOperationException("A disconnected game transport is required at bootstrap.");
            if (stageCatalog == null) stageCatalog = GameStageCatalog.RequireDefault();
            stageCatalog.Validate();
            stageSettings = new GameStageSettings(network.config, stageCatalog);
            LabConfig initial = stageSettings.CreateStageConfig(1);
            try
            {
                if (!network.ConfigureHostSession(initial, network.TargetParticipantCount))
                    throw new InvalidOperationException("The initial game stage settings could not be prepared.");
            }
            finally { Destroy(initial); }
            network.GameFlowDeveloperConfigValidation = ValidateBattleDeveloperConfig;
            DontDestroyOnLoad(gameObject);
            startOverlay = GameStartOverlay.Create(transform);
            network.GameFlowMessage += ReceiveControl;
            network.GameFlowDisconnected += OnPeerDisconnected;
            network.GameFlowDeveloperRestartRequested += OnDeveloperRestart;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            // Directly opening a menu in the Editor still has one explicit initial state.
            if (SceneManager.GetActiveScene().path != GameScenePaths.Home && !network.IsConnected)
                QueueScene(GameScenePaths.Home);
        }

        private void Update()
        {
            if (Instance != this || network == null) return;
            pendingRooms.Tick(out string pendingError);
            if (!string.IsNullOrEmpty(pendingError)) { UiNotice = pendingError; Changed?.Invoke(); }
            while (controls.Count > 0) ApplyControl(controls.Dequeue());
            if (restartPending)
            {
                restartPending = false;
                GameFlowState before = hostFlow.State;
                if (network.IsHost && hostFlow.RestartCurrentStage(before.runId, before.stageIndex, before.revision))
                    PrepareStage();
            }
            if (goingHome) return;
            if (state.phase == GameFlowPhase.Home && network.IsConnected)
            {
                if (network.IsHost) { hostFlow.EnterLobby(hostMode); SetHostState(); }
                else SetLocalState(new GameFlowState
                    { phase = GameFlowPhase.Lobby, mode = joiningMode, totalStages = GameRunFlow.StagesFor(joiningMode) });
            }
            if (state.phase == GameFlowPhase.Lobby && !network.IsConnected && !network.IsJoiningRoom)
                SetLocalState(new GameFlowState());
            if (battleReadyPending && network.HasBattleReferences && sceneRoutine == null
                && StageConfigurationReady)
            {
                battleReadyPending = false;
                localBattleReady = true;
                SendReady();
            }
            double now = Time.realtimeSinceStartupAsDouble;
            UpdateBattleStart(now);
            if (network.IsHost && network.IsConnected && now >= nextStateAt)
            {
                BroadcastState();
                nextStateAt = now + 1d;
            }
            if ((state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed)
                && SceneManager.GetActiveScene().path == GameScenePaths.StageResult && sceneRoutine == null
                && now >= nextResultReadyAt)
            {
                nextResultReadyAt = now + .5d;
                if (network.IsHost) resultReady.Add(network.LocalPlayerId);
                else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("result_ready")));
            }
            LabSnapshot snapshot = network.Snapshot;
            if (network.IsHost && state.phase == GameFlowPhase.Playing && MatchesBattle(snapshot))
            {
                if (snapshot.phase == LabPhase.Defeat)
                    AbortHost(snapshot.remaining <= 0d ? "Time ran out." : "All players are down.");
                else if (snapshot.phase == LabPhase.Victory && !LabGourdFinish.DelayVictory(network, snapshot)
                    && hostFlow.MarkVictory(snapshot.sessionId, snapshot.round)) SetHostState();
            }
        }

        private bool MatchesBattle(LabSnapshot snapshot) => snapshot != null
            && snapshot.sessionId == state.battleSessionId && snapshot.round == state.battleRound;

        private void ReceiveControl(ulong sender, string json)
        {
            if (goingHome || controls.Count >= 128) return;
            try
            {
                Control control = JsonUtility.FromJson<Control>(json);
                if (control == null || string.IsNullOrEmpty(control.kind)) return;
                control.sender = sender;
                controls.Enqueue(control);
            }
            catch (ArgumentException) { }
        }

        private void ApplyControl(Control control)
        {
            if (!network.IsConnected || goingHome) return;
            if (network.IsHost)
            {
                if (!MatchesControlIdentity(control, control.kind == "exit")) return;
                if (control.kind == "loaded")
                    hostFlow.MarkBattleReady(control.sender, control.runId, control.stageIndex, control.revision);
                else if (control.kind == "initial_ready")
                    hostFlow.MarkInitialStateReady(control.sender, control.runId, control.stageIndex, control.revision);
                else if (control.kind == "countdown_ready")
                    hostFlow.MarkCountdownReady(control.sender, control.runId, control.stageIndex, control.revision);
                else if (control.kind == "result_ready" && control.runId == state.runId
                    && control.stageIndex == state.stageIndex && control.revision == state.revision
                    && (state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed))
                    resultReady.Add(control.sender);
                else if (control.kind == "exit" && state.IsRunActive && control.runId == state.runId
                    && control.stageIndex == state.stageIndex)
                    AbortHost("A player left the game.", "한 명이 나가 판을 멈추고 로비로 돌아왔소.");
                return;
            }
            if (control.kind == "countdown_commit")
            {
                if (state.phase == GameFlowPhase.Countdown && MatchesControlIdentity(control)) countdownCommitted = true;
                return;
            }
            if (control.kind != "state" || control.state == null || !ValidStateControl(control)) return;
            if (control.state.runId == state.runId && control.state.revision == state.revision
                && control.state.stageIndex == state.stageIndex && control.state.phase == state.phase)
                ReadStartProgress(control);
            if (state.phase == GameFlowPhase.GameOver && control.state.phase != GameFlowPhase.GameOver) return;
            bool newRun = state.phase == GameFlowPhase.Home || state.phase == GameFlowPhase.Lobby
                || BackToLobby(control.state);
            if (replica.Apply(control.state, newRun, out _))
            {
                expectedSettingsRevision = control.settingsRevision;
                expectedSettingsFingerprint = control.settingsFingerprint;
                SetLocalState(replica.State);
                ReadStartProgress(control);
            }
        }

        private void SetHostState()
        {
            SetLocalState(hostFlow.State);
            BroadcastState();
        }

        private void BroadcastState()
        {
            if (network.IsHost && network.IsConnected)
                network.BroadcastGameFlow(JsonUtility.ToJson(MakeControl("state", hostFlow.State)));
        }

        /// <summary>
        /// 2:2 (#281): the Host ends the battle or its result and keeps the room. Only a lobby state newer than this
        /// run is taken, so an old pre-battle lobby state can never pull a peer out of a later battle.
        /// </summary>
        private bool BackToLobby(GameFlowState incoming) => incoming.phase == GameFlowPhase.Lobby
            && state.mode == GameMode.Territory && incoming.mode == GameMode.Territory
            && (state.IsRunActive || state.phase == GameFlowPhase.Completed) && incoming.revision > state.revision;

        private void SetLocalState(GameFlowState value)
        {
            GameFlowState previous = state;
            state = value.Copy();
            OnStartStateChanged(previous);
            if (state.phase != GameFlowPhase.Home && state.phase != GameFlowPhase.Lobby)
                network.LockGameRoster();
            if (state.phase == GameFlowPhase.Lobby && previous.phase != GameFlowPhase.Home
                && previous.phase != GameFlowPhase.Lobby)
            {
                // 2:2 (#281): the same room opens again for seat changes and a replacement player.
                network.UnlockGameRoster();
                ReleaseResult();
                UiNotice = state.notice;
            }
            else if (state.phase == GameFlowPhase.LoadingBattle && previous.phase == GameFlowPhase.Lobby)
                UiNotice = string.Empty;
            if (previous.runId != state.runId || previous.stageIndex != state.stageIndex
                || previous.phase != state.phase) resultReady.Clear();
            if (state.phase == GameFlowPhase.LoadingBattle
                && (previous.runId != state.runId || previous.revision != state.revision))
            {
                ReleaseResult();
                loadStartedAt = Time.realtimeSinceStartupAsDouble;
                localBattleReady = battleReadyPending = false;
                forceBattleReload = SceneManager.GetActiveScene().path == GameScenePaths.Battle;
            }
            if (state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed)
                CacheResultSnapshot();
            if (state.phase == GameFlowPhase.GameOver) network.FreezeGameBattle();
            QueueScene(SceneFor(state.phase));
            Changed?.Invoke();
        }

        public static string SceneFor(GameFlowPhase phase)
        {
            switch (phase)
            {
                case GameFlowPhase.Lobby: return GameScenePaths.Lobby;
                case GameFlowPhase.LoadingBattle:
                case GameFlowPhase.SynchronizingBattle:
                case GameFlowPhase.Countdown:
                case GameFlowPhase.Playing: return GameScenePaths.Battle;
                case GameFlowPhase.StageResult:
                case GameFlowPhase.Completed: return GameScenePaths.StageResult;
                case GameFlowPhase.GameOver: return GameScenePaths.GameOver;
                default: return GameScenePaths.Home;
            }
        }

        private void QueueScene(string path)
        {
            wantedScene = path;
            if (sceneRoutine == null && (SceneManager.GetActiveScene().path != path || forceBattleReload))
                sceneRoutine = StartCoroutine(ChangeScene());
        }

        private IEnumerator ChangeScene()
        {
            // Never unload a terminal Battle before its actual committed paint map is copied.
            yield return null;
            while (SceneManager.GetActiveScene().path != wantedScene || forceBattleReload)
            {
                string destination = wantedScene;
                if (destination == GameScenePaths.StageResult)
                {
                    double waitUntil = Time.realtimeSinceStartupAsDouble + loadingTimeoutSeconds;
                    while (!CanCaptureResult() && wantedScene == destination
                        && network.IsConnected && Time.realtimeSinceStartupAsDouble < waitUntil)
                        yield return null;
                    if (wantedScene != destination) continue;
                    if (!CanCaptureResult() && state.phase != GameFlowPhase.Completed)
                    {
                        ExitWithLocalReason("The stage result could not be synchronized.");
                        continue;
                    }
                    if (!CanCaptureResult())
                        UiNotice = "All stages are clear. The completed gourd preview could not be synchronized.";
                    CaptureResult();
                }
                network.UnbindBattleReferences();
                forceBattleReload = false;
                AsyncOperation load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
                if (load == null) throw new InvalidOperationException("Could not load " + destination);
                yield return load;
                yield return null;
            }
            sceneRoutine = null;
        }

        private bool CanCaptureResult()
        {
            CacheResultSnapshot();
            // A final committed clear stays a clear even if the Host exits unexpectedly before
            // a slower peer receives its optional preview texture. Never manufacture its map.
            if (state.phase == GameFlowPhase.Completed && !network.IsConnected) return true;
            if (!MatchesBattle(network.Snapshot) || network.Snapshot.phase != LabPhase.Victory) return false;
            if (network.config == null || !network.config.CompletedGourdViewerEnabled
                || network.config.GourdMode != LabGourdMode.Paint) return true;
            return LabGourdResultPreview.SurfaceMatches(network, out _);
        }

        private void CaptureResult()
        {
            CacheResultSnapshot();
            if (preview != null) return;
            if (network.config != null && network.config.CompletedGourdViewerEnabled
                && network.config.GourdMode == LabGourdMode.Paint
                && LabGourdResultPreview.TryCreate(network, out var copy, out string error))
            {
                preview = copy;
                DontDestroyOnLoad(preview.PreviewCamera.transform.root.gameObject);
            }
        }

        private void CacheResultSnapshot()
        {
            if (ResultSnapshot != null && ResultSnapshot.sessionId == state.battleSessionId
                && ResultSnapshot.round == state.battleRound) return;
            if (MatchesBattle(network.Snapshot) && network.Snapshot.phase == LabPhase.Victory)
            {
                ResultSnapshot = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(network.Snapshot));
                RecordStageResult(ResultSnapshot);
            }
        }

        /// <summary>깬 스테이지의 결과를 이번 판 기록에 넣는다. 새 판이면 이전 판 기록은 버린다.</summary>
        private void RecordStageResult(LabSnapshot result)
        {
            if (result == null || state.stageIndex < 1 || state.stageIndex > 3) return;
            if (stageResultsRunId != state.runId) { stageResults.Clear(); stageResultsRunId = state.runId; }
            stageResults[state.stageIndex] = new LabResultStage(result.players, result.playedSeconds);
        }

        private bool StageConfigurationReady => network.MatchesGameStageConfig(expectedSettingsRevision,
            expectedSettingsFingerprint);

        private Control MakeControl(string kind, GameFlowState value = null)
        {
            GameFlowState current = value ?? state;
            Identity(current, out string stageId, out LabStageYokai yokai);
            return new Control
            {
                kind = kind, state = kind == "state" ? current : null,
                runId = current.runId, stageIndex = current.stageIndex, revision = current.revision,
                stageId = stageId,
                yokai = yokai,
                settingsRevision = network.IsHost ? network.SessionConfigRevision : expectedSettingsRevision,
                settingsFingerprint = network.IsHost ? network.SessionConfigFingerprint : expectedSettingsFingerprint,
                readyCount = StartReadyCount, participantCount = StartParticipantCount
            };
        }

        private bool ValidStateControl(Control control)
        {
            GameFlowState incoming = control.state;
            if (!GameFlowSnapshot.IsValid(incoming, out _)) return false;
            if (control.runId != incoming.runId || control.stageIndex != incoming.stageIndex
                || control.revision != incoming.revision) return false;
            if (incoming.stageIndex == 0)
                return string.IsNullOrEmpty(control.stageId) && control.yokai == LabStageYokai.None;
            Identity(incoming, out string stageId, out LabStageYokai yokai);
            return control.stageId == stageId && control.yokai == yokai
                && control.settingsRevision > 0 && !string.IsNullOrEmpty(control.settingsFingerprint)
                && control.settingsFingerprint.Length <= 128;
        }

        private bool MatchesControlIdentity(Control control, bool allowPreviousRevision = false)
        {
            if (state.stageIndex < 1 || state.stageIndex > state.totalStages) return false;
            Identity(state, out string stageId, out LabStageYokai yokai);
            return control.runId == state.runId && control.stageIndex == state.stageIndex
                && (control.revision == state.revision || allowPreviousRevision
                    && control.revision > 0 && control.revision < state.revision)
                && control.stageId == stageId
                && control.yokai == yokai && control.settingsRevision == expectedSettingsRevision
                && control.settingsFingerprint == expectedSettingsFingerprint;
        }

        /// <summary>A yokai stage from the catalog, or the yokai-free 2:2 battle (#281).</summary>
        private void Identity(GameFlowState value, out string stageId, out LabStageYokai yokai)
        {
            stageId = string.Empty; yokai = LabStageYokai.None;
            if (value == null || value.stageIndex < 1) return;
            if (value.mode == GameMode.Territory) { stageId = TerritoryStageId; return; }
            GameStageDefinition definition = stageCatalog.Get(value.stageIndex);
            stageId = definition.stageId; yokai = definition.yokai;
        }

        /// <summary>The room's settings for its mode: one yokai stage, or the 2:2 profile (#281).</summary>
        private LabConfig CreateModeConfig(GameMode mode, int stage) => mode == GameMode.Territory
            ? stageSettings.CreateTerritoryConfig() : stageSettings.CreateStageConfig(stage >= 1 ? stage : 1);

        /// <summary>Install the room's mode before hosting; a previous 2:2 room must not leak into a story room.</summary>
        private bool ConfigureRoomMode(GameMode mode, int capacity)
        {
            LabConfig candidate = CreateModeConfig(mode, 1);
            try { if (!network.ConfigureHostSession(candidate, capacity)) return false; }
            finally { Destroy(candidate); }
            hostMode = mode;
            return true;
        }

        private int SettingsScope => IsTerritory ? GameStageSettings.TerritoryScope
            : state.stageIndex >= 1 ? state.stageIndex : 1;

        public void NotifyBattleBound(LabOrbBoard board)
        {
            if (network.board != board) return;
            if (!IsPreparingBattle && state.phase != GameFlowPhase.Playing)
            {
                network.FreezeGameBattle();
                return;
            }
            if (IsPreparingBattle) network.SetGameStartHeld(true);
            battleReadyPending = true;
        }

        private void SendReady()
        {
            if (state.phase != GameFlowPhase.LoadingBattle || !network.HasBattleReferences
                || !StageConfigurationReady) return;
            nextReadyAt = Time.realtimeSinceStartupAsDouble + .5d;
            if (network.IsHost) hostFlow.MarkBattleReady(network.LocalPlayerId, state.runId, state.stageIndex, state.revision);
            else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("loaded")));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.path == GameScenePaths.Home && roomBrowserVisible
                && !network.IsConnected && network.CanChooseConnection)
                network.StartRoomSearch();
        }

        public void EnterRoomBrowser()
        {
            if (state.phase != GameFlowPhase.Home) return;
            roomBrowserVisible = true;
            if (network.CanChooseConnection && !network.IsSearchingRooms) network.StartRoomSearch();
            Changed?.Invoke();
        }

        public bool CreatePendingRoom(int capacity) => CreatePendingRoom(capacity, GameMode.Story);

        /// <summary>A 2:2 room (#281) always waits for exactly four players.</summary>
        public bool CreatePendingRoom(int capacity, GameMode mode)
        {
            if (state.phase != GameFlowPhase.Home || !network.CanChooseConnection) return false;
            roomBrowserVisible = true;
            bool created = pendingRooms.TryCreate(capacity, mode, out string error);
            UiNotice = error;
            Changed?.Invoke();
            return created;
        }

        public bool EnterPendingRoom(string key)
        {
            if (state.phase != GameFlowPhase.Home || !network.CanChooseConnection || !pendingRooms.CanEnter(key)) return false;
            ResetRoomFlow();
            GamePendingRoomInfo room = null;
            foreach (GamePendingRoomInfo item in pendingRooms.Rooms) if (item.Key == key) room = item;
            if (room == null || !ConfigureRoomMode(room.Mode, room.Capacity))
            {
                UiNotice = "방 설정을 준비하지 못했소. 다시 눌러주시오.";
                Changed?.Invoke();
                return false;
            }
            bool entered = pendingRooms.TryEnter(key, network, out string error);
            roomBrowserVisible = true;
            UiNotice = error;
            Changed?.Invoke();
            return entered;
        }

        public void Host()
        {
            if (!network.CanChooseConnection || state.phase != GameFlowPhase.Home) return;
            roomBrowserVisible = true;
            ResetRoomFlow();
            if (!ConfigureRoomMode(GameMode.Story, network.TargetParticipantCount)) return;
            network.Host();
        }

        public void JoinRoom(string roomKey)
        {
            if (!network.CanJoinDiscoveredRoom(roomKey) || state.phase != GameFlowPhase.Home) return;
            roomBrowserVisible = true;
            ResetRoomFlow();
            foreach (LabDiscoveredRoom room in network.DiscoveredRooms)
                if (room.Key == roomKey) joiningMode = room.Team ? GameMode.Territory : GameMode.Story;
            network.JoinDiscoveredRoom(roomKey);
        }

        public void JoinDirect(string address)
        {
            if (!network.CanChooseConnection || state.phase != GameFlowPhase.Home) return;
            roomBrowserVisible = true;
            ResetRoomFlow(); network.Join(address);
        }

        public void SelectSlot(int slot) => network.RequestPlayerSlot(slot);

        public void StartGame()
        {
            if (!network.CanStartBattle || state.phase != GameFlowPhase.Lobby) return;
            if (hostFlow.BeginRun(network.ConnectedPlayerIds)) PrepareStage();
        }

        public void StartSolo() => StartSolo(GameMode.Story);

        /// <summary>DEV 2:2 solo check (#281): alone on the Blue team, with combined colors alternating Blue and Red.</summary>
        public void StartSolo(GameMode mode)
        {
            if (state.phase != GameFlowPhase.Home || !network.PrepareDeveloperSoloGame()) return;
            hostMode = mode;
            hostFlow.EnterLobby(mode);
            if (hostFlow.BeginRun(network.ConnectedPlayerIds)) PrepareStage();
        }

        public void NextStage()
        {
            if (!CanAdvanceStage) return;
            // 2:2 (#281) plays one battle; its result leads everyone back to the same room's lobby.
            if (IsTerritory)
            {
                if (state.phase == GameFlowPhase.Completed) ReturnTerritoryToLobby("한 판이 끝났소. 다시 자리를 고르시오.");
                return;
            }
            if (hostFlow.NextStage(state.runId, state.stageIndex, state.revision)) PrepareStage();
        }

        /// <summary>
        /// Skip a paused Host battle without claiming a clear. Uses accepted profiles only;
        /// unsaved DEV form inputs are deliberately not applied by this action.
        /// </summary>
        public bool DeveloperNextStage()
        {
            if (!CanDeveloperAdvanceStage
                || !hostFlow.DeveloperNextStage(state.runId, state.stageIndex, state.revision)) return false;
            PrepareStage();
            return true;
        }

        private void PrepareStage()
        {
            GameFlowState loading = hostFlow.State;
            Identity(loading, out string stageId, out LabStageYokai yokai);
            LabConfig candidate = CreateModeConfig(loading.mode, loading.stageIndex);
            try
            {
                if (!network.PrepareGameStage(candidate, loading.stageIndex, stageId, yokai))
                { AbortHost("The stage configuration could not be prepared.", "판 설정을 준비하지 못해 로비로 돌아왔소."); return; }
            }
            finally { Destroy(candidate); }
            expectedSettingsRevision = network.SessionConfigRevision;
            expectedSettingsFingerprint = network.SessionConfigFingerprint;
            SetHostState();
        }

        public void Pause() => network.PauseBattle();
        public void Resume() => network.ResumeBattle();
        public void OpenGourdViewer() { IsViewerOpen = PreviewTexture != null; Changed?.Invoke(); }
        public void CloseGourdViewer() { IsViewerOpen = false; Changed?.Invoke(); }
        public void RotatePreview(Vector2 delta) { preview?.Rotate(delta); }
        public void ZoomPreview(float delta) { preview?.Zoom(delta); }
        public void ResetPreview() { preview?.ResetView(); }

        public float[] CaptureDeveloperSettings(int scope) => stageSettings.Capture(scope);

        public bool ApplyDeveloperSettings(int scope, float[] values, out string error)
        {
            error = "Only the Host can change settings in Home, the lobby or a paused battle.";
            if (!CanEditDeveloperSettings) return false;
            float[][] before = CaptureProfiles();
            if (!stageSettings.TryApply(scope, values, out error)) return false;
            if (InstallEditedProfile(out error)) return true;
            RestoreProfiles(before);
            return false;
        }

        /// <summary>Compatibility with the Battle DEV form: Common and this stage only, atomically.</summary>
        public bool ApplyDeveloperSettings(float[] values, out string error)
        {
            error = "Only the Host can change settings in Home, the lobby or a paused battle.";
            if (!CanEditDeveloperSettings) return false;
            float[][] before = CaptureProfiles();
            if (!stageSettings.TryApplyBattle(SettingsScope, values, out error)) return false;
            if (InstallEditedProfile(out error)) return true;
            RestoreProfiles(before);
            return false;
        }

        private bool CanEditDeveloperSettings => network != null && network.DeveloperBuildAvailable
            && network.config != null && (state.phase == GameFlowPhase.Home && network.CanChooseConnection
                || state.phase == GameFlowPhase.Lobby && network.IsHost
                || state.phase == GameFlowPhase.Playing && network.IsHost && network.IsBattlePaused);

        private bool InstallEditedProfile(out string error)
        {
            LabConfig candidate = CreateModeConfig(state.mode, state.stageIndex);
            try
            {
                bool applied = state.phase == GameFlowPhase.Home
                    ? network.ConfigureHostSession(candidate, network.TargetParticipantCount)
                    : state.phase == GameFlowPhase.Lobby && network.IsHost
                        ? network.ApplyHostLobbyConfig(candidate)
                        : state.phase == GameFlowPhase.Playing && network.IsBattlePaused && network.IsHost
                            && (network.IsDeveloperSolo ? network.RestartGameSoloWithConfig(candidate)
                                : network.RestartBattleWithDeveloperConfig(candidate));
                error = applied ? string.Empty : "Settings cannot be applied in the current game state.";
                return applied;
            }
            finally { Destroy(candidate); }
        }

        private float[][] CaptureProfiles() => new[]
        {
            stageSettings.Capture(0), stageSettings.Capture(1), stageSettings.Capture(2), stageSettings.Capture(3),
            stageSettings.Capture(GameStageSettings.TerritoryScope)
        };

        private void RestoreProfiles(float[][] before)
        {
            // Restore Common first: it updates the common fields embedded in all three effective profiles.
            stageSettings.TryApply(0, before[0], out _);
            for (int stage = 1; stage <= GameStageSettings.TerritoryScope; stage++) stageSettings.TryApply(stage, before[stage], out _);
        }

        private bool ValidateBattleDeveloperConfig(LabConfig candidate, out string error)
        {
            error = "Only this paused Host stage may change battle settings.";
            if (!CanEditDeveloperSettings || state.phase != GameFlowPhase.Playing || candidate == null) return false;
            return stageSettings.TryApplyBattle(SettingsScope, candidate.CaptureDeveloperValues(), out error, commit: false);
        }

        private void OnDeveloperRestart()
        {
            if (!network.IsHost || state.phase != GameFlowPhase.Playing) return;
            if (!stageSettings.TryApplyBattle(SettingsScope, network.config.CaptureDeveloperValues(), out string error))
            { UiNotice = error; AbortHost("The stage settings could not be retained."); return; }
            restartPending = true;
        }

        public void ExitRun()
        {
            if (state.phase == GameFlowPhase.Home || state.phase == GameFlowPhase.Lobby
                || state.phase == GameFlowPhase.Completed || state.phase == GameFlowPhase.GameOver)
            { GoHome(); return; }
            ExitWithLocalReason("A player left the game.");
        }

        private void ExitWithLocalReason(string reason)
        {
            // The Host leaving ends the room for everyone, also in 2:2.
            if (network.IsHost) { if (hostFlow.Abort(reason)) SetHostState(); return; }
            network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("exit")));
            GameFlowState failed = state.Copy();
            failed.phase = GameFlowPhase.GameOver; failed.endReason = reason; failed.revision++;
            SetLocalState(failed);
        }

        /// <summary>2:2 (#281): another player leaving or failing only stops the battle; the room returns to its lobby.</summary>
        private void AbortHost(string reason, string territoryNotice = "판을 멈추고 로비로 돌아왔소.")
        {
            if (IsTerritory && !network.IsDeveloperSolo && ReturnTerritoryToLobby(territoryNotice)) return;
            if (hostFlow.Abort(reason)) SetHostState();
        }

        private bool ReturnTerritoryToLobby(string notice)
        {
            if (!network.IsHost || !hostFlow.ReturnToLobby(notice)) return false;
            network.ReturnGameToLobby();
            SetHostState();
            return true;
        }

        private void OnPeerDisconnected(ulong _, bool lostHost)
        {
            if (state.phase == GameFlowPhase.Completed) { CacheResultSnapshot(); return; }
            if (goingHome || !state.IsRunActive) return;
            if (network.IsHost) AbortHost("A player disconnected.", "한 명이 나가 판을 멈추고 로비로 돌아왔소.");
            else
            {
                GameFlowState failed = state.Copy();
                failed.phase = GameFlowPhase.GameOver;
                failed.endReason = lostHost ? "The Host disconnected." : "A player disconnected.";
                failed.revision++;
                SetLocalState(failed);
            }
        }

        public void GoHome()
        {
            if (state.IsRunActive) { ExitRun(); return; }
            if (!CanReturnHome) return;
            roomBrowserVisible = state.phase == GameFlowPhase.Lobby;
            goingHome = true;
            network.Stop();
            network.UnbindBattleReferences();
            ResetRoomFlow();
            SetLocalState(new GameFlowState());
            goingHome = false;
        }

        public void GoHomeToRoomBrowser()
        {
            if (state.phase == GameFlowPhase.Lobby) GoHome();
            else if (state.phase == GameFlowPhase.Home) EnterRoomBrowser();
        }

        private void ResetRoomFlow()
        {
            hostFlow.ReturnHome(); replica.ResetForNewRoom(); controls.Clear();
            restartPending = battleReadyPending = localBattleReady = false; resultReady.Clear();
            ResetBattleStart();
            expectedSettingsRevision = 0; expectedSettingsFingerprint = null;
            hostMode = joiningMode = GameMode.Story;
            ReleaseResult(); UiNotice = string.Empty;
            stageResults.Clear(); stageResultsRunId = null;
        }

        private void ReleaseResult()
        {
            preview?.Dispose(); preview = null; ResultSnapshot = null; IsViewerOpen = false;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (network != null)
            {
                network.GameFlowMessage -= ReceiveControl;
                network.GameFlowDisconnected -= OnPeerDisconnected;
                network.GameFlowDeveloperRestartRequested -= OnDeveloperRestart;
                network.GameFlowDeveloperConfigValidation = null;
            }
            stageSettings?.Dispose();
            pendingRooms.Dispose();
            ReleaseResult(); Instance = null;
        }
    }
}
