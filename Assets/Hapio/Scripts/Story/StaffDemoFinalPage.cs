using System;
using System.Collections;
using UnityEngine;

namespace Hapio
{
    /// <summary>Saved demo-only final page. Entering it never consumes the gesture that finished the ending.</summary>
    public sealed class StaffDemoFinalPage : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/Demo/Staff/FinalPage";
        public UnityEngine.UI.Text heading, message, qrCaption;
        public UnityEngine.UI.Image qrImage;
        public GameObject placeholder;
        public UnityEngine.UI.Button returnButton;
        private Action returnToSelection;
        private Coroutine armRoutine;
        private bool armed;

        public bool IsReady => armed;

        public static StaffDemoFinalPage Create(Transform parent)
        {
            var prefab = Resources.Load<StaffDemoFinalPage>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException("The saved staff demo final page is missing.");
            return Instantiate(prefab, parent, false);
        }

        public void Show(StaffDemoFinalPageConfig config, Action onReturn)
        {
            Hide();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            returnToSelection = onReturn;
            heading.text = config != null ? config.heading : "함께 봉인해 주어 고맙소!";
            message.text = config != null ? config.message : "합이오 시연을 마쳤소.";
            bool hasQr = config != null && config.qrSprite != null;
            qrImage.sprite = hasQr ? config.qrSprite : null;
            qrImage.preserveAspect = true;
            qrImage.gameObject.SetActive(hasQr);
            placeholder.SetActive(!hasQr);
            qrCaption.text = hasQr ? config.qrCaption : "QR 준비 중 · 추후 교체 예정";
            returnButton.onClick.AddListener(Return);
            armRoutine = StartCoroutine(ArmAfterRelease());
        }

        private IEnumerator ArmAfterRelease()
        {
            yield return null;
            while (StoryTapAdvance.HasHeldPointer) yield return null;
            armRoutine = null;
            armed = true;
            returnButton.interactable = true;
        }

        private void Return()
        {
            if (!armed || StoryTapAdvance.HasHeldPointer) return;
            armed = false;
            returnButton.interactable = false;
            returnToSelection?.Invoke();
        }

        public void Hide()
        {
            if (armRoutine != null) StopCoroutine(armRoutine);
            armRoutine = null;
            armed = false;
            returnToSelection = null;
            if (returnButton != null)
            {
                returnButton.interactable = false;
                returnButton.onClick.RemoveListener(Return);
            }
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (armRoutine != null) StopCoroutine(armRoutine);
            armRoutine = null;
            armed = false;
            if (returnButton != null) returnButton.interactable = false;
        }
    }
}
