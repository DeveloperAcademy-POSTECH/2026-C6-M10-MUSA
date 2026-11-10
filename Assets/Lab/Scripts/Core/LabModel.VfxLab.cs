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
