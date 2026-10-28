using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Small, owner-scoped Jigwi controls; no LAN discovery or paint transport changes.</summary>
    public sealed partial class LabNetwork
    {
        private readonly Dictionary<string, LabJigwiFireSurface> jigwiFireSurfaces =
            new Dictionary<string, LabJigwiFireSurface>(StringComparer.Ordinal);
        private readonly HashSet<long> predictedJigwiBurns = new HashSet<long>();
        private Transform jigwiFireRoot;

        /// <summary>The standalone Lab keeps its manual tools; a game run exposes them only in Jigwi's stage.</summary>
        public bool JigwiManualControlsAllowed => (!gameFlowEnabled || gameStageYokai == LabStageYokai.Jigwi)
            && !(gameFlowEnabled && gameStageYokai == LabStageYokai.Jigwi && config != null && config.JigwiStageTimelineEnabled);

        public bool CanDevStartJigwi => JigwiManualControlsAllowed && DeveloperBuildAvailable && IsHost && IsConnected && model != null
            && (model.StageYokai == LabStageYokai.None || model.StageYokai == LabStageYokai.Jigwi)
            && model.Phase == LabPhase.Playing && !battleClock.IsPaused && SessionConfigReady;

        public bool DevStartJigwiFire(ulong player)
        {
            if (!CanDevStartJigwi) return false;
            bool started = model.TryStartJigwiFire(player, battleClock.Now(Time.realtimeSinceStartupAsDouble), out string reason);
            SetNotice(reason);
            if (started) PublishSnapshot(true);
            return started;
        }

        public bool DevStartJigwiGenerationBurn(ulong player)
        {
            if (!CanDevStartJigwi) return false;
            bool started = model.TryStartJigwiGenerationBurn(player, battleClock.Now(Time.realtimeSinceStartupAsDouble), out string reason);
            SetNotice(reason);
            if (started) PublishSnapshot(true);
            return started;
        }

        private void OnJigwiBurnRequested(string first, string second)
        {
            if (latest == null || latest.phase != LabPhase.Playing || latest.paused)
            {
                board?.RejectPendingLaunch(first);
                if (second != null) board?.RejectPendingLaunch(second);
                return;
            }
            Request(new LabNetworkPacket { type = "jigwi_burn", requestId = ++requestSequence,
                firstId = first, secondId = second });
        }

        private void CommitJigwiBurn(LabNetworkPacket request)
        {
            if (predictedShots.TryGetValue(request.requestId, out var shot) && shot != null)
                Destroy(shot.gameObject);
            predictedShots.Remove(request.requestId);
            if (request.type == "generate")
            {
                if (!predictedJigwiBurns.Contains(request.requestId))
                    board.PlayJigwiGenerationBurn(new Vector2(request.x, request.y));
                return; // The drum is the request input, never a consumed orb.
            }
            if (request.type == "combine" && !string.IsNullOrEmpty(request.provisionalId))
                board.RejectCombinePreview(request.provisionalId);
            string first = ResolveLocalId(request.firstId);
            string second = TakesTwoOrbs(request) ? ResolveLocalId(request.secondId) : null;
            board.PlayJigwiBurnPreview(first, second);
            board.CommitPendingLaunch(first);
            if (second != null) board.CommitPendingLaunch(second);
        }

        private IEnumerable<LabJigwiFireSurface> JigwiFireSurfacesFor(ulong player) =>
            jigwiFireSurfaces.Values.Where(surface => surface != null && surface.OwnerPlayerId == player);

        private void SyncJigwiFireSurfaces()
        {
            if (latest == null || latest.phase != LabPhase.Playing || target == null || config == null)
            {
                ClearJigwiFireSurfaces();
                return;
            }
            if (latest.jigwiAB?.enabled == true)
            {
                SyncJigwiABFireSurfaces();
                return;
            }
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (LabJigwiState fire in latest.jigwi ?? Array.Empty<LabJigwiState>())
            {
                if (fire == null || !fire.FireActive(latest.serverTime)) continue;
                LabPlayerState player = latest.players?.FirstOrDefault(candidate => candidate.id == fire.playerId);
                if (player == null) continue;
                foreach (LabJigwiFireTarget spot in fire.targets)
                {
                    if (spot == null || spot.extinguished) continue;
                    string key = fire.playerId + "/" + fire.fireId + "/" + spot.id;
                    active.Add(key);
                    Vector3 position = LabJigwiFireSurface.WorldPosition(target, player.seat,
                        latest.players.Length, spot.x, spot.y, config);
                    float radius = Mathf.Clamp(config.ThrowRadius * 2.6f, .25f, 1.2f);
                    if (!jigwiFireSurfaces.TryGetValue(key, out var surface) || surface == null)
                    {
                        if (jigwiFireRoot == null)
                        {
                            var root = new GameObject("Jigwi Fire Targets");
                            root.transform.SetParent(transform, false);
                            jigwiFireRoot = root.transform;
                        }
                        surface = LabJigwiFireSurface.Create(jigwiFireRoot, fire.playerId, fire.fireId,
                            spot.id, position, radius, fire.playerId == LocalPlayerId);
                        jigwiFireSurfaces[key] = surface;
                    }
                    else surface.Configure(position, radius, true, fire.playerId == LocalPlayerId);
                }
            }
            foreach (string key in jigwiFireSurfaces.Keys.Where(key => !active.Contains(key)).ToArray())
            {
                var surface = jigwiFireSurfaces[key];
                if (surface != null) { surface.gameObject.SetActive(false); Destroy(surface.gameObject); }
                jigwiFireSurfaces.Remove(key);
            }
        }

        private void ClearJigwiFireSurfaces()
        {
            jigwiFireSurfaces.Clear();
            if (jigwiFireRoot == null) return;
            jigwiFireRoot.gameObject.SetActive(false);
            Destroy(jigwiFireRoot.gameObject);
            jigwiFireRoot = null;
        }
    }

    /// <summary>Reject malformed bounded fire metadata before it reaches runtime visuals.</summary>
    public static class LabJigwiNetworkValidation
    {
        public const int MaximumFireTargets = 12;
        public static LabJigwiState[] Copy(LabJigwiState[] states) =>
            (states ?? Array.Empty<LabJigwiState>()).Select(state => state?.Copy()).ToArray();

        public static bool Valid(LabJigwiState[] states)
        {
            if (states == null || states.Length > LabBoardReplica.MaximumPlayers) return false;
            var players = new HashSet<ulong>();
            foreach (LabJigwiState state in states)
            {
                if (state == null || !players.Add(state.playerId) || !Time(state.fireStartedAt)
                    || !Time(state.fireEndsAt) || !Time(state.overheatUntil) || !Time(state.generationBurnUntil)
                    || state.targets == null || state.targets.Length > MaximumFireTargets) return false;
                if (state.targets.Length > 0 && (!Id(state.fireId) || state.fireStartedAt < 0d
                    || state.fireEndsAt <= state.fireStartedAt)) return false;
                var targets = new HashSet<string>(StringComparer.Ordinal);
                foreach (LabJigwiFireTarget target in state.targets)
                    if (target == null || !Id(target.id) || !targets.Add(target.id) || !Unit(target.x) || !Unit(target.y)) return false;
            }
            return true;
        }

        private static bool Time(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= -1d;
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
    }
}
