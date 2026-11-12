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
        /// 구미호·야광귀: everyone sees "다음 요괴 잡으러 ..."; every participant gets 다음 봉인.
        /// 지귀 (final): no next-yokai heading for anyone, and everyone’s button reads 마치기.
        /// </summary>
        public void Present(bool ready, bool isHost, bool canAdvance, bool? isFinalStage = null)
        {
            bool final = isFinalStage ?? finalStage;
            nextSeal.gameObject.SetActive(ready);
            nextSeal.interactable = ready && canAdvance;
            waiting.gameObject.SetActive(false);
            if (heading != null) heading.gameObject.SetActive(!final);
            var caption = nextSeal.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (caption != null) caption.text = final ? FinishLabel : NextSealLabel;
        }
    }
}
