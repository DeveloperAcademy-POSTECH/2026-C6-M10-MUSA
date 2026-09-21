using C6.Prototype.Attack;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Jangsanbeom (장산범) sound effects: hit and defeat. Presentation only.
    /// It watches the synced monster HP in the attack snapshot (not the Host-only ValidHitAt event),
    /// so Host and clients hear the same thing, and it never changes HP, hits, or damage.
    ///
    /// Every volume slider takes effect while the game is running: enter Play mode, drag, and the
    /// next hit is already at the new level.
    ///
    /// Setup: add to an object in ContinuousTransferBattle (e.g. an empty "Audio" object) and drag
    /// clips into the Inspector. Empty slots are simply skipped.
    /// Each monster gets its own sound script (JangsanbeomSound, ...) so their sounds stay separate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JangsanbeomSound : MonoBehaviour
    {
        [Header("Hit — one is picked at random when several are assigned")]
        [SerializeField] private AudioClip[] hitClips = new AudioClip[0];
        [Tooltip("Live: drag during Play and the next hit uses the new level.")]
        [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;
        [Tooltip("Small random pitch change so repeated hits do not sound identical.")]
        [SerializeField, Range(0f, .3f)] private float hitPitchJitter = .08f;

        [Header("Defeat (HP reaches 0) — plays instead of the hit sound")]
        [SerializeField] private AudioClip defeatClip;
        [SerializeField, Range(0f, 1f)] private float defeatVolume = 1f;

        private const float BindRetrySeconds = .5f;

        private AudioSource source;
        private AttackSession attack;
        private float nextBindTime;

        private string trackedSession;
        private uint trackedRound;
        private int? lastHp;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D: same volume wherever the camera is
        }

        // AttackSession is added at runtime by T09BattleController, so it cannot be dragged into
        // the Inspector. Find it once it exists.
        private void Update()
        {
            if (attack != null || Time.unscaledTime < nextBindTime) return;
            nextBindTime = Time.unscaledTime + BindRetrySeconds;
            attack = FindAnyObjectByType<AttackSession>();
            if (attack != null) { attack.Changed += OnAttackChanged; OnAttackChanged(); }
        }

        private void OnDestroy()
        {
            if (attack != null) attack.Changed -= OnAttackChanged;
        }

        // HP went down -> hit. HP went down to 0 -> defeat.
        // A new session/round or an HP increase (reset) only updates the baseline, silently.
        private void OnAttackChanged()
        {
            var state = attack != null ? attack.Snapshot : null;
            if (state == null) { lastHp = null; return; }

            if (state.sessionId != trackedSession || state.roundId != trackedRound)
            {
                trackedSession = state.sessionId;
                trackedRound = state.roundId;
                lastHp = state.hp;
                return;
            }

            int hp = state.hp;
            if (lastHp.HasValue && hp < lastHp.Value)
            {
                if (hp <= 0) Play(defeatClip, defeatVolume, 1f);
                else PlayHit();
            }
            lastHp = hp;
        }

        private void PlayHit()
        {
            if (hitClips == null || hitClips.Length == 0) return;
            var clip = hitClips[Random.Range(0, hitClips.Length)];
            Play(clip, hitVolume, 1f + Random.Range(-hitPitchJitter, hitPitchJitter));
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
