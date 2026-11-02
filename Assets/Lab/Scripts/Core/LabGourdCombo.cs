using System;

namespace C6Lab
{
    /// <summary>박 색상 콤보 한 판의 끝 (#222).</summary>
    public enum LabComboResult { None, Success, WrongColor, Timeout }

    /// <summary>
    /// 박 색상 콤보 실험 (#222)의 판정만 맡는다. Host의 LabModel이 시각을 정해 Start를 부르고,
    /// 박에 맞은 결합 구슬의 속성을 Judge로 넘긴다. 순서와 색이 모두 맞아야 다음 칸으로 가고,
    /// 다른 색이 맞으면 바로 실패, 제한 시간이 지나면 시간 초과로 실패한다.
    /// #414: 콤보가 나오고 봐주기 초(GraceSeconds) 안에 틀린 색이 맞으면 그 콤보에서 첫 번째 한 번만 봐주고(경고) 콤보를 이어 간다.
    /// 두 번째 틀림이나 봐주기 뒤의 틀림은 지금처럼 실패다.
    /// 시각은 모두 LabModel의 전투 시계(일시정지 시간 제외) 기준이다.
    /// </summary>
    public sealed class LabGourdCombo
    {
        public const int Length = 4;

        private readonly LabOrbElement[] colors = new LabOrbElement[Length];

        /// <summary>지금 진행 중인 콤보가 있는지</summary>
        public bool Active { get; private set; }
        /// <summary>맞힌 칸 수 (0~4)</summary>
        public int Progress { get; private set; }
        public double StartedAt { get; private set; } = -1d;
        public double EndsAt { get; private set; } = -1d;
        /// <summary>이번 라운드에 나온 콤보 수. 새 콤보가 나올 때마다 1씩 는다.</summary>
        public uint Count { get; private set; }
        /// <summary>#414 봐주기 초. Start 때 정해진다. 0이면 봐주지 않는다.</summary>
        public double GraceSeconds { get; private set; }
        /// <summary>#414 이번 라운드에 봐준 틀림 수(모든 콤보를 합침)와 마지막으로 봐준 시각. 화면이 경고를 띄우는 데 쓴다.</summary>
        public uint ForgivenCount { get; private set; }
        public double LastForgivenAt { get; private set; } = -1d;
        private bool forgivenThisCombo;

        /// <summary>마지막으로 끝난 콤보의 결과와, 몇 번째 콤보였는지, 언제 끝났는지</summary>
        public LabComboResult LastResult { get; private set; }
        public uint LastResultCount { get; private set; }
        public double LastResultAt { get; private set; } = -1d;
        /// <summary>틀린 색으로 끝났을 때 그 구슬을 던진 사람. 그 밖에는 0.</summary>
        public ulong LastFailedBy { get; private set; }

        public LabOrbElement ColorAt(int index) => colors[index];
        public LabOrbElement[] Colors => (LabOrbElement[])colors.Clone();

        public void Reset()
        {
            Array.Clear(colors, 0, colors.Length);
            Active = false;
            Progress = 0;
            StartedAt = EndsAt = -1d;
            Count = 0;
            GraceSeconds = 0d;
            ForgivenCount = 0;
            LastForgivenAt = -1d;
            forgivenThisCombo = false;
            LastResult = LabComboResult.None;
            LastResultCount = 0;
            LastResultAt = -1d;
            LastFailedBy = 0;
        }

        /// <summary>새 콤보를 낸다. 색은 화·수·목·금·토(Fire~Earth)만 쓸 수 있다.</summary>
        public void Start(LabOrbElement[] nextColors, double at, double timeLimit, double graceSeconds = 0d)
        {
            if (nextColors == null || nextColors.Length != Length)
                throw new ArgumentException("A combo needs exactly four colors.", nameof(nextColors));
            foreach (LabOrbElement color in nextColors)
                if (color < LabOrbElement.Fire || color > LabOrbElement.Earth)
                    throw new ArgumentOutOfRangeException(nameof(nextColors), "Combo colors must be elements.");
            if (double.IsNaN(at) || double.IsInfinity(at) || !(timeLimit > 0d))
                throw new ArgumentOutOfRangeException(nameof(timeLimit));
            Array.Copy(nextColors, colors, Length);
            Active = true;
            Progress = 0;
            StartedAt = at;
            EndsAt = at + timeLimit;
            GraceSeconds = double.IsNaN(graceSeconds) || graceSeconds < 0d ? 0d : graceSeconds;
            forgivenThisCombo = false;
            Count++;
        }

        /// <summary>
        /// 박에 맞은 결합 구슬 하나를 판정한다. 콤보가 없거나 속성이 없는 구슬(폭탄·릴레이)이면
        /// 아무 일도 없다. 콤보가 끝났으면 그 결과를, 아직 진행 중이면 None을 돌려준다.
        /// </summary>
        public LabComboResult Judge(LabOrbElement element, ulong thrower, double at)
        {
            if (!Active || element < LabOrbElement.Fire || element > LabOrbElement.Earth)
                return LabComboResult.None;
            if (at >= EndsAt) return Finish(LabComboResult.Timeout, EndsAt, 0);
            if (element != colors[Progress])
            {
                // #414: 처음 봐주기 초 안의 첫 틀림은 경고만 하고 넘어간다.
                if (!forgivenThisCombo && at < StartedAt + GraceSeconds)
                {
                    forgivenThisCombo = true;
                    ForgivenCount++;
                    LastForgivenAt = at;
                    return LabComboResult.None;
                }
                return Finish(LabComboResult.WrongColor, at, thrower);
            }
            Progress++;
            return Progress >= Length ? Finish(LabComboResult.Success, at, 0) : LabComboResult.None;
        }

        /// <summary>Cancel a scheduled pattern without a success/failure result or a new reward/penalty.</summary>
        public void Stop(double at)
        {
            if (!Active) return;
            Active = false;
            EndsAt = Math.Min(EndsAt, at);
        }

        /// <summary>제한 시간이 지났으면 시간 초과로 끝내고 true. 끝난 시각은 EndsAt이다.</summary>
        public bool TryExpire(double now)
        {
            if (!Active || now < EndsAt) return false;
            Finish(LabComboResult.Timeout, EndsAt, 0);
            return true;
        }

        private LabComboResult Finish(LabComboResult result, double at, ulong failedBy)
        {
            Active = false;
            LastResult = result;
            LastResultCount = Count;
            LastResultAt = at;
            LastFailedBy = failedBy;
            return result;
        }
    }
}
