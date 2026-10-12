using System;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UI = UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Add the room browser and ordinary player-count controls to the saved Canvas.</summary>
    public static class LabRoomBrowserSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Upgrade Room Browser UI")]
        public static void UpgradeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null || hud.sessionControls == null || hud.peersText == null
                || hud.joinButton == null || hud.findHostButton == null)
                throw new InvalidOperationException("The saved Lab Canvas has no complete lobby controls.");
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("LabHud must be on LabCanvas.");

            Transform controls = hud.sessionControls.transform;
            hud.decreasePlayersButton = EnsureButton(controls, "RoomSizeMinusButton", hud.joinButton,
                "-", new Vector2(.02f, .01f), new Vector2(.16f, .19f));
            hud.increasePlayersButton = EnsureButton(controls, "RoomSizePlusButton", hud.joinButton,
                "+", new Vector2(.84f, .01f), new Vector2(.98f, .19f));
            RectTransform peersRect = hud.peersText.rectTransform;
            Stretch(peersRect, new Vector2(.18f, .01f), new Vector2(.82f, .19f));
            hud.peersText.fontSize = 12;

            hud.roomNameText = EnsureText(controls, "RoomNameText", hud.peersText,
                "REGISTERING ROOM…", new Vector2(.02f, .58f), new Vector2(.98f, .94f));
            hud.roomNameText.alignment = TextAnchor.MiddleCenter;
            hud.roomNameText.fontSize = 14;
            hud.roomNameText.color = new Color(.82f, .94f, .89f, 1f);
            hud.roomNameText.gameObject.SetActive(false);
            UI.Text findLabel = hud.findHostButton.GetComponentInChildren<UI.Text>();
            if (findLabel == null) throw new InvalidOperationException("FIND HOST has no text.");
            findLabel.text = "FIND ROOMS";
            findLabel.fontSize = 10;

            Transform existingPanel = canvas.transform.Find("RoomListPanel");
            RectTransform panel = existingPanel == null
                ? NewRect("RoomListPanel", canvas.transform)
                : RequireRect(existingPanel);
            Stretch(panel, new Vector2(.04f, .15f), new Vector2(.96f, .83f));
            UI.Image panelImage = EnsureImage(panel.gameObject);
            panelImage.color = new Color(.035f, .065f, .09f, .97f);
            panelImage.raycastTarget = true;

            UI.Text title = EnsureText(panel, "RoomListTitle", hud.peersText,
                "AVAILABLE ROOMS", new Vector2(0f, 1f), Vector2.one);
            Edges(title.rectTransform, new Vector2(12f, -45f), new Vector2(-12f, -8f));
            title.fontSize = 19;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            hud.roomSearchStatusText = EnsureText(panel, "RoomSearchStatusText", hud.peersText,
                "Searching for rooms…", new Vector2(0f, 1f), Vector2.one);
            Edges(hud.roomSearchStatusText.rectTransform, new Vector2(12f, -82f),
                new Vector2(-12f, -49f));
            hud.roomSearchStatusText.fontSize = 13;
            hud.roomSearchStatusText.alignment = TextAnchor.MiddleCenter;

            Transform existingScroll = panel.Find("RoomListScrollView");
            RectTransform scrollRoot = existingScroll == null
                ? NewRect("RoomListScrollView", panel) : RequireRect(existingScroll);
            Stretch(scrollRoot, Vector2.zero, Vector2.one);
            Edges(scrollRoot, new Vector2(10f, 62f), new Vector2(-10f, -90f));
            UI.Image scrollImage = EnsureImage(scrollRoot.gameObject);
            scrollImage.color = new Color(.065f, .11f, .14f, 1f);
            scrollImage.raycastTarget = true;

            Transform existingViewport = scrollRoot.Find("Viewport");
            RectTransform viewport = existingViewport == null
                ? NewRect("Viewport", scrollRoot) : RequireRect(existingViewport);
            Stretch(viewport, Vector2.zero, Vector2.one);
            UI.Image viewportImage = EnsureImage(viewport.gameObject);
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = false;
            UI.Mask mask = viewport.GetComponent<UI.Mask>();
            if (mask == null) mask = viewport.gameObject.AddComponent<UI.Mask>();
            mask.showMaskGraphic = false;

            Transform existingContent = viewport.Find("RoomListContent");
            RectTransform content = existingContent == null
                ? NewRect("RoomListContent", viewport) : RequireRect(existingContent);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            UI.VerticalLayoutGroup layout = content.GetComponent<UI.VerticalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            UI.ContentSizeFitter fitter = content.GetComponent<UI.ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<UI.ContentSizeFitter>();
            fitter.horizontalFit = UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UI.ContentSizeFitter.FitMode.PreferredSize;
            hud.roomListContent = content;

            UI.ScrollRect scroll = scrollRoot.GetComponent<UI.ScrollRect>();
            if (scroll == null) scroll = scrollRoot.gameObject.AddComponent<UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            hud.roomListRowTemplate = EnsureButton(content, "RoomRowTemplate", hud.joinButton,
                "ROOM", Vector2.zero, Vector2.one);
            UI.LayoutElement rowLayout = hud.roomListRowTemplate.GetComponent<UI.LayoutElement>();
            if (rowLayout == null)
                rowLayout = hud.roomListRowTemplate.gameObject.AddComponent<UI.LayoutElement>();
            rowLayout.preferredHeight = 48f;
            UI.Text rowLabel = hud.roomListRowTemplate.GetComponentInChildren<UI.Text>();
            if (rowLabel == null) throw new InvalidOperationException("Room row needs a label.");
            rowLabel.fontSize = 14;
            rowLabel.alignment = TextAnchor.MiddleCenter;
            hud.roomListRowTemplate.gameObject.SetActive(false);

            hud.refreshRoomsButton = EnsureButton(panel, "RefreshRoomsButton", hud.joinButton,
                "REFRESH", new Vector2(.04f, 0f), new Vector2(.48f, 0f));
            Edges(hud.refreshRoomsButton.GetComponent<RectTransform>(),
                new Vector2(0f, 12f), new Vector2(0f, 53f));
            hud.cancelRoomSearchButton = EnsureButton(panel, "CancelRoomSearchButton", hud.joinButton,
                "CANCEL", new Vector2(.52f, 0f), new Vector2(.96f, 0f));
            Edges(hud.cancelRoomSearchButton.GetComponent<RectTransform>(),
                new Vector2(0f, 12f), new Vector2(0f, 53f));

            hud.roomListPanel = panel.gameObject;
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(false);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("C6_LAB_ROOM_BROWSER_SCENE_UPGRADED: lobby size, room name, and selectable room list saved.");
        }

        private static UI.Button EnsureButton(Transform parent, string name, UI.Button source,
            string label, Vector2 min, Vector2 max)
        {
            Transform existing = parent.Find(name);
            GameObject gameObject = existing == null
                ? UnityEngine.Object.Instantiate(source.gameObject, parent)
                : existing.gameObject;
            gameObject.name = name;
            UI.Button button = gameObject.GetComponent<UI.Button>();
            UI.Text text = gameObject.GetComponentInChildren<UI.Text>(true);
            if (button == null || text == null || button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException(name + " needs a button, label, and no persistent action.");
            Stretch(RequireRect(gameObject.transform), min, max);
            text.text = label;
            text.fontSize = 14;
            return button;
        }

        private static UI.Text EnsureText(Transform parent, string name, UI.Text source,
            string value, Vector2 min, Vector2 max)
        {
            Transform existing = parent.Find(name);
            GameObject gameObject = existing == null
                ? UnityEngine.Object.Instantiate(source.gameObject, parent)
                : existing.gameObject;
            gameObject.name = name;
            UI.Text text = gameObject.GetComponent<UI.Text>();
            if (text == null) throw new InvalidOperationException(name + " needs a Text component.");
            Stretch(RequireRect(gameObject.transform), min, max);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static RectTransform RequireRect(Transform transform)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) throw new InvalidOperationException(transform.name + " needs RectTransform.");
            return rect;
        }

        private static UI.Image EnsureImage(GameObject gameObject)
        {
            UI.Image image = gameObject.GetComponent<UI.Image>();
            return image == null ? gameObject.AddComponent<UI.Image>() : image;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Edges(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
