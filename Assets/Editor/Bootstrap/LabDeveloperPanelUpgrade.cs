using System;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>One-time, idempotent upgrade of the saved DEV Canvas; never rebuilds existing rows.</summary>
    public static class LabDeveloperPanelUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Upgrade Developer Controls")]
        public static void UpgradeScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabDeveloperMode developer = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            if (developer == null || developer.panel == null || developer.valueInputs == null
                || developer.valueInputs.Length != LabConfig.DeveloperFields.Count)
                throw new InvalidOperationException("The saved Lab Scene has no complete DEV panel.");

            Transform content = developer.valueInputs[0].transform.parent.parent;
            if (content == null || content.GetComponent<VerticalLayoutGroup>() == null
                || content.GetComponent<ContentSizeFitter>() == null)
                throw new InvalidOperationException("DEV setting rows require the saved scroll content layout.");

            Slider[] sliders = new Slider[developer.valueInputs.Length];
            for (int i = 0; i < developer.valueInputs.Length; i++)
            {
                InputField input = developer.valueInputs[i];
                Transform row = input.transform.parent;
                if (row == null || row.name != "Setting" + i.ToString("D2"))
                    throw new InvalidOperationException("Unexpected DEV row for setting " + i);
                Text label = row.Find("SettingLabel")?.GetComponent<Text>();
                if (label == null) throw new InvalidOperationException("Setting label missing at " + i);
                LayoutElement labelLayout = label.GetComponent<LayoutElement>();
                LayoutElement inputLayout = input.GetComponent<LayoutElement>();
                if (labelLayout == null || inputLayout == null)
                    throw new InvalidOperationException("Setting layout missing at " + i);
                labelLayout.preferredWidth = 105;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                inputLayout.preferredWidth = 70;
                input.contentType = LabConfig.DeveloperFields[i].WholeNumber
                    ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
                Slider slider = row.Find("SettingSlider")?.GetComponent<Slider>();
                if (slider == null) slider = CreateSlider(row, "SettingSlider", 88);
                StyleSliderHandle(slider);
                sliders[i] = slider;
            }
            developer.valueSliders = sliders;

            Transform targetRow = content.Find("TargetParticipantsRow");
            if (targetRow == null)
            {
                GameObject row = new GameObject("TargetParticipantsRow", typeof(RectTransform),
                    typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                targetRow = row.transform;
                targetRow.SetParent(content, false);
                targetRow.SetSiblingIndex(0);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.up;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, 1f);
                HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
                group.padding = new RectOffset(3, 3, 2, 2);
                group.spacing = 6;
                group.childAlignment = TextAnchor.MiddleLeft;
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = false;
                group.childForceExpandHeight = false;
                row.GetComponent<LayoutElement>().preferredHeight = 42;

                GameObject labelObject = UnityEngine.Object.Instantiate(
                    developer.valueInputs[0].transform.parent.Find("SettingLabel").gameObject,
                    targetRow);
                labelObject.name = "TargetParticipantsLabel";
                Text label = labelObject.GetComponent<Text>();
                label.text = "ROOM PLAYERS 3 / 5";
                label.GetComponent<LayoutElement>().preferredWidth = 130;
                Slider targetSlider = CreateSlider(targetRow, "TargetParticipantsSlider", 130);
                targetSlider.minValue = 2f;
                targetSlider.maxValue = 5f;
                targetSlider.wholeNumbers = true;
                targetSlider.value = 3f;
            }
            developer.targetParticipantsLabel = targetRow.Find("TargetParticipantsLabel")?.GetComponent<Text>();
            developer.targetParticipantsSlider = targetRow.Find("TargetParticipantsSlider")?.GetComponent<Slider>();
            if (developer.targetParticipantsLabel == null || developer.targetParticipantsSlider == null)
                throw new InvalidOperationException("ROOM PLAYERS control was not connected.");
            StyleSliderHandle(developer.targetParticipantsSlider);

            Transform panel = developer.panel.transform;
            Transform soloButton = developer.applyStartButton.transform;
            Transform hostButton = panel.Find("ApplyHostRoomButton");
            if (hostButton == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(soloButton.gameObject, panel);
                clone.name = "ApplyHostRoomButton";
                hostButton = clone.transform;
            }
            RectTransform soloRect = soloButton.GetComponent<RectTransform>();
            RectTransform hostRect = hostButton.GetComponent<RectTransform>();
            soloRect.anchorMin = new Vector2(.04f, .045f);
            soloRect.anchorMax = new Vector2(.49f, .12f);
            soloRect.anchoredPosition = Vector2.zero;
            soloRect.sizeDelta = Vector2.zero;
            hostRect.anchorMin = new Vector2(.51f, .045f);
            hostRect.anchorMax = new Vector2(.96f, .12f);
            hostRect.anchoredPosition = Vector2.zero;
            hostRect.sizeDelta = Vector2.zero;
            Button host = hostButton.GetComponent<Button>();
            Text hostText = hostButton.GetComponentInChildren<Text>();
            if (host == null || hostText == null)
                throw new InvalidOperationException("The Host button clone has no Button/Text.");
            if (host.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("The Host button clone has unexpected persistent click actions.");
            hostText.text = "CREATE ROOM";
            developer.applyHostButton = host;
            developer.applyHostLabel = hostText;

            LabHud hud = developer.GetComponent<LabHud>();
            if (hud == null || hud.sessionControls == null || hud.hostButton == null
                || hud.joinButton == null || hud.startButton == null || hud.leaveButton == null)
                throw new InvalidOperationException("The saved lobby controls are incomplete.");
            Transform controls = hud.sessionControls.transform;
            Transform find = controls.Find("FindHostButton");
            if (find == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(hud.joinButton.gameObject, controls);
                clone.name = "FindHostButton";
                find = clone.transform;
            }
            Button[] lobbyButtons =
            {
                hud.hostButton, hud.joinButton, find.GetComponent<Button>(), hud.startButton, hud.leaveButton
            };
            if (lobbyButtons[2] == null)
                throw new InvalidOperationException("FIND HOST has no Button component.");
            if (lobbyButtons[2].onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("FIND HOST has unexpected persistent click actions.");
            for (int i = 0; i < lobbyButtons.Length; i++)
            {
                RectTransform buttonRect = lobbyButtons[i].GetComponent<RectTransform>();
                float left = .026f + i * .192f;
                buttonRect.anchorMin = new Vector2(left, .25f);
                buttonRect.anchorMax = new Vector2(left + .178f, .52f);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.sizeDelta = Vector2.zero;
                Text text = lobbyButtons[i].GetComponentInChildren<Text>();
                if (text != null) text.fontSize = 12;
            }
            Text findText = find.GetComponentInChildren<Text>();
            if (findText == null) throw new InvalidOperationException("FIND HOST has no label.");
            findText.text = "FIND HOST";
            findText.fontSize = 11;
            hud.findHostButton = lobbyButtons[2];

            Transform hint = panel.Find("DeveloperHint");
            if (hint != null && hint.TryGetComponent(out Text hintText))
                hintText.text = "Adjust a SOLO round or create a shared Host room. Host may re-apply before START.";

            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1
                || developer.GetComponent<GraphicRaycaster>() == null)
                throw new InvalidOperationException("The interactive DEV Canvas needs one EventSystem and a GraphicRaycaster.");

            LabTransportUpgrade.UpgradeOpenScene();
            EditorUtility.SetDirty(developer);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("C6_LAB_DEV_SCENE_UPGRADED: 39 sliders, player target, shared-room and FIND HOST buttons saved.");
        }

        private static Slider CreateSlider(Transform parent, string name, float preferredWidth)
        {
            GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            LayoutElement layout = sliderObject.GetComponent<LayoutElement>();
            layout.minWidth = 75f;
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = 1f;
            layout.preferredHeight = 34f;
            Image hitArea = sliderObject.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, .01f);
            hitArea.raycastTarget = true;

            RectTransform track = CreateRect("Track", rect);
            track.anchorMin = new Vector2(0f, .5f);
            track.anchorMax = new Vector2(1f, .5f);
            track.offsetMin = new Vector2(7f, -4f);
            track.offsetMax = new Vector2(-7f, 4f);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(.16f, .19f, .23f, 1f);
            trackImage.raycastTarget = false;

            RectTransform fillArea = CreateRect("FillArea", rect);
            fillArea.anchorMin = new Vector2(0f, .5f);
            fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.offsetMin = new Vector2(7f, -4f);
            fillArea.offsetMax = new Vector2(-7f, 4f);
            RectTransform fill = CreateRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(.28f, .82f, .72f, 1f);
            fillImage.raycastTarget = false;

            RectTransform handleArea = CreateRect("HandleSlideArea", rect);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(7f, 0f);
            handleArea.offsetMax = new Vector2(-7f, 0f);
            RectTransform handle = CreateRect("Handle", handleArea);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.clear;
            handleImage.raycastTarget = true;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            return slider;
        }

        private static void StyleSliderHandle(Slider slider)
        {
            RectTransform handle = slider.handleRect;
            if (handle == null) throw new InvalidOperationException("The DEV slider has no handle.");
            // Slider drives its handle to the full row height. A centered child
            // stays short and avoids making 39 stacked thumbs look like one line.
            if (handle.TryGetComponent(out Image hitArea))
            {
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
                EditorUtility.SetDirty(hitArea);
            }
            Transform thumb = handle.Find("Thumb");
            if (thumb == null)
            {
                GameObject child = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(handle, false);
                thumb = child.transform;
            }
            RectTransform thumbRect = thumb.GetComponent<RectTransform>();
            thumbRect.anchorMin = new Vector2(.5f, .5f);
            thumbRect.anchorMax = new Vector2(.5f, .5f);
            thumbRect.anchoredPosition = Vector2.zero;
            thumbRect.sizeDelta = new Vector2(10f, 16f);
            Image thumbImage = thumb.GetComponent<Image>();
            thumbImage.color = new Color(.48f, .86f, .77f, 1f);
            thumbImage.raycastTarget = false;
            slider.targetGraphic = thumbImage;
            EditorUtility.SetDirty(handle);
            EditorUtility.SetDirty(thumbRect);
            EditorUtility.SetDirty(thumbImage);
            EditorUtility.SetDirty(slider);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }
    }
}
