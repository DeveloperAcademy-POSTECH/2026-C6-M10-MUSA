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
        public UnityEngine.UI.Button[] groupButtons;
        public UnityEngine.UI.Button groupChangeButton;
        public UnityEngine.UI.Text status;
        public UnityEngine.UI.Button backButton;
        private GameSession session;
        private UnityEngine.UI.Text title, caption;

        public void Initialize(GameSession value)
        {
            if (session != null) return;
            if (value == null || stageButtons == null || stageButtons.Length != 3
                || groupButtons == null || groupButtons.Length != 2 || groupChangeButton == null
                || status == null || backButton == null)
                throw new InvalidOperationException("The saved staff screen needs A/B groups, stages, group change, status and cancel.");
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
            }
            for (int i = 0; i < groupButtons.Length; i++)
            {
                int group = i + 1;
                if (groupButtons[i] == null) throw new InvalidOperationException("A staff group button is missing.");
                groupButtons[i].onClick.AddListener(() => session.SelectDemoGroup(group));
                var label = groupButtons[i].GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (label != null) label.text = (group == 1 ? "A그룹" : "B그룹")
                    + (LabGameBuildVariant.IsStaffStageTwoOnly ? "\n방 만들기 / 입장" : string.Empty);
            }
            title = transform.Find("SafeArea/Title")?.GetComponent<UnityEngine.UI.Text>();
            caption = transform.Find("SafeArea/StaffCaption")?.GetComponent<UnityEngine.UI.Text>();
            backButton.onClick.AddListener(() => session.CancelDemoEntry());
            groupChangeButton.onClick.AddListener(() => session.ClearDemoGroup());
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (session == null) return;
            bool pending = session.IsDemoEntryPending || session.IsStaffRetryPending;
            bool idle = session.CanChangeDemoGroup;
            bool directEntry = LabGameBuildVariant.IsStaffStageTwoOnly;
            bool hasGroup = session.SelectedDemoGroup == 1 || session.SelectedDemoGroup == 2;
            string groupName = session.SelectedDemoGroup == 1 ? "A그룹" : "B그룹";
            foreach (UnityEngine.UI.Button button in groupButtons)
            {
                button.gameObject.SetActive(directEntry || !hasGroup);
                button.interactable = idle;
            }
            for (int i = 0; i < stageButtons.Length; i++)
            {
                stageButtons[i].gameObject.SetActive(!directEntry && hasGroup && LabGameBuildVariant.AllowsStaffDemoStage(i + 1));
                stageButtons[i].interactable = idle && hasGroup;
            }
            groupChangeButton.gameObject.SetActive(!directEntry && hasGroup && !pending);
            groupChangeButton.interactable = idle;
            backButton.gameObject.SetActive(pending);
            if (title != null) title.text = directEntry ? "시연 방 입장" : hasGroup ? "스테이지 선택" : "시연 그룹 선택";
            if (caption != null) caption.text = directEntry ? "A/B 독립 시연 · 각 3명"
                : hasGroup ? groupName + " · 3인 단판 시연" : "함께할 세 기기는 같은 그룹을 고르시오.";
            string idleNotice = directEntry
                ? (hasGroup ? "현재 " + groupName + "\n" : string.Empty)
                    + "함께할 세 기기는 같은 그룹을 고르시오.\n방 입장 후 자리를 고르면 방장이 시작하오."
                : hasGroup ? groupName + "에서 플레이할 스테이지를 선택하시오."
                    : "A/B는 서로 다른 방이오.\n같은 그룹의 세 명이 함께 출발하오.";
            status.text = !string.IsNullOrEmpty(session.DemoEntryNotice) ? session.DemoEntryNotice
                : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice : idleNotice;
        }
    }
}
