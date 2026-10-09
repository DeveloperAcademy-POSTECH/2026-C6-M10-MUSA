using System;
using System.Globalization;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Development-build-only solo entrance and in-app tuning. The saved LabConfig asset is
    /// never edited: this component installs one runtime clone before the board is configured.
    /// Each Apply starts a fresh solo round so copied Host rules and live physics agree.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class LabDeveloperMode : MonoBehaviour
    {
        public LabConfig sourceConfig;
        public LabNetwork network;
        public LabSceneController sceneController;
        public LabSeatCamera seatCamera;
        public GameObject panel;
        public UnityEngine.UI.Button toggleButton;
        public UnityEngine.UI.Button closeButton;
        public UnityEngine.UI.Button defaultsButton;
        public UnityEngine.UI.Button endSoloButton;
        public UnityEngine.UI.Button applyStartButton;
        public UnityEngine.UI.Text applyStartLabel;
        public UnityEngine.UI.Text feedbackText;
        public UnityEngine.UI.InputField[] valueInputs;

        private LabConfig runtimeConfig;
        private bool available;
        private bool soloTuningActive;

        private void Awake()
        {
            available = Application.isEditor || Debug.isDebugBuild;
            if (toggleButton != null) toggleButton.gameObject.SetActive(available);
            if (panel != null) panel.SetActive(false);
            if (!available) return;

            if (sourceConfig == null || network == null || sceneController == null || seatCamera == null)
            {
                Debug.LogError("C6_LAB_DEV_SETUP_MISSING: config, network, board controller, or seat camera.");
                available = false;
                if (toggleButton != null) toggleButton.gameObject.SetActive(false);
                return;
            }

            runtimeConfig = Instantiate(sourceConfig);
            runtimeConfig.name = sourceConfig.name + " (Developer Runtime)";
            runtimeConfig.hideFlags = HideFlags.DontSave;
        }

        private void Start()
        {
            if (!available) return;
            if (!EnsureValueInputs())
            {
                Debug.LogError("C6_LAB_DEV_SETUP_MISSING: tuning input references do not match LabConfig.");
                available = false;
                if (toggleButton != null) toggleButton.gameObject.SetActive(false);
                return;
            }

            if (toggleButton != null) toggleButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (defaultsButton != null) defaultsButton.onClick.AddListener(ShowDefaults);
            if (endSoloButton != null) endSoloButton.onClick.AddListener(EndSolo);
            if (applyStartButton != null) applyStartButton.onClick.AddListener(ApplyAndStart);
            Fill(runtimeConfig);
        }

        private void Update()
        {
            if (!available || network == null) return;
            if (soloTuningActive && !network.IsDeveloperSolo)
                RestoreSourceConfig();
            if (toggleButton != null)
                toggleButton.interactable = !network.IsConnected || network.IsDeveloperSolo;
            if (endSoloButton != null)
                endSoloButton.interactable = network.IsDeveloperSolo;
            if (applyStartLabel != null)
            {
                string title = network.IsDeveloperSolo ? "APPLY + RESTART SOLO" : "APPLY + START SOLO";
                if (applyStartLabel.text != title) applyStartLabel.text = title;
            }
        }

        public void Open()
        {
            if (!available || network == null || (network.IsConnected && !network.IsDeveloperSolo)) return;
            Fill(runtimeConfig);
            SetFeedback(string.IsNullOrEmpty(network.LastShotReport)
                ? "Values apply to the next solo round. This app session only."
                : network.LastShotReport);
            if (panel != null) panel.SetActive(true);
            if (sceneController != null && sceneController.board != null)
                sceneController.board.SetUiInputBlocked(true);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (sceneController != null && sceneController.board != null)
                sceneController.board.SetUiInputBlocked(false);
        }

        public void ShowDefaults()
        {
            if (!available) return;
            Fill(sourceConfig);
            SetFeedback("Asset defaults loaded. Press APPLY + START to use them.");
        }

        public void EndSolo()
        {
            if (network == null || !network.IsDeveloperSolo) return;
            network.Stop();
            RestoreSourceConfig();
            SetFeedback("Solo ended. Settings remain until this app closes.");
        }

        public void ApplyAndStart()
        {
            if (!available || network == null || runtimeConfig == null) return;
            if (network.IsConnected && !network.IsDeveloperSolo)
            {
                SetFeedback("Leave the three-player room before changing developer settings.");
                return;
            }

            var values = new float[valueInputs.Length];
            for (int i = 0; i < valueInputs.Length; i++)
            {
                string raw = valueInputs[i] == null ? string.Empty : valueInputs[i].text;
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])
                    && !float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out values[i]))
                {
                    SetFeedback("Enter a number for " + LabConfig.DeveloperFields[i].Label + ".");
                    return;
                }
            }

            if (sceneController.orbCamera == null)
            {
                SetFeedback("The orb camera is not connected in the saved Scene.");
                return;
            }
            float halfHeight = values[16] * .5f;
            float margin = values[15] + .05f;
            if (halfHeight <= margin || halfHeight * sceneController.orbCamera.aspect <= margin)
            {
                SetFeedback("Orb Board Height is too small for the selected Orb Radius.");
                return;
            }

            if (!runtimeConfig.TryApplyDeveloperValues(values, out string error))
            {
                SetFeedback(error);
                return;
            }

            InstallRuntimeConfig();
            sceneController.ReconfigureBoard();
            seatCamera.RefreshPose();
            bool started = network.IsDeveloperSolo
                ? network.RestartDeveloperSolo()
                : network.StartDeveloperSolo();
            if (!started)
            {
                RestoreSourceConfig();
                SetFeedback("Solo could not start. Check the local Host port and try again.");
                return;
            }

            soloTuningActive = true;

            Debug.Log("C6_LAB_DEV_APPLIED hp=" + runtimeConfig.MonsterMaxHp
                + " duration=" + runtimeConfig.BattleDurationSeconds.ToString("F1", CultureInfo.InvariantCulture)
                + " orbLife=" + runtimeConfig.OrbLifetimeSeconds.ToString("F1", CultureInfo.InvariantCulture));
            Close();
        }

        private void InstallRuntimeConfig()
        {
            network.config = runtimeConfig;
            sceneController.config = runtimeConfig;
            seatCamera.config = runtimeConfig;
        }

        private void RestoreSourceConfig()
        {
            soloTuningActive = false;
            network.config = sourceConfig;
            sceneController.config = sourceConfig;
            seatCamera.config = sourceConfig;
            sceneController.ReconfigureBoard();
            seatCamera.RefreshPose();
        }

        /// <summary>
        /// Extend an older saved panel from its final editable row. The Scene keeps its existing
        /// layout and references; the Editor upgrade command can persist these extra rows.
        /// </summary>
        public bool EnsureValueInputs()
        {
            int expected = LabConfig.DeveloperFields.Count;
            if (valueInputs == null || valueInputs.Length == 0 || valueInputs.Length > expected)
                return false;
            for (int i = 0; i < valueInputs.Length; i++)
                if (valueInputs[i] == null) return false;
            if (valueInputs.Length == expected) return true;

            UnityEngine.UI.InputField template = valueInputs[valueInputs.Length - 1];
            Transform templateRow = template.transform.parent;
            Transform content = templateRow == null ? null : templateRow.parent;
            if (content == null || templateRow.Find("SettingLabel") == null
                || templateRow.Find("SettingValue") == null) return false;
            int previousCount = valueInputs.Length;
            Array.Resize(ref valueInputs, expected);
            for (int i = previousCount; i < expected; i++)
            {
                GameObject row = Instantiate(templateRow.gameObject, content);
                row.name = "Setting" + i.ToString("D2");
                row.transform.SetAsLastSibling();
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                UnityEngine.UI.Text label = row.transform.Find("SettingLabel")?.GetComponent<UnityEngine.UI.Text>();
                UnityEngine.UI.InputField input = row.transform.Find("SettingValue")?.GetComponent<UnityEngine.UI.InputField>();
                if (label == null || input == null) return false;
                label.text = field.Label;
                if (input.placeholder is UnityEngine.UI.Text placeholder)
                    placeholder.text = field.Label;
                input.contentType = field.WholeNumber
                    ? UnityEngine.UI.InputField.ContentType.IntegerNumber
                    : UnityEngine.UI.InputField.ContentType.DecimalNumber;
                input.text = string.Empty;
                valueInputs[i] = input;
            }
            return true;
        }

        private void Fill(LabConfig config)
        {
            if (config == null || valueInputs == null) return;
            float[] values = config.CaptureDeveloperValues();
            for (int i = 0; i < Mathf.Min(values.Length, valueInputs.Length); i++)
                if (valueInputs[i] != null)
                    valueInputs[i].text = values[i].ToString("0.######", CultureInfo.InvariantCulture);
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null) feedbackText.text = message ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (runtimeConfig != null) Destroy(runtimeConfig);
        }
    }
}
