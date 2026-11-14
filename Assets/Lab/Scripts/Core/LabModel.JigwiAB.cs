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
        /// <summary>#696: smoke round id + seat that already cleared its smoke (one handful of shards each).</summary>
        private readonly HashSet<string> jigwiSmokeCleared = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>#696: soot shards one cleared smoke gives; merged two by two they make one black orb.</summary>
        public const int JigwiSootShardCount = 4;
        /// <summary>#696: how soon after the smoke covers the screen its shards can be asked for (rubbing starts right away).</summary>
        public const double JigwiSmokeClearMinimumSeconds = 0d;
        /// <summary>#696 검정 구슬이 칠하는 먹색.</summary>
        public static readonly Color SootInk = new Color(.13f, .12f, .14f, 1f);
        private bool jigwiABEnabled;
        private const double JigwiABMissionSeconds = LabJigwiABTimeline.GroupMissionSeconds;
        private const uint JigwiGroupPickSalt = 0x509B1C4Bu;
        // #509: where each picked seat's last counted Water hit the boss, for the success splash.
        private readonly Dictionary<ulong, Vector3> jigwiSyncDirections = new Dictionary<ulong, Vector3>();
        private float jigwiSplashShare, jigwiBoardFireSeconds;
        /// <summary>#540: the timetable this battle runs and, for the flow timetable, the common gimmick rhythm.</summary>
        private int jigwiSchedule;
        private double jigwiCommonStart = 40d, jigwiCommonInterval = 15d;
        /// <summary>#663: seconds after a smoke ends that the next talisman starts (0 = the fixed timetable).</summary>
        private double jigwiSmokeToTalisman;
        /// <summary>#663: the next slot's activation when a smoke ended early pulled it forward (+∞ = its fixed time).</summary>
        private double jigwiNextSlotPulledAt = double.PositiveInfinity;
        /// <summary>The next slot's activation: its fixed time, or earlier when a finished smoke pulled the talisman forward.</summary>
        private double JigwiNextSlotActiveAt => LabJigwiABTimeline.IsRelay(jigwiSchedule) && jigwiStageNextSlot > 0
            ? jigwiNextSlotPulledAt // #713: after the first smoke every mission waits for the one before it
            : Math.Min(jigwiNextSlotPulledAt, jigwiStageStartedAt + LabJigwiABTimeline.ActiveOffset(jigwiStageNextSlot, jigwiSchedule));
        /// <summary>#713 이어달리기: 연기가 끝나고 / 부적이 끝나고 다음 미션이 시작하기까지(초, 예고 포함).</summary>
        private double jigwiRelayGap = 14d, jigwiRelayGapAfterTalisman = 12d;
        /// <summary>#713: smoke round id → seats that rubbed their smoke all away (all of them = the smoke is over).</summary>
        private readonly Dictionary<string, HashSet<ulong>> jigwiSmokeWiped = new Dictionary<string, HashSet<ulong>>(StringComparer.Ordinal);
        /// <summary>#540: once Jigwi's line is over, every orb on an unfinished talisman's board burns away together in this long.</summary>
        public const double JigwiTalismanFailBlazeSeconds = 1d;
        /// <summary>#540: the fireball falls onto a failed seat's board this long after the A ends.</summary>
        public const double JigwiTalismanFailBurnDelaySeconds = .45d;
        /// <summary>
        /// #540: the first orb catches fire this long after the A ends. #663: it waited 3.7 s for Jigwi's "모두 재가 되어라!"
        /// (`JigwiVoiceAFail`), which no longer exists, so the board sat still; now it catches fire just after the fireball lands
        /// (0.45 s). The default of LabConfig Jigwi A Fail Burn Start Seconds (Host, Inspector).
        /// </summary>
        public const double JigwiTalismanFailFirstBurnSeconds = .6d;
        private double jigwiAFailBurnStart = JigwiTalismanFailFirstBurnSeconds;
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
            jigwiSmokeFlow = config.JigwiSmokeFlow; // #595: with B Test Only it tests the smoke B (every 17 s) instead of the old B
            jigwiSchedule = config.JigwiBTestOnly ? LabJigwiABTimeline.BTestSchedule // #551 시험용: B만
                : config.JigwiATestOnly ? LabJigwiABTimeline.ATestSchedule // #595 시험용: 부적만
                // #696 연기 6 / 부적 30 / 연기 44 / 부적 68 / 연기 82, 끄면 #595 연기 10 / 부적 45 / 연기 66 / 부적 83
                : jigwiSmokeFlow ? (config.JigwiRelayTimeline ? LabJigwiABTimeline.RelaySmokeFlowSchedule // #713 이어달리기
                    : config.JigwiDenseTimeline ? LabJigwiABTimeline.DenseSmokeFlowSchedule : LabJigwiABTimeline.SmokeFlowSchedule)
                : config.JigwiFlowSchedule ? LabJigwiABTimeline.FlowSchedule : LabJigwiABTimeline.LegacySchedule;
            jigwiAHoldBurn = config.JigwiAHoldBurnUntilEnd;
            jigwiAFailBurnStart = config.JigwiAFailBurnStartSeconds; // #663
            jigwiBBucket = config.JigwiBBucket;
            jigwiBCountdown = config.JigwiBCountdown;
            jigwiCommonStart = config.JigwiCommonStartSeconds; jigwiCommonInterval = config.JigwiCommonIntervalSeconds;
            jigwiSmokeToTalisman = config.JigwiSmokeToTalismanSeconds; jigwiNextSlotPulledAt = double.PositiveInfinity;
            jigwiRelayGap = config.JigwiRelayGapSeconds; jigwiRelayGapAfterTalisman = config.JigwiRelayGapAfterTalismanSeconds;
            jigwiSmokeWiped.Clear();
            jigwiABRounds.Clear(); jigwiABActivated.Clear(); jigwiSmokeCleared.Clear(); jigwiABFailureRevision = 0;
            jigwiABLastBurnAt.Clear(); jigwiABBurnQueueId = null; jigwiTalismanDrops.Clear(); jigwiFailBurns.Clear(); jigwiAPausedOrbs.Clear(); jigwiTalismanShards.Clear(); jigwiCommonPours.Clear();
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
            if (jigwiStageCutoffReached || jigwiSchedule == LabJigwiABTimeline.BTestSchedule
                || jigwiSchedule == LabJigwiABTimeline.ATestSchedule) return double.PositiveInfinity; // #551 B만 / #595 부적만 시험
            if (jigwiSchedule != LabJigwiABTimeline.FlowSchedule && !LabJigwiABTimeline.IsSmokeFlow(jigwiSchedule))
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
                Include(JigwiNextSlotActiveAt - LabJigwiABTimeline.WarningSeconds);
            foreach (var item in jigwiABRounds)
            {
                if (item.resolution == LabJigwiABResolution.None)
                {
                    Include(item.activeAt); Include(item.endsAt);
                    if (item.kind == LabJigwiABKind.GroupFire && item.syncWindowAt >= 0d)
                        Include(item.syncWindowAt + item.SyncWindow);
                    // #551: "지금!" and the moment everyone must have let go.
                    if (item.kind == LabJigwiABKind.GroupFire && item.countdownAt >= 0d)
                    { Include(item.countdownAt); Include(item.GoAt); Include(item.GoAt + item.PullSeconds); }
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
                jigwiTalismanDrops.Clear(); jigwiFailBurns.Clear(); jigwiCommonPours.Clear();
                CancelJigwiABForBattleEnd(now);
                return;
            }
            GrantDueJigwiTalismanWater(now);
            IgniteDueJigwiFailBurns(now);
            PourDueJigwiCommonTalismans(now);
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
                && JigwiNextSlotActiveAt - LabJigwiABTimeline.WarningSeconds <= now)
            {
                double activeAt = JigwiNextSlotActiveAt;
                int slot = jigwiStageNextSlot++;
                jigwiNextSlotPulledAt = double.PositiveInfinity;
                BeginJigwiABSlot(slot, activeAt);
            }
            var active = ActiveJigwiABRound(now);
            if (active != null) AdvanceJigwiABActive(active, now);
        }
        /// <summary>
        /// #663: in the smoke flow, once a smoke is over (wiped away or timed out) the next talisman follows Jigwi Smoke To Talisman
        /// Seconds later instead of waiting for its fixed time (45 s / 83 s), never later than that. A common gimmick that would
        /// fall from the talisman's warning to its end waits until 1 s after it, so the two never overlap.
        /// </summary>
        private void PullTalismanAfterSmoke(LabJigwiABRound item, LabJigwiABResolution result)
        {
            if (LabJigwiABTimeline.IsRelay(jigwiSchedule)) return; // #713: the relay schedules every mission after the last one
            if (!item.smoke || jigwiSmokeToTalisman <= 0d || result == LabJigwiABResolution.Cancelled
                || result == LabJigwiABResolution.Skipped || !LabJigwiABTimeline.IsSmokeFlow(jigwiSchedule)
                || jigwiStageNextSlot >= LabJigwiABTimeline.SlotCountFor(jigwiSchedule)
                || LabJigwiABTimeline.KindForSlot(jigwiStageNextSlot, jigwiSchedule) != LabJigwiABKind.PersonalBurn) return;
            double fixedAt = jigwiStageStartedAt + LabJigwiABTimeline.ActiveOffset(jigwiStageNextSlot, jigwiSchedule);
            double pulled = item.resolvedAt + Math.Max(LabJigwiABTimeline.WarningSeconds, jigwiSmokeToTalisman);
            if (pulled >= fixedAt || pulled >= jigwiStageCutoffAt) return;
            jigwiNextSlotPulledAt = pulled;
            double from = pulled - LabJigwiABTimeline.WarningSeconds, until = pulled + jigwiABDuration + 1d;
            if (nextCommonGimmickAt >= from && nextCommonGimmickAt < until) nextCommonGimmickAt = until;
        }
        /// <summary>
        /// #713 이어달리기: 이 미션이 화면에서 다 끝나는 때. 연기는 모두 닦았거나 15초가 된 때, 부적 성공은 물폭포가 박에 쏟아진 뒤,
        /// 부적 실패는 실패 대사 뒤 판의 구슬이 다 타 버린 뒤.
        /// </summary>
        public double JigwiMissionDoneAt(LabJigwiABRound item)
        {
            if (item == null || item.resolvedAt < 0d) return double.PositiveInfinity;
            if (item.kind != LabJigwiABKind.PersonalBurn) return item.resolvedAt;
            if (item.resolution == LabJigwiABResolution.Completed && item.common)
                return item.resolvedAt + LabJigwiABTimeline.CommonTalismanRevealSeconds + LabJigwiABTimeline.CommonTalismanPourSeconds;
            if (item.resolution == LabJigwiABResolution.Expired)
                return item.resolvedAt + jigwiAFailBurnStart + JigwiTalismanFailBlazeSeconds;
            return item.resolvedAt;
        }

        /// <summary>
        /// #713 이어달리기: 미션이 끝나면 그 연출까지 끝난 때부터 jigwiRelayGap초 뒤(예고 포함)에 다음 미션을 잡는다.
        /// 다음 미션을 피버 전에 끝낼 수 없으면 더 잡지 않는다 (공통 기믹은 따로 계속 나온다).
        /// </summary>
        private void ScheduleJigwiRelayAfter(LabJigwiABRound item, LabJigwiABResolution result)
        {
            if (!LabJigwiABTimeline.IsRelay(jigwiSchedule) || result == LabJigwiABResolution.Cancelled
                || jigwiStageNextSlot >= LabJigwiABTimeline.SlotCountFor(jigwiSchedule)) return;
            double gap = item.kind == LabJigwiABKind.PersonalBurn ? jigwiRelayGapAfterTalisman : jigwiRelayGap;
            double activeAt = JigwiMissionDoneAt(item) + Math.Max(LabJigwiABTimeline.WarningSeconds, gap);
            // #737: 벌칙(부적 실패의 북 미끄러움)이 끝나고 쉬는 시간이 지난 뒤에 다음 예고가 시작된다.
            activeAt = Math.Max(activeAt, PenaltyRestUntil(JigwiPenaltyEndAt(item)) + LabJigwiABTimeline.WarningSeconds);
            bool personal = LabJigwiABTimeline.KindForSlot(jigwiStageNextSlot, jigwiSchedule) == LabJigwiABKind.PersonalBurn;
            double length = personal ? jigwiABDuration + jigwiAFailBurnStart + JigwiTalismanFailBlazeSeconds : JigwiABMissionSeconds;
            if (!(activeAt + length <= jigwiStageCutoffAt)) { jigwiStageNextSlot = LabJigwiABTimeline.SlotCountFor(jigwiSchedule); return; }
            jigwiNextSlotPulledAt = activeAt;
        }

        /// <summary>
        /// #713: 이 자리가 자기 화면의 연기를 다 닦았다(LabJigwiBucketHandleView가 셈). 연기를 받은 모두가 닦으면 그 연기는 그때 끝난다
        /// (15초를 기다리지 않음). 연기마다 한 사람 한 번.
        /// </summary>
        public bool TryWipeJigwiSmoke(ulong sender, string roundId, double now)
        {
            if (!jigwiABEnabled || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _) || phase != LabPhase.Playing
                || !players.ContainsKey(sender)) return false;
            var round = jigwiABRounds.FirstOrDefault(value => value.id == roundId);
            if (round == null || !round.smoke || round.resolution != LabJigwiABResolution.None || now >= round.endsAt) return false;
            var seat = round.players?.FirstOrDefault(value => value.playerId == sender);
            if (seat == null || now < seat.readyAt) return false;
            if (!jigwiSmokeWiped.TryGetValue(roundId, out var wiped)) jigwiSmokeWiped[roundId] = wiped = new HashSet<ulong>();
            if (!wiped.Add(sender)) return false;
            // Resolved as over-in-time (not a B success): no splash, no board fire; it only frees the timeline.
            if (round.players.All(value => value != null && wiped.Contains(value.playerId)))
                ResolveJigwiAB(round, LabJigwiABResolution.Expired, now);
            revision++;
            return true;
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
            bool smoke = !personal && jigwiSmokeFlow; // #595: B is smoke for everyone at once (no bucket to pour)
            var item = new LabJigwiABRound
            {
                id = "jigwi-ab/" + sessionId + "/" + round + "/" + slot, slot = slot,
                kind = kind,
                warningAt = activeAt - LabJigwiABTimeline.WarningSeconds, activeAt = activeAt,
                endsAt = Math.Min(jigwiStageCutoffAt, personal
                    ? Math.Min(activeAt + jigwiABDuration, LabJigwiABTimeline.IsRelay(jigwiSchedule) ? double.PositiveInfinity
                        : jigwiStageStartedAt + LabJigwiABTimeline.NextOffset(slot, jigwiSchedule))
                    : activeAt + JigwiABMissionSeconds),
                // Reserved shrink fields remain on the wire, but the timed mission never shrinks an orb.
                shrinkAt = activeAt, shrinkScale = 1f,
                moveAmount = jigwiABMoveAmount, movePeriodSeconds = jigwiABMovePeriod, targetRadius = jigwiABTargetRadius,
                // #509: B success no longer gives a timed launch reward.
                rewardSizeScale = 1f, rewardPaintScale = 1f,
                // #551: with the countdown the picked hits may land a little further apart.
                syncWindowSeconds = !personal && jigwiBCountdown && !jigwiBBucket && !smoke ? LabJigwiABTimeline.CountdownSyncWindowSeconds : 0f,
                bucket = !personal && (jigwiBBucket || smoke), smoke = smoke,
                common = personal && jigwiSmokeFlow // #595: every talisman written = reward for all, one missing = all boards burn
            };
            // #479: A ignites every seat at once; each seat clears its own fire with a talisman.
            if (personal) item.players = seats.Take(activeSeatCount).Select(owner => new LabJigwiABPlayerFire
            {
                playerId = owner, targets = Array.Empty<LabJigwiABFireTarget>()
            }).ToArray();
            // #509: B picks a few seats; each picked screen gets one fire pillar in front of the boss.
            // The others are helpers. The draw is stable per seed/slot and differs between B rounds.
            if (!personal) jigwiSyncDirections.Clear();
            if (!personal) item.players = JigwiGroupPicks(slot, item.bucket).Select((owner, order) => new LabJigwiABPlayerFire
            {
                playerId = owner, targets = new[] { new LabJigwiABFireTarget { id = item.id + "/" + owner + "/0", x = .5f, y = .62f } },
                // #551 나무 물통: buckets arrive one seat after another (the draw order), like fox fire.
                // #595 연기: every screen smokes at the same moment.
                readyAt = item.smoke ? activeAt + LabJigwiABTimeline.SmokeLeadSeconds
                    : item.bucket ? activeAt + LabJigwiABTimeline.BucketLeadSeconds + order * LabJigwiABTimeline.BucketStaggerSeconds : -1d
            }).ToArray();
            jigwiABRounds.Add(item); revision++;
        }
        private ulong[] JigwiGroupPicks(int slot, bool bucket = false)
        {
            ulong[] all = seats.Take(activeSeatCount).ToArray();
            // #551 나무 물통: everyone has a bucket.
            int count = bucket ? all.Length : LabJigwiABTimeline.GroupPickCount(all.Length);
            return all.OrderBy(owner => DrawFor(seed ^ JigwiGroupPickSalt, owner, (uint)slot)).ThenBy(owner => owner)
                .Take(count).ToArray();
        }
        private void ResolveJigwiAB(LabJigwiABRound item, LabJigwiABResolution result, double at)
        {
            if (item == null || item.resolution != LabJigwiABResolution.None) return;
            item.resolution = result; item.resolvedAt = Math.Max(item.warningAt, at);
            PullTalismanAfterSmoke(item, result);
            ScheduleJigwiRelayAfter(item, result);
            // #540: an A that runs out punishes every seat that did not finish its talisman (each seat for itself):
            // when Jigwi's line is over, every orb then on its board burns away at once. Drums keep working.
            var failed = new HashSet<ulong>();
            // #595: a common talisman that someone did not finish burns every board, finished or not.
            if (item.kind == LabJigwiABKind.PersonalBurn && result == LabJigwiABResolution.Expired)
                foreach (var seat in item.players ?? Array.Empty<LabJigwiABPlayerFire>())
                    if (seat != null && item.TalismanFailed(seat.playerId)) failed.Add(seat.playerId);
            // #540: which orbs burn is decided when Jigwi's line ends (see IgniteDueJigwiFailBurns), so orbs made meanwhile burn too.
            foreach (ulong owner in failed) jigwiFailBurns.Add((owner, item.id, item.resolvedAt + jigwiAFailBurnStart));
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
                // #551: the bucket B is personal — a seat that did not pour only smokes its own screen; nobody else's board burns.
                if (result == LabJigwiABResolution.Expired && jigwiBoardFireSeconds > 0f && !item.bucket)
                    item.boardFireUntil = Math.Max(item.resolvedAt, Math.Min(jigwiStageCutoffAt, item.resolvedAt + jigwiBoardFireSeconds));
            }
            revision++;
        }
        private void ApplyJigwiABOrb(LabOrbState orb, double now)
        {
            if (!jigwiABEnabled || orb.inFlight || orb.jigwiTalismanReward || orb.soot || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) return;
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
        private bool jigwiBCountdown, jigwiBBucket, jigwiSmokeFlow;
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
            if (seat == null || active.bucket || active.countdownAt >= 0d && now >= active.GoAt) return false;
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
        /// <summary>#551: someone had not thrown <see cref="LabJigwiABRound.PullSeconds"/> after "지금!" — the one chance is gone, B fails.</summary>
        private void FailLateJigwiBRelease(LabJigwiABRound active, double now)
        {
            if (active.resolution != LabJigwiABResolution.None || active.countdownAt < 0d) return;
            double due = active.GoAt + active.PullSeconds;
            if (now < due || active.players.All(value => value.launchedAt >= 0d)) return;
            FailJigwiSync(active, due);
        }
        /// <summary>
        /// #551 나무 물통 (개별): this seat rocked its phone left-right-left (3 sloshes) and poured its own bucket. Once per seat,
        /// only between its bucket's arrival (readyAt) and BucketSeatSeconds later; its water lands at once as one big Water splash
        /// on its side of the boss. Everyone poured = B completed.
        /// The phone-side counting lives in LabJigwiBucketHandleView; the Host only takes the pour.
        /// </summary>
        public bool TryJigwiBPull(ulong sender, string roundId, double now)
        {
            if (!jigwiABEnabled || !jigwiBBucket || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _)
                || phase != LabPhase.Playing) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null || !active.bucket || active.smoke || !active.Active(now) || active.id != roundId) return false; // #595 연기엔 쏟을 양동이가 없다
            var seat = active.players.FirstOrDefault(value => value.playerId == sender);
            if (seat == null || seat.launchedAt >= 0d || now < seat.readyAt
                || now > seat.readyAt + LabJigwiABTimeline.BucketSeatSeconds) return false;
            seat.launchedAt = now;
            revision++;
            PaintJigwiBucketPour(sender, now);
            if (phase == LabPhase.Playing && active.resolution == LabJigwiABResolution.None
                && active.players.All(value => value.launchedAt >= 0d))
            {
                active.syncCount = LabJigwiABTimeline.GroupSyncRequired;
                active.syncWindowAt = -1d;
                foreach (var target in active.players.SelectMany(p => p.targets)) { target.lit = false; target.extinguishedAt = -1d; }
                ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
            }
            return true;
        }
        /// <summary>
        /// #696 연기 보상: 이 자리가 연기를 다 걷어 냈다(문지르기는 화면 LabJigwiBucketHandleView가 센다). 연기마다 한 번,
        /// 연기가 덮이고 JigwiSmokeClearMinimumSeconds가 지난 뒤부터 그 연기가 끝난 1초 뒤까지 받는다.
        /// 그 사람 조합판에 검은 파편 JigwiSootShardCount개를 준다(판이 가득해도 준다).
        /// </summary>
        public bool TryClearJigwiSmoke(ulong sender, string roundId, double now)
        {
            if (!jigwiABEnabled || string.IsNullOrEmpty(roundId) || !TryAdvance(now, out _) || phase != LabPhase.Playing
                || !players.ContainsKey(sender)) return false;
            var round = jigwiABRounds.FirstOrDefault(value => value.id == roundId);
            if (round == null || !round.smoke || now > round.endsAt + 1d) return false;
            var seat = round.players?.FirstOrDefault(value => value.playerId == sender);
            if (seat == null || now < seat.readyAt + JigwiSmokeClearMinimumSeconds) return false;
            if (!jigwiSmokeCleared.Add(roundId + "/" + sender)) return false;
            for (int i = 0; i < JigwiSootShardCount; i++)
            {
                Vector2 spot = JigwiSootShardSpot(i);
                var shard = new LabOrbState
                {
                    id = NewOrbId(), owner = sender, kind = LabOrbKind.SootShard, element = LabOrbElement.None, sootPieces = 1 << i,
                    origin = LabOrbOrigin.Gourd, originOwner = sender, x = spot.x, y = spot.y,
                    createdAt = now, expiresAt = now + FiveColorOrbLifetimeSeconds, revision = ++revision
                };
                orbs.Add(shard.id, shard);
            }
            revision++;
            return true;
        }
        /// <summary>
        /// #696: 깨진 검정 구슬이 사방으로 튄 것처럼, 북 조각은 위 / 서 조각은 왼쪽 / 남 조각은 아래 / 동 조각은 오른쪽 끝 가까이 멀리 떨어져 놓인다.
        /// i는 조각 쪽 번호 (0 북, 1 서, 2 남, 3 동).
        /// </summary>
        public static Vector2 JigwiSootShardSpot(int i)
        {
            float angle = (90f + 90f * (i & 3)) * Mathf.Deg2Rad;
            return new Vector2(.5f + JigwiSootShardSpreadX * Mathf.Cos(angle), .52f + JigwiSootShardSpreadY * Mathf.Sin(angle));
        }

        /// <summary>#696: 파편이 판 가운데에서 얼마나 멀리 흩어지는지 (판 너비/높이 비율). 서로 끌어다 붙여야 할 만큼 멀리.</summary>
        public const float JigwiSootShardSpreadX = .39f, JigwiSootShardSpreadY = .34f; // #724 QA: farther apart (was .32 / .27)

        /// <summary>#696: 검정 구슬은 보통 결합 구슬보다 이만큼 크다 (판 위 / 날아갈 때).</summary>
        public const float SootOrbSizeScale = 1.3f;

        /// <summary>#696: 네 쪽이 다 모인 검정 구슬.</summary>
        public const int SootWholePieces = 15;

        public static int SootPieceCount(int pieces)
        {
            int count = 0;
            for (int bit = 0; bit < 4; bit++) if ((pieces & (1 << bit)) != 0) count++;
            return count;
        }

        /// <summary>
        /// #696: 조각들이 이어진 한 덩어리(북→서→남→동 차례로 이웃)면 그 덩어리가 시작하는 쪽 번호, 아니면 -1.
        /// 예: 북+서 = 0, 동+북 = 3, 북+남(마주 보는 쪽) = -1.
        /// </summary>
        public static int SootArcStart(int pieces)
        {
            if (pieces <= 0 || pieces > SootWholePieces) return -1;
            if (pieces == SootWholePieces) return 0;
            int count = SootPieceCount(pieces);
            for (int start = 0; start < 4; start++)
            {
                int arc = 0;
                for (int k = 0; k < count; k++) arc |= 1 << ((start + k) & 3);
                if (arc == pieces) return start;
            }
            return -1;
        }

        /// <summary>
        /// #724 UT: 두 파편 덩어리는 합쳐 네 쪽을 넘지 않으면 어떤 쪽끼리든 붙는다 (#696의 "이웃한 쪽끼리만"은 UT에서
        /// "붙였는데 안 붙는다"로 읽혀 없앴다). 붙은 덩어리는 조각 수만큼 북부터 채운 모양이 된다 (<see cref="SootPiecesFor"/>).
        /// </summary>
        public static bool SootPiecesFit(int first, int second) =>
            first > 0 && second > 0 && first < SootWholePieces && second < SootWholePieces
            && SootPieceCount(first) + SootPieceCount(second) <= 4;

        /// <summary>#724: count quarters as one piece filled from the north on (1 → 북, 2 → 북+서, 3 → 북+서+남, 4 → 검정 구슬).</summary>
        public static int SootPiecesFor(int count) => (1 << Mathf.Clamp(count, 0, 4)) - 1;

        /// <summary>#724: 판 가운데 검정 구슬 빈자리 (정규 좌표). 다 차면 검정 구슬이 여기서 생긴다.</summary>
        public static readonly Vector2 SootSocketCenter = new Vector2(.5f, .52f);

        /// <summary>
        /// #724 UT: 탭한 연기 파편을 판 가운데 빈 구슬에 넣는다. 조각은 제 자리(북 조각은 위쪽)에 들어가고, 순서는 따지지 않는다.
        /// 네 쪽이 차면 그 자리에서 검정 구슬이 된다.
        /// 빈자리에 있는 조각은 판 위에 없고 아무 동작도 받지 않는다 (UsableBy).
        /// </summary>
        private bool SocketSootShard(ulong sender, LabOrbState shard, double now, out LabOrbState orb, out string reason)
        {
            LabOrbState socket = orbs.Values.FirstOrDefault(item => item.owner == sender && item.sootSocketed && item.kind.IsSootShard());
            // #724 QA: each piece goes into its own place (a south shard fills the south). A piece already there (a second set
            // of shards) takes the first free place instead.
            int filled = socket != null ? socket.sootPieces : 0;
            for (int q = 0; q < 4; q++)
            {
                if ((shard.sootPieces & (1 << q)) == 0) continue;
                int bit = 1 << q;
                if ((filled & bit) != 0)
                    for (int free = 0; free < 4; free++) if ((filled & (1 << free)) == 0) { bit = 1 << free; break; }
                filled |= bit;
            }
            orbs.Remove(shard.id);
            if (socket != null) orbs.Remove(socket.id);
            bool whole = (filled & SootWholePieces) == SootWholePieces;
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, element = LabOrbElement.None,
                kind = whole ? LabOrbKind.Combined : LabOrbKind.SootShard, sootPieces = whole ? 0 : filled,
                sootSocketed = !whole, soot = whole, fiveColor = whole,
                origin = LabOrbOrigin.Combined, originOwner = sender, x = SootSocketCenter.x, y = SootSocketCenter.y,
                createdAt = now, expiresAt = now + FiveColorOrbLifetimeSeconds, revision = ++revision
            };
            orbs.Add(orb.id, orb);
            reason = whole ? "SOOT_ORB" : "SOOT_SOCKETED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// #696: 이웃한 연기 파편 둘을 합친다. 쪽(sootPieces)을 모으고, 네 쪽이 다 모이면 검정 구슬(soot, 오색구슬처럼 넓게 먹색으로 칠함)이 된다.
        /// </summary>
        private bool MergeSootShards(ulong sender, LabOrbState first, LabOrbState second, Vector2 center, Vector2 velocity,
            double now, out LabOrbState orb, out string reason)
        {
            orbs.Remove(first.id); orbs.Remove(second.id);
            int pieces = SootPiecesFor(SootPieceCount(first.sootPieces) + SootPieceCount(second.sootPieces));
            bool whole = pieces == SootWholePieces;
            Vector2 motion = Vector2.ClampMagnitude(velocity, rules.MaximumMotionSpeed);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, element = LabOrbElement.None,
                kind = whole ? LabOrbKind.Combined : LabOrbKind.SootShard, sootPieces = whole ? 0 : pieces,
                soot = whole, fiveColor = whole,
                origin = LabOrbOrigin.Combined, originOwner = sender, x = center.x, y = center.y, vx = motion.x, vy = motion.y,
                createdAt = now, expiresAt = now + FiveColorOrbLifetimeSeconds, revision = ++revision
            };
            orbs.Add(orb.id, orb);
            reason = whole ? "SOOT_ORB" : "SOOT_MERGED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>#551: one poured bucket = one big Water splash on the top of the boss, leaning to that seat's side (can win the battle).</summary>
        private void PaintJigwiBucketPour(ulong owner, double now)
        {
            if (teamMode || jigwiSplashShare <= 0f || !players.TryGetValue(owner, out var player)) return;
            bool painted = paintSurface.RegisterHit(JigwiStageSeatDirection(player.seat, 0f, LabJigwiABTimeline.BucketPourPitch), PaintColorFor(LabOrbElement.Water),
                player.id, player.seat + 1, Mathf.Min(3f, jigwiSplashShare));
            if (painted && paintSurface.IsComplete && phase == LabPhase.Playing)
            {
                phase = LabPhase.Victory;
                if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                AdvanceJigwiStage(now);
            }
            revision++;
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
            if (active.common) return CompleteCommonJigwiTalisman(active, sender, now);
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
        /// #595 공용 부적: 쓴 사람의 판은 아직 타지 않게만 하고 보상은 미룬다. 마지막 사람이 쓰는 순간 모두의 멈춘 구슬 수명이
        /// 돌아가고, 박 위로 폭포가 쏟아져(자리마다 양동이 한 번만큼 물 칠) A가 끝난다. 한 명이라도 못 쓰면 끝날 때 모두의 판이 탄다(ResolveJigwiAB).
        /// </summary>
        private bool CompleteCommonJigwiTalisman(LabJigwiABRound active, ulong sender, double now)
        {
            foreach (var orb in orbs.Values)
                if (orb.owner == sender && orb.jigwiABBurnEventId == active.id)
                { orb.jigwiABBurnEventId = null; orb.jigwiABBurnAt = -1d; orb.revision = ++revision; }
            revision++;
            if (!active.players.All(value => value.talismanAt >= 0d)) return true;
            // #595: the reward is a waterfall on the boss (like each seat's poured bucket), not talisman orbs.
            // #610: it paints when the waterfall actually lands — after the big talisman's melody ends (Reveal) and the
            // small talismans glow and fly to the boss (Pour) — not the moment the last talisman is written.
            double pourAt = now + LabJigwiABTimeline.CommonTalismanRevealSeconds + LabJigwiABTimeline.CommonTalismanPourSeconds;
            foreach (var seat in active.players)
            {
                ResumeJigwiAOrbLifetimes(seat.playerId, active.endsAt - now);
                jigwiCommonPours.Add((seat.playerId, pourAt));
            }
            ResolveJigwiAB(active, LabJigwiABResolution.Completed, now);
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
            LastHitFiveColor = false; LastHitTalisman = true; LastHitSoot = false;
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
        // #610: the common talisman's waterfall paint, one per seat, due when the waterfall lands on the boss.
        private readonly List<(ulong owner, double dueAt)> jigwiCommonPours = new List<(ulong owner, double dueAt)>();
        private void PourDueJigwiCommonTalismans(double now)
        {
            for (int k = 0; k < jigwiCommonPours.Count; k++)
            {
                var (owner, dueAt) = jigwiCommonPours[k];
                if (dueAt > now) continue;
                jigwiCommonPours.RemoveAt(k--);
                if (phase == LabPhase.Playing) PaintJigwiBucketPour(owner, dueAt);
            }
        }
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
                    if (orb.owner != owner || orb.inFlight || orb.jigwiTalismanReward || orb.soot || (!orb.kind.IsRaw() && orb.kind != LabOrbKind.Combined)) continue;
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
