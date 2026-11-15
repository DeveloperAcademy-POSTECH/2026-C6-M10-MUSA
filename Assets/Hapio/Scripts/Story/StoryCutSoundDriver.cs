using System.Collections;
using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// #757 StoryPlayer 옆에서 지금 컷의 StoryCutSound(컷 View 프리팹에 저장된 목록)를 시각에 맞춰 낸다. 장면이 열릴 때 모든
    /// StoryPlayer에 스스로 붙는다(StoryPlayer 코드는 그대로). 컷이 바뀌거나 같은 컷을 다시 띄우면(새 View) 처음부터 다시 센다.
    /// 소리 크기는 × 일시정지 메뉴 효과음 설정. 이 기기에서만 난다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryCutSoundDriver : MonoBehaviour
    {
        private StoryPlayer player;
        private StorySequence sequence;
        private StoryCutView view;
        private int cut = -1;
        private float startedAt;
        private readonly List<StoryCutSound.Entry> pending = new List<StoryCutSound.Entry>();
        private AudioSource source;
        // #757 리뷰: 진행 중인 테이프 멈춤. 컷이 바뀌면 멈추고 곡의 빠르기를 되돌린다(다음 컷 곡을 멈추지 않게).
        private Coroutine stopping;
        private AudioSource stoppingSong;
        private float stoppingPitch = 1f;

        /// <summary>테이프 멈춤이 진행 중인지 (검사·확인용).</summary>
        public bool TapeStopping => stopping != null;

        /// <summary>지금 컷에서 낸 효과음 수 (확인용).</summary>
        public int PlayedCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
            Attach(default, LoadSceneMode.Single);
        }

        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            foreach (var story in FindObjectsByType<StoryPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Ensure(story);
        }

        public static StoryCutSoundDriver Ensure(StoryPlayer story)
        {
            if (story == null) return null;
            var driver = story.GetComponent<StoryCutSoundDriver>();
            return driver != null ? driver : story.gameObject.AddComponent<StoryCutSoundDriver>();
        }

        private void Awake() => player = GetComponent<StoryPlayer>();

        private void Update()
        {
            if (player == null) return;
            if (!player.IsPlaying)
            {
                if (cut != -1) CancelTapeStop();
                cut = -1; view = null; pending.Clear();
                return;
            }
            if (player.sequence != sequence || player.CurrentCutIndex != cut || player.CurrentView != view) Begin();
            float t = Time.unscaledTime - startedAt;
            for (int i = 0; i < pending.Count;)
            {
                if (pending[i].delay > t) { i++; continue; }
                Fire(pending[i]);
                pending.RemoveAt(i);
            }
        }

        private void Begin()
        {
            CancelTapeStop();
            sequence = player.sequence; cut = player.CurrentCutIndex; view = player.CurrentView;
            startedAt = Time.unscaledTime; pending.Clear(); PlayedCount = 0;
            StoryCutDefinition definition = sequence != null && cut >= 0 && cut < sequence.cuts.Count ? sequence.cuts[cut] : null;
            StoryCutSound data = definition != null && definition.viewPrefab != null ? definition.viewPrefab.GetComponent<StoryCutSound>() : null;
            if (data == null || data.entries == null) return;
            foreach (var entry in data.entries)
                if (entry != null && (entry.clip != null || entry.stopMusic)) pending.Add(entry);
        }

        private void Fire(StoryCutSound.Entry entry)
        {
            PlayedCount++;
            if (entry.stopMusic) TapeStop(entry.stopSeconds);
            if (entry.clip == null) return;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(entry.clip, entry.volume * LabAudioSettings.SfxVolume);
        }

        /// <summary>이야기 배경음악(StoryPlayer가 만든 "Story Music")을 seconds초에 걸쳐 느려지며 멈춘다.</summary>
        private void TapeStop(float seconds)
        {
            CancelTapeStop();
            Transform music = player.transform.Find("Story Music");
            AudioSource song = music != null ? music.GetComponent<AudioSource>() : null;
            if (song == null || !song.isPlaying) return;
            stoppingSong = song;
            stoppingPitch = song.pitch;
            stopping = StartCoroutine(Stop(song, song.clip, player.CurrentView, Mathf.Max(.05f, seconds)));
        }

        /// <summary>
        /// 같은 곡 / 같은 컷인 동안만 느려지게 하고 끝에서 멈춘다. 그 사이 컷이 바뀌었거나(앞뒤로 넘김) 다른 곡이 올라왔으면
        /// 멈추지 않고 빠르기만 되돌린 채 끝낸다.
        /// </summary>
        private IEnumerator Stop(AudioSource song, AudioClip clip, StoryCutView startedOn, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!StillOurs(song, clip, startedOn)) { CancelTapeStop(); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                song.pitch = Mathf.Lerp(stoppingPitch, .05f, k * k);
                yield return null;
            }
            if (StillOurs(song, clip, startedOn)) song.Stop();
            if (song != null) song.pitch = stoppingPitch;
            stopping = null; stoppingSong = null;
        }

        private bool StillOurs(AudioSource song, AudioClip clip, StoryCutView startedOn) =>
            song != null && song.isPlaying && song.clip == clip && player != null && player.IsPlaying && player.CurrentView == startedOn;

        /// <summary>진행 중인 테이프 멈춤을 그만두고 곡의 빠르기를 원래대로 돌린다 (곡은 멈추지 않는다).</summary>
        private void CancelTapeStop()
        {
            if (stopping != null) StopCoroutine(stopping);
            stopping = null;
            if (stoppingSong != null) stoppingSong.pitch = stoppingPitch;
            stoppingSong = null;
        }

        private void OnDisable() => CancelTapeStop();
    }
}
