using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab
{
    /// <summary>
    /// Development-only tuning for solo rounds and Host-created rooms. The saved config asset
    /// is never edited; the Host shares an approved runtime copy with every participant.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class LabDeveloperMode : MonoBehaviour
    {
        public LabConfig sourceConfig;
        public LabNetwork network;
        public LabSceneController sceneController;
        public LabSeatCamera seatCamera;
        public GameObject panel;
        public Button toggleButton;
        public Button closeButton;
        public Button defaultsButton;
        public Button endSoloButton;
        public Button applyStartButton;
        public Text applyStartLabel;
        public Button applyHostButton;
        public Text applyHostLabel;
        public Slider targetParticipantsSlider;
        public Text targetParticipantsLabel;
        public Text feedbackText;
        public InputField[] valueInputs;
        public Slider[] valueSliders;

        private LabConfig runtimeConfig;
        private bool available;
        private bool soloTuningActive;
        private float[] defaultValues;

        private bool HostLobby => network != null && network.IsHost && !network.IsDeveloperSolo
            && network.Snapshot == null;

        private void Awake()
        {
            available = Application.isEditor || Debug.isDebugBuild;
            if (toggleButton != null) toggleButton.gameObject.SetActive(available);
            if (panel != null) panel.SetActive(false);
            // Client room configuration must reach board/camera in ordinary builds too.
            if (network != null) network.ConfigApplied += OnConfigApplied;
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
            defaultValues = sourceConfig.CaptureDeveloperValues();
        }

        private void Start()
        {
            if (!available) return;
            if (!EnsureValueInputs() || !FindValueSliders())
            {
                Debug.LogError("C6_LAB_DEV_SETUP_MISSING: setting rows need numeric inputs and sliders.");
                available = false;
                if (toggleButton != null) toggleButton.gameObject.SetActive(false);
                return;
            }

            if (toggleButton != null) toggleButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (defaultsButton != null) defaultsButton.onClick.AddListener(ShowDefaults);
            if (endSoloButton != null) endSoloButton.onClick.AddListener(EndSolo);
            if (applyStartButton != null) applyStartButton.onClick.AddListener(ApplyAndStart);
            if (applyHostButton != null) applyHostButton.onClick.AddListener(ApplyAndHost);
            for (int i = 0; i < valueInputs.Length; i++)
            {
                int fieldIndex = i;
                valueInputs[i].onValueChanged.AddListener(raw => SyncSliderFromInput(fieldIndex, raw));
                valueSliders[i].onValueChanged.AddListener(position => SyncInputFromSlider(fieldIndex, position));
            }
            if (targetParticipantsSlider != null)
            {
                targetParticipantsSlider.minValue = 2f;
                targetParticipantsSlider.maxValue = 5f;
                targetParticipantsSlider.wholeNumbers = true;
                targetParticipantsSlider.onValueChanged.AddListener(_ => RefreshTargetLabel());
                targetParticipantsSlider.SetValueWithoutNotify(network.TargetParticipantCount);
            }
            Fill(runtimeConfig);
            RefreshTargetLabel();
        }

        private void Update()
        {
            if (!available || network == null) return;
            if (soloTuningActive && !network.IsDeveloperSolo)
                RestoreSourceConfig();

            bool canOpen = !network.IsConnected || network.IsDeveloperSolo || HostLobby;
            if (toggleButton != null) toggleButton.interactable = canOpen;
            if (endSoloButton != null) endSoloButton.interactable = network.IsDeveloperSolo;
            if (applyHostButton != null) applyHostButton.interactable = !network.IsConnected || HostLobby;
            if (targetParticipantsSlider != null) targetParticipantsSlider.interactable = !network.IsConnected;
            string soloTitle = network.IsDeveloperSolo ? "RESTART SOLO" : "START SOLO";
            if (applyStartLabel != null && applyStartLabel.text != soloTitle) applyStartLabel.text = soloTitle;
            string hostTitle = HostLobby ? "APPLY TO ROOM" : "CREATE ROOM";
            if (applyHostLabel != null && applyHostLabel.text != hostTitle) applyHostLabel.text = hostTitle;
        }

        public void Open()
        {
            if (!available || network == null || (network.IsConnected && !network.IsDeveloperSolo && !HostLobby)) return;
            Fill(network.IsConnected && network.config != null ? network.config : runtimeConfig);
            if (targetParticipantsSlider != null)
                targetParticipantsSlider.SetValueWithoutNotify(network.TargetParticipantCount);
            RefreshTargetLabel();
            SetFeedback(string.IsNullOrEmpty(network.LastShotReport)
                ? "Solo restarts a round. CREATE ROOM shares these values with every player."
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
            SetFeedback("Asset defaults loaded. Choose SOLO or CREATE ROOM to apply them.");
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
                SetFeedback("Leave the shared room before starting Developer Solo.");
                return;
            }
            if (!TryReadAndApplyForm()) return;

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

        /// <summary>Create a tuned shared room or update the Host's existing pre-battle lobby.</summary>
        public void ApplyAndHost()
        {
            if (!available || network == null || runtimeConfig == null) return;
            bool lobby = HostLobby;
            if (network.IsConnected && !lobby)
            {
                SetFeedback("Shared settings are editable only by the Host before battle starts.");
                return;
            }
            if (!TryReadAndApplyForm()) return;

            if (lobby)
            {
                if (!network.ApplyHostLobbyConfig(runtimeConfig))
                {
                    SetFeedback("Room settings were not accepted. Check the Host status.");
                    return;
                }
                SetFeedback("Room settings sent. START is available after every player confirms them.");
                Close();
                return;
            }

            int targetCount = targetParticipantsSlider == null ? 3 : Mathf.RoundToInt(targetParticipantsSlider.value);
            if (!network.ConfigureHostSession(runtimeConfig, targetCount))
            {
                SetFeedback("Room settings or player count were not accepted.");
                return;
            }
            network.Host();
            if (!network.IsHost)
            {
                SetFeedback("Room could not start. Check the Host port and connection.");
                return;
            }
            Close();
        }

        private bool TryReadAndApplyForm()
        {
            var values = new float[valueInputs.Length];
            for (int i = 0; i < valueInputs.Length; i++)
            {
                string raw = valueInputs[i] == null ? string.Empty : valueInputs[i].text;
                if (!TryParse(raw, out values[i]))
                {
                    SetFeedback("Enter a number for " + LabConfig.DeveloperFields[i].Label + ".");
                    return false;
                }
            }

            if (sceneController.orbCamera == null)
            {
                SetFeedback("The orb camera is not connected in the saved Scene.");
                return false;
            }
            float halfHeight = values[16] * .5f;
            float margin = values[15] + .05f;
            if (halfHeight <= margin || halfHeight * sceneController.orbCamera.aspect <= margin)
            {
                SetFeedback("Orb Board Height is too small for the selected Orb Radius.");
                return false;
            }

            if (!runtimeConfig.TryApplyDeveloperValues(values, out string error))
            {
                SetFeedback(error);
                return false;
            }
            return true;
        }

        private static bool TryParse(string raw, out float value) =>
            float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

        private void OnConfigApplied(LabConfig applied)
        {
            if (applied == null || sceneController == null || seatCamera == null) return;
            sceneController.config = applied;
            seatCamera.config = applied;
            sceneController.ReconfigureBoard();
            seatCamera.RefreshPose();
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

        /// <summary>Extend older saved panels without deleting existing Inspector-edited rows.</summary>
        public bool EnsureValueInputs()
        {
            int expected = LabConfig.DeveloperFields.Count;
            if (valueInputs == null || valueInputs.Length == 0 || valueInputs.Length > expected)
                return false;
            for (int i = 0; i < valueInputs.Length; i++)
                if (valueInputs[i] == null) return false;
            if (valueInputs.Length == expected) return true;

            InputField template = valueInputs[valueInputs.Length - 1];
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
                Text label = row.transform.Find("SettingLabel")?.GetComponent<Text>();
                InputField input = row.transform.Find("SettingValue")?.GetComponent<InputField>();
                if (label == null || input == null) return false;
                label.text = field.Label;
                if (input.placeholder is Text placeholder)
                    placeholder.text = field.Label;
                input.contentType = field.WholeNumber
                    ? InputField.ContentType.IntegerNumber
                    : InputField.ContentType.DecimalNumber;
                input.text = string.Empty;
                valueInputs[i] = input;
            }
            return true;
        }

        private bool FindValueSliders()
        {
            int expected = LabConfig.DeveloperFields.Count;
            if (valueSliders == null || valueSliders.Length != expected)
                valueSliders = new Slider[expected];
            for (int i = 0; i < expected; i++)
            {
                if (valueSliders[i] == null)
                    valueSliders[i] = valueInputs[i].transform.parent.Find("SettingSlider")?.GetComponent<Slider>();
                if (valueSliders[i] == null) return false;
                valueSliders[i].minValue = 0f;
                valueSliders[i].maxValue = 1f;
                valueSliders[i].wholeNumbers = false;
            }
            return targetParticipantsSlider != null && applyHostButton != null;
        }

        private void Fill(LabConfig config)
        {
            if (config == null || valueInputs == null) return;
            float[] values = config.CaptureDeveloperValues();
            for (int i = 0; i < Mathf.Min(values.Length, valueInputs.Length); i++)
            {
                if (valueInputs[i] == null) continue;
                valueInputs[i].SetTextWithoutNotify(values[i].ToString("0.######", CultureInfo.InvariantCulture));
                if (valueSliders != null && i < valueSliders.Length && valueSliders[i] != null)
                    valueSliders[i].SetValueWithoutNotify(LabDeveloperSliderScale.Normalize(
                        LabConfig.DeveloperFields[i], defaultValues[i], values[i]));
            }
        }

        private void SyncSliderFromInput(int index, string raw)
        {
            if (!TryParse(raw, out float value) || index >= valueSliders.Length) return;
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < field.Min || value > field.Max) return;
            valueSliders[index].SetValueWithoutNotify(LabDeveloperSliderScale.Normalize(field, defaultValues[index], value));
        }

        private void SyncInputFromSlider(int index, float position)
        {
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            float value = LabDeveloperSliderScale.Denormalize(field, defaultValues[index], position);
            if (field.WholeNumber) value = Mathf.Round(value);
            valueInputs[index].SetTextWithoutNotify(value.ToString("0.######", CultureInfo.InvariantCulture));
        }

        private void RefreshTargetLabel()
        {
            if (targetParticipantsLabel == null) return;
            int count = targetParticipantsSlider == null ? 3 : Mathf.RoundToInt(targetParticipantsSlider.value);
            targetParticipantsLabel.text = "ROOM PLAYERS " + count + " / 5";
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null) feedbackText.text = message ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (network != null) network.ConfigApplied -= OnConfigApplied;
            if (runtimeConfig != null) Destroy(runtimeConfig);
        }
    }

    /// <summary>Maps very broad DEV ranges to a usable slider without restricting numeric entry.</summary>
    public static class LabDeveloperSliderScale
    {
        public static float Normalize(LabDeveloperField field, float baseline, float value)
        {
            if (field.Max <= field.Min) return 0f;
            if (!Adaptive(field, baseline))
                return Mathf.InverseLerp(field.Min, field.Max, value);
            double center = Math.Max(field.Min, Math.Min(field.Max, baseline));
            double scale = Scale(field, center);
            double lo = Asinh((field.Min - center) / scale);
            double hi = Asinh((field.Max - center) / scale);
            double current = Asinh((Math.Max(field.Min, Math.Min(field.Max, value)) - center) / scale);
            return (float)Math.Max(0d, Math.Min(1d, (current - lo) / (hi - lo)));
        }

        public static float Denormalize(LabDeveloperField field, float baseline, float position)
        {
            if (field.Max <= field.Min) return field.Min;
            if (!Adaptive(field, baseline))
                return Mathf.Lerp(field.Min, field.Max, Mathf.Clamp01(position));
            double center = Math.Max(field.Min, Math.Min(field.Max, baseline));
            double scale = Scale(field, center);
            double lo = Asinh((field.Min - center) / scale);
            double hi = Asinh((field.Max - center) / scale);
            double decoded = center + scale * Math.Sinh(lo + (hi - lo) * Mathf.Clamp01(position));
            return (float)Math.Max(field.Min, Math.Min(field.Max, decoded));
        }

        private static bool Adaptive(LabDeveloperField field, float baseline) =>
            field.Max - field.Min > 100f && field.Max - field.Min > Math.Max(1f, Math.Abs(baseline)) * 20f;

        private static double Scale(LabDeveloperField field, double center) =>
            Math.Max(.001d, Math.Max(Math.Abs(center) * .5d, (field.Max - field.Min) * .000001d));

        private static double Asinh(double x) => x < 0d
            ? -Math.Log(-x + Math.Sqrt(x * x + 1d))
            : Math.Log(x + Math.Sqrt(x * x + 1d));
    }
}
