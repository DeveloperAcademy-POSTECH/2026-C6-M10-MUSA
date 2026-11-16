using System;
using System.Collections;
using UnityEngine;

namespace Hapio
{
    /// <summary>Saved demo-only final page. The completion gesture never also activates its return action.</summary>
    public sealed class StaffDemoFinalPage : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/Demo/Staff/FinalPage";
        public UnityEngine.UI.Text heading, message, qrCaption;
        public UnityEngine.UI.Image qrImage;
        public GameObject placeholder;
        public UnityEngine.UI.Button returnButton;
        private Action returnToSelection;
        private Func<bool> canReturn;
        private Coroutine armRoutine;
        private bool armed;

        public bool IsReady => armed;
        private bool CanReturn => armed && (canReturn == null || canReturn());

        public static StaffDemoFinalPage Create(Transform parent)
        {
            var prefab = Resources.Load<StaffDemoFinalPage>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException("The saved staff demo final page is missing.");
            return Instantiate(prefab, parent, false);
        }

        public void Show(StaffDemoFinalPageConfig config, Action onReturn, Func<bool> canReturn = null)
        {
            Hide();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            returnToSelection = onReturn;
            this.canReturn = canReturn;
            heading.text = config != null ? config.heading : "함께 봉인해 주어 고맙소!";
            message.text = config != null ? config.message : "합이오 시연을 마쳤소.";
            bool hasQr = config != null && config.qrSprite != null;
            qrImage.sprite = hasQr ? config.qrSprite : null;
            qrImage.preserveAspect = true;
            qrImage.gameObject.SetActive(hasQr);
            placeholder.SetActive(!hasQr);
            qrCaption.text = hasQr ? config.qrCaption : "QR 준비 중 · 추후 교체 예정";
            if (C6Lab.LabGameBuildVariant.IsStaffStageTwoOnly)
            {
                var label = returnButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (label != null) label.text = "방 선택으로 돌아가기";
            }
            returnButton.onClick.AddListener(Return);
            armRoutine = StartCoroutine(ArmAfterRelease());
        }

        private IEnumerator ArmAfterRelease()
        {
            yield return null;
            while (StoryTapAdvance.HasHeldPointer) yield return null;
            armRoutine = null;
            armed = true;
            RefreshReturnAvailability();
        }

        private void Update() => RefreshReturnAvailability();

        private void RefreshReturnAvailability()
        {
            if (returnButton != null) returnButton.interactable = CanReturn;
        }

        private void Return()
        {
            // A Host may still be receiving the final result acknowledgements. Keep this action armed
            // while they arrive, rather than consuming the tap and leaving a permanently disabled page.
            if (!CanReturn || StoryTapAdvance.HasHeldPointer) { RefreshReturnAvailability(); return; }
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
            canReturn = null;
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
