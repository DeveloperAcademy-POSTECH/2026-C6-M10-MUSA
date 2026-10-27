using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 섬광 파훼 보상 (#329). 섬광을 흔들어 푼 사람이 끊기지 않고 이어서 최대 흔들기 횟수까지 흔들면, 그 기기가 Host에
    /// 한 번 알리고 Host가 그 사람 판에 황금알 구슬을 떨어뜨린다. 흔든 수와 끊김은 그 사람 기기가 세고, Host는 이번 섬광에서
    /// 푼(또는 지금 눈부신) 사람인지와 이번 섬광에 이미 받았는지만 확인한다.
    /// </summary>
    public sealed partial class LabModel
    {
        private const uint GoldenEggXSalt = 0x60D1E66Au, GoldenEggYSalt = 0x60D1E66Bu;
        private const ulong GoldenEggDrawKey = ulong.MaxValue - 8UL;
        /// <summary>황금알은 수명이 없다: 만든 시각부터 이만큼 뒤로 끝을 둔다 (사실상 판이 끝날 때까지).</summary>
        private const double GoldenEggLifetimeSeconds = 1e6d;

        /// <summary>마지막으로 확정한 명중이 황금알이었는지 (#329). 금빛 반짝이·진동을 알릴 때 쓴다.</summary>
        public bool LastHitGolden { get; private set; }

        /// <summary>
        /// 이번 섬광(generation)에서 끝까지 흔든 사람에게 황금알을 준다. 이미 받았거나, 이번 섬광을 맞지 않았거나,
        /// 보상이 꺼져 있으면 false. 판이 가득 찼으면 빈자리만큼만 떨어진다(0개여도 이번 섬광 보상은 쓴 것으로 친다).
        /// 아직 눈부신 채로 왔으면(판의 구슬이 많아 최대 횟수에서야 다 풀린 경우) 먼저 풀어 준다.
        /// </summary>
        public bool TryFlashGoldenBonus(ulong sender, uint generation, double now, out int dropped)
        {
            dropped = 0;
            if (!rules.FlashShakeRelease || rules.GoldenEggCount <= 0 || generation == 0
                || !TryAdvance(now, out _) || phase != LabPhase.Playing || generation != flashCount
                || !players.TryGetValue(sender, out LabPlayerState state) || state.flashBonusFlash == generation) return false;
            if (rules.FlashTargetOne && lastFlashTarget != sender) return false;
            bool released = state.flashReleasedAt >= lastFlashAt;
            if (!released && !FlashGlared(sender, now)) return false;
            if (!released) state.flashReleasedAt = now;
            state.flashBonusFlash = generation;
            int room = rules.OrbStorageLimit - CountStored(sender, now);
            int count = Mathf.Min(rules.GoldenEggCount, Mathf.Max(0, room));
            for (int i = 0; i < count; i++)
            {
                uint spot = generation * 16u + (uint)i;
                var egg = new LabOrbState
                {
                    id = NewOrbId(), owner = sender, kind = LabOrbKind.Combined, element = LabOrbElement.None,
                    golden = true, origin = LabOrbOrigin.Gourd, originOwner = sender,
                    x = Mathf.Lerp(FoxLandingMinX, FoxLandingMaxX, Unit01(DrawFor(seed ^ GoldenEggXSalt, GoldenEggDrawKey, spot))),
                    y = Mathf.Lerp(FoxLandingMinY, FoxLandingMaxY, Unit01(DrawFor(seed ^ GoldenEggYSalt, GoldenEggDrawKey, spot))),
                    createdAt = now, expiresAt = now + GoldenEggLifetimeSeconds, revision = ++revision, inFlight = false
                };
                orbs.Add(egg.id, egg);
            }
            dropped = count;
            return true;
        }
    }
}
