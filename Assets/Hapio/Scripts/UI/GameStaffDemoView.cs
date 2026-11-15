using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Saved staff-only selection UI. All discovery and connection lifetime belongs to GameSession.</summary>
    public sealed class GameStaffDemoView : MonoBehaviour
    {
        public const string ResourcePath = "Hapio/Demo/Staff/StageSelection";
        public UnityEngine.UI.Button[] stageButtons;
        public UnityEngine.UI.Text status;
        public UnityEngine.UI.Button backButton;
        private GameSession session;

        public void Initialize(GameSession value)
        {
            if (session != null) return;
            if (value == null || stageButtons == null || stageButtons.Length != 3 || status == null || backButton == null)
                throw new InvalidOperationException("The saved staff screen needs three stage buttons, status and cancel.");
            if (transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            session = value;
            for (int i = 0; i < stageButtons.Length; i++)
            {
                int stage = i + 1;
                if (stageButtons[i] == null) throw new InvalidOperationException("A staff stage button is missing.");
                stageButtons[i].onClick.AddListener(() => session.SelectDemoStage(stage));
                stageButtons[i].gameObject.SetActive(LabGameBuildVariant.AllowsStaffDemoStage(stage));
            }
            if (LabGameBuildVariant.IsStaffStageTwoOnly)
            {
                Transform title = transform.Find("SafeArea/Title");
                if (title != null) title.GetComponent<UnityEngine.UI.Text>().text = "야광귀 시연";
                Transform caption = transform.Find("SafeArea/StaffCaption");
                if (caption != null) caption.GetComponent<UnityEngine.UI.Text>().text = "2스테이지 · 3인 단판 시연";
            }
            backButton.onClick.AddListener(() => session.CancelDemoEntry());
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (session == null) return;
            bool pending = session.IsDemoEntryPending || session.IsStaffRetryPending;
            bool idle = !pending && session.Network.CanChooseConnection && session.State.phase == GameFlowPhase.Home;
            foreach (UnityEngine.UI.Button button in stageButtons) button.interactable = idle;
            backButton.gameObject.SetActive(pending);
            string idleNotice = LabGameBuildVariant.IsStaffStageTwoOnly
                ? "야광귀를 선택하시오.\n같은 방의 세 명이 함께 출발하오."
                : "플레이할 스테이지를 선택하시오.\n같은 스테이지를 고른 세 명이 함께 출발하오.";
            status.text = !string.IsNullOrEmpty(session.DemoEntryNotice) ? session.DemoEntryNotice
                : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice : idleNotice;
        }
    }
}
