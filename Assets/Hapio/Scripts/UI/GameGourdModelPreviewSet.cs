using System;
using UnityEngine;

namespace Hapio
{
    public enum GameGourdModelFit
    {
        /// <summary>작가가 맞춘 크기(게임 박 높이)를 그대로 쓰고, 프리팹 원점을 흰 박 가운데에 둔다.</summary>
        Authored,
        /// <summary>가장 큰 렌더러(몸통)의 높이와 가운데를 흰 박에 맞춘다. 크기를 맞추지 않은 모델용.</summary>
        MatchBody
    }

    [Serializable]
    public sealed class GameGourdModelChoice
    {
        public string label = "모델";
        public GameObject prefab;
        public GameGourdModelFit fit = GameGourdModelFit.Authored;
        [Tooltip("맞춘 크기에 곱하는 배율")]
        [Range(.3f, 3f)] public float scale = 1f;
        [Tooltip("박 기준 위치 보정 (박 로컬 단위)")]
        public Vector3 offset;
        [Tooltip("박 기준 좌우 회전(도)")]
        [Range(-180f, 180f)] public float yaw;
        [Tooltip("0이면 애니메이션 컨트롤러대로 재생. 0보다 크면 이 초마다 처음부터 다시 재생 (한 번만 재생되는 애니메이션 확인용)")]
        [Min(0f)] public float replaySeconds;
    }

    /// <summary>
    /// 보여주기용 박 모델 비교 설정 (#452 preview). Play 중에 Inspector에서 바꿔도 바로 반영된다.
    /// 도색/충돌/네트워크 로직과는 연결되지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Hapio/Gourd Model Preview")]
    public sealed class GameGourdModelPreviewSet : ScriptableObject
    {
        public const string ResourcesPath = "Hapio/GourdPreview/GourdModelPreviewSet";

        [Tooltip("0 = 기존 흰 박, 1 = models[0], 2 = models[1] ...")]
        [Min(0)] public int show = 2;
        [Tooltip("켜면 야광귀 스테이지에서만 바꿔 보여 준다. 끄면 모든 스테이지에서 보여 준다 (빠른 확인용).")]
        public bool onlyYaguang = true;
        [Tooltip("보이는 박 모델 크기 배율 (모델별 Scale에 곱해짐). Match Hitbox가 켜져 있으면 히트박스도 같은 배율로 커진다.")]
        [Range(.3f, 3f)] public float gourdScale = 1f;
        [Tooltip("켜면 공이 맞는 히트박스도 보이는 박과 같은 배율(Gourd Scale × 모델 Scale)로 키운다 (#469). 물감 방향 계산은 그대로 맞다.")]
        public bool matchHitbox = true;
        [Tooltip("화면 왼쪽에 '박: ○○' 버튼을 띄워 눌러서 바꿔 볼 수 있게 한다 (에디터/개발 빌드에서만).")]
        public bool showButton;
        [Tooltip("숨은 흰 박에 칠해지는 물감을 새 모델 위에 덧그린다.")]
        public bool showPaint = true;
        [Tooltip("꺼져 있으면 몸통(가장 큰 부품)에만, 켜면 줄/리본/눈 등 모든 부품에 물감을 덧그린다.")]
        public bool paintAllParts;
        [Range(0f, 1f)] public float paintStrength = 1f;
        [Tooltip("#455의 새 조명(주변광 회색 단색 0.5 + 해 밝기 1.5). 끄면 예전 조명(하늘 주변광 + 해 밝기 1.35)으로 본다. Battle 씬은 바꾸지 않는다.")]
        public bool newLighting = true;
        public GameGourdModelChoice[] models = Array.Empty<GameGourdModelChoice>();

        public int Count => (models != null ? models.Length : 0) + 1;

        public GameGourdModelChoice ChoiceAt(int index) =>
            models != null && index >= 1 && index <= models.Length ? models[index - 1] : null;

        public string LabelAt(int index)
        {
            if (index == 0) return "기존";
            GameGourdModelChoice choice = ChoiceAt(index);
            return choice != null && !string.IsNullOrEmpty(choice.label) ? choice.label : index.ToString();
        }
    }
}
