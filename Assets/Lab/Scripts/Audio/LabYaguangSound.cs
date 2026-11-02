using UnityEngine;

namespace C6Lab
{
    /// <summary>야광귀 스테이지 효과음의 종류 (#395~). 숫자는 폴더·크기 순서다. 새 소리는 뒤에 붙인다.</summary>
    public enum LabYaguangSfx { FlashWarn, FlashGlow, FlashCount, FlashBurst, FlashTaunt, ShakeBell, ShakeBellFinal, FlashCaught,
        ComboCountOne, ComboCountTwo, ComboCountThree, ComboCountFour, ComboStart, ComboTick, ComboTickFast,
        ComboTaunt, ComboSmallStart, ComboSmallEnd, ComboBigStart, ComboBigEnd, ComboClearedFirst, ComboClearedSecond }

    /// <summary>
    /// 야광귀 섬광 예고와 번쩍 효과음 (#395). Host 스냅샷의 섬광 시각만 보고 소리를 낸다(화면 코드와 따로 돈다).
    /// - 예고 시작 (Audio/YaguangFlashWarn): 박 옆 말풍선에 야광귀 눈이 뜨는 순간, 모든 기기
    /// - 눈이 빛나기 시작 (Audio/YaguangFlashGlow): 예고 30% 지점, 눈이 노랗게 빛나고 가장자리 빛이 차오를 때, 맞을 사람 기기
    /// - 3/2/1 (Audio/YaguangFlashCount): 화면 가운데 숫자가 바뀔 때마다, 맞을 사람 기기. 숫자가 줄수록 음이 높아진다.
    /// - 번쩍 (Audio/YaguangFlashBurst): 섬광이 터진 순간, 맞은 사람 기기 (진동과 같은 때)
    /// #396 섬광 반응:
    /// - "안보이지롱~" 대사 (Audio/YaguangFlashTaunt): 번쩍 뒤 Inspector 지연(기본 0.35초)만큼 늦게, 맞은 사람 기기 (징 뒤에 들리게)
    /// - 흔들기 1~(최대-1)번 (Audio/YaguangShakeBell): 흔든 수가 하나 오를 때마다 같은 무당 방울 (Inspector로 횟수마다 높일 수 있음). 흔든 사람 기기
    /// - 마지막 흔들기 (Audio/YaguangShakeBellFinal): 최대 횟수(기본 10번)를 채운 순간 더 큰 무당 방울. 흔든 사람 기기
    /// - "으악 어지러워~" 대사 (Audio/YaguangFlashCaught): Host가 내 눈부심을 푼 뒤 Inspector 지연(기본 0.2초)만큼 늦게, 푼 사람 기기
    /// #397 박 색상 콤보 예고와 진행 (모든 기기):
    /// - 예고 카운트 (Audio/YaguangComboCount1~4): 남은 초가 바뀔 때마다 야광귀 목소리 "하나/둘/셋/넷" (박 옆 말풍선과 같은 수)
    /// - 콤보 시작 (Audio/YaguangComboStart): 4칸 색이 공개되는 순간 꽹과리 "깽!"
    /// - 제한 시간 (Audio/YaguangComboTick): 콤보가 나온 뒤 Inspector 간격(기본 0.5초)마다 시계 째깍. 남은 시간이 Inspector 초
    ///   (기본 4초) 이하면 (Audio/YaguangComboTickFast, 같은 높이의 째깍) 더 짧은 간격(기본 0.25초)마다 빠른 째깍. 콤보가 끝나면(성공/실패) 바로 멈춘다.
    /// #398 박 색상 콤보 결과 (모든 기기, 콤보는 팀이 같이 하므로 누가 받든 모두 듣는다):
    /// - "메롱~" 대사 (Audio/YaguangComboTaunt): 콤보 실패(틀린 색/시간 초과) 뒤 Inspector 지연(기본 0.3초), 실패 소리 다음에
    /// - SMALL 시작 (Audio/YaguangComboSmallStart): 결과 문구가 사라지고 SMALL 배지가 나오는 때 (결과 + LabGourdComboHud.ResultSeconds)
    /// - SMALL 끝 (Audio/YaguangComboSmallEnd): 페널티가 끝나 공이 원래 크기로 돌아올 때 (가장 늦게 끝나는 사람 기준)
    /// - BIG 시작 (Audio/YaguangComboBigStart): 성공 뒤 BIG 배지와 부적 비가 시작될 때
    /// - BIG 끝 (Audio/YaguangComboBigEnd): 보상이 끝날 때
    /// - 성공 대사 (Audio/YaguangComboClearedFirst "이런!", Audio/YaguangComboClearedSecond "이럴 수가!"): 콤보 성공 뒤
    ///   Inspector 지연(기본 0.8초). 이번 라운드 첫 성공은 "이런!", 그 뒤 성공은 "이럴 수가!" (이 기기에서 본 성공 수 기준).
    ///   다음 콤보 결과가 먼저 나오면 앞 결과의 남은 소리는 내지 않는다.
    /// 그 순간보다 LateSeconds 넘게 늦게 알아챈 기기(늦게 들어옴, 일시정지 뒤)는 내지 않는다. 일시정지 중·시작 대기 중에는
    /// 새로 내지 않는다. 폴더가 비어 있으면 그 소리만 조용히 넘어간다(여러 개면 무작위).
    /// 크기는 LabConfig Inspector의 소리별 크기 × 일시정지 메뉴 효과음 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabYaguangSound : MonoBehaviour
    {
        public const string FlashWarnFolder = "Audio/YaguangFlashWarn";
        public const string FlashGlowFolder = "Audio/YaguangFlashGlow";
        public const string FlashCountFolder = "Audio/YaguangFlashCount";
        public const string FlashBurstFolder = "Audio/YaguangFlashBurst";
        public const string FlashTauntFolder = "Audio/YaguangFlashTaunt";
        public const string ShakeBellFolder = "Audio/YaguangShakeBell";
        public const string ShakeBellFinalFolder = "Audio/YaguangShakeBellFinal";
        public const string FlashCaughtFolder = "Audio/YaguangFlashCaught";
        public const string ComboCountFolder = "Audio/YaguangComboCount";
        public const string ComboStartFolder = "Audio/YaguangComboStart";
        public const string ComboTickFolder = "Audio/YaguangComboTick";
        public const string ComboTickFastFolder = "Audio/YaguangComboTickFast";
        public const string ComboTauntFolder = "Audio/YaguangComboTaunt";
        public const string ComboSmallStartFolder = "Audio/YaguangComboSmallStart";
        public const string ComboSmallEndFolder = "Audio/YaguangComboSmallEnd";
        public const string ComboBigStartFolder = "Audio/YaguangComboBigStart";
        public const string ComboBigEndFolder = "Audio/YaguangComboBigEnd";
        public const string ComboClearedFirstFolder = "Audio/YaguangComboClearedFirst";
        public const string ComboClearedSecondFolder = "Audio/YaguangComboClearedSecond";
        private static readonly string[] Folders = { FlashWarnFolder, FlashGlowFolder, FlashCountFolder, FlashBurstFolder,
            FlashTauntFolder, ShakeBellFolder, ShakeBellFinalFolder, FlashCaughtFolder,
            ComboCountFolder + "1", ComboCountFolder + "2", ComboCountFolder + "3", ComboCountFolder + "4",
            ComboStartFolder, ComboTickFolder, ComboTickFastFolder,
            ComboTauntFolder, ComboSmallStartFolder, ComboSmallEndFolder, ComboBigStartFolder, ComboBigEndFolder,
            ComboClearedFirstFolder, ComboClearedSecondFolder };
        /// <summary>째깍은 그 박자보다 이 초 넘게 늦게 알아채면 건너뛴다 (스냅샷이 늦게 와도 몰아서 울리지 않게).</summary>
        public const double TickLateSeconds = .3d;
        public const double DefaultTickInterval = .5d;
        public const double DefaultTickFastInterval = .25d;
        public const double DefaultTickFastSeconds = 4d;
        /// <summary>그 순간보다 이 초 넘게 늦게 알아채면 내지 않는다.</summary>
        public const double LateSeconds = .5d;

        private AudioClip[][] clips;
        private readonly int[] counts = new int[Folders.Length];
        private AudioSource source, countSource, bellSource;
        private LabNetwork network;

        private string session;
        private uint round;
        private bool started;
        private uint seenFlashCount;
        private double warnFor = double.NaN, glowFor = double.NaN, countFor = double.NaN;
        private int lastCount;
        // #396: 대사를 낼 섬광(섬광 수)과 그 시각(Host 시각). 0이면 기다리는 대사 없음.
        private uint tauntFor, caughtFor, caughtSeen;
        private double tauntAt, caughtAt;
        private int seenShakes;
        // #397 콤보: 예고 카운트를 센 콤보(4칸이 나올 시각)와 마지막 수, 시작음을 낸 콤보 수, 마지막 째깍 시각
        private double comboCountFor = double.NaN;
        private int lastComboCount;
        private uint seenComboCount;
        private double lastTickAt = double.NegativeInfinity;
        // #398 콤보 결과: 마지막으로 본 결과 순번, 그 결과 시각과 효과(-1 SMALL, 1 BIG, 0 없음), 아직 낼 소리
        private uint seenResultCount;
        private double resultAt;
        private int resultEffect;
        private bool tauntDue, effectStartDue, effectEndDue, clearedDue;
        // 이번 라운드에 본 콤보 성공 수 (첫 성공 "이런!", 그 뒤 "이럴 수가!")
        private int clearedCount;
        private LabYaguangFlashHud flashHud;
        private string clockSession;
        private uint clockRound;
        private double seenServerTime = -1d;
        private float seenAt;

        public static string Folder(LabYaguangSfx sfx) => Folders[(int)sfx];
        public int ClipCount(LabYaguangSfx sfx) => clips == null ? 0 : clips[(int)sfx].Length;
        /// <summary>이 소리를 낼 순간을 알아챈 횟수 (테스트·확인용). 소리 파일이 없어도 센다.</summary>
        public int PlayCount(LabYaguangSfx sfx) => counts[(int)sfx];
        /// <summary>마지막으로 낸 3/2/1 숫자 (테스트·확인용). 아직 없으면 0.</summary>
        public int LastCount { get; private set; }

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            clips = new AudioClip[Folders.Length][];
            for (int i = 0; i < Folders.Length; i++) clips[i] = Resources.LoadAll<AudioClip>(Folders[i]);
            source = NewSource();
            // 3/2/1은 숫자마다 높이가 달라서 따로 둔다 (다른 소리의 높이가 바뀌지 않게).
            countSource = NewSource();
            // 흔들기 방울도 횟수마다 높이가 달라서 따로 둔다.
            bellSource = NewSource();
        }

