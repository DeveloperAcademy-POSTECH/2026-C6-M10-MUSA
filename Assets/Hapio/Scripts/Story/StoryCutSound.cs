using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// #757 컷 하나에 덧붙이는 효과음 목록 (데이터만). 컷 View 프리팹 맨 위에 붙이고, StoryCutSoundDriver가 그 컷이 시작된 뒤
    /// delay초에 한 번씩 낸다. stopMusic을 켜면 그 순간 이야기 배경음악을 테이프가 멈추듯 느려지며 끊는다(소리 파일은 없어도 된다).
    /// 엔딩 SFX 창(Hapio > 엔딩 SFX)에서 고치고 듣는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryCutSound : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public AudioClip clip;
            [Tooltip("컷이 시작되고 몇 초 뒤에 내는지")]
            [Min(0)] public float delay;
            [Range(0, 1)] public float volume = .8f;
            [Tooltip("이 순간 배경음악을 테이프 멈추듯 끊는다")]
            public bool stopMusic;
            [Tooltip("테이프 멈춤에 걸리는 초")]
            [Min(.05f)] public float stopSeconds = .7f;
        }

        public List<Entry> entries = new List<Entry>();
    }
}
