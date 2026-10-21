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
    public sealed class GameSession : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/GameSession";
        [SerializeField] private LabNetwork network;
        [SerializeField, Min(5f)] private float loadingTimeoutSeconds = 30f;
        private readonly GameRunFlow hostFlow = new GameRunFlow();
        private readonly GameFlowReplica replica = new GameFlowReplica();
        private readonly Queue<Control> controls = new Queue<Control>();
        private readonly HashSet<ulong> resultReady = new HashSet<ulong>();
        private GameFlowState state = new GameFlowState();
        private Coroutine sceneRoutine;
        private string wantedScene;
        private bool forceBattleReload, goingHome, battleReadyPending, localBattleReady;
        private bool restartPending;
        private double loadStartedAt, nextStateAt, nextReadyAt, nextResultReadyAt;
        private LabGourdResultPreview preview;

        [Serializable]
        private sealed class Control
        {
            public string kind;
            public GameFlowState state;
            public string runId;
            public int stageIndex;
            public uint revision;
            [NonSerialized] public ulong sender;
        }

        public static GameSession Instance { get; private set; }
        public LabNetwork Network => network;
        public GameFlowState State => state;
        public LabSnapshot ResultSnapshot { get; private set; }
        public Texture PreviewTexture => preview != null && !preview.IsDisposed ? preview.Texture : null;
        public bool IsViewerOpen { get; private set; }
        public string UiNotice { get; private set; } = string.Empty;
        public bool IsLoadingScene => sceneRoutine != null || state.phase == GameFlowPhase.LoadingBattle;
        public bool PendingSettingsApply => network != null && network.IsConnected && !network.SessionConfigReady;
        public bool CanAdvanceStage => network != null && network.IsHost && !IsLoadingScene && AllResultsReady;
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
            DontDestroyOnLoad(gameObject);
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
                if (network.IsHost) { hostFlow.EnterLobby(); SetHostState(); }
                else SetLocalState(new GameFlowState { phase = GameFlowPhase.Lobby });
            }
            if (state.phase == GameFlowPhase.Lobby && !network.IsConnected && !network.IsJoiningRoom)
                SetLocalState(new GameFlowState());
            if (battleReadyPending && network.HasBattleReferences && sceneRoutine == null)
            {
                battleReadyPending = false;
                localBattleReady = true;
                SendReady();
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (state.phase == GameFlowPhase.LoadingBattle)
            {
                if (localBattleReady && !network.IsHost && now >= nextReadyAt) SendReady();
                if (network.IsHost && hostFlow.IsBattleReady && sceneRoutine == null
                    && (network.IsDeveloperSolo || network.SessionConfigReady))
                {
                    GameFlowState loading = hostFlow.State;
                    if (network.StartCurrentGameStage() && network.Snapshot != null
                        && hostFlow.MarkPlaying(loading.runId, loading.stageIndex, loading.revision,
                            network.Snapshot.sessionId, network.Snapshot.round)) SetHostState();
                }
                if (network.IsHost && now - loadStartedAt > loadingTimeoutSeconds)
                    AbortHost("A player could not finish loading the stage.");
            }
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
                else network.SendGameFlowToHost(JsonUtility.ToJson(new Control
                { kind = "result_ready", runId = state.runId, stageIndex = state.stageIndex, revision = state.revision }));
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
                if (control.kind == "loaded")
                    hostFlow.MarkBattleReady(control.sender, control.runId, control.stageIndex, control.revision);
                else if (control.kind == "result_ready" && control.runId == state.runId
                    && control.stageIndex == state.stageIndex && control.revision == state.revision
                    && (state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed))
                    resultReady.Add(control.sender);
                else if (control.kind == "exit" && state.IsRunActive && control.runId == state.runId
                    && control.stageIndex == state.stageIndex)
                    AbortHost("A player left the game.");
                return;
            }
            if (control.kind != "state" || control.state == null) return;
            if (state.phase == GameFlowPhase.GameOver && control.state.phase != GameFlowPhase.GameOver) return;
            bool newRun = state.phase == GameFlowPhase.Home || state.phase == GameFlowPhase.Lobby;
            if (replica.Apply(control.state, newRun, out _)) SetLocalState(replica.State);
        }

        private void SetHostState()
        {
            SetLocalState(hostFlow.State);
            BroadcastState();
        }

        private void BroadcastState()
        {
            if (network.IsHost && network.IsConnected)
                network.BroadcastGameFlow(JsonUtility.ToJson(new Control { kind = "state", state = hostFlow.State }));
        }

        private void SetLocalState(GameFlowState value)
        {
            GameFlowState previous = state;
            state = value.Copy();
            if (state.phase != GameFlowPhase.Home && state.phase != GameFlowPhase.Lobby)
                network.LockGameRoster();
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
                ResultSnapshot = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(network.Snapshot));
        }

        public void NotifyBattleBound(LabOrbBoard board)
        {
            if (network.board != board) return;
            if (state.phase != GameFlowPhase.LoadingBattle && state.phase != GameFlowPhase.Playing)
            {
                network.FreezeGameBattle();
                return;
            }
            battleReadyPending = true;
        }

        private void SendReady()
        {
            if (state.phase != GameFlowPhase.LoadingBattle || !network.HasBattleReferences) return;
            nextReadyAt = Time.realtimeSinceStartupAsDouble + .5d;
            if (network.IsHost) hostFlow.MarkBattleReady(network.LocalPlayerId, state.runId, state.stageIndex, state.revision);
            else network.SendGameFlowToHost(JsonUtility.ToJson(new Control
            { kind = "loaded", runId = state.runId, stageIndex = state.stageIndex, revision = state.revision }));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.path == GameScenePaths.Home && !network.IsConnected && network.CanChooseConnection)
                network.StartRoomSearch();
        }

        public void Host()
        {
            if (!network.CanChooseConnection || state.phase != GameFlowPhase.Home) return;
            ResetRoomFlow(); network.Host();
        }

        public void JoinRoom(string roomKey)
        {
            if (!network.CanJoinDiscoveredRoom(roomKey) || state.phase != GameFlowPhase.Home) return;
            ResetRoomFlow(); network.JoinDiscoveredRoom(roomKey);
        }

        public void JoinDirect(string address)
        {
            if (!network.CanChooseConnection || state.phase != GameFlowPhase.Home) return;
            ResetRoomFlow(); network.Join(address);
        }

        public void SelectSlot(int slot) => network.RequestPlayerSlot(slot);

        public void StartGame()
        {
            if (!network.CanStartBattle || state.phase != GameFlowPhase.Lobby) return;
            if (hostFlow.BeginRun(network.ConnectedPlayerIds)) PrepareStage();
        }

        public void StartSolo()
        {
            if (state.phase != GameFlowPhase.Home || !network.PrepareDeveloperSoloGame()) return;
            hostFlow.EnterLobby();
            if (hostFlow.BeginRun(network.ConnectedPlayerIds)) PrepareStage();
        }

        public void NextStage()
        {
            if (!CanAdvanceStage) return;
            if (hostFlow.NextStage(state.runId, state.stageIndex, state.revision)) PrepareStage();
        }

        private void PrepareStage()
        {
            if (!network.PrepareGameStage()) return;
            SetHostState();
        }

        public void Pause() => network.PauseBattle();
        public void Resume() => network.ResumeBattle();
        public void OpenGourdViewer() { IsViewerOpen = PreviewTexture != null; Changed?.Invoke(); }
        public void CloseGourdViewer() { IsViewerOpen = false; Changed?.Invoke(); }
        public void RotatePreview(Vector2 delta) { preview?.Rotate(delta); }
        public void ZoomPreview(float delta) { preview?.Zoom(delta); }
        public void ResetPreview() { preview?.ResetView(); }

        public bool ApplyDeveloperSettings(float[] values, out string error)
        {
            error = "Only the Host can change settings in the lobby or a paused battle.";
            if (!network.DeveloperBuildAvailable || network.config == null) return false;
            LabConfig candidate = Instantiate(network.config);
            try
            {
                if (!candidate.TryApplyDeveloperValues(values, out error)) return false;
                bool applied = state.phase == GameFlowPhase.Home
                    ? network.ConfigureHostSession(candidate, network.TargetParticipantCount)
                    : state.phase == GameFlowPhase.Lobby && network.IsHost
                        ? network.ApplyHostLobbyConfig(candidate)
                        : state.phase == GameFlowPhase.Playing && network.IsBattlePaused && network.IsHost
                            && (network.IsDeveloperSolo ? network.RestartGameSoloWithConfig(candidate)
                                : network.RestartBattleWithDeveloperConfig(candidate));
                if (!applied) { error = "Settings cannot be applied in the current game state."; return false; }
                error = string.Empty;
                return true;
            }
            finally { Destroy(candidate); }
        }

        private void OnDeveloperRestart() { restartPending = true; }

        public void ExitRun()
        {
            if (state.phase == GameFlowPhase.Home || state.phase == GameFlowPhase.Lobby
                || state.phase == GameFlowPhase.Completed || state.phase == GameFlowPhase.GameOver)
            { GoHome(); return; }
            ExitWithLocalReason("A player left the game.");
        }

        private void ExitWithLocalReason(string reason)
        {
            if (network.IsHost) { AbortHost(reason); return; }
            network.SendGameFlowToHost(JsonUtility.ToJson(new Control
            { kind = "exit", runId = state.runId, stageIndex = state.stageIndex, revision = state.revision }));
            GameFlowState failed = state.Copy();
            failed.phase = GameFlowPhase.GameOver; failed.endReason = reason; failed.revision++;
            SetLocalState(failed);
        }

        private void AbortHost(string reason)
        {
            if (hostFlow.Abort(reason)) SetHostState();
        }

        private void OnPeerDisconnected(ulong _, bool lostHost)
        {
            if (state.phase == GameFlowPhase.Completed) { CacheResultSnapshot(); return; }
            if (goingHome || !state.IsRunActive) return;
            if (network.IsHost) AbortHost("A player disconnected.");
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
            goingHome = true;
            network.Stop();
            network.UnbindBattleReferences();
            ResetRoomFlow();
            SetLocalState(new GameFlowState());
            goingHome = false;
        }

        private void ResetRoomFlow()
        {
            hostFlow.ReturnHome(); replica.ResetForNewRoom(); controls.Clear();
            restartPending = battleReadyPending = localBattleReady = false; resultReady.Clear();
            ReleaseResult(); UiNotice = string.Empty;
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
            }
            ReleaseResult(); Instance = null;
        }
    }
}
