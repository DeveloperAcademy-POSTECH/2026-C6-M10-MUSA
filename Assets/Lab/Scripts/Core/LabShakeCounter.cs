using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 폰 흔들기 세기 (#301, 야광귀 섬광 해제). 가속도계 값을 받아 "세게 한 번 흔들었다"를 센다.
    /// 천천히 따라가는 평균으로 중력(폰이 놓인 방향)을 빼고, 남은 흔들림이 중력 크기의 threshold배를 넘으면 한 번으로 친다.
    /// 중력 크기로 나눠 보므로 단위(g 또는 m/s²)와 상관없다. 같은 흔들림이 여러 번 세지지 않게 minGap초 안에는 다시 세지 않는다.
    /// 화면·네트워크와 분리해 Editor 테스트에서도 그대로 쓴다.
    /// </summary>
    public sealed class LabShakeCounter
    {
        /// <summary>흔들림이 중력의 몇 배를 넘으면 한 번으로 칠지 (기본 1배)</summary>
        public float Threshold = 1f;
        /// <summary>한 번 센 뒤 다시 세기까지 기다리는 시간 (초)</summary>
        public float MinGap = .14f;
        /// <summary>중력을 따라가는 빠르기 (0~1, 클수록 빨리 따라감)</summary>
        public float GravityFollow = .08f;

        private Vector3 gravity;
        private bool hasGravity;
        private float lastCountAt = float.NegativeInfinity;

        /// <summary>지금까지 센 횟수</summary>
        public int Count { get; private set; }

        /// <summary>센 횟수와 중력 추정을 처음으로 돌린다 (새 섬광마다).</summary>
        public void Reset()
        {
            Count = 0;
            hasGravity = false;
            lastCountAt = float.NegativeInfinity;
        }

        /// <summary>가속도 한 번을 넣는다. 이번에 한 번 흔든 것으로 셌으면 true.</summary>
        public bool Feed(Vector3 acceleration, float time)
        {
            if (!IsFinite(acceleration)) return false;
            if (!hasGravity)
            {
                gravity = acceleration;
                hasGravity = true;
                return false;
            }
            Vector3 shake = acceleration - gravity;
            gravity = Vector3.Lerp(gravity, acceleration, Mathf.Clamp01(GravityFollow));
            float scale = Mathf.Max(gravity.magnitude, .01f);
            if (shake.magnitude / scale < Threshold || time - lastCountAt < MinGap) return false;
            lastCountAt = time;
            Count++;
            return true;
        }

        /// <summary>센서가 없을 때(Editor 키 등) 한 번 흔든 것으로 센다.</summary>
        public void AddOne() => Count++;

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
