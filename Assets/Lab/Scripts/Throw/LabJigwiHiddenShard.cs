using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// #540: 부적 구슬 옆 두 갈래는 보이지 않는다(구슬은 하나만 파도를 타고 간다). 투사체가 그림(공·꼬리)을 늦게 만들어도
    /// 매 프레임 꺼 둔다. 판정과 칠은 그대로다.
    /// </summary>
    public sealed class LabJigwiHiddenShard : MonoBehaviour
    {
        private void LateUpdate()
        {
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
                if (part.enabled) part.enabled = false;
        }
    }
}
