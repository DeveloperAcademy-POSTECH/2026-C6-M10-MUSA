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
        public void Present(bool ready, bool isHost, bool canAdvance)
        {
            nextSeal.gameObject.SetActive(ready && isHost);
            nextSeal.interactable = ready && isHost && canAdvance;
            waiting.gameObject.SetActive(ready && !isHost && !finalStage);
            if (heading != null) heading.gameObject.SetActive(!finalStage || isHost);
        }
    }
}
