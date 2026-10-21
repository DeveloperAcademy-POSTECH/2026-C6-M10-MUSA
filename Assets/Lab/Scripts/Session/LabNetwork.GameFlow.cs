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
        private static readonly byte[] GameConnectionToken = Encoding.ASCII.GetBytes("C6HapioGame/1");
        private const int MaximumGameFlowBytes = 8192;
        private bool gameFlowEnabled;
        private bool gameBattleFrozen;
        private bool gameStageStarting;
        private bool gameRosterLocked;
        private Transform ownedGameProjectileRoot;

        public event Action<ulong, string> GameFlowMessage;
        /// <summary>Raised before connection cleanup; true means this client lost its Host.</summary>
        public event Action<ulong, bool> GameFlowDisconnected;
        /// <summary>Accepted DEV settings require the same-stage Scene load barrier before combat restarts.</summary>
        public event Action GameFlowDeveloperRestartRequested;
        public bool GameFlowEnabled => gameFlowEnabled;
        public bool HasBattleReferences => board != null && target != null && battleFloor != null;
        /// <summary>A copy of confirmed transport members, independent of chosen P numbers.</summary>
        public ulong[] ConnectedPlayerIds => order.ToArray();
        public Camera BattleCamera { get; private set; }
        private byte[] ActiveConnectionToken => gameFlowEnabled ? GameConnectionToken : LabConnectionToken;

        /// <summary>Choose the room family before Host/Join; live rooms cannot change family.</summary>
        public bool SetGameFlowEnabled(bool enabled)
        {
            if (!CanChooseConnection) return false;
            gameFlowEnabled = enabled;
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
        /// Attach the loaded Battle Scene after its board has been configured. No Scene object
        /// becomes persistent: only this transport and the authoritative data survive a load.
        /// </summary>
        public void BindBattleReferences(LabOrbBoard battleBoard, LabTarget battleTarget, Camera camera,
            Collider floor, LabImpactFeedback feedback, Transform projectileContainer = null)
        {
            if (!gameFlowEnabled) throw new InvalidOperationException("Enable game flow before binding a Battle Scene.");
            if (battleBoard == null || battleTarget == null || floor == null)
                throw new ArgumentException("A configured board, target and floor are required.");
            UnbindBattleReferences();
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
            ClearProjectiles();
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
            value.ThrowRequested += OnThrowRequested;
            value.UseRequested += OnUseRequested;
            value.DrumTapped += Generate;
        }

        private void UnsubscribeBoard(LabOrbBoard value)
        {
            if (value == null) return;
            value.EdgeCrossed -= OnEdgeCrossed;
            value.CombineRequested -= OnCombineRequested;
            value.ThrowRequested -= OnThrowRequested;
            value.UseRequested -= OnUseRequested;
            value.DrumTapped -= Generate;
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

        /// <summary>Apply a Scene-independent copy of DEV Solo settings before its same-stage restart.</summary>
        public bool RestartGameSoloWithConfig(LabConfig candidate)
        {
            if (!gameFlowEnabled || !DeveloperBuildAvailable || !developerSolo || !IsHost
                || order.Count != 1 || latest == null || latest.phase != LabPhase.Playing || !latest.paused)
                return false;
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
