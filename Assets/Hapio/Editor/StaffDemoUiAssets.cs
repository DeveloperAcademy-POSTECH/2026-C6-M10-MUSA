using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Explicitly authors the staff-only selector without editing game scenes or normal lobby art.</summary>
    public static class StaffDemoUiAssets
    {
        public const string PrefabPath = "Assets/Hapio/Resources/Hapio/Demo/Staff/StageSelection.prefab";

        [MenuItem("Hapio/Demo/Update Staff Demo UI")]
        public static void CreateOrUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring the saved staff demo selector.");
            EnsureFolder("Assets/Hapio/Resources/Hapio/Demo/Staff");
            Font font = AssetDatabase.LoadAssetAtPath<Font>(GameSceneBuilder.SharedFontPath)
                ?? throw new InvalidOperationException("The saved Hapio UI font is missing.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("StaffStageSelection", typeof(RectTransform));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                var background = Rect(root.transform, "Background", Vector2.zero, Vector2.one)
                    .gameObject.AddComponent<UnityEngine.UI.Image>();
                // TextColor/SecondaryLightIvory/100; presentation colors never change the battle palette.
                background.color = GameLobbySeatColors.LightText;
                background.raycastTarget = true;
                RectTransform safe = Rect(root.transform, "SafeArea", Vector2.zero, Vector2.one);
                safe.gameObject.AddComponent<GameSafeArea>();
                Label(safe, "Title", font, new Vector2(.08f, .90f), new Vector2(.92f, .98f), "스테이지 선택", 28);
                Label(safe, "StaffCaption", font, new Vector2(.08f, .85f), new Vector2(.92f, .90f), "스태프용 · 3인 단판 시연", 16);

                var groups = new UnityEngine.UI.Button[2];
                for (int index = 0; index < groups.Length; index++)
                {
                    float top = .77f - index * .25f;
                    groups[index] = BoxButton(safe, "Group" + (index == 0 ? "A" : "B"), font,
                        new Vector2(.10f, top - .20f), new Vector2(.90f, top),
                        (index == 0 ? "A그룹" : "B그룹") + "\n방 확인 중", 25);
                }

                var buttons = new UnityEngine.UI.Button[3];
                string[] yokai = { "구미호", "야광귀", "지귀" };
                for (int index = 0; index < buttons.Length; index++)
                {
                    float top = .82f - index * .19f;
                    RectTransform box = Rect(safe, "Stage" + (index + 1), new Vector2(.10f, top - .16f), new Vector2(.90f, top));
                    var image = box.gameObject.AddComponent<UnityEngine.UI.Image>();
                    // TextColor/SecondaryLightIvory/200.
                    image.color = GameBattleUiColors.Light;
                    var outline = box.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = GameBattleUiColors.Brown; // TextColor/PrimaryBrown/500***.
                    outline.effectDistance = new Vector2(1f, -1f);
                    buttons[index] = box.gameObject.AddComponent<UnityEngine.UI.Button>();
                    buttons[index].targetGraphic = image;
                    Label(box, "StageNumber", font, new Vector2(.08f, .46f), new Vector2(.92f, .86f),
                        (index + 1) + "스테이지", 25);
                    Label(box, "YokaiName", font, new Vector2(.08f, .12f), new Vector2(.92f, .46f), yokai[index], 20);
                }
                var status = Label(safe, "Status", font, new Vector2(.08f, .12f), new Vector2(.92f, .25f),
                    "플레이할 스테이지를 선택하시오.\n같은 스테이지의 방으로 입장하오.", 16);
                RectTransform cancel = Rect(safe, "CancelEntry", new Vector2(.18f, .04f), new Vector2(.82f, .11f));
                var cancelImage = cancel.gameObject.AddComponent<UnityEngine.UI.Image>();
                cancelImage.color = GameBattleUiColors.Light;
                var back = cancel.gameObject.AddComponent<UnityEngine.UI.Button>();
                back.targetGraphic = cancelImage;
                Label(cancel, "Label", font, Vector2.zero, Vector2.one, "취소", 20);
                cancel.gameObject.SetActive(false);
                var groupChange = BoxButton(safe, "ChangeGroup", font, new Vector2(.18f, .04f),
                    new Vector2(.82f, .11f), "그룹 변경", 20);
                groupChange.gameObject.SetActive(false);
                RectTransform confirmation = Rect(root.transform, "RoomCreationConfirmation", Vector2.zero, Vector2.one);
                var blocker = confirmation.gameObject.AddComponent<UnityEngine.UI.Image>();
                // TextColor/DarkIvory/500***70%: use the token's actual alpha, not its name.
                blocker.color = new Color(0.8352941274642944f, 0.7450980544090271f, 0.6470588445663452f, .5f);
                blocker.raycastTarget = true;
                RectTransform confirmationSafe = Rect(confirmation, "SafeArea", Vector2.zero, Vector2.one);
                confirmationSafe.gameObject.AddComponent<GameSafeArea>();
                RectTransform card = Rect(confirmationSafe, "Card", new Vector2(.08f, .34f), new Vector2(.92f, .66f));
                var cardImage = card.gameObject.AddComponent<UnityEngine.UI.Image>();
                cardImage.color = GameLobbySeatColors.LightText;
                cardImage.raycastTarget = true;
                var cardOutline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
                cardOutline.effectColor = GameBattleUiColors.Brown;
                cardOutline.effectDistance = new Vector2(1f, -1f);
                var confirmationMessage = Label(card, "Message", font, new Vector2(.08f, .43f), new Vector2(.92f, .90f),
                    "A그룹 방이 없습니다.\n새로 만들까요?", 23);
                var cancelCreation = BoxButton(card, "CancelCreation", font, new Vector2(.08f, .10f),
                    new Vector2(.46f, .33f), "취소", 21);
                var confirmCreation = BoxButton(card, "ConfirmCreation", font, new Vector2(.54f, .10f),
                    new Vector2(.92f, .33f), "확인", 21);
                confirmation.gameObject.SetActive(false);
                var view = root.AddComponent<GameStaffDemoView>();
                view.stageButtons = buttons;
                view.groupButtons = groups;
                view.groupChangeButton = groupChange;
                view.status = status;
                view.backButton = back;
                view.creationConfirmation = confirmation.gameObject;
                view.creationConfirmationMessage = confirmationMessage;
                view.confirmCreationButton = confirmCreation;
                view.cancelCreationButton = cancelCreation;
                // This is a saved UI panel, not another root Overlay Canvas. Preview scenes have
                // no physical display, so a Canvas would bake a driven zero-scale transform here.
                // Runtime installs the panel under the existing Home root canvas/raycaster and
                // stretches it only after initialization, before GameLobbyView reveals the panel.
                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = rootRect.anchorMax = new Vector2(.5f, .5f);
                rootRect.pivot = new Vector2(.5f, .5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(390f, 844f);
                rootRect.localScale = Vector3.one;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("The staff stage selector could not be saved.");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            Validate();
        }

        /// <summary>Read-only. Export must never regenerate an artist's saved UI implicitly.</summary>
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var view = prefab != null ? prefab.GetComponent<GameStaffDemoView>() : null;
            var rect = prefab != null ? prefab.GetComponent<RectTransform>() : null;
            if (view == null || rect == null
                || view.stageButtons == null || view.stageButtons.Length != 3
                || view.groupButtons == null || view.groupButtons.Length != 2 || view.groupChangeButton == null
                || view.status == null || view.backButton == null || view.creationConfirmation == null
                || view.creationConfirmationMessage == null || view.confirmCreationButton == null || view.cancelCreationButton == null)
                throw new InvalidOperationException("Prepare the saved staff selector with Hapio/Demo/Update Staff Demo UI before exporting.");
            foreach (var button in System.Linq.Enumerable.Concat(view.stageButtons, view.groupButtons))
                if (button == null || button.targetGraphic == null || !button.targetGraphic.raycastTarget
                    || !button.transform.IsChildOf(prefab.transform))
                    throw new InvalidOperationException("Every stage box must have its own saved, clickable button binding.");
            if (!view.groupChangeButton.transform.IsChildOf(prefab.transform)
                || view.groupChangeButton.targetGraphic == null || !view.groupChangeButton.targetGraphic.raycastTarget)
                throw new InvalidOperationException("The group change action needs its own saved button binding.");
            var blocker = view.creationConfirmation.GetComponent<UnityEngine.UI.Image>();
            var blockerRect = view.creationConfirmation.GetComponent<RectTransform>();
            if (!view.creationConfirmation.transform.IsChildOf(prefab.transform) || view.creationConfirmation.activeSelf
                || blocker == null || !blocker.raycastTarget || blockerRect == null
                || blockerRect.anchorMin != Vector2.zero || blockerRect.anchorMax != Vector2.one
                || blockerRect.offsetMin != Vector2.zero || blockerRect.offsetMax != Vector2.zero
                || view.confirmCreationButton == view.cancelCreationButton
                || !view.creationConfirmationMessage.transform.IsChildOf(view.creationConfirmation.transform))
                throw new InvalidOperationException("The saved room confirmation must start hidden and block the entire selector.");
            foreach (var button in new[] { view.confirmCreationButton, view.cancelCreationButton })
                if (!button.transform.IsChildOf(view.creationConfirmation.transform)
                    || button.targetGraphic == null || !button.targetGraphic.raycastTarget)
                    throw new InvalidOperationException("Room creation needs separate saved confirm and cancel buttons.");
            if (prefab.GetComponentInChildren<Canvas>(true) != null
                || prefab.GetComponentInChildren<UnityEngine.UI.CanvasScaler>(true) != null
                || prefab.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(true) != null)
                throw new InvalidOperationException("The staff selector must reuse the saved Home canvas, scaler and raycaster.");
            if (rect.localScale != Vector3.one || rect.rect.width <= 0f || rect.rect.height <= 0f)
                throw new InvalidOperationException("The saved staff selector is collapsed. Regenerate the staff UI before exporting.");
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static UnityEngine.UI.Button BoxButton(Transform parent, string name, Font font,
            Vector2 min, Vector2 max, string label, int size)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = GameBattleUiColors.Light; // TextColor/SecondaryLightIvory/200.
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = GameBattleUiColors.Brown; // TextColor/PrimaryBrown/500***.
            outline.effectDistance = new Vector2(1f, -1f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Label(rect, "Label", font, new Vector2(.05f, .08f), new Vector2(.95f, .92f), label, size);
            return button;
        }

        private static UnityEngine.UI.Text Label(Transform parent, string name, Font font,
            Vector2 min, Vector2 max, string value, int size)
        {
            var label = Rect(parent, name, min, max).gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = GameBattleUiColors.Ink; // TextColor/PrimaryBrown/700.
            label.text = value;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
