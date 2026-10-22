using System;
using System.IO;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace C6Lab.Editor
{
    /// <summary>Install only the pre-battle presentation, preserving existing battle and DEV bindings.</summary>
    public static class LabLobbyDesignUpgrade
    {
        private const string LabScene = "Assets/Lab/Scenes/Lab.unity";
        private const string Art = "Assets/Lab/Art/Lobby/";
        private static Font bold, medium;
        private static Color Ink => LabLobbyView.Html("292921");
        [MenuItem("C6 Lab/Apply Figma Lobby Design")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty) throw new InvalidOperationException("Save or review Scene changes first.");
            if (scene.path != "Assets/Lab/Scenes/Lab.unity") scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null || hud.network == null) throw new InvalidOperationException("Saved HUD/network required.");
            LoadFonts();
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { Art }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(path)) as TextureImporter;
                if (importer == null) continue;
                LabLobbyTextureQuality.Configure(importer);
                importer.SaveAndReimport();
            }
            // Preserve legacy bindings and their state for DEV and regression fixtures, but
            // remove their presentation/input while the product lobby owns the screen.
            foreach (GameObject legacy in new[] { hud.sessionControls, hud.roomListPanel, hud.playerSlotPanel,
                hud.statusText == null ? null : hud.statusText.gameObject,
                hud.noticeText == null ? null : hud.noticeText.gameObject,
                hud.peersText == null ? null : hud.peersText.gameObject })
            {
                if (legacy == null) continue;
                var group = legacy.GetComponent<CanvasGroup>();
                if (group == null) group = legacy.AddComponent<CanvasGroup>();
                group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            }
            var canvas = hud.GetComponentInParent<Canvas>();
            var old = canvas.transform.Find("FigmaLobby");
            if (old != null && old.TryGetComponent(out LabLobbyView authored))
            {
                UpgradeCompatibility(authored, hud);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return;
            }
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = Rect(canvas.transform, "FigmaLobby"); Stretch(root);
            // Draw after legacy panels, before DEV's toggle and modal.
            if (hud.developerMode != null && hud.developerMode.toggleButton != null)
                root.SetSiblingIndex(hud.developerMode.toggleButton.transform.GetSiblingIndex());
            var view = root.gameObject.AddComponent<LabLobbyView>(); view.hud = hud; view.mediumFont = medium;
            var bg = Rect(root, "Painting"); Stretch(bg);
            view.background = bg.gameObject.AddComponent<RawImage>();
            view.background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Source/3a857.png");
            view.background.uvRect = LabLobbyView.BackgroundUv(true, new Vector2(402,874));
            view.background.raycastTarget = true;
            view.design = Rect(root, "SafeAreaDesign"); view.design.anchorMin = view.design.anchorMax = new Vector2(.5f,.5f);
            view.design.sizeDelta = new Vector2(402,874);
            var title = Page(view.design, "StartPage"); view.titlePage = title.gameObject;
            TextAt(title, "Title", "합이오", -4, 179, 410, 141, 110, true, TextAnchor.MiddleCenter);
            TextAt(title, "SealPrompt", "요괴를 봉인하시오", 54, 669, 293, 30, 17, true, TextAnchor.MiddleCenter);
            view.enterButton = ButtonAt(title, "EnterLobby", "게임 시작", 79.5f, 712, 243, 58, 20);
            var browser = Page(view.design, "RoomBrowser"); view.browserPage = browser.gameObject;
            TextAt(browser, "Heading", "혼자선 수습 불가오.\n함께할 동지를 모으시오.", 40, 137, 322, 76, 32, true, TextAnchor.MiddleCenter);
            browser.Find("Heading").GetComponent<Text>().color = Color.black;
            var wifi = TextAt(browser, "Wifi", "같은 와이-파이에 연결해야 입장할 수 있소.", 28, 229, 346, 25, 16, false, TextAnchor.MiddleCenter);
            wifi.color = LabLobbyView.Html("394759");
            TextAt(browser, "People", "함께할 인원", 28, 341, 140, 44, 20);
            view.countButtons = new Button[4];
            for (int i=0;i<4;i++) view.countButtons[i] = ButtonAt(browser,"Count"+(i+2),(i+2)+"명",174+i*50,341,46,44,18,false);
            view.createButton = ButtonAt(browser,"CreateRoom","새 방 만들기 +",28,405,346,44,20);
            TextAt(browser,"OpenRooms","열린 방",28,510,170,28,20);
            view.refreshButton = PlainButton(browser,"Refresh","↻ 다시 찾기",260,503,114,44,14);
            view.cancelButton = PlainButton(browser,"Cancel","입장 취소",260,503,114,44,14);
            var empty=Rect(browser,"EmptyRooms"); LabLobbyView.Place(empty,28.5f,548,345,250); view.emptyRooms=empty.gameObject;
            SpriteAt(empty,"Paper","81e78",0,0,345,250);
            TextAt(empty,"EmptyTitle","아직 모인 동지가 없소...",26,89,293,42,25,false,TextAnchor.MiddleCenter);
            var hint=TextAt(empty,"Hint","방을 직접 만들어 보시오.",26,133,293,24,14,false,TextAnchor.MiddleCenter); hint.color=LabLobbyView.Html("575750");
            var scroll=Rect(browser,"RoomScroll"); LabLobbyView.Place(scroll,28,548,346,286);
            var scrollRect=scroll.gameObject.AddComponent<ScrollRect>(); scroll.gameObject.AddComponent<RectMask2D>();
            var content=Rect(scroll,"Content"); content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=14; layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content=content;scrollRect.viewport=scroll;scrollRect.horizontal=false;scrollRect.movementType=ScrollRect.MovementType.Clamped;
            view.roomContent=content;
            var row=Rect(content,"RoomTemplate"); row.sizeDelta=new Vector2(346,86);
            var paper=row.gameObject.AddComponent<LabRoundedGraphic>();paper.radius=24;paper.color=LabLobbyView.Html("E8DFCB");
            var rowButton=row.gameObject.AddComponent<Button>();rowButton.targetGraphic=paper;
            var rowLayout=row.gameObject.AddComponent<LayoutElement>();rowLayout.preferredHeight=86;
            TextAt(row,"Name","수장고",28,15,210,32,25);
            var roomCount=TextAt(row,"Count","",28,49,220,23,14);roomCount.color=LabLobbyView.Html("575750");
            TextAt(row,"Action","입장 →",250,26,80,32,20,false,TextAnchor.MiddleCenter);
            row.gameObject.SetActive(false);view.roomTemplate=rowButton;
            var position=Page(view.design,"PositionPage");view.positionPage=position.gameObject;
            view.leaveButton=PlainButton(position,"LeaveRoom","< 방 나가기",27,71,346,44,20);
            view.heading=TextAt(position,"Heading","빈 자리를 고르시오.",28,151,346,50,36,true,TextAnchor.MiddleCenter);
            view.heading.color=Color.black;
            view.subheading=TextAt(position,"Hint","실제로 앉은 위치의 빈 자리를 눌러주시오.",28,204,346,28,16,false,TextAnchor.MiddleCenter);
            view.subheading.color=LabLobbyView.Html("394759");
            SpriteAt(position,"RoomPaper","20c44",28,267,346,418);
            view.roomName=TextAt(position,"RoomName","수장고",45,282,313,32,25);
            view.joinedCount=TextAt(position,"Joined","1 / 3명 입장",45,310,160,25,15);
            view.selectedCount=TextAt(position,"Selected","1 / 3명 자리 확정",200,310,158,25,15,false,TextAnchor.MiddleRight);
            view.joinedCount.color=view.selectedCount.color=LabLobbyView.Html("575750");
            view.connectionLines=SpriteAt(position,"SeatConnections","4ba8d",96.7f,417,213.643f,171.635f);
            view.lines=new[]{Sprite("4ba8d"),Sprite("cbb55"),Sprite("ec7bb")};
            view.emptySlots=new[]{Sprite("544fe"),Sprite("56dc7"),Sprite("c4379"),Sprite("f037d"),Sprite("814ee")};
            view.filledSlots=new[]{Sprite("544fe"),Sprite("6ece5"),Sprite("2859c"),Sprite("c2f7a"),Sprite("2b4c9")};
            view.slotButtons=new Button[5];view.slotImages=new Image[5];view.slotLabels=new Text[5];view.localLabels=new Text[5];
            for(int i=0;i<5;i++)
            {
                var slot=Rect(position,"SlotP"+(i+1));LabLobbyView.Place(slot,0,0,68.8311f,67.9734f);
                var hit=slot.gameObject.AddComponent<Image>();hit.color=Color.clear;
                view.slotButtons[i]=slot.gameObject.AddComponent<Button>();view.slotButtons[i].targetGraphic=hit;
                view.slotImages[i]=SpriteAt(slot,"Artwork","544fe",0,0,68.8311f,67.9734f);
                // Dashed rings are 68x68; keep their source proportions inside the maximum artwork slot.
                view.slotImages[i].preserveAspect=true;
                view.slotLabels[i]=TextAt(slot,"Number","P"+(i+1),0,0,68.8311f,67.9734f,24,false,TextAnchor.MiddleCenter);
                view.localLabels[i]=TextAt(position,"LocalP"+(i+1),"",0,0,120,25,17,false,TextAnchor.MiddleCenter);
                view.localLabels[i].color=LabLobbyView.Html("655B4B");
                var center=LabLobbyView.SlotCenter(3,Mathf.Min(i+1,3));LabLobbyView.Place(slot,center.x-34.41555f,center.y-33.9867f,68.8311f,67.9734f);
            }
            view.sealButton=ButtonAt(position,"Seal","요괴 봉인 대기",28,726,346,58,25);
            view.sealButton.targetGraphic.color=LabLobbyView.Html("C7C0B0");
            view.message=TextAt(view.design,"ConnectionMessage","",28,836,346,35,13,false,TextAnchor.MiddleCenter);
            view.message.color=LabLobbyView.Html("AF4738");
            view.browserPage.SetActive(false);view.positionPage.SetActive(false);
            // Keep the development shortcut reachable without covering a selectable seat.
            if (hud.developerMode != null && hud.developerMode.toggleButton != null)
            {
                var toggle = (RectTransform)hud.developerMode.toggleButton.transform;
                toggle.anchorMin = new Vector2(.82f, .95f);
                toggle.anchorMax = new Vector2(.97f, .99f);
                toggle.offsetMin = toggle.offsetMax = Vector2.zero;
            }
            ApplyBrowserStyle(view);
            ApplyPositionStyle(view);
            UpgradeCompatibility(view, hud);
            EditorUtility.SetDirty(hud.network);EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("C6_LOBBY_DESIGN_SAVED");
        }

        /// <summary>Copy only the authored Figma root, never the old Scene's battle, model or settings.</summary>
        public static void IntegrateAuthoredRoot(string sourceScenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!File.Exists(sourceScenePath)) throw new FileNotFoundException("The preserved PR Scene is required.", sourceScenePath);
            Scene destination = SceneManager.GetSceneByPath(LabScene);
            if (!destination.IsValid() || !destination.isLoaded)
                destination = EditorSceneManager.OpenScene(LabScene, OpenSceneMode.Additive);
            if (destination.isDirty) throw new InvalidOperationException("Review or save current Lab edits before importing the authored lobby.");
            LabHud hud = destination.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabHud>(true)).Single();
            string temporary = "Assets/Editor/Bootstrap/_LobbyImport_" + Guid.NewGuid().ToString("N") + ".unity";
            Scene source = default;
            try
            {
                File.Copy(sourceScenePath, temporary);
                AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport);
                source = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
                LabLobbyView original = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LabLobbyView>(true)).Single();
                foreach (LabLobbyView existing in destination.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LabLobbyView>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(existing.gameObject);
                GameObject clone = UnityEngine.Object.Instantiate(original.gameObject, hud.GetComponentInParent<Canvas>().transform, false);
                clone.name = "FigmaLobby";
                LabLobbyView view = clone.GetComponent<LabLobbyView>();
                UpgradeCompatibility(view, hud);
                EditorSceneManager.MarkSceneDirty(destination);
                EditorSceneManager.SaveScene(destination, LabScene);
                Debug.Log("C6_LOBBY_AUTHORED_ROOT_INTEGRATED: latest Lab battle/config preserved; authored lobby only.");
            }
            finally
            {
                if (source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                AssetDatabase.DeleteAsset(temporary);
                if (destination.IsValid() && destination.isLoaded) SceneManager.SetActiveScene(destination);
            }
        }

        /// <summary>Extend required controls while retaining the authored layout, sprites and row styling.</summary>
        public static void UpgradeCompatibility(LabLobbyView view, LabHud hud)
        {
            LoadFonts(); view.hud = hud; view.mediumFont = medium;
            // The source fixed-Host design had no empty P1 art. Reuse an existing dashed
            // empty seat rather than falsely presenting an unclaimed P1 as filled.
            if (view.emptySlots.Length > 1 && view.emptySlots[0] == view.filledSlots[0])
                view.emptySlots[0] = view.emptySlots[1];
            foreach (Text text in view.GetComponentsInChildren<Text>(true))
                if (text.font != null && AssetDatabase.GetAssetPath(text.font) == "Assets/Lab/Art/Fonts/Shilla_Culture(B).otf") text.font = bold;
            foreach (GameObject legacy in new[] { hud.sessionControls, hud.roomListPanel, hud.playerSlotPanel,
                hud.statusText == null ? null : hud.statusText.gameObject,
                hud.noticeText == null ? null : hud.noticeText.gameObject,
                hud.peersText == null ? null : hud.peersText.gameObject })
            {
                if (legacy == null) continue;
                var group = legacy.GetComponent<CanvasGroup>();
                if (group == null) group = legacy.AddComponent<CanvasGroup>();
                group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            }
            Transform browser = view.browserPage.transform;
            view.countButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                Transform existing = browser.Find("Count" + (i + 2));
                view.countButtons[i] = existing != null ? existing.GetComponent<Button>()
                    : ButtonAt(browser, "Count" + (i + 2), (i + 2) + "명", 174 + i * 50, 341, 46, 44, 18, false);
                LabLobbyView.Place((RectTransform)view.countButtons[i].transform, 174 + i * 50, 341, 46, 44);
                Text label = view.countButtons[i].GetComponentInChildren<Text>();
                label.text = (i + 2) + "명"; label.fontSize = 18;
                LabLobbyView.Place(label.rectTransform, 0, 0, 46, 44);
            }
            if (view.clearSlotButton == null)
                view.clearSlotButton = PlainButton(view.positionPage.transform, "ClearSelection", "자리 선택 해제", 111, 683, 180, 34, 16);
            if (view.directConnectionButton == null)
                view.directConnectionButton = PlainButton(browser, "DirectConnection", "방장 IP로 직접 연결", 28, 458, 346, 36, 15);
            if (view.directConnectionPanel == null)
            {
                RectTransform modal = Page(view.design, "DirectConnectionPanel");
                modal.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .2f);
                RectTransform panel = Rect(modal, "Card");
                LabLobbyView.Place(panel, 20, 270, 362, 330);
                var paper = panel.gameObject.AddComponent<LabRoundedGraphic>(); paper.radius = 24; paper.color = LabLobbyView.Html("E8DFCB");
                view.directConnectionPanel = modal.gameObject;
                TextAt(panel, "Title", "방장 IP로 직접 연결", 20, 18, 322, 35, 22, true, TextAnchor.MiddleCenter);
                TextAt(panel, "AddressLabel", "방장 IP (IPv4 / IPv6)", 20, 66, 322, 25, 15);
                view.directAddress = CopyInput(hud.hostAddress, panel, "HostAddress", 20, 95, 322, 42);
                TextAt(panel, "PortLabel", "포트", 20, 143, 100, 25, 15);
                view.directPort = CopyInput(hud.port, panel, "Port", 126, 141, 216, 36);
                view.directFeedback = TextAt(panel, "Feedback", "", 20, 184, 322, 65, 14);
                view.directJoinButton = ButtonAt(panel, "Join", "연결", 20, 266, 210, 44, 18);
                view.closeDirectConnectionButton = PlainButton(panel, "Close", "닫기", 240, 266, 102, 44, 18);
                modal.gameObject.SetActive(false);
            }
            // Keep pre-connect DEV/Solo reachable above the authored painting, including in Development players.
            if (hud.developerMode?.toggleButton != null)
            {
                Transform toggle = hud.developerMode.toggleButton.transform;
                if (toggle.parent == view.transform.parent) view.transform.SetSiblingIndex(toggle.GetSiblingIndex());
                var rect = (RectTransform)toggle;
                rect.anchorMin = new Vector2(.82f, .95f); rect.anchorMax = new Vector2(.97f, .99f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            EditorUtility.SetDirty(view);
        }

        private static void LoadFonts()
        {
            bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            medium = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
            if (bold == null || medium == null) throw new InvalidOperationException("Both supplied Shilla fonts are required.");
        }

        private static InputField CopyInput(InputField source, Transform parent, string name, float x, float y, float width, float height)
        {
            if (source == null) throw new InvalidOperationException("The current Lab direct-connect input is missing.");
            InputField input = UnityEngine.Object.Instantiate(source, parent, false);
            input.name = name; input.gameObject.SetActive(true);
            LabLobbyView.Place((RectTransform)input.transform, x, y, width, height);
            foreach (Text text in input.GetComponentsInChildren<Text>(true)) { text.font = medium; text.fontSize = 17; }
            return input;
        }
        public static void ApplyBrowserStyle(LabLobbyView view)
        {
            view.refreshButton.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            view.cancelButton.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            var heading = view.browserPage.transform.Find("Heading").GetComponent<Text>();
            LabLobbyView.Place(heading.rectTransform, 63, 137, 276, 76);
            heading.fontSize = 30;
            var row = view.roomTemplate;
            row.targetGraphic.color = LabLobbyView.Html("E9DFCA");
            LabLobbyView.Place(row.transform.Find("Name").GetComponent<RectTransform>(), 28, 18, 220, 29);
            LabLobbyView.Place(row.transform.Find("Count").GetComponent<RectTransform>(), 28, 53, 220, 18);
            LabLobbyView.Place(row.transform.Find("Action").GetComponent<RectTransform>(), 264, 28, 65, 30);
        }

        public static void ApplyPositionStyle(LabLobbyView view)
        {
            view.leaveButton.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            foreach (var slot in view.slotButtons) slot.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            // Figma 2658:12570: visible text starts at (27,81); retain a 44pt tap target.
            LabLobbyView.Place((RectTransform)view.leaveButton.transform, 27, 71, 346, 44);
            var label = view.leaveButton.GetComponentInChildren<Text>();
            LabLobbyView.Place(label.rectTransform, 0, 10, 346, 24);
            label.alignment = TextAnchor.MiddleLeft;
        }

        [MenuItem("C6 Lab/Open Offline Physics Tuning")]
        public static void OpenOfflinePhysicsTuning()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var ui = UnityEngine.Object.FindFirstObjectByType<LabDeveloperMode>();
            if (ui == null || ui.network.IsConnected) return;
            ui.allowOfflineEditorTuning = true;
            ui.Open();
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png") ?? throw new InvalidOperationException("Missing lobby asset: "+name);
        private static RectTransform Rect(Transform parent,string name)
        {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        private static RectTransform Page(Transform parent,string name){var r=Rect(parent,name);Stretch(r);return r;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.sizeDelta=Vector2.zero;r.anchoredPosition=Vector2.zero;}
        private static Text TextAt(Transform parent,string name,string text,float x,float y,float w,float h,int size,bool useBold=false,TextAnchor anchor=TextAnchor.MiddleLeft)
        {var r=Rect(parent,name);LabLobbyView.Place(r,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=useBold?bold:medium;t.fontSize=size;t.text=text;t.color=Ink;t.alignment=anchor;t.raycastTarget=false;t.supportRichText=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
        private static Image SpriteAt(Transform parent,string name,string asset,float x,float y,float w,float h)
        {var r=Rect(parent,name);LabLobbyView.Place(r,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=Sprite(asset);image.raycastTarget=false;return image;}
        private static Button ButtonAt(Transform parent,string name,string label,float x,float y,float w,float h,int size,bool useBold=true)
        {var r=Rect(parent,name);LabLobbyView.Place(r,x,y,w,h);var image=r.gameObject.AddComponent<LabRoundedGraphic>();image.radius=27;image.color=LabLobbyView.Html("3F3D38");var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var colors=b.colors;colors.disabledColor=Color.white;colors.selectedColor=Color.white;b.colors=colors;var t=TextAt(r,"Label",label,0,0,w,h,size,useBold,TextAnchor.MiddleCenter);t.color=LabLobbyView.Html("F3EBDD");return b;}
        private static Button PlainButton(Transform parent,string name,string label,float x,float y,float w,float h,int size)
        {var b=ButtonAt(parent,name,label,x,y,w,h,size,false);b.targetGraphic.color=Color.clear;b.targetGraphic.canvasRenderer.cullTransparentMesh=false;b.GetComponentInChildren<Text>().color=Ink;return b;}
    }
}
