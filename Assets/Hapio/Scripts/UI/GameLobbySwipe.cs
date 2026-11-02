using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hapio
{
    /// <summary>3:3 자리 화면 (#394): 팀 쪽을 좌우로 쓸어 넘긴다. 짧게 끌거나 세로로 끈 것은 넘기지 않는다.</summary>
    public sealed class GameLobbySwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("화면 너비 대비 이만큼 넘게 옆으로 끌어야 쪽이 넘어간다.")]
        public float minimumScreenFraction = .08f;

        /// <summary>true면 손가락이 왼쪽으로 가서 오른쪽(다음) 쪽을, false면 왼쪽(앞) 쪽을 연다.</summary>
        public event Action<bool> Swiped;

        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data) { }

        public void OnEndDrag(PointerEventData data)
        {
            Vector2 delta = data.position - data.pressPosition;
            if (Mathf.Abs(delta.x) < Screen.width * minimumScreenFraction || Mathf.Abs(delta.x) <= Mathf.Abs(delta.y)) return;
            Swiped?.Invoke(delta.x < 0f);
        }
    }
}
