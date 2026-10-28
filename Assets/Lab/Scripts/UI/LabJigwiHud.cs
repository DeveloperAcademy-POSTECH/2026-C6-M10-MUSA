using System;
using System.Linq;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Saved Canvas controls for Host-only manual tests; effect feedback is visible to its recipient.</summary>
    public sealed class LabJigwiHud : MonoBehaviour
    {
        public LabNetwork network;
        public Canvas canvas;
        public UnityEngine.UI.Image heat;
        public UnityEngine.UI.Text effectText, feedback;
        public UnityEngine.UI.Text stageNotice;
        public UnityEngine.UI.Button testToggle, fireButton, generationButton, closeButton;
        public UnityEngine.UI.Button[] playerButtons;
        public GameObject testPanel;
        private int targetSlot;
        private bool choseTarget;
        private double lastServerTime = -1d;
        private float receivedAt;
        private bool wired;
        private LabJigwiScreenPresentation screenPresentation;
        private float blockedUntilRealtime = -1f;

        public void Bind(LabNetwork source, Canvas targetCanvas)
        {
            network = source;
            canvas = targetCanvas != null ? targetCanvas.rootCanvas : null;
            EnsureWidgets();
            Wire();
            EnsureScreenPresentation();
        }

        private void Start()
        {
            if (network == null) network = FindFirstObjectByType<LabNetwork>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            EnsureWidgets();
            Wire();
            EnsureScreenPresentation();
        }

        private void EnsureScreenPresentation()
        {
            if (!Application.isPlaying || canvas == null || network == null) return;
            LabHud battleHud = canvas.GetComponentInChildren<LabHud>(true);
            Camera camera = network.BattleCamera != null ? network.BattleCamera : battleHud != null ? battleHud.battleCamera : null;
            screenPresentation = LabJigwiScreenPresentation.EnsureFor(network, canvas, camera);
        }

        /// <summary>Editor creation and a missing-reference fallback share the same small hierarchy.</summary>
        public void EnsureWidgets()
        {
            if (canvas == null) return;
            if (heat == null)
            {
                heat = Image("Jigwi Heat", canvas.transform, new Color(1f, .22f, .015f, 0f));
                Rect(heat.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                heat.raycastTarget = false;
                for (int side = 0; side < 2; side++)
                {
                    var band = Image(side == 0 ? "Left Flames" : "Right Flames", heat.transform,
                        new Color(1f, .18f, .01f, .18f));
                    Rect(band.rectTransform, new Vector2(side == 0 ? 0f : .96f, 0f),
                        new Vector2(side == 0 ? .04f : 1f, 1f), Vector2.zero, Vector2.zero);
                    band.raycastTarget = false;
                }
                heat.gameObject.SetActive(false);
            }
            if (effectText == null)
            {
                effectText = Label("Jigwi Status", canvas.transform, 17);
                Rect(effectText.rectTransform, new Vector2(.12f, .68f), new Vector2(.88f, .78f), Vector2.zero, Vector2.zero);
                effectText.color = new Color(.48f, .07f, .01f);
                effectText.gameObject.SetActive(false);
            }
            if (testToggle == null) testToggle = Button("Jigwi DEV", canvas.transform, "JIGWI DEV", .02f, .81f, .28f, .855f);
            if (testPanel == null)
            {
                var background = Image("Jigwi Test Panel", canvas.transform, new Color(.08f, .06f, .05f, .97f));
                Rect(background.rectTransform, new Vector2(.06f, .27f), new Vector2(.94f, .76f), Vector2.zero, Vector2.zero);
                testPanel = background.gameObject;
                var title = Label("Title", testPanel.transform, 21);
                title.text = "JIGWI / HOST MANUAL TEST";
                Rect(title.rectTransform, new Vector2(.04f, .83f), new Vector2(.96f, .98f), Vector2.zero, Vector2.zero);
                playerButtons = new UnityEngine.UI.Button[5];
                for (int i = 0; i < playerButtons.Length; i++)
                    playerButtons[i] = Button("Target P" + (i + 1), testPanel.transform, "P" + (i + 1),
                        .04f + i * .185f, .65f, .205f + i * .185f, .79f);
                fireButton = Button("Fire Challenge", testPanel.transform, "START FIRE CHALLENGE", .06f, .45f, .94f, .6f);
                generationButton = Button("Generation Burn", testPanel.transform, "START RAW GENERATION BURN", .06f, .25f, .94f, .4f);
                feedback = Label("Feedback", testPanel.transform, 14);
                Rect(feedback.rectTransform, new Vector2(.04f, .1f), new Vector2(.96f, .24f), Vector2.zero, Vector2.zero);
                closeButton = Button("Close", testPanel.transform, "CLOSE", .3f, .015f, .7f, .1f);
                testPanel.SetActive(false);
            }
        }

        private void Wire()
        {
            if (wired || testToggle == null || testPanel == null) return;
            wired = true;
            testToggle.onClick.AddListener(() => SetPanel(!testPanel.activeSelf));
            closeButton.onClick.AddListener(() => SetPanel(false));
            fireButton.onClick.AddListener(() => Trigger(true));
            generationButton.onClick.AddListener(() => Trigger(false));
            for (int i = 0; playerButtons != null && i < playerButtons.Length; i++)
            {
                int slot = i + 1;
                playerButtons[i].onClick.AddListener(() => { targetSlot = slot; choseTarget = true; });
            }
        }

        private void SetPanel(bool visible)
        {
            if (testPanel != null) testPanel.SetActive(visible);
            if (network != null && network.board != null) network.board.SetUiInputBlocked(visible);
        }

        private ulong TargetOwner => network == null ? ulong.MaxValue : network.IsDeveloperSolo
            ? network.LocalPlayerId : network.GetSlotOwner(targetSlot);

        private void Trigger(bool fire)
        {
            if (network == null || TargetOwner == ulong.MaxValue) return;
            bool accepted = fire ? network.DevStartJigwiFire(TargetOwner) : network.DevStartJigwiGenerationBurn(TargetOwner);
            if (accepted) SetPanel(false);
            else if (feedback != null) feedback.text = "Could not start: resume battle / wait for the current effect to end.";
        }

        private void LateUpdate()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            bool playing = snapshot != null && snapshot.phase == LabPhase.Playing;
            bool canTest = playing && snapshot.jigwiStage?.enabled != true && snapshot.jigwiAB?.enabled != true
                && network.DeveloperBuildAvailable && network.IsHost && network.JigwiManualControlsAllowed;
            if (testToggle != null) testToggle.gameObject.SetActive(canTest);
            if (!canTest && testPanel != null && testPanel.activeSelf) SetPanel(false);
            if (!choseTarget && playing) targetSlot = Math.Max(1, network.LocalSlotNumber);
            if (fireButton != null) fireButton.interactable = canTest && !snapshot.paused && TargetOwner != ulong.MaxValue;
            if (generationButton != null) generationButton.interactable = canTest && !snapshot.paused && TargetOwner != ulong.MaxValue;
            for (int i = 0; playerButtons != null && i < playerButtons.Length; i++)
            {
                bool available = playing && (network.IsDeveloperSolo ? i == 0
                    : network.GetSlotOwner(i + 1) != ulong.MaxValue && i < network.TargetParticipantCount);
                playerButtons[i].interactable = available;
                var text = playerButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                text.text = "P" + (i + 1) + (i + 1 == targetSlot ? " *" : string.Empty);
            }
            if (feedback != null && testPanel != null && testPanel.activeSelf && playing && snapshot.paused)
                feedback.text = "Resume the battle before manually triggering a pattern.";
            if (!playing)
            {
                if (stageNotice != null) stageNotice.gameObject.SetActive(false);
                if (heat != null) heat.gameObject.SetActive(false);
                if (effectText != null) effectText.gameObject.SetActive(false);
                lastServerTime = -1d;
                return;
            }
            if (snapshot.serverTime != lastServerTime)
            { lastServerTime = snapshot.serverTime; receivedAt = Time.unscaledTime; }
            double now = snapshot.serverTime + (snapshot.paused ? 0d : Time.unscaledTime - receivedAt);
            if (snapshot.jigwiAB?.enabled == true)
            {
                if (screenPresentation == null || screenPresentation.source != network) EnsureScreenPresentation();
                if (stageNotice != null) stageNotice.gameObject.SetActive(false);
                if (heat != null) heat.gameObject.SetActive(false);
                if (effectText != null) effectText.gameObject.SetActive(false);
                return;
            }
            if (snapshot.jigwiStage?.enabled == true)
            {
                ApplyStageSnapshot(snapshot, network.LocalPlayerId, now);
                return;
            }
            if (stageNotice != null) stageNotice.gameObject.SetActive(false);
            LabJigwiState state = snapshot.JigwiFor(network.LocalPlayerId);
            bool fireActive = state != null && state.FireActive(now);
            bool overheat = state != null && state.Overheated(now);
            bool generation = state != null && state.GenerationBurnActive(now);
            if (heat != null)
            {
                heat.gameObject.SetActive(fireActive || overheat);
                heat.color = new Color(1f, overheat ? .1f : .35f, .015f,
                    (overheat ? .15f : .045f) + .025f * Mathf.Sin(Time.time * 8f));
            }
            if (effectText != null)
            {
                effectText.gameObject.SetActive(fireActive || overheat || generation);
                string message = string.Empty;
                if (fireActive) message = "FIRE " + state.targets.Count(item => !item.extinguished) + "/" + state.targets.Length
                    + "  /  WATER COMBINED\n" + Math.Max(0d, state.fireEndsAt - now).ToString("0.0") + "s";
                else if (overheat) message = "OVERHEAT " + Math.Max(0d, state.overheatUntil - now).ToString("0.0")
                    + "s\nRELEASING ORBS BURNS THEM";
                if (generation) message += (message.Length == 0 ? string.Empty : "\n") + "RAW BURN "
                    + Math.Max(0d, state.generationBurnUntil - now).ToString("0.0") + "s";
                effectText.text = message;
            }
        }

        /// <summary>Passive stage guidance: never adds a full-screen raycast/input blocker.</summary>
        public void ApplyStageSnapshot(LabSnapshot snapshot, ulong localPlayer, double now)
        {
            bool active = snapshot != null && snapshot.phase == LabPhase.Playing && snapshot.jigwiStage?.enabled == true;
            if (!active)
            {
                if (stageNotice != null) stageNotice.gameObject.SetActive(false);
                return;
            }
            if (heat != null) heat.gameObject.SetActive(false);
            if (effectText != null) effectText.gameObject.SetActive(false);
            if (testToggle != null) testToggle.gameObject.SetActive(false);
            if (testPanel != null && testPanel.activeSelf) SetPanel(false);
            EnsureStageNotice();
            if (stageNotice == null) return;
            string message = DescribeStage(snapshot.jigwiStage, snapshot, localPlayer, now);
            if (snapshot.jigwiStage.fever) blockedUntilRealtime = -1f;
            if (Time.unscaledTime < blockedUntilRealtime) message = "불에 막힘\n" + message;
            stageNotice.text = message;
            stageNotice.gameObject.SetActive(message.Length > 0);
        }

        public void ShowStageBlocked()
        {
            blockedUntilRealtime = Time.unscaledTime + .65f;
            if (network?.Snapshot != null)
                ApplyStageSnapshot(network.Snapshot, network.LocalPlayerId, network.Snapshot.serverTime);
        }

        public static string DescribeStage(LabJigwiStageSnapshot stage, LabSnapshot snapshot, ulong localPlayer, double now)
        {
            if (stage == null || !stage.enabled || snapshot == null || snapshot.phase != LabPhase.Playing) return string.Empty;
            if (stage.fever) return "피버 · 스태미나 자동 회복 2배";
            LabJigwiStageGroup[] live = (stage.groups ?? Array.Empty<LabJigwiStageGroup>())
                .Where(group => group != null && (group.Warning(now) || group.Active(now)))
                .ToArray();
            LabJigwiStageGroup[] mine = live.Where(group => group.affectedPlayer == localPlayer).ToArray();
            LabJigwiStageGroup[] help = live.Where(group => group.helperPlayer == localPlayer).ToArray();
            string instruction;
            if (mine.Any(group => group.pattern == LabJigwiStagePattern.Curtain)
                && help.Any(group => group.pattern == LabJigwiStagePattern.Curtain))
            {
                var incoming = mine.First(group => group.pattern == LabJigwiStagePattern.Curtain);
                var outgoing = help.First(group => group.pattern == LabJigwiStagePattern.Curtain);
                instruction = PlayerLabel(snapshot, incoming.helperPlayer) + "가 내 불을 끌 수 있어요\n"
                    + "근원을 맞혀 " + PlayerLabel(snapshot, outgoing.affectedPlayer) + "를 도와주세요";
            }
            else if (help.Length > 0)
            {
                var group = help[0];
                instruction = group.pattern == LabJigwiStagePattern.Curtain
                    ? PlayerLabel(snapshot, group.affectedPlayer) + "의 불장막 · 근원을 맞혀 도와주세요"
                    : (help.Length > 1 ? "근원 " + help.Length + "개 · " : string.Empty) + "큰 불씨를 맞혀 불을 끄세요";
            }
            else if (mine.Length > 0)
                instruction = PlayerLabel(snapshot, mine[0].helperPlayer) + "가 도와줄 수 있어요 · 열린 곳은 칠할 수 있어요";
            else
            {
                LabJigwiStageGroup recent = (stage.groups ?? Array.Empty<LabJigwiStageGroup>())
                    .Where(group => group != null && group.resolution == LabJigwiStageResolution.Extinguished
                        && group.resolvedAt >= 0d && now >= group.resolvedAt && now - group.resolvedAt < .8d
                        && (group.affectedPlayer == localPlayer || group.helperPlayer == localPlayer))
                    .OrderByDescending(group => group.resolvedAt).FirstOrDefault();
                if (recent == null) return string.Empty;
                if (recent.affectedPlayer == recent.helperPlayer) return "불을 껐어요 · 다시 칠하세요";
                return recent.affectedPlayer == localPlayer
                    ? PlayerLabel(snapshot, recent.helperPlayer) + "가 불을 꺼줬어요"
                    : PlayerLabel(snapshot, recent.affectedPlayer) + "의 불장막을 껐어요";
            }
            bool warning = live.Any(group => (group.affectedPlayer == localPlayer || group.helperPlayer == localPlayer) && group.Warning(now));
            return warning ? "선덕여왕 사랑해요!!!\n" + instruction : instruction;
        }

        private static string PlayerLabel(LabSnapshot snapshot, ulong owner)
        {
            LabPlayerState player = snapshot.players?.FirstOrDefault(value => value != null && value.id == owner);
            return player != null ? "P" + (player.seat + 1) : "동료";
        }

        private void EnsureStageNotice()
        {
            if (stageNotice != null || canvas == null) return;
            stageNotice = Label("Jigwi Stage Notice", canvas.transform, 18);
            Rect(stageNotice.rectTransform, new Vector2(.1f, .68f), new Vector2(.9f, .79f), Vector2.zero, Vector2.zero);
            foreach (var text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (text != stageNotice && text.font != null && text.font.HasCharacter('선'))
                { stageNotice.font = text.font; break; }
            stageNotice.color = new Color(1f, .96f, .78f);
            stageNotice.raycastTarget = false;
            var outline = stageNotice.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.08f, .04f, .02f, .95f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        private void OnDisable()
        {
            if (stageNotice != null) stageNotice.gameObject.SetActive(false);
            if (screenPresentation != null) screenPresentation.Hide();
            if (network != null && network.board != null) network.board.SetUiInputBlocked(false);
        }
        private void OnDestroy() { if (network != null && network.board != null) network.board.SetUiInputBlocked(false); }

        private static void Rect(RectTransform target, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { target.anchorMin = min; target.anchorMax = max; target.offsetMin = offsetMin; target.offsetMax = offsetMax; }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var value = go.GetComponent<UnityEngine.UI.Image>(); value.color = color; return value;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false);
            var value = go.GetComponent<UnityEngine.UI.Text>();
            value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); value.fontSize = size;
            value.alignment = TextAnchor.MiddleCenter; value.color = Color.white; value.raycastTarget = false;
            value.resizeTextForBestFit = true; value.resizeTextMinSize = 10; value.resizeTextMaxSize = size;
            return value;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, float x0, float y0, float x1, float y1)
        {
            var image = Image(name, parent, new Color(.37f, .17f, .045f, .98f));
            Rect(image.rectTransform, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Label("Label", image.transform, 15); label.text = text;
            Rect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 2f), new Vector2(-3f, -2f));
            return button;
        }
    }
}
