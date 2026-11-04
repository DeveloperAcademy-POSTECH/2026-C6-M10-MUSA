using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 속 요괴 애니메이션의 시간표와 움찔 대상 (#507). 트리거(콤보·폭탄·방해·섬광·흔들어 풀기)는 모델과 무관하게
    /// <see cref="LabGourdYokai"/>가 정하고, 여기에는 모델마다 다른 클립 안의 시각, 움찔·튀어나옴 대상 경로, 들썩임 축·크기만 둔다.
    /// <see cref="Legacy"/>는 흰 박(`Gourd_Demo`)의 기존 상수 그대로다.
    /// </summary>
    [CreateAssetMenu(menuName = "C6 Lab/Gourd Yokai Profile")]
    public sealed class LabGourdYokaiProfile : ScriptableObject
    {
        [Tooltip("뚜껑이 열리기 시작하는 클립 시각 (그 전에는 닫혀 있음)")]
        public float lidOpensAt = LabGourdYokai.LidOpensAt;
        [Tooltip("요괴가 다 올라온 시각")]
        public float risenAt = LabGourdYokai.RisenAt;
        [Tooltip("두리번 구간 시작·끝. 이 사이를 오가며 좌우를 본다.")]
        public float lookFrom = LabGourdYokai.LookFrom, lookTo = LabGourdYokai.LookTo;
        [Tooltip("눈이 가운데로 돌아온 시각, 내려가기 시작하는 시각, 다 내려간 시각, 뚜껑이 다 닫힌 시각")]
        public float eyesBackAt = LabGourdYokai.EyesBackAt, sinkFrom = LabGourdYokai.SinkFrom,
            sunkAt = LabGourdYokai.SunkAt, lidClosedAt = LabGourdYokai.LidClosedAt;
        [Tooltip("클립 길이 (초)")]
        public float clipLength = LabGourdYokai.ClipLength;
        [Tooltip("두리번 구간 양 끝의 눈 각도 (도). 두리번 중 붙잡을 때 같은 모습의 시각을 찾는 데 쓴다.")]
        public float eyeAngle = LabGourdYokai.LegacyEyeAngle;
        [Tooltip("움찔·튀어나옴 대상. 요괴 Animation이 붙은 transform 기준 경로.")]
        public string bodyPath = LabGourdYokai.LegacyBodyPath;
        [Tooltip("움찔·튀어나옴 때 들썩이는 방향 (대상의 로컬 축)")]
        public Vector3 hopAxis = Vector3.forward;
        [Tooltip("들썩이는 크기 (대상 로컬 단위)")]
        public float hop = LabGourdYokai.LegacyHop;
        [Tooltip("평소에 클립을 반복하는지. 끄면 평소에는 뚜껑이 닫힌 채로 있다가 게임 이벤트에만 나온다.")]
        public bool idleLoop = true;

        private static LabGourdYokaiProfile legacy;

        /// <summary>흰 박의 기존 상수로 만든 시간표. 저장하지 않는 런타임 인스턴스다.</summary>
        public static LabGourdYokaiProfile Legacy
        {
            get
            {
                if (legacy == null)
                {
                    legacy = CreateInstance<LabGourdYokaiProfile>();
                    legacy.name = "Legacy white gourd yokai";
                    legacy.hideFlags = HideFlags.DontSave;
                }
                return legacy;
            }
        }

        /// <summary>시각이 순서대로이고 유한한지. 잘못된 시간표는 박 모델 계약 검사에서 흰 박으로 돌아가게 한다.</summary>
        public bool IsValid(out string reason)
        {
            float[] order = { 0f, lidOpensAt, risenAt, lookFrom, lookTo, eyesBackAt, sinkFrom, sunkAt, lidClosedAt, clipLength };
            for (int i = 1; i < order.Length; i++)
            {
                if (float.IsNaN(order[i]) || float.IsInfinity(order[i])) { reason = "A yokai time is not finite."; return false; }
                if (order[i] < order[i - 1]) { reason = "Yokai times must be in order (index " + i + ")."; return false; }
            }
            if (!(lookTo > lookFrom)) { reason = "The look-around window must have a length."; return false; }
            if (!(clipLength > 0f)) { reason = "The clip length must be positive."; return false; }
            if (float.IsNaN(eyeAngle) || float.IsInfinity(eyeAngle) || float.IsNaN(hop) || float.IsInfinity(hop)
                || float.IsNaN(hopAxis.x) || float.IsNaN(hopAxis.y) || float.IsNaN(hopAxis.z))
            { reason = "The eye angle or hop is not finite."; return false; }
            if (string.IsNullOrEmpty(bodyPath)) { reason = "The yokai body path is empty."; return false; }
            reason = null;
            return true;
        }
    }
}
