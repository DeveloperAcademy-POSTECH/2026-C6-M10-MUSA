using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public enum GameScreenKind { Home, Lobby, StageResult, GameOver }

    /// <summary>
    /// Only binds saved, Inspector-editable Canvas references; never constructs a screen at runtime.
    /// The saved Figma result view reuses LabResultStats; the legacy territory result retains LabResultTable.
    /// </summary>
    public sealed class GameScreen : MonoBehaviour
    {
        public GameScreenKind kind;
        public GameStageResultView resultView;
        public GameObject legacyResultContent;
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
        // 2:2 (#281) result page: the final Blue/Red bar and the saved title colour for a draw.
        private LabTeamBar resultBar;
        private Color? resultTitleColor;
        private string[] roomKeys = Array.Empty<string>();
        private bool syncingDeveloper;
        private string localError;
        private RectTransform resultTable;
        private string resultTableKey;
        // ALL STAGES CLEAR: false면 3스테이지 표, true면 전체 게임 대시보드. 새 판이면 3스테이지 표부터.
        private bool showOverall;
        private string overallRunId;
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
            if (resultView != null)
            {
                Click(resultView.nextButton, OnNextButton);
                Click(resultView.viewerButton, () => session.OpenGourdViewer());
                Click(resultView.homeButton, () => session.GoHome());
            }
            Click(nextButton, OnNextButton);
            Click(viewerButton, () => session.OpenGourdViewer());
            Click(closeViewerButton, () => session.CloseGourdViewer());
            Click(resetViewerButton, () => session.ResetPreview());
            GamePreviewAspect.Attach(viewerImage);
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
                    if (resultView != null) { RefreshDesignedResult(net, false); break; }
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
            if (resultView != null)
            {
                Show(resultView.gameObject, !session.IsTerritory);
                Show(resultView.background.gameObject, !session.IsTerritory);
                Show(legacyResultContent, session.IsTerritory);
                if (!session.IsTerritory) { RefreshDesignedResult(net, true); return; }
            }
            if (session.IsTerritory) { RefreshTerritoryResult(net); return; }
            bool complete = session.State.phase == GameFlowPhase.Completed;
            GameStageDefinition stage = session.CurrentStageDefinition;
            string stageName = stage != null ? stage.displayName : string.Empty;
            Text(title, complete ? "ALL STAGES CLEAR" : "STAGE " + session.State.stageIndex + " · " + stageName + " CLEAR");
            LabSnapshot result = session.ResultSnapshot;
            string goal = result == null ? string.Empty : net.config != null && net.config.GourdMode == LabGourdMode.Paint
                ? "PAINT " + (result.paintCoverage * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                : "TARGET HP " + result.hp;
            var stages = complete ? session.StageResults : null;
            bool hasOverall = stages != null && stages.Count > 0;
            if (overallRunId != session.State.runId) { overallRunId = session.State.runId; showOverall = false; }
            // 3스테이지(마지막 스테이지) 표를 먼저 보여주고, 버튼으로 전체 표와 오간다.
            bool overall = hasOverall && showOverall;
            Text(status, overall ? "전체 " + stages.Count + "스테이지 평균 기록"
                : result == null ? "Stage result is being prepared."
                : (hasOverall ? session.State.stageIndex + "스테이지 기록   ·   " : string.Empty) + goal
                + "   ·   TIME LEFT " + Math.Ceiling(result.remaining).ToString(CultureInfo.InvariantCulture) + "s");
            ShowResultTable(net, result, overall ? stages : null);
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
            // ALL STAGES CLEAR에서는 NEXT 자리를 "전체 기록 보기 / 3스테이지 기록 보기" 전환 버튼으로 쓴다 (모든 기기).
            Show(nextButton == null ? null : nextButton.gameObject, complete ? hasOverall : net.IsHost);
            Show(homeButton == null ? null : homeButton.gameObject, true);
            Interactable(nextButton, complete ? hasOverall : session.CanAdvanceStage);
            Interactable(homeButton, !complete || session.CanReturnHome);
            Label(homeButton, complete ? "HOME" : "QUIT RUN");
            Label(nextButton, complete ? (showOverall ? session.State.stageIndex + "스테이지 기록 보기" : "전체 기록 보기")
                : "NEXT · STAGE " + (session.State.stageIndex + 1));
            Show(viewerButton == null ? null : viewerButton.gameObject, net.config != null && net.config.CompletedGourdViewerEnabled
                && net.config.GourdMode == LabGourdMode.Paint && result != null && result.paintCoverage >= 1f);
            Texture texture = session.PreviewTexture;
            Show(viewerPanel, session.IsViewerOpen && texture != null);
            if (viewerImage != null) viewerImage.texture = texture;
        }

        private void RefreshDesignedResult(LabNetwork net, bool victory)
        {
            Show(legacyResultContent, false);
            bool complete = victory && session.State.phase == GameFlowPhase.Completed;
            if (overallRunId != session.State.runId) { overallRunId = session.State.runId; showOverall = complete; }
            bool hasOverall = complete && session.StageResults.Count > 0;
            bool overall = hasOverall && showOverall;
            // Defeat is presentation-only: never promote a failed run into the committed victory cache.
            LabSnapshot result = victory ? session.ResultSnapshot : net.Snapshot;
            if (!victory && (result == null || result.phase != LabPhase.Defeat
                || result.sessionId != session.State.battleSessionId || result.round != session.State.battleRound)) result = null;
            var rows = overall ? LabResultTable.OverallRowsFor(session.StageResults, net.LocalPlayerId)
                : result == null ? new List<LabResultRow>() : LabResultTable.RowsFor(
                    LabResultStats.Awards(result.players, victory), net.LocalPlayerId, result.playedSeconds);
            int seat = result != null && result.players != null
                ? Array.FindIndex(result.players, player => player != null && player.id == net.LocalPlayerId) : -1;
            string localName = seat < 0 ? null : LabResultStats.PlayerName(result.players[seat]);
            for (int i = 0; i < rows.Count; i++)
            {
                LabResultRow row = rows[i];
                string who = localName != null ? row.Who.Replace(localName, localName + "(나)") : row.Who;
                rows[i] = new LabResultRow(row.Title == "북 독점" ? "북 독점왕" : row.Title, who, row.Record, row.Mine);
            }
            string guidance = !victory ? "요괴를 다시 봉인하고싶다면\n게임을 새로 시작하시오"
                : complete ? "모든 요괴의 봉인을 마쳤소\n견습생 여러분, 수고하셨소"
                : "다음 스테이지가 시작될 동안\n잠시만 기다리시오";
            if (!string.IsNullOrEmpty(session.UiNotice)) guidance = session.UiNotice;
            resultView.Present(victory, session.State.stageIndex,
                session.CurrentStageDefinition != null ? session.CurrentStageDefinition.displayName : "요괴",
                result, rows, guidance, overall, complete);
            Show(resultView.nextButton.gameObject, complete && hasOverall);
            Interactable(resultView.nextButton, complete ? hasOverall : session.CanAdvanceStage);
            GameStageResultView.Label(resultView.nextButton, complete
                ? (overall ? session.State.stageIndex + "스테이지 기록 보기" : "전체 기록 보기") : "다음 스테이지");
            Interactable(resultView.homeButton, !complete || session.CanReturnHome);
            GameStageResultView.Label(resultView.homeButton, "나가기");
            Interactable(resultView.viewerButton, victory && session.PreviewTexture != null);
            Texture texture = session.PreviewTexture;
            Show(viewerPanel, session.IsViewerOpen && texture != null);
            if (viewerImage != null) viewerImage.texture = texture;
        }

        /// <summary>2:2 (#281) result: the winning team or a draw, each team's share of the whole gourd, then back to the lobby.</summary>
        private void RefreshTerritoryResult(LabNetwork net)
        {
            LabSnapshot result = session.ResultSnapshot;
            LabTeam winner = result != null ? result.teamWinner : LabTeam.None;
            Text(title, result == null ? "결과를 정리하고 있소" : winner == LabTeam.Blue ? "청팀 승리!"
                : winner == LabTeam.Red ? "홍팀 승리!" : "무승부");
            if (title != null)
            {
                if (resultTitleColor == null) resultTitleColor = title.color;
                title.color = winner == LabTeam.Blue ? LabTeamBar.Blue : winner == LabTeam.Red ? LabTeamBar.Red
                    : resultTitleColor.Value;
            }
            int cells = result != null ? Math.Max(1, result.paintCellCount) : 1;
            // The same Blue/Red bar as the battle, filled with the final shares and their numbers.
            if (result != null && status != null)
            {
                if (resultBar == null)
                    resultBar = LabTeamBar.Create((RectTransform)status.transform.parent, title != null ? title.font : null,
                        new Vector2(.5f, .83f));
                resultBar.ShowFinal(result, status.color);
            }
            Text(status, string.Empty);
            var text = new StringBuilder();
            if (result != null && result.players != null)
                foreach (LabTeam team in new[] { LabTeam.Blue, LabTeam.Red })
                {
                    text.Append(team == LabTeam.Blue ? "청팀" : "홍팀").Append(team == winner ? " · 승리" : string.Empty).Append('\n');
                    foreach (LabPlayerState player in result.players)
                        if (player != null && LabTeams.TeamOfSeat(player.seat) == team)
                            text.Append("   P").Append(player.seat + 1)
                                .Append("   칠한 넓이 ").Append(Percent(player.paintedCells, cells))
                                .Append("  ·  빼앗은 넓이 ").Append(Percent(player.stolenCells, cells)).Append('\n');
                    text.Append('\n');
                }
            bool solo = net.IsDeveloperSolo;
            text.Append(solo ? "혼자 확인을 마쳤소. HOME으로 돌아가시오."
                : net.IsHost ? (session.CanAdvanceStage ? "모두 준비되면 로비로 돌아가시오." : "모두가 결과를 받기를 기다리고 있소…")
                : "방장이 로비로 돌아가기를 기다리시오.");
            if (!string.IsNullOrEmpty(session.UiNotice)) text.Append("\n\n").Append(session.UiNotice);
            Text(details, text.ToString());
            Show(nextButton == null ? null : nextButton.gameObject, net.IsHost && !solo);
            Interactable(nextButton, session.CanAdvanceStage);
            Label(nextButton, "로비로");
            Show(homeButton == null ? null : homeButton.gameObject, true);
            Interactable(homeButton, !net.IsHost || session.CanReturnHome);
            Label(homeButton, "HOME");
            Show(viewerButton == null ? null : viewerButton.gameObject, session.PreviewTexture != null);
            Texture texture = session.PreviewTexture;
            Show(viewerPanel, session.IsViewerOpen && texture != null);
            if (viewerImage != null) viewerImage.texture = texture;
            // The finished gourd viewer covers the whole result; the bar belongs to the result page only.
            if (resultBar != null) Show(resultBar.gameObject, result != null && !(session.IsViewerOpen && texture != null));
        }

        private static string Percent(int count, int cells) =>
            (count * 100f / cells).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        /// <summary>
        /// 글자 목록 대신 전투 결과 카드와 같은 칭호 표를 Details 자리 위쪽에 그린다.
        /// stages가 있으면(ALL STAGES CLEAR) 스테이지 평균으로 매긴 전체 게임 대시보드를 그린다.
        /// 같은 결과는 한 번만 그리고, 안내 글(Details)은 표 바로 밑으로 내린다.
        /// </summary>
        private void ShowResultTable(LabNetwork net, LabSnapshot result, IReadOnlyList<LabResultStage> stages = null)
        {
            if (details == null) return;
            bool overall = stages != null && stages.Count > 0;
            if (!overall && (result == null || result.players == null))
            {
                if (resultTable != null) resultTable.gameObject.SetActive(false);
                resultTableKey = null;
                return;
            }
            string key = overall
                ? "overall:" + session.State.runId + ":" + stages.Count + ":" + (result != null ? result.sessionId + ":" + result.round : "")
                    + ":" + net.LocalPlayerId
                : result.sessionId + ":" + result.round + ":" + net.LocalPlayerId;
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
            List<LabResultRow> rows;
            if (overall) rows = LabResultTable.OverallRowsFor(stages, net.LocalPlayerId);
            else
            {
                var players = Array.FindAll(result.players, p => p != null);
                Array.Sort(players, (a, b) => a.seat.CompareTo(b.seat));
                rows = LabResultTable.RowsFor(LabResultStats.Awards(players, result.phase == LabPhase.Victory),
                    net.LocalPlayerId, result.playedSeconds);
            }
            float height = LabResultTable.Fill(resultTable, rows, resultTableStyle, details.font);
            // 안내 글이 표 밑에서 시작하도록 Details 윗변을 표 높이만큼 내린다.
            area.offsetMax = new Vector2(area.offsetMax.x, -(height + resultTableGap));
            // Details 바로 다음 순서로 둬서, 나중에 그려지는 완성 박 보기 화면이 표를 덮게 한다.
            resultTable.SetSiblingIndex(area.GetSiblingIndex() + 1);
        }

        /// <summary>
        /// NEXT 버튼: 스테이지 결과에서는 다음 스테이지, ALL STAGES CLEAR에서는 표 전환.
        /// 2:2 (#281)는 한 판이라 결과가 곧 Completed이고, 이 버튼은 "로비로"다.
        /// </summary>
        private void OnNextButton()
        {
            if (session.State.phase == GameFlowPhase.Completed && !session.IsTerritory) { showOverall = !showOverall; return; }
            session.NextStage();
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
            if (session == null || session.IsCreatingRoom) return;
            // A 2:2 room (#281) opens on its own tab; story rooms keep Common first.
            SelectDeveloperScope(session.IsTerritory ? GameStageSettings.TerritoryScope : 0);
            Text(developerFeedback, "공통 설정과 요괴별 설정은 각각 APPLY로 적용합니다. 저장된 LabConfig는 유지됩니다.");
            Show(developerPanel, true);
        }

        /// <summary>Display only the selected settings scope. Switching tabs discards unapplied form edits.</summary>
        public void SelectDeveloperScope(int scope)
        {
            if (session == null || scope < 0 || scope > GameStageSettings.TerritoryScope) return;
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
            string name = scope == 0 ? "공통" : scope == GameStageSettings.TerritoryScope ? "2:2 땅따먹기"
                : GameStageCatalog.RequireDefault().Get(scope).displayName;
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
