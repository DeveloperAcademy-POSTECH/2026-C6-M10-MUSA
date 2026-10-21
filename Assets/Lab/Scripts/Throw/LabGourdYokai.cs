using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 속 요괴 (#244). 평소에는 박 프리팹의 6초 반복 애니메이션(`Gourd_Demo`: 뚜껑 열림 → 요괴 올라옴 →
    /// 두리번 → 들어감 → 뚜껑 닫힘)을 그대로 둔다. 콤보 예고(#241)가 시작되면 요괴가 올라와서 콤보가 끝날
    /// 때까지 들어가지 않고 계속 두리번거리며, 콤보 칸을 맞힐 때마다 움찔한다. 콤보가 끝나면 그 자리부터
    /// 애니메이션을 이어서 틀어 들어가고, 다시 평소 반복으로 돌아간다.
    /// 콤보 구간은 Host 스냅샷(`comboWarningAt`/`comboNextAt`/`comboActive`)으로 정하므로 모든 기기에서 같은
    /// 때 나오고 들어간다. 평소 반복은 기기마다 따로 돈다(이전과 같음). 화면 표시만 하며 판정에 관여하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdYokai : MonoBehaviour
    {
        // `Gourd_Demo` 안의 시각 (초). 애니메이션 파일의 키 위치와 맞춘다.
        /// <summary>뚜껑이 열리기 시작하는 시각 (그 전에는 닫혀 있음)</summary>
        public const float LidOpensAt = .5f;
        /// <summary>요괴가 다 올라온 시각</summary>
        public const float RisenAt = 1.8f;
        /// <summary>두리번 구간: 이 사이를 오가면 눈이 왼쪽(+24°)과 오른쪽(-24°)을 번갈아 본다</summary>
        public const float LookFrom = 2f, LookTo = 2.8f;
        /// <summary>눈이 가운데로 돌아온 시각, 요괴가 내려가기 시작하는 시각, 뚜껑이 다 닫힌 시각</summary>
        public const float EyesBackAt = 3.5f, SinkFrom = 3.8f, SunkAt = 4.5f, LidClosedAt = 5.3f;
        public const float ClipLength = 6f;
        /// <summary>움찔하는 시간 (초)</summary>
        public const float FlinchSeconds = .35f;

        private const float EyeAngle = 24f;
        private const float FlinchSquash = .16f;
        private const float FlinchHop = .0006f; // YokaiRoot 기준 (부모가 162배라 화면에서는 약 0.1)

        private LabNetwork network;
        private Animation clipPlayer;
        private AnimationState state;
        private Transform yokai;
        private Vector3 yokaiScale = Vector3.one;
        private bool ready;

        private bool holding;
        private float cursor;
        private float lookDirection = 1f;
        private bool rewinding;
        private float flinchElapsed = -1f;
        private uint seenComboCount;
        private int seenProgress;

        private LabSnapshot seenSnapshot;
        private double seenAtRealtime;

        /// <summary>콤보 때문에 요괴를 붙잡아 두고 있는지 (예고 ~ 콤보 끝)</summary>
        public bool Holding => holding;
        /// <summary>움찔하는 중인지</summary>
        public bool Flinching => flinchElapsed >= 0f;
        /// <summary>지금 애니메이션 시각 (0~6초)</summary>
        public float ClipTime => state != null ? Mathf.Repeat(state.time, ClipLength) : 0f;
        public bool Ready => ready;

        public void Bind(LabNetwork target) => network = target;

        private void Awake() => EnsureReady();

        private bool EnsureReady()
        {
            if (ready) return true;
            clipPlayer = GetComponentInChildren<Animation>(true);
            if (clipPlayer == null || clipPlayer.clip == null) return false;
            state = clipPlayer[clipPlayer.clip.name];
            yokai = clipPlayer.transform.Find("YokaiRoot");
            if (state == null || yokai == null) return false;
            yokaiScale = yokai.localScale;
            ready = true;
            return true;
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool paused = snapshot != null && snapshot.paused;
            double serverNow = snapshot == null ? 0d : paused ? snapshot.serverTime : EstimateServerNow(snapshot);
            Advance(snapshot, serverNow, paused ? 0f : Time.deltaTime);
        }

        /// <summary>
        /// 한 프레임 진행. 스냅샷과 Host 시각으로 콤보 구간인지 보고, 구간이면 애니메이션 시각을 직접 정한다.
        /// 검사에서 시간을 정해 부를 수 있게 공개한다.
        /// </summary>
        public void Advance(LabSnapshot snapshot, double serverNow, float deltaTime)
        {
            if (!EnsureReady()) return;
            bool want = InComboWindow(snapshot, serverNow);
            DetectHits(snapshot, want);

            if (want && !holding) Hold();
            else if (!want && holding) Release();

            if (holding)
            {
                StepHeld(Mathf.Max(0f, deltaTime));
                state.time = cursor;
                clipPlayer.Sample();
            }
            ApplyFlinch(Mathf.Max(0f, deltaTime));
        }

        /// <summary>예고 중이거나 콤보가 진행 중이면 true (전투 중일 때만)</summary>
        public static bool InComboWindow(LabSnapshot snapshot, double serverNow) =>
            snapshot != null && snapshot.phase == LabPhase.Playing
            && (snapshot.comboActive || snapshot.ComboWarningOn(serverNow));

        private void DetectHits(LabSnapshot snapshot, bool want)
        {
            if (snapshot == null) return;
            if (snapshot.comboCount != seenComboCount)
            {
                seenComboCount = snapshot.comboCount;
                // A coalesced snapshot may already include the first hit of this combo.
                // Compare it with a fresh combo's zero progress, while the window/holding
                // check below keeps an old completed combo from replaying on late arrival.
                seenProgress = 0;
            }
            // 칸을 맞혀 진행이 늘면 움찔. 마지막 칸(성공)은 콤보가 끝나도 움찔한 뒤 들어간다.
            if (snapshot.comboProgress > seenProgress && (want || holding)) flinchElapsed = 0f;
            seenProgress = snapshot.comboProgress;
        }

        /// <summary>
        /// 평소 반복의 어느 순간이든 튀지 않게, 지금과 똑같은 모습의 시각에서 시작한다.
        /// 애니메이션 키는 모두 기울기 0이라 키 사이가 S자(smoothstep)로 움직인다.
        /// </summary>
        private void Hold()
        {
            holding = true;
            float t = ClipTime;
            lookDirection = 1f;
            rewinding = false;
            if (t <= LookTo) cursor = t; // 닫힘 · 올라오는 중 · 두리번 앞부분: 그대로 이어서
            else if (t <= EyesBackAt)
            {
                // 눈이 오른쪽에서 가운데로 돌아오는 중: 두리번 구간에서 눈 각도가 같은 곳으로, 왼쪽을 향해.
                float eye = -EyeAngle * (1f - Smooth((t - LookTo) / (EyesBackAt - LookTo)));
                cursor = LookFrom + (LookTo - LookFrom) * InverseSmooth((EyeAngle - eye) / (2f * EyeAngle));
                lookDirection = -1f;
            }
            else if (t <= SinkFrom)
            {
                cursor = (LookFrom + LookTo) * .5f; // 다 올라와 정면을 봄
                lookDirection = -1f;
            }
            else if (t <= LidClosedAt)
            {
                // 내려가는 중이거나 뚜껑이 닫히는 중: 그 자리에서 애니메이션을 거꾸로 돌려 다시 올라온다.
                // 3.8초(다 올라옴 · 정면 · 뚜껑 열림)에 닿으면 모습이 같은 두리번 가운데(2.4초)로 이어진다.
                cursor = t;
                rewinding = true;
            }
            else cursor = LidOpensAt; // 닫혀 있음 → 바로 열리기 시작
            state.speed = 0f;
            state.enabled = true;
            state.weight = 1f;
        }

        /// <summary>기울기 0인 두 키 사이의 곡선 모양 (0~1 → 0~1)</summary>
        public static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        /// <summary>Smooth의 역함수</summary>
        public static float InverseSmooth(float y) =>
            .5f - Mathf.Sin(Mathf.Asin(1f - 2f * Mathf.Clamp01(y)) / 3f);

        private void StepHeld(float deltaTime)
        {
            if (rewinding)
            {
                cursor -= deltaTime;
                if (cursor > SinkFrom) return;
                rewinding = false;
                cursor = (LookFrom + LookTo) * .5f;
                lookDirection = -1f;
                return;
            }
            if (cursor < LookFrom)
            {
                cursor = Mathf.Min(LookFrom, cursor + deltaTime);
                return;
            }
            cursor += deltaTime * lookDirection;
            if (cursor >= LookTo) { cursor = LookTo - (cursor - LookTo); lookDirection = -1f; }
            if (cursor <= LookFrom) { cursor = LookFrom + (LookFrom - cursor); lookDirection = 1f; }
            cursor = Mathf.Clamp(cursor, LookFrom, LookTo);
        }

        /// <summary>
        /// 붙잡은 자리부터 애니메이션을 정방향으로 다시 튼다. 두리번 중이면 눈이 가운데로 돌아온 뒤 들어가고,
        /// 아직 올라오는 중이었으면 다 올라왔다가 평소처럼 들어간다. 이후로는 평소 6초 반복이다.
        /// </summary>
        private void Release()
        {
            holding = false;
            rewinding = false;
            state.time = cursor;
            state.speed = 1f;
            state.enabled = true;
            state.weight = 1f;
        }

        private void ApplyFlinch(float deltaTime)
        {
            if (flinchElapsed < 0f) return;
            // Held poses were sampled by Advance. After Release, sample the ordinary
            // animation's current pose too: a paused Animation does not overwrite the
            // previous frame's hop, so adding to that transform would otherwise drift.
            if (!holding) clipPlayer.Sample();
            flinchElapsed += deltaTime;
            float u = flinchElapsed / FlinchSeconds;
            if (u >= 1f)
            {
                flinchElapsed = -1f;
                yokai.localScale = yokaiScale;
                return;
            }
            // 먼저 움츠러들었다가(작아짐) 톡 튀어나오며 원래 크기로. 위로 살짝 들썩인다.
            float wave = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u);
            yokai.localScale = yokaiScale * (1f - FlinchSquash * wave);
            // Every frame adds the hop to the sampled animation pose, including paused frames.
            yokai.localPosition += new Vector3(0f, 0f, FlinchHop * Mathf.Sin(u * Mathf.PI));
        }

        private double EstimateServerNow(LabSnapshot snapshot)
        {
            double realtime = Time.realtimeSinceStartupAsDouble;
            if (!ReferenceEquals(snapshot, seenSnapshot))
            {
                seenSnapshot = snapshot;
                seenAtRealtime = realtime;
            }
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAtRealtime);
        }

        private void OnDisable()
        {
            if (!ready) return;
            if (holding) Release();
            if (flinchElapsed >= 0f) clipPlayer.Sample();
            flinchElapsed = -1f;
            if (yokai != null) yokai.localScale = yokaiScale;
        }
    }
}