        private AudioSource NewSource()
        {
            AudioSource made = gameObject.AddComponent<AudioSource>();
            made.playOnAwake = false;
            made.spatialBlend = 0f; // 화면 기준 소리
            return made;
        }

        private void Update()
        {
            if (network == null || network.GameStartHeld) return;
            LabSnapshot snapshot = network.Snapshot;
            if (snapshot == null) return;
            ObserveFlash(snapshot, ServerNow(snapshot), network.LocalPlayerId);
            if (flashHud == null) flashHud = GetComponent<LabYaguangFlashHud>();
            if (flashHud != null && network.config != null && snapshot.phase == LabPhase.Playing)
                ObserveShakes(flashHud.ShakeCount, network.config.YaguangFlashShakeCount);
        }

        /// <summary>
        /// 이번 섬광에서 흔든 수(count)가 늘었으면 방울을 낸다 (#396). 최대 횟수(needed)에 닿은 흔들기는 큰 방울, 그 전은
        /// 같은 방울(Inspector 반음 간격이 0보다 크면 횟수마다 높아짐). 한 번에 여러 번 늘었으면 마지막 횟수의 소리 하나만 낸다. 새 섬광이라 수가 줄면 다시 센다.
        /// 낸 소리 종류를 돌려준다 (없으면 null).
        /// </summary>
        public LabYaguangSfx? ObserveShakes(int count, int needed)
        {
            if (count < seenShakes) seenShakes = count;
            if (count <= seenShakes) return null;
            seenShakes = count;
            if (needed > 0 && count >= needed)
            {
                Play(LabYaguangSfx.ShakeBellFinal);
                return LabYaguangSfx.ShakeBellFinal;
            }
            LabConfig config = network != null ? network.config : null;
            float step = config != null ? config.YaguangShakeBellStepSemitones : 0f;
            Play(LabYaguangSfx.ShakeBell, bellSource, BellPitch(count, step));
            return LabYaguangSfx.ShakeBell;
        }

