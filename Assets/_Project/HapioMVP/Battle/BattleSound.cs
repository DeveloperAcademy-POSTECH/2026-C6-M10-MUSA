using System.Collections.Generic;
using C6.Prototype.Attack;
using UnityEngine;

namespace C6.Prototype.Battle
{
    /// <summary>
    /// Battle-wide sound: background music, round start, the orb throw sound, and MISS.
    /// These are the sounds that stay the same whichever monster is on screen
    /// (monster-specific hit/defeat sounds live in JangsanbeomSound).
    ///
    /// Presentation only — it listens to the synced snapshots, so Host and clients hear the same
    /// thing, and it never changes the clock, HP, or any gameplay state.
    ///
    /// Every volume slider takes effect while the game is running: enter Play mode, drag, and the
    /// change is audible on the next sound (immediately for music).
    ///
    /// Setup: add to an object in ContinuousTransferBattle (e.g. an empty "Audio" object) and drag
    /// clips into the Inspector. Empty slots are simply skipped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleSound : MonoBehaviour
    {
        [Header("Background music")]
        [SerializeField] private AudioClip battleMusic;
        [Tooltip("Live: drag during Play and the music follows at once.")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = .2f;
        [Tooltip("On: music loops only while the battle is Playing. Off: music loops from scene start.")]
        [SerializeField] private bool musicOnlyWhilePlaying = true;
        [Tooltip("0 = loop the whole clip (perfectly seamless, Unity handles it).\n" +
                 "Above 0 = play from the top once, then every repeat restarts at this second. " +
                 "Use it when the clip opens with a quiet build-up you do not want back mid-round.")]
        [SerializeField, Min(0f)] private float musicLoopStart;

        [Header("Orb throw — played IN ORDER, one clip per throw, then back to the first")]
        [SerializeField] private AudioClip[] throwClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float throwVolume = 1f;
        [Tooltip("On: only my own throws make a sound. Off: the other player's throws do too.")]
        [SerializeField] private bool onlyMyThrows = true;

        [Header("MISS — my throw went past the monster")]
        [Tooltip("Plays with the MISS popup, so it is heard by whoever threw it, not by everyone.")]
        [SerializeField] private AudioClip[] missClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float missVolume = 1f;

        [Header("Round start (optional)")]
        [SerializeField] private AudioClip roundStartClip;
        [SerializeField, Range(0f, 1f)] private float roundStartVolume = .8f;

        private const float BindRetrySeconds = .5f;

        // How early the jump back happens. It has to outlast one frame, or a slow frame would let
        // the clip run out before we catch it, so it is a compromise: too small stutters, too large
        // clips the tail. Only used when musicLoopStart > 0.
        private const float LoopGuardSeconds = .06f;

        private AudioSource music;
        private AudioSource cue;
        private BattleSession battle;
        private AttackSession attack;
        private T09BattleController controller;
        private float nextBindTime;
        private BattlePhase lastPhase = BattlePhase.Boot;
        private bool musicWanted;

        // Projectiles already heard. A throw is "a projectile id that was not in the previous snapshot".
        private readonly HashSet<string> knownProjectiles = new HashSet<string>(System.StringComparer.Ordinal);
        private bool projectilesSeeded;
        private int throwIndex;
        private int missIndex;

        private void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f; // 2D: same volume wherever the camera is

            cue = gameObject.AddComponent<AudioSource>();
            cue.playOnAwake = false;
            cue.spatialBlend = 0f;
        }

        private void Start()
        {
            if (!musicOnlyWhilePlaying) StartMusic();
        }

        // BattleSession / AttackSession / T09BattleController are added at runtime, so they cannot
        // be dragged into the Inspector. Find them once they exist.
        private void Update()
        {
            // Read the slider every frame so dragging it in Play mode is heard straight away.
            if (music != null) music.volume = musicVolume;
            AdvanceMusicLoop();

            if ((battle != null && attack != null && controller != null) || Time.unscaledTime < nextBindTime) return;
            nextBindTime = Time.unscaledTime + BindRetrySeconds;

            if (battle == null)
            {
                battle = FindAnyObjectByType<BattleSession>();
                if (battle != null) { battle.Changed += OnBattleChanged; OnBattleChanged(); }
            }
            if (attack == null)
            {
                attack = FindAnyObjectByType<AttackSession>();
                if (attack != null) { attack.Changed += OnAttackChanged; OnAttackChanged(); }
            }
            if (controller == null)
            {
                controller = FindAnyObjectByType<T09BattleController>();
                if (controller != null) controller.MissShown += OnMissShown;
            }
        }

        private void OnDestroy()
        {
            if (battle != null) battle.Changed -= OnBattleChanged;
            if (attack != null) attack.Changed -= OnAttackChanged;
            if (controller != null) controller.MissShown -= OnMissShown;
        }

        private void OnBattleChanged()
        {
            var phase = battle != null ? battle.Phase : BattlePhase.Boot;
            if (phase == lastPhase) return;

            if (phase == BattlePhase.Playing)
            {
                if (roundStartClip != null) cue.PlayOneShot(roundStartClip, roundStartVolume);
                if (musicOnlyWhilePlaying) StartMusic();
            }
            else if (lastPhase == BattlePhase.Playing && musicOnlyWhilePlaying)
            {
                StopMusic();
            }
            // Later: BattlePhase.Victory / BattlePhase.Defeat cues go here.
            lastPhase = phase;
        }

        // A new projectile in the snapshot means someone just threw an orb.
        private void OnAttackChanged()
        {
            var state = attack != null ? attack.Snapshot : null;
            if (state == null || state.projectiles == null)
            {
                knownProjectiles.Clear();
                projectilesSeeded = false;
                return;
            }

            var current = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var projectile in state.projectiles)
            {
                if (projectile == null || string.IsNullOrEmpty(projectile.id)) continue;
                current.Add(projectile.id);
                // The first snapshot after binding only records what is already in flight: no sound.
                if (!projectilesSeeded || knownProjectiles.Contains(projectile.id)) continue;
                if (onlyMyThrows && projectile.owner != attack.LocalPlayerId) continue;
                throwIndex = PlayInOrder(throwClips, throwIndex, throwVolume);
            }
            knownProjectiles.Clear();
            foreach (var id in current) knownProjectiles.Add(id);
            projectilesSeeded = true;
        }

