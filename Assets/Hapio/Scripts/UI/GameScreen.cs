using System;
using System.Globalization;
using System.Text;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public enum GameScreenKind { Home, Lobby, StageResult, GameOver }

    /// <summary>Only binds saved, Inspector-editable Canvas references; never constructs a screen at runtime.</summary>
    public sealed class GameScreen : MonoBehaviour
    {
        public GameScreenKind kind;
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

        private GameSession session;
        private string[] roomKeys = Array.Empty<string>();
        private bool syncingDeveloper;
        private string localError;

        private void Awake()
        {
            session = GameSession.EnsureExists();
            Click(hostButton, () => { if (ApplyPort()) session.Host(); });
            Click(searchButton, () => { if (ApplyPort()) session.Network.StartRoomSearch(); });
            Click(cancelButton, () => { session.Network.CancelJoin(); session.Network.CancelRoomSearch(); });
            Click(joinDirectButton, () => { if (ApplyPort()) session.JoinDirect(addressInput.text); });
            Click(soloButton, () => session.StartSolo());
            Click(fewerPlayersButton, () => ChangePlayers(-1));
            Click(morePlayersButton, () => ChangePlayers(1));
            Click(startButton, () => session.StartGame());
            Click(homeButton, () => session.GoHome());
            Click(nextButton, () => session.NextStage());
            Click(viewerButton, () => session.OpenGourdViewer());
            Click(closeViewerButton, () => session.CloseGourdViewer());
            Click(resetViewerButton, () => session.ResetPreview());
            Click(developerButton, OpenDeveloper);
            Click(closeDeveloperButton, () => Show(developerPanel, false));
            Click(applyDeveloperButton, ApplyDeveloper);
            if (roomButtons != null)
            {
                roomKeys = new string[roomButtons.Length];
                for (int i = 0; i < roomButtons.Length; i++)
                {
                    int index = i;
                    Click(roomButtons[i], () => { if (roomKeys[index] != null) session.JoinRoom(roomKeys[index]); });
                }
            }
            if (slotButtons != null)
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
                case GameScreenKind.Home: RefreshHome(net); break;
                case GameScreenKind.Lobby: RefreshLobby(net); break;
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
            Text(title, complete ? "ALL STAGES CLEAR" : "STAGE " + session.State.stageIndex + " CLEAR");
            LabSnapshot result = session.ResultSnapshot;
            string goal = result == null ? string.Empty : net.config != null && net.config.GourdMode == LabGourdMode.Paint
                ? "PAINT " + (result.paintCoverage * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                : "TARGET HP " + result.hp;
            Text(status, result == null ? "Stage result is being prepared." : goal
                + "   ·   TIME LEFT " + Math.Ceiling(result.remaining).ToString(CultureInfo.InvariantCulture) + "s");
            var text = new StringBuilder();
            if (result != null && result.players != null)
                foreach (LabPlayerState player in result.players)
                    if (player != null)
                    {
                        text.Append("P").Append(player.seat + 1).Append("   HP ").Append(player.personalHp)
                            .Append("  ·  HITS ").Append(player.hitTotal).Append('\n')
                            .Append("GENERATED ").Append(player.generatedTotal).Append("  ·  COMBINED ").Append(player.combinedTotal)
                            .Append("  ·  PASSED ").Append(player.passedTotal).Append("\n\n");
                    }
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

        private void OpenDeveloper()
        {
            float[] values = session.Network.config.CaptureDeveloperValues();
            syncingDeveloper = true;
            for (int i = 0; developerInputs != null && i < Math.Min(values.Length, developerInputs.Length); i++)
            {
                developerInputs[i].SetTextWithoutNotify(values[i].ToString("0.######", CultureInfo.InvariantCulture));
                developerSliders[i].SetValueWithoutNotify(values[i]);
            }
            syncingDeveloper = false;
            Text(developerFeedback, "Development settings apply to this run only. The saved LabConfig is preserved.");
            Show(developerPanel, true);
        }

        private void ApplyDeveloper()
        {
            float[] values = new float[LabConfig.DeveloperFields.Count];
            for (int i = 0; i < values.Length; i++)
                if (!float.TryParse(developerInputs[i].text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                { Text(developerFeedback, LabConfig.DeveloperFields[i].Label + ": enter a number."); return; }
            Text(developerFeedback, session.ApplyDeveloperSettings(values, out string error) ? "Settings approved for this session." : error);
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
