using System;

namespace C6Lab
{
    /// <summary>
    /// 판에서 받아 주는 손동작 (#477 튜토리얼). 보통 판은 All이라 지금까지와 같다. 튜토리얼은 단계마다 그 단계의 동작만 연다.
    /// 예: 1단계 북 치기는 DrumTap만 열어서, 북은 눌러도 움직이지 않고 구슬도 잡히지 않는다.
    /// </summary>
    [Flags]
    public enum LabBoardGestures
    {
        None = 0,
        /// <summary>북을 눌러 구슬 만들기.</summary>
        DrumTap = 1,
        /// <summary>북을 끌거나 밀기 (옆으로 넘기기 포함). 없으면 북은 눌러도 제자리에 있다.</summary>
        DrumMove = 2,
        /// <summary>북이 아닌 구슬·아이템을 잡아 끌거나 밀기 (합치기·넘기기 포함).</summary>
        OrbMove = 4,
        /// <summary>오행구슬을 위로 밀어 던지기.</summary>
        Throw = 8,
        All = DrumTap | DrumMove | OrbMove | Throw
    }

    public sealed partial class LabOrbBoard
    {
        private LabBoardGestures allowedGestures = LabBoardGestures.All;

        public LabBoardGestures AllowedGestures => allowedGestures;

        /// <summary>Open only these gestures. A held orb that is no longer allowed is let go where it is.</summary>
        public void SetAllowedGestures(LabBoardGestures gestures)
        {
            allowedGestures = gestures & LabBoardGestures.All;
            if (heldId != null && entries.TryGetValue(heldId, out Entry held) && !MayHold(held)) CancelDrag();
        }

        private bool Allows(LabBoardGestures gesture) => (allowedGestures & gesture) == gesture;

        /// <summary>A drum may be pressed to tap or move; anything else only to move or throw.</summary>
        private bool MayHold(Entry entry) => entry.View.Kind.IsDrum()
            ? (allowedGestures & (LabBoardGestures.DrumTap | LabBoardGestures.DrumMove)) != 0
            : (allowedGestures & (LabBoardGestures.OrbMove | LabBoardGestures.Throw)) != 0;

        /// <summary>A pressed drum that may not move stays where it is under the finger.</summary>
        private bool DrumPinned(Entry entry) => entry.View.Kind.IsDrum() && !Allows(LabBoardGestures.DrumMove);
    }
}
