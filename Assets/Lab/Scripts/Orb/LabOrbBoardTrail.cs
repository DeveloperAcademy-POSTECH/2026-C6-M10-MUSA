using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 조합판 위 결합(오행) 구슬의 얕은 꼬리 (#254). 던진 구슬에 붙는 오행 먹 꼬리(`LabElementFlightTrail`)와
    /// 같은 그림을 판 구슬 크기에 맞춰 작게 쓰고, 구슬이 움직일 때만, 빠를수록 진하게 보인다. 멈추면 사라진다.
    /// 음양 구슬·아이템·북에는 붙지 않는다. DEV `Board Orb Trail`로 켜고 끄며, 크기와 진하기는 이 Inspector에서
    /// 정한다. 화면 표시만 하며 물리·판정·통신에 관여하지 않는다(속도는 화면 위 위치 변화로 잰다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabOrbBoardTrail : MonoBehaviour
    {
        [Tooltip("던진 구슬 꼬리 대비 크기 (판 구슬 크기에 맞춘 뒤 곱함). 작을수록 얕다.")]
        [SerializeField, Range(.1f, 2f)] private float size = .6f;
        [Tooltip("가장 빠를 때의 진하기 (던진 구슬 꼬리 = 1)")]
        [SerializeField, Range(0f, 1f)] private float strength = .55f;
        [Tooltip("이 속도(초당 구슬 반지름 몇 개)보다 느리면 꼬리가 없다")]
        [SerializeField, Min(0f)] private float fadeInSpeed = 1.5f;
        [Tooltip("이 속도(초당 구슬 반지름 몇 개) 이상이면 가장 진하다")]
        [SerializeField, Min(.01f)] private float fullSpeed = 10f;

        private LabOrbView view;
        private LabElementFlightTrail trail;
        private LabElementTrailProfile scaled;
        private LabElementTrailProfile source;
        private float scaledFor = -1f;
        private bool on;
        private Vector3 lastPosition;
        private bool hasLast;

        public LabElementFlightTrail Trail => trail;
        public bool On => on && trail != null && trail.IsEmitting;
        public float Intensity => trail != null ? trail.Intensity : 0f;
        public float Size => size;
        public float Strength => strength;

        /// <summary>
        /// 판이 매 프레임 부른다. 켜져 있고 결합 구슬이면 그 오행 꼬리를 (판 반지름 / 던진 공 반지름 × size)
        /// 크기로 붙이고, 아니면 지운다.
        /// </summary>
        public void Sync(LabOrbView orb, LabOrbAppearance look, bool enabledByDev, float boardToThrowScale)
        {
            view = orb;
            LabElementTrailProfile wanted = enabledByDev && orb != null && orb.Kind == LabOrbKind.Combined
                && look != null ? look.ElementTrailFor(orb.Element) : null;
            float scale = boardToThrowScale * size;
            if (wanted == null)
            {
                TurnOff();
                return;
            }
            if (wanted != source || !Mathf.Approximately(scale, scaledFor) || trail == null || !trail.IsEmitting)
            {
                ReleaseProfile();
                source = wanted;
                scaledFor = scale;
                scaled = wanted.CreateScaled(scale);
                EnsureTrail();
                trail.Begin(orb.transform, scaled);
                trail.Intensity = 0f;
                hasLast = false;
            }
            on = true;
        }

        /// <summary>속도 → 진하기. 0 이하면 0, fullSpeed 이상이면 strength, 그 사이는 부드럽게.</summary>
        public float IntensityFor(float radiiPerSecond)
        {
            float u = Mathf.InverseLerp(fadeInSpeed, Mathf.Max(fadeInSpeed + .01f, fullSpeed), radiiPerSecond);
            return strength * u * u * (3f - 2f * u);
        }

        /// <summary>한 프레임 진행 (검사에서 시간을 정해 부를 수 있게 공개).</summary>
        public void Step(float deltaTime)
        {
            if (!on || trail == null || view == null) return;
            Vector3 position = view.transform.position;
            if (!hasLast || deltaTime <= 0f)
            {
                lastPosition = position;
                hasLast = true;
                if (deltaTime <= 0f) trail.Intensity = 0f;
                return;
            }
            float radius = view.HitSphere != null && view.HitSphere.radius > 0f ? view.HitSphere.radius : .32f;
            Vector3 moved = position - lastPosition;
            moved.z = 0f;
            lastPosition = position;
            trail.Intensity = IntensityFor(moved.magnitude / deltaTime / radius);
        }

        private void LateUpdate() => Step(Time.deltaTime);

        private void EnsureTrail()
        {
            if (trail != null) return;
            var holder = new GameObject("Board Element Trail");
            holder.layer = gameObject.layer;
            holder.transform.SetParent(transform, false);
            trail = holder.AddComponent<LabElementFlightTrail>();
        }

        private void TurnOff()
        {
            on = false;
            hasLast = false;
            if (trail != null)
            {
                trail.Intensity = 0f;
                trail.Clear();
            }
            ReleaseProfile();
        }

        private void ReleaseProfile()
        {
            if (scaled != null) Destroy(scaled);
            scaled = null;
            source = null;
            scaledFor = -1f;
        }

        private void OnDisable() => TurnOff();

        private void OnDestroy()
        {
            ReleaseProfile();
            if (trail != null) Destroy(trail.gameObject);
        }
    }
}
