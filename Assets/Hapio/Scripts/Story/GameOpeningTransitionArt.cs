using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 오프닝이 끝나고 튜토리얼로 넘어갈 때의 연출 값 (#635). Resources/Hapio/Story/OpeningTransitionArt 하나를 읽는다.
    /// - 마지막 장면을 끝까지 보면: 대사창과 위 버튼이 바로 사라지고, 장면이 산골짜기 쪽으로 빠르게 확대되면서 한지 구름이 덮는다.
    /// - 중간에 건너뛰면: 확대 없이 보던 화면 위로 구름만 덮는다.
    /// 구름 자체(모양·경로·덮기 0.62초)는 #630 공용 구름 프리팹에서 고친다.
    /// </summary>
    [CreateAssetMenu(fileName = "OpeningTransitionArt", menuName = "Hapio/Opening Transition Art")]
    public sealed class GameOpeningTransitionArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/Story/OpeningTransitionArt";

        [Header("마지막 장면 확대 (끝까지 보았을 때만)")]
        [Tooltip("장면이 다가가는 곳. 장면(402×874)의 왼쪽 위가 (0, 0), 오른쪽 아래가 (1, 1). 기본은 산봉우리와 절 지붕 사이 골짜기.")]
        public Vector2 zoomFocus = new Vector2(.533f, .254f);
        [Tooltip("확대가 끝났을 때의 배율 (1 = 그대로)")] [Min(1f)] public float zoomScale = 2.5f;
        [Tooltip("확대에 걸리는 시간 (초). 기본은 구름이 다 덮는 시간과 같은 0.62초.")] [Min(.05f)] public float zoomSeconds = .62f;
        [Tooltip("확대 진행 곡선 (가로: 시간 0→1, 세로: 확대 0→1). 기본은 처음에 빠르고 끝에서 부드럽게 멈춘다.")]
        public AnimationCurve zoomCurve = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("확대하면서 다가가는 곳을 화면 가운데로 끌어오는 정도 (0 = 제자리에서 커짐, 1 = 끝날 때 가운데). 장면 밖 빈 곳은 보이지 않게 막는다.")]
        [Range(0f, 1f)] public float focusToCenter = 1f;

        /// <summary>How far the zoom has gone at <paramref name="seconds"/> after it began (0 to 1).</summary>
        public float ZoomAt(float seconds)
        {
            float t = Mathf.Clamp01(seconds / Mathf.Max(.05f, zoomSeconds));
            float eased = zoomCurve != null && zoomCurve.length > 0 ? zoomCurve.Evaluate(t) : 1f - Mathf.Pow(1f - t, 3f);
            return t >= 1f ? 1f : Mathf.Clamp01(eased);
        }

        /// <summary>
        /// The picture's scale and its move (on top of growing about the page's middle) at zoom <paramref name="zoom"/>
        /// (0 to 1). <paramref name="focus"/> is the spot zoomed toward, from the page's middle, y up. The move never lets
        /// the grown picture uncover any part of the page.
        /// </summary>
        public static (float scale, Vector2 move) ZoomPose(float zoom, Vector2 focus, Vector2 pageSize, float maxScale, float toCenter)
        {
            float scale = Mathf.Lerp(1f, Mathf.Max(1f, maxScale), Mathf.Clamp01(zoom));
            // The spot would sit at focus × scale; bring it toward the middle as the zoom goes on.
            Vector2 move = focus * (1f - Mathf.Clamp01(toCenter) * Mathf.Clamp01(zoom)) - focus * scale;
            Vector2 slack = pageSize * .5f * (scale - 1f);
            move.x = Mathf.Clamp(move.x, -slack.x, slack.x);
            move.y = Mathf.Clamp(move.y, -slack.y, slack.y);
            return (scale, move);
        }

        /// <summary>The focus as a point from the page's middle (y up), for a page of <paramref name="pageSize"/>.</summary>
        public Vector2 FocusFromMiddle(Vector2 pageSize) =>
            new Vector2((zoomFocus.x - .5f) * pageSize.x, (.5f - zoomFocus.y) * pageSize.y);

        public static GameOpeningTransitionArt Load() => Resources.Load<GameOpeningTransitionArt>(ResourcePath);
    }
}
