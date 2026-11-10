namespace Hapio
{
    /// <summary>
    /// 장면이 바뀌어도 남는 한지 구름 덮개 (#635). 오프닝이 끝나 튜토리얼로 넘어갈 때 화면을 덮고, 튜토리얼이 준비될 때까지
    /// 덮인 채로 기다렸다가 걷힌다. 덮개 자체는 <see cref="GameCloudCover"/>이고, 여기서는 세션이 하나를 갖고 있다.
    /// </summary>
    public sealed partial class GameSession
    {
        private GameCloudCover cloudCover;

        /// <summary>The persistent cloud cover, or null when its art is missing (the game then goes on without clouds).</summary>
        public GameCloudCover CloudCover => cloudCover;
        /// <summary>The clouds are on the screen (closing, covering or lifting); nothing under them can be touched.</summary>
        public bool CloudsUp => cloudCover != null && cloudCover.IsUp;
    }
}
