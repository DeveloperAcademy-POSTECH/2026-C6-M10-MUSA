using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Hapio HUD art only. The existing HUD owns values/input and the stage background owns its scenery.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GameBattleUiPresentation : MonoBehaviour
    {
        public GameBattleUiTheme theme;
        private GameBattleBindings bindings;
        private GameSession session;
        private RectTransform parts, paint;
        private Image underline;
        private GameUiFrame paintFrame, staminaFrame;
        private Material plateMaterial, originalPlateMaterial;
        private RawImage boardArtwork;
        private Renderer plateRenderer;
        private bool originalPlateEnabled;
        public RawImage BoardArtwork => boardArtwork;

        // Figma frame 402×874; original, uncropped component bounds (before export padding).
        public static Rect BoardDesignBounds(int stage) => new Rect(stage == 1 ? -162 : -165,459,732.850891f,494.996063f);
        private LabTeamBar dressedTeamBar;
        private int appliedStage = -1;
        public int AppliedStage => appliedStage;
        public GameBattleUiTheme Theme => theme;

        public void Bind(GameBattleBindings target, GameSession owner)
        {
            bindings = target; session = owner;
            if (theme == null) theme = Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            if (theme == null || bindings.hud == null) { enabled = false; return; }
            ApplyOrbAppearance(); Build(); ApplyStage(GameStageBackgrounds.PresentationStage(owner));
            gameObject.AddComponent<GameBoardBackgroundContinuation>().Bind(bindings);
        }

        public void ApplyOrbAppearance()
        {
            if (theme != null && session != null && session.Network != null && session.Network.config != null)
                session.Network.config.SetOrbPresentationOverride(theme.orbAppearance);
        }

        private void Build()
        {
            LabHud hud = bindings.hud;
            RectTransform parent = (RectTransform)hud.battleUi.transform;
            parts = Rect(parent, "Hapio Battle UI Components"); Stretch(parts); parts.SetAsFirstSibling();
            bool territory = session.IsTerritory;
            float timeX = territory ? 26 : 25;
            float timeY = territory ? 34 : 33;
            PaperFrame(parts, "Time", new Rect(timeX, timeY, 77, 30), GameBattleUiColors.Brown, 4, 11);
            Transform oldTime = hud.timeText.transform.parent;
            hud.timeText.transform.SetParent(parts, false); Guide(hud.timeText.rectTransform, timeX, timeY - 1, 77, 30);
            Style(hud.timeText, theme.bold, 22, GameBattleUiColors.Ink);
            if (oldTime != parent && oldTime != parts) oldTime.gameObject.SetActive(false);
            var pause = Rect(parts, "Pause Artwork"); Guide(pause, 340, 32, 33, 34);
            PaperFrame(pause, "Paper", new Rect(2, 2, 30, 30), GameBattleUiColors.Brown, 4, 15, false);
            Picture(pause, "Left Stroke", theme.pauseStroke, new Rect(11, 11, 4, 13));
            Picture(pause, "Right Stroke", theme.pauseStroke, new Rect(19, 11, 4, 13));
            foreach (Transform child in hud.pauseButton.transform) child.gameObject.SetActive(false);
            hud.pauseButton.targetGraphic.color = Color.clear;
            Guide((RectTransform)hud.pauseButton.transform, 334, 27, 44, 44);
            bindings.stageLabel.transform.SetParent(parts, false);
            if (territory) Guide(bindings.stageLabel.rectTransform, 109, 51, 189.4f, 55);
            else Guide(bindings.stageLabel.rectTransform, 124, 63, 157, 44);
            Style(bindings.stageLabel, session.IsTerritory ? theme.bold : theme.medium, session.IsTerritory ? 30 : 25, GameBattleUiColors.Ink);
            underline = Picture(parts, "Stage Underline", theme.underlineDark, new Rect());
            Guide(underline.rectTransform, territory ? 126 : 124, territory ? 92.65f : 99, 158.25f, 17.1f);

            Transform oldPaint = hud.hpText.transform.parent;
            paint = Rect(parts, "Paint Progress"); Guide(paint, 71, 136, 308, 28);
            PaperFrame(paint, "Frame", new Rect(0, 0, 242, 27), GameBattleUiColors.Ink, 3, 8, false);
            paintFrame = paint.Find("Frame").GetComponent<GameUiFrame>();
            var mask = Rect(paint, "Fill Mask"); Place(mask, 3, 3, 236, 21);
            var rounded = mask.gameObject.AddComponent<LabRoundedGraphic>(); rounded.radius = 5;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            hud.yokaiHpFill.transform.SetParent(mask, false); Stretch(hud.yokaiHpFill.rectTransform);
            // The fill sprite is about 7.9:1 and this track 11.2:1; keeping its aspect drew 100% at about 70% of the track.
            hud.yokaiHpFill.preserveAspect = false;
            hud.yokaiHpFill.color = GameBattleUiColors.Red;
            if (hud.yokaiHpFill is LabShapeFillImage shaped)
            { shaped.filledColor = GameBattleUiColors.Red; shaped.emptyColor = Color.clear; hud.yokaiHpFill.color = Color.white; }
            hud.hpText.transform.SetParent(paint, false); Place(hud.hpText.rectTransform, 242, 0, 66, 28);
            Style(hud.hpText, theme.bold, 21, GameBattleUiColors.Ink);
            if (oldPaint != parent && oldPaint != parts) oldPaint.gameObject.SetActive(false);

            RectTransform stamina = (RectTransform)hud.staminaFill.transform.parent;
            Guide(stamina, 111, 804, 241, 39);
            foreach (Transform child in stamina) if (child != hud.staminaFill.transform) child.gameObject.SetActive(false);
            Place(hud.staminaFill.rectTransform, 9, 8, 223, 23);
            hud.staminaFill.sprite = theme.staminaCells; hud.staminaFill.color = Color.white;
            if (hud.staminaFill is LabShapeFillImage cells)
            { cells.filledColor = GameBattleUiColors.Blue; cells.emptyColor = GameBattleUiColors.BlueEmpty; }
            staminaFrame = Frame(stamina, "Stage Stamina Outline", new Rect(0,0,241,39), GameBattleUiColors.Brown, 6, 11);
            hud.playerCircleSprites = theme.playerBadges;
            Guide(hud.localPlayerCircle.rectTransform, territory ? 57 : 58, 800, 45, 47);
            if (hud.leftPlayerCircle != null) hud.leftPlayerCircle.rectTransform.sizeDelta = new Vector2(45,47);
            if (hud.rightPlayerCircle != null) hud.rightPlayerCircle.rectTransform.sizeDelta = new Vector2(45,47);
            var minimap = bindings.sceneController.GetComponent<LabDrumMinimap>() ?? bindings.sceneController.gameObject.AddComponent<LabDrumMinimap>();
            minimap.compactGamePresentation = true; minimap.size = new Vector2(100,72); minimap.lineWidth = 2;
            minimap.labelSize = 10; minimap.lineColor = GameBattleUiColors.Brown; minimap.otherTextColor = GameBattleUiColors.Ink;
            minimap.playerSprites = theme.minimapBadges; minimap.drumIconSprite = theme.drumIcon; minimap.soloDrumSprite = theme.drumIcon;
            minimap.teamBlueMarker = theme.teamBlueMarker; minimap.teamRedMarker = theme.teamRedMarker;
            minimap.teamPairConnector = theme.teamPairConnector;
            minimap.teamTrioConnector = theme.teamTrioConnector;
            boardArtwork = Rect(parent,"Hapio Board Artwork").gameObject.AddComponent<RawImage>();
            boardArtwork.raycastTarget = false; boardArtwork.enabled = false;
            Renderer plate = bindings.board.transform.Find("OrbBoardPlate").GetComponent<Renderer>();
            plateRenderer = plate; originalPlateEnabled = plate.enabled; originalPlateMaterial = plate.sharedMaterial;
            plateMaterial = new Material(plate.sharedMaterial) { name = "Hapio Board UI (local instance)" };
            plate.sharedMaterial = plateMaterial;
        }

        public void ApplyStage(int stage)
        {
            if (theme == null || parts == null) return;
            appliedStage = Mathf.Clamp(stage, 1, 3);
            bool light = GameBattleUiTheme.LightInkForStage(appliedStage);
            Color ink = light ? GameBattleUiColors.Light : GameBattleUiColors.Ink;
            underline.sprite = light ? theme.underlineLight : theme.underlineDark;
            bindings.stageLabel.color = ink; bindings.hud.hpText.color = ink; paintFrame.color = ink;
            staminaFrame.color = theme.staminaBorders[appliedStage - 1];
            plateMaterial.mainTexture = theme.BoardFor(appliedStage);
            boardArtwork.texture = theme.BoardFor(appliedStage);
            Rect bounds = BoardDesignBounds(appliedStage);
            // The local 2× export has 1.5 design units of transparent padding around the original layers.
            Guide(boardArtwork.rectTransform,bounds.x-1.5f,bounds.y-1.5f,
                boardArtwork.texture.width*.5f,boardArtwork.texture.height*.5f);
            dressedTeamBar = null;
        }

        private void Start()
        {
            if (bindings == null || theme == null) return;
            bindings.sceneController.GetComponent<LabDrumMinimap>()?.Bind(session.Network, bindings.hud.rightPlayerCircle.rectTransform, theme.medium);
            var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            if (composition != null) composition.presentationBackdrop = boardArtwork.rectTransform;
        }

        private void LateUpdate()
        {
            if (session == null || theme == null) return;
            int stage = Mathf.Clamp(GameStageBackgrounds.PresentationStage(session), 1, 3);
            if (appliedStage != stage) ApplyStage(stage);
            bindings.stageLabel.text = session.IsTerritory ? "땅따먹기" : appliedStage == 1 ? "첫 번째 요괴" : appliedStage == 2 ? "두 번째 요괴" : "세 번째 요괴";
            var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            if (composition != null)
            {
                composition.presentationBackdrop = boardArtwork.rectTransform;
                boardArtwork.enabled = composition.IsCompositing;
                plateRenderer.enabled = !composition.IsCompositing && originalPlateEnabled;
            }
            var bar = bindings.hud.TeamBar;
            if (bar != null && bar != dressedTeamBar)
            { DressTeamBar(bar, GameBattleUiTheme.LightInkForStage(appliedStage)); dressedTeamBar = bar; }
            if (bar != null && bar.LocalRing != null)
                bar.LocalRing.color = LabTeams.TeamOf(session.Network.Snapshot, session.Network.LocalPlayerId) == LabTeam.Blue ? GameBattleUiColors.Blue : GameBattleUiColors.Fire;
        }

        private void DressTeamBar(LabTeamBar bar, bool light)
        {
            var root = (RectTransform)bar.transform;
            Guide(root, 30, 128, 342, 27);
            bar.PinFlagsToEnds();
            Color ink = light ? GameBattleUiColors.Light : GameBattleUiColors.Ink;
            root.Find("Frame").gameObject.SetActive(false);
            var outline = root.Find("Figma Team Outline");
            if (outline == null) outline = PaperFrame(root,"Figma Team Outline",new Rect(0,0,342,27),ink,3,8,false);
            outline.GetComponent<GameUiFrame>().color = ink; outline.SetAsFirstSibling();
            var blue = root.Find("Fill Mask/Blue Fill").GetComponent<Image>(); blue.color = GameBattleUiColors.Blue;
            var red = root.Find("Fill Mask/Red Fill").GetComponent<Image>(); red.color = GameBattleUiColors.Fire;
            foreach (Transform child in root) if (child.name.StartsWith("Tick ")) child.gameObject.SetActive(false);
            for (int i = 1; i < 10; i++)
            {
                var tick = Picture(root, "Figma Tick " + i, i == 5 ? theme.tickCenter : light ? theme.tickLight : theme.tickDark, new Rect(i * 34.2f - (i == 5 ? 2 : .5f), 0, i == 5 ? 4 : 1, 26));
                tick.color = Color.white;
            }
            Flag(root.Find("Blue Flag"), light ? theme.blueFlagLight : theme.blueFlagDark, "청팀",
                light ? GameBattleUiColors.Light : GameBattleUiColors.Blue, false);
            Flag(root.Find("Red Flag"), light ? theme.redFlagLight : theme.redFlagDark, "홍팀",
                light ? GameBattleUiColors.Light : GameBattleUiColors.Fire, true);
        }

        private void Flag(Transform flag, Sprite art, string label, Color ink, bool right)
        {
            foreach (Transform child in flag) child.gameObject.SetActive(false);
            var image = Picture(flag, "Figma Flag", art, new Rect(right ? -31.7f : -12.5f, right ? -41 : -42, 43.73f, 44.76f));
            image.rectTransform.localScale = new Vector3(right ? -1 : 1, 1, 1);
            image.gameObject.SetActive(true);
            var words = flag.Find("Figma Team Name")?.GetComponent<Text>();
            if (words == null) words = Rect(flag, "Figma Team Name").gameObject.AddComponent<Text>();
            Place(words.rectTransform, right ? -38.2f : -20.2f, right ? -33.7f : -34.7f, 58, 28);
            Style(words, theme.medium, 18, ink);
            words.text = label;
            words.rectTransform.localEulerAngles = new Vector3(0, 0, right ? -21.45f : 16.88f);
            words.gameObject.SetActive(true);
        }

        private RectTransform PaperFrame(Transform parent, string name, Rect area, Color border, float stroke, float radius, bool guide = true)
        {
            var root = Rect(parent, name);
            if (guide) Guide(root,area.x,area.y,area.width,area.height); else Place(root,area.x,area.y,area.width,area.height);
            var mask = Rect(root, "Paper Mask"); Place(mask, stroke,stroke,area.width-2*stroke,area.height-2*stroke);
            var shape = mask.gameObject.AddComponent<LabRoundedGraphic>(); shape.radius = radius-stroke; shape.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var paper = Picture(mask, "Paper", theme.paper, new Rect(0,0,area.width-2*stroke,area.height-2*stroke));
            paper.color = new Color(1,1,1,name == "Frame" || name == "Figma Team Outline" ? .6f : 1);
            var frame = root.gameObject.AddComponent<GameUiFrame>(); frame.color = border; frame.stroke = stroke; frame.radius = radius; frame.raycastTarget = false;
            return root;
        }
        private static GameUiFrame Frame(Transform parent, string name, Rect box, Color color, float stroke, float radius)
        {
            var rect = parent.Find(name) as RectTransform ?? Rect(parent,name); Place(rect,box.x,box.y,box.width,box.height);
            var frame = rect.GetComponent<GameUiFrame>() ?? rect.gameObject.AddComponent<GameUiFrame>();
            frame.color=color; frame.stroke=stroke; frame.radius=radius; frame.raycastTarget=false; return frame;
        }
        private static Image Picture(Transform parent, string name, Sprite sprite, Rect box)
        {
            var rect = parent.Find(name) as RectTransform ?? Rect(parent,name); Place(rect,box.x,box.y,box.width,box.height);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>(); image.sprite=sprite; image.raycastTarget=false; return image;
        }
        private static RectTransform Rect(Transform parent, string name)
        { var go = new GameObject(name,typeof(RectTransform)); go.layer=parent.gameObject.layer; go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
        private static void Style(Text text, Font font, int size, Color color)
        { text.font=font; text.fontSize=size; text.fontStyle=FontStyle.Normal; text.color=color; text.alignment=TextAnchor.MiddleCenter; text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow; text.raycastTarget=false; }
        private static void Place(RectTransform rect,float x,float y,float w,float h) => LabLobbyView.Place(rect,x,y,w,h);
        private static void Place(Transform rect,float x,float y,float w,float h) => Place((RectTransform)rect,x,y,w,h);
        private static void Guide(RectTransform rect,float x,float y,float w,float h)
        { rect.anchorMin=rect.anchorMax=new Vector2((x+w*.5f)/402f,1-(y+h*.5f)/874f); rect.pivot=Vector2.one*.5f; rect.anchoredPosition=Vector2.zero; rect.sizeDelta=new Vector2(w,h); }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private void OnDestroy()
        {
            if (plateRenderer != null)
            {
                plateRenderer.enabled = originalPlateEnabled;
                if (plateRenderer.sharedMaterial == plateMaterial) plateRenderer.sharedMaterial = originalPlateMaterial;
            }
            if (bindings != null && bindings.hud != null)
            {
                var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
                if (composition != null && boardArtwork != null && composition.presentationBackdrop == boardArtwork.rectTransform)
                    composition.presentationBackdrop = null;
            }
            if (boardArtwork != null) Destroy(boardArtwork.gameObject);
            if (plateMaterial != null) Destroy(plateMaterial);
        }
    }
}
