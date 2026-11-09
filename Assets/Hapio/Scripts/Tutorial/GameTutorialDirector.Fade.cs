using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 4단계 구슬 사라짐 시연 (#552). 튜토리얼은 구슬 수명을 꺼 두지만(하루), 이 장면에서만 남은 구슬 하나의 사라질 시각을
    /// 당겨 실제 게임과 똑같이 보여 준다: 마지막 몇 초의 흰 반짝임(#235)이 점점 빨라지다 연기와 함께 사라진다.
    /// - "어? 구슬이 빛나는데?" (OrbGlow): 내 판의 음 → 양 → 오행 구슬 순으로 하나를 고른다. 없으면 음구슬을 하나 떨어뜨린다.
    ///   이 대사가 떠 있는 동안은 남은 시간을 반짝임이 막 시작하는 값으로 계속 되돌려, 처음 빠르기로 빛나기만 한다.
    /// - "…빛이 나기 시작하면 곧 없어져!" (OrbVanish): 되돌리기를 멈추고 그 값부터 흐르게 둔다. 구슬이 사라지기 전에는
    ///   이 대사를 넘길 수 없다(▽도 사라진 뒤에 뜬다).
    /// 두 대사 동안 그 구슬만 밝히고, 사라진 뒤에도 같은 자리를 밝혀 둔다.
    /// </summary>
    public sealed partial class GameTutorialDirector
    {
        /// <summary>How often the glow's time left is wound back while "어? 구슬이 빛나는데?" is up (real seconds).</summary>
        private const float FadeHoldRefreshSeconds = .2f;
        /// <summary>If the orb has not gone this long after its last seconds began, the line lets go anyway.</summary>
        private const float FadeGiveUpSeconds = 3f;

        private string fadeOrbId;
        private bool fadeHolding;
        private float fadeRefreshAt = -1f, fadeVanishAt = -1f, fadeGoneAt = -1f;
        private bool fadeLitKnown;
        private Vector2 fadeLitCenter;
        private float fadeLitRadius;

        /// <summary>The orb the tutorial lets run out (4단계, #552); null before the demo or when none could be found.</summary>
        public string FadeDemoOrbId => fadeOrbId;
        /// <summary>The demo orb has run out and gone from my board.</summary>
        public bool FadeDemoGone => fadeGoneAt >= 0f;

        /// <summary>The seconds of white flashing before an orb runs out (#235); the demo keeps this much left while it glows.</summary>
        private float FadeWarningSeconds
        {
            get
            {
                LabOrbAppearance look = session.Network != null && session.Network.config != null
                    ? session.Network.config.OrbAppearance : null;
                float seconds = (look != null ? look : LabOrbAppearance.Defaults).ExpiryWarningSeconds;
                return seconds > 0f ? seconds : 3f;
            }
        }

        /// <summary>"어? 구슬이 빛나는데?": pick (or drop) the orb and let it glow at the first flashes.</summary>
        private void BeginOrbGlow()
        {
            fadeGoneAt = fadeVanishAt = -1f;
            fadeLitKnown = false;
            fadeOrbId = PickFadeOrb();
            if (fadeOrbId == null && session.Network.TutorialDropOrb(LabOrbKind.Yin, LabOrbElement.None,
                    GameTutorialScript.FadeDemoLanding))
                fadeOrbId = PickFadeOrb();
            fadeHolding = fadeOrbId != null;
            fadeRefreshAt = -1f;
            if (fadeOrbId == null) Debug.LogWarning("HAPIO_TUTORIAL_FADE_DEMO_NO_ORB", this);
        }

        /// <summary>"…곧 없어져!": stop winding it back; its last seconds run out and it goes up in smoke.</summary>
        private void BeginOrbVanish()
        {
            fadeHolding = false;
            if (fadeOrbId == null) return;
            fadeVanishAt = Now;
            if (!session.Network.TutorialSetOrbLife(fadeOrbId, FadeWarningSeconds))
                Debug.LogWarning("HAPIO_TUTORIAL_FADE_DEMO_REFUSED " + fadeOrbId, this);
        }

        /// <summary>Every frame (also behind the skip dialog, so the glowing orb never runs out unseen).</summary>
        private void UpdateFadeDemo()
        {
            if (fadeOrbId == null || fadeGoneAt >= 0f || session.Network == null) return;
            if (!OrbOnMyBoard(fadeOrbId))
            {
                fadeGoneAt = Now;
                return;
            }
            if (!fadeHolding || Time.unscaledTime < fadeRefreshAt) return;
            fadeRefreshAt = Time.unscaledTime + FadeHoldRefreshSeconds;
            session.Network.TutorialSetOrbLife(fadeOrbId, FadeWarningSeconds);
        }

        /// <summary>"…곧 없어져!" waits for the orb to go before a tap moves it on.</summary>
        private bool FadeDemoHoldsTap => HasBeat && CurrentBeat.IsLine && CurrentBeat.Cue == GameTutorialCue.OrbVanish
            && fadeOrbId != null && fadeGoneAt < 0f && fadeVanishAt >= 0f
            && Now - fadeVanishAt < FadeWarningSeconds + FadeGiveUpSeconds;

        /// <summary>When the held line may move on: once the orb has gone (never earlier than <paramref name="ready"/>).</summary>
        private float FadeTapReadyAt(float ready)
        {
            if (!HasBeat || !CurrentBeat.IsLine || CurrentBeat.Cue != GameTutorialCue.OrbVanish || fadeOrbId == null) return ready;
            if (FadeDemoHoldsTap) return float.PositiveInfinity;
            float released = fadeGoneAt >= 0f ? fadeGoneAt : fadeVanishAt + FadeWarningSeconds + FadeGiveUpSeconds;
            return Mathf.Max(ready, released);
        }

        /// <summary>The light on the demo orb; after it has gone, the place it was.</summary>
        private bool TryFadeOrbCircle(out Vector2 center, out float radius)
        {
            if (fadeOrbId != null && fadeGoneAt < 0f && TryOrbCircle(fadeOrbId, out center, out radius))
            {
                fadeLitKnown = true;
                fadeLitCenter = center;
                fadeLitRadius = radius;
                return true;
            }
            center = fadeLitCenter;
            radius = fadeLitRadius;
            return fadeLitKnown;
        }

        /// <summary>A resting orb of mine to let run out: a yin first (the one left from 2단계), then a yang, then an elemental orb.</summary>
        private string PickFadeOrb()
        {
            LabSnapshot snapshot = session.Network != null ? session.Network.Snapshot : null;
            if (snapshot?.orbs == null) return null;
            ulong me = session.Network.LocalPlayerId;
            LabOrbState best = null;
            foreach (LabOrbState orb in snapshot.orbs)
            {
                if (orb == null || orb.owner != me || orb.inFlight) continue;
                int rank = FadeRank(orb.kind);
                if (rank < 0) continue;
                if (best == null || rank < FadeRank(best.kind) || rank == FadeRank(best.kind) && orb.createdAt > best.createdAt)
                    best = orb;
            }
            return best?.id;
        }

        /// <summary>Which orbs the demo may use, best first; -1 for none (drums, bombs and items keep their own rules).</summary>
        public static int FadeRank(LabOrbKind kind) =>
            kind == LabOrbKind.Yin ? 0 : kind == LabOrbKind.Yang ? 1 : kind == LabOrbKind.Combined ? 2 : -1;

        private bool OrbOnMyBoard(string orbId)
        {
            LabSnapshot snapshot = session.Network.Snapshot;
            if (snapshot?.orbs == null) return false;
            ulong me = session.Network.LocalPlayerId;
            foreach (LabOrbState orb in snapshot.orbs)
                if (orb != null && orb.id == orbId && orb.owner == me) return true;
            return false;
        }
    }
}
