namespace C6Lab
{
    /// <summary>
    /// 튜토리얼 (#477) 연습 판. 판을 연 직후 북을 플레이어 한 사람의 판 가운데에 모은다. 보통 판은 북 자리를 seed로 뽑지만,
    /// 튜토리얼은 언제나 가운데 앉은 "나"가 북을 들고 시작해야 첫 단계(북 치기)를 바로 할 수 있다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>Gives every drum to <paramref name="owner"/> at the center of that board. False before a round or for a stranger.</summary>
        public bool TutorialHandDrumsTo(ulong owner)
        {
            if (phase != LabPhase.Playing || !players.ContainsKey(owner)) return false;
            foreach (LabOrbState drum in drums.Values)
            {
                drum.owner = owner;
                drum.x = .5f;
                drum.y = .5f;
                drum.vx = drum.vy = 0f;
                drum.revision = ++revision;
            }
            return true;
        }
    }
}
