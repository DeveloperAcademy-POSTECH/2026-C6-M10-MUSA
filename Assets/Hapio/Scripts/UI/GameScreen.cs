using System;
using System.Globalization;
using System.Text;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public enum GameScreenKind { Home, Lobby, StageResult, GameOver }

    /// <summary>
    /// Only binds saved, Inspector-editable Canvas references; never constructs a screen at runtime.
    /// 예외: STAGE CLEAR 화면의 칭호 표는 전투 결과 카드와 같은 생김새를 쓰도록 LabResultTable이 코드로 만든다.
    /// </summary>
    public sealed class GameScreen : MonoBehaviour
    {
        public GameScreenKind kind;
        [Tooltip("The saved Figma view owns Home/Lobby navigation; this component keeps the scoped DEV panel.")]
        public bool externalNavigation;
        public UnityEngine.UI.Text title, status, details, playerCount, developerFeedback;
        public UnityEngine.UI.Button hostButton, searchButton, cancelButton, joinDirectButton, soloButton;
        public UnityEngine.UI.Button fewerPlayersButton, morePlayersButton, startButton, homeButton, nextButton;
        public UnityEngine.UI.Button viewerButton, closeViewerButton, resetViewerButton;
        public UnityEngine.UI.Button developerButton, closeDeveloperButton, applyDeveloperButton;
        public UnityEngine.UI.InputField addressInput, portInput;
        public UnityEngine.UI.Button[] roomButtons, slotButtons;
        public GameObject developerPanel, viewerPanel;
        public UnityEngine.UI.RawImage viewerImage;
        public UnityEngine.UI.InputField[] developerInputs;
        public UnityEngine.UI.Slider[] developerSliders;
        public UnityEngine.UI.Button[] developerScopeButtons;
        public UnityEngine.UI.Text developerScopeLabel;
        public int DeveloperScope { get; private set; }
        /// <summary>STAGE CLEAR 화면 칭호 표 생김새. 기본값은 전투 결과 카드의 표와 같다.</summary>
        public LabResultTableStyle resultTableStyle = new LabResultTableStyle();
        /// <summary>칭호 표와 아래 안내 글 사이 간격</summary>
        public float resultTableGap = 12f;

        private GameSession session;
        private string[] roomKeys = Array.Empty<string>();
        private bool syncingDeveloper;
        private string localError;
        private RectTransform resultTable;
        private string resultTableKey;
        private bool UsesExternalNavigation => externalNavigation && (kind == GameScreenKind.Home || kind == GameScreenKind.Lobby);

        private void Awake()
        {
            session = GameSession.EnsureExists();
            if (!UsesExternalNavigation)
            {
                Click(hostButton, () => { if (ApplyPort()) session.Host(); });
                Click(searchButton, () => { if (ApplyPort()) session.Network.StartRoomSearch(); });
                Click(cancelButton, () => { session.Network.CancelJoin(); session.Network.CancelRoomSearch(); });
                Click(joinDirectButton, () => { if (ApplyPort()) session.JoinDirect(addressInput.text); });
                Click(soloButton, () => session.StartSolo());
                Click(fewerPlayersButton, () => ChangePlayers(-1));
                Click(morePlayersButton, () => ChangePlayers(1));
                Click(startButton, () => session.StartGame());
                Click(homeButton, () => session.GoHome());
            }
            Click(nextButton, () => session.NextStage());
            Click(viewerButton, () => session.OpenGourdViewer());
            Click(closeViewerButton, () => session.CloseGourdViewer());
            Click(resetViewerButton, () => session.ResetPreview());
            Click(developerButton, OpenDeveloper);
            for (int i = 0; developerScopeButtons != null && i < developerScopeButtons.Length; i++)
            {
                int scope = i;
                Click(developerScopeButtons[i], () => SelectDeveloperScope(scope));
            }
            Click(closeDeveloperButton, () => Show(developerPanel, false));
            Click(applyDeveloperButton, ApplyDeveloper);
            if (!UsesExternalNavigation && roomButtons != null)
            {
                roomKeys = new string[roomButtons.Length];
                for (int i = 0; i < roomButtons.Length; i++)
                {
                    int index = i;
                    Click(roomButtons[i], () => { if (roomKeys[index] != null) session.JoinRoom(roomKeys[index]); });
                }
            }
            if (!UsesExternalNavigation && slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int slot = i + 1;
                    Click(slotButtons[i], () => session.SelectSlot(session.Network.LocalSlotNumber == slot ? 0 : slot));
                }
            if (developerInputs != null && developerSliders != null)
                for (int i = 0; i < Math.Min(developerInputs.Length, developerSliders.Length); i++)
                {
                    int index = i;
                    if (developerInputs[i] != null) developerInputs[i].onValueChanged.AddListener(raw => SyncSlider(index, raw));
                    if (developerSliders[i] != null) developerSliders[i].onValueChanged.AddListener(value => SyncInput(index, value));
                }
            if (portInput != null) portInput.text = session.Network.port.ToString(CultureInfo.InvariantCulture);
            Show(developerPanel, false);
            Show(viewerPanel, false);
        }

        private void Update()
        {
            if (session == null || session.Network == null) return;
            LabNetwork net = session.Network;
            switch (kind)
            {
                case GameScreenKind.Home: if (!UsesExternalNavigation) RefreshHome(net); break;
                case GameScreenKind.Lobby: if (!UsesExternalNavigation) RefreshLobby(net); break;
                case GameScreenKind.StageResult: RefreshResult(net); break;
                case GameScreenKind.GameOver:
                    Text(title, "GAME OVER");
                    Text(status, session.State.endReason);
                    Text(details, "The run has ended for everyone. Tap HOME to return.");
                    break;
            }
            Show(developerButton == null ? null : developerButton.gameObject, net.DeveloperBuildAvailable);
            Interactable(developerButton, net.CanChooseConnection || (net.IsHost && net.Snapshot == null));
            if (developerPanel != null && developerPanel.activeSelf)
                Interactable(applyDeveloperButton, net.CanChooseConnection || (net.IsHost && net.Snapshot == null));
        }

        private void RefreshHome(LabNetwork net)
        {
            Text(title, "HAPIO");
            Text(status, !string.IsNullOrEmpty(localError) ? localError : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice : net.RoomSearchMessage);
            Text(details, net.IsJoiningRoom ? net.Status : "Create a room or choose a discovered room. LAN and personal hotspot use the same connection.");
            Text(playerCount, "ROOM PLAYERS  " + net.TargetParticipantCount + " / 5");
            Interactable(hostButton, net.CanChooseConnection);
            Interactable(searchButton, !net.IsJoiningRoom);
            Interactable(joinDirectButton, net.CanChooseConnection);
            Interactable(soloButton, net.CanChooseConnection);
            Interactable(fewerPlayersButton, net.CanChooseConnection && net.TargetParticipantCount > 2);
            Interactable(morePlayersButton, net.CanChooseConnection && net.TargetParticipantCount < 5);
            for (int i = 0; i < roomKeys.Length; i++)
            {
                bool visible = i < net.DiscoveredRooms.Count;
                Show(roomButtons[i] == null ? null : roomButtons[i].gameObject, visible);
                roomKeys[i] = visible ? net.DiscoveredRooms[i].Key : null;
                if (!visible) continue;
                Label(roomButtons[i], net.DiscoveredRooms[i].Name + "   JOIN");
                Interactable(roomButtons[i], net.CanJoinDiscoveredRoom(roomKeys[i]));
            }
        }

        private void RefreshLobby(LabNetwork net)
        {
            Text(title, "ROOM LOBBY");
            Text(status, net.ConnectedRoomName + "\n" + net.ParticipantCount + " / " + net.TargetParticipantCount + " PLAYERS");
            var listing = new StringBuilder("Choose one unique position before the Host starts.\n");
            for (int i = 0; slotButtons != null && i < slotButtons.Length; i++)
            {
                int slot = i + 1;
                bool available = slot <= net.TargetParticipantCount;
                Show(slotButtons[i] == null ? null : slotButtons[i].gameObject, available);
                if (!available) continue;
                ulong owner = net.GetSlotOwner(slot);
                bool own = net.LocalSlotNumber == slot;
                bool occupied = owner != ulong.MaxValue;
                Label(slotButtons[i], "P" + slot + (own ? "  YOU · CLEAR" : occupied ? "  TAKEN" : "  SELECT"));
                Interactable(slotButtons[i], net.CanSelectPlayerSlot && (!occupied || own));
                listing.Append("P").Append(slot).Append(own ? "  YOU" : occupied ? "  CONNECTED" : "  EMPTY").Append('\n');
            }
            listing.Append(net.SlotSelectionPending ? "Waiting for Host approval…" : !string.IsNullOrEmpty(session.UiNotice) ? session.UiNotice : net.Notice);
            Text(details, listing.ToString());
            Show(startButton == null ? null : startButton.gameObject, net.IsHost);
            Interactable(startButton, net.CanStartBattle);
            Label(startButton, "START · 3 STAGES");
        }

        private void RefreshResult(LabNetwork net)
        {
            bool complete = session.State.phase == GameFlowPhase.Completed;
            GameStageDefinition stage = session.CurrentStageDefinition;
            string stageName = stage != null ? stage.displayName : string.Empty;
            Text(title, complete ? "ALL STAGES CLEAR" : "STAGE " + session.State.stageIndex + " · " + stageName + " CLEAR");
            LabSnapshot result = session.ResultSnapshot;
            string goal = result == null ? string.Empty : net.config != null && net.config.GourdMode == LabGourdMode.Paint
                ? "PAINT " + (result.paintCoverage * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                : "TARGET HP " + result.hp;
            Text(status, result == null ? "Stage result is being prepared." : goal
                + "   ·   TIME LEFT " + Math.Ceiling(result.remaining).ToString(CultureInfo.InvariantCulture) + "s");
            ShowResultTable(net, result);
            var text = new StringBuilder();
            text.Append(net.IsHost && !session.CanReturnHome && complete
                ? "Waiting for every player to receive the final result…"
                : net.IsHost && !complete && !session.CanAdvanceStage
                    ? "Waiting for every player to receive the stage result…"
                    : complete ? "The run is complete. Return home when ready."
                        : net.IsHost ? "Start the next stage when everyone is ready."
                            : "Waiting for the Host to start the next stage.");
            if (!string.IsNullOrEmpty(session.UiNotice)) text.Append("\n\n").Append(session.UiNotice);
            Text(details, text.ToString());
            Show(nextButton == null ? null : nextButton.gameObject, !complete && net.IsHost);
            Show(homeButton == null ? null : homeButton.gameObject, true);
            Interactable(nextButton, session.CanAdvanceStage);
            Interactable(homeButton, !complete || session.CanReturnHome);
            Label(homeButton, complete ? "HOME" : "QUIT RUN");
            Label(nextButton, "NEXT · STAGE " + (session.State.stageIndex + 1));
            Show(viewerButton == null ? null : viewerButton.gameObject, net.config != null && net.config.CompletedGourdViewerEnabled
                && net.config.GourdMode == LabGourdMode.Paint && result != null && result.paintCoverage >= 1f);
            Texture texture = session.PreviewTexture;
            Show(viewerPanel, session.IsViewerOpen && texture != null);
            if (viewerImage != null) viewerImage.texture = texture;
        }

        /// <summary>
        /// 글자 목록 대신 전투 결과 카드와 같은 칭호 표를 Details 자리 위쪽에 그린다.
        /// 같은 결과는 한 번만 그리고, 안내 글(Details)은 표 바로 밑으로 내린다.
        /// </summary>
        private void ShowResultTable(LabNetwork net, LabSnapshot result)
        {
            if (details == null) return;
            if (result == null || result.players == null)
            {
                if (resultTable != null) resultTable.gameObject.SetActive(false);
                resultTableKey = null;
                return;
            }
            string key = result.sessionId + ":" + result.round + ":" + net.LocalPlayerId;
            if (key == resultTableKey && resultTable != null && resultTable.gameObject.activeSelf) return;
            resultTableKey = key;
            var area = details.rectTransform;
            if (resultTable == null)
            {
                resultTable = LabResultTable.Create(area.parent, "ResultDashboard");
                // 안내 글은 표 밑에 가운데 맞춤으로 둔다.
                details.alignment = TextAnchor.UpperCenter;
            }
            // Details 자리의 윗변 가운데에 표 윗변을 맞춘다.
            resultTable.anchorMin = resultTable.anchorMax = new Vector2((area.anchorMin.x + area.anchorMax.x) * .5f, area.anchorMax.y);
            resultTable.anchoredPosition = Vector2.zero;
            var players = Array.FindAll(result.players, p => p != null);
            Array.Sort(players, (a, b) => a.seat.CompareTo(b.seat));
            float height = LabResultTable.Fill(resultTable,
                LabResultTable.RowsFor(LabResultStats.Awards(players, result.phase == LabPhase.Victory),
                    net.LocalPlayerId, result.playedSeconds),
                resultTableStyle, details.font);
            // 안내 글이 표 밑에서 시작하도록 Details 윗변을 표 높이만큼 내린다.
            area.offsetMax = new Vector2(area.offsetMax.x, -(height + resultTableGap));
            // Details 바로 다음 순서로 둬서, 나중에 그려지는 완성 박 보기 화면이 표를 덮게 한다.
            resultTable.SetSiblingIndex(area.GetSiblingIndex() + 1);
        }

        private void ChangePlayers(int delta)
        {
            localError = null;
            session.Network.SetTargetParticipantCount(session.Network.TargetParticipantCount + delta);
        }

        private bool ApplyPort()
        {
            localError = null;
            if (portInput != null && ushort.TryParse(portInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value) && value > 0)
            { session.Network.port = value; return true; }
            localError = "Enter a port from 1 to 65535.";
            return false;
        }

        public void OpenDeveloper()
        {
            SelectDeveloperScope(0);
            Text(developerFeedback, "공통 설정과 요괴별 설정은 각각 APPLY로 적용합니다. 저장된 LabConfig는 유지됩니다.");
            Show(developerPanel, true);
        }

        /// <summary>Display only the selected settings scope. Switching tabs discards unapplied form edits.</summary>
        public void SelectDeveloperScope(int scope)
        {
            if (session == null || scope < 0 || scope > 3) return;
            float[] values = session.CaptureDeveloperSettings(scope);
            if (developerInputs == null || developerSliders == null
                || developerInputs.Length != values.Length || developerSliders.Length != values.Length)
            {
                Text(developerFeedback, "DEV UI 설정 수가 다릅니다. 게임 Scene 업그레이드를 실행해주세요.");
                return;
            }
            DeveloperScope = scope;
            syncingDeveloper = true;
            for (int i = 0; i < values.Length; i++)
            {
                developerInputs[i].SetTextWithoutNotify(values[i].ToString("0.######", CultureInfo.InvariantCulture));
                developerSliders[i].SetValueWithoutNotify(values[i]);
                Show(developerInputs[i].transform.parent.gameObject, GameStageSettings.IsFieldVisible(scope, i));
            }
            syncingDeveloper = false;
            string name = scope == 0 ? "공통" : GameStageCatalog.RequireDefault().Get(scope).displayName;
            Text(developerScopeLabel, name + " 설정");
            for (int i = 0; developerScopeButtons != null && i < developerScopeButtons.Length; i++)
                if (developerScopeButtons[i] != null && developerScopeButtons[i].targetGraphic != null)
                    developerScopeButtons[i].targetGraphic.color = i == scope
                        ? new Color(.18f, .43f, .47f, 1f) : new Color(.12f, .18f, .21f, 1f);
            Text(developerFeedback, name + " 설정 · 탭을 바꾸기 전에 APPLY로 적용해주세요.");
            var scroll = developerPanel != null ? developerPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true) : null;
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void ApplyDeveloper()
        {
            float[] values = session.CaptureDeveloperSettings(DeveloperScope);
            for (int i = 0; i < values.Length; i++)
            {
                if (!GameStageSettings.IsFieldVisible(DeveloperScope, i)) continue;
                if (!float.TryParse(developerInputs[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                { Text(developerFeedback, LabConfig.DeveloperFields[i].Label + ": enter a number."); return; }
            }
            bool applied = session.ApplyDeveloperSettings(DeveloperScope, values, out string error);
            if (applied) SelectDeveloperScope(DeveloperScope);
            Text(developerFeedback, applied ? "선택한 범위의 설정을 적용했습니다. 스테이지 전환과 새 판에도 유지됩니다." : error);
        }

        private void SyncSlider(int index, string raw)
        {
            if (syncingDeveloper || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return;
            syncingDeveloper = true;
            developerSliders[index].SetValueWithoutNotify(value);
            syncingDeveloper = false;
        }
        private void SyncInput(int index, float value)
        {
            if (syncingDeveloper) return;
            syncingDeveloper = true;
            developerInputs[index].SetTextWithoutNotify(value.ToString("0.######", CultureInfo.InvariantCulture));
            syncingDeveloper = false;
        }
        private static void Click(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.AddListener(action); }
        private static void Label(UnityEngine.UI.Button button, string text) { if (button != null) Text(button.GetComponentInChildren<UnityEngine.UI.Text>(true), text); }
        private static void Text(UnityEngine.UI.Text label, string value) { if (label != null && label.text != value) label.text = value ?? string.Empty; }
        private static void Interactable(UnityEngine.UI.Button button, bool value) { if (button != null) button.interactable = value; }
        private static void Show(GameObject target, bool visible) { if (target != null && target.activeSelf != visible) target.SetActive(visible); }
    }
}
