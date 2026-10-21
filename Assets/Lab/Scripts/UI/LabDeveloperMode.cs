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
        private bool HostPausedBattle => network != null && network.CanRestartSharedBattle;

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
            BuildCategoryLayout();
            BuildToggles();
            BuildChoices();

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
                targetParticipantsSlider.onValueChanged.AddListener(value =>
                {
                    if (network != null && network.CanChooseConnection)
                        network.SetTargetParticipantCount(Mathf.RoundToInt(value));
                    RefreshTargetLabel();
                });
                targetParticipantsSlider.SetValueWithoutNotify(network.TargetParticipantCount);
            }
            Fill(runtimeConfig);
            RefreshTargetLabel();
        }

        private void Update()
        {
            if (!available || network == null) return;
            RefreshToggles();
            if (soloTuningActive && !network.IsDeveloperSolo)
                RestoreSourceConfig();

            // The legacy toggle belongs to the room setup screen; battle settings
            // are opened from the shared pause menu instead.
            bool showToggle = network.Snapshot == null;
            if (toggleButton != null && toggleButton.gameObject.activeSelf != showToggle)
                toggleButton.gameObject.SetActive(showToggle);
            bool canOpen = network.CanChooseConnection || network.IsDeveloperSolo || HostLobby || HostPausedBattle;
            if (toggleButton != null) toggleButton.interactable = canOpen;
            if (endSoloButton != null) endSoloButton.interactable = network.IsDeveloperSolo;
            if (applyHostButton != null) applyHostButton.interactable = network.CanChooseConnection || HostLobby || HostPausedBattle;
            if (targetParticipantsSlider != null) targetParticipantsSlider.interactable = network.CanChooseConnection;
            string soloTitle = network.IsDeveloperSolo ? "RESTART SOLO" : "START SOLO";
            if (applyStartLabel != null && applyStartLabel.text != soloTitle) applyStartLabel.text = soloTitle;
            string hostTitle = HostPausedBattle ? "APPLY & RESTART"
                : HostLobby ? "APPLY TO ROOM" : "CREATE ROOM";
            if (applyHostLabel != null && applyHostLabel.text != hostTitle) applyHostLabel.text = hostTitle;
        }

        public void Open()
        {
            if (!available || network == null ||
                (!network.CanChooseConnection && !network.IsDeveloperSolo && !HostLobby && !HostPausedBattle)) return;
            Fill(network.IsConnected && network.config != null ? network.config : runtimeConfig);
            if (targetParticipantsSlider != null)
                targetParticipantsSlider.SetValueWithoutNotify(network.TargetParticipantCount);
            RefreshTargetLabel();
            SetFeedback(string.IsNullOrEmpty(network.LastShotReport)
                ? HostPausedBattle
                    ? "Apply settings to restart the whole room after every player confirms."
                    : "Solo restarts a round. CREATE ROOM shares these values with every player."
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
            // Fill suppresses input callbacks; update the visible toggle state in this
            // button action rather than leaving the previous label until the next frame.
            RefreshToggles();
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
            bool restart = HostPausedBattle;
            if (!lobby && !restart && !network.CanChooseConnection)
            {
                SetFeedback("Cancel room search before creating a room.");
                return;
            }
            if (network.IsConnected && !lobby && !restart)
            {
                SetFeedback("Shared settings are editable only by the Host before battle starts.");
                return;
            }
            if (!TryReadAndApplyForm()) return;

            if (restart)
            {
                if (!network.RestartBattleWithDeveloperConfig(runtimeConfig))
                {
                    SetFeedback("Room restart was not accepted. Check the Host status.");
                    return;
                }
                SetFeedback("Settings sent; the room will restart after every player confirms.");
                Close();
                return;
            }

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

            int targetCount = network.TargetParticipantCount;
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
            if (!sceneController.ValidateBoardTuning(values[16], values[15], values[75], out string areaError))
            {
                SetFeedback(areaError);
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

        /// <summary>
        /// 설정 줄을 카테고리별로 모으고, 카테고리마다 접기/펼치기 제목 줄을 붙인다.
        /// 줄의 위치만 바꾸므로 값 번호(valueInputs 순서)와 APPLY 동작은 그대로다.
        /// </summary>
        private void BuildCategoryLayout()
        {
            GameObject template = valueInputs[0].transform.parent.gameObject;
            Transform content = template.transform.parent;
            Text labelStyle = template.transform.Find("SettingLabel").GetComponent<Text>();
            int sibling = 1; // 0번 자리는 인원수 줄(TargetParticipantsRow)이 그대로 쓴다.

            foreach (LabDeveloperCategory category in LabConfig.DeveloperCategories)
            {
                // 제목 줄은 글자 + 버튼만 가진 한 겹짜리 오브젝트로 새로 만든다.
                // 설정 줄들과 같은 위계(Content 바로 아래)에 놓인다.
                GameObject header = new GameObject("Category " + category.Title,
                    typeof(RectTransform), typeof(Text), typeof(Button), typeof(LayoutElement));
                header.layer = content.gameObject.layer;
                header.transform.SetParent(content, false);

                Text title = header.GetComponent<Text>();
                title.font = labelStyle.font;
                title.fontSize = labelStyle.fontSize;
                title.color = labelStyle.color;
                title.fontStyle = FontStyle.Bold;
                // 세모(▶/▼)가 세로 한 줄에 맞도록 왼쪽 정렬한다. 가운데 정렬이면 제목 길이마다 시작점이 달라 읽기 어렵다.
                title.alignment = TextAnchor.MiddleLeft;
                title.raycastTarget = true;

                LayoutElement titleLayout = header.GetComponent<LayoutElement>();
                titleLayout.preferredHeight = 30f;
                titleLayout.flexibleWidth = 1f;
                header.transform.SetSiblingIndex(sibling++);

                // 이 카테고리의 설정 줄들을 제목 바로 아래로 옮긴다.
                GameObject[] rows = new GameObject[category.FieldIndices.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i] = valueInputs[category.FieldIndices[i]].transform.parent.gameObject;
                    rows[i].transform.SetSiblingIndex(sibling++);
                }

                // 처음에는 모두 접힌 상태로 시작한다. 제목을 누르면 접고 펼친다.
                // 숨긴 줄의 값도 APPLY 때 그대로 읽힌다.
                string name = category.Title;
                bool expanded = false;
                SetCategoryExpanded(title, name, rows, expanded);
                Button toggle = header.GetComponent<Button>();
                toggle.targetGraphic = title;
                toggle.onClick.AddListener(() =>
                {
                    expanded = !expanded;
                    SetCategoryExpanded(title, name, rows, expanded);
                });
            }
        }

        /// <summary>펼침 상태에 맞춰 제목의 세모(▶ 접힘 / ▼ 펼침)와 항목 줄 표시를 바꾼다.</summary>
        private static void SetCategoryExpanded(Text title, string name, GameObject[] rows, bool expanded)
        {
            title.text = (expanded ? "\u25BC  " : "\u25B6  ") + name;
            foreach (GameObject row in rows) row.SetActive(expanded);
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
                bool fixedValue = LabConfig.DeveloperFields[i].Min == LabConfig.DeveloperFields[i].Max;
                valueInputs[i].readOnly = fixedValue;
                valueSliders[i].interactable = !fixedValue;
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
            RefreshToggles();
        }

        private void SyncSliderFromInput(int index, string raw)
        {
            if (!TryParse(raw, out float value) || index >= valueSliders.Length) return;
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < field.Min || value > field.Max) return;
            valueSliders[index].SetValueWithoutNotify(LabDeveloperSliderScale.Normalize(field, defaultValues[index], value));
            if (index == 112 || index == 139 || index == PaintDryingField) RefreshToggles();
        }

        private void SyncInputFromSlider(int index, float position)
        {
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            float value = LabDeveloperSliderScale.Denormalize(field, defaultValues[index], position);
            if (field.WholeNumber) value = Mathf.Round(value);
            valueInputs[index].SetTextWithoutNotify(value.ToString("0.######", CultureInfo.InvariantCulture));
            if (index == 112 || index == 139 || index == PaintDryingField) RefreshToggles();
        }

        // 0/1 항목(켜기/끄기, 박 모드)은 숫자 입력칸·슬라이더 대신 누를 때마다 바뀌는 버튼으로 보여준다.
        // 숨긴 입력칸에 "0"/"1"을 그대로 넣기 때문에 APPLY·방 공유·검사는 숫자 항목과 똑같이 동작한다.
        private static readonly Color ToggleOnColor = new Color(.565f, .337f, .247f, 1f);
        private static readonly Color ToggleOffColor = new Color(.55f, .55f, .55f, 1f);
        private Button[] toggleButtons;
        private Text[] toggleLabels;

        private void BuildToggles()
        {
            int count = LabConfig.DeveloperFields.Count;
            toggleButtons = new Button[count];
            toggleLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                if (!field.IsToggle || valueInputs[i] == null || valueSliders[i] == null) continue;
                Transform row = valueInputs[i].transform.parent;
                Text rowLabel = row.Find("SettingLabel")?.GetComponent<Text>();
                if (rowLabel != null) rowLabel.text = field.DisplayLabel;

                // 숫자 입력칸은 숨기고(값은 그대로 보관), 슬라이더 자리에 버튼을 놓는다.
                valueInputs[i].gameObject.SetActive(false);
                Slider slider = valueSliders[i];
                slider.interactable = false;
                foreach (Transform part in slider.transform) part.gameObject.SetActive(false);

                var go = new GameObject("SettingToggle", typeof(RectTransform), typeof(Image), typeof(Button));
                go.layer = slider.gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(slider.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4f, 2f);
                rect.offsetMax = new Vector2(-4f, -2f);

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.layer = go.layer;
                var textRect = (RectTransform)textGo.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                Text text = textGo.GetComponent<Text>();
                text.font = rowLabel != null ? rowLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = rowLabel != null ? rowLabel.fontSize : 14;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;

                int fieldIndex = i;
                Button button = go.GetComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                button.onClick.AddListener(() => FlipToggle(fieldIndex));
                toggleButtons[i] = button;
                toggleLabels[i] = text;
            }
            RefreshToggles();
        }

        private const int PaintDryingField = 153;
        private const int PaintPatternField = 139;
        private const int LegacyGrowthField = 133;
        private Button[][] choiceButtons;

        /// <summary>Four named buttons replace the pattern's numeric input and slider.</summary>
        private void BuildChoices()
        {
            choiceButtons = new Button[LabConfig.DeveloperFields.Count][];
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                if (!field.IsChoice) continue;
                Transform row = valueInputs[i].transform.parent;
                Text rowLabel = row.Find("SettingLabel")?.GetComponent<Text>();
                valueInputs[i].gameObject.SetActive(false);
                Slider slider = valueSliders[i];
                slider.interactable = false;
                foreach (Transform part in slider.transform) part.gameObject.SetActive(false);
                LayoutElement rowLayout = row.GetComponent<LayoutElement>();
                if (rowLayout != null) rowLayout.preferredHeight = 82f;
                LayoutElement sliderLayout = slider.GetComponent<LayoutElement>();
                if (sliderLayout != null) sliderLayout.preferredHeight = 76f;

                var container = new GameObject("SettingChoices", typeof(RectTransform));
                container.layer = slider.gameObject.layer;
                RectTransform containerRect = (RectTransform)container.transform;
                containerRect.SetParent(slider.transform, false);
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.offsetMin = containerRect.offsetMax = Vector2.zero;
                choiceButtons[i] = new Button[field.Choices.Count];
                for (int option = 0; option < field.Choices.Count; option++)
                {
                    var go = new GameObject("Choice" + option, typeof(RectTransform), typeof(Image), typeof(Button));
                    go.layer = container.layer;
                    RectTransform rect = (RectTransform)go.transform;
                    rect.SetParent(containerRect, false);
                    // Two rows of two keep four names readable on the narrow mobile panel; six short
                    // choices (Drum Count's AUTO and 1-5, #236) go three to a row.
                    int columns = field.Choices.Count <= 4 ? 2 : 3;
                    float width = 1f / columns;
                    float x = (option % columns) * width;
                    float y = 1f - ((option / columns) + 1) * .5f;
                    rect.anchorMin = new Vector2(x, y);
                    rect.anchorMax = new Vector2(x + width, y + .5f);
                    rect.offsetMin = new Vector2(3f, 2f);
                    rect.offsetMax = new Vector2(-3f, -2f);
                    var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    textGo.layer = go.layer;
                    RectTransform textRect = (RectTransform)textGo.transform;
                    textRect.SetParent(rect, false);
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                    Text text = textGo.GetComponent<Text>();
                    text.font = rowLabel != null ? rowLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 11;
                    text.fontStyle = FontStyle.Bold;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.white;
                    text.raycastTarget = false;
                    text.text = field.Choices[option];
                    Button button = go.GetComponent<Button>();
                    button.targetGraphic = go.GetComponent<Image>();
                    int fieldIndex = i, selected = option;
                    button.onClick.AddListener(() => SelectChoice(fieldIndex, selected));
                    choiceButtons[i][option] = button;
                }
            }
            RefreshChoices();
        }

        private void SelectChoice(int index, int selected)
        {
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            if (!field.IsChoice || selected < 0 || selected >= field.Choices.Count) return;
            valueInputs[index].text = selected.ToString(CultureInfo.InvariantCulture);
            RefreshToggles();
        }

        private void RefreshChoices()
        {
            if (choiceButtons == null) return;
            bool canEdit = network != null && (network.CanChooseConnection || network.IsDeveloperSolo
                || HostLobby || HostPausedBattle);
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                int selected = TryParse(valueInputs[i].text, out float value) ? Mathf.RoundToInt(value) : -1;
                for (int option = 0; option < choiceButtons[i].Length; option++)
                {
                    Button button = choiceButtons[i][option];
                    button.interactable = canEdit;
                    ((Image)button.targetGraphic).color = option == selected ? ToggleOnColor : ToggleOffColor;
                }
            }
            if (valueInputs.Length <= PaintPatternField || toggleButtons == null) return;
            bool legacy = TryParse(valueInputs[PaintPatternField].text, out float pattern)
                && pattern == (float)LabPaintPattern.Legacy;
            if (toggleButtons[LegacyGrowthField] != null)
                toggleButtons[LegacyGrowthField].interactable = canEdit && legacy;
        }

        private static bool ToggleIsOn(InputField input) =>
            input != null && TryParse(input.text, out float value) && value >= .5f;

        private void FlipToggle(int index)
        {
            if (index == LegacyGrowthField && valueInputs.Length > PaintPatternField
                && (!TryParse(valueInputs[PaintPatternField].text, out float pattern)
                    || pattern != (float)LabPaintPattern.Legacy)) return;
            InputField input = valueInputs[index];
            if (input == null) return;
            input.text = ToggleIsOn(input) ? "0" : "1";
            RefreshToggles();
        }

        /// <summary>숨긴 입력칸 값(DEFAULTS·방 설정 불러오기 포함)에 맞춰 버튼 글자·색을 바꾼다.</summary>
        private void RefreshToggles()
        {
            if (toggleButtons == null) return;
            for (int i = 0; i < toggleButtons.Length; i++)
            {
                if (toggleButtons[i] == null) continue;
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                bool on = ToggleIsOn(valueInputs[i]);
                string label = on ? field.OnText : field.OffText;
                if (toggleLabels[i].text != label) toggleLabels[i].text = label;
                var image = (Image)toggleButtons[i].targetGraphic;
                Color color = on ? ToggleOnColor : ToggleOffColor;
                if (image.color != color) image.color = color;
            }
            RefreshThrowFormInputs();
            RefreshPaintDryingInputs();
            RefreshChoices();
        }

        // Read the uncommitted form value so ARC/STRAIGHT immediately enables its own controls.
        // The approved runtime config and original asset change only through their existing APPLY paths.
        private void RefreshThrowFormInputs()
        {
            if (valueInputs == null || valueInputs.Length <= 118 || valueSliders == null
                || valueSliders.Length <= 118) return;
            bool canEdit = network != null && (network.CanChooseConnection || network.IsDeveloperSolo
                || HostLobby || HostPausedBattle);
            bool enabled = canEdit && ToggleIsOn(valueInputs[112]);
            for (int i = 113; i <= 118; i++)
            {
                if (valueInputs[i] != null)
                {
                    valueInputs[i].readOnly = !enabled;
                    valueInputs[i].interactable = enabled;
                }
                if (valueSliders[i] != null) valueSliders[i].interactable = enabled;
            }
        }

        // Pending form state controls editability immediately; only APPLY installs shared settings.
        private void RefreshPaintDryingInputs()
        {
            if (valueInputs == null || valueInputs.Length <= 156 || valueSliders == null
                || valueSliders.Length <= 156) return;
            bool canEdit = network != null && (network.CanChooseConnection || network.IsDeveloperSolo
                || HostLobby || HostPausedBattle);
            bool enabled = canEdit && ToggleIsOn(valueInputs[PaintDryingField]);
            for (int i = 154; i <= 156; i++)
            {
                if (valueInputs[i] != null)
                {
                    valueInputs[i].readOnly = !enabled;
                    valueInputs[i].interactable = enabled;
                }
                if (valueSliders[i] != null) valueSliders[i].interactable = enabled;
            }
            if (toggleButtons != null && toggleButtons.Length > PaintDryingField
                && toggleButtons[PaintDryingField] != null)
                toggleButtons[PaintDryingField].interactable = canEdit;
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
