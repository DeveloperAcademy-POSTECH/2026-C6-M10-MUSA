using System;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 터치 먹물 번짐 (#587) 그림·값. Resources/Hapio/UI/TouchInkArt 하나를 읽는다.
    /// 크기는 다른 화면과 같은 기준 화면(390 × 844 pt) 단위이고, Play 중에 바꿔도 다음 터치부터 바로 보인다.
    /// </summary>
    [CreateAssetMenu(fileName = "TouchInkArt", menuName = "Hapio/Touch Ink Art")]
    public sealed class GameTouchInkArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/UI/TouchInkArt";

        [Header("그림")]
        [Tooltip("가운데 먹 얼룩 그림들. 터치마다 하나를 무작위로 고른다.")] public Texture2D[] stains = Array.Empty<Texture2D>();
        [Tooltip("주변 작은 먹 방울 그림들. 방울마다 하나를 무작위로 고른다.")] public Texture2D[] droplets = Array.Empty<Texture2D>();
        [Tooltip("먹 색")] public Color inkColor = GameBattleUiColors.Ink;
        [Tooltip("얼룩 그림에서 실제 얼룩이 차지하는 비율. 여백을 뺀 크기를 맞추는 데 쓴다.")] [Range(.05f, 1f)] public float stainFill = .66f;
        [Tooltip("방울 그림에서 실제 방울이 차지하는 비율. 여백을 뺀 크기를 맞추는 데 쓴다.")] [Range(.05f, 1f)] public float dropletFill = .24f;

        [Header("희미한 정도와 시간")]
        [Tooltip("희미한 정도. 막 찍힌 순간의 진하기 (0 = 안 보임, 1 = 가장 진함)")] [Range(0f, 1f)] public float inkStrength = .35f;
        [Tooltip("찍힌 순간부터 완전히 사라질 때까지. 머무는 시간 없이 바로 옅어진다.")] [Min(.05f)] public float fadeSeconds = .6f;
        [Tooltip("막 찍힌 순간의 크기 (최종 모양 1 기준)")] [Min(.01f)] public float startScale = .85f;
        [Tooltip("다 번졌을 때의 크기 (최종 모양 1 기준)")] [Min(.01f)] public float endScale = 1.1f;

        [Header("가운데 얼룩")]
        [Tooltip("가운데 얼룩 크기 (pt)")] [Min(1f)] public float stainSize = 50f;
        [Tooltip("크기가 터치마다 달라지는 정도 (0.2 = ±20%)")] [Range(0f, 1f)] public float stainSizeVariation = .2f;

        [Header("주변 먹 방울")]
        [Tooltip("먹 방울 수 (최소)")] [Min(0)] public int dropletsMin = 3;
        [Tooltip("먹 방울 수 (최대)")] [Min(0)] public int dropletsMax = 6;
        [Tooltip("먹 방울 크기 (pt, 최소)")] [Min(.5f)] public float dropletSizeMin = 7f;
        [Tooltip("먹 방울 크기 (pt, 최대)")] [Min(.5f)] public float dropletSizeMax = 15f;
        [Tooltip("터치한 자리에서 먹 방울까지 거리 (pt, 최소)")] [Min(0f)] public float dropletDistanceMin = 32f;
        [Tooltip("터치한 자리에서 먹 방울까지 거리 (pt, 최대)")] [Min(0f)] public float dropletDistanceMax = 50f;

        [Header("소리")]
        [Tooltip("터치할 때 나는 소리. 비워 두면 소리 없이 먹물만 나온다. 아래 Tap Sounds가 있으면 그쪽을 쓴다.")] public AudioClip tapSound;
        [Tooltip("#629 터치할 때마다 번갈아 나는 소리들 (먹이 종이에 툭 떨어지는 소리 3개). 바로 앞과 같은 소리는 연달아 나지 않는다. 비우면 Tap Sound 하나를 쓴다.")]
        public AudioClip[] tapSounds = new AudioClip[0];
        [Tooltip("#629 터치 소리를 원본보다 몇 배 크게 낼지 (1 = 원본 그대로). Sound Volume(최대 1)으로 부족할 때 올린다. 너무 올리면 소리가 찢어진다.")]
        [Range(1f, 8f)] public float tapLoudness = 1f;
        [Tooltip("#629 터치 소리의 빠르기 (1 = 원본, 0.5 = 절반 속도). 테이프를 천천히 돌리듯 느려질수록 길어지고 낮아진다.")]
        [Range(.1f, 2f)] public float tapSpeed = 1f;

        /// <summary>#629: 이번 터치에 낼 소리. 여러 개면 바로 앞(<paramref name="previous"/>)과 다른 것을 무작위로 고른다.</summary>
        public AudioClip PickTapSound(int previous, out int picked)
        {
            picked = -1;
            int count = 0;
            if (tapSounds != null) foreach (AudioClip clip in tapSounds) if (clip != null) count++;
            if (count == 0) return tapSound;
            int skip = count > 1 && previous >= 0 ? 1 : 0;
            int roll = UnityEngine.Random.Range(0, count - skip);
            for (int i = 0, n = 0; i < tapSounds.Length; i++)
            {
                if (tapSounds[i] == null) continue;
                if (skip == 1 && i == previous) continue;
                if (n++ == roll) { picked = i; return tapSounds[i]; }
            }
            return tapSound;
        }
        [Tooltip("터치 소리 크기 (0 = 안 남, 1 = 원본 크기). 게임의 효과음 크기 설정이 여기에 곱해진다.")] [Range(0f, 1f)] public float soundVolume = .5f;

        [Header("연타")]
        [Tooltip("동시에 보이는 번짐 수. 넘으면 가장 오래된 것부터 지우고 새로 찍는다.")] [Min(1)] public int maxMarks = 8;

        public static GameTouchInkArt Load() => Resources.Load<GameTouchInkArt>(ResourcePath);
    }
}
