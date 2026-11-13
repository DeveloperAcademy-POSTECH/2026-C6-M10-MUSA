using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 야광귀 섬광 해제 (#301): 눈부신 사람이 폰을 정해진 횟수만큼 흔들면 그 기기가 Host에 한 번 알린다.
    /// 흔든 횟수는 그 사람 기기가 세고(하얀 막이 조금씩 걷히는 것도 그 화면에서만), 해제 여부는 Host가
    /// `LabModel.TryFlashShakeRelease`로 확정해 스냅샷의 `flashReleasedAt`으로 모두에게 알린다.
    /// </summary>
    public sealed partial class LabNetwork
    {
        /// <summary>흔들어 풀기 요청 패킷 종류</summary>
        public const string FlashShakePacket = "flashShake";

        /// <summary>이 기기 플레이어가 폰을 다 흔들었다고 Host에 알린다. 전투 중이 아니면 보내지 않는다.</summary>
        public bool RequestFlashShakeRelease()
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null
                || latest.phase != LabPhase.Playing || latest.paused || latest.flashCount == 0) return false;
            Request(new LabNetworkPacket { type = FlashShakePacket, requestId = ++requestSequence,
                flashGeneration = latest.flashCount });
            return true;
        }

        /// <summary>섬광 파훼 보상 요청 패킷 종류 (#329)</summary>
        public const string FlashBonusPacket = "flashBonus";

        /// <summary>
        /// 이 기기 플레이어가 풀린 뒤에도 끊기지 않고 최대 횟수까지 흔들었다고 Host에 알린다 (#329). Host가 이번 섬광에서
        /// 한 번만 오색구슬을 준다. 전투 중이 아니면 보내지 않는다.
        /// </summary>
        public bool RequestFlashFiveColorBonus()
        {
            if (!IsConnected || !CanProcessBattleActions || latest == null
                || latest.phase != LabPhase.Playing || latest.paused || latest.flashCount == 0) return false;
            Request(new LabNetworkPacket { type = FlashBonusPacket, requestId = ++requestSequence,
                flashGeneration = latest.flashCount });
            return true;
        }

        /// <summary>내 판에 오색구슬이 새로 생겼다 (#329). 받은 개수. 진동·소리가 듣는다.</summary>
        public event System.Action<int> FiveColorOrbsReceived;
        /// <summary>#719: flash-reward five-colour orbs not yet drawn on my board; they pop in (grow + five-colour stars).</summary>
        private readonly System.Collections.Generic.HashSet<string> fiveColorAppearing =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        /// <summary>#719: how long a flash-reward five-colour orb takes to grow in on its spot.</summary>
        public const float FiveColorAppearSeconds = .5f;

        /// <summary>
        /// #719 섬광 보상 오색구슬이 생기는 모습: 제자리에서 점처럼 작게 시작해 커지며 나타나고, 그 자리에서 다섯 색 별가루가 퍼진다.
        /// 몸(물리)은 처음부터 제자리·제 크기이고 그림만 커진다.
        /// </summary>
        private void PresentFiveColorAppear(LabOrbView view)
        {
            if (view == null || !view.isActiveAndEnabled) return;
            view.PlaySpawnPop(Vector3.zero, 0f, FiveColorAppearSeconds);
            float radius = config != null ? config.OrbRadius : .3f;
            LabFiveColorSparkle burst = LabFiveColorSparkle.Burst(view.transform.position, radius * 1.3f);
            if (burst == null) return;
            int layer = view.gameObject.layer;
            foreach (Transform part in burst.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        }

        /// <summary>내가 던진 오색구슬이 박에 맞았다 (#329)</summary>
        public event System.Action LocalFiveColorHit;
        private readonly System.Collections.Generic.HashSet<string> knownFiveColorOrbs =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>스냅샷의 내 판에서 처음 보는 오색구슬 수를 세어, 있으면 한 번 알린다.</summary>
        private void NoticeFiveColorOrbs(LabSnapshot snapshot)
        {
            if (snapshot?.orbs == null) return;
            int fresh = 0;
            foreach (LabOrbState orb in snapshot.orbs)
                if (orb != null && orb.fiveColor && orb.owner == LocalPlayerId && knownFiveColorOrbs.Add(orb.id)
                    && orb.originOwner == LocalPlayerId && snapshot.serverTime - orb.createdAt < 2d)
                {
                    fresh++;
                    if (orb.origin == LabOrbOrigin.Gourd) fiveColorAppearing.Add(orb.id); // #719 the flash reward pops in
                }
            if (fresh > 0) FiveColorOrbsReceived?.Invoke(fresh);
        }

        /// <summary>오색구슬 명중: 공통 클레이 접촉은 기존 피격 경로가 그리고, 던진 사람에게 소리·진동을 알린다.</summary>
        private void PresentFiveColorHit(Vector3 targetLocalPoint, ulong owner, bool soot = false)
        {
            if (target == null) return;
            if (soot) PresentSootHit(targetLocalPoint);
            // #713: the black orb has its own hit sound (every device), so the five-colour gayageum stays quiet for it.
            LastFiveColorHitSoot = soot;
            if (owner == LocalPlayerId) LocalFiveColorHit?.Invoke();
        }

        /// <summary>#713: the last five-colour hit presented was the Jigwi black orb (its own sound plays instead).</summary>
        public bool LastFiveColorHitSoot { get; private set; }
        /// <summary>#713 검정 구슬이 박에 맞았다 (모든 기기). 전용 명중음이 듣는다.</summary>
        public event System.Action SootHit;
        /// <summary>#713 연기를 닦다가 밑에 깔린 파편이 하나 이상 드러났다 (내 기기). 드러난 수를 넘긴다.</summary>
        public event System.Action<int> SootShardRevealed;
        public void NotifySootShardRevealed(int count) { if (count > 0) SootShardRevealed?.Invoke(count); }

        /// <summary>
        /// #696 검정 구슬 명중: 박에는 오색구슬만큼 넓게 먹색이 칠해지고(모델),
        /// #713부터 맞은 자리에서 판 위 기운과 같은 검정 기운(먹빛 연기가 확 퍼지고 보랏빛 빛무리가 번진 뒤 연기가 잠시 감돎)이 터진다.
        /// 모든 기기에서 보인다.
        /// </summary>
        private void PresentSootHit(Vector3 targetLocalPoint)
        {
            Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
            Vector3 point = hitbox.TransformPoint(targetLocalPoint);
            float radius = config != null ? config.OrbRadius : .3f;
            LabSootAura.PlayHit(point, radius * LabOrbSpriteArt.SizeScaleFor(LabOrbKind.Combined) * LabModel.SootOrbSizeScale,
                target.gameObject.layer);
            SootHit?.Invoke();
        }

        /// <summary>#540 지귀 부적 구슬 명중 이벤트 (맞은 자리 박 기준 좌표, 던진 사람). 소리·진동이 듣는다.</summary>
        public event System.Action<ulong> TalismanHit;

        /// <summary>
        /// #540 부적 구슬 명중: 맞은 자리에 붉은 도장이 찍힌 부적이 "철썩" 붙고, 푸른 빛 가루와 물방울 별이 함께 터진다.
        /// 모든 기기에서 보인다.
        /// </summary>
        private void PresentTalismanHit(Vector3 targetLocalPoint, ulong owner)
        {
            if (target == null) return;
            Transform hitbox = target.Hitbox != null ? target.Hitbox.transform : target.transform;
            Vector3 point = hitbox.TransformPoint(targetLocalPoint);
            float radius = config != null ? config.OrbRadius : .3f;
            LabFiveColorSparkle.DustBurst(point, radius, LabFiveColorSparkle.DefaultDustPuffScale * 1.3f, LabFiveColorSparkle.BlueTones);
            LabFiveColorSparkle.Burst(point, radius, LabFiveColorSparkle.BlueTones);
            // #540: a big splash of water bursts out from the gourd where it hit.
            if (config != null) LabJigwiWaterVolley.Splash(point, point - hitbox.position,
                config.ThrowRadius * LabJigwiWaterVolley.SizeScale, target.gameObject.layer);
            TalismanHit?.Invoke(owner);
        }
    }
}
