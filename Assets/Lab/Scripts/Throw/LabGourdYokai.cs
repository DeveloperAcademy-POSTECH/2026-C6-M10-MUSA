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
    /// 박이 폭탄을 떨어뜨리면(#250, 각 기기가 수신하는 `GourdBombDropped`) "!" 말풍선이 떠 있는 동안
    /// 평소보다 빠르게 쏙 올라와 톡 튀고 두리번거린다. 콤보 중이면 들어가지 않고 톡 튀기만 하며, 콤보와 폭탄이
    /// 둘 다 끝나야 들어간다. 통합 방해공작 추첨(#251)이 넘기기 막기·여우불·매혹을 걸 때도 똑같이 나온다
    /// (폭탄은 위의 `GourdBombDropped`로 이미 나온다).
    /// 야광귀 섬광(#268) 예고가 시작되면 모든 기기에서 폭탄 때처럼 쏙 올라와 두리번거리다가, 번쩍이는 순간 톡 튀고
    /// 잠시 뒤 들어간다(섬광을 누가 맞든 박은 모두에게 보인다). 시각은 Host 스냅샷(`flashWarningAt`/`flashAt`/`flashLastAt`)이다.
    /// 섬광 흔들어 풀기(#301)가 켜져 있으면 한 명이라도 눈부신 동안 계속 나와 두리번거리고(모든 기기, Host 시각 기준),
    /// 누군가 폰을 흔들어 풀 때마다 어지러운 듯 움찔한다.
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

        /// <summary>폭탄을 떨어뜨릴 때 톡 튀는 시간 (초)</summary>
        public const float PopSeconds = .3f;

        [Tooltip("박이 폭탄을 떨어뜨릴 때 요괴가 나와 있는 시간 (초). \"!\" 말풍선(LabGourdAlert.showSeconds)과 맞춘다.")]
        [SerializeField, Min(.3f)] private float bombShowSeconds = 1.4f;
        [Tooltip("폭탄 때 올라오는 빠르기 (평소 = 1). 클수록 쏙 올라온다.")]
        [SerializeField, Min(1f)] private float bombRiseSpeed = 3f;
        [Tooltip("야광귀 섬광(#268)이 번쩍인 뒤 요괴가 더 나와 있는 시간 (초). 그 뒤 들어간다.")]
        [SerializeField, Min(0f)] private float flashStaySeconds = .6f;

        private const float EyeAngle = 24f;
        private const float FlinchSquash = .16f;
        private const float PopStretch = .22f;
        private const float FlinchHop = .0006f; // YokaiRoot 기준 (부모가 162배라 화면에서는 약 0.1)

        private LabNetwork network;
        private Animation clipPlayer;
        private AnimationState state;
        private Transform yokai;
        private Vector3 yokaiScale = Vector3.one;
        private bool ready;

        private bool holding;
        private bool wasInComboWindow;
        private float cursor;
        private float lookDirection = 1f;
        private bool rewinding;
        private float flinchElapsed = -1f;
        private float effectSeconds = FlinchSeconds;
        private float effectSign = -1f; // -1 움찔(먼저 작아짐), +1 톡 튐(먼저 커짐)
        private float bombLeft;
        private bool popPending;
        private uint seenComboCount;
        private int seenProgress;
        private uint seenFlashCount;
        private uint releaseFlashCount;
        private int seenReleases;

        private LabSnapshot seenSnapshot;
        private double seenAtRealtime;

        /// <summary>콤보 또는 폭탄 때문에 요괴를 붙잡아 두고 있는지</summary>
        public bool Holding => holding;
        /// <summary>움찔하는 중인지</summary>
        public bool Flinching => flinchElapsed >= 0f && effectSign < 0f;
        /// <summary>폭탄 때 톡 튀는 중인지 (아직 올라오는 중이면 다 올라온 뒤 튄다)</summary>
        public bool Popping => flinchElapsed >= 0f && effectSign > 0f;
        /// <summary>폭탄 때문에 남은 나와 있는 시간 (초)</summary>
        public float BombSecondsLeft => bombLeft;
        public float BombShowSeconds => bombShowSeconds;
        /// <summary>지금 애니메이션 시각 (0~6초)</summary>
        public float ClipTime => state != null ? Mathf.Repeat(state.time, ClipLength) : 0f;
        public bool Ready => ready;

        public void Bind(LabNetwork target)
        {
            if (network == target) return;
            if (network != null)
            {
                network.GourdBombDropped -= OnGourdBombDropped;
                network.SabotageCast -= OnSabotageCast;
            }
            network = target;
            if (network != null)
            {
                network.GourdBombDropped += OnGourdBombDropped;
                network.SabotageCast += OnSabotageCast;
            }
        }

        private void OnGourdBombDropped(ulong targetPlayer, string bombId, Vector2 landing) => BombDropped();

        private void OnSabotageCast(LabSabotageKind kind, ulong targetPlayer)
        {
            if (kind != LabSabotageKind.GourdBomb) BombDropped();
        }

        /// <summary>박이 폭탄을 떨어뜨렸다: 말풍선이 떠 있는 동안 나와 있고, 다 올라오면 톡 튄다.</summary>
        public void BombDropped()
        {
            bombLeft = bombShowSeconds;
            popPending = true;
        }

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
            deltaTime = Mathf.Max(0f, deltaTime);
            bool combo = InComboWindow(snapshot, serverNow);
            // 폭탄 때 나와 있는 시간은 전투 중에만 흐른다(일시정지 중에는 deltaTime이 0). 전투가 아니면 바로 끝.
            if (snapshot == null || snapshot.phase != LabPhase.Playing) { bombLeft = 0f; popPending = false; }
            else bombLeft = Mathf.Max(0f, bombLeft - deltaTime);
            bool flash = InFlashWindow(snapshot, serverNow, flashStaySeconds);
            LabConfig config = network != null ? network.config : null;
            // 섬광 흔들어 풀기 (#301): 한 명이라도 눈부신 동안은 계속 나와 있다.
            bool glared = config != null && config.YaguangFlashShakeRelease
                && LabYaguangFlash.AnyGlared(snapshot, serverNow, config.YaguangFlashGlareSeconds);
            bool want = combo || bombLeft > 0f || flash || glared;
            DetectHits(snapshot, combo);
            DetectFlash(snapshot, serverNow);
            DetectRelease(snapshot);
            wasInComboWindow = combo;

            if (want && !holding) Hold();
            else if (!want && holding) Release();

            if (holding)
            {
                // 폭탄·섬광(예고 1.5초)만으로 나올 때는 짧으니 쏙 올라온다. 콤보 예고는 3초라 평소 빠르기.
                StepHeld(deltaTime * (combo ? 1f : bombRiseSpeed), deltaTime);
                state.time = cursor;
                clipPlayer.Sample();
                if (popPending && !rewinding && cursor >= RisenAt)
                {
                    popPending = false;
                    StartEffect(1f, PopSeconds);
                }
            }
            else popPending = false;
            ApplyFlinch(deltaTime);
        }

        /// <summary>예고 중이거나 콤보가 진행 중이면 true (전투 중일 때만)</summary>
        public static bool InComboWindow(LabSnapshot snapshot, double serverNow) =>
            snapshot != null && snapshot.phase == LabPhase.Playing
            && (snapshot.comboActive || snapshot.ComboWarningOn(serverNow));

        /// <summary>
        /// 야광귀 섬광 예고 중이거나, 번쩍인 뒤 stay초가 지나기 전이면 true (전투 중일 때만, #268).
        /// 섬광을 누가 맞든 박은 모두의 화면에 있으므로 대상과 상관없이 나온다.
        /// </summary>
        public static bool InFlashWindow(LabSnapshot snapshot, double serverNow, float staySeconds) =>
            snapshot != null && snapshot.phase == LabPhase.Playing
            && (snapshot.FlashWarningOn(serverNow)
                || (snapshot.flashCount > 0 && snapshot.flashLastAt >= 0d && serverNow >= snapshot.flashLastAt
                    && serverNow - snapshot.flashLastAt < staySeconds));

        /// <summary>새 섬광이 번쩍이면 톡 튄다. 새 라운드나 접속 직후 이미 지난 섬광에는 튀지 않는다.</summary>
        private void DetectFlash(LabSnapshot snapshot, double serverNow)
        {
            if (snapshot == null) return;
            if (snapshot.flashCount < seenFlashCount) seenFlashCount = 0;
            if (snapshot.flashCount == seenFlashCount) return;
            bool fresh = seenFlashCount != 0 || snapshot.flashCount == 1;
            seenFlashCount = snapshot.flashCount;
            if (fresh && holding && serverNow - snapshot.flashLastAt < flashStaySeconds) StartEffect(1f, PopSeconds);
        }

        /// <summary>누군가 폰을 흔들어 눈부심을 풀면(#301) 움찔한다. 새 섬광이면 센 수를 처음부터 다시 센다.</summary>
        private void DetectRelease(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.players == null) return;
            if (snapshot.flashCount != releaseFlashCount)
            {
                releaseFlashCount = snapshot.flashCount;
                seenReleases = 0;
            }
            int releases = 0;
            foreach (LabPlayerState player in snapshot.players)
                if (player != null && snapshot.FlashReleasedAt(player.id) >= 0d) releases++;
            if (releases > seenReleases && holding) StartEffect(-1f, FlinchSeconds);
            seenReleases = releases;
        }

        private void DetectHits(LabSnapshot snapshot, bool currentCombo)
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
            if (snapshot.comboProgress > seenProgress && (currentCombo || wasInComboWindow)) StartEffect(-1f, FlinchSeconds);
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

        private void StartEffect(float sign, float seconds)
        {
            effectSign = sign;
            effectSeconds = seconds;
            flinchElapsed = 0f;
        }

        /// <summary>올라오는(되감는) 동안은 riseTime, 다 올라와 두리번거리는 동안은 lookTime만큼 진행한다.</summary>
        private void StepHeld(float riseTime, float lookTime)
        {
            float deltaTime = riseTime;
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
            deltaTime = lookTime;
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
            float u = flinchElapsed / effectSeconds;
            if (u >= 1f)
            {
                flinchElapsed = -1f;
                yokai.localScale = yokaiScale;
                return;
            }
            // 움찔: 먼저 움츠러들었다가(작아짐) 톡 튀어나오며 원래 크기로.
            // 톡 튐(폭탄): 먼저 커지며 위로 튀었다가 원래 크기로. 둘 다 위로 들썩인다.
            float wave = Mathf.Sin(u * Mathf.PI * 2f) * (1f - u);
            bool pop = effectSign > 0f;
            yokai.localScale = yokaiScale * (pop ? 1f + PopStretch * wave : 1f - FlinchSquash * wave);
            // Add the hop only after sampling the base pose, including paused frames.
            yokai.localPosition += new Vector3(0f, 0f, (pop ? 2f : 1f) * FlinchHop * Mathf.Sin(u * Mathf.PI));
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

        private void OnDestroy()
        {
            if (network == null) return;
            network.GourdBombDropped -= OnGourdBombDropped;
            network.SabotageCast -= OnSabotageCast;
        }

        private void OnDisable()
        {
            wasInComboWindow = false;
            bombLeft = 0f;
            popPending = false;
            if (!ready) return;
            if (holding) Release();
            if (flinchElapsed >= 0f) clipPlayer.Sample();
            flinchElapsed = -1f;
            if (yokai != null) yokai.localScale = yokaiScale;
        }
    }
}
