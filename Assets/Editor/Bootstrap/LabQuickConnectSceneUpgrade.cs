using System;
using System.Text;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UI = UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Rearrange the existing lobby controls in the saved Canvas, without rebuilding battle UI.</summary>
    public static class LabQuickConnectSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Upgrade Quick Connect UI")]
        public static void UpgradeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before upgrading the saved lobby.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
                throw new InvalidOperationException("Save or review the current Scene changes before upgrading the lobby.");
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            RequireBindings(hud);
            string protectedBefore = CaptureProtectedState(hud);
            Transform panel = hud.roomListPanel.transform;
            Transform controls = hud.sessionControls.transform;
            RectTransform scroll = panel.Find("RoomListScrollView") as RectTransform;
            if (scroll == null) throw new InvalidOperationException("Existing room-list scroll view is missing.");

            // Keep the same Host button, player-count controls, row template, and scroll hierarchy.
            Stretch(RequireRect(panel), new Vector2(.04f, .05f), new Vector2(.96f, .92f));
            UI.Text title = panel.Find("RoomListTitle")?.GetComponent<UI.Text>();
            if (title == null) throw new InvalidOperationException("Existing room-list title is missing.");
            Top(title.rectTransform, .04f, .96f, 8f, 44f);
            StyleText(title, "LOCAL ROOMS", 20, TextAnchor.MiddleCenter);
            Top(hud.roomSearchStatusText.rectTransform, .04f, .96f, 48f, 88f);
            StyleText(hud.roomSearchStatusText, "Searching for rooms…", 14, TextAnchor.MiddleCenter);

            Move(hud.hostButton.transform, panel);
            Top(RequireRect(hud.hostButton.transform), .04f, .96f, 94f, 146f);
            StyleButton(hud.hostButton, "CREATE ROOM", 18);
            Move(hud.decreasePlayersButton.transform, panel);
            Move(hud.increasePlayersButton.transform, panel);
            Top(RequireRect(hud.decreasePlayersButton.transform), .04f, .18f, 154f, 196f);
            Top(RequireRect(hud.increasePlayersButton.transform), .82f, .96f, 154f, 196f);
            StyleButton(hud.decreasePlayersButton, "−", 22);
            StyleButton(hud.increasePlayersButton, "+", 22);
            hud.quickPlayerCountText = EnsureText(panel, "QuickRoomPlayerCountText", hud.peersText);
            Top(hud.quickPlayerCountText.rectTransform, .20f, .80f, 154f, 196f);
            StyleText(hud.quickPlayerCountText, "ROOM PLAYERS 3 / 5", 14, TextAnchor.MiddleCenter);

            Stretch(scroll, Vector2.zero, Vector2.one);
            scroll.offsetMin = new Vector2(10f, 110f);
            scroll.offsetMax = new Vector2(-10f, -205f);
            hud.roomListScrollView = scroll;
            UI.LayoutElement rowLayout = hud.roomListRowTemplate.GetComponent<UI.LayoutElement>();
            if (rowLayout == null)
                rowLayout = hud.roomListRowTemplate.gameObject.AddComponent<UI.LayoutElement>();
            rowLayout.preferredHeight = 54f;
            StyleButton(hud.roomListRowTemplate, "ROOM", 15);
            hud.roomListRowTemplate.gameObject.SetActive(false);

            hud.directConnectionButton = EnsureButton(panel, "DirectConnectionButton", hud.joinButton);
            Bottom(RequireRect(hud.directConnectionButton.transform), .04f, .96f, 60f, 100f);
            StyleButton(hud.directConnectionButton, "DIRECT IP", 14);
            Transform direct = panel.Find("DirectConnectionPanel");
            if (direct == null)
            {
                GameObject created = new GameObject("DirectConnectionPanel", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(UI.Image));
                created.transform.SetParent(panel, false);
                direct = created.transform;
                UI.Image image = created.GetComponent<UI.Image>();
                image.color = new Color(.065f, .11f, .14f, 1f);
                image.raycastTarget = true;
            }
            Bottom(RequireRect(direct), .04f, .96f, 110f, 220f);
            hud.directConnectionPanel = direct.gameObject;
            Move(hud.hostAddress.transform, direct);
            Move(hud.port.transform, direct);
            Move(hud.joinButton.transform, direct);
            Stretch(RequireRect(hud.hostAddress.transform), new Vector2(.03f, .54f), new Vector2(.69f, .94f));
            Stretch(RequireRect(hud.port.transform), new Vector2(.72f, .54f), new Vector2(.97f, .94f));
            Stretch(RequireRect(hud.joinButton.transform), new Vector2(.03f, .06f), new Vector2(.97f, .46f));
            hud.hostAddress.textComponent.fontSize = 15;
            hud.port.textComponent.fontSize = 15;
            StyleButton(hud.joinButton, "JOIN BY IP", 15);
            direct.gameObject.SetActive(false);

            Bottom(RequireRect(hud.refreshRoomsButton.transform), .04f, .96f, 10f, 50f);
            StyleButton(hud.refreshRoomsButton, "REFRESH ROOMS", 14);
            Bottom(RequireRect(hud.cancelRoomSearchButton.transform), .04f, .96f, 10f, 50f);
            StyleButton(hud.cancelRoomSearchButton, "CANCEL CONNECTION", 14);
            hud.cancelRoomSearchButton.gameObject.SetActive(false);
            // Retain the old FIND ROOMS object/reference for compatibility, but automatic browsing
            // and REFRESH now provide its normal entry point.
            hud.findHostButton.gameObject.SetActive(false);

            // Connected-room controls stay separate from the browser. No battle RectTransforms move.
            Stretch(RequireRect(controls), new Vector2(.04f, .65f), new Vector2(.96f, .90f));
            Stretch(hud.roomNameText.rectTransform, new Vector2(.04f, .70f), new Vector2(.96f, .96f));
            StyleText(hud.roomNameText, "REGISTERING ROOM…", 16, TextAnchor.MiddleCenter);
            Stretch(hud.peersText.rectTransform, new Vector2(.04f, .50f), new Vector2(.96f, .68f));
            hud.peersText.fontSize = 15;
            Stretch(RequireRect(hud.startButton.transform), new Vector2(.04f, .25f), new Vector2(.48f, .48f));
            Stretch(RequireRect(hud.leaveButton.transform), new Vector2(.52f, .25f), new Vector2(.96f, .48f));
            StyleButton(hud.startButton, "START", 16);
            StyleButton(hud.leaveButton, "LEAVE ROOM", 15);
            Move(hud.statusText.transform, controls);
            Stretch(hud.statusText.rectTransform, new Vector2(.04f, .02f), new Vector2(.96f, .23f));
            hud.statusText.fontSize = 13;
            hud.statusText.alignment = TextAnchor.MiddleCenter;
            hud.sessionControls.SetActive(false);
            hud.roomListPanel.SetActive(true);

            // DEV must be reachable while discovery continues. Keep both its toggle and modal
            // after the browser in draw order, without moving their saved layout.
            if (hud.developerMode != null && hud.developerMode.toggleButton != null)
            {
                int toggleIndex = hud.developerMode.toggleButton.transform.GetSiblingIndex();
                int beforeToggle = panel.GetSiblingIndex() < toggleIndex ? toggleIndex - 1 : toggleIndex;
                panel.SetSiblingIndex(beforeToggle);
            }
            if (protectedBefore != CaptureProtectedState(hud))
                throw new InvalidOperationException("Battle UI or source config changed unexpectedly; review before saving.");
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Quick-connect lobby could not be saved.");
            Debug.Log("C6_LAB_QUICK_CONNECT_UI_UPGRADED: original lobby controls reused; battle UI/config preserved.");
        }

        private static void RequireBindings(LabHud hud)
        {
            if (hud == null || hud.GetComponent<Canvas>() == null || hud.sessionControls == null
                || hud.roomListPanel == null || hud.hostButton == null || hud.joinButton == null
                || hud.findHostButton == null || hud.decreasePlayersButton == null
                || hud.increasePlayersButton == null || hud.roomNameText == null || hud.peersText == null
                || hud.hostAddress == null || hud.port == null || hud.startButton == null
                || hud.leaveButton == null || hud.statusText == null || hud.roomSearchStatusText == null
                || hud.refreshRoomsButton == null || hud.cancelRoomSearchButton == null
                || hud.roomListContent == null || hud.roomListRowTemplate == null)
                throw new InvalidOperationException("Upgrade the existing Room Browser UI before Quick Connect.");
            foreach (UI.Button button in new[] { hud.hostButton, hud.joinButton, hud.findHostButton,
                hud.decreasePlayersButton, hud.increasePlayersButton, hud.startButton, hud.leaveButton,
                hud.refreshRoomsButton, hud.cancelRoomSearchButton, hud.roomListRowTemplate })
                if (button.onClick.GetPersistentEventCount() != 0)
                    throw new InvalidOperationException(button.name + " has persistent actions that need review.");
        }

        private static string CaptureProtectedState(LabHud hud)
        {
            var state = new StringBuilder();
            if (hud.battleUi != null)
                foreach (Component component in hud.battleUi.GetComponentsInChildren<Component>(true))
                    if (component != null) state.Append(EditorJsonUtility.ToJson(component));
            if (hud.network != null && hud.network.config != null)
                state.Append(EditorJsonUtility.ToJson(hud.network.config));
            return state.ToString();
        }

        private static UI.Button EnsureButton(Transform parent, string name, UI.Button source)
        {
            Transform existing = parent.Find(name);
            GameObject gameObject = existing == null
                ? UnityEngine.Object.Instantiate(source.gameObject, parent) : existing.gameObject;
            gameObject.name = name;
            UI.Button button = gameObject.GetComponent<UI.Button>();
            if (button == null || button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException(name + " needs a Button without persistent actions.");
            return button;
        }

        private static UI.Text EnsureText(Transform parent, string name, UI.Text source)
        {
            Transform existing = parent.Find(name);
            GameObject gameObject = existing == null
                ? UnityEngine.Object.Instantiate(source.gameObject, parent) : existing.gameObject;
            gameObject.name = name;
            UI.Text text = gameObject.GetComponent<UI.Text>();
            if (text == null) throw new InvalidOperationException(name + " needs a Text component.");
            text.raycastTarget = false;
            return text;
        }

        private static void StyleButton(UI.Button button, string label, int fontSize)
        {
            UI.Text text = button.GetComponentInChildren<UI.Text>(true);
            if (text == null) throw new InvalidOperationException(button.name + " needs a saved label.");
            StyleText(text, label, fontSize, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
        }

        private static void StyleText(UI.Text text, string label, int fontSize, TextAnchor alignment)
        {
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(.94f, .96f, .98f, 1f);
            text.raycastTarget = false;
        }

        private static void Move(Transform child, Transform parent)
        {
            if (child.parent != parent) child.SetParent(parent, false);
        }

        private static RectTransform RequireRect(Transform transform)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) throw new InvalidOperationException(transform.name + " needs RectTransform.");
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Top(RectTransform rect, float minX, float maxX, float inset, float bottomInset)
        {
            Stretch(rect, new Vector2(minX, 1f), new Vector2(maxX, 1f));
            rect.offsetMin = new Vector2(0f, -bottomInset);
            rect.offsetMax = new Vector2(0f, -inset);
        }

        private static void Bottom(RectTransform rect, float minX, float maxX, float bottom, float top)
        {
            Stretch(rect, new Vector2(minX, 0f), new Vector2(maxX, 0f));
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, top);
        }
    }
}
