using System;

namespace C6Lab
{
    /// <summary>
    /// 판 하나의 기믹 기록 (#311, UT용). Host 모델이 기믹을 걸 때마다 센다. 걸 사람이 없어 건너뛴 차례는 세지 않는다.
    /// 기록을 밖으로 남기는 것은 <see cref="LabConfig.RecordGimmickStats"/>가 켜진 Host뿐이며 기본은 꺼져 있다.
    /// JsonUtility로 한 줄씩 남기므로 필드 이름이 그대로 기록의 열 이름이 된다.
    /// </summary>
    [Serializable]
    public sealed class LabGimmickStats
    {
        /// <summary>Version 2 includes the Yaguang flash/combo casts in total.</summary>
        public int schemaVersion = 2;
        public string schedulePolicy = "periodic";
        public float timetableEndSeconds;
        public string sessionId = string.Empty;
        public uint round;
        /// <summary>요괴 스테이지(None·Gumiho·Yaguang·Jigwi) 또는 2:2 땅따먹기면 Territory.</summary>
        public string stage = string.Empty;
        public int players;
        /// <summary>Playing·Victory·Defeat. 판이 끝난 뒤 남기므로 보통 Victory나 Defeat다.</summary>
        public string result = string.Empty;
        /// <summary>전투가 이어진 시간(초). 일시정지 시간은 들지 않는다.</summary>
        public double seconds;
        public float commonInterval, uniqueInterval;
        /// <summary>이 판에 건 기믹 수(모든 종류 합).</summary>
        public int total;
        public int gourdBomb, swipeBlock, foxFire, charm;
        public int flash, combo;
        /// <summary>공용 기믹과 고유 기믹이 같은 시각에 둘 다 걸린 횟수.</summary>
        public int simultaneous;

        public int CountOf(LabSabotageKind kind) =>
            kind == LabSabotageKind.GourdBomb ? gourdBomb
            : kind == LabSabotageKind.SwipeBlock ? swipeBlock
            : kind == LabSabotageKind.FoxFire ? foxFire
            : kind == LabSabotageKind.Charm ? charm : 0;

        internal void Add(LabSabotageKind kind)
        {
            total++;
            if (kind == LabSabotageKind.GourdBomb) gourdBomb++;
            else if (kind == LabSabotageKind.SwipeBlock) swipeBlock++;
            else if (kind == LabSabotageKind.FoxFire) foxFire++;
            else if (kind == LabSabotageKind.Charm) charm++;
        }

        /// <summary>Count a unique pattern once when its warning actually begins, including a zero-warning start.</summary>
        internal void AddYaguang(bool isCombo)
        {
            total++;
            if (isCombo) combo++; else flash++;
        }

        public LabGimmickStats Copy() => (LabGimmickStats)MemberwiseClone();
    }
}