        /// <summary>count번째 흔들기 방울의 재생 속도. 1번이 원래 높이이고 한 번 늘 때마다 step 반음씩 높다 (최대 +12반음).</summary>
        public static float BellPitch(int count, float step)
        {
            float up = Mathf.Min(12f, Mathf.Max(0, count - 1) * Mathf.Clamp(step, 0f, 4f));
            return Mathf.Pow(2f, up / 12f);
        }

        /// <summary>
        /// 스냅샷의 섬광 상태를 보고 낼 소리를 낸다 (테스트에서는 now를 직접 준다). 새 세션·라운드면 처음부터 다시 센다.
        /// </summary>
        public void ObserveFlash(LabSnapshot snapshot, double now, ulong me)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return;
            if (!started || snapshot.sessionId != session || snapshot.round != round)
            {
                started = true;
                session = snapshot.sessionId;
                round = snapshot.round;
                seenFlashCount = snapshot.flashCount;
                warnFor = glowFor = countFor = double.NaN;
                lastCount = 0;
                // 접속 직후 이미 지난 섬광의 대사는 내지 않는다.
                tauntFor = caughtFor = 0;
                caughtSeen = snapshot.flashCount;
                comboCountFor = double.NaN;
                lastComboCount = 0;
                seenComboCount = snapshot.comboCount;
                lastTickAt = double.NegativeInfinity;
                // 접속 직후 이미 지난 콤보 결과의 소리는 내지 않는다.
                seenResultCount = snapshot.comboResultCount;
                tauntDue = effectStartDue = effectEndDue = clearedDue = false;
                clearedCount = 0;
            }
            ObserveCombo(snapshot, now);
            ObserveComboResult(snapshot, now);

