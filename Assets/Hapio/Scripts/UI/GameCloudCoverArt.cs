using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// 장면이 바뀌어도 남는 한지 구름 덮개 (#635, <see cref="GameCloudCover"/>)가 쓰는 그림. Resources/Hapio/UI/CloudCoverArt 하나를 읽는다.
    /// 구름 모양·경로·덮기와 걷기 시간은 #630의 공용 구름 프리팹(StoryCloudWipe)에서 고친다.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudCoverArt", menuName = "Hapio/Cloud Cover Art")]
    public sealed class GameCloudCoverArt : ScriptableObject
    {
        public const string ResourcePath = "Hapio/UI/CloudCoverArt";

        [Tooltip("화면을 덮었다 걷히는 한지 구름 (#630 StoryCloudWipe 프리팹). 덮는 데 0.62초, 걷히는 데 0.72초.")]
        public StoryCloudWipe cloudWipe;

        public static GameCloudCoverArt Load() => Resources.Load<GameCloudCoverArt>(ResourcePath);
    }
}
