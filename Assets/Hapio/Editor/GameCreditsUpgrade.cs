using System;
using C6Lab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>
    /// 공통 로비 Prefab의 타이틀 화면에 "크레딧" 버튼과 크레딧 창을 저장한다 (#602).
    /// 이미 있으면 같은 이름의 오브젝트를 다시 쓰고, 다른 화면/연결은 건드리지 않는다.
    /// 위치/크기/글자 크기는 저장 뒤 Inspector에서 바로 고칠 수 있다.
    /// </summary>
    public static class GameCreditsUpgrade
    {
        public const string CreditsTextPath = "Assets/Hapio/Art/UI/Credits/Credits.txt";
        private static readonly Color Ink = LabLobbyView.Html("312B23"), Muted = LabLobbyView.Html("6E6F61"),
            Brown = LabLobbyView.Html("51473B"), Ivory = LabLobbyView.Html("FAF6F1"), Card = LabLobbyView.Html("EDDBC7");

        [MenuItem("Hapio/Upgrade Home Credits")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing the saved menu.");
            GameObject root = PrefabUtility.LoadPrefabContents(GameLobbySceneUpgrade.PrefabPath);
            try
            {
                var view = root.GetComponent<GameLobbyView>();
                Font bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                Font body = view.mediumFont != null ? view.mediumFont : bold;
                var credits = root.GetComponent<GameCreditsView>();
                if (credits == null) credits = root.AddComponent<GameCreditsView>();
                credits.titlePage = view.titlePage;
                credits.source = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsTextPath);

                // 타이틀 오른쪽 위 모서리의 알약 버튼 (게임들이 설정/정보 버튼을 두는 자리). 로고(164~277) 위, 상태 표시줄 아래.
                RectTransform open = Child(view.titlePage.transform, "Credits Button");
                // 예전 저장본은 투명 Image였다. Graphic은 한 오브젝트에 하나만 둘 수 있다.
                var oldImage = open.GetComponent<Image>(); if (oldImage != null) UnityEngine.Object.DestroyImmediate(oldImage, true);
                Ensure<LabRoundedGraphic>(open); Ensure<Button>(open); Ensure<Shadow>(open);
                LabLobbyView.Place(open, 296, 62, 90, 38);
                var openGraphic = open.GetComponent<LabRoundedGraphic>(); openGraphic.radius = 19;
                openGraphic.color = new Color(Brown.r, Brown.g, Brown.b, .92f);
                var openShadow = open.GetComponent<Shadow>();
                openShadow.effectColor = new Color(0, 0, 0, .25f); openShadow.effectDistance = new Vector2(0, -2);
                credits.openButton = open.GetComponent<Button>(); credits.openButton.targetGraphic = openGraphic;
                open.SetAsLastSibling();
                Label(open, "Label", "크레딧", bold, 18, Ivory, new Rect(0, 0, 90, 38));

                // 창: 디자인 전체를 덮는 어두운 뒤판 + 한지색 카드.
                RectTransform panel = Child(view.design, "Credits Panel");
                LabLobbyView.Place(panel, 0, 0, 402, 874);
                credits.panel = panel.gameObject;
                RectTransform dim = Child(panel, "Dismiss", typeof(Image), typeof(Button));
                LabLobbyView.Place(dim, 0, 0, 402, 874);
                var dimImage = dim.GetComponent<Image>();
                dimImage.color = new Color(0, 0, 0,
                    QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f - Mathf.GammaToLinearSpace(.5f) : .5f);
                credits.dismissButton = dim.GetComponent<Button>(); credits.dismissButton.targetGraphic = dimImage;
                credits.dismissButton.transition = Selectable.Transition.None;

                RectTransform card = Child(panel, "Card", typeof(LabRoundedGraphic));
                LabLobbyView.Place(card, 20, 70, 362, 734);
                var cardGraphic = card.GetComponent<LabRoundedGraphic>(); cardGraphic.radius = 30; cardGraphic.color = Card;
                Label(card, "Title", "크레딧", bold, 24, Ink, new Rect(20, 26, 322, 36));

                RectTransform viewport = Child(card, "Scroll", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                LabLobbyView.Place(viewport, 20, 78, 322, 566);
                viewport.GetComponent<Image>().color = Color.clear;
                RectTransform content = Child(viewport, "Body", typeof(Text), typeof(ContentSizeFitter));
                content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
                content.offsetMin = new Vector2(4, 0); content.offsetMax = new Vector2(-4, 0); content.anchoredPosition = Vector2.zero;
                var text = content.GetComponent<Text>();
                text.font = body; text.fontSize = 14; text.color = Ink; text.lineSpacing = 1.15f;
                text.alignment = TextAnchor.UpperLeft; text.supportRichText = true; text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
                var fitter = content.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.content = content; scroll.viewport = viewport;
                scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 20;
                credits.body = text; credits.scroll = scroll;
                credits.Fill();

                RectTransform close = Child(card, "Close Button", typeof(LabRoundedGraphic), typeof(Button));
                LabLobbyView.Place(close, 20, 662, 322, 48);
                var closeGraphic = close.GetComponent<LabRoundedGraphic>(); closeGraphic.radius = 24; closeGraphic.color = Brown;
                credits.closeButton = close.GetComponent<Button>(); credits.closeButton.targetGraphic = closeGraphic;
                Label(close, "Label", "닫기", bold, 20, Ivory, new Rect(0, 0, 322, 48));

                panel.gameObject.SetActive(false);
                EditorUtility.SetDirty(credits);
                PrefabUtility.SaveAsPrefabAsset(root, GameLobbySceneUpgrade.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_CREDITS_READY: Home title credits button and panel saved.");
        }

        private static RectTransform Child(Transform parent, string name, params Type[] components)
        {
            Transform target = parent.Find(name);
            if (target == null)
            {
                var created = new GameObject(name, typeof(RectTransform));
                created.layer = parent.gameObject.layer;
                target = created.transform; target.SetParent(parent, false);
            }
            foreach (Type type in components)
                if (target.GetComponent(type) == null) target.gameObject.AddComponent(type);
            return (RectTransform)target;
        }

        private static T Ensure<T>(Component owner) where T : Component
        {
            T found = owner.GetComponent<T>();
            return found != null ? found : owner.gameObject.AddComponent<T>();
        }

        private static void Label(Transform parent, string name, string value, Font font, int size, Color color, Rect rect)
        {
            RectTransform target = Child(parent, name, typeof(Text));
            LabLobbyView.Place(target, rect.x, rect.y, rect.width, rect.height);
            var text = target.GetComponent<Text>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