            // 번쩍: 섬광 수가 늘었고 이번에 내가 맞았으면 (진동과 같은 기준, 1초 넘게 지난 섬광은 내지 않음).
            if (snapshot.flashCount < seenFlashCount) seenFlashCount = snapshot.flashCount;
            if (snapshot.flashCount > seenFlashCount)
            {
                seenFlashCount = snapshot.flashCount;
                if (snapshot.LastFlashHit(me) && now - snapshot.flashLastAt <= 1d)
                {
                    Play(LabYaguangSfx.FlashBurst);
                    // "안보이지롱~"은 징 뒤에 들리게 조금 늦게 낸다.
                    tauntFor = snapshot.flashCount;
                    tauntAt = snapshot.flashLastAt + TauntDelay;
                }
            }
            ObserveCaught(snapshot, now, me);
            if (!snapshot.paused)
            {
                if (tauntFor != 0 && tauntFor == snapshot.flashCount && now >= tauntAt)
                {
                    tauntFor = 0;
                    if (now - tauntAt <= LateSeconds) Play(LabYaguangSfx.FlashTaunt);
                }
                if (caughtFor != 0 && caughtFor == snapshot.flashCount && now >= caughtAt)
                {
                    caughtFor = 0;
                    if (now - caughtAt <= LateSeconds) Play(LabYaguangSfx.FlashCaught);
                }
            }

            if (snapshot.paused || !snapshot.FlashWarningOn(now)) return;
            double key = snapshot.flashAt;

            // 예고 시작: 모든 기기
            if (warnFor != key)
            {
                warnFor = key;
                if (now - snapshot.flashWarningAt <= LateSeconds) Play(LabYaguangSfx.FlashWarn);
            }
            if (!snapshot.FlashTargets(me)) return;

            // 눈이 빛나기 시작: 화면의 눈 빛(LabYaguangFlashHud.EyeGlowAt)이 켜지는 지점
            double glowAt = snapshot.flashWarningAt
                + (snapshot.flashAt - snapshot.flashWarningAt) * LabYaguangFlashHud.EyesLightFrom;
            if (glowFor != key && now >= glowAt)
            {
                glowFor = key;
                if (now - glowAt <= LateSeconds) Play(LabYaguangSfx.FlashGlow);
            }

