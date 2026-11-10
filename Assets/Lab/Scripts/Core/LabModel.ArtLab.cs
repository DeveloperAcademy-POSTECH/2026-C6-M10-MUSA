namespace C6Lab
{
    /// <summary>
    /// 아트팀 폭탄 실험실 전용(#584). 에디터 창이 떨어뜨린 그 폭탄 하나의 도화선 길이만 바꾼다. 설정(LabConfig)과 다른 폭탄은
    /// 그대로라 실제 게임 규칙에는 들어가지 않는다.
    /// </summary>
    public sealed partial class LabModel
    {
        /// <summary>지금 판의 폭탄 도화선 길이(초) = 아이템 수명.</summary>
        public float ArtLabFuseSeconds => rules != null ? rules.ItemLifetimeSeconds : 0f;

        /// <summary>
        /// 이 폭탄이 <paramref name="now"/>부터 <paramref name="fuseSeconds"/> 뒤에 터지게 한다. 만든 시각을 앞으로 당겨서
        /// Host의 폭발 시각과 화면의 도화선 그림이 같은 값을 본다. 도화선 길이보다 길게는 못 한다.
        /// </summary>
        public bool ArtLabSetBombFuse(string bombId, double now, float fuseSeconds)
        {
            if (rules == null || bombId == null || !orbs.TryGetValue(bombId, out LabOrbState bomb) || bomb.kind != LabOrbKind.Bomb
                || bomb.lockSwipes <= 0 || !(fuseSeconds > 0f)) return false;
            float fuse = UnityEngine.Mathf.Min(fuseSeconds, rules.ItemLifetimeSeconds);
            bomb.createdAt = now - (rules.ItemLifetimeSeconds - fuse);
            bomb.revision = ++revision;
            return true;
        }
    }
}
