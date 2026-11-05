using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 튜토리얼 버튼 (#477 건너뛰기 창의 "계속 배우기"·"건너뛰기"): 손가락을 대고 있는 동안(마우스는 올려 둔 동안) 살짝 커지고,
    /// 떼거나 벗어나면 제 크기로 돌아온다. 크기만 바꾸고 눌림 처리는 버튼이 그대로 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameTutorialPressScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("손가락을 대고 있는 동안의 크기 (제 크기 1)")] [Min(1f)] public float pressedScale = 1.08f;
        [Tooltip("커지거나 돌아오는 데 걸리는 시간")] [Min(.01f)] public float seconds = .08f;

        private bool over, down;
        private Selectable selectable;

        /// <summary>A finger (or the mouse) is on the button and it can be pressed.</summary>
        public bool Raised => (over || down) && (selectable == null || selectable.IsInteractable());

        private void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData _) => over = true;
        public void OnPointerExit(PointerEventData _) => over = false;
        public void OnPointerDown(PointerEventData _) => down = true;
        public void OnPointerUp(PointerEventData _) => down = false;

        private void Update()
        {
            float target = Raised ? pressedScale : 1f;
            transform.localScale = Vector3.one * Approach(transform.localScale.x, target, pressedScale, seconds, Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            over = down = false;
            transform.localScale = Vector3.one;
        }

        /// <summary>One frame toward <paramref name="target"/>: the whole way from 1 to <paramref name="raised"/> takes <paramref name="seconds"/>.</summary>
        public static float Approach(float current, float target, float raised, float seconds, float deltaTime)
        {
            float span = Mathf.Max(.0001f, raised - 1f);
            return seconds <= 0f ? target : Mathf.MoveTowards(current, target, span * deltaTime / seconds);
        }
    }
}