        // The controller already owns the rule for what counts as a miss and when to show it.
        // Following its event keeps the sound on the same frame as the MISS popup.
        private void OnMissShown() => missIndex = PlayInOrder(missClips, missIndex, missVolume);

        /// <summary>Clips are used in order — 1, 2, 3, 1, 2 ... — not at random. Returns the next index.</summary>
        private int PlayInOrder(AudioClip[] clips, int index, float volume)
        {
            if (clips == null || clips.Length == 0) return index;
            for (int attempt = 0; attempt < clips.Length; attempt++)
            {
                var clip = clips[index % clips.Length];
                index = (index + 1) % clips.Length;
                if (clip != null) { cue.PlayOneShot(clip, volume); return index; }
            }
            return index;
        }

        private void StartMusic()
        {
            if (battleMusic == null || music.isPlaying) return;
            music.clip = battleMusic;
            music.volume = musicVolume;
            // With no custom loop point, Unity's own looping is sample-exact. Only take it over
            // when we actually need to skip part of the clip on repeat.
            music.loop = !UsesLoopPoint;
            music.time = 0f;
            music.Play();
            musicWanted = true;
        }

        private void StopMusic()
        {
            musicWanted = false;
            music.Stop();
        }

        private bool UsesLoopPoint => musicLoopStart > 0f;

        // Only runs when a loop point is set: jump back just before the clip would run out, and
        // restart if a hitch let it stop anyway.
        private void AdvanceMusicLoop()
        {
            if (!musicWanted || !UsesLoopPoint || music == null || music.clip == null) return;

            float end = music.clip.length - LoopGuardSeconds;
            if (musicLoopStart >= end) return; // loop point past the end: leave it alone

            if (!music.isPlaying)
            {
                music.time = musicLoopStart;
                music.Play();
            }
            else if (music.time >= end)
            {
                music.time = musicLoopStart;
            }
        }
    }
}
