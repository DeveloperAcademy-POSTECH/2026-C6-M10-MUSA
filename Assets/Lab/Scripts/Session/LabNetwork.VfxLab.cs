using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// VFX 녹화용 실험실 (#610): 에디터 창(Hapio/VFX 실험실)의 버튼이 부르는 곳. 폭탄 실험실(#584)처럼 Host이고 전투 중일 때만 되고,
    /// 부른 뒤 바로 스냅샷을 보내 화면이 실제 게임과 같은 길로 그린다. 이 창이 부르지 않으면 아무 일도 없다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        // Y5: 실험실이 피버를 당긴 판(session + round)과 그때의 전투 경과 초. 다른 판에는 쓰지 않는다.
        private double vfxLabYaguangFeverPlayed = -1d;
        private string vfxLabYaguangFeverSession;
        private uint vfxLabYaguangFeverRound;

        // #673: 혼자 Host일 때만. 다른 기기가 접속해 있으면 실험실은 아무것도 하지 않는다(실제 판에 끼어들지 않게).
        public bool VfxLabReady => IsHost && model != null && latest != null && latest.phase == LabPhase.Playing
            && order.Count <= 1;
        /// <summary>지금 게임 스테이지(구미호/야광귀/지귀). Lab 단판이면 None.</summary>
        public LabStageYokai VfxLabStage => gameFlowEnabled ? gameStageYokai : LabStageYokai.None;
        public bool VfxLabComboRunning => VfxLabReady && model.VfxLabComboRunning;
        public bool VfxLabComboActive => VfxLabReady && model.VfxLabComboActive;
        public bool VfxLabYaguangFeverReady => VfxLabReady && model.VfxLabYaguangTimelineOn;

        private double VfxLabNow => battleClock.Now(Time.realtimeSinceStartupAsDouble);

        /// <summary>Y2: 콤보 예고(COMBO 3/2/1)부터 바로 시작</summary>
        public bool VfxLabStartCombo() => VfxLabReady && VfxLabPublish(model.VfxLabStartCombo(VfxLabNow));

        /// <summary>Y2: 콤보가 다음에 원하는 색 구슬 하나를 내 판에 놓기</summary>
        public bool VfxLabGiveComboOrb(out LabOrbElement element)
        {
            element = LabOrbElement.None;
            return VfxLabReady && VfxLabPublish(model.VfxLabGiveComboOrb(LocalPlayerId, VfxLabNow, out element));
        }

        /// <summary>#719 Y2: 다음 칸 하나만 맞히기</summary>
        public bool VfxLabHitComboSlot() => VfxLabReady && VfxLabPublish(model.VfxLabHitComboSlot(LocalPlayerId, VfxLabNow));
        /// <summary>#719 Y2: 틀린 색이지만 첫 칸 전이라 봐주기</summary>
        public bool VfxLabForgiveWrongCombo() => VfxLabReady && VfxLabPublish(model.VfxLabForgiveWrongCombo(LocalPlayerId, VfxLabNow));
        /// <summary>#719 Y2: 콤보가 원하지 않는 색 구슬 받기</summary>
        public bool VfxLabGiveOtherColorOrb(out LabOrbElement element)
        {
            element = LabOrbElement.None;
            return VfxLabReady && VfxLabPublish(model.VfxLabGiveOtherColorOrb(LocalPlayerId, VfxLabNow, out element));
        }
        /// <summary>#696/#719 J: 검은 파편 4개 / 검정 구슬 받기</summary>
        public bool VfxLabGiveSootShards() => VfxLabReady && VfxLabPublish(model.VfxLabGiveSootShards(LocalPlayerId, VfxLabNow));
        public bool VfxLabGiveSootOrb() => VfxLabReady && VfxLabPublish(model.VfxLabGiveSootOrb(LocalPlayerId, VfxLabNow));

        /// <summary>#741 Y3: BIG / SMALL이 걸려 있는지, 그리고 지금 끝내기</summary>
        public bool VfxLabComboEffectOn => VfxLabReady && model.VfxLabComboEffectOn(VfxLabNow);
        public bool VfxLabEndComboEffect() => VfxLabReady && VfxLabPublish(model.VfxLabEndComboEffect(VfxLabNow));
        /// <summary>#741 Y1: 지금 내가 섬광에 눈부신지 (실험실 "섬광 바로 해제" 버튼이 켜지는 때)</summary>
        public bool VfxLabFlashGlared => VfxLabReady && config != null && latest != null
            && LabYaguangFlash.Glared(latest, BattlePresentationTime, LocalPlayerId, config.YaguangFlashGlareLimitSeconds);

        /// <summary>Y2/Y3: 남은 칸을 바로 맞혀 성공 (BIG 보상)</summary>
        public bool VfxLabCompleteCombo() => VfxLabReady && VfxLabPublish(model.VfxLabCompleteCombo(LocalPlayerId, VfxLabNow));

        /// <summary>Y4: 오색구슬을 내 판에 놓기. 놓은 수를 돌려준다.</summary>
        public int VfxLabGiveFiveColorOrbs(int count)
        {
            if (!VfxLabReady) return 0;
            int dropped = model.VfxLabGiveFiveColorOrbs(LocalPlayerId, count, VfxLabNow);
            VfxLabPublish(dropped > 0);
            return dropped;
        }

        /// <summary>Y5: 야광귀 피버 바로 시작. Host 규칙과 화면 시각(YaguangFeverElapsed)을 같은 순간으로 맞춘다.</summary>
        public bool VfxLabStartYaguangFever()
        {
            if (!VfxLabReady || VfxLabStage != LabStageYokai.Yaguang || !model.VfxLabStartYaguangFever(VfxLabNow)) return false;
            PublishSnapshot(true);
            vfxLabYaguangFeverPlayed = latest.playedSeconds;
            vfxLabYaguangFeverSession = latest.sessionId;
            vfxLabYaguangFeverRound = latest.round;
            PublishSnapshot(true);
            return true;
        }

        // ───────── #673: 1~3라운드 모든 기믹 ─────────
        public bool VfxLabJigwiOn => VfxLabReady && model.VfxLabJigwiOn;
        public LabJigwiABKind? VfxLabJigwiMission => VfxLabReady ? model.VfxLabJigwiMission(VfxLabNow) : null;

        /// <summary>공통/구미호 기믹(박 폭탄, 넘기기 막기, 여우불, 매혹)을 지금 나에게 건다.</summary>
        public bool VfxLabCastGimmick(LabSabotageKind kind) =>
            VfxLabReady && VfxLabPublish(model.VfxLabCastGimmick(kind, LocalPlayerId, VfxLabNow));
        public bool VfxLabStartFlash() => VfxLabReady && VfxLabPublish(model.VfxLabStartFlash(VfxLabNow));
        public bool VfxLabFailCombo() => VfxLabReady && VfxLabPublish(model.VfxLabFailCombo(LocalPlayerId, VfxLabNow));
        public bool VfxLabStartGumihoFever() => VfxLabReady && VfxLabStage == LabStageYokai.Gumiho
            && VfxLabPublish(model.VfxLabStartGumihoFever(VfxLabNow));
        public bool VfxLabStartNextJigwiMission() => VfxLabReady && VfxLabPublish(model.VfxLabStartNextJigwiMission(VfxLabNow));
        public bool VfxLabFinishJigwiMission(bool complete) => VfxLabReady && VfxLabPublish(model.VfxLabFinishJigwiMission(complete, VfxLabNow));
        public bool VfxLabStartJigwiFever() => VfxLabReady && VfxLabStage == LabStageYokai.Jigwi
            && VfxLabPublish(model.VfxLabStartJigwiFever(VfxLabNow));

        /// <summary>실험실이 이 판의 야광귀 피버를 당겼으면 그 전투 경과 초, 아니면 -1 (YaguangFeverElapsed가 본다).</summary>
        private double VfxLabYaguangFeverPlayedFor(LabSnapshot snapshot) =>
            snapshot != null && vfxLabYaguangFeverPlayed >= 0d && snapshot.round == vfxLabYaguangFeverRound
                && snapshot.sessionId == vfxLabYaguangFeverSession
                ? vfxLabYaguangFeverPlayed : -1d;

        private bool VfxLabPublish(bool changed)
        {
            if (changed) PublishSnapshot(true);
            return changed;
        }
    }
}
