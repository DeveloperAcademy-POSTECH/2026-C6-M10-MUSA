using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>야광귀 모드에서 한 번에 하나만 열리는 이벤트 종류 (#271).</summary>
    internal enum LabYaguangEvent { Combo, Flash, GourdBomb, SwipeBlock, Sabotage }

    /// <summary>
    /// A locked bomb that reached its fuse: the board it blasts and where, in 0..1 board units,
    /// its holder last reported it.
    /// </summary>
    public readonly struct LabBombBlast
    {
        public readonly ulong Owner;
        public readonly string BombId;
        public readonly Vector2 Position;

        public LabBombBlast(ulong owner, string bombId, Vector2 position)
        {
            Owner = owner;
            BombId = bombId;
            Position = position;
        }
    }

    /// <summary>
    /// An orb whose lifetime ran out (#235): the board that held it, which goes there in a puff of
    /// smoke, unlike an orb that was handed on, combined, thrown or used.
    /// </summary>
    public readonly struct LabOrbExpiry
    {
        public readonly ulong Owner;
        public readonly string OrbId;

        public LabOrbExpiry(ulong owner, string orbId)
        {
            Owner = owner;
            OrbId = orbId;
        }
    }

    /// <summary>
    /// A locked bomb the gourd dropped onto one player's board as sabotage (#177): who receives it
    /// and where, in 0..1 board units, it lands.
    /// </summary>
    public readonly struct LabGourdBomb
    {
        public readonly ulong Target;
        public readonly string BombId;
        public readonly Vector2 Position;

        public LabGourdBomb(ulong target, string bombId, Vector2 position)
        {
            Target = target;
            BombId = bombId;
            Position = position;
        }
    }

    /// <summary>A sabotage the sabotage mix cast on one player (#251), and the Host time it began.</summary>
    public readonly struct LabSabotage
    {
        public readonly LabSabotageKind Kind;
        public readonly ulong Target;
        public readonly double At;

        public LabSabotage(LabSabotageKind kind, ulong target, double at)
        {
            Kind = kind;
            Target = target;
            At = at;
        }
    }

    /// <summary>Fox orbs the fox fire dropped onto one player's board (#251).</summary>
    public readonly struct LabFoxFireDrop
    {
        public readonly ulong Target;
        public readonly string[] OrbIds;

        public LabFoxFireDrop(ulong target, string[] orbIds)
        {
            Target = target;
            OrbIds = orbIds;
        }
    }

    /// <summary>
    /// Plain Host-only battle state. Transport authenticates sender IDs before calling this class;
    /// clients render copies from Snapshot and never decide ownership, damage, or expiry.
    /// </summary>
    public sealed partial class LabModel
    {
        public const int MinimumParticipants = 2;
        public const int MaximumParticipants = 5;
        public const int DefaultParticipants = 3;
        private const int MaximumRequestsPerRound = 4096;
        private const float EdgeTolerance = .06f;
        private const float SecretBoxSpread = .05f;
        private const int RelayMaxStage = 3;
        private const uint LockDrawSalt = 0x10C4ED00u;
        private const uint SpawnAngleSalt = 0x5A4E0A11u;
        private const uint SpawnSpeedSalt = 0x5A4E05EDu;
        private const uint DrumSeatSalt = 0xD2D2D2D2u;
        private const uint GourdLandingSalt = 0x60BD1A4Du;
        private const uint SwipeBlockSideSalt = 0x5B105D1Eu;
        // 박의 넘기기 막기 뽑기도 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (#215).
        private const ulong SwipeBlockDrawKey = ulong.MaxValue - 3UL;
        private const uint DrumZoneSalt = 0xD2017E5Au;
        private const uint DrumSpotSalt = 0xD2051075u;
        private const uint ComboColorSalt = 0xC0B0C010u;
        // 박 색상 콤보 뽑기는 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다.
        private const ulong ComboDrawKey = ulong.MaxValue - 3UL;
        // 북 전달 뽑기는 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다.
        private const ulong DrumHandoffKey = ulong.MaxValue - 2UL;
        // 박 폭탄 뽑기는 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (북 자리 뽑기는 ulong.MaxValue).
        private const ulong GourdDrawKey = ulong.MaxValue - 1UL;
        private const uint SabotageKindSalt = 0x5AB0C1D5u;
        private const uint SabotageTargetSalt = 0x5AB07A26u;
        // 기믹 묶음 (#311): 공용 묶음은 위 두 소금을, 고유 묶음은 아래 두 소금을 써 서로 다른 뽑기가 나온다.
        private const uint UniqueGimmickKindSalt = 0x5AB0C1E6u;
        private const uint UniqueGimmickTargetSalt = 0x5AB07A37u;
        // 통합 방해공작 추첨도 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (#251).
        private const ulong SabotageDrawKey = ulong.MaxValue - 4UL;
        private const uint FlashTargetSalt = 0x7A6F1A5Bu;
        // 야광귀 섬광 대상 뽑기도 실제 플레이어 ID와 겹치지 않는 고정 키를 쓴다 (#268).
        private const ulong FlashDrawKey = ulong.MaxValue - 5UL;
        private const uint FoxCountSalt = 0xF0C5C017u;
        private const uint FoxLandingXSalt = 0xF0C51A0Du;
        private const uint FoxLandingYSalt = 0xF0C51A1Du;
        // 여우구슬이 떨어지는 곳: 판 너비의 15~85%, 높이의 25~80% 사이에서 하나씩 뽑는다.
        private const float FoxLandingMinX = .15f, FoxLandingMaxX = .85f, FoxLandingMinY = .25f, FoxLandingMaxY = .8f;
        // 박 폭탄이 떨어지는 곳: 판 너비의 20~80% 사이, 판 높이의 이 지점 (보드가 영역 안으로 맞춘다).
        private const float GourdLandingMinX = .2f, GourdLandingMaxX = .8f, GourdLandingY = .6f;
        // DEV에서 간격을 아주 작게 넣어도 한 프레임에 폭탄이 쏟아지지 않도록 하는 최소 간격.
        private const double GourdMinimumDelaySeconds = 1d;
        // 갓 생성된 구슬은 옆 가장자리(이웃에게 넘어가는 선)에서 이만큼 안쪽까지만 미끄러진다.
        private const float SpawnEdgeMargin = .08f;
        // 한 판에 북이 여러 개 놓일 때 가운데부터 좌우로 벌려 놓는다.
        private static readonly float[] DrumStartX = { .5f, .3f, .7f, .15f, .85f };
        private readonly Dictionary<ulong, LabPlayerState> players = new Dictionary<ulong, LabPlayerState>();
        private readonly Dictionary<string, LabOrbState> orbs = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly Dictionary<string, LabOrbState> drums = new Dictionary<string, LabOrbState>(StringComparer.Ordinal);
        private readonly HashSet<string> requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly ulong[] seats = new ulong[MaximumParticipants];
        private int activeSeatCount;
        private readonly Dictionary<ulong, uint> generatedCounts = new Dictionary<ulong, uint>();
        private readonly List<LabBombBlast> bombExplosions = new List<LabBombBlast>();
        private readonly List<LabOrbExpiry> orbExpiries = new List<LabOrbExpiry>();
        private uint swipeBlockCount;
        private ulong swipeBlockedPlayer;
        private bool swipeBlockedRight;
        private double swipeBlockStartedAt = -1d, swipeBlockedUntil = -1d;
        /// <summary>Blasts sent to their holders and not yet reported back, by bomb ID, with their Host time (#213).</summary>
        private readonly Dictionary<string, KeyValuePair<ulong, double>> unreportedBlasts =
            new Dictionary<string, KeyValuePair<ulong, double>>();
        /// <summary>How long after a blast the Host still takes its holder's report of the orbs it flung.</summary>
        public const double BlastReportWindowSeconds = 3d;
        /// <summary>More orb IDs than any board holds; a longer report is dropped.</summary>
        public const int MaximumBlastReportOrbs = 64;
        private readonly List<LabGourdBomb> gourdBombs = new List<LabGourdBomb>();
        private uint gourdBombCount;
        private readonly List<LabSabotage> sabotages = new List<LabSabotage>();
        // 기믹 묶음 (#311): 공용·고유 묶음마다 다음 발동 시각, 지금까지 뽑은 수, 바로 앞에 건 사람.
        private double nextCommonGimmickAt, nextUniqueGimmickAt;
        private uint commonGimmickCount, uniqueGimmickCount;
        private ulong? lastCommonTarget, lastUniqueTarget;
        // 판별 기믹 기록 (#311, UT용)과 동시 발동을 세기 위한 이번 판의 마지막 공용 기믹 시각.
        private LabGimmickStats gimmickStats = new LabGimmickStats();
        private double lastCommonCastAt = double.NegativeInfinity;
        private readonly List<LabFoxFireDrop> foxFires = new List<LabFoxFireDrop>();
        private ulong charmedPlayer;
        private double charmStartedAt = -1d, charmUntil = -1d;
        private int charmTaps;
        // 매혹 예고와 양옆 풀기 (#305): the Host picks the charmed player charmWarnedAt, the charm starts after the
        // warning, and each player beside keeps their own tap count.
        private double charmWarnedAt = -1d;
        private readonly Dictionary<ulong, int> charmHelperTaps = new Dictionary<ulong, int>();
        private uint drumHandoffCount;
        private readonly LabGourdCombo combo = new LabGourdCombo();
        // 던진 순간의 페널티 배율 (#222). 날아가는 동안 페널티가 끝나도 그 공은 던질 때 크기 그대로 칠한다.
        private readonly Dictionary<string, float> launchScales = new Dictionary<string, float>(StringComparer.Ordinal);
        private double nextComboAt;
        // 야광귀 섬광 (#268): 다음 번쩍임 시각·대상, 지금까지 번쩍인 수, 마지막 번쩍임, 콤보 때문에 기다리는 중인지
        private double nextFlashAt;
        private ulong nextFlashTarget;
        private uint flashCount;
        private double lastFlashAt;
        private ulong lastFlashTarget;
        private bool flashHeld;
        // 콤보 때문에 섬광 예고를 미뤘었는지. 콤보가 끝난 첫 순간 예고를 처음부터 다시 시작한다.
        private bool flashWasHeld;
        // 섬광 예고가 실제로 시작됐는지 (#271). 시작된 뒤에는 다른 이벤트가 열리지 않는다.
        private bool flashWarned;
        // 야광귀 모드의 이벤트 차례 (#271): 콤보 예고가 실제로 시작됐는지, 다른 이벤트 때문에 미뤘는지
        private bool comboWarned, comboHeld, comboWasHeld;
        // 차례를 기다리는 콤보·섬광 (#271): 예고를 시작할 때가 됐는데 다른 이벤트 때문에 미루기 시작한 시각, 없으면 -1.
        // 기다리는 동안 새 폭탄·넘기기 막기·방해공작을 걸지 않아 화면이 곧 비고, 둘 다 기다리면 먼저 기다린 쪽이 먼저 한다.
        private double comboQueuedAt = -1d, flashQueuedAt = -1d;
        // 화면마다 콤보·섬광·그 밖의 이벤트(폭탄·넘기기 막기·여우불·매혹)가 마지막으로 열려 있던 시각 (#271).
        // 이벤트가 닫힌 뒤 EventGapSeconds 동안은 그 화면을 쉬게 한다.
        private readonly Dictionary<ulong, double[]> screenBusyAt = new Dictionary<ulong, double[]>();
        /// <summary>야광귀 모드에서 한 화면의 이벤트가 끝나고 다음 이벤트가 시작되기까지 쉬는 시간 (#271)</summary>
        internal const double EventGapSeconds = 3d;
        private Rules rules;
        private string sessionId;
        private uint round;
        private uint seed;
        private uint combinedCount;
        private ulong revision;
        private int hp;
        private readonly LabPaintSurface paintSurface = new LabPaintSurface();
        private double lastTime;
        private double deadline;
        private double remaining;
        private double played;
        private LabPhase phase = LabPhase.Idle;

        public string SessionId => sessionId;
        public uint Round => round;
        public LabPhase Phase => phase;
        public LabStageYokai StageYokai => rules != null ? rules.StageYokai : LabStageYokai.None;
        /// <summary>Current ledger revision; reading it does not advance the Host battle clock.</summary>
        public ulong Revision => revision;

        /// <summary>Independent player resource/statistic records in the established P/seat order.</summary>
        public LabPlayerState[] CopyPlayers() => players.Values.OrderBy(player => player.seat)
            .Select(player => player.Copy()).ToArray();

        /// <summary>
        /// Independent ledger entries for one owner, including drums and in-flight orbs. Callers
        /// select which entries to display; this does not tick expiry or allocate every board.
        /// </summary>
        public LabOrbState[] CopyOwnedStates(ulong owner) => orbs.Values.Concat(drums.Values)
            .Where(orb => orb.owner == owner).OrderBy(orb => orb.id, StringComparer.Ordinal)
            .Select(orb => orb.Copy()).ToArray();
        public int Hp => hp;
        /// <summary>Round-owned CPU paint state; rendering reads this state and never decides victory.</summary>
        public LabPaintSurface PaintSurface => paintSurface;

        /// <summary>The element palette captured when this round began, independent of later material edits.</summary>
        public Color PaintColorFor(LabOrbElement element) => rules != null
            && element >= LabOrbElement.Fire && element <= LabOrbElement.Earth
            ? rules.PaintColors[(int)element] : Color.clear;

        /// <summary>ids must be the Host-approved player IDs in P1..PN order.</summary>
        public void Begin(string id, ulong[] ids, double now, LabConfig config,
            LabStageYokai stageYokai = LabStageYokai.None)
        {
            if (ids == null || ids.Length < MinimumParticipants || ids.Length > MaximumParticipants
                || ids.Distinct().Count() != ids.Length)
                throw new ArgumentException("Two to five distinct player IDs are required.", nameof(ids));
            BeginWithSeats(id, ids, now, config, stageYokai);
        }

        /// <summary>Development-only battle model with one real player and no transferable neighbor.</summary>
        public void BeginSolo(string id, ulong playerId, double now, LabConfig config,
            LabStageYokai stageYokai = LabStageYokai.None)
        {
            BeginWithSeats(id, new[] { playerId }, now, config, stageYokai);
        }

        private void BeginWithSeats(string id, ulong[] ids, double now, LabConfig config, LabStageYokai stageYokai)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) throw new ArgumentException("A bounded session ID is required.", nameof(id));
            if (!Finite(now) || now < 0d || config == null) throw new ArgumentException("Valid Host time and a Config are required.");
            if (phase == LabPhase.Playing) throw new InvalidOperationException("End the current round before beginning another.");
            if (sessionId == id && round == uint.MaxValue) throw new InvalidOperationException("The round number is exhausted.");
            if (stageYokai < LabStageYokai.None || stageYokai > LabStageYokai.Jigwi)
                throw new ArgumentOutOfRangeException(nameof(stageYokai));
            // 2:2 땅따먹기 (#281)는 요괴 없이 진행한다.
            bool nextTeamMode = TeamModeFor(config, ids.Length);
            if (nextTeamMode) stageYokai = LabStageYokai.None;
            var nextRules = new Rules(config, stageYokai, nextTeamMode);
            double nextDeadline = now + nextRules.BattleDurationSeconds;
            if (!Finite(nextDeadline) || nextDeadline <= now)
                throw new ArgumentOutOfRangeException(nameof(now), "The battle deadline is not representable.");

            round = sessionId == id ? round + 1u : 1u;
            sessionId = id;
            rules = nextRules;
            teamMode = nextTeamMode;
            ResetTeam();
            activeSeatCount = ids.Length;
            Array.Clear(seats, 0, seats.Length);
            for (int i = 0; i < activeSeatCount; i++) seats[i] = ids[i];
            players.Clear(); orbs.Clear(); drums.Clear(); requests.Clear(); generatedCounts.Clear(); bombExplosions.Clear();
            rawGenerationStreaks.Clear();
            unreportedBlasts.Clear();
            orbExpiries.Clear();
            gourdBombs.Clear();
            sabotages.Clear();
            foxFires.Clear();
            for (int i = 0; i < activeSeatCount; i++)
            {
                players.Add(ids[i], new LabPlayerState
                {
                    id = ids[i], seat = i, stamina = rules.StaminaStart, personalHp = rules.PlayerMaxHp
                });
                generatedCounts.Add(ids[i], 0u);
            }
            seed = SeedFor(id, round);
            combinedCount = 0;
            hp = rules.MonsterMaxHp;
            paintSurface.Reset(rules.PaintGrowthSettings, seed);
            revision = 0;
            PlaceDrums(now);
            gourdBombCount = 0;
            drumHandoffCount = 0;
            swipeBlockCount = 0;
            swipeBlockedPlayer = 0;
            swipeBlockedRight = false;
            swipeBlockStartedAt = swipeBlockedUntil = -1d;
            charmedPlayer = 0;
            charmStartedAt = charmUntil = charmWarnedAt = -1d;
            charmTaps = 0;
            charmHelperTaps.Clear();
            // 기믹 묶음 (#311): 각 묶음은 전투 시작 뒤 자기 간격만큼 지나 처음 발동한다.
            commonGimmickCount = uniqueGimmickCount = 0;
            lastCommonTarget = lastUniqueTarget = null;
            gimmickStats = new LabGimmickStats();
            lastCommonCastAt = double.NegativeInfinity;
            nextCommonGimmickAt = FirstGimmickAt(now, rules.CommonGimmickInterval, rules.CommonGimmicks);
            nextUniqueGimmickAt = FirstGimmickAt(now, rules.UniqueGimmickInterval, rules.UniqueGimmicks);
            combo.Reset();
            launchScales.Clear();
            nextComboAt = now + rules.ComboFirstStartSeconds;
            flashCount = 0;
            lastFlashAt = -1d;
            lastFlashTarget = 0;
            flashHeld = false;
            flashWasHeld = false;
            flashWarned = false;
            comboWarned = comboHeld = comboWasHeld = false;
            comboQueuedAt = flashQueuedAt = -1d;
            screenBusyAt.Clear();
            nextFlashAt = rules.FlashEnabled ? now + rules.FlashFirstStartSeconds : double.PositiveInfinity;
            nextFlashTarget = DrawFlashTarget(0u);
            lastTime = now;
            deadline = nextDeadline;
            remaining = rules.BattleDurationSeconds;
            played = 0d;
            phase = LabPhase.Playing;
            ResetJigwi(config);
            ResetStageYokai(config, now);
            ResetYaguangTimeline(now);
            ResetJigwiStage(config, now);
        }

        /// <summary>Advance the absolute Host clock; only Idle orbs expire.</summary>
        public void Tick(double now)
        {
            if (!TryAdvance(now, out string reason)) throw new ArgumentOutOfRangeException(nameof(now), reason);
        }

        /// <summary>
        /// Install the Host's next local gravity sample after consuming the previous interval.
        /// Requests and snapshots share this state; they do not resample target pose themselves.
        /// </summary>
        public bool SetPaintFlowDirection(Vector3 localDown, double now)
        {
            float magnitude = localDown.sqrMagnitude;
            if (!Finite(localDown.x) || !Finite(localDown.y) || !Finite(localDown.z)
                || !Finite(magnitude) || magnitude < .00001f) return false;
            if (!TryAdvance(now, out _)) return false;
            return paintSurface.SetFlowDirection(localDown);
        }

        /// <summary>Generate from the sender's first drum at its last Host-known position.</summary>
        public bool TryGenerate(ulong sender, string requestId, double now, out LabOrbState orb, out string reason)
            => TryGenerate(sender, null, null, requestId, now, out orb, out reason);

        /// <summary>
        /// A player can generate only from a drum on their own board. The orb starts at the drum's
        /// position, which the owner's screen reports with the tap because only it simulates the drum.
        /// </summary>
        public bool TryGenerate(ulong sender, string drumId, Vector2? drumPosition, string requestId, double now,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            // Without a tapped drum, use the sender's earliest placed one (drums are never removed).
            LabOrbState drum = drumId == null
                ? drums.Values.FirstOrDefault(candidate => candidate.owner == sender)
                : drums.TryGetValue(drumId, out var tapped) && tapped.owner == sender ? tapped : null;
            if (drum == null) return Reject("NO_DRUM", out reason);
            Vector2 position = drumPosition ?? new Vector2(drum.x, drum.y);
            if (!Unit(position.x) || !Unit(position.y)) return Reject("INVALID_MOTION", out reason);
            var player = players[sender];
            // An overheated press still pays for a burnt attempt but creates no stored entity.
            if (!JigwiOverheated(sender, now) && CountStored(sender) >= rules.OrbStorageLimit)
                return Reject("STORAGE_FULL", out reason);
            if (player.stamina + .00001f < rules.GenerateCost) return Reject("INSUFFICIENT_STAMINA", out reason);
            uint generated = generatedCounts[sender];
            if (generated == uint.MaxValue) return Reject("GENERATION_LIMIT", out reason);
            player.stamina = Mathf.Max(0f, player.stamina - rules.GenerateCost);
            var kind = GeneratedKindFor(seed, sender, generated, rules.GenerationChances);
            kind = CorrectRawGenerationKind(kind, sender, generated);
            int lockSwipes = kind.IsItem() ? LockSwipesFor(seed, sender, generated) : 0;
            int relayStage = kind == LabOrbKind.Relay && lockSwipes == 0 ? 1 : 0;
            generatedCounts[sender] = generated + 1;
            player.generatedTotal++;
            if (JigwiOverheated(sender, now))
            {
                reason = "JIGWI_BURNED";
                return true;
            }
            Vector2 launch = SpawnVelocityFor(seed, sender, generated, position.x);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = kind, lockSwipes = lockSwipes, relayStage = relayStage,
                origin = LabOrbOrigin.Generated, originOwner = sender,
                x = position.x, y = position.y,
                vx = launch.x, vy = launch.y, createdAt = now, revision = ++revision, inFlight = false
            };
            if (teamMode) ApplyFeverGeneration(orb, sender, now);
            ApplyJigwiGenerationBurn(orb, now);
            orbs.Add(orb.id, orb);
            RecordRawGeneration(sender, orb.kind);
            reason = "GENERATED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// Use the owner's current local combination center rather than a periodic motion sample.
        /// The Host still decides polarity, consumption, the new ID and the global element draw.
        /// </summary>
        public bool TryCombineAt(ulong sender, string firstId, string secondId, string requestId,
            double now, Vector2 normalizedCenter, Vector2 normalizedVelocity,
            out LabOrbState orb, out string reason, bool passiveContact = false)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!Unit(normalizedCenter.x) || !Unit(normalizedCenter.y) || !Finite(normalizedVelocity))
                return Reject("INVALID_MOTION", out reason);
            if (string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId) || firstId == secondId ||
                !orbs.TryGetValue(firstId, out var first) || !orbs.TryGetValue(secondId, out var second))
                return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(first, sender) || !UsableBy(second, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (!first.kind.IsRaw() || !second.kind.IsRaw() || first.kind == second.kind)
                return Reject("POLARITY_MISMATCH", out reason);

            if (!passiveContact && JigwiOverheated(sender, now))
            {
                RemoveJigwiBurned(firstId);
                RemoveJigwiBurned(secondId);
                reason = "JIGWI_BURNED";
                return true;
            }

            orbs.Remove(firstId); orbs.Remove(secondId);
            var element = CombinedElementFor(sender);
            Vector2 velocity = Vector2.ClampMagnitude(normalizedVelocity, rules.MaximumMotionSpeed);
            orb = new LabOrbState
            {
                id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, element = element,
                origin = LabOrbOrigin.Combined, originOwner = sender,
                x = normalizedCenter.x, y = normalizedCenter.y,
                vx = velocity.x, vy = velocity.y, createdAt = now, revision = ++revision, inFlight = false,
                // One blast-shrunk half makes the whole orb small, and its fire burns on (#213).
                small = first.small || second.small, burnedAt = Math.Max(first.burnedAt, second.burnedAt),
                gourdBlastOnlySmall = (first.small || second.small)
                    && (!first.small || first.gourdBlastOnlySmall) && (!second.small || second.gourdBlastOnlySmall),
                gourdBlastBurnedAt = Math.Max(first.gourdBlastBurnedAt, second.gourdBlastBurnedAt),
                ordinaryBlastBurnedAt = Math.Max(first.ordinaryBlastBurnedAt, second.ordinaryBlastBurnedAt)
            };
            orbs.Add(orb.id, orb);
            players[sender].combinedTotal++;
            reason = "COMBINED";
            orb = orb.Copy();
            return true;
        }

        /// <summary>
        /// 여우불 (#251): 여우구슬과 음·양 구슬이 닿거나 겹쳐지면 둘 다 사라진다. 떨어진 뒤 보호 시간 동안은 받지 않는다.
        /// 결합 구슬도, 새 ID도 만들지 않는다.
        /// </summary>
        public bool TryFoxFireAt(ulong sender, string foxId, string orbId, string requestId, double now, out string reason)
        {
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (string.IsNullOrEmpty(foxId) || string.IsNullOrEmpty(orbId) || foxId == orbId
                || !orbs.TryGetValue(foxId, out var fox) || !orbs.TryGetValue(orbId, out var orb))
                return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(fox, sender) || !UsableBy(orb, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (fox.kind != LabOrbKind.FoxFire || !orb.kind.IsRaw()) return Reject("NOT_FOX_FIRE", out reason);
            if (now - fox.createdAt < rules.FoxFireSafeSeconds) return Reject("FOX_FIRE_SAFE", out reason);
            orbs.Remove(foxId);
            orbs.Remove(orbId);
            reason = "FOX_FIRE";
            return true;
        }

        /// <summary>
        /// Atomically validate the current boundary position and hand off the orb. No preceding
        /// motion packet is required and rejected geometry never overwrites the stored position.
        /// </summary>
        public bool TryTransferAt(ulong sender, string id, bool right, Vector2 normalizedPosition,
            Vector2 normalizedVelocity, string requestId, double now, LabEdgeCrossingKind crossing,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!TryFindMovable(id, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (activeSeatCount == 1 && current.kind != LabOrbKind.FoxFire) return Reject("NO_NEIGHBOR", out reason);
            if (!Unit(normalizedPosition.x) || !Unit(normalizedPosition.y) || !Finite(normalizedVelocity) ||
                normalizedVelocity.sqrMagnitude > rules.MaximumMotionSpeed * rules.MaximumMotionSpeed)
                return Reject("INVALID_MOTION", out reason);
            float boundaryX = normalizedPosition.x;
            if (right ? boundaryX < 1f - EdgeTolerance || normalizedVelocity.x <= 0f
                : boundaryX > EdgeTolerance || normalizedVelocity.x >= 0f)
                return Reject("NOT_OUTWARD_EDGE", out reason);
            // The gourd's swipe block (#215) stops only this player's own passes that way; a blast's push
            // and a drum running on through still go, and nothing coming in is stopped.
            bool passingDrum = crossing == LabEdgeCrossingKind.Passing
                && current.kind.IsDrum() && current.arrival == LabDrumArrival.Pass;
            if (crossing != LabEdgeCrossingKind.Pushed && !passingDrum && SwipeBlocked(sender, right, now))
                return Reject("SWIPE_BLOCKED", out reason);
            // 2:2 땅따먹기 (#281): 바깥쪽 가장자리는 막혀 있고 안쪽 가장자리로만 팀원에게 넘긴다. 밀려 넘어가도 같다.
            if (teamMode && current.kind != LabOrbKind.FoxFire && !LabTeams.EdgeOpen(players[sender].seat, right))
                return Reject("TEAM_EDGE", out reason);
            if (current.kind == LabOrbKind.FoxFire)
            {
                // A fox orb never reaches a neighbour (#251): over a side edge, even solo, it goes up in blue smoke.
                orbs.Remove(current.id);
                reason = "FOX_FIRE_GONE";
                return true;
            }
            int receiverSeat = teamMode ? LabTeams.TeammateSeat(players[sender].seat)
                : LabPlayerSlots.NeighborSeat(players[sender].seat, activeSeatCount, right);
            ulong receiver = seats[receiverSeat];
            // A drum is not a stored orb, so a full board still receives it.
            if (!current.kind.IsDrum() && CountStored(receiver) >= rules.OrbStorageLimit)
                return Reject("RECEIVER_FULL", out reason);
            if (current.ownershipVersion == ulong.MaxValue)
                return Reject("OWNERSHIP_VERSION_LIMIT", out reason);
            current.owner = receiver;
            current.ownershipVersion++;
            current.x = right ? 0f : 1f;
            current.y = normalizedPosition.y;
            current.vx = normalizedVelocity.x;
            current.vy = normalizedVelocity.y;
            current.unlockedAt = -1d;
            if (current.lockSwipes > 0)
            {
                current.lockSwipes--;
                if (current.lockSwipes == 0) current.unlockedAt = now;
                if (current.lockSwipes == 0 && current.kind == LabOrbKind.Relay) current.relayStage = 1;
                if (current.lockSwipes == 0 && current.kind == LabOrbKind.Bomb) current.createdAt = now;
            }
            else if (current.kind == LabOrbKind.Relay && current.relayStage < RelayMaxStage) current.relayStage++;
            if (current.kind.IsDrum()) ArriveDrum(current, crossing, now);
            current.revision = ++revision;
            if (current.kind.IsDrum()) players[sender].drumPassedTotal++;
            else players[sender].passedTotal++;
            reason = "TRANSFERRED";
            orb = current.Copy();
            return true;
        }

        public bool TryLaunch(ulong sender, string id, string requestId, double now,
            out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!orbs.TryGetValue(id ?? string.Empty, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (JigwiOverheated(sender, now))
            {
                RemoveJigwiBurned(id);
                reason = "JIGWI_BURNED";
                return true;
            }
            if (!current.kind.CanThrow(current.lockSwipes)) return Reject("RAW_CANNOT_ATTACK", out reason);
            // Charmed (#251): too dazed to attack until it ends or the players beside wake them.
            if (Charmed(sender, now)) return Reject("CHARMED", out reason);
            current.inFlight = true;
            current.vx = current.vy = 0f;
            current.revision = ++revision;
            // 릴레이·폭탄은 던지는 것이 곧 아이템 사용이다.
            if (current.kind == LabOrbKind.Relay) players[sender].relayUsed++;
            else if (current.kind == LabOrbKind.Bomb) players[sender].bombUsed++;
            float launchScale = ThrowScaleFor(sender, now);
            if (launchScale != 1f) launchScales[current.id] = launchScale;
            reason = "LAUNCHED";
            orb = current.Copy();
            return true;
        }

        /// <summary>Call only after the Host projectile actually collides with the cylinder target.</summary>
        public bool ConfirmHit(string id, double now)
            => ConfirmHit(id, now, out _, out _);

        /// <summary>
        /// Compatibility path for callers without a surface direction. Confirms damage and hit
        /// rewards, but cannot add paint or decide a paint victory from an external coverage value.
        /// </summary>
        public bool ConfirmHit(string id, double now, out LabOrbElement element, out int actualDamage)
            => ConfirmHit(id, now, Vector3.zero, out element, out actualDamage, out _);

        /// <summary>
        /// Atomically confirm one Host-owned projectile collision, rewards, CPU paint and victory.
        /// actualDamage is the HP really removed. impactValue is the positive presentation/attack
        /// value even after the diagnostic HP reaches zero.
        /// </summary>
        public bool ConfirmHit(string id, double now, Vector3 paintDirection,
            out LabOrbElement element, out int actualDamage, out int impactValue)
        {
            element = LabOrbElement.None;
            actualDamage = 0;
            impactValue = 0;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing
                || !orbs.TryGetValue(id ?? string.Empty, out var orb) || !orb.inFlight
                || !players.TryGetValue(orb.owner, out var attacker)) return false;
            element = orb.element;
            if (element >= LabOrbElement.Fire && element <= LabOrbElement.Earth)
                TryExtinguishJigwiStageRoot(orb.owner, paintDirection, now, out _);
            orbs.Remove(id);
            float areaScale = LaunchScaleOf(id);
            launchScales.Remove(id);
            int attackValue = DamageFor(orb);
            int previousHp = hp;
            hp = Math.Max(0, hp - attackValue);
            actualDamage = previousHp - hp;
            impactValue = attackValue;
            attacker.stamina = Mathf.Min(rules.StaminaMax, attacker.stamina + rules.HitRecovery);
            attacker.hitTotal++;
            attacker.throwTotal++;
            // Keep the attack-score statistic even after diagnostic HP reaches zero.
            // Long paint rounds can outlive diagnostic HP, so prevent the integer score wrapping.
            attacker.damageTotal = (int)Math.Min(int.MaxValue,
                (long)attacker.damageTotal + attackValue);

            if (teamMode)
                ConfirmTeamHit(attacker, element, paintDirection, (orb.small ? rules.SmallOrbPaintShare : 1f) * areaScale, now);
            else
            {
                if (element >= LabOrbElement.Fire && element <= LabOrbElement.Earth
                    && paintSurface.RegisterHit(paintDirection, PaintColorFor(element), attacker.id, attacker.seat + 1,
                        (orb.small ? rules.SmallOrbPaintShare : 1f) * areaScale)
                    && paintSurface.IsComplete)
                {
                    phase = LabPhase.Victory;
                    if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                    AdvanceJigwiStage(now);
                }
            }
            if (rules.ComboEnabled && combo.Active)
            {
                LabComboResult result = combo.Judge(element, attacker.id, now);
                if (result != LabComboResult.None) OnComboFinished(result);
            }
            return true;
        }

        /// <summary>
        /// 박 색상 콤보 실패 페널티(#222) 중이면 이 사람이 지금 던지는 공의 크기·칠 면적 배율,
        /// 아니면 1. 실험이 꺼져 있으면 언제나 1이다.
        /// </summary>
        public float LaunchScaleOf(string id) =>
            id != null && launchScales.TryGetValue(id, out float scale) ? scale : 1f;

        /// <summary>박 색상 콤보 페널티/보상(#222) 배율. 던지는 공 반지름과 칠 면적에 곱한다.</summary>
        public float ThrowScaleFor(ulong playerId, double now)
        {
            if (rules == null || !rules.ComboEnabled || !players.TryGetValue(playerId, out var player)) return 1f;
            if (player.comboPenaltyUntil > now) return rules.ComboPenaltySizeScale;
            return player.comboRewardUntil > now ? rules.ComboRewardSizeScale : 1f;
        }

        /// <summary>
        /// 박 색상 콤보 (#222): 정해 둔 시각마다 4칸 색 콤보를 내고, 제한 시간이 지나면 실패로 끝낸다.
        /// 박 폭탄처럼 시간을 한 번에 많이 건너뛰어도 그 사이 있었어야 할 일을 시각 순서대로 처리한다.
        /// 간격은 앞 콤보가 나온 시각부터 재고, 앞 콤보가 그때까지 진행 중이면 끝난 직후에 나온다.
        /// </summary>
        private void AdvanceCombo(double until)
        {
            comboHeld = false;
            if (!rules.ComboEnabled) { comboQueuedAt = -1d; return; }
            while (true)
            {
                if (combo.Active)
                {
                    if (!combo.TryExpire(until)) return;
                    OnComboFinished(LabComboResult.Timeout);
                    continue;
                }
                if (!comboWarned)
                {
                    // 야광귀 모드 (#271): 다른 이벤트(폭탄·넘기기 막기·섬광·방해공작)가 열려 있으면 예고를 시작하지 않고,
                    // 그것이 닫히면 예고부터 처음 다시 보여준다.
                    if (nextComboAt - rules.ComboWarningSeconds > until) return;
                    if (AnyScreenOpen(LabYaguangEvent.Combo, until) || FlashGoesFirst)
                    {
                        if (comboQueuedAt < 0d) comboQueuedAt = until;
                        comboHeld = comboWasHeld = true;
                        nextComboAt = Math.Max(nextComboAt, until + rules.ComboWarningSeconds);
                        return;
                    }
                    if (comboWasHeld)
                    {
                        comboWasHeld = false;
                        nextComboAt = Math.Max(nextComboAt, until + rules.ComboWarningSeconds);
                    }
                    comboQueuedAt = -1d;
                    comboWarned = true;
                }
                if (nextComboAt > until) return;
                comboWarned = false;
                double at = nextComboAt;
                var colors = new LabOrbElement[LabGourdCombo.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    uint index = combo.Count * (uint)LabGourdCombo.Length + (uint)i;
                    colors[i] = (LabOrbElement)(1 + (int)(DrawFor(seed ^ ComboColorSalt, ComboDrawKey, index) % 5UL));
                }
                combo.Start(colors, at, rules.ComboTimeLimitSeconds);
                // Scheduled stages receive the next occurrence from CastGimmicks, never from this effect.
                nextComboAt = rules.YaguangTimeline ? double.PositiveInfinity : at + rules.ComboRepeatIntervalSeconds;
            }
        }

        /// <summary>
        /// 야광귀 섬광 (#268): 정해 둔 시각마다 번쩍인다. 화면 표시(하얗게 보이기)만 하므로 Host는 시각과 대상만 정한다.
        /// 콤보 한 판(예고 → 4칸 → 결과 → 페널티/보상 효과)이 진행 중인 동안에는 섬광을 시작하지 않고 기다렸다가,
        /// 그 판이 완전히 끝나면 예고부터 다시 보여준다(#271). 섬광이 먼저 시작했으면 콤보가 섬광이 끝나기를 기다린다.
        /// 차례를 기다리는 동안은 새 폭탄·넘기기 막기를 걸지 않아, 섬광도 콤보처럼 정해 둔 간격 가까이 나온다.
        /// 그래서 섬광과 콤보 한 판은 상태로 서로를 피하고, DEV에서 시각을 어떻게 당겨도 겹치지 않는다.
        /// 간격은 번쩍인 시각부터 재고, 눈부심·회복·다음 예고가 겹치지 않을 만큼은 언제나 띄운다.
        /// </summary>
        private void AdvanceFlash(double until)
        {
            flashHeld = false;
            if (!rules.FlashEnabled) { flashQueuedAt = -1d; return; }
            while (true)
            {
                if (!flashWarned)
                {
                    if (nextFlashAt - rules.FlashWarningSeconds > until) return;
                    if (FlashBlocked(until))
                    {
                        // 예고가 시작될 때가 됐는데 콤보 한 판이나 다른 이벤트가 열려 있으면, 닫힌 뒤 예고를 처음부터 보여준다.
                        if (flashQueuedAt < 0d) flashQueuedAt = until;
                        flashHeld = flashWasHeld = true;
                        nextFlashAt = Math.Max(nextFlashAt, until + rules.FlashWarningSeconds);
                        return;
                    }
                    if (flashWasHeld)
                    {
                        // 막던 것이 방금 닫혔다: 미룬 섬광을 지난 시각에 바로 터뜨리지 않고, 지금부터 예고를 다 보여준다.
                        flashWasHeld = false;
                        nextFlashAt = Math.Max(nextFlashAt, until + rules.FlashWarningSeconds);
                    }
                    SettleFlashTarget(until);
                    flashQueuedAt = -1d;
                    flashWarned = true;
                }
                if (nextFlashAt > until) return; // 예고 중
                flashWarned = false;
                double at = nextFlashAt;
                flashCount++;
                lastFlashAt = at;
                lastFlashTarget = nextFlashTarget;
                double gap = Math.Max(rules.FlashIntervalSeconds,
                    rules.FlashGlareSeconds + rules.FlashRecoverySeconds + rules.FlashWarningSeconds);
                nextFlashAt = rules.YaguangTimeline ? double.PositiveInfinity : at + gap;
                nextFlashTarget = DrawFlashTarget(flashCount);
            }
        }

        /// <summary>
        /// 콤보 한 판이 섬광을 막는지 (#271): 콤보가 진행 중이거나 예고 중이거나, 섬광보다 먼저 차례를 기다리고 있을 때.
        /// 다음 콤보가 곧 올 차례라고 섬광이 미리 비켜 주지는 않는다. 먼저 시작한 쪽이 먼저 하고, 다른 쪽은 끝나기를
        /// 기다렸다가 예고부터 다시 한다. 콤보 뒤 페널티/보상은 받은 사람 화면에서 막는다(ScreenOpen).
        /// </summary>
        private bool ComboHoldsFlash(double at) => rules.ComboEnabled && (combo.Active || comboWarned || ComboGoesFirst);

        /// <summary>둘 다 차례를 기다릴 때 콤보가 먼저인지: 콤보가 먼저(같은 순간이면 콤보) 기다리기 시작했을 때.</summary>
        private bool ComboGoesFirst => rules.ComboEnabled && comboQueuedAt >= 0d
            && (flashQueuedAt < 0d || comboQueuedAt <= flashQueuedAt);

        /// <summary>섬광이 콤보보다 먼저 차례를 기다리고 있는지</summary>
        private bool FlashGoesFirst => rules.FlashEnabled && flashQueuedAt >= 0d
            && (comboQueuedAt < 0d || flashQueuedAt < comboQueuedAt);

        /// <summary>
        /// 섬광 예고를 지금 시작하면 안 되는지 (#271): 콤보 한 판(직전 포함) 중이거나, 맞을 화면에 다른 이벤트가 열려 있을 때.
        /// 모두(ALL)면 한 화면이라도 열려 있으면 막히고, 한 명(ONE)이면 비어 있는 화면이 하나도 없을 때만 막힌다.
        /// </summary>
        private bool FlashBlocked(double at) => ComboHoldsFlash(at)
            || (ExclusiveEvents && (rules.FlashTargetOne ? FreeFlashTargets(at).Count == 0 : AnyScreenOpen(LabYaguangEvent.Flash, at)));

        // 미룰 섬광은 예고 시각도 알리지 않는다(#271): 지금 막혀 있거나, 예정된 예고 시작 시각에 지금 상태 그대로라면
        // 미뤄질 섬광이면 -1을 보내, 어느 기기에서도 요괴·눈 말풍선이 미리 나오지 않게 한다. 이미 시작한 예고는 그대로 알린다.
        // 콤보 예고가 섬광 예고보다 먼저(같은 순간 포함) 시작될 차례면 섬광은 그 콤보 한 판 뒤로 밀리므로 역시 알리지 않는다.
        private bool FlashWaiting => phase == LabPhase.Playing && rules != null && rules.FlashEnabled
            && Finite(nextFlashAt) && !flashHeld
            && (flashWarned || (!FlashBlocked(lastTime) && !ComboWarnsBeforeFlash
                && !FlashBlocked(Math.Max(lastTime, nextFlashAt - rules.FlashWarningSeconds))));

        private bool ComboWarnsBeforeFlash => ComboWaiting && !comboWarned
            && nextComboAt - rules.ComboWarningSeconds <= nextFlashAt - rules.FlashWarningSeconds;

        // 섬광 예고가 콤보 예고보다 먼저 시작되고 콤보 예고 시각까지 이어질 차례면, 콤보는 그 섬광 뒤로 밀린다.
        private bool FlashRunsIntoCombo
        {
            get
            {
                if (rules == null || !rules.FlashEnabled || flashWarned || phase != LabPhase.Playing) return false;
                double flashWarningAt = nextFlashAt - rules.FlashWarningSeconds;
                double comboWarningAt = nextComboAt - rules.ComboWarningSeconds;
                return flashWarningAt < comboWarningAt
                    && nextFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds > comboWarningAt;
            }
        }

        /// <summary>
        /// 야광귀 모드 (#271): 콤보나 섬광이 켜져 있으면 <b>각자의 화면에서</b> 이벤트가 한 번에 하나만 열린다.
        /// 박 폭탄·넘기기 막기·섬광·콤보·방해공작(여우불·매혹)이 그 화면에 열려 있으면 그 화면에 새 이벤트를 걸지 않는다.
        /// </summary>
        private bool ExclusiveEvents => rules != null && !rules.YaguangTimeline && (rules.ComboEnabled || rules.FlashEnabled);

        /// <summary>
        /// 이 사람 화면에 지금 열린 이벤트가 있는지 (#271). 시각을 미리 계산하는 것이 아니라 지금 상태를 본다.
        /// self 종류의 열림은 빼고 보되, 박 폭탄과 넘기기 막기는 같은 종류도 본다(한 화면에 폭탄 하나, 막기는 한 번에 하나).
        /// - 콤보: 예고가 시작됐거나 진행 중이면 모든 화면, 그 뒤 페널티/보상 효과는 받은 사람 화면
        /// - 섬광: 예고 ~ 회복 동안 맞을 사람 화면(모두면 모든 화면)
        /// - 박 폭탄: 그 폭탄을 지금 가진 사람 화면(터지거나 쓰여 없어질 때까지)
        /// - 넘기기 막기 · 매혹 · 여우구슬: 걸린 사람 화면
        /// </summary>
        private bool ScreenOpen(ulong player, double at, LabYaguangEvent self)
        {
            if (!ExclusiveEvents) return false;
            // 콤보·섬광이 차례를 기다리는 동안에는 새 폭탄·넘기기 막기·방해공작을 걸지 않는다(이미 열린 것은 끝까지 간다).
            // 그래야 화면이 곧 비어 콤보·섬광이 밀리지 않고 정해 둔 간격 가까이 나온다.
            if (self != LabYaguangEvent.Combo && self != LabYaguangEvent.Flash
                && ((rules.ComboEnabled && comboQueuedAt >= 0d) || (rules.FlashEnabled && flashQueuedAt >= 0d))) return true;
            // 앞 이벤트가 닫힌 지 EventGapSeconds가 안 됐으면 아직 쉬는 중이다. 콤보·섬광은 자기 다음 차례와의 간격을
            // 따로 지키므로(간격·예고) 자기 종류의 쉬는 시간은 보지 않는다.
            if (screenBusyAt.TryGetValue(player, out double[] busy))
                for (int kind = 0; kind < busy.Length; kind++)
                {
                    if ((kind == BusyCombo && self == LabYaguangEvent.Combo) || (kind == BusyFlash && self == LabYaguangEvent.Flash)) continue;
                    if (at < busy[kind] + EventGapSeconds) return true;
                }
            if (self != LabYaguangEvent.Combo && ComboOpenOn(player, at)) return true;
            if (self != LabYaguangEvent.Flash && FlashOpenOn(player, at)) return true;
            return OtherOpenOn(player, at, self);
        }

        private const int BusyCombo = 0, BusyFlash = 1, BusyOther = 2;

        /// <summary>콤보 한 판이 이 화면에 열려 있는지: 예고·4칸 동안은 모든 화면, 페널티/보상 효과는 받은 사람 화면</summary>
        private bool ComboOpenOn(ulong player, double at)
        {
            if (!rules.ComboEnabled) return false;
            if (comboWarned || combo.Active) return true;
            return players.TryGetValue(player, out LabPlayerState state)
                && (state.comboPenaltyUntil > at || state.comboRewardUntil > at);
        }

        /// <summary>폭탄·넘기기 막기·매혹·여우구슬이 이 화면에 열려 있는지. 방해공작은 자기 매혹·여우불은 보지 않는다.</summary>
        private bool OtherOpenOn(ulong player, double at, LabYaguangEvent self)
        {
            if (BlockOn(at) && swipeBlockedPlayer == player) return true;
            if (self != LabYaguangEvent.Sabotage && CharmComingOrOn(at) && charmedPlayer == player) return true;
            foreach (LabOrbState orb in orbs.Values)
            {
                if (orb.owner != player) continue;
                if (orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd) return true;
                if (self != LabYaguangEvent.Sabotage && orb.kind == LabOrbKind.FoxFire) return true;
            }
            return false;
        }

        /// <summary>이 사람이 지금 섬광(예고 ~ 회복) 중인지</summary>
        private bool FlashOpenOn(ulong player, double at)
        {
            if (!rules.FlashEnabled) return false;
            ulong target;
            if (flashWarned) target = nextFlashTarget;
            else if (flashCount > 0 && at < lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds) target = lastFlashTarget;
            else return false;
            return !rules.FlashTargetOne || target == player;
        }

        /// <summary>
        /// 직전 시각에 이벤트가 열려 있던 화면에 지금 시각을 적어 둔다 (#271). 이벤트가 닫히면 그 뒤 EventGapSeconds 동안
        /// ScreenOpen이 그 화면을 계속 열린 것으로 봐, 다음 이벤트가 숨 돌릴 틈 없이 바로 이어지지 않는다.
        /// </summary>
        private void MarkBusyScreens(double before, double now)
        {
            if (!ExclusiveEvents) return;
            for (int i = 0; i < activeSeatCount; i++)
            {
                ulong player = seats[i];
                if (!screenBusyAt.TryGetValue(player, out double[] busy))
                    screenBusyAt[player] = busy = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
                // 직전 시각에 열려 있었으면 닫힌 시각부터 쉰다. 끝 시각을 아는 것(섬광 회복, 콤보 효과, 넘기기 막기, 매혹)은
                // 그 시각을, 모르는 것(폭탄·여우구슬이 없어진 때)은 지금을 쓴다. 그래서 쉬는 시간이 3초보다 짧아지지 않는다.
                if (ComboOpenOn(player, before))
                    busy[BusyCombo] = ComboOpenOn(player, now) ? now : ClosedAt(before, now, ComboEffectEnd(player));
                if (FlashOpenOn(player, before))
                    busy[BusyFlash] = FlashOpenOn(player, now) ? now
                        : ClosedAt(before, now, lastFlashAt + rules.FlashGlareSeconds + rules.FlashRecoverySeconds);
                if (OtherOpenOn(player, before, LabYaguangEvent.GourdBomb))
                    busy[BusyOther] = OtherOpenOn(player, now, LabYaguangEvent.GourdBomb) ? now : ClosedAt(before, now, OtherEnd(player, before));
            }
        }

        private static double ClosedAt(double before, double now, double end) => Math.Max(before, Math.Min(now, end));

        private double ComboEffectEnd(ulong player) =>
            players.TryGetValue(player, out LabPlayerState state) ? Math.Max(state.comboPenaltyUntil, state.comboRewardUntil) : lastTime;

        /// <summary>직전 시각에 이 화면에 열려 있던 폭탄 밖 이벤트의 끝 시각. 폭탄·여우구슬이 있었으면 알 수 없어 무한대.</summary>
        private double OtherEnd(ulong player, double before)
        {
            foreach (LabOrbState orb in orbs.Values)
                if (orb.owner == player && ((orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd) || orb.kind == LabOrbKind.FoxFire))
                    return double.PositiveInfinity;
            double end = before;
            if (BlockOn(before) && swipeBlockedPlayer == player) end = Math.Max(end, swipeBlockedUntil);
            if (CharmComingOrOn(before) && charmedPlayer == player) end = Math.Max(end, charmUntil);
            return end;
        }

        private bool AnyScreenOpen(LabYaguangEvent self, double at)
        {
            for (int i = 0; i < activeSeatCount; i++)
                if (ScreenOpen(seats[i], at, self)) return true;
            return false;
        }

        /// <summary>한 명(ONE) 섬광을 받을 수 있는 화면: 바로 앞에 맞은 사람(두 명 이상일 때)과 이벤트 중인 화면을 뺀다.</summary>
        private List<ulong> FreeFlashTargets(double at)
        {
            var free = new List<ulong>();
            for (int i = 0; i < activeSeatCount; i++)
            {
                ulong player = seats[i];
                if (flashCount > 0 && activeSeatCount > 1 && player == lastFlashTarget) continue;
                if (!ScreenOpen(player, at, LabYaguangEvent.Flash)) free.Add(player);
            }
            return free;
        }

        /// <summary>한 명 섬광의 예고를 시작할 때, 미리 뽑아 둔 사람 화면이 이벤트 중이면 비어 있는 화면에서 다시 고른다.</summary>
        private void SettleFlashTarget(double at)
        {
            if (!rules.FlashTargetOne || !ExclusiveEvents || !ScreenOpen(nextFlashTarget, at, LabYaguangEvent.Flash)) return;
            List<ulong> free = FreeFlashTargets(at);
            if (free.Count == 0) return;
            nextFlashTarget = free[(int)(DrawFor(seed ^ FlashTargetSalt, FlashDrawKey, flashCount + 0x10000u) % (ulong)free.Count)];
        }

        /// <summary>
        /// 섬광 대상. ALL 여부는 별도 bool로 공유한다. ONE이면 실제 ID 0도 포함한 자리 순서에서 한 명을 뽑는다.
        /// 두 명 이상이면 바로 앞 섬광을 맞은 사람은 빼서 같은 사람이 연달아 맞지 않게 한다.
        /// </summary>
        private ulong DrawFlashTarget(uint index)
        {
            if (!rules.FlashTargetOne || activeSeatCount <= 0) return 0;
            var candidates = new List<ulong>();
            for (int i = 0; i < activeSeatCount; i++)
                if (index == 0u || activeSeatCount == 1 || seats[i] != lastFlashTarget) candidates.Add(seats[i]);
            return candidates[(int)(DrawFor(seed ^ FlashTargetSalt, FlashDrawKey, index) % (ulong)candidates.Count)];
        }

        /// <summary>전투 중이고 콤보가 켜져 있으며, 진행 중인 콤보 없이 다음 콤보를 기다리는 중인지 (#241)</summary>
        private bool ComboWaiting => phase == LabPhase.Playing && rules != null && rules.ComboEnabled
            && Finite(nextComboAt) && !combo.Active;

        // 다른 이벤트 때문에 미룰 콤보 예고는 알리지 않는다 (#271). 이미 시작한 예고는 그대로 알린다.
        private bool ComboAnnounced => ComboWaiting && !comboHeld
            && (comboWarned || (!AnyScreenOpen(LabYaguangEvent.Combo, lastTime) && !FlashGoesFirst && !FlashRunsIntoCombo));

        /// <summary>
        /// 콤보가 끝난 뒤 처리. 성공은 팀 전체에 보상을 주고, 실패는 페널티를 건다.
        /// 틀린 색 + "던진 사람만" 설정이면 그 사람에게만, 그 밖(시간 초과 포함)에는 모두에게 건다.
        /// 다음 콤보는 아무리 일러도 끝난 시각 + 예고 시간(#241) 뒤에 나온다. 앞 콤보가 늦게 끝나도
        /// 예고(COMBO! 3, 2, 1)를 다 보여준 다음 4칸이 나오게 하려는 것이다.
        /// </summary>
        private void OnComboFinished(LabComboResult result)
        {
            double at = combo.LastResultAt;
            double earliest = at + rules.ComboWarningSeconds;
            if (nextComboAt < earliest) nextComboAt = earliest;
            if (result == LabComboResult.Success)
            {
                // 4칸을 다 같이 채웠으므로 보상은 언제나 팀 전체. 진행 중이던 페널티는 여기서 끝난다.
                if (rules.ComboRewardDurationSeconds <= 0f) return;
                double rewardUntil = at + rules.ComboRewardDurationSeconds;
                foreach (var player in players.Values)
                {
                    player.comboRewardUntil = Math.Max(player.comboRewardUntil, rewardUntil);
                    player.comboPenaltyUntil = Math.Min(player.comboPenaltyUntil, at);
                }
                return;
            }
            if (rules.ComboPenaltyDurationSeconds <= 0f) return;
            double until = at + rules.ComboPenaltyDurationSeconds;
            bool throwerOnly = rules.ComboPenaltyThrowerOnly && result == LabComboResult.WrongColor;
            foreach (var player in players.Values)
            {
                if (throwerOnly && player.id != combo.LastFailedBy) continue;
                player.comboPenaltyUntil = Math.Max(player.comboPenaltyUntil, until);
                // 진행 중이던 보상은 페널티를 받는 사람에게서만 끝난다.
                player.comboRewardUntil = Math.Min(player.comboRewardUntil, at);
            }
        }

        /// <summary>
        /// Host-only entry point for a future player-damage event. No gameplay action invokes it yet.
        /// One player reaching zero does not stop the round; all players reaching zero ends in Defeat.
        /// </summary>
        public bool TryApplyPlayerDamage(ulong playerId, int amount, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || amount <= 0 ||
                !players.TryGetValue(playerId, out var player) || player.personalHp <= 0) return false;

            player.personalHp = Math.Max(0, player.personalHp - amount);
            if (players.Values.All(current => current.personalHp == 0)) phase = LabPhase.Defeat;
            return true;
        }

        /// <summary>
        /// Double-tap use of the owner's unlocked item. A secret box opens into two throwable element
        /// orbs beside it, which needs one more free storage slot. A potion heals its user's personal
        /// HP up to the maximum; at full HP it is refused and kept so it can still be passed to a
        /// neighbor. A player already at 0 HP cannot be revived by a potion.
        /// The owner supplies its actual local position without a motion pre-update.
        /// </summary>
        public bool TryUseItemAt(ulong sender, string id, string requestId, double now,
            Vector2 normalizedPosition, out LabOrbState orb, out string reason)
        {
            orb = null;
            if (!BeginRequest(sender, requestId, now, out reason)) return false;
            if (!Unit(normalizedPosition.x) || !Unit(normalizedPosition.y)) return Reject("INVALID_MOTION", out reason);
            if (!orbs.TryGetValue(id ?? string.Empty, out var current)) return Reject("ORB_NOT_FOUND", out reason);
            if (!UsableBy(current, sender)) return Reject("NOT_OWNED_IDLE", out reason);
            if (JigwiOverheated(sender, now))
            {
                RemoveJigwiBurned(id);
                reason = "JIGWI_BURNED";
                return true;
            }
            if (!current.kind.IsDoubleTapUsable()) return Reject("NOT_USABLE", out reason);
            if (current.lockSwipes > 0) return Reject("ITEM_LOCKED", out reason);
            if (current.kind == LabOrbKind.SecretBox)
            {
                if (CountStored(sender) + 1 > rules.OrbStorageLimit) return Reject("STORAGE_FULL", out reason);
                orbs.Remove(current.id);
                foreach (float side in new[] { -1f, 1f })
                {
                    var opened = new LabOrbState
                    {
                        id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, element = CombinedElementFor(sender),
                        origin = LabOrbOrigin.Combined, originOwner = sender,
                        x = Mathf.Clamp01(normalizedPosition.x + side * SecretBoxSpread),
                        y = normalizedPosition.y,
                        vx = 0f, vy = 0f, createdAt = now, revision = ++revision, inFlight = false
                    };
                    orbs.Add(opened.id, opened);
                }
                players[sender].secretBoxUsed++;
                reason = "ITEM_USED";
                orb = current.Copy();
                return true;
            }
            var player = players[sender];
            if (player.personalHp <= 0) return Reject("PLAYER_DOWN", out reason);
            if (player.personalHp >= rules.PlayerMaxHp) return Reject("HP_FULL", out reason);
            player.personalHp = Math.Min(rules.PlayerMaxHp, player.personalHp + rules.PotionHeal);
            player.potionUsed++;
            orbs.Remove(current.id);
            reason = "ITEM_USED";
            orb = current.Copy();
            return true;
        }

        /// <summary>
        /// The holder's board reports which orbs a blast flung (#213). Only that board knows where its
        /// orbs were, so it names them; the Host checks that it sent this blast to this player a moment
        /// ago and that each orb is still theirs on the board, then sets it alight and shrinks it for the
        /// rest of its life. Drums and bombs never burn. Each blast is taken once. Returns how many
        /// orbs caught fire.
        /// </summary>
        public int MarkBlasted(ulong sender, string bombId, IReadOnlyList<string> orbIds, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing) return 0;
            foreach (string stale in unreportedBlasts.Where(blast => now - blast.Value.Value > BlastReportWindowSeconds)
                .Select(blast => blast.Key).ToArray())
                unreportedBlasts.Remove(stale);
            if (bombId == null || orbIds == null || orbIds.Count > MaximumBlastReportOrbs
                || !unreportedBlasts.TryGetValue(bombId, out var blast) || blast.Key != sender)
                return 0;
            unreportedBlasts.Remove(bombId);
            int burned = 0;
            foreach (string id in orbIds.Distinct())
            {
                if (id == null || !orbs.TryGetValue(id, out var orb) || orb.owner != sender || orb.inFlight
                    || orb.kind == LabOrbKind.Bomb || orb.kind.IsDrum() || orb.kind == LabOrbKind.FoxFire)
                    continue;
                if (JigwiStageEnabled && jigwiGourdBlastIds.Contains(bombId))
                {
                    if (!orb.small) orb.gourdBlastOnlySmall = true;
                    orb.gourdBlastBurnedAt = now;
                }
                else
                {
                    orb.ordinaryBlastBurnedAt = now;
                    orb.gourdBlastOnlySmall = false;
                }
                orb.burnedAt = now;
                orb.small = true;
                orb.revision = ++revision;
                burned++;
            }
            return burned;
        }

        /// <summary>Orbs whose lifetime ran out since the last call, each reported once (#235).</summary>
        public LabOrbExpiry[] DrainOrbExpiries()
        {
            LabOrbExpiry[] expired = orbExpiries.ToArray();
            orbExpiries.Clear();
            return expired;
        }

        /// <summary>Locked bombs that blasted at their fuse since the last call, each reported once.</summary>
        public LabBombBlast[] DrainBombExplosions()
        {
            LabBombBlast[] blasts = bombExplosions.ToArray();
            bombExplosions.Clear();
            return blasts;
        }

        /// <summary>Bombs the gourd dropped since the last call, each reported once.</summary>
        public LabGourdBomb[] DrainGourdBombs()
        {
            LabGourdBomb[] dropped = gourdBombs.ToArray();
            gourdBombs.Clear();
            return dropped;
        }

        /// <summary>Fox orbs the fox fire dropped since the last call, each drop reported once (#251).</summary>
        public LabFoxFireDrop[] DrainFoxFires()
        {
            LabFoxFireDrop[] dropped = foxFires.ToArray();
            foxFires.Clear();
            return dropped;
        }

        /// <summary>Sabotages the sabotage mix cast since the last call, each reported once (#251).</summary>
        public LabSabotage[] DrainSabotages()
        {
            LabSabotage[] cast = sabotages.ToArray();
            sabotages.Clear();
            return cast;
        }

        /// <summary>
        /// 이 사람이 지금 섬광에 눈부신지 (#301): 마지막 섬광을 맞았고, 눈부심 시간이 아직 지나지 않았고,
        /// 그 뒤 폰을 흔들어 풀지 않았다. 회복 중은 눈부신 것으로 치지 않는다.
        /// </summary>
        public bool FlashGlared(ulong player, double now)
        {
            if (!rules.FlashEnabled || phase != LabPhase.Playing || flashCount == 0 || lastFlashAt < 0d) return false;
            if (rules.FlashTargetOne && lastFlashTarget != player) return false;
            if (!players.TryGetValue(player, out var state)) return false;
            if (now < lastFlashAt || now >= lastFlashAt + rules.FlashGlareSeconds) return false;
            return state.flashReleasedAt < lastFlashAt;
        }

        /// <summary>
        /// 야광귀 섬광 해제 (#301): 눈부신 사람이 폰을 다 흔들었다. 그 사람의 눈부심만 풀고 이 시각을 남긴다.
        /// 흔들어 풀기가 꺼져 있거나, 보낸 사람이 지금 눈부시지 않으면(이미 풀었거나 시간이 지났으면) false.
        /// </summary>
        public bool TryFlashShakeRelease(ulong sender, double now)
        {
            if (!rules.FlashShakeRelease || !TryAdvance(now, out _) || !FlashGlared(sender, now)) return false;
            players[sender].flashReleasedAt = now;
            return true;
        }

        /// <summary>A wire request belongs to exactly one flash; late reports cannot clear a later flash.</summary>
        public bool TryFlashShakeRelease(ulong sender, uint generation, double now)
        {
            if (!rules.FlashShakeRelease || generation == 0 || !TryAdvance(now, out _)
                || generation != flashCount) return false;
            return TryFlashShakeRelease(sender, now);
        }

        /// <summary>
        /// Remove an expired or missed projectile without damage or a hit reward.
        /// countThrow: 빗나간 투척으로 던진 공 수(명중률 분모)에 넣을지. 지귀 불을 노린 투척은 false.
        /// 라운드가 진행 중일 때만 센다.
        /// </summary>
        public bool ConfirmMiss(string id, double now, bool countThrow = true)
        {
            if (!TryAdvance(now, out _) || !orbs.TryGetValue(id ?? string.Empty, out var orb) || !orb.inFlight) return false;
            if (countThrow && phase == LabPhase.Playing && players.TryGetValue(orb.owner, out var thrower))
                thrower.throwTotal++;
            launchScales.Remove(id);
            return orbs.Remove(id);
        }

        private bool BlockOn(double now) => phase == LabPhase.Playing && swipeBlockedUntil > now && now >= swipeBlockStartedAt;
        private bool CharmOn(double now) => phase == LabPhase.Playing && charmUntil > now && now >= charmStartedAt;
        /// <summary>The charm warning (#305): the charmed player is chosen and every screen is told, but they can still throw.</summary>
        private bool CharmWarning(double now) => phase == LabPhase.Playing && charmWarnedAt >= 0d && now >= charmWarnedAt
            && now < charmStartedAt;
        private bool CharmComingOrOn(double now) => CharmWarning(now) || CharmOn(now);

        /// <summary>Whether the fox's charm (#251) keeps this player from attacking at this Host time.</summary>
        public bool Charmed(ulong player, double now) => CharmOn(now) && player == charmedPlayer;

        /// <summary>
        /// 매혹 (#251, #305): 홀린 사람의 양옆 사람이 하트 말풍선을 누른 한 번. 양옆 사람이 각자 정해 둔 횟수를 채워야
        /// 매혹이 풀린다 (둘이 하는 판은 상대 한 사람). 먼저 다 채운 사람의 더 누르기는 세지 않는다. 양옆이 아닌 사람,
        /// 홀린 사람 자신, 예고 중이거나 매혹이 없을 때, 깨우기가 꺼져 있을 때는 받지 않는다. 이 탭으로 깨어났으면
        /// woken에 그 사람을, 아니면 null을 돌려준다 (Host의 플레이어 ID도 0이므로 구분한다).
        /// </summary>
        public bool TryCharmTap(ulong sender, double now, out ulong? woken)
        {
            woken = null;
            if (!TryAdvance(now, out _) || !CharmOn(now) || rules.CharmRescueTaps <= 0 || sender == charmedPlayer
                || Array.IndexOf(CharmHelpers(), sender) < 0)
                return false;
            charmHelperTaps.TryGetValue(sender, out int taps);
            if (taps >= rules.CharmRescueTaps) return true;
            charmHelperTaps[sender] = taps + 1;
            charmTaps++;
            foreach (ulong helper in CharmHelpers())
                if (!charmHelperTaps.TryGetValue(helper, out int done) || done < rules.CharmRescueTaps) return true;
            charmUntil = now;
            woken = charmedPlayer;
            return true;
        }

        /// <summary>The players beside the charmed one, in seat order: two, or one in a two-player room, or none solo (#305).</summary>
        private ulong[] CharmHelpers()
        {
            if (!players.TryGetValue(charmedPlayer, out var charmed) || activeSeatCount < 2) return Array.Empty<ulong>();
            int seat = charmed.seat;
            ulong before = seats[(seat + activeSeatCount - 1) % activeSeatCount];
            ulong after = seats[(seat + 1) % activeSeatCount];
            return before == after ? new[] { before } : new[] { before, after };
        }

        public LabSnapshot Snapshot(double now)
        {
            if (phase == LabPhase.Idle)
            {
                if (!Finite(now) || now < 0d) throw new ArgumentOutOfRangeException(nameof(now));
                return new LabSnapshot { sessionId = string.Empty, serverTime = now, phase = LabPhase.Idle };
            }
            Tick(now);
            LabSnapshot snapshot = new LabSnapshot
            {
                sessionId = sessionId ?? string.Empty,
                round = round,
                phase = phase,
                serverTime = now,
                remaining = remaining,
                playedSeconds = played,
                hp = hp,
                paintCoverage = paintSurface.Coverage,
                paintGoalPercent = rules.PaintVictoryPercent,
                paintColumns = paintSurface.Columns,
                paintRows = paintSurface.Rows,
                paintRevision = paintSurface.Revision,
                paintCoveredCount = paintSurface.CoveredCount,
                paintCellCount = paintSurface.CellCount,
                paintCount = paintSurface.PaintCount,
                players = players.Values.OrderBy(player => player.seat).Select(player => player.Copy()).ToArray(),
                orbs = orbs.Values.OrderBy(orb => orb.id, StringComparer.Ordinal).Select(orb => orb.Copy()).ToArray(),
                drums = drums.Values.OrderBy(drum => drum.id, StringComparer.Ordinal).Select(drum => drum.Copy()).ToArray(),
                jigwi = CopyJigwiStates(),
                jigwiStage = CopyJigwiStageSnapshot(now),
                swipeBlockedPlayer = BlockOn(now) ? swipeBlockedPlayer : 0,
                swipeBlockedRight = BlockOn(now) && swipeBlockedRight,
                swipeBlockStartedAt = BlockOn(now) ? swipeBlockStartedAt : -1d,
                swipeBlockedUntil = BlockOn(now) ? swipeBlockedUntil : -1d,
                charmedPlayer = CharmComingOrOn(now) ? charmedPlayer : 0,
                charmStartedAt = CharmComingOrOn(now) ? charmStartedAt : -1d,
                charmUntil = CharmComingOrOn(now) ? charmUntil : -1d,
                charmTaps = CharmOn(now) ? charmTaps : 0,
                charmWarnedAt = CharmComingOrOn(now) ? charmWarnedAt : -1d,
                charmHelpers = CharmOn(now) ? CharmHelpers() : Array.Empty<ulong>(),
                charmHelperTaps = CharmOn(now) ? CharmHelpers().Select(helper =>
                    charmHelperTaps.TryGetValue(helper, out int taps) ? taps : 0).ToArray() : Array.Empty<int>(),
                comboActive = combo.Active,
                comboColors = combo.Count == 0 ? Array.Empty<LabOrbElement>() : combo.Colors,
                comboProgress = combo.Progress,
                comboStartedAt = combo.StartedAt,
                comboEndsAt = combo.EndsAt,
                comboCount = combo.Count,
                comboResult = combo.LastResult,
                comboResultCount = combo.LastResultCount,
                comboResultAt = combo.LastResultAt,
                comboFailedBy = combo.LastFailedBy,
                comboNextAt = ComboAnnounced ? nextComboAt : -1d,
                comboWarningAt = ComboAnnounced ? nextComboAt - rules.ComboWarningSeconds : -1d,
                flashAt = FlashWaiting ? nextFlashAt : -1d,
                flashWarningAt = FlashWaiting ? nextFlashAt - rules.FlashWarningSeconds : -1d,
                flashTarget = nextFlashTarget,
                flashTargetAll = !rules.FlashTargetOne,
                flashCount = flashCount,
                flashLastAt = lastFlashAt,
                flashLastTarget = lastFlashTarget,
                flashLastTargetAll = !rules.FlashTargetOne
            };
            FillTeamSnapshot(snapshot);
            return snapshot;
        }

        private bool BeginRequest(ulong sender, string requestId, double now, out string reason)
        {
            if (!TryAdvance(now, out reason)) return false;
            if (phase != LabPhase.Playing) return Reject("BATTLE_NOT_PLAYING", out reason);
            if (!players.ContainsKey(sender)) return Reject("UNKNOWN_PLAYER", out reason);
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) return Reject("INVALID_REQUEST_ID", out reason);
            if (requests.Contains(requestId)) return Reject("DUPLICATE_REQUEST", out reason);
            if (requests.Count >= MaximumRequestsPerRound) return Reject("REQUEST_LIMIT", out reason);
            requests.Add(requestId);
            reason = null;
            return true;
        }

        private bool TryAdvance(double now, out string reason)
        {
            if (StageYokai == LabStageYokai.None || phase != LabPhase.Playing
                || !Finite(now) || now < lastTime) return TryAdvanceSlice(now, out reason);
            bool advanced = false;
            while (phase == LabPhase.Playing && lastTime < now)
            {
                if (!TryAdvanceSlice(NextStageBoundary(now), out reason)) return false;
                advanced = true;
            }
            if (advanced && lastTime == now) { reason = null; return true; }
            return TryAdvanceSlice(now, out reason);
        }

        private bool TryAdvanceSlice(double now, out string reason)
        {
            reason = null;
            if (phase == LabPhase.Idle) return Reject("NO_SESSION", out reason);
            if (!Finite(now) || now < lastTime) return Reject("INVALID_TIME", out reason);
            if (phase == LabPhase.Playing)
            {
                double effectiveNow = Math.Min(now, deadline);
                double elapsed = effectiveNow - lastTime;
                bool paintCompleted = false;
                if (elapsed > 0d)
                {
                    // A late frame still resolves growth that was due before the deadline.
                    // Growth due exactly at the deadline cannot outrun the existing timeout rule.
                    double completionOffset = paintSurface.Advance(elapsed, now < deadline);
                    // 2:2 (#281): 박이 두 색으로 다 차도 끝나지 않는다. 한 팀이 모두 차지했는지는 아래에서 본다.
                    if (completionOffset >= 0d && !teamMode)
                    {
                        effectiveNow = lastTime + completionOffset;
                        elapsed = completionOffset;
                        paintCompleted = true;
                    }
                }
                if (elapsed > 0d)
                {
                    played += elapsed;
                    foreach (var player in players.Values)
                        player.stamina = Mathf.Min(rules.StaminaMax,
                            (float)(player.stamina + elapsed * rules.StaminaRecoveryPerSecond * JigwiStageRecoveryMultiplier(lastTime)));
                    // 결과 대시보드 기록: 이 사이에 북을 점유한 사람의 점유 시간 (북 점유율 계산용)
                    // (북은 넘길 때도 먼저 시간을 진행시키므로 넘기기 전 주인에게 쌓인다)
                    foreach (var holder in drums.Values.Select(drum => drum.owner).Distinct())
                        if (players.TryGetValue(holder, out var drummer)) drummer.drumSeconds += elapsed;
                }
                MarkBusyScreens(lastTime, effectiveNow);
                // The schedule cutoff cancels before same-time bomb expiry or combo timeout can award a penalty.
                EndYaguangTimeline(effectiveNow);
                AdvanceJigwiStage(effectiveNow);
                if (StageYokai != LabStageYokai.None)
                {
                    ExpireOrbs(effectiveNow);
                    AdvanceJigwi(effectiveNow);
                }
                CastGimmicks(effectiveNow);
                AdvanceJigwi(effectiveNow);
                AdvanceStageJigwi(effectiveNow);
                AdvanceCombo(effectiveNow);
                AdvanceFlash(effectiveNow);
                ExpireOrbs(effectiveNow);
                remaining = Math.Max(0d, deadline - effectiveNow);
                if (teamMode)
                {
                    SettleTeamSweep();
                    if (phase == LabPhase.Playing && now >= deadline) EndTeamBattleAtDeadline();
                }
                else if (paintCompleted)
                {
                    phase = LabPhase.Victory;
                    if (players.TryGetValue(paintSurface.LastPainter, out var painter)) painter.lastHit = true;
                }
                else if (now >= deadline) phase = LabPhase.Defeat;
                if (phase != LabPhase.Playing) AdvanceJigwiStage(effectiveNow);
            }
            lastTime = now;
            return true;
        }

        /// <summary>
        /// 박 폭탄 (#177): 공용 기믹(#311)으로 뽑히면 이 사람 판에 잠긴 폭탄을 떨어뜨린다. 구슬이 가득 찬 판에도 떨어지고
        /// 스태미나는 쓰지 않는다. 잠금 횟수·도화선은 북에서 나온 폭탄과 같다: 잠긴 채 도화선(아이템 수명)이 끝나면 그때
        /// 들고 있는 사람 판에서 터진다.
        /// </summary>
        private void DropGourdBomb(ulong target, double at)
        {
            uint index = gourdBombCount++;
            var landing = new Vector2(
                Mathf.Lerp(GourdLandingMinX, GourdLandingMaxX, Unit01(DrawFor(seed ^ GourdLandingSalt, GourdDrawKey, index))),
                GourdLandingY);
            var bomb = new LabOrbState
            {
                id = NewOrbId(), owner = target, kind = LabOrbKind.Bomb,
                lockSwipes = LockSwipesFor(seed, GourdDrawKey, index),
                origin = LabOrbOrigin.Gourd, originOwner = target,
                x = landing.x, y = landing.y, createdAt = at, revision = ++revision, inFlight = false
            };
            orbs.Add(bomb.id, bomb);
            gourdBombs.Add(new LabGourdBomb(target, bomb.id, landing));
        }

        /// <summary>
        /// 옆 사람에게 넘어가는 북이 받는 판에서 어떻게 들어올지 Host가 정한다 (#178).
        /// 스와이프로 넘기면 가까운 쪽·가운데·먼 쪽 구간 또는 통과를 설정 확률로 뽑고, 구간 안의 멈출
        /// 자리(들어온 가장자리에서 판 폭 비율)도 뽑는다. 앞 판을 통과해 온 북은 이번 판의 가까운 쪽에서
        /// 반드시 멈추고, 폭발에 밀려 넘어온 북은 제 힘대로 미끄러지되 먼 쪽 끝 안에서 멈춘다.
        /// 뽑기는 seed와 전달 순번으로 정해져 모든 기기가 스냅샷으로 같은 결과를 받는다.
        /// </summary>
        private void ArriveDrum(LabOrbState drum, LabEdgeCrossingKind crossing, double now)
        {
            uint index = drumHandoffCount++;
            float[] bounds = rules.DrumStopBounds;
            int zone;
            if (crossing == LabEdgeCrossingKind.Pushed) zone = -1;
            // Only a drum that really arrived as a pass may claim to be passing on.
            else if (crossing == LabEdgeCrossingKind.Passing && drum.arrival == LabDrumArrival.Pass) zone = 0;
            else zone = DrumZoneFor(DrawFor(seed ^ DrumZoneSalt, DrumHandoffKey, index), rules.DrumStopChances);
            // 2:2 (#281): 팀원의 바깥쪽 가장자리는 막혀 있으므로 지나가는 대신 가장 먼 구역에 멈춘다.
            if (teamMode && zone >= 3) zone = 2;

            drum.arrivedAt = now;
            if (zone < 0)
            {
                drum.arrival = LabDrumArrival.Coast;
                drum.stopAt = bounds[3];
            }
            else if (zone >= 3)
            {
                drum.arrival = LabDrumArrival.Pass;
                drum.stopAt = -1f;
            }
            else
            {
                drum.arrival = LabDrumArrival.Stop;
                drum.stopAt = Mathf.Lerp(bounds[zone], bounds[zone + 1],
                    Unit01(DrawFor(seed ^ DrumSpotSalt, DrumHandoffKey, index)));
            }
        }

        /// <summary>0 near, 1 middle, 2 far, 3 pass, by the configured percentages.</summary>
        private static int DrumZoneFor(ulong draw, int[] chances)
        {
            int roll = (int)(draw % 100UL);
            for (int i = 0; i < chances.Length; i++)
            {
                roll -= chances[i];
                if (roll < 0) return i;
            }
            return 0;
        }

        /// <summary>The gourd blocks one way, drawn at random, of this player's own passes.</summary>
        private void StartSwipeBlock(ulong target, double at)
        {
            uint index = swipeBlockCount++;
            swipeBlockedPlayer = target;
            swipeBlockedRight = (DrawFor(seed ^ SwipeBlockSideSalt, SwipeBlockDrawKey, index) & 1UL) == 1UL;
            swipeBlockStartedAt = at;
            swipeBlockedUntil = at + rules.SwipeBlockSeconds;
        }

        /// <summary>
        /// 매혹 (#251): 이 사람은 정해 둔 시간 동안 헤롱헤롱해 공격(위로 던지기)을 못 한다. #305: 예고 시간만큼 먼저 모두에게
        /// 알리고 그 뒤에 걸린다 (예고 0이면 바로).
        /// </summary>
        private void StartCharm(ulong target, double at)
        {
            charmedPlayer = target;
            charmWarnedAt = at;
            charmStartedAt = at + rules.CharmWarningSeconds;
            charmUntil = charmStartedAt + rules.CharmSeconds;
            charmTaps = 0;
            charmHelperTaps.Clear();
        }

        /// <summary>
        /// 여우불 (#251): 이 사람의 판에 푸른 여우구슬을 정해 둔 개수만큼 떨어뜨린다. 판이 거의 차 있으면 빈 칸 수만큼만
        /// 떨어진다. 떨어질 자리는 판 안쪽에서 seed로 하나씩 뽑는다.
        /// </summary>
        private void DropFoxFire(ulong target, double at, uint index)
        {
            int span = rules.FoxFireCountMax - rules.FoxFireCountMin + 1;
            int count = Math.Min(rules.OrbStorageLimit - CountStored(target, at),
                rules.FoxFireCountMin + (int)(DrawFor(seed ^ FoxCountSalt, SabotageDrawKey, index) % (ulong)span));
            var ids = new string[count];
            for (int i = 0; i < count; i++)
            {
                uint spot = index * 16u + (uint)i;
                var fox = new LabOrbState
                {
                    id = NewOrbId(), owner = target, kind = LabOrbKind.FoxFire,
                    origin = LabOrbOrigin.Gourd, originOwner = target,
                    x = Mathf.Lerp(FoxLandingMinX, FoxLandingMaxX, Unit01(DrawFor(seed ^ FoxLandingXSalt, SabotageDrawKey, spot))),
                    y = Mathf.Lerp(FoxLandingMinY, FoxLandingMaxY, Unit01(DrawFor(seed ^ FoxLandingYSalt, SabotageDrawKey, spot))),
                    createdAt = at, revision = ++revision, inFlight = false
                };
                orbs.Add(fox.id, fox);
                ids[i] = fox.id;
            }
            foxFires.Add(new LabFoxFireDrop(target, ids));
        }

        /// <summary>
        /// 기믹 묶음 (#311): 공용 기믹(폭탄·넘기기 막기)과 요괴 고유 기믹(구미호: 여우불·매혹)을 각자 고정 간격마다 하나씩
        /// 건다. 두 묶음이 같은 시각이면 공용 → 고유 순서로 둘 다 건다. 시간을 한 번에 많이 건너뛰어도 그 사이 걸렸어야 할
        /// 기믹을 정해 둔 시각 그대로 차례대로 건다. 뽑기는 seed와 묶음별 순번으로 정해 모든 기기가 같은 결과를 받는다.
        /// </summary>
        private void CastGimmicks(double until)
        {
            if (rules.YaguangTimeline)
            {
                if (yaguangTimelineEnded) return;
                // Finish the previous unique effect at this gameplay boundary before admitting a waiting one.
                AdvanceCombo(until);
                AdvanceFlash(until);
            }
            while (nextCommonGimmickAt <= until || nextUniqueGimmickAt <= until)
            {
                if (nextCommonGimmickAt <= nextUniqueGimmickAt)
                {
                    double at = nextCommonGimmickAt;
                    uint index = commonGimmickCount++;
                    nextCommonGimmickAt = JigwiStageEnabled ? NextJigwiStageCommonAt(commonGimmickCount)
                        : rules.YaguangTimeline ? NextYaguangCommonAt(commonGimmickCount)
                        : at + GimmickInterval(rules.CommonGimmickInterval);
                    bool cast = rules.YaguangTimeline ? CastYaguangCommon(at, index)
                        : CastGimmick(rules.CommonGimmicks, at, index, SabotageKindSalt, SabotageTargetSalt,
                            ref lastCommonTarget);
                    if (cast) lastCommonCastAt = at;
                }
                else
                {
                    double at = nextUniqueGimmickAt;
                    uint index = uniqueGimmickCount++;
                    nextUniqueGimmickAt = rules.YaguangTimeline ? NextYaguangUniqueAt(uniqueGimmickCount)
                        : at + GimmickInterval(rules.UniqueGimmickInterval);
                    if (rules.YaguangTimeline) yaguangUniquePending.Enqueue(rules.YaguangTimelineUniqueSchedule[index]);
                    else if (CastGimmick(rules.UniqueGimmicks, at, index, UniqueGimmickKindSalt, UniqueGimmickTargetSalt,
                        ref lastUniqueTarget) && lastCommonCastAt == at) gimmickStats.simultaneous++;
                }
            }
            if (rules.YaguangTimeline) StartWaitingYaguangUnique(until);
        }

        /// <summary>A group's first cast: its interval after the start, or never when it is 0 or has no gimmicks (#311).</summary>
        private static double FirstGimmickAt(double now, float interval, LabSabotageKind[] kinds) =>
            interval > 0f && kinds.Length > 0 ? now + GimmickInterval(interval) : double.PositiveInfinity;

        private static double GimmickInterval(float interval) => Math.Max(GourdMinimumDelaySeconds, interval);

        /// <summary>
        /// 이 묶음에서 켜져 있고 지금 걸 사람이 있는 기믹 중 하나를 비율(%)대로 골라, 걸 수 있는 사람 중 무작위 한 명에게 건다
        /// (#311). 어떤 사람에게 걸 수 없는 기믹은 그 사람을 빼고 고르므로 다른 사람에게 가고, 아무에게도 걸 수 없는 기믹은
        /// 후보에서 빠진다. 남은 기믹이 없으면 이번 차례는 건너뛰고 false를 돌려준다.
        /// </summary>
        private bool CastGimmick(LabSabotageKind[] kinds, double at, uint index, uint kindSalt, uint targetSalt, ref ulong? last)
        {
            var targets = new List<ulong>[kinds.Length];
            var weights = new int[kinds.Length];
            int total = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                int kind = (int)kinds[i];
                if (!rules.GimmickEnabled[kind] || rules.GimmickChances[kind] <= 0) continue;
                targets[i] = GimmickTargets(kinds[i], at, last);
                if (targets[i].Count > 0) total += weights[i] = rules.GimmickChances[kind];
            }
            if (total == 0) return false;
            int roll = (int)(DrawFor(seed ^ kindSalt, SabotageDrawKey, index) % (ulong)total);
            for (int i = 0; i < kinds.Length; i++)
            {
                roll -= weights[i];
                if (roll >= 0) continue;
                ulong target = targets[i][(int)(DrawFor(seed ^ targetSalt, SabotageDrawKey, index) % (ulong)targets[i].Count)];
                last = target;
                DispatchGimmick(kinds[i], target, at, index);
                return true;
            }
            return false;
        }

        /// <summary>The one effect/notification/statistics path for periodic and scheduled common gimmicks.</summary>
        private void DispatchGimmick(LabSabotageKind kind, ulong target, double at, uint index)
        {
            if (kind == LabSabotageKind.GourdBomb) DropGourdBomb(target, at);
            else if (kind == LabSabotageKind.SwipeBlock) StartSwipeBlock(target, at);
            else if (kind == LabSabotageKind.FoxFire) DropFoxFire(target, at, index);
            else if (kind == LabSabotageKind.Charm) StartCharm(target, at);
            sabotages.Add(new LabSabotage(kind, target, at));
            gimmickStats.Add(kind);
        }

        /// <summary>
        /// 이번 판의 기믹 기록 (#311, UT용) 복사본. 센 값에 판 정보·결과·전투가 이어진 시간을 지금 상태로 채운다.
        /// 시간은 판이 끝난 순간에 멈춘 남은 시간으로 재므로 끝난 뒤에 읽어도 늘어나지 않는다.
        /// </summary>
        public LabGimmickStats GimmickStats()
        {
            LabGimmickStats stats = gimmickStats.Copy();
            stats.sessionId = sessionId ?? string.Empty;
            stats.round = round;
            stats.stage = teamMode ? "Territory" : StageYokai.ToString();
            stats.players = activeSeatCount;
            stats.result = phase.ToString();
            if (rules == null) return stats;
            stats.seconds = Math.Max(0d, rules.BattleDurationSeconds - remaining);
            stats.commonInterval = rules.CommonGimmicks.Length > 0 ? rules.CommonGimmickInterval : 0f;
            stats.uniqueInterval = rules.UniqueGimmicks.Length > 0 ? rules.UniqueGimmickInterval : 0f;
            PopulateYaguangStats(stats);
            return stats;
        }

        /// <summary>
        /// Who this gimmick may fall on now, in seat order. Not the player this group's last one fell on (unless solo),
        /// nor one whose ways are still blocked or who is still charmed or warned of a charm. One swipe block and one charm
        /// at a time. A charm falls in any room: with two players the other one alone wakes them, and solo it only wears off.
        /// </summary>
        private List<ulong> GimmickTargets(LabSabotageKind kind, double at, ulong? last)
        {
            var targets = new List<ulong>();
            bool blocking = BlockOn(at);
            bool charming = CharmComingOrOn(at);
            if (kind == LabSabotageKind.SwipeBlock && blocking) return targets;
            if (kind == LabSabotageKind.Charm && charming) return targets;
            // 야광귀 모드 (#271): 폭탄·넘기기 막기는 자기 종류도 보고(한 화면에 폭탄 하나), 여우불·매혹은 방해공작으로 본다.
            LabYaguangEvent self = kind == LabSabotageKind.GourdBomb ? LabYaguangEvent.GourdBomb
                : kind == LabSabotageKind.SwipeBlock ? LabYaguangEvent.SwipeBlock : LabYaguangEvent.Sabotage;
            for (int i = 0; i < activeSeatCount; i++)
            {
                ulong player = seats[i];
                if (last.HasValue && player == last.Value && activeSeatCount > 1) continue;
                if (blocking && player == swipeBlockedPlayer) continue;
                if (charming && player == charmedPlayer) continue;
                if (JigwiBlocksSabotage(player, at)) continue;
                if (kind == LabSabotageKind.FoxFire && CountStored(player, at) >= rules.OrbStorageLimit) continue;
                // 야광귀 모드 (#271): 다른 이벤트가 열려 있는 화면에는 걸지 않는다.
                if (ScreenOpen(player, at, self)) continue;
                targets.Add(player);
            }
            return targets;
        }

        /// <summary>Whether the gourd's swipe block (#215) stops this player's own passes that way at this Host time.</summary>
        public bool SwipeBlocked(ulong player, bool right, double now) =>
            phase == LabPhase.Playing && swipeBlockedUntil > now && now >= swipeBlockStartedAt
            && player == swipeBlockedPlayer && right == swipeBlockedRight;

        private int DamageFor(LabOrbState orb)
        {
            if (orb.kind == LabOrbKind.Bomb) return Math.Max(1, (int)Math.Round(rules.Damage * rules.BombMultiplier));
            if (orb.kind != LabOrbKind.Relay) return rules.Damage;
            float multiplier = orb.relayStage >= RelayMaxStage ? rules.RelayMaxMultiplier
                : orb.relayStage == 2 ? rules.RelayDoubleMultiplier : 1f;
            return Math.Max(1, (int)Math.Round(rules.Damage * multiplier));
        }

        // Items last the item lifetime; for a locked bomb that is its fuse. An unlocked bomb is an
        // ordinary attack orb and keeps the orb lifetime.
        private float LifetimeOf(LabOrbState orb) =>
            orb.kind.LifetimeFor(orb.lockSwipes, rules.OrbLifetimeSeconds, rules.ItemLifetimeSeconds);
        private double ExpiresAt(LabOrbState orb) => orb.expiresAt >= 0d ? orb.expiresAt : orb.createdAt + LifetimeOf(orb);
        private int CountStored(ulong owner) => orbs.Values.Count(orb => orb.owner == owner && !orb.inFlight && !orb.jigwiSupply);

        /// <summary>A late frame's earlier sabotage draws ignore orbs whose lifetime had already ended at that draw.</summary>
        private int CountStored(ulong owner, double at) => orbs.Values.Count(orb => orb.owner == owner && !orb.inFlight
            && !orb.jigwiSupply && at < ExpiresAt(orb));

        /// <summary>Orbs and drums both move between screens; look the ID up in either list.</summary>
        private bool TryFindMovable(string id, out LabOrbState found) =>
            orbs.TryGetValue(id ?? string.Empty, out found) || drums.TryGetValue(id ?? string.Empty, out found);

        /// <summary>
        /// Read one authoritative orb or drum without advancing time or exposing mutable ledger
        /// state. Command validation can inspect ownership without allocating a full snapshot.
        /// </summary>
        public bool TryGetMovableState(string id, out LabOrbState state)
        {
            state = null;
            if (!TryFindMovable(id, out var found)) return false;
            state = found.Copy();
            return true;
        }

        /// <summary>
        /// 판마다 북을 받을 사람을 무작위로 정한다. 북이 인원 이하이면 서로 다른 사람이 받고,
        /// 더 많으면 모두 하나씩 받은 뒤 남는 북도 무작위로 나눈다. Host만 뽑으므로 모든 기기가
        /// 스냅샷으로 같은 결과를 받는다. 북은 받은 사람 판의 가운데에서 시작한다.
        /// Drum Count가 AUTO(0)이면 이번 판 인원으로 개수를 정한다(#236, 2·3명 1개, 4·5명 2개).
        /// </summary>
        private void PlaceDrums(double now)
        {
            if (teamMode) { PlaceTeamDrums(now); return; }
            int[] shuffled = Enumerable.Range(0, activeSeatCount)
                .OrderBy(seat => DrawFor(seed ^ DrumSeatSalt, (ulong)seat, 0u)).ToArray();
            var perBoard = new int[activeSeatCount];
            // AUTO follows the players in this round (#236); a fixed count is used as it is.
            int drumCount = rules.DrumCount != LabConfig.AutoDrumCount ? rules.DrumCount
                : LabConfig.DrumsForPlayers(activeSeatCount);
            for (int i = 0; i < drumCount; i++)
            {
                int seat = i < activeSeatCount ? shuffled[i]
                    : (int)(DrawFor(seed ^ DrumSeatSalt, ulong.MaxValue, (uint)i) % (ulong)activeSeatCount);
                var drum = new LabOrbState
                {
                    id = NewOrbId(), owner = seats[seat], kind = LabOrbKind.Drum,
                    x = DrumStartX[perBoard[seat]++], y = .5f, createdAt = now, revision = ++revision
                };
                drums.Add(drum.id, drum);
            }
        }

        /// <summary>
        /// 북에서 튀어나가는 속도 (보드 너비/초). 방향은 위쪽 기준 좌우 각도 안(180°면 모든 방향),
        /// 속도는 최소~최대 사이에서 Host가 뽑는다. 종류 뽑기와 같은 seed 방식이라
        /// 한 사람의 생성이 다른 사람의 결과를 바꾸지 않고, 모든 기기가 스냅샷으로 같은 값을 받는다.
        /// 북이 옆 가장자리 가까이 있어도 나오자마자 이웃에게 넘어가지 않도록, 바닥 감속으로
        /// 계산한 옆 이동 거리가 그쪽 가장자리까지 남은 거리를 넘으면 그만큼만 속도를 줄인다.
        /// </summary>
        private Vector2 SpawnVelocityFor(uint drawSeed, ulong playerId, uint index, float originX)
        {
            float angle = Mathf.Lerp(-rules.SpawnFanAngle, rules.SpawnFanAngle,
                Unit01(DrawFor(drawSeed ^ SpawnAngleSalt, playerId, index))) * Mathf.Deg2Rad;
            float speed = Mathf.Lerp(rules.SpawnMinSpeed, rules.SpawnMaxSpeed,
                Unit01(DrawFor(drawSeed ^ SpawnSpeedSalt, playerId, index)));
            float sin = Mathf.Sin(angle);
            float side = Mathf.Abs(sin);
            if (rules.FloorDeceleration > 0f && side > .0001f)
            {
                float room = sin > 0f ? 1f - SpawnEdgeMargin - originX : originX - SpawnEdgeMargin;
                // 멈출 때까지 옆으로 가는 거리 = 속도² × sin / (2 × 감속)
                float safeSpeed = Mathf.Sqrt(2f * rules.FloorDeceleration * Mathf.Max(0f, room) / side);
                speed = Mathf.Min(speed, safeSpeed);
            }
            speed = Mathf.Min(speed, rules.MaximumMotionSpeed);
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;
        }

        private static float Unit01(ulong draw) => (float)((draw >> 11) * (1.0 / (1UL << 53)));
        private string NewOrbId()
        {
            string id;
            do id = Guid.NewGuid().ToString("N"); while (orbs.ContainsKey(id) || drums.ContainsKey(id));
            return id;
        }
        private static bool UsableBy(LabOrbState orb, ulong sender) => orb.owner == sender && !orb.inFlight;
        private static bool Reject(string value, out string reason) { reason = value; return false; }
        private static bool Unit(float value) => Finite(value) && value >= 0f && value <= 1f;
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static uint SeedFor(string id, uint round)
        {
            unchecked
            {
                uint hash = 2166136261u ^ round;
                foreach (char character in id) { hash ^= character; hash *= 16777619u; }
                return hash;
            }
        }

        /// <summary>
        /// 결합 구슬의 오행 속성을 정한다. 다섯 속성이 같은 확률로 나온다.
        /// Host만 호출하므로 모든 기기가 스냅샷으로 같은 결과를 받는다.
        /// 규칙을 바꿀 때는 이 함수만 고치면 된다.
        /// </summary>
        private static LabOrbElement ElementFor(uint seed, uint combinedIndex)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ 0xE1E3E7A5UL;
                value += 0x9E3779B97F4A7C15UL * (combinedIndex + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (LabOrbElement)(1 + (int)(value % 5UL));
            }
        }

        // Counter-based Host draw: one player's accepted generation cannot change another's
        // sequence. Each draw is independent, so results follow the configured percentages.
        private static LabOrbKind GeneratedKindFor(uint seed, ulong playerId, uint index, int[] chances)
        {
            int total = 0;
            foreach (int chance in chances) total += chance;
            int roll = (int)(DrawFor(seed, playerId, index) % (ulong)total);
            for (int i = 0; i < chances.Length; i++)
            {
                roll -= chances[i];
                if (roll < 0) return LabConfig.GeneratedKinds[i];
            }
            return LabOrbKind.Yin;
        }

        private int LockSwipesFor(uint seed, ulong playerId, uint index)
        {
            int span = rules.ItemUnlockSwipesMax - rules.ItemUnlockSwipesMin + 1;
            return rules.ItemUnlockSwipesMin + (int)(DrawFor(seed ^ LockDrawSalt, playerId, index) % (ulong)span);
        }

        private static ulong DrawFor(uint seed, ulong playerId, uint index)
        {
            unchecked
            {
                ulong value = ((ulong)seed << 32) ^ playerId;
                value += 0x9E3779B97F4A7C15UL * (index + 1UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return value;
            }
        }

        private sealed class Rules
        {
            public readonly LabStageYokai StageYokai;
            /// <summary>2:2 땅따먹기 (#281): 피버에 필요한 팀 명중 수(0이면 피버 없음)와 피버 시간.</summary>
            public readonly int TeamFeverHits;
            public readonly float TeamFeverSeconds;
            public readonly int MonsterMaxHp, PlayerMaxHp, PotionHeal, Damage, OrbStorageLimit;
            public readonly float RelayDoubleMultiplier, RelayMaxMultiplier, BombMultiplier;
            /// <summary>How much a blast-shrunk orb paints, against a whole one (#213).</summary>
            public readonly float SmallOrbPaintShare;
            public readonly float BattleDurationSeconds, StaminaMax, StaminaStart, GenerateCost,
                StaminaRecoveryPerSecond, HitRecovery, OrbLifetimeSeconds, MaximumMotionSpeed, ItemLifetimeSeconds;
            public readonly float SpawnFanAngle, SpawnMinSpeed, SpawnMaxSpeed, FloorDeceleration;
            public readonly int[] GenerationChances;
            public readonly bool RawStreakCorrectionEnabled;
            public readonly int RawStreakOppositeChancePercent, RawStreakMaximum;
            public readonly int ItemUnlockSwipesMin, ItemUnlockSwipesMax, DrumCount;
            public readonly LabGourdMode GourdMode;
            public readonly int PaintVictoryPercent;
            public readonly LabPaintGrowthSettings PaintGrowthSettings;
            /// <summary>The gourd's swipe block (#215): how long it lasts.</summary>
            public readonly float SwipeBlockSeconds;
            /// <summary>
            /// Gimmick groups (#311): the common gimmicks (gourd bomb, swipe block) and this yokai's own (the nine-tailed fox:
            /// fox fire, charm), each group cast one at a time every its interval; which may be drawn and each one's percent
            /// chance within its group, in <see cref="LabSabotageKind"/> order.
            /// </summary>
            public readonly float CommonGimmickInterval, UniqueGimmickInterval;
            public readonly LabSabotageKind[] CommonGimmicks, UniqueGimmicks;
            public readonly bool[] GimmickEnabled;
            public readonly int[] GimmickChances;
            public readonly bool YaguangTimeline;
            public readonly double YaguangTimelineEndSeconds;
            public readonly double[] YaguangTimelineCommonSeconds;
            public readonly YaguangUniqueSlot[] YaguangTimelineUniqueSchedule;
            /// <summary>Fox fire (#251): how many fox orbs fall, and how long after they fall they cannot be touched off.</summary>
            public readonly int FoxFireCountMin, FoxFireCountMax;
            public readonly float FoxFireSafeSeconds;
            /// <summary>
            /// Charm (#251): how long it lasts, and how many taps each player beside must give to wake the charmed one
            /// (#305); the warning before it starts.
            /// </summary>
            public readonly float CharmSeconds, CharmWarningSeconds;
            public readonly int CharmRescueTaps;
            public readonly float[] DrumStopBounds;
            public readonly int[] DrumStopChances;
            public readonly Color[] PaintColors;
            public readonly bool ComboEnabled, ComboPenaltyThrowerOnly;
            public readonly float ComboFirstStartSeconds, ComboRepeatIntervalSeconds, ComboTimeLimitSeconds,
                ComboPenaltyDurationSeconds, ComboPenaltySizeScale, ComboRewardDurationSeconds, ComboRewardSizeScale,
                ComboWarningSeconds;
            /// <summary>Yaguang flash (#268): on/off, everyone or one at a time, and its timing.</summary>
            public readonly bool FlashEnabled, FlashTargetOne;
            public readonly float FlashFirstStartSeconds, FlashIntervalSeconds, FlashWarningSeconds,
                FlashGlareSeconds, FlashRecoverySeconds;
            /// <summary>Yaguang flash release (#301): a dazzled player shakes their phone to clear their own glare.</summary>
            public readonly bool FlashShakeRelease;

            public Rules(LabConfig config, LabStageYokai stageYokai, bool teamMode)
            {
                StageYokai = stageYokai;
                GenerationChances = config.GenerationChances();
                RawStreakCorrectionEnabled = config.RawStreakCorrectionEnabled;
                RawStreakOppositeChancePercent = config.RawStreakOppositeChancePercent;
                RawStreakMaximum = config.RawStreakMaximum;
                ItemLifetimeSeconds = config.ItemLifetimeSeconds;
                ItemUnlockSwipesMin = config.ItemUnlockSwipesMin;
                ItemUnlockSwipesMax = config.ItemUnlockSwipesMax;
                MonsterMaxHp = config.MonsterMaxHp;
                GourdMode = config.GourdMode;
                PaintVictoryPercent = config.PaintVictoryPercent;
                PaintGrowthSettings = config.PaintGrowthSettings;
                SwipeBlockSeconds = config.SwipeBlockSeconds;
                CommonGimmickInterval = config.CommonGimmickInterval;
                UniqueGimmickInterval = config.UniqueGimmickInterval;
                CommonGimmicks = new[] { LabSabotageKind.GourdBomb, LabSabotageKind.SwipeBlock };
                // Only the nine-tailed fox has its own gimmicks so far; the Lab's single scene borrows them when asked to.
                UniqueGimmicks = stageYokai == LabStageYokai.Gumiho
                    || (stageYokai == LabStageYokai.None && config.LabUniqueGimmicksEnabled)
                    ? new[] { LabSabotageKind.FoxFire, LabSabotageKind.Charm } : Array.Empty<LabSabotageKind>();
                GimmickEnabled = config.GimmickSwitches();
                GimmickChances = config.GimmickChances();
                FoxFireCountMin = config.FoxFireCountMin;
                FoxFireCountMax = config.FoxFireCountMax;
                FoxFireSafeSeconds = config.FoxFireSafeSeconds;
                CharmSeconds = config.CharmSeconds;
                CharmWarningSeconds = config.CharmWarningSeconds;
                CharmRescueTaps = config.CharmRescueTaps;
                DrumStopBounds = config.DrumStopBounds();
                DrumStopChances = config.DrumStopChances();
                PaintColors = new Color[(int)LabOrbElement.Earth + 1];
                for (int i = (int)LabOrbElement.Fire; i <= (int)LabOrbElement.Earth; i++)
                    PaintColors[i] = LabPaintPalette.ColorFor((LabOrbElement)i);
                PlayerMaxHp = config.PlayerMaxHp;
                PotionHeal = config.PotionHeal;
                RelayDoubleMultiplier = config.RelayDoubleMultiplier;
                RelayMaxMultiplier = config.RelayMaxMultiplier;
                BombMultiplier = config.BombMultiplier;
                SmallOrbPaintShare = config.SmallOrbPaintShare;
                Damage = config.Damage;
                OrbStorageLimit = config.OrbStorageLimit;
                BattleDurationSeconds = stageYokai == LabStageYokai.Jigwi && config.JigwiStageTimelineEnabled
                    ? 120f : config.BattleDurationSeconds;
                StaminaMax = config.StaminaMax;
                StaminaStart = config.StaminaStart;
                GenerateCost = config.GenerateCost;
                StaminaRecoveryPerSecond = config.StaminaRecoveryPerSecond;
                HitRecovery = config.HitRecovery;
                OrbLifetimeSeconds = config.OrbLifetimeSeconds;
                MaximumMotionSpeed = config.OrbMaxReleaseSpeed * 16f;
                SpawnFanAngle = config.SpawnFanAngle;
                SpawnMinSpeed = config.SpawnMinSpeed;
                SpawnMaxSpeed = config.SpawnMaxSpeed;
                FloorDeceleration = config.OrbFloorDeceleration;
                DrumCount = config.DrumCount;
                ComboEnabled = config.GourdColorComboEnabled
                    && (stageYokai == LabStageYokai.None || stageYokai == LabStageYokai.Yaguang);
                ComboFirstStartSeconds = config.ComboFirstStartSeconds;
                ComboRepeatIntervalSeconds = config.ComboRepeatIntervalSeconds;
                ComboTimeLimitSeconds = config.ComboTimeLimitSeconds;
                ComboPenaltyDurationSeconds = config.ComboPenaltyDurationSeconds;
                ComboPenaltySizeScale = config.ComboPenaltySizeScale;
                ComboPenaltyThrowerOnly = config.ComboPenaltyThrowerOnly;
                ComboRewardDurationSeconds = config.ComboRewardDurationSeconds;
                ComboRewardSizeScale = config.ComboRewardSizeScale;
                ComboWarningSeconds = config.ComboWarningSeconds;
                FlashEnabled = config.YaguangFlashEnabled
                    && (stageYokai == LabStageYokai.None || stageYokai == LabStageYokai.Yaguang);
                FlashTargetOne = config.YaguangFlashTargetOne;
                FlashFirstStartSeconds = config.YaguangFlashFirstStartSeconds;
                FlashIntervalSeconds = config.YaguangFlashIntervalSeconds;
                FlashWarningSeconds = config.YaguangFlashWarningSeconds;
                FlashGlareSeconds = config.YaguangFlashGlareSeconds;
                FlashRecoverySeconds = config.YaguangFlashRecoverySeconds;
                FlashShakeRelease = config.YaguangFlashShakeRelease;
                // One stage policy feeds the shared scheduler; there is no second timeline execution loop.
                YaguangTimeline = stageYokai == LabStageYokai.Yaguang && !teamMode;
                YaguangTimelineEndSeconds = YaguangTimeline ? config.YaguangTimelineSabotageEndSeconds : 0d;
                if (YaguangTimeline)
                {
                    BattleDurationSeconds = config.YaguangTimelineBattleSeconds;
                    var commons = new List<double>();
                    var unique = new List<YaguangUniqueSlot>();
                    for (int i = 0; i < LabConfig.YaguangTimelineSlots; i++)
                    {
                        double common = config.YaguangTimelineCommonSeconds(i);
                        if (common > 0d) commons.Add(common);
                        double seconds = config.YaguangTimelineUniqueSeconds(i);
                        bool isCombo = config.YaguangTimelineUniqueIsCombo(i);
                        if (seconds > 0d && (isCombo ? ComboEnabled : FlashEnabled))
                            unique.Add(new YaguangUniqueSlot(seconds, isCombo));
                    }
                    YaguangTimelineCommonSeconds = commons.OrderBy(value => value).ToArray();
                    YaguangTimelineUniqueSchedule = unique.OrderBy(value => value.Seconds)
                        .ThenBy(value => value.IsCombo ? 0 : 1).ToArray();
                }
                else
                {
                    YaguangTimelineCommonSeconds = Array.Empty<double>();
                    YaguangTimelineUniqueSchedule = Array.Empty<YaguangUniqueSlot>();
                }
                TeamFeverHits = teamMode ? config.TeamFeverHits : 0;
                TeamFeverSeconds = config.TeamFeverSeconds;
                if (!teamMode) return;
                // 2:2 땅따먹기 (#281): 요괴·방해공작·콤보·섬광·마르기를 끄고, 도색으로만 겨룬다.
                BattleDurationSeconds = config.TeamBattleSeconds;
                GourdMode = LabGourdMode.Paint;
                CommonGimmicks = UniqueGimmicks = Array.Empty<LabSabotageKind>();
                ComboEnabled = FlashEnabled = false;
                PaintGrowthSettings.DryingEnabled = false;
                // 덮어쓰기는 얼룩 모양이 있어야 하므로 옛 둥근 붓은 흩뿌림 무늬로 바꾼다.
                if (PaintGrowthSettings.Pattern == LabPaintPattern.Legacy) PaintGrowthSettings.Pattern = LabPaintPattern.Splatter;
            }
        }
    }
}
