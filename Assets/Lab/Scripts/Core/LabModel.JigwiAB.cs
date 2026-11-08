using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Screen-only A/B policy, separate from the retained surface and standalone Lab experiments.</summary>
    public sealed partial class LabModel
    {
        private readonly List<LabJigwiABRound> jigwiABRounds = new List<LabJigwiABRound>();
        private readonly HashSet<string> jigwiABActivated = new HashSet<string>(StringComparer.Ordinal);
        private bool jigwiABEnabled;
        private const double JigwiABMissionSeconds = LabJigwiABTimeline.GroupMissionSeconds;
        private const uint JigwiGroupPickSalt = 0x509B1C4Bu;
        // #509: where each picked seat's last counted Water hit the boss, for the success splash.
        private readonly Dictionary<ulong, Vector3> jigwiSyncDirections = new Dictionary<ulong, Vector3>();
        private float jigwiSplashShare, jigwiBoardFireSeconds;
        /// <summary>#540: the timetable this battle runs and, for the flow timetable, the common gimmick rhythm.</summary>
        private int jigwiSchedule;
        private double jigwiCommonStart = 40d, jigwiCommonInterval = 15d;
        /// <summary>#540: once Jigwi's line is over, every orb on an unfinished talisman's board burns away together in this long.</summary>
        public const double JigwiTalismanFailBlazeSeconds = 1d;
        /// <summary>#540: the fireball falls onto a failed seat's board this long after the A ends.</summary>
        public const double JigwiTalismanFailBurnDelaySeconds = .45d;
        /// <summary>
        /// #540: the first orb catches fire this long after the A ends — once Jigwi's "모두 재가 되어라!" (`JigwiVoiceAFail`, 3.64 s,
        /// played as the fireball falls) has finished.
        /// </summary>
        public const double JigwiTalismanFailFirstBurnSeconds = 3.7d;
        // #479: A burns each owner's orbs one at a time. The last queued deadline per owner for the running A.
        private readonly Dictionary<ulong, double> jigwiABLastBurnAt = new Dictionary<ulong, double>();
        private string jigwiABBurnQueueId;
        /// <summary>#540: no orb burns while the talisman is being written; only an A that runs out burns the board.</summary>
        private bool jigwiAHoldBurn;
        /// <summary>
        /// #540: orbs whose lifetime is paused while their seat writes the talisman (orb id → seat). Host memory only;
        /// the pause shows in the snapshot as a later expiresAt.
        /// </summary>
        private readonly Dictionary<string, ulong> jigwiAPausedOrbs = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private int jigwiTalismanWaterCount;
        /// <summary>#509: talisman Water drops in one at a time; each pending orb's owner, index and Host due time.</summary>
        private readonly List<(ulong owner, int index, double dueAt)> jigwiTalismanDrops = new List<(ulong, int, double)>();
        /// <summary>#540: boards whose talisman failed, and when (the end of Jigwi's line) every orb then on them catches fire.</summary>
        private readonly List<(ulong owner, string roundId, double dueAt)> jigwiFailBurns = new List<(ulong, string, double)>();
        public const double JigwiTalismanDropSeconds = .3d;
        /// <summary>#540: after the talisman orb lands, the seat's other orbs stay at least this long so the reward is not lost in smoke.</summary>
        public const double JigwiTalismanCalmSeconds = 1d;
        /// <summary>#540: a thrown talisman orb flies as this many Water orbs side by side (itself + shards); each paints.</summary>
        public const int JigwiTalismanVolleyCount = 3;
        /// <summary>#540: the projectile id of the k-th shard (1, 2) flying beside talisman orb orbId.</summary>
        public static string JigwiTalismanShardId(string orbId, int k) => orbId + "/talisman-" + k;
        public static bool IsJigwiTalismanShard(string id) => id != null && id.Contains("/talisman-");
        // #540: shards in the air → (thrower, paint share). The Host's projectiles resolve them like the orb itself.
        private readonly Dictionary<string, (ulong owner, float paint)> jigwiTalismanShards
            = new Dictionary<string, (ulong, float)>(StringComparer.Ordinal);
        /// <summary>#540: where the i-th talisman Water appears on its owner's board (the folded talisman drops right there).</summary>
        public static Vector2 JigwiTalismanWaterSpot(int i) =>
            new Vector2(.15f + .7f * ((i * .61803398875f + .5f) % 1f), .22f + .1f * (i % 3));
        private float jigwiTalismanWaterPaint, jigwiTalismanRewardDelay;
        /// <summary>#540: 부적 구슬은 수명이 없다(오색구슬처럼 만든 시각부터 아주 멀리 끝을 둔다). 던질 때까지 남는다.</summary>
        public const double JigwiTalismanOrbLifetimeSeconds = 1e6d;
        private float jigwiABDuration, jigwiABBurnSeconds, jigwiABBurnStagger,
            jigwiABMoveAmount, jigwiABMovePeriod, jigwiABTargetRadius;
        private ulong jigwiABFailureRevision;
        public bool JigwiABEnabled => jigwiABEnabled;
        public ulong JigwiABFailureRevision => jigwiABFailureRevision;
        /// <summary>Small independent outcome metadata, without copying paint maps or the complete battle snapshot.</summary>
        public LabJigwiABSnapshot JigwiABState => CopyJigwiABSnapshot(lastTime);
        private LabJigwiABRound ActiveJigwiABRound(double at) => jigwiABRounds.FirstOrDefault(value => value.Active(at));
        private LabJigwiABRound JigwiABRewardAt(double at) => phase == LabPhase.Playing && jigwiABEnabled
            ? jigwiABRounds.LastOrDefault(value => value.RewardActive(at)) : null;

        private void ResetJigwiAB(LabConfig config, double now)
        {
            jigwiABEnabled = !teamMode && StageYokai == LabStageYokai.Jigwi && config.JigwiStageTimelineEnabled;
            jigwiSchedule = config.JigwiBTestOnly ? LabJigwiABTimeline.BTestSchedule // #551 시험용: B만
                : config.JigwiFlowSchedule ? LabJigwiABTimeline.FlowSchedule : LabJigwiABTimeline.LegacySchedule;
            jigwiAHoldBurn = config.JigwiAHoldBurnUntilEnd;
            jigwiBBucket = config.JigwiBBucket;
            jigwiBCountdown = config.JigwiBCountdown || jigwiBBucket; // the bucket runs on the 3-2-1
            jigwiCommonStart = config.JigwiCommonStartSeconds; jigwiCommonInterval = config.JigwiCommonIntervalSeconds;
            jigwiABRounds.Clear(); jigwiABActivated.Clear(); jigwiABFailureRevision = 0;
            jigwiABLastBurnAt.Clear(); jigwiABBurnQueueId = null; jigwiTalismanDrops.Clear(); jigwiFailBurns.Clear(); jigwiAPausedOrbs.Clear(); jigwiTalismanShards.Clear();
            ResetJigwiABSupply(config);
            if (!jigwiABEnabled) return;
            // Preserve the old code as an inactive migration path, never run two policies together.
            jigwiStageEnabled = false; jigwiStageGroups.Clear(); jigwiGourdBlastIds.Clear();
            jigwiStageNextSlot = 0; jigwiStageCutoffReached = false;
            jigwiStageStartedAt = now; jigwiStageCutoffAt = now + 100d;
            jigwiStageWarning = (float)LabJigwiABTimeline.WarningSeconds; jigwiStageFever = config.JigwiStageFeverMultiplier;
            jigwiABDuration = Mathf.Min(15f, config.JigwiABPersonalDurationSeconds);
            jigwiABBurnSeconds = config.JigwiABPersonalBurnSeconds; jigwiABBurnStagger = config.JigwiABBurnStaggerSeconds;
            jigwiTalismanWaterCount = config.JigwiTalismanWaterCount;
            jigwiTalismanRewardDelay = config.JigwiTalismanRewardDelaySeconds;
            jigwiTalismanWaterPaint = config.JigwiTalismanWaterPaintScale;
            jigwiSplashShare = config.JigwiBSplashPaintShare; jigwiBoardFireSeconds = config.JigwiBBoardFireSeconds;
            jigwiSyncDirections.Clear();
            jigwiABMoveAmount = config.JigwiABFireMoveAmount; jigwiABMovePeriod = config.JigwiABFireMovePeriodSeconds;
            jigwiABTargetRadius = config.JigwiABFireTargetRadius;
            paintSurface.SetPaintBlocker(null);
            nextCommonGimmickAt = NextJigwiABCommonAt(0u);
            nextUniqueGimmickAt = nextComboAt = nextFlashAt = nextStageJigwiAt = double.PositiveInfinity;
        }
        private double NextJigwiABCommonAt(uint index)
        {
            if (jigwiStageCutoffReached || jigwiSchedule == LabJigwiABTimeline.BTestSchedule) return double.PositiveInfinity; // #551 B만 시험
            if (jigwiSchedule != LabJigwiABTimeline.FlowSchedule)
                return index < JigwiStageCommonSlots.Length ? jigwiStageStartedAt + JigwiStageCommonSlots[index] : double.PositiveInfinity;
            // #540: from the start of the middle phase, every interval, until the fever.
            double offset = jigwiCommonStart + index * Math.Max(3d, jigwiCommonInterval);
            return offset < jigwiStageCutoffAt - jigwiStageStartedAt ? jigwiStageStartedAt + offset : double.PositiveInfinity;
        }
        private double NextJigwiABBoundary(double requested)
        {
            if (phase != LabPhase.Playing || jigwiStageCutoffReached) return requested;
            double next = requested;
            void Include(double at) { if (at > lastTime && at < next) next = at; }
            Include(jigwiStageCutoffAt);
            foreach (var drop in jigwiTalismanDrops) Include(drop.dueAt); // #509 talisman Water, one at a time
            foreach (var burn in jigwiFailBurns) Include(burn.dueAt); // #540 the failed boards catch fire right on time
            if (jigwiStageNextSlot < LabJigwiABTimeline.SlotCountFor(jigwiSchedule))
                Include(jigwiStageStartedAt + LabJigwiABTimeline.ActiveOffset(jigwiStageNextSlot, jigwiSchedule) - LabJigwiABTimeline.WarningSeconds);
            foreach (var item in jigwiABRounds)
            {
                if (item.resolution == LabJigwiABResolution.None)
                {
                    Include(item.activeAt); Include(item.endsAt);
                    if (item.kind == LabJigwiABKind.GroupFire && item.syncWindowAt >= 0d)
                        Include(item.syncWindowAt + item.SyncWindow);
                    // #551: "지금!" and the moment everyone must have let go.
                    if (item.kind == LabJigwiABKind.GroupFire && item.countdownAt >= 0d)
                    { Include(item.GoAt); Include(item.GoAt + LabJigwiABTimeline.ReleaseSeconds); }
                }
                Include(item.boardFireUntil); // #509: the board fire ends exactly on time

            }
            return next;
        }
        private void CancelJigwiABForBattleEnd(double now)
        {
            foreach (var item in jigwiABRounds.Where(value => value.resolution == LabJigwiABResolution.None).ToArray())
                ResolveJigwiAB(item, LabJigwiABResolution.Cancelled, Math.Min(now, jigwiStageCutoffAt));
        }
        private void AdvanceJigwiAB(double now)
        {
            if (phase != LabPhase.Playing)
            {
                jigwiTalismanDrops.Clear(); jigwiFailBurns.Clear();
                CancelJigwiABForBattleEnd(now);
                return;
            }
            GrantDueJigwiTalismanWater(now);
            IgniteDueJigwiFailBurns(now);
            if (!jigwiStageCutoffReached && now >= jigwiStageCutoffAt)
            {
                CancelJigwiABForBattleEnd(jigwiStageCutoffAt);
                EndJigwiStageInterference();
            }
            if (jigwiStageCutoffReached) return;
            // Resolve an ending A before activating B at the same instant. Its warning may already be visible.
            foreach (var item in jigwiABRounds.Where(value => value.resolution == LabJigwiABResolution.None).ToArray())
                if (now >= item.endsAt) ResolveJigwiAB(item, LabJigwiABResolution.Expired, item.endsAt);
            while (jigwiStageNextSlot < LabJigwiABTimeline.SlotCountFor(jigwiSchedule)
                && jigwiStageStartedAt + LabJigwiABTimeline.ActiveOffset(jigwiStageNextSlot, jigwiSchedule) - LabJigwiABTimeline.WarningSeconds <= now)
            {
                int slot = jigwiStageNextSlot++;
                BeginJigwiABSlot(slot, jigwiStageStartedAt + LabJigwiABTimeline.ActiveOffset(slot, jigwiSchedule));
            }
            var active = ActiveJigwiABRound(now);
            if (active != null) AdvanceJigwiABActive(active, now);
        }
        private void AdvanceJigwiABActive(LabJigwiABRound active, double now)
        {
            bool firstActivation = jigwiABActivated.Add(active.id);
            if (firstActivation)
            {
                // A stable order so each owner's orbs catch fire one after another in creation order.
                foreach (var orb in orbs.Values.OrderBy(value => value.createdAt).ThenBy(value => value.id, StringComparer.Ordinal).ToArray())
                    ApplyJigwiABOrb(orb, active.activeAt);
                if (active.kind == LabJigwiABKind.PersonalBurn && jigwiAHoldBurn) PauseJigwiAOrbLifetimes(active);
                revision++;
            }
            if (active.kind != LabJigwiABKind.GroupFire) return;
            CloseJigwiSyncWindow(active, now);
            FailLateJigwiBRelease(active, now);
            if (firstActivation && !active.bucket) StartJigwiABSupply(active, now); // #551 물통엔 특수 물구슬이 없다
        }
        private void BeginJigwiABSlot(int slot, double activeAt)
        {
            LabJigwiABKind kind = LabJigwiABTimeline.KindForSlot(slot, jigwiSchedule);
            bool personal = kind == LabJigwiABKind.PersonalBurn;
            var item = new LabJigwiABRound
            {
                id = "jigwi-ab/" + sessionId + "/" + round + "/" + slot, slot = slot,
                kind = kind,
                warningAt = activeAt - LabJigwiABTimeline.WarningSeconds, activeAt = activeAt,
                endsAt = Math.Min(jigwiStageCutoffAt, personal
                    ? Math.Min(activeAt + jigwiABDuration, jigwiStageStartedAt + LabJigwiABTimeline.NextOffset(slot, jigwiSchedule))
                    : activeAt + JigwiABMissionSeconds),
                // Reserved shrink fields remain on the wire, but the timed mission never shrinks an orb.
                shrinkAt = activeAt, shrinkScale = 1f,
                moveAmount = jigwiABMoveAmount, movePeriodSeconds = jigwiABMovePeriod, targetRadius = jigwiABTargetRadius,
                // #509: B success no longer gives a timed launch reward.
                rewardSizeScale = 1f, rewardPaintScale = 1f,
                // #551: with the countdown the picked hits may land a little further apart.
                syncWindowSeconds = !personal && jigwiBCountdown ? LabJigwiABTimeline.CountdownSyncWindowSeconds : 0f,
                bucket = !personal && jigwiBBucket
            };
            // #479: A ignites every seat at once; each seat clears its own fire with a talisman.
            if (personal) item.players = seats.Take(activeSeatCount).Select(owner => new LabJigwiABPlayerFire
            {
                playerId = owner, targets = Array.Empty<LabJigwiABFireTarget>()
            }).ToArray();
            // #509: B picks a few seats; each picked screen gets one fire pillar in front of the boss.
            // The others are helpers. The draw is stable per seed/slot and differs between B rounds.
            if (!personal) jigwiSyncDirections.Clear();
            if (!personal) item.players = (item.bucket ? seats.Take(activeSeatCount).ToArray() : JigwiGroupPicks(slot)).Select(owner => new LabJigwiABPlayerFire
            {
                playerId = owner, targets = new[] { new LabJigwiABFireTarget { id = item.id + "/" + owner + "/0", x = .5f, y = .62f } }
            }).ToArray();
            jigwiABRounds.Add(item); revision++;
        }
        private ulong[] JigwiGroupPicks(int slot)
        {
            ulong[] all = seats.Take(activeSeatCount).ToArray();
            int count = LabJigwiABTimeline.GroupPickCount(all.Length);
            return all.OrderBy(owner => DrawFor(seed ^ JigwiGroupPickSalt, owner, (uint)slot)).ThenBy(owner => owner)
                .Take(count).ToArray();
        }
        private void ResolveJigwiAB(LabJigwiABRound item, LabJigwiABResolution result, double at)
        {
            if (item == null || item.resolution != LabJigwiABResolution.None) return;
            item.resolution = result; item.resolvedAt = Math.Max(item.warningAt, at);
            // #540: an A that runs out punishes every seat that did not finish its talisman (each seat for itself):
            // when Jigwi's line is over, every orb then on its board burns away at once. Drums keep working.
            var failed = new HashSet<ulong>();
            if (item.kind == LabJigwiABKind.PersonalBurn && result == LabJigwiABResolution.Expired)
                foreach (var seat in item.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null && seat.talismanAt < 0d) failed.Add(seat.playerId);
            // #540: which orbs burn is decided when Jigwi's line ends (see IgniteDueJigwiFailBurns), so orbs made meanwhile burn too.
            foreach (ulong owner in failed) jigwiFailBurns.Add((owner, item.id, item.resolvedAt + JigwiTalismanFailFirstBurnSeconds));
            foreach (var orb in orbs.Values)
            {
                if (failed.Contains(orb.owner)) continue;
                if (item.kind == LabJigwiABKind.PersonalBurn && orb.jigwiABBurnEventId == item.id)
                { orb.jigwiABBurnEventId = null; orb.jigwiABBurnAt = -1d; orb.revision = ++revision; }
                if (item.kind == LabJigwiABKind.GroupFire && !orb.inFlight && orb.jigwiABRadiusScale != 1f)
                { orb.jigwiABRadiusScale = 1f; orb.revision = ++revision; }
            }
            if (item.kind == LabJigwiABKind.GroupFire)
            {
                ClearJigwiABSupplies(item);
                // #509: failure sets every board on fire for a few seconds (no drum presses); orbs and stamina stay.
                // It stops at the fever cutoff like every other yokai interference.
                if (result == LabJigwiABResolution.Expired && jigwiBoardFireSeconds > 0f)
                    item.boardFireUntil = Math.Max(item.resolvedAt, Math.Min(jigwiStageCutoffAt, item.resolvedAt + jigwiBoardFireSeconds));
            }
            revision++;
        }
        private void ApplyJigwiABOrb(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || orb.inFlight || orb.jigwiTalismanReward || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) return;
            var active = ActiveJigwiABRound(now);
            if (active == null || !active.Active(now)) return;
            if (active.kind == LabJigwiABKind.PersonalBurn && !jigwiAHoldBurn)
            {
                // An orb already burning in this A (combined or passed on) keeps its place in the queue.
                if (orb.jigwiABBurnAt >= 0d && orb.jigwiABBurnEventId == active.id) return;
                // A board whose talisman is finished no longer catches fire (a passed burning orb keeps burning).
                if (active.players.Any(player => player.playerId == orb.owner && player.talismanAt >= 0d)) return;
                double deadline = NextJigwiABBurnAt(active.id, orb.owner, now);
                if (orb.jigwiABBurnAt < 0d || deadline < orb.jigwiABBurnAt)
                { orb.jigwiABBurnAt = deadline; orb.jigwiABBurnEventId = active.id; orb.revision = ++revision; }
            }
        }
        /// <summary>
        /// #479: the first orb on a board burns after the DEV burn delay, each next one a stagger later,
        /// so fire looks like it spreads from orb to orb. Positions are never read.
        /// </summary>
        private double NextJigwiABBurnAt(string roundId, ulong owner, double now)
        {
            if (jigwiABBurnQueueId != roundId) { jigwiABLastBurnAt.Clear(); jigwiABBurnQueueId = roundId; }
            double next = now + jigwiABBurnSeconds;
            if (jigwiABLastBurnAt.TryGetValue(owner, out double last)) next = Math.Max(next, last + jigwiABBurnStagger);
            jigwiABLastBurnAt[owner] = next;
            return next;
        }
        private void CaptureJigwiABLaunchReward(LabOrbState orb, double now)
        {
            // #479: talisman water keeps its own paint scale and never takes B's fire trail or size.
            if (orb.jigwiTalismanReward) { orb.jigwiABReward = false; orb.jigwiABRewardSizeScale = 1f; return; }
            var reward = JigwiABRewardAt(now);
            orb.jigwiABReward = reward != null;
            orb.jigwiABRewardSizeScale = reward != null ? reward.rewardSizeScale : 1f;
            orb.jigwiABRewardPaintScale = reward != null ? reward.rewardPaintScale : 1f;
        }
        private void CarryJigwiABBurn(LabOrbState first, LabOrbState second, LabOrbState combined, double now)
        {
            var earliest = first.jigwiABBurnAt >= 0d && (second.jigwiABBurnAt < 0d || first.jigwiABBurnAt <= second.jigwiABBurnAt)
                ? first : second;
            combined.jigwiABBurnAt = earliest.jigwiABBurnAt; combined.jigwiABBurnEventId = earliest.jigwiABBurnEventId;
            ApplyJigwiABOrb(combined, now);
        }
        /// <summary>#509: B no longer puts a curtain in front of the boss; nobody's boss is blocked.</summary>
        public bool JigwiABBlocksBoss(ulong owner, double now) => false;

        /// <summary>
        /// #509: during B, a picked player's Water Combined that really hits the boss is that player's sync hit
        /// (one second for every picked player, three times). The hit itself is judged as usual afterwards.
        /// </summary>
        private void RecordJigwiBossSyncHit(LabOrbState orb, double now, Vector3 paintDirection)
        {
            if (!jigwiABEnabled || orb.kind != LabOrbKind.Combined || orb.element != LabOrbElement.Water) return;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || !active.Active(now)) return;
            // #509: only this B's special Water counts, for the picked seat it was given to (whoever throws it).
            if (orb.jigwiABSupplyEventId != active.id) return;
            var picked = active.players.FirstOrDefault(player => player.playerId == orb.originOwner);
            var slot = picked?.targets?.FirstOrDefault();
            if (slot == null || !slot.lit) return;
            if (paintDirection.sqrMagnitude > 1e-6f && !float.IsNaN(paintDirection.x)) jigwiSyncDirections[orb.originOwner] = paintDirection.normalized;
            RecordJigwiSyncHit(active, slot, now);
            if (active.resolution == LabJigwiABResolution.Completed) SplashJigwiSuccess(active, now);
        }

        /// <summary>
        /// #509 B success: each picked seat's Water bursts into a big blue splash where it last hit the boss
        /// (the largest single-hit paint share). It can win the battle on the spot.
        /// </summary>
        private void SplashJigwiSuccess(LabJigwiABRound round, double now)
        {
            if (teamMode || jigwiSplashShare <= 0f) return;
            foreach (var picked in round.players)
            {
                if (!players.TryGetValue(picked.playerId, out var player)) continue;
                Vector3 direction = jigwiSyncDirections.TryGetValue(picked.playerId, out var hit) ? hit : Vector3.forward;
                bool painted = paintSurface.RegisterHit(direction, PaintColorFor(LabOrbElement.Water), player.id, player.seat + 1,
                    Mathf.Min(3f, jigwiSplashShare));
                if (painted && paintSurface.IsComplete && phase == LabPhase.Playing)
                {
                    phase = LabPhase.Victory;
                    if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                    AdvanceJigwiStage(now);
                }
            }
            revision++;
        }

        /// <summary>#509: true while a failed B keeps every board on fire (drums cannot be pressed).</summary>
        public bool JigwiBoardOnFire(double now) => jigwiABEnabled && jigwiABRounds.Any(value =>
            value.kind == LabJigwiABKind.GroupFire && value.resolution == LabJigwiABResolution.Expired
            && value.boardFireUntil > now);
        private void ConsumeJigwiABWaterImpact(LabOrbState orb, LabJigwiABPlayerFire owner, double now)
        {
            orbs.Remove(orb.id); launchScales.Remove(orb.id);
            if (orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water)
                owner.lastWaterSplashAt = now;
            revision++;
        }
        /// <summary>Final authority backstop if a real projectile curves around the visible curtain or crosses a collision boundary.</summary>
        private bool TryBlockJigwiABBossHit(LabOrbState orb, double now)
        {
            if (!JigwiABBlocksBoss(orb.owner, now)) return false;
            var owner = ActiveJigwiABRound(now).players.First(player => player.playerId == orb.owner);
            ConsumeJigwiABWaterImpact(orb, owner, now);
            return true;
        }
        /// <summary>Called only for a real Host fire collider collision, without normal paint/hit rewards or statistics.</summary>
        public bool ConfirmJigwiABFireHit(string projectileOrbId, string eventId, string targetId, double now, out bool extinguished)
        {
            extinguished = false;
            if (!jigwiABEnabled || !TryAdvance(now, out _) || phase != LabPhase.Playing
                || !orbs.TryGetValue(projectileOrbId ?? string.Empty, out var orb) || !orb.inFlight) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || !active.Active(now) || active.id != eventId) return false;
            if (!string.IsNullOrEmpty(orb.jigwiABSupplyEventId) && orb.jigwiABSupplyEventId != active.id) return false;
            var owner = active.players.FirstOrDefault(p => p.playerId == orb.owner);
            if (owner == null) return false;
            bool curtain = targetId == LabJigwiABFireMotion.CurtainId(active.id, orb.owner)
                && owner.targets.Any(value => value.lit);
            var fire = owner.targets.FirstOrDefault(value => value.id == targetId && value.lit);
            if (!curtain && fire == null) return false;
            ConsumeJigwiABWaterImpact(orb, owner, now);
            if (!curtain && orb.kind == LabOrbKind.Combined && orb.element == LabOrbElement.Water)
                extinguished = RecordJigwiSyncHit(active, fire, now);
            return true;
        }
        /// <summary>
        /// #509: a picked player's Water on their own pillar. The first hit opens a one-second window; when every
        /// picked player has hit inside it, one synchronized clear counts. Three clears finish B. A late or missing
        /// partner closes the window and resets the count to zero (see <see cref="CloseJigwiSyncWindow"/>).
        /// Returns true only when this hit completed a synchronized clear.
        /// </summary>
        private bool RecordJigwiSyncHit(LabJigwiABRound active, LabJigwiABFireTarget fire, double now)
        {
            CloseJigwiSyncWindow(active, now);
            if (active.resolution != LabJigwiABResolution.None) return false;
            if (active.syncWindowAt < 0d) active.syncWindowAt = now;
            if (fire.extinguishedAt < 0d) fire.extinguishedAt = now;
            revision++;
            if (!active.players.All(p => p.targets.All(value => value.extinguishedAt >= 0d))) return false;
            active.syncCount++;
            active.syncWindowAt = -1d;
            foreach (var target in active.players.SelectMany(p => p.targets)) target.extinguishedAt = -1d;
            if (active.syncCount >= LabJigwiABTimeline.GroupSyncRequired)
            {
                foreach (var target in active.players.SelectMany(p => p.targets)) target.lit = false;
                ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
            }
            return true;
        }
        /// <summary>#509: a window whose second passed without every picked hit fails B (one chance only).</summary>
        private void CloseJigwiSyncWindow(LabJigwiABRound active, double now)
        {
            if (active.syncWindowAt < 0d || now < active.syncWindowAt + active.SyncWindow) return;
            FailJigwiSync(active, active.syncWindowAt + active.SyncWindow);
        }
        private bool jigwiBCountdown, jigwiBBucket;
        /// <summary>
        /// #551 B countdown: a picked seat starts or stops holding its special Water. When every picked seat holds it
        /// the 3-2-1 starts (countdownAt); anyone letting go before "지금!" stops it. After "지금!" holding no longer
        /// matters. False when nothing changed or the request does not apply.
        /// </summary>
        public bool TrySetJigwiBReady(ulong sender, string roundId, bool holding, double now)
        {
            if (!jigwiABEnabled || !jigwiBCountdown || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _)
                || phase != LabPhase.Playing) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || !active.Active(now) || active.id != roundId) return false;
            var seat = active.players.FirstOrDefault(value => value.playerId == sender);
            if (seat == null || active.countdownAt >= 0d && now >= active.GoAt) return false;
            if (holding)
            {
                if (seat.readyAt >= 0d) return false;
                seat.readyAt = now;
                if (active.countdownAt < 0d && active.players.All(value => value.readyAt >= 0d)) active.countdownAt = now;
            }
            else
            {
                if (seat.readyAt < 0d) return false;
                seat.readyAt = -1d;
                active.countdownAt = -1d;
            }
            revision++;
            return true;
        }
        /// <summary>#551: before "지금!" a picked seat's special Water cannot be thrown (letting go only cancels the 3-2-1).</summary>
        private bool JigwiBHoldsLaunch(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || !jigwiBCountdown || orb == null || string.IsNullOrEmpty(orb.jigwiABSupplyEventId)) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || active.id != orb.jigwiABSupplyEventId) return false;
            if (!active.players.Any(value => value.playerId == orb.originOwner)) return false;
            return active.countdownAt < 0d || now < active.GoAt;
        }
        /// <summary>#551: remember when each picked seat threw its special Water after "지금!".</summary>
        private void MarkJigwiBLaunch(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || !jigwiBCountdown || orb == null || string.IsNullOrEmpty(orb.jigwiABSupplyEventId)) return;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.GroupFire || active.id != orb.jigwiABSupplyEventId) return;
            var seat = active.players.FirstOrDefault(value => value.playerId == orb.originOwner);
            if (seat != null && seat.launchedAt < 0d) { seat.launchedAt = now; revision++; }
        }
        /// <summary>#551: someone still held on ReleaseSeconds after "지금!" — the one chance is gone, B fails.</summary>
        private void FailLateJigwiBRelease(LabJigwiABRound active, double now)
        {
            if (active.resolution != LabJigwiABResolution.None || active.countdownAt < 0d) return;
            double due = active.GoAt + LabJigwiABTimeline.ReleaseSeconds;
            if (now < due || active.players.All(value => value.launchedAt >= 0d)) return;
            FailJigwiSync(active, due);
        }
        /// <summary>
        /// #551 나무 물통: a seat pulled its handle (tilted the phone toward itself while holding). Counts only after "당겨!"
        /// and within <see cref="LabJigwiABTimeline.ReleaseSeconds"/>, once per seat, while still holding. When every seat
        /// has pulled the bucket flips: B succeeds and each seat paints one big Water splash on its own side of the boss.
        /// </summary>
        public bool TryJigwiBPull(ulong sender, string roundId, double now)
        {
            if (!jigwiABEnabled || !jigwiBBucket || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _)
                || phase != LabPhase.Playing) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || !active.bucket || !active.Active(now) || active.id != roundId || active.countdownAt < 0d) return false;
            if (now < active.GoAt || now > active.GoAt + LabJigwiABTimeline.ReleaseSeconds) return false;
            var seat = active.players.FirstOrDefault(value => value.playerId == sender);
            if (seat == null || seat.readyAt < 0d || seat.launchedAt >= 0d) return false;
            seat.launchedAt = now;
            revision++;
            if (active.players.All(value => value.launchedAt >= 0d)) SucceedJigwiBucket(active, now);
            return true;
        }
        private void SucceedJigwiBucket(LabJigwiABRound active, double now)
        {
            active.syncCount = LabJigwiABTimeline.GroupSyncRequired;
            active.syncWindowAt = -1d;
            foreach (var target in active.players.SelectMany(p => p.targets)) { target.lit = false; target.extinguishedAt = -1d; }
            ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
            // The water lands on everyone's side of the boss, a little above the middle.
            jigwiSyncDirections.Clear();
            foreach (var picked in active.players)
                if (players.TryGetValue(picked.playerId, out var player))
                    jigwiSyncDirections[picked.playerId] = JigwiStageSeatDirection(player.seat, 0f, 12f);
            SplashJigwiSuccess(active, now);
        }
        /// <summary>
        /// #479 지귀 부적: 이 회차 A에서 "물"을 다 쓴 사람의 불만 끄고(남은 구슬은 더 타지 않음) 그 판에 물 구슬을 준다.
        /// 한 사람은 한 회차에 한 번만. 모두 다 쓰면 A가 일찍 끝난다.
        /// </summary>
        public bool TryCompleteJigwiTalisman(ulong sender, string roundId, double now)
        {
            if (!jigwiABEnabled || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _) || phase != LabPhase.Playing) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || active.kind != LabJigwiABKind.PersonalBurn || !active.Active(now) || active.id != roundId) return false;
            var player = active.players.FirstOrDefault(value => value.playerId == sender);
            if (player == null || player.talismanAt >= 0d) return false;
            player.talismanAt = now;
            ResumeJigwiAOrbLifetimes(sender, active.endsAt - now);
            foreach (var orb in orbs.Values)
                if (orb.owner == sender && orb.jigwiABBurnEventId == active.id)
                { orb.jigwiABBurnEventId = null; orb.jigwiABBurnAt = -1d; orb.revision = ++revision; }
            GrantJigwiTalismanWater(sender, now);
            HoldJigwiOrbsForTalismanReward(sender, now);
            revision++;
            if (active.players.All(value => value.talismanAt >= 0d)) ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
            return true;
        }
        /// <summary>
        /// #540: while a seat writes its talisman, the orbs already on its board stop ageing (their lifetime is paused),
        /// so an unfinished talisman's penalty burns a full board. The pause is booked up front as the A's whole length
        /// and the unused part is handed back when the seat finishes early.
        /// </summary>
        private void PauseJigwiAOrbLifetimes(LabJigwiABRound active)
        {
            jigwiAPausedOrbs.Clear();
            double hold = Math.Max(0d, active.endsAt - active.activeAt);
            if (hold <= 0d) return;
            foreach (var orb in orbs.Values)
            {
                if (orb.inFlight || orb.jigwiTalismanReward || orb.createdAt > active.activeAt
                    || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) continue;
                if (!active.players.Any(seat => seat.playerId == orb.owner && seat.talismanAt < 0d)) continue;
                double ordinary = orb.expiresAt >= 0d ? orb.expiresAt : orb.createdAt + LifetimeOf(orb);
                if (ordinary <= active.activeAt) continue;
                orb.expiresAt = ordinary + hold;
                orb.revision = ++revision;
                jigwiAPausedOrbs[orb.id] = orb.owner;
            }
        }
        private void ResumeJigwiAOrbLifetimes(ulong seat, double unused)
        {
            if (jigwiAPausedOrbs.Count == 0) return;
            foreach (var pair in jigwiAPausedOrbs.Where(value => value.Value == seat).ToArray())
            {
                jigwiAPausedOrbs.Remove(pair.Key);
                if (unused > 0d && orbs.TryGetValue(pair.Key, out var orb))
                { orb.expiresAt -= unused; orb.revision = ++revision; }
            }
        }
        /// <summary>
        /// #540: none of the seat's orbs runs out while the talisman turns into its orb and for a moment after it lands
        /// (JigwiTalismanCalmSeconds); anything due in that window goes when the window ends.
        /// </summary>
        private void HoldJigwiOrbsForTalismanReward(ulong owner, double now)
        {
            double until = now + jigwiTalismanRewardDelay + JigwiTalismanCalmSeconds;
            foreach (var orb in orbs.Values)
            {
                if (orb.owner != owner || orb.inFlight || orb.jigwiTalismanReward || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) continue;
                double ordinary = orb.expiresAt >= 0d ? orb.expiresAt : orb.createdAt + LifetimeOf(orb);
                if (ordinary >= until) continue;
                orb.expiresAt = until; orb.revision = ++revision;
            }
        }
        /// <summary>#540: a talisman orb leaves the board — its two shards take off with it.</summary>
        private void LaunchJigwiTalismanShards(LabOrbState orb)
        {
            if (orb == null || !orb.jigwiTalismanReward) return;
            for (int k = 1; k < JigwiTalismanVolleyCount; k++)
                jigwiTalismanShards[JigwiTalismanShardId(orb.id, k)] = (orb.owner, orb.jigwiABRewardPaintScale);
        }
        /// <summary>
        /// #540: a talisman shard reached the gourd. It paints Water like the talisman orb itself (same share), but
        /// deals no damage and gives no other reward. False when unknown, already resolved or blocked.
        /// </summary>
        public bool ConfirmJigwiTalismanShardHit(string id, double now, Vector3 paintDirection, float paintImpactStrength = .5f)
        {
            if (id == null || !jigwiTalismanShards.TryGetValue(id, out var shard)) return false;
            jigwiTalismanShards.Remove(id);
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !players.TryGetValue(shard.owner, out var attacker)) return false;
            if (JigwiABBlocksBoss(shard.owner, now)) return false;
            TryExtinguishJigwiStageRoot(shard.owner, paintDirection, now, out _);
            LastHitFiveColor = false; LastHitTalisman = true;
            bool painted = paintSurface.RegisterHit(paintDirection, PaintColorFor(LabOrbElement.Water), attacker.id, attacker.seat + 1,
                Mathf.Min(3f, shard.paint), paintImpactStrength);
            if (painted && paintSurface.IsComplete && phase == LabPhase.Playing)
            {
                phase = LabPhase.Victory;
                if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                AdvanceJigwiStage(now);
            }
            revision++;
            return true;
        }
        /// <summary>#540: a talisman shard missed (floor, fire, gone). Forget it.</summary>
        public bool DropJigwiTalismanShard(string id) => id != null && jigwiTalismanShards.Remove(id);
        private void GrantJigwiTalismanWater(ulong owner, double now)
        {
            // #509: one at a time, every 0.3 s, so each drop falls in with its own moment.
            for (int i = 0; i < jigwiTalismanWaterCount; i++)
                jigwiTalismanDrops.Add((owner, i, now + jigwiTalismanRewardDelay + i * JigwiTalismanDropSeconds));
            GrantDueJigwiTalismanWater(now);
        }
        /// <summary>
        /// #540: the moment Jigwi's "모두 재가 되어라!" ends, every orb on a failed board (element and combined orbs on the board,
        /// not in flight; a talisman orb never burns) catches fire and burns away together in JigwiTalismanFailBlazeSeconds.
        /// </summary>
        private void IgniteDueJigwiFailBurns(double now)
        {
            for (int k = 0; k < jigwiFailBurns.Count; k++)
            {
                var (owner, roundId, dueAt) = jigwiFailBurns[k];
                if (dueAt > now) continue;
                jigwiFailBurns.RemoveAt(k--);
                double burnAt = dueAt + JigwiTalismanFailBlazeSeconds;
                foreach (var orb in orbs.Values)
                {
                    if (orb.owner != owner || orb.inFlight || orb.jigwiTalismanReward || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) continue;
                    // B's special Water is the next mission's ammunition (it can arrive while Jigwi is still speaking): never burn it.
                    if (!string.IsNullOrEmpty(orb.jigwiABSupplyEventId) || orb.jigwiSupply) continue;
                    if (orb.jigwiABBurnAt < 0d || burnAt < orb.jigwiABBurnAt)
                    { orb.jigwiABBurnAt = burnAt; orb.jigwiABBurnEventId = roundId; orb.revision = ++revision; }
                }
                revision++;
            }
        }
        private void GrantDueJigwiTalismanWater(double now)
        {
            for (int k = 0; k < jigwiTalismanDrops.Count; k++)
            {
                var (owner, i, dueAt) = jigwiTalismanDrops[k];
                if (dueAt > now) continue;
                jigwiTalismanDrops.RemoveAt(k--);
                if (!players.ContainsKey(owner)) continue;
                var water = new LabOrbState
                {
                    id = NewOrbId(), owner = owner, kind = LabOrbKind.Combined, element = LabOrbElement.Water,
                    origin = LabOrbOrigin.Gourd, originOwner = owner,
                    x = JigwiTalismanWaterSpot(i).x, y = JigwiTalismanWaterSpot(i).y,
                    createdAt = dueAt, expiresAt = dueAt + JigwiTalismanOrbLifetimeSeconds,
                    jigwiTalismanReward = true, jigwiABRewardPaintScale = jigwiTalismanWaterPaint, revision = ++revision
                };
                orbs.Add(water.id, water);
            }
        }
        private LabJigwiABSnapshot CopyJigwiABSnapshot(double now) => !jigwiABEnabled ? new LabJigwiABSnapshot()
            : new LabJigwiABSnapshot { enabled = true, fever = phase == LabPhase.Playing && now >= jigwiStageCutoffAt,
                startedAt = jigwiStageStartedAt, cutoffAt = jigwiStageCutoffAt, schedule = jigwiSchedule,
                rounds = jigwiABRounds.Select(item => item.Copy()).ToArray() };
    }
}
