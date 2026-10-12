using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace C6Lab
{
    /// <summary>Saved Canvas references are editable in the Scene; only values are bound in code.</summary>
    public sealed class LabHud : MonoBehaviour
    {
        public LabNetwork network;
        public GameObject sessionControls;
        public InputField hostAddress;
        public InputField port;
        public Button hostButton;
        public Button joinButton;
        public Button findHostButton;
        public Button startButton;
        public Button generateButton;
        public Text generateLabel;
        public Button leaveButton;
        public Text statusText;
        public Text noticeText;
        public Text peersText;
        public Text hpText;
        public Text timeText;
        public Text staminaText;
        public Text orbsText;
        public Text seatText;
        public Image monsterHpFill;
        public Image playerHpFill;
        public Image staminaFill;

        private void Awake()
        {
            EnsureUiInputModule();
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (findHostButton != null) findHostButton.onClick.AddListener(FindHost);
            if (startButton != null) startButton.onClick.AddListener(() => network?.StartBattle());
            if (generateButton != null) generateButton.onClick.AddListener(() => network?.Generate());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => network?.Stop());
        }

        private void Start()
        {
            if (hostAddress != null && network != null) hostAddress.text = network.hostAddress;
            if (port != null && network != null) port.text = network.port.ToString();
        }

        private void Update()
        {
            if (network == null) return;
            var snapshot = network.Snapshot;
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            SetVisible(sessionControls, !playing);
            SetVisible(statusText == null ? null : statusText.gameObject, !playing);
            bool connected = network.IsConnected;
            if (hostButton != null) hostButton.interactable = network.CanChooseConnection;
            if (joinButton != null) joinButton.interactable = network.CanChooseConnection;
            if (findHostButton != null) findHostButton.interactable = network.CanChooseConnection;
            if (startButton != null) startButton.interactable = network.CanStartBattle;
            if (generateButton != null) generateButton.interactable = connected && snapshot != null && snapshot.phase == LabPhase.Playing;
            if (network.config != null) Set(generateLabel, "GENERATE / " + network.config.GenerateCost.ToString("0.#"));
            if (leaveButton != null) leaveButton.interactable = connected || !network.CanChooseConnection;
            Set(statusText, network.Status);
            Set(noticeText, network.Notice);
            Set(peersText, network.IsDeveloperSolo
                ? "DEVELOPER SOLO 1 / 1"
                : "PLAYERS " + network.ParticipantCount + " / " + network.TargetParticipantCount);
            if (snapshot == null)
            {
                Set(hpText, "YOKAI HP —");
                Set(timeText, "TIME —");
                Set(staminaText, "STAMINA —");
                Set(orbsText, "ORBS —");
                Set(seatText, connected ? "PLAYER " + network.LocalPlayerId : "NO SESSION");
                SetFill(monsterHpFill, 0f);
                SetFill(playerHpFill, connected ? 1f : 0f);
                SetFill(staminaFill, 0f);
                return;
            }
            Set(hpText, "YOKAI HP " + snapshot.hp + " / " + network.config.MonsterMaxHp);
            Set(timeText, "TIME " + Mathf.CeilToInt((float)snapshot.remaining));
            SetFill(monsterHpFill, network.config == null ? 0f : (float)snapshot.hp / network.config.MonsterMaxHp);
            SetFill(playerHpFill, 1f);
            LabPlayerState local = Array.Find(snapshot.players ?? Array.Empty<LabPlayerState>(), p => p.id == network.LocalPlayerId);
            Set(staminaText, local == null ? "STAMINA —" : "STAMINA " + Mathf.FloorToInt(local.stamina) + " / " + network.config.StaminaMax);
            SetFill(staminaFill, local == null || network.config == null ? 0f : local.stamina / network.config.StaminaMax);
            int count = 0;
            foreach (var orb in snapshot.orbs ?? Array.Empty<LabOrbState>()) if (orb.owner == network.LocalPlayerId && !orb.inFlight) count++;
            Set(orbsText, "ORBS " + count + " / " + network.config.OrbStorageLimit);
            Set(seatText, local == null ? "PLAYER —" : "PLAYER " + local.id + " · SEAT "
                + (local.seat + 1) + (network.IsDeveloperSolo ? " · SOLO" : string.Empty));
        }

        private void Host()
        {
            if (network == null || !TryPort()) return;
            network.Host();
        }

        private void Join()
        {
            if (network == null || !TryPort()) return;
            network.Join(hostAddress == null ? network.hostAddress : hostAddress.text);
        }

        private void FindHost()
        {
            if (network == null || !TryPort()) return;
            network.FindAndJoinRoom();
        }

        private bool TryPort()
        {
            if (port == null || !ushort.TryParse(port.text, out var parsed) || parsed == 0)
            {
                if (noticeText != null) noticeText.text = "Enter port 1–65535.";
                return false;
            }
            network.port = parsed;
            return true;
        }

        private static void EnsureUiInputModule()
        {
            EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            foreach (InputSystemUIInputModule module in eventSystem.GetComponents<InputSystemUIInputModule>())
                module.enabled = false;

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null) standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            standalone.enabled = true;
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }

        private static void Set(Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null) target.fillAmount = Mathf.Clamp01(value);
        }
    }
}
