using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>#629 지금 나와야 할 타이틀·오프닝 음악.</summary>
    public enum GameStoryMusicMode { Silent, Calm, Broken }

    /// <summary>
    /// #629 타이틀·오프닝·방 목록 음악. GameSession이 하나 만들어 Scene이 바뀌어도 계속 남는다.
    /// - 타이틀 화면: 평화 곡이 들어와 반복된다. 시작을 눌러 오프닝이 열려도 같은 곡이 끊기지 않고 이어진다.
    /// - 오프닝에서 박이 떨어지기 시작하는 컷(<see cref="GameStoryMusicArt.breakCutId"/>)이 시작되면, 평화 곡이 테이프가 멈추듯
    ///   빠르기와 음높이가 함께 떨어지며 사라지고 메아리만 남는다. 잠시 뒤 깨진 이후 곡이 스며들 듯 들어온다.
    ///   한 번 깨지면 앞 컷으로 돌아가도 오프닝이 끝날 때까지 깨진 이후 곡이 이어진다.
    /// - 오프닝이 끝나거나 건너뛰면 곡이 사라진다. 그 뒤 튜토리얼·전투에서는 나오지 않고, 방 목록(Home)과 대기실에서는 평화 곡이 다시 나온다.
    /// - 크기는 게임 설정의 BGM 볼륨을 따른다. Home에는 일부러 AudioListener가 없어서, 곡이 나오는 동안만 잠깐 붙인다(터치 먹물과 같은 방식).
    /// - 이 기기에서만 나는 소리이고 네트워크로 보내지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameStoryMusic : MonoBehaviour
    {
        private GameStoryMusicArt art;
        private AudioSource calm, broken;
        private AudioEchoFilter echo;
        private AudioListener borrowed;
        private float calmFade, brokenFade;
        private bool breaking;
        private float breakElapsed;
        private bool openingSeenPlaying, openingBroken;

        /// <summary>테스트에서 끄면 Update가 화면을 보고 스스로 정하지 않는다. <see cref="Advance"/>를 직접 부른다.</summary>
        public bool followsGame = true;

        public GameStoryMusicMode Mode { get; private set; }
        public AudioSource CalmSource => calm;
        public AudioSource BrokenSource => broken;
        public bool Breaking => breaking;
        public bool EchoOn => echo != null && echo.enabled;

        public static GameStoryMusic Create(Transform parent, GameStoryMusicArt art = null)
        {
            art = art != null ? art : GameStoryMusicArt.Load();
            if (art == null)
            {
                Debug.LogWarning("HAPIO_STORY_MUSIC_ART_MISSING: Resources/" + GameStoryMusicArt.ResourcePath);
                return null;
            }
            var go = new GameObject("GameStoryMusic");
            if (parent != null) go.transform.SetParent(parent, false);
            var music = go.AddComponent<GameStoryMusic>();
            music.Build(art);
            return music;
        }

        private void Build(GameStoryMusicArt musicArt)
        {
            art = musicArt;
            calm = NewSource("Calm");
            echo = calm.gameObject.AddComponent<AudioEchoFilter>();
            echo.enabled = false;
            broken = NewSource("Broken");
        }

        private AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>어느 화면에서 어떤 곡이 나와야 하는지. 오프닝이 가장 먼저다.</summary>
        public static GameStoryMusicMode ModeFor(string scenePath, bool tutorial, bool openingShown, bool openingFinished,
            bool openingBroken, bool playInLobby)
        {
            if (openingShown)
                return openingFinished ? GameStoryMusicMode.Silent
                    : openingBroken ? GameStoryMusicMode.Broken : GameStoryMusicMode.Calm;
            if (tutorial) return GameStoryMusicMode.Silent;
            if (scenePath == GameScenePaths.Home) return GameStoryMusicMode.Calm;
            if (scenePath == GameScenePaths.Lobby && playInLobby) return GameStoryMusicMode.Calm;
            return GameStoryMusicMode.Silent;
        }

        /// <summary>이 오프닝이 박이 떨어지는 컷에 닿았거나 지나갔는지.</summary>
        public static bool ReachedBreak(StoryPlayer player, string breakCutId)
        {
            if (player == null || player.sequence == null || player.sequence.cuts == null || string.IsNullOrEmpty(breakCutId)) return false;
            int at = player.sequence.cuts.FindIndex(cut => cut != null && cut.cutId == breakCutId);
            return at >= 0 && player.CurrentCutIndex >= at;
        }

        private void Update()
        {
            if (!followsGame || art == null) return;
            Advance(WantedNow(), Time.unscaledDeltaTime);
        }

        private GameStoryMusicMode WantedNow()
        {
            bool shown = GameOpeningController.IsPresenting;
            StoryPlayer player = GameOpeningController.PresentingPlayer;
            bool playing = player != null && player.IsPlaying;
            if (!shown) openingSeenPlaying = openingBroken = false;
            if (playing)
            {
                openingSeenPlaying = true;
                openingBroken |= ReachedBreak(player, art.breakCutId);
            }
            GameSession session = GameSession.Instance;
            return ModeFor(SceneManager.GetActiveScene().path, session != null && session.IsTutorial, shown,
                openingSeenPlaying && !playing, openingBroken, art.playInLobby);
        }

        /// <summary>원하는 곡 쪽으로 <paramref name="seconds"/>만큼 다가간다.</summary>
        public void Advance(GameStoryMusicMode wanted, float seconds)
        {
            if (art == null) return;
            Mode = wanted;
            if (wanted == GameStoryMusicMode.Broken && !breaking) BeginBreak();
            else if (wanted == GameStoryMusicMode.Calm && breaking) EndBreak();

            float bgm = LabAudioSettings.BgmVolume;
            if (breaking)
            {
                breakElapsed += seconds;
                float progress = breakElapsed / Mathf.Max(.1f, art.tapeStopSeconds);
                calm.pitch = Mathf.Max(0f, art.TapeSpeedAt(progress));
                // Silence (the opening was skipped mid-fall) also takes the dying tape down with everything else.
                if (wanted == GameStoryMusicMode.Silent) calmFade = Toward(calmFade, 0f, seconds);
                calm.volume = calmFade * GameStoryMusicArt.TapeLoudnessAt(progress) * art.calmVolume * bgm;
                // Keep the source running at a standstill so the echo can ring out; then let it go.
                if (calm.isPlaying && breakElapsed >= art.tapeStopSeconds + art.echoTailSeconds) calm.Stop();
            }
            else
            {
                calmFade = Toward(calmFade, wanted == GameStoryMusicMode.Calm ? 1f : 0f, seconds);
                calm.pitch = 1f;
                calm.volume = calmFade * art.calmVolume * bgm;
                SetPlaying(calm, art.calmMusic, calmFade > 0f);
            }

            bool brokenWanted = wanted == GameStoryMusicMode.Broken && breakElapsed >= art.brokenDelaySeconds;
            float brokenSeconds = brokenWanted ? art.brokenFadeInSeconds : art.fadeOutSeconds;
            brokenFade = Mathf.MoveTowards(brokenFade, brokenWanted ? 1f : 0f, brokenSeconds <= 0f ? 1f : seconds / brokenSeconds);
            broken.volume = brokenFade * art.brokenVolume * bgm;
            SetPlaying(broken, art.brokenMusic, brokenFade > 0f);

            // The opening is over (or the screen went silent): the next calm music starts again from the top.
            if (breaking && wanted == GameStoryMusicMode.Silent && brokenFade <= 0f && (!calm.isPlaying || calmFade <= 0f)) EndBreak();
            KeepListener(calm.isPlaying || broken.isPlaying);
        }

        private float Toward(float level, float goal, float seconds)
        {
            float span = goal > level ? art.fadeInSeconds : art.fadeOutSeconds;
            return Mathf.MoveTowards(level, goal, span <= 0f ? 1f : seconds / span);
        }

        private void BeginBreak()
        {
            breaking = true;
            breakElapsed = 0f;
            echo.delay = art.echoDelayMs;
            echo.decayRatio = art.echoDecay;
            echo.wetMix = art.echoWet;
            echo.dryMix = 1f;
            echo.enabled = true;
            // Reached straight from silence (e.g. a test or a very late start): there is no calm music to stop.
            if (!calm.isPlaying) calmFade = 0f;
        }

        private void EndBreak()
        {
            breaking = false;
            breakElapsed = 0f;
            echo.enabled = false;
            calm.Stop();
            calm.pitch = 1f;
            calmFade = 0f;
        }

        private static void SetPlaying(AudioSource source, AudioClip clip, bool on)
        {
            if (on)
            {
                if (source.clip != clip) { source.Stop(); source.clip = clip; }
                if (clip != null && !source.isPlaying) source.Play();
            }
            else if (source.isPlaying) source.Stop();
        }

        private void KeepListener(bool sounding)
        {
            if (sounding && borrowed == null && !AnotherListener()) borrowed = gameObject.AddComponent<AudioListener>();
            else if (borrowed != null && (!sounding || AnotherListener())) ReturnListener();
        }

        private bool AnotherListener()
        {
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != borrowed && listener.isActiveAndEnabled) return true;
            return false;
        }

        private void ReturnListener()
        {
            if (borrowed != null) Destroy(borrowed);
            borrowed = null;
        }

        private void OnDisable()
        {
            ReturnListener();
            if (calm != null) calm.Stop();
            if (broken != null) broken.Stop();
        }
    }
}
