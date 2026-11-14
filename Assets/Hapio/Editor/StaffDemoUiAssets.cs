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
                var view = root.AddComponent<GameStaffDemoView>();
                view.stageButtons = buttons;
                view.status = status;
                view.backButton = back;
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
                || view.stageButtons == null || view.stageButtons.Length != 3 || view.status == null || view.backButton == null)
                throw new InvalidOperationException("Prepare the saved staff selector with Hapio/Demo/Update Staff Demo UI before exporting.");
            foreach (var button in view.stageButtons)
                if (button == null || button.targetGraphic == null || !button.targetGraphic.raycastTarget
                    || !button.transform.IsChildOf(prefab.transform))
                    throw new InvalidOperationException("Every stage box must have its own saved, clickable button binding.");
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
