using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    [Serializable]
    public sealed class StoryDialoguePage
    {
        [InspectorName("화자")] public string speaker;
        [InspectorName("대사"), TextArea(2, 8)] public string dialogue;
        [InspectorName("자동 진행 대기 시간(초)"), Min(.1f)] public float autoReadSeconds = 4f;
        [InspectorName("음성·효과음"), Tooltip("대사가 없는 페이지에도 효과음을 연결할 수 있습니다.")] public AudioClip voice;
    }

    [CreateAssetMenu(menuName = "Hapio/Story/Cut", fileName = "StoryCut")]
    public sealed class StoryCutDefinition : ScriptableObject
    {
        public string cutId;
        public StoryCutView viewPrefab;
        public List<StoryDialoguePage> pages = new List<StoryDialoguePage>();
        [InspectorName("연출 메모"), TextArea(1, 4)] public string direction;

        // Retain serialized single-page data until the explicit editor migration is used.
        [HideInInspector] public string speaker;
        [HideInInspector] public string dialogue;
        [HideInInspector] public float autoReadSeconds = 4f;
        [HideInInspector] public AudioClip voice;

        public int PageCount => pages != null && pages.Count > 0 ? pages.Count : 1;

        public StoryDialoguePage GetPage(int index)
        {
            if (index < 0 || index >= PageCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (pages != null && pages.Count > 0) return pages[index] ?? new StoryDialoguePage();
            return new StoryDialoguePage
            {
                speaker = speaker, dialogue = dialogue, autoReadSeconds = autoReadSeconds, voice = voice
            };
        }
    }
}
