using System;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Opt-in transport bridge for the separate game Scenes. The Lab Scene never enables it,
    /// so its connection token, lifecycle and single-round buttons remain unchanged.
    /// </summary>
    public sealed partial class LabNetwork
    {
        private static readonly byte[] GameConnectionToken = Encoding.ASCII.GetBytes("C6HapioGame/22");
        private const int MaximumGameFlowBytes = 8192;
        private bool gameFlowEnabled;
        private bool gameBattleFrozen;
        private bool gameStartHeld;
        private string preparedGameSession;
        private uint preparedGameRound;
        private bool gameStageStarting;
        private bool gameRosterLocked;
        private LabStageYokai gameStageYokai;
        private int gameStageIndex;
        private string gameStageId;
        private Transform ownedGameProjectileRoot;

        public delegate bool GameDeveloperConfigCheck(LabConfig candidate, out string error);
        /// <summary>The game adapter rejects edits outside Common and this stage before ending a round.</summary>
        public GameDeveloperConfigCheck GameFlowDeveloperConfigValidation;

        public event Action<ulong, string> GameFlowMessage;
        /// <summary>Raised before connection cleanup; true means this client lost its Host.</summary>
        public event Action<ulong, bool> GameFlowDisconnected;
        /// <summary>Accepted DEV settings require the same-stage Scene load barrier before combat restarts.</summary>
        public event Action GameFlowDeveloperRestartRequested;
        public bool GameFlowEnabled => gameFlowEnabled;
        /// <summary>
        /// The yokai of the stage being played, on every device (#351): the Host's from PrepareGameStage, a Guest's from
        /// the Host's game-flow state (<see cref="ObserveGameStage"/>). None outside the game flow and for the 2:2 battle.
        /// </summary>
        public LabStageYokai GameStageYokai => gameFlowEnabled ? gameStageYokai : LabStageYokai.None;
        public LabRoomFamily RoomFamily => gameFlowEnabled ? LabRoomFamily.Game : LabRoomFamily.Lab;
        public int SessionConfigRevision => configRevision;
        public string SessionConfigFingerprint => configFingerprint;
        public bool HasBattleReferences => board != null && target != null && battleFloor != null;
        public bool GameStartHeld => gameFlowEnabled && gameStartHeld;
        private bool CanProcessBattleActions => !gameBattleFrozen && !GameStartHeld && (!gameFlowEnabled || HasBattleReferences);
        /// <summary>A copy of confirmed transport members, independent of chosen P numbers.</summary>
        public ulong[] ConnectedPlayerIds => order.ToArray();
        public Camera BattleCamera { get; private set; }
        private byte[] ActiveConnectionToken => gameFlowEnabled ? GameConnectionToken : LabConnectionToken;

        /// <summary>Choose the room family before Host/Join; live rooms cannot change family.</summary>
        public bool SetGameFlowEnabled(bool enabled)
        {
            if (!CanChooseConnection) return false;
            if (gameFlowEnabled == enabled) return true;
            gameFlowEnabled = enabled;
            // A discovery instance never changes family. Retire any old browse/cache before switching.
            DisposeDiscovery();
            return true;
        }

        /// <summary>
        /// A run owns its roster across model-null Scene loads, results and Game Over. Only
        /// leaving the room resets this latch; a delayed lobby packet must never reopen it.
        /// </summary>
        public void LockGameRoster()
        {
            if (!gameFlowEnabled || gameRosterLocked) return;
            gameRosterLocked = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// 2:2 땅따먹기 (#281)는 한 판이 끝나거나 한 명이 나가면 같은 방의 로비로 돌아간다. 그때만 GameSession이 잠금을 푼다.
        /// </summary>
        public void UnlockGameRoster()
        {
            if (!gameFlowEnabled || !gameRosterLocked) return;
            gameRosterLocked = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// 2:2 땅따먹기 (#281): 전투를 정리하고 연결·P번호·설정은 그대로 둔 채 로비로 돌아간다. 새 참가와 자리 바꾸기가 다시 열린다.
        /// </summary>
        public bool ReturnGameToLobby()
        {
            if (!gameFlowEnabled || !IsHost) return false;
            gameBattleFrozen = false;
            gameStageIndex = 0; gameStageId = null; gameStageYokai = LabStageYokai.None;
            EndBattleToLobby();
            UnlockGameRoster();
            if (!developerSolo) BroadcastLobby();
            return true;
        }

        /// <summary>
        /// Attach the loaded Battle Scene after its board has been configured. No Scene object
        /// becomes persistent: only this transport and the authoritative data survive a load.
        /// </summary>
        public void BindBattleReferences(LabOrbBoard battleBoard, LabTarget battleTarget, Camera camera,
            Collider floor, LabImpactFeedback feedback, Transform projectileContainer = null)
        {
            if (!gameFlowEnabled) throw new InvalidOperationException("Enable game flow before binding a Battle Scene.");
            if (battleBoard == null || battleTarget == null || floor == null)
                throw new ArgumentException("A configured board, target and floor are required.");
            bool preserveStartHold = gameStartHeld;
            UnbindBattleReferences();
            gameStartHeld = preserveStartHold;
            gameBattleFrozen = false;
            board = battleBoard;
            target = battleTarget;
            BattleCamera = camera;
            battleFloor = floor;
            impactFeedback = feedback;
            if (projectileContainer == null)
            {
                var container = new GameObject("Game Projectiles");
                container.transform.SetParent(battleBoard.transform, false);
                ownedGameProjectileRoot = container.transform;
                projectileRoot = ownedGameProjectileRoot;
            }
            else projectileRoot = projectileContainer;
            SubscribeBoard(board);
            ConfigureTargetSpin();
            SyncBoard();
        }

        /// <summary>Release disposable Scene references while keeping result, paint and room data.</summary>
        public void UnbindBattleReferences()
        {
            if (!gameFlowEnabled) return;
            ResetGameStartHold();
            ClearProjectiles();
            // Scene-owned previews and their dependent retries cannot survive the board
            // that produced them. Keep the committed result, paint and room separately.
            ResetActionTransport();
            UnsubscribeBoard(board);
            if (board != null)
            {
                board.SetInteractionEnabled(false);
                board.Clear();
            }
            impactFeedback?.Clear();
            board = null;
            target = null;
            battleFloor = null;
            impactFeedback = null;
            projectileRoot = null;
            BattleCamera = null;
            visibleOrbIds.Clear();
            if (ownedGameProjectileRoot != null) Destroy(ownedGameProjectileRoot.gameObject);
            ownedGameProjectileRoot = null;
            // Paused gameplay must not pause the result or Scene-loading UI.
            ApplyLocalPause(false);
        }

        private void SubscribeBoard(LabOrbBoard value)
        {
            if (value == null) return;
            value.EdgeCrossed += OnEdgeCrossed;
            value.CombineRequested += OnCombineRequested;
            value.FoxFireTouched += OnFoxFireTouched;
            value.ThrowRequested += OnThrowRequested;
            value.UseRequested += OnUseRequested;
            value.DrumTapped += Generate;
            value.JigwiBurnRequested += OnJigwiBurnRequested;
        }

        private void UnsubscribeBoard(LabOrbBoard value)
        {
            if (value == null) return;
            value.EdgeCrossed -= OnEdgeCrossed;
            value.CombineRequested -= OnCombineRequested;
            value.FoxFireTouched -= OnFoxFireTouched;
            value.ThrowRequested -= OnThrowRequested;
            value.UseRequested -= OnUseRequested;
            value.DrumTapped -= Generate;
            value.JigwiBurnRequested -= OnJigwiBurnRequested;
        }

        public bool SendGameFlowToHost(string json)
        {
            if (!CanSendGameFlow(json)) return false;
            if (IsHost) { GameFlowMessage?.Invoke(LocalPlayerId, json); return true; }
            return Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "game_flow", reason = json });
        }

        /// <summary>Host broadcasts to remote peers; the caller applies its local state explicitly.</summary>
        public bool BroadcastGameFlow(string json)
        {
            if (!IsHost || !CanSendGameFlow(json)) return false;
            bool sent = true;
            foreach (ulong id in order.ToArray())
                if (id != LocalPlayerId)
                    sent &= Send(id, new LabNetworkPacket { type = "game_flow", reason = json });
            return sent;
        }

        public bool SendGameFlowToPeer(ulong playerId, string json)
        {
            if (!IsHost || !CanSendGameFlow(json) || !order.Contains(playerId)) return false;
            if (playerId == LocalPlayerId) { GameFlowMessage?.Invoke(LocalPlayerId, json); return true; }
            return Send(playerId, new LabNetworkPacket { type = "game_flow", reason = json });
        }

        private bool CanSendGameFlow(string json) => gameFlowEnabled && IsConnected && ValidGameFlowJson(json);

        private static bool ValidGameFlowJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumGameFlowBytes) return false;
            try { return Utf8.GetByteCount(json) <= MaximumGameFlowBytes; }
            catch (EncoderFallbackException) { return false; }
        }

        private void ReceiveGameFlow(ulong sender, string json)
        {
            if (!CanSendGameFlow(json)) return;
            if (IsHost)
            {
                if (sender == LocalPlayerId || !order.Contains(sender)) return;
            }
            else if (sender != NetworkManager.ServerClientId) return;
            GameFlowMessage?.Invoke(sender, json);
        }

        /// <summary>Open a DEV Solo room without starting combat before Battle finishes loading.</summary>
        public bool PrepareDeveloperSoloGame()
        {
            if (!gameFlowEnabled || !DeveloperBuildAvailable || !CanChooseConnection) return false;
            Host();
            if (!IsHost || order.Count != 1) return false;
            developerSolo = true;
            DisposeDiscovery();
            return true;
        }

        /// <summary>Clear one completed round while retaining transport, P choices and DEV settings.</summary>
        public bool PrepareGameStage()
        {
            if (!gameFlowEnabled || !IsHost) return false;
            LockGameRoster();
            gameBattleFrozen = false;
            EndBattleToLobby();
            if (!developerSolo) BroadcastLobby();
            return true;
        }

        /// <summary>Install one Host-authored effective profile and invalidate old settings ACKs.</summary>
        public bool PrepareGameStage(LabConfig stageConfig, int stageIndex, string stageId, LabStageYokai yokai)
        {
            if (!gameFlowEnabled || !IsHost || stageConfig == null
                || !ValidGameStage(stageIndex, stageId, yokai, stageConfig.TeamTerritoryEnabled)
                || !TryInstallSessionConfig(stageConfig, out _)) return false;
            if (!PrepareGameStage()) return false;
            gameStageIndex = stageIndex;
            gameStageId = stageId;
            gameStageYokai = yokai;
            PublishHostConfigRevision();
            return true;
        }

        /// <summary>
        /// A Guest learns the stage's yokai from the Host's game-flow state (#351), so stage-only presentation (the yaguang
        /// fever, the gumiho BGM) plays on every device and not only the Host's. The Host keeps the one it prepared.
        /// </summary>
        public void ObserveGameStage(LabStageYokai yokai)
        {
            if (!gameFlowEnabled || IsHost || !Enum.IsDefined(typeof(LabStageYokai), yokai)) return;
            gameStageYokai = yokai;
        }

        /// <summary>The three yokai stages, or the single yokai-free 2:2 territory battle (#281).</summary>
        private static bool ValidGameStage(int stageIndex, string stageId, LabStageYokai yokai, bool territory) =>
            stageIndex >= 1 && stageIndex <= 3 && !string.IsNullOrWhiteSpace(stageId)
            && stageId.Length <= 64 && !System.Linq.Enumerable.Any(stageId, char.IsControl)
            && Enum.IsDefined(typeof(LabStageYokai), yokai)
            && (territory ? stageIndex == 1 && yokai == LabStageYokai.None : (int)yokai == stageIndex);

        /// <summary>Both the Scene-ready receipt and start command name the exact settings generation.</summary>
        public bool MatchesGameStageConfig(int revision, string fingerprint) =>
            revision > 0 && revision == configRevision && !string.IsNullOrEmpty(fingerprint)
            && fingerprint == configFingerprint && SessionConfigReady;

        public bool StartCurrentGameStage(int stageIndex, string stageId, LabStageYokai yokai,
            int settingsRevision, string settingsFingerprint)
        {
            if (config == null || !ValidGameStage(stageIndex, stageId, yokai, config.TeamTerritoryEnabled) || stageIndex != gameStageIndex
                || stageId != gameStageId || yokai != gameStageYokai
                || !MatchesGameStageConfig(settingsRevision, settingsFingerprint)) return false;
            return StartCurrentGameStage();
        }

        /// <summary>Only the game-flow load barrier calls this once every Battle Scene is ready.</summary>
        public bool StartCurrentGameStage()
        {
            if (!gameFlowEnabled || !IsHost || !HasBattleReferences || model != null || latest != null) return false;
            if (developerSolo)
            {
                if (order.Count != 1) return false;
                BeginDeveloperSoloRound();
            }
            else
            {
                gameStageStarting = true;
                try { StartBattle(); }
                finally { gameStageStarting = false; }
            }
            return latest != null && latest.phase == LabPhase.Playing;
        }

        /// <summary>NGO's synchronized server clock; unrelated to the paused gameplay clock.</summary>
        public bool TryGetGameServerTime(out double now)
        {
            now = 0d;
            if (!gameFlowEnabled || !IsConnected || manager.NetworkTimeSystem == null
                || manager.NetworkTickSystem == null) return false;
            now = manager.ServerTime.Time;
            return !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0d;
        }

        /// <summary>Internal start barrier. It never exposes a user pause or freezes the loading Canvas.</summary>
        public void SetGameStartHeld(bool held)
        {
            if (!gameFlowEnabled || gameStartHeld == held) return;
            gameStartHeld = held;
            if (IsHost && model != null && model.Phase == LabPhase.Playing)
            {
                if (held) battleClock.Pause(Time.realtimeSinceStartupAsDouble);
                else if (!gameBattleFrozen) battleClock.Resume(Time.realtimeSinceStartupAsDouble);
            }
            if (held) ApplyLocalPause(false);
            ApplyGameStartSimulationHold();
            SyncBoard();
            Changed?.Invoke();
        }

        /// <summary>Create and publish a round without allowing its rules or actions to run.</summary>
        public bool PrepareCurrentGameStage(int stageIndex, string stageId, LabStageYokai yokai,
            int settingsRevision, string settingsFingerprint)
        {
            if (!gameFlowEnabled || !IsHost || model != null || latest != null) return false;
            SetGameStartHeld(true);
            if (!StartCurrentGameStage(stageIndex, stageId, yokai, settingsRevision, settingsFingerprint)) return false;
            preparedGameSession = latest.sessionId;
            preparedGameRound = latest.round;
            return IsGameInitialStateReady(preparedGameSession, preparedGameRound);
        }

        /// <summary>Snapshot, paint and the owner checkpoint must belong to this exact prepared round.</summary>
        public bool IsGameInitialStateReady(string battleSessionId, uint battleRound)
        {
            if (!gameFlowEnabled || !IsConnected || !HasBattleReferences || !SessionConfigReady
                || string.IsNullOrEmpty(battleSessionId) || battleRound == 0 || latest == null
                || latest.sessionId != battleSessionId || latest.round != battleRound
                || latest.phase != LabPhase.Playing || latest.players == null
                || Array.Find(latest.players, player => player != null && player.id == LocalPlayerId) == null
                || displayedPaintSession != battleSessionId || displayedPaintRound != battleRound
                || displayedPaintRevision != latest.paintRevision || displayedPaintCount != latest.paintCount
                || clientPaintSurface.Columns != latest.paintColumns || clientPaintSurface.Rows != latest.paintRows)
                return false;
            if (!IsHost && (actionSession != battleSessionId || actionRound != battleRound
                || boardReplica.LatestSequence <= 0 || boardReplica.NeedsCheckpoint || !PaintReceiver.Matches(latest)))
                return false;
            // An empty local board is valid: some participants do not initially hold a drum.
            var shown = new System.Collections.Generic.HashSet<string>(board.OrbIds, StringComparer.Ordinal);
            foreach (LabOrbState orb in latest.orbs ?? Array.Empty<LabOrbState>())
                if (orb != null && orb.owner == LocalPlayerId && !orb.inFlight && !shown.Contains(orb.id)) return false;
            foreach (LabOrbState drum in latest.drums ?? Array.Empty<LabOrbState>())
                if (drum != null && drum.owner == LocalPlayerId && !shown.Contains(drum.id)) return false;
            return true;
        }

        /// <summary>Only the Host releases the exact prepared identity after the shared countdown.</summary>
        public bool ReleasePreparedGameStage(string battleSessionId, uint battleRound)
        {
            if (!IsHost || !GameStartHeld || gameBattleFrozen || preparedGameSession != battleSessionId
                || preparedGameRound != battleRound || !IsGameInitialStateReady(battleSessionId, battleRound)) return false;
            SetGameStartHeld(false);
            preparedGameSession = null;
            preparedGameRound = 0;
            PublishSnapshot(true);
            return true;
        }

        private void ResetGameStartHold()
        {
            gameStartHeld = false;
            preparedGameSession = null;
            preparedGameRound = 0;
            ApplyGameStartSimulationHold();
        }

        private void ApplyGameStartSimulationHold()
        {
            if (board != null) board.SetSimulationHeld(GameStartHeld);
            if (target != null) target.GetComponent<LabHangingTarget>()?.SetSimulationHeld(GameStartHeld);
        }

        /// <summary>Apply a Scene-independent copy of DEV Solo settings before its same-stage restart.</summary>
        public bool RestartGameSoloWithConfig(LabConfig candidate)
        {
            if (!gameFlowEnabled || !DeveloperBuildAvailable || !developerSolo || !IsHost
                || GameStartHeld || order.Count != 1 || latest == null || latest.phase != LabPhase.Playing || !latest.paused)
                return false;
            if (!ValidateGameDeveloperConfig(candidate, out string validationError))
            {
                SetNotice(validationError);
                return false;
            }
            if (!TryInstallSessionConfig(candidate, out string error))
            {
                SetNotice(error);
                return false;
            }
            RequestGameSoloRestart();
            return true;
        }

        private void RequestGameSoloRestart()
        {
            EndBattleToLobby();
            GameFlowDeveloperRestartRequested?.Invoke();
        }

        private bool ValidateGameDeveloperConfig(LabConfig candidate, out string error)
        {
            error = string.Empty;
            return !gameFlowEnabled || GameFlowDeveloperConfigValidation == null
                || GameFlowDeveloperConfigValidation(candidate, out error);
        }

        /// <summary>Keep a disconnect/exit terminal screen stable until each player returns Home.</summary>
        public void FreezeGameBattle()
        {
            if (!gameFlowEnabled) return;
            gameBattleFrozen = true;
            if (IsHost && model != null && model.Phase == LabPhase.Playing)
                battleClock.Pause(Time.realtimeSinceStartupAsDouble);
            if (board != null) board.SetInteractionEnabled(false);
            ClearProjectiles();
            ApplyLocalPause(false);
        }
    }
}
