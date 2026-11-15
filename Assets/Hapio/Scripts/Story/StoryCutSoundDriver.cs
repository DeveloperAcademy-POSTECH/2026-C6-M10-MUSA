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
            Transform music = player.transform.Find("Story Music");
            AudioSource song = music != null ? music.GetComponent<AudioSource>() : null;
            if (song != null && song.isPlaying) StartCoroutine(Stop(song, Mathf.Max(.05f, seconds)));
        }

        private static IEnumerator Stop(AudioSource song, float seconds)
        {
            float pitch = song.pitch, t = 0f;
            while (t < seconds && song != null && song.isPlaying)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                song.pitch = Mathf.Lerp(pitch, .05f, k * k);
                yield return null;
            }
            if (song == null) yield break;
            song.Stop();
            song.pitch = pitch;
        }
    }
}
