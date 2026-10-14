using System;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Adds editable lobby P-slot controls without rebuilding the saved Lab Canvas.</summary>
    public static class LabPlayerSlotSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Upgrade Player Slot UI")]
        public static void UpgradeScene()
        {
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
                throw new InvalidOperationException("Save the open Scene before upgrading the Lab lobby UI.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null || hud.sessionControls == null || hud.joinButton == null)
                throw new InvalidOperationException("The saved Lab Canvas has no complete lobby controls.");
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException("LabHud must be on the saved LabCanvas.");

            Transform existingPanel = canvas.transform.Find("PlayerSlotPanel");
            RectTransform panel;
            if (existingPanel == null)
            {
                panel = NewRect("PlayerSlotPanel", canvas.transform);
                Stretch(panel, new Vector2(.04f, .35f), new Vector2(.96f, .56f));
                UnityEngine.UI.Image background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(.045f, .075f, .095f, .94f);
                background.raycastTarget = false;
                panel.SetSiblingIndex(hud.sessionControls.transform.GetSiblingIndex() + 1);
                panel.gameObject.SetActive(false);
            }
            else
            {
                panel = existingPanel as RectTransform;
                if (panel == null) throw new InvalidOperationException("PlayerSlotPanel needs a RectTransform.");
            }

            UnityEngine.UI.Text source = hud.joinButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (source == null || source.font == null)
                throw new InvalidOperationException("The saved JOIN button has no usable font.");

            EnsureText(panel, "PlayerSlotTitle", source, "CHOOSE PLAYER POSITION",
                new Vector2(.03f, .76f), new Vector2(.97f, .97f), 15);
            RectTransform row = EnsureRect(panel, "PlayerSlotRow",
                new Vector2(.03f, .34f), new Vector2(.97f, .73f));
            UnityEngine.UI.HorizontalLayoutGroup layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                layout.padding = new RectOffset(0, 0, 0, 0);
                layout.spacing = 5f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            var buttons = new UnityEngine.UI.Button[LabModel.MaximumParticipants];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = EnsureSlotButton(row, source, hud.joinButton, i + 1);

            hud.playerSlotPanel = panel.gameObject;
            hud.playerSlotButtons = buttons;
            hud.playerSlotStatusText = EnsureText(panel, "PlayerSlotStatusText", source,
                "CHOOSE AN OPEN P NUMBER BEFORE START",
                new Vector2(.03f, .04f), new Vector2(.97f, .29f), 12);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the updated Lab Scene.");
            Debug.Log("C6_LAB_PLAYER_SLOT_UI_SAVED: editable P1-P5 lobby controls are bound.");
        }

        private static UnityEngine.UI.Button EnsureSlotButton(RectTransform row,
            UnityEngine.UI.Text sourceText, UnityEngine.UI.Button sourceButton, int slotNumber)
        {
            string name = "PlayerSlotP" + slotNumber + "Button";
            Transform existing = row.Find(name);
            RectTransform rect;
            if (existing == null)
            {
                rect = NewRect(name, row);
                UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Image sourceImage = sourceButton.GetComponent<UnityEngine.UI.Image>();
                image.color = sourceImage == null ? new Color(.15f, .35f, .43f, .94f) : sourceImage.color;
                image.raycastTarget = true;
                UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                button.colors = sourceButton.colors;
                EnsureText(rect, "Label", sourceText, "P" + slotNumber + "\nOPEN",
                    Vector2.zero, Vector2.one, 10);
                return button;
            }

            rect = existing as RectTransform;
            if (rect == null) throw new InvalidOperationException(name + " needs a RectTransform.");
            UnityEngine.UI.Button savedButton = rect.GetComponent<UnityEngine.UI.Button>();
            if (savedButton == null || savedButton.GetComponentInChildren<UnityEngine.UI.Text>(true) == null)
                throw new InvalidOperationException(name + " is missing its Button or label.");
            return savedButton;
        }

        private static UnityEngine.UI.Text EnsureText(Transform parent, string name,
            UnityEngine.UI.Text source, string content, Vector2 anchorMin, Vector2 anchorMax,
            int fontSize)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                UnityEngine.UI.Text saved = existing.GetComponent<UnityEngine.UI.Text>();
                if (saved == null) throw new InvalidOperationException(name + " needs a Text component.");
                return saved;
            }

            RectTransform rect = NewRect(name, parent);
            Stretch(rect, anchorMin, anchorMax);
            UnityEngine.UI.Text text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = source.font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.94f, .96f, .98f, 1f);
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static RectTransform EnsureRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform saved = existing as RectTransform;
                if (saved == null) throw new InvalidOperationException(name + " needs a RectTransform.");
                return saved;
            }
            RectTransform rect = NewRect(name, parent);
            Stretch(rect, anchorMin, anchorMax);
            return rect;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
