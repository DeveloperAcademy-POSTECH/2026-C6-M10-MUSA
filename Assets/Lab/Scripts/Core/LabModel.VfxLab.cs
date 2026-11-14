using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// VFX 녹화용 실험실 전용 (#610). 에디터 창(Hapio/VFX 실험실)이 혼자 Host로 하는 전투에서 녹화하기 어려운 장면을 바로 띄운다.
    /// 게임 설정(LabConfig)과 시간표는 바꾸지 않고, 지금 판의 상태만 실제 게임과 같은 길로 움직인다. 이 창이 부르지 않으면 아무 일도 없다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>야광귀 콤보 4칸이 지금 진행 중인지</summary>
        public bool VfxLabComboActive => combo.Active;
        /// <summary>야광귀 콤보 예고(COMBO 3/2/1)나 4칸이 진행 중인지</summary>
        public bool VfxLabComboRunning => comboWarned || combo.Active;
        /// <summary>야광귀 시간표가 돌고 있어(피버 전) 콤보/피버를 띄울 수 있는지</summary>
        public bool VfxLabYaguangTimelineOn => rules != null && rules.YaguangTimeline && !yaguangTimelineEnded;

        /// <summary>#741 공통: 이 사람 판에 있는 잠긴 박 폭탄(가장 먼저 생긴 것). 없으면 null.</summary>
        public string VfxLabLockedBombId(ulong owner) => orbs.Values
            .Where(orb => orb.owner == owner && orb.kind == LabOrbKind.Bomb && !orb.inFlight && orb.lockSwipes > 0)
            .OrderBy(orb => orb.createdAt).ThenBy(orb => orb.id, System.StringComparer.Ordinal).Select(orb => orb.id).FirstOrDefault();

        /// <summary>
        /// #741 공통: 내 판의 잠긴 박 폭탄을 도화선 끝까지 기다리지 않고 지금 터뜨린다. 도화선이 다 탄 것처럼 시각만 당기므로
        /// 폭발 연출과 소리는 실제 게임과 같은 길로 나온다 (아트팀 폭탄 실험실과 같은 방법). 폭탄이 없으면 false.
        /// </summary>
        public bool VfxLabDetonateBomb(ulong owner, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing) return false;
            string id = VfxLabLockedBombId(owner);
            return id != null && ArtLabSetBombFuse(id, now, .05f);
        }

        /// <summary>#741 Y3: 지금 누군가에게 BIG 보상이나 SMALL 벌칙이 걸려 있는지</summary>
        public bool VfxLabComboEffectOn(double now) => players.Values.Any(p => p.comboRewardUntil > now || p.comboPenaltyUntil > now);

        /// <summary>
        /// #741 Y3: 걸려 있는 BIG 보상 / SMALL 벌칙을 지금 끝낸다. 15초를 기다린 것처럼 끝나는 시각만 당기므로 끝 연출과 소리는
        /// 실제 게임과 같은 길로 나온다. 걸려 있는 것이 없으면 false.
        /// </summary>
        public bool VfxLabEndComboEffect(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing) return false;
            bool changed = false;
            foreach (LabPlayerState player in players.Values)
            {
                if (player.comboRewardUntil > now) { player.comboRewardUntil = now; changed = true; }
                if (player.comboPenaltyUntil > now) { player.comboPenaltyUntil = now; changed = true; }
            }
            if (changed) revision++;
            return changed;
        }

        /// <summary>
        /// Y2(콤보): 지금 바로 콤보 예고(COMBO 3/2/1)를 시작한다. 시간표가 콤보 차례를 꺼낼 때와 같은 길이다.
        /// 다른 콤보/섬광이 진행 중이거나 그 보상/벌칙이 남아 있으면 false.
        /// </summary>
        public bool VfxLabStartCombo(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !VfxLabYaguangTimelineOn || !rules.ComboEnabled
                || YaguangUniqueBusy(now)) return false;
            nextComboAt = now + rules.ComboWarningSeconds;
            comboWarned = true;
            comboHeld = comboWasHeld = false;
            comboQueuedAt = -1d;
            AdvanceCombo(now);
            revision++;
            return true;
        }

        /// <summary>
        /// Y2(콤보): 콤보가 다음에 원하는 색의 결합 구슬 하나를 내 판에 놓는다. 직접 던져 맞히면 실제 게임처럼 칸이 체크된다.
        /// 판이 가득 찼거나 4칸이 진행 중이 아니면 false.
        /// </summary>
        public bool VfxLabGiveComboOrb(ulong owner, double now, out LabOrbElement element)
        {
            element = LabOrbElement.None;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active || !players.ContainsKey(owner)
                || CountStored(owner, now) >= rules.OrbStorageLimit) return false;
            element = combo.ColorAt(combo.Progress);
            AddVfxLabOrb(owner, element, false, now);
            return true;
        }

        /// <summary>#719 Y2: 다음 칸 하나만 이 사람이 맞힌 것으로 처리한다 (도장, 강조가 다음 칸으로 옮겨 가는 것 확인). 마지막 칸이면 성공.</summary>
        public bool VfxLabHitComboSlot(ulong thrower, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active) return false;
            LabComboResult result = combo.Judge(combo.ColorAt(combo.Progress), thrower, now);
            if (result != LabComboResult.None) OnComboFinished(result);
            revision++;
            return true;
        }

        /// <summary>
        /// #719 Y2: 지금 칸에 틀린 색을 맞힌 것으로 처리하되, 첫 칸 전이면 실제 게임처럼 봐준다(실패 없이 금옥 "다시 해 보자").
        /// 봐줬으면 true, 첫 칸을 이미 맞혀 봐줄 수 없으면 false (그때는 실패 버튼을 쓴다).
        /// </summary>
        public bool VfxLabForgiveWrongCombo(ulong thrower, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active || !combo.Forgiving) return false;
            LabOrbElement wanted = combo.ColorAt(combo.Progress);
            LabOrbElement wrong = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth }
                .First(element => element != wanted);
            uint before = combo.ForgivenCount;
            LabComboResult result = combo.Judge(wrong, thrower, now);
            if (result != LabComboResult.None) OnComboFinished(result);
            revision++;
            return combo.ForgivenCount > before;
        }

        /// <summary>#719 Y2: 콤보가 지금 원하지 않는 색의 결합 구슬 하나를 놓는다 (빛 테두리가 맞는 색에만 붙는지 확인).</summary>
        public bool VfxLabGiveOtherColorOrb(ulong owner, double now, out LabOrbElement element)
        {
            element = LabOrbElement.None;
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active || !players.ContainsKey(owner)
                || CountStored(owner, now) >= rules.OrbStorageLimit) return false;
            LabOrbElement wanted = combo.ColorAt(combo.Progress);
            element = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth }
                .Where(value => value != wanted).ElementAt(Random.Range(0, 4));
            AddVfxLabOrb(owner, element, false, now);
            return true;
        }

        /// <summary>#696/#719 J: 연기 없이 검은 파편 4개(북/서/남/동)를 내 판 가장자리에 놓는다. 판이 가득해도 놓는다.</summary>
        public bool VfxLabGiveSootShards(ulong owner, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !players.ContainsKey(owner)) return false;
            for (int i = 0; i < JigwiSootShardCount; i++)
            {
                Vector2 spot = JigwiSootShardSpot(i);
                var shard = new LabOrbState
                {
                    id = NewOrbId(), owner = owner, kind = LabOrbKind.SootShard, element = LabOrbElement.None, sootPieces = 1 << i,
                    origin = LabOrbOrigin.Gourd, originOwner = owner, x = spot.x, y = spot.y,
                    createdAt = now, expiresAt = now + FiveColorOrbLifetimeSeconds, revision = ++revision
                };
                orbs.Add(shard.id, shard);
            }
            revision++;
            return true;
        }

        /// <summary>#696/#719 J: 다 합쳐진 검정 구슬 하나를 내 판에 놓는다 (기운 / 던지기 / 명중 효과 확인).
        /// #724: 연기를 닦았을 때처럼 판 가운데에 주므로, 파편 4개가 모여드는 모습(LabSootSocket)도 같이 확인된다.</summary>
        public bool VfxLabGiveSootOrb(ulong owner, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !players.ContainsKey(owner)
                || CountStored(owner, now) >= rules.OrbStorageLimit) return false;
            var orb = new LabOrbState
            {
                id = NewOrbId(), owner = owner, kind = LabOrbKind.Combined, element = LabOrbElement.None, soot = true, fiveColor = true,
                origin = LabOrbOrigin.Gourd, originOwner = owner, x = SootSocketCenter.x, y = SootSocketCenter.y,
                createdAt = now, expiresAt = now + FiveColorOrbLifetimeSeconds, revision = ++revision
            };
            orbs.Add(orb.id, orb);
            revision++;
            return true;
        }

        /// <summary>Y2/Y3: 남은 칸을 이 사람이 차례로 맞힌 것으로 처리해 콤보를 성공시킨다. 성공 보상(BIG)이 실제 게임처럼 걸린다.</summary>
        public bool VfxLabCompleteCombo(ulong thrower, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active) return false;
            LabComboResult result = LabComboResult.None;
            while (combo.Active && result == LabComboResult.None)
                result = combo.Judge(combo.ColorAt(combo.Progress), thrower, now);
            if (result != LabComboResult.None) OnComboFinished(result);
            revision++;
            return result == LabComboResult.Success;
        }

        /// <summary>Y4(오색구슬): 섬광 흔들기 없이 오색구슬을 내 판에 놓는다. 빈자리만큼만 놓고 놓은 수를 돌려준다.</summary>
        public int VfxLabGiveFiveColorOrbs(ulong owner, int count, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !players.ContainsKey(owner)) return 0;
            int room = Mathf.Max(0, rules.OrbStorageLimit - CountStored(owner, now));
            int dropped = Mathf.Min(Mathf.Max(0, count), room);
            for (int i = 0; i < dropped; i++) AddVfxLabOrb(owner, LabOrbElement.None, true, now);
            return dropped;
        }

        /// <summary>
        /// Y5(야광귀 피버): 시간표 방해가 끝나는 때(기본 100초)를 지금으로 당겨 피버를 바로 시작한다.
        /// 걸려 있던 방해를 푸는 것까지 100초에 하는 일과 같다. 화면 쪽 시각은 LabNetwork가 같은 순간으로 맞춘다.
        /// </summary>
        public bool VfxLabStartYaguangFever(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !VfxLabYaguangTimelineOn
                || rules.YaguangTimelineEndSeconds <= 0d) return false;
            yaguangTimelineEndAt = now;
            EndYaguangTimeline(now);
            revision++;
            return yaguangTimelineEnded;
        }

        // ───────── #673: 1~3라운드 모든 기믹 ─────────

        /// <summary>
        /// #673 공통/구미호 기믹을 지금 바로 건다 (주기·확률·스테이지 켜기와 상관없이). 박 폭탄·넘기기 막기·매혹은 이 사람에게,
        /// 여우불은 모두의 판에 떨어진다. 같은 것이 이미 걸려 있으면(넘기기 막기, 매혹) false.
        /// </summary>
        public bool VfxLabCastGimmick(LabSabotageKind kind, ulong target, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !players.ContainsKey(target)) return false;
            if (kind == LabSabotageKind.SwipeBlock && BlockOn(now)) return false;
            if (kind == LabSabotageKind.Charm && CharmComingOrOn(now)) return false;
            uint index = commonGimmickCount + uniqueGimmickCount + 1000u + (uint)sabotages.Count;
            if (kind == LabSabotageKind.FoxFire)
                DropFoxFireOnAll(seats.Take(activeSeatCount).ToList(), now, index);
            else DispatchGimmick(kind, target, now, index);
            revision++;
            return true;
        }

        /// <summary>#673 Y1(섬광): 지금 바로 섬광 예고를 시작한다. 시간표가 섬광 차례를 꺼낼 때와 같은 길이다.</summary>
        public bool VfxLabStartFlash(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !VfxLabYaguangTimelineOn || !rules.FlashEnabled
                || YaguangUniqueBusy(now)) return false;
            nextFlashAt = now + rules.FlashWarningSeconds;
            SettleFlashTarget(now);
            flashWarned = true;
            flashHeld = flashWasHeld = false;
            flashQueuedAt = -1d;
            AdvanceFlash(now);
            revision++;
            return true;
        }

        /// <summary>#673 Y2(콤보 실패): 지금 칸에 틀린 색을 맞힌 것으로 처리해 콤보를 실패시킨다. SMALL 벌칙이 실제 게임처럼 걸린다.</summary>
        public bool VfxLabFailCombo(ulong thrower, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !combo.Active) return false;
            combo.EndForgiving(); // #695: the lab's fail button fails even the round's first combo
            LabOrbElement wanted = combo.ColorAt(combo.Progress);
            LabOrbElement wrong = new[] { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth }
                .First(element => element != wanted);
            LabComboResult result = combo.Judge(wrong, thrower, now);
            if (result != LabComboResult.None) OnComboFinished(result);
            revision++;
            return result == LabComboResult.WrongColor;
        }

        /// <summary>#673 G(구미호 피버): 남은 시간이 피버 시간이 된 때를 지금으로 당겨 피버를 바로 시작한다.</summary>
        public bool VfxLabStartGumihoFever(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || rules.FeverTimeSeconds <= 0d || feverTimeStarted) return false;
            feverTimeAt = now;
            StartFeverTime(now);
            revision++;
            return feverTimeStarted;
        }

        /// <summary>#673 지귀 시간표가 돌고 있어(피버 전) 미션을 부를 수 있는지</summary>
        public bool VfxLabJigwiOn => jigwiABEnabled && !jigwiStageCutoffReached;
        /// <summary>#673 지금 진행 중인(예고 포함) 지귀 미션 종류. 없으면 null.</summary>
        public LabJigwiABKind? VfxLabJigwiMission(double now)
        {
            var item = jigwiABRounds.LastOrDefault(value => value.resolution == LabJigwiABResolution.None && now >= value.warningAt);
            return item?.kind;
        }

        /// <summary>
        /// #673 J(지귀): 시간표의 다음 미션(연기 또는 부적)을 지금 예고부터 시작한다. 진행 중인 미션이 있거나 남은 미션이 없으면 false.
        /// </summary>
        public bool VfxLabStartNextJigwiMission(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !VfxLabJigwiOn
                || jigwiStageNextSlot >= LabJigwiABTimeline.SlotCountFor(jigwiSchedule)
                || jigwiABRounds.Any(value => value.resolution == LabJigwiABResolution.None)) return false;
            jigwiNextSlotPulledAt = now + LabJigwiABTimeline.WarningSeconds;
            AdvanceJigwiAB(now);
            revision++;
            return true;
        }

        /// <summary>
        /// #673 J(지귀): 진행 중인 미션을 지금 끝낸다. complete면 성공(부적은 모든 자리가 다 쓴 것으로 → 완성 연출/보상),
        /// 아니면 시간이 다 된 것으로(부적은 실패 → 불덩이와 판 타기, 연기는 그냥 끝남).
        /// </summary>
        public bool VfxLabFinishJigwiMission(bool complete, double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing) return false;
            var active = ActiveJigwiABRound(now);
            if (active == null) return false;
            if (complete && active.kind == LabJigwiABKind.PersonalBurn)
            {
                foreach (var seat in active.players.Where(value => value.talismanAt < 0d).ToArray())
                    TryCompleteJigwiTalisman(seat.playerId, active.id, now);
            }
            else ResolveJigwiAB(active, complete ? LabJigwiABResolution.Completed : LabJigwiABResolution.Expired, now);
            revision++;
            return active.resolution != LabJigwiABResolution.None;
        }

        /// <summary>
        /// #673 J(지귀 피버): 방해가 끝나는 때(100초)를 지금으로 당겨 피버를 바로 시작한다. 진행 중이던 미션은 지금 끝난 것으로 잘린다.
        /// </summary>
        public bool VfxLabStartJigwiFever(double now)
        {
            if (!TryAdvance(now, out _) || phase != LabPhase.Playing || !VfxLabJigwiOn) return false;
            jigwiStageCutoffAt = now;
            // A mission still in its warning would start after the new cutoff: drop it (the timetable is over anyway).
            jigwiABRounds.RemoveAll(value => value.activeAt > now);
            foreach (var item in jigwiABRounds) if (item.endsAt > now) item.endsAt = now;
            AdvanceJigwiAB(now);
            revision++;
            return jigwiStageCutoffReached;
        }

        private void AddVfxLabOrb(ulong owner, LabOrbElement element, bool fiveColor, double now)
        {
            var orb = new LabOrbState
            {
                id = NewOrbId(), owner = owner, kind = LabOrbKind.Combined, element = element, fiveColor = fiveColor,
                origin = fiveColor ? LabOrbOrigin.Gourd : LabOrbOrigin.Combined, originOwner = owner,
                x = Random.Range(FoxLandingMinX, FoxLandingMaxX), y = Random.Range(FoxLandingMinY, FoxLandingMaxY),
                vx = 0f, vy = 0f, createdAt = now, revision = ++revision, inFlight = false
            };
            // 오색구슬은 섬광 보상처럼 수명이 없다. 색 구슬은 결합 구슬과 같은 수명이다.
            if (fiveColor) orb.expiresAt = now + FiveColorOrbLifetimeSeconds;
            orbs.Add(orb.id, orb);
        }
    }
}
