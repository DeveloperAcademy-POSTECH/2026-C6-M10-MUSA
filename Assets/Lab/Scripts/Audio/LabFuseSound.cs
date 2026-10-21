using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 내 판에 잠긴 폭탄이 있는 동안 도화선이 타는 치지직 소리를 반복한다. 가장 빨리 터질 폭탄을
    /// 기준으로, 빨갛게 깜빡이는 마지막 몇 초에는 소리가 커지고 빨라진다. 잠금이 풀리거나 터지면,
    /// 일시정지 중이거나 전투가 끝나면 멈춘다.
    /// 소리 파일은 Resources/Audio/Fuse 폴더에 넣으면 그 첫 파일을 쓰고, 없으면 코드로 만든 소리를 쓴다.
    /// 크기는 LabConfig의 Fuse Sound Volume × 일시정지 메뉴 SFX 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabFuseSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/Fuse";
        private const int GeneratedRate = 22050;
        // 빨간 깜빡임이 시작되면 소리는 이 배율까지 커지고 빨라진다.
        private const float CalmVolume = .6f;
        private const float UrgentPitch = 1.35f;

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 Fuse Sound Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .5f;

        private static AudioClip generatedClip;
        private static bool listeningForAudioChanges;
        private AudioSource source;
        private bool usesGeneratedClip;
        private LabOrbBoard board;
        private LabNetwork network;

        public bool Playing => source != null && source.isPlaying;
        /// <summary>내 판에서 잠긴 폭탄이 타고 있어 소리를 내야 하는지. 오디오 장치가 꺼져 있어도 같다.</summary>
        public bool Burning { get; private set; }
        public AudioClip Clip => source != null ? source.clip : null;
        public float CurrentVolume => source != null ? source.volume : 0f;
        public float CurrentPitch => source != null ? source.pitch : 1f;

        private LabConfig Config => network != null ? network.config : null;
        private float BaseVolume => Config != null ? Config.FuseSoundVolume : volume;

        public void Bind(LabOrbBoard targetBoard, LabNetwork targetNetwork)
        {
            board = targetBoard;
            network = targetNetwork;
        }

        private void Awake()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(ClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 화면 기준 소리라 위치와 상관없이 같은 크기로 들린다.
            usesGeneratedClip = clips.Length == 0;
            source.clip = usesGeneratedClip ? GeneratedSizzle() : clips[0];
        }

        private void Update()
        {
            // Unity recreates imported clips after an audio device reset, but loses clips
            // created by script. Rebind every source to the replacement shared loop.
            if (usesGeneratedClip && (source.clip != generatedClip || !ValidGeneratedClip(source.clip)))
                source.clip = GeneratedSizzle();
            float fuse = board != null ? board.ShortestFuse() : -1f;
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool battle = network == null
                || (snapshot != null && snapshot.phase == LabPhase.Playing && !snapshot.paused);
            Burning = fuse >= 0f && battle;
            if (!Burning || source.clip == null)
            {
                if (source.isPlaying) source.Stop();
                return;
            }
            LabOrbAppearance look = Config != null ? Config.OrbAppearance : null;
            float warning = look != null ? look.BombWarningSeconds : 3f;
            float urgency = warning > 0f ? 1f - Mathf.Clamp01(fuse / warning) : 0f;
            source.volume = BaseVolume * LabAudioSettings.SfxVolume * Mathf.Lerp(CalmVolume, 1f, urgency);
            source.pitch = Mathf.Lerp(1f, UrgentPitch, urgency);
            if (!source.isPlaying) source.Play();
        }

        /// <summary>
        /// 1초짜리 반복용 치지직 소리: 높은 쉬익 소리가 불규칙하게 떨리고, 그 위로 짧은 탁탁 튐이
        /// 초당 수십 번 섞인다. 매번 같은 씨앗으로 만들어 모든 기기에서 같은 소리가 난다.
        /// </summary>
        public static AudioClip GeneratedSizzle()
        {
            if (!listeningForAudioChanges)
            {
                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
                listeningForAudioChanges = true;
            }
            if (ValidGeneratedClip(generatedClip)) return generatedClip;
            if (generatedClip != null) Destroy(generatedClip);
            var random = new System.Random(177);
            var samples = new float[GeneratedRate];
            float previous = 0f, crackle = 0f, flutter = 1f;
            for (int i = 0; i < samples.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                // 바로 앞 값과의 차이만 남기면 낮은 소리가 빠져 쉬익 하는 소리가 된다.
                float hiss = white - previous;
                previous = white;
                // 불꽃이 일렁이듯 쉬익 크기가 천천히 오르내린다.
                if (i % 441 == 0) flutter = .55f + .45f * (float)random.NextDouble();
                // 초당 약 45번, 짧게 터졌다 사라지는 탁 소리.
                if (random.NextDouble() < 45d / GeneratedRate) crackle = .5f + .5f * (float)random.NextDouble();
                crackle *= .99f;
                float pop = crackle * ((float)random.NextDouble() * 2f - 1f);
                samples[i] = Mathf.Clamp(.22f * hiss * flutter + .55f * pop, -1f, 1f);
            }
            generatedClip = AudioClip.Create("Lab fuse sizzle", samples.Length, 1, GeneratedRate, false);
            generatedClip.SetData(samples, 0);
            generatedClip.hideFlags = HideFlags.DontSave;
            return generatedClip;
        }

        private static bool ValidGeneratedClip(AudioClip clip)
        {
            return clip != null && clip.samples == GeneratedRate && clip.channels == 1
                && clip.frequency == GeneratedRate && clip.length > 0f;
        }

        private static void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // A non-null Unity object can survive the reset with no native sample data.
            // Drop it even when its wrapper still reports the old dimensions.
            AudioClip previous = generatedClip;
            generatedClip = null;
            if (previous != null) Destroy(previous);
        }
    }
}
