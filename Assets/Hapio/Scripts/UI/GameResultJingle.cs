using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// #745 스토리 1~3스테이지 결과 징글과 햅틱. 결과 이야기(GameStageStoryPresentation)가 기록 카드(StoryCardRecords)나 봉인 게이지
    /// (StorySealGauge)가 있는 장면을 처음 띄우는 순간, 판 결과(ResultSnapshot)가 성공이면 승리 징글, 실패면 패배 징글을 한 번 틀고
    /// 소리 박자에 맞춘 진동을 낸다. 같은 판(run + stage + battle session + round)에서는 한 번만. 땅따먹기 / 튜토리얼은 조용하다.
    /// 소리는 Resources/Hapio/Audio/ResultVictory, ResultDefeat (없으면 조용히 넘어가고 진동만). 이 기기에서만 나며 네트워크로 보내지 않는다.
    /// 장면이 열릴 때 결과 이야기에 스스로 붙는다(팀원 결과 화면 코드는 그대로).
    /// </summary>
    public sealed class GameResultJingle : MonoBehaviour
    {
        public const string VictoryPath = "Hapio/Audio/ResultVictory", DefeatPath = "Hapio/Audio/ResultDefeat";

        /// <summary>진동 한 박: at초에 style로 intensity만큼, continuousSeconds가 있으면 그동안 이어서 울린다.</summary>
        public struct Beat
        {
            public float at, intensity, continuousSeconds, continuousIntensity, continuousSharpness;
            public LabHapticStyle style;
            public Beat(float at, LabHapticStyle style, float intensity, float continuousSeconds = 0f, float continuousIntensity = 0f,
                float continuousSharpness = .5f)
            {
                this.at = at; this.style = style; this.intensity = intensity; this.continuousSeconds = continuousSeconds;
                this.continuousIntensity = continuousIntensity; this.continuousSharpness = continuousSharpness;
            }
        }

        /// <summary>승리: 꽹과리 세 번 톡톡톡, 0.63초 큰 징 쾅 + 울림.</summary>
        public static readonly Beat[] VictoryBeats =
        {
            new Beat(.03f, LabHapticStyle.Light, .55f), new Beat(.2f, LabHapticStyle.Light, .6f), new Beat(.38f, LabHapticStyle.Medium, .7f),
            new Beat(.63f, LabHapticStyle.Heavy, 1f, 1.4f, .55f, .3f),
        };
        /// <summary>패배: 낮은 북 심장 소리에 맞춰 쿵 / 쿵 / 쿵, 마지막 징 울림 동안 낮게 길게 떨리다 사라진다.</summary>
        public static readonly Beat[] DefeatBeats =
        {
            new Beat(.14f, LabHapticStyle.Heavy, .8f), new Beat(.47f, LabHapticStyle.Medium, .5f),
            new Beat(.77f, LabHapticStyle.Heavy, .7f), new Beat(1.27f, LabHapticStyle.Heavy, .6f, 1.4f, .35f, .1f),
        };

        [Range(0f, 1f)] public float volume = .9f;
        [Range(0f, 2f)] public float hapticStrength = 1f;

        private GameStageStoryPresentation story;
        private StoryPlayer player;
        private string playedKey;
        private AudioSource source;
        private AudioListener borrowed;
        private Beat[] beats;
        private int nextBeat;
        private float startedAt;

        /// <summary>마지막으로 튼 결과 (테스트·확인용): 1 = 승리, -1 = 패배, 0 = 아직 없음.</summary>
        public int LastPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
            Attach(default, LoadSceneMode.Single);
        }

        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            foreach (var presentation in FindObjectsByType<GameStageStoryPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (presentation.GetComponent<GameResultJingle>() == null) presentation.gameObject.AddComponent<GameResultJingle>();
        }

        private void Awake() => story = GetComponent<GameStageStoryPresentation>();

        private void Update()
        {
            if (player == null && story != null && story.player != null)
            {
                player = story.player;
                player.ViewChanged += OnView;
            }
            DriveBeats();
        }

        private void OnDestroy()
        {
            if (player != null) player.ViewChanged -= OnView;
            if (borrowed != null) Destroy(borrowed);
        }

        private void OnView(StoryCutView view)
        {
            if (view == null || !IsResultView(view)) return;
            GameSession session = GameSession.Instance;
            if (session == null || session.IsTerritory || session.IsTutorial) return;
            LabSnapshot result = session.ResultSnapshot;
            if (result == null || result.phase != LabPhase.Victory && result.phase != LabPhase.Defeat) return;
            string key = session.State.runId + ":" + session.State.stageIndex + ":" + result.sessionId + ":" + result.round;
            if (key == playedKey) return;
            playedKey = key;
            Play(result.phase == LabPhase.Victory, session.Network != null ? session.Network.config : null);
        }

        /// <summary>기록 카드나 봉인 게이지가 있는 장면 = 결과 표가 보이는 장면.</summary>
        public static bool IsResultView(StoryCutView view) => view != null
            && (view.GetComponentInChildren<StoryCardRecords>(true) != null || view.GetComponentInChildren<StorySealGauge>(true) != null);

        /// <summary>징글과 진동을 지금 낸다 (VFX 실험실 미리 보기도 이것을 부른다).</summary>
        public void Play(bool victory, LabConfig config)
        {
            LastPlayed = victory ? 1 : -1;
            AudioClip clip = Resources.Load<AudioClip>(victory ? VictoryPath : DefeatPath);
            if (clip != null)
            {
                if (source == null)
                {
                    source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                }
                if (borrowed == null && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
                    borrowed = gameObject.AddComponent<AudioListener>();
                source.PlayOneShot(clip, volume * LabAudioSettings.SfxVolume);
            }
            this.config = config;
            beats = victory ? VictoryBeats : DefeatBeats;
            nextBeat = 0;
            startedAt = Time.unscaledTime;
            DriveBeats();
        }

        private LabConfig config;

        private void DriveBeats()
        {
            if (beats == null) return;
            float t = Time.unscaledTime - startedAt;
            while (nextBeat < beats.Length && beats[nextBeat].at <= t)
            {
                Beat b = beats[nextBeat++];
                if (hapticStrength > 0f)
                    LabHaptics.PlayDirect(config, b.style, Mathf.Clamp01(b.intensity * hapticStrength), b.continuousSeconds,
                        Mathf.Clamp01(b.continuousIntensity * hapticStrength), b.continuousSharpness);
            }
            if (nextBeat >= beats.Length) beats = null;
        }

        /// <summary>VFX 실험실(#745): 결과 화면 없이 징글과 진동만 지금 낸다.</summary>
        public static bool Preview(bool victory, LabConfig config)
        {
            var host = new GameObject("Result Jingle Preview");
            var jingle = host.AddComponent<GameResultJingle>();
            jingle.Play(victory, config);
            Destroy(host, 6f);
            return true;
        }
    }
}