            // 3/2/1: 화면 숫자와 같은 계산. 숫자가 바뀔 때마다 한 번.
            if (countFor != key)
            {
                countFor = key;
                lastCount = 0;
            }
            int count = LabYaguangFlashHud.CountdownAt(snapshot, now, me);
            if (count > 0 && count != lastCount)
            {
                lastCount = count;
                if (now - (snapshot.flashAt - count) <= LateSeconds) PlayCount(count);
            }
        }

        /// <summary>
        /// 박 색상 콤보 예고·시작·제한 시간 소리 (#397). 모든 기기에서 같은 Host 시각으로 낸다. 일시정지 중에는 내지 않는다.
        /// </summary>
        private void ObserveCombo(LabSnapshot snapshot, double now)
        {
            // 시작: 콤보 수가 늘었다 = 4칸이 나왔다.
            if (snapshot.comboCount < seenComboCount) seenComboCount = snapshot.comboCount;
            if (snapshot.comboCount > seenComboCount)
            {
                seenComboCount = snapshot.comboCount;
                lastTickAt = snapshot.comboStartedAt;
                if (!snapshot.paused && snapshot.comboStartedAt >= 0d && now - snapshot.comboStartedAt <= LateSeconds)
                    Play(LabYaguangSfx.ComboStart);
            }
            if (snapshot.paused) return;

            // 예고: 남은 초(올림)가 바뀔 때마다 그 수를 야광귀가 센다. 4초보다 길면 넷부터 다시 (말풍선과 같음).
            if (snapshot.ComboWarningOn(now))
            {
                if (comboCountFor != snapshot.comboNextAt)
                {
                    comboCountFor = snapshot.comboNextAt;
                    lastComboCount = 0;
                }
                int count = Mathf.Max(1, Mathf.CeilToInt((float)(snapshot.comboNextAt - now)));
                if (count != lastComboCount)
                {
                    lastComboCount = count;
                    if (now - (snapshot.comboNextAt - count) <= LateSeconds) Play(CountSfx(count));
                }
            }

            // 제한 시간 째깍
            if (!snapshot.comboActive || snapshot.comboStartedAt < 0d || snapshot.comboEndsAt <= snapshot.comboStartedAt) return;
            LabConfig config = network != null ? network.config : null;
            double fastFor = config != null ? config.YaguangComboTickFastSeconds : DefaultTickFastSeconds;
            double every = config != null ? config.YaguangComboTickIntervalSeconds : DefaultTickInterval;
            double fastEvery = config != null ? config.YaguangComboTickFastIntervalSeconds : DefaultTickFastInterval;
            double tick = LatestTickAt(snapshot.comboStartedAt, snapshot.comboEndsAt, now, fastFor, every, fastEvery, out bool fast);
            if (tick <= lastTickAt) return;
            lastTickAt = tick;
            if (now - tick <= TickLateSeconds) Play(fast ? LabYaguangSfx.ComboTickFast : LabYaguangSfx.ComboTick);
        }

        /// <summary>
        /// 박 색상 콤보 결과 소리 (#398): 실패 뒤 "메롱~", SMALL/BIG 배지가 나올 때와 끝날 때. 모든 기기에서 같은 Host 시각으로 낸다.
        /// </summary>
        private void ObserveComboResult(LabSnapshot snapshot, double now)
        {
            if (snapshot.comboResultCount < seenResultCount) seenResultCount = snapshot.comboResultCount;
            if (snapshot.comboResultCount > seenResultCount)
            {
                seenResultCount = snapshot.comboResultCount;
                resultAt = snapshot.comboResultAt;
                bool failed = snapshot.comboResult == LabComboResult.WrongColor || snapshot.comboResult == LabComboResult.Timeout;
                resultEffect = failed ? -1 : snapshot.comboResult == LabComboResult.Success ? 1 : 0;
                tauntDue = failed && resultAt >= 0d;
                clearedDue = resultEffect > 0 && resultAt >= 0d;
                if (clearedDue) clearedCount++;
                effectStartDue = effectEndDue = resultEffect != 0 && resultAt >= 0d
                    && EffectUntil(snapshot, resultEffect, resultAt) > resultAt + LabGourdComboHud.ResultSeconds;
            }
            if (snapshot.paused) return;

            double tauntAt = resultAt + ComboTauntDelay;
            if (tauntDue && now >= tauntAt)
            {
                tauntDue = false;
                if (now - tauntAt <= LateSeconds) Play(LabYaguangSfx.ComboTaunt);
            }
            double clearedAt = resultAt + ClearedDelay;
            if (clearedDue && now >= clearedAt)
            {
                clearedDue = false;
                if (now - clearedAt <= LateSeconds) Play(ClearedSfx(clearedCount));
            }
            double startAt = resultAt + LabGourdComboHud.ResultSeconds;
            if (effectStartDue && now >= startAt)
            {
                effectStartDue = false;
                if (now - startAt <= LateSeconds) Play(resultEffect < 0 ? LabYaguangSfx.ComboSmallStart : LabYaguangSfx.ComboBigStart);
            }
            if (!effectEndDue) return;
            double endAt = EffectUntil(snapshot, resultEffect, resultAt);
            if (now < endAt) return;
            effectEndDue = false;
            if (now - endAt <= LateSeconds && endAt > startAt)
                Play(resultEffect < 0 ? LabYaguangSfx.ComboSmallEnd : LabYaguangSfx.ComboBigEnd);
        }

        /// <summary>
        /// 결과 시각(at) 뒤에 효과(-1 SMALL, 1 BIG)가 가장 늦게 끝나는 시각. 그 효과를 받은 사람이 없으면 at.
        /// </summary>
        public static double EffectUntil(LabSnapshot snapshot, int effect, double at)
        {
            double until = at;
            if (snapshot == null || snapshot.players == null || effect == 0) return until;
            foreach (LabPlayerState player in snapshot.players)
            {
                if (player == null) continue;
                double end = effect < 0 ? player.comboPenaltyUntil : player.comboRewardUntil;
                if (end > until) until = end;
            }
            return until;
        }

        /// <summary>이번 라운드 count번째 콤보 성공의 대사. 1번째는 "이런!", 그 뒤는 "이럴 수가!".</summary>
        public static LabYaguangSfx ClearedSfx(int count) =>
            count <= 1 ? LabYaguangSfx.ComboClearedFirst : LabYaguangSfx.ComboClearedSecond;

        /// <summary>남은 초 count를 야광귀식으로 센 소리 (1 하나, 2 둘, 3 셋, 4 넷, 5부터는 다시 하나부터).</summary>
        public static LabYaguangSfx CountSfx(int count) => LabYaguangSfx.ComboCountOne + (Mathf.Max(1, count) - 1) % 4;

        /// <summary>
        /// 콤보 제한 시간 [start, end]에서 now 이하의 가장 늦은 째깍 시각. 처음에는 start에서 every초마다, 남은 시간이 fastFor초
        /// 이하가 되면 end - fastFor부터 fastEvery초마다 (fast = true). 끝나는 순간(end)에는 울리지 않는다. 아직 없으면 -무한.
        /// </summary>
        public static double LatestTickAt(double start, double end, double now, double fastFor, double every, double fastEvery, out bool fast)
        {
            fast = false;
            if (now <= start || end <= start) return double.NegativeInfinity;
            every = System.Math.Max(.05d, every);
            fastEvery = System.Math.Max(.05d, fastEvery);
            double fastFrom = System.Math.Max(start, end - System.Math.Max(0d, fastFor));
            double t = System.Math.Min(now, end - 1e-6);
            if (t >= fastFrom && fastFor > 0d)
            {
                fast = true;
                return fastFrom + System.Math.Floor((t - fastFrom) / fastEvery + 1e-9) * fastEvery;
            }
            double k = System.Math.Floor((t - start) / every + 1e-9);
            return k >= 1d ? start + k * every : double.NegativeInfinity;
        }

        /// <summary>
        /// Host가 이번 섬광의 내 눈부심을 풀었으면 "으악 어지러워~"를 예약한다 (#396). 섬광마다 한 번, 푼 지 1초 넘게 지나 알아채면
        /// 내지 않는다 (말풍선 LabYaguangFlashHud.CheckCaught와 같은 기준).
        /// </summary>
        private void ObserveCaught(LabSnapshot snapshot, double now, ulong me)
        {
            double released = snapshot.FlashReleasedAt(me);
            if (released < 0d || caughtSeen == snapshot.flashCount) return;
            caughtSeen = snapshot.flashCount;
            if (now - released > 1d) return;
            caughtFor = snapshot.flashCount;
            caughtAt = released + CaughtDelay;
        }

        private double TauntDelay => network != null && network.config != null ? network.config.YaguangFlashTauntDelaySeconds : .35d;
        private double ComboTauntDelay => network != null && network.config != null ? network.config.YaguangComboTauntDelaySeconds : .3d;
        private double ClearedDelay => network != null && network.config != null ? network.config.YaguangComboClearedDelaySeconds : .8d;
        private double CaughtDelay => network != null && network.config != null ? network.config.YaguangFlashCaughtDelaySeconds : .2d;

        /// <summary>이 소리를 한 번 낸다. 소리를 냈으면 true (파일이 없으면 세기만 하고 false).</summary>
        public bool Play(LabYaguangSfx sfx) => Play(sfx, source, 1f);

        /// <summary>3/2/1 숫자 소리. 3이 원래 높이이고 2, 1로 줄수록 Inspector 반음만큼 높아진다.</summary>
        public bool PlayCount(int count)
        {
            LastCount = count;
            LabConfig config = network != null ? network.config : null;
            float step = config != null ? config.YaguangFlashCountStepSemitones : 2f;
            return Play(LabYaguangSfx.FlashCount, countSource, CountPitch(count, step));
        }

        /// <summary>
        /// 숫자 count의 재생 속도. 3 이상은 원래 높이, 그 아래로 한 칸 줄 때마다 step 반음씩 높다(2는 +step, 1은 +2×step).
        /// </summary>
        public static float CountPitch(int count, float step)
        {
            int up = Mathf.Clamp(3 - count, 0, 2);
            return Mathf.Pow(2f, up * Mathf.Clamp(step, 0f, 12f) / 12f);
        }

        private bool Play(LabYaguangSfx sfx, AudioSource player, float pitch)
        {
            counts[(int)sfx]++;
            AudioClip[] set = clips != null ? clips[(int)sfx] : null;
            if (set == null || set.Length == 0 || player == null) return false;
            player.pitch = pitch;
            player.PlayOneShot(set[Random.Range(0, set.Length)], Volume(sfx) * LabAudioSettings.SfxVolume);
            return true;
        }

        private float Volume(LabYaguangSfx sfx)
        {
            LabConfig config = network != null ? network.config : null;
            if (config == null) return 1f;
            switch (sfx)
            {
                case LabYaguangSfx.FlashWarn: return config.YaguangFlashWarnSoundVolume;
                case LabYaguangSfx.FlashGlow: return config.YaguangFlashGlowSoundVolume;
                case LabYaguangSfx.FlashCount: return config.YaguangFlashCountSoundVolume;
                case LabYaguangSfx.FlashTaunt: return config.YaguangFlashTauntSoundVolume;
                case LabYaguangSfx.ShakeBell: return config.YaguangShakeBellSoundVolume;
                case LabYaguangSfx.ShakeBellFinal: return config.YaguangShakeBellFinalSoundVolume;
                case LabYaguangSfx.FlashCaught: return config.YaguangFlashCaughtSoundVolume;
                case LabYaguangSfx.ComboCountOne:
                case LabYaguangSfx.ComboCountTwo:
                case LabYaguangSfx.ComboCountThree:
                case LabYaguangSfx.ComboCountFour: return config.YaguangComboCountSoundVolume;
                case LabYaguangSfx.ComboStart: return config.YaguangComboStartSoundVolume;
                case LabYaguangSfx.ComboTick: return config.YaguangComboTickSoundVolume;
                case LabYaguangSfx.ComboTickFast: return config.YaguangComboTickFastSoundVolume;
                case LabYaguangSfx.ComboTaunt: return config.YaguangComboTauntSoundVolume;
                case LabYaguangSfx.ComboSmallStart: return config.YaguangComboSmallStartSoundVolume;
                case LabYaguangSfx.ComboSmallEnd: return config.YaguangComboSmallEndSoundVolume;
                case LabYaguangSfx.ComboBigStart: return config.YaguangComboBigStartSoundVolume;
                case LabYaguangSfx.ComboBigEnd: return config.YaguangComboBigEndSoundVolume;
                case LabYaguangSfx.ComboClearedFirst:
                case LabYaguangSfx.ComboClearedSecond: return config.YaguangComboClearedSoundVolume;
                default: return config.YaguangFlashBurstSoundVolume;
            }
        }

        // 스냅샷 사이에도 Host 시각을 이어서 어림한다 (LabYaguangFlashHud와 같은 방식, 최대 1초, 일시정지면 멈춤).
        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.paused || snapshot.sessionId != clockSession || snapshot.round != clockRound
                || snapshot.serverTime != seenServerTime)
            {
                clockSession = snapshot.sessionId;
                clockRound = snapshot.round;
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.paused ? snapshot.serverTime : snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }
    }
}
