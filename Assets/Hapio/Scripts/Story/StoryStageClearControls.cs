using UnityEngine;
using UnityEngine.UI;
namespace Hapio
{
    /// <summary>Role-specific controls on the last authored stage picture; GameSession owns permission.</summary>
    public sealed class StoryStageClearControls : MonoBehaviour
    {
        public Button nextSeal;
        public Text waiting;
        public Text heading;
        public bool finalStage;
        public const string NextSealLabel = "다음 봉인";
        public const string FinishLabel = "마치기";
        /// <summary>
        /// 구미호·야광귀: everyone sees "다음 요괴 잡으러 ..."; the Host gets 다음 봉인 and members the waiting line.
        /// 지귀 (final): no next-yokai heading for anyone, and the Host's button reads 마치기.
        /// </summary>
        public void Present(bool ready, bool isHost, bool canAdvance)
        {
            nextSeal.gameObject.SetActive(ready && isHost);
            nextSeal.interactable = ready && isHost && canAdvance;
            waiting.gameObject.SetActive(ready && !isHost && !finalStage);
            if (heading != null) heading.gameObject.SetActive(!finalStage);
        }
    }
}
