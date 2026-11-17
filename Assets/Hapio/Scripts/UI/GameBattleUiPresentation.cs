using System;
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
        private Image staminaPanel, staminaNameArt;
        private Text staminaName;
        private Sprite[] staminaPanels, staminaNames;
        private const string CharacterArt = "Hapio/UI/BattleCharacters/";
        private static readonly string[] CharacterNames = { "불이", "솔이", "묵이", "금옥", "만복" };
        // Figma 5573:4233. Match the visible name and portrait in each variant. Colour tokens (Hapio.tokens.json), in
        // art order 불이·솔이·묵이·금옥·만복: 5OrbsColor/Firered/300***, 5OrbsColor/TreeGreen/300***,
        // 5OrbsColor/WaterBlue/300***, 5OrbsColor/MetalYellow/400, 5OrbsColor/SoilBrown/300***.
        private static readonly Color32[] FilledCells = {
            new Color32(229,130,115,255), new Color32(130,165,144,255), new Color32(110,160,190,255),
            new Color32(250,196,77,255), new Color32(163,140,127,255)
        };
        // The same families' 100 tokens: 5OrbsColor/{Firered,TreeGreen,WaterBlue,MetalYellow,SoilBrown}/100.
        private static readonly Color32[] EmptyCells = {
            new Color32(246,213,208,255), new Color32(213,225,218,255), new Color32(207,223,233,255),
            new Color32(254,240,211,255), new Color32(224,217,212,255)
        };
        private static readonly Rect[] NameBounds = {
            new Rect(106,64,34,26), new Rect(106,64,34,26), new Rect(106,64,34,26),
            new Rect(107.5f,67,31,21), new Rect(104,64,38,26)
        };
        private Material plateMaterial, originalPlateMaterial;
        private RawImage boardArtwork;
        private Renderer plateRenderer;
        private bool originalPlateEnabled;
        public RawImage BoardArtwork => boardArtwork;

        // Figma frame 402×874; original, uncropped component bounds (before export padding).
        public static Rect BoardDesignBounds(int stage) => new Rect(stage == 1 ? -162 : -165,459,732.850891f,494.996063f);
        private LabTeamBar dressedTeamBar;
        private int appliedStage = -1;
        private int displayedStaminaCharacter = -2;
        private string displayedStaminaTeam;
        public int AppliedStage => appliedStage;
        public GameBattleUiTheme Theme => theme;

        internal static void BuildFinishReplayCaption(RectTransform caption, int playerSlot)
        {
            var replayTheme = Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            Font bold = replayTheme != null && replayTheme.bold != null
                ? replayTheme.bold : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var page = GameViewerHelpUi.Design(caption, "Final Hit Design");
            // Figma 4813:9429. TextColor/DarkIvory/100 and TextColor/GreenGray/700.
            Color title = new Color32(247, 242, 237, 255);
            Color hint = new Color32(66, 67, 58, 255);
            if (playerSlot > 0)
                AddReplayLabel(page, "Player", "P" + playerSlot, bold, title, 50, 79, 55);
            AddReplayLabel(page, "Final Hit", "마지막 일격!", bold, title, 50, playerSlot > 0 ? 139 : 79, 55);
            AddReplayLabel(page, "Skip Hint", "눌러서 건너뛰시오.", bold, hint, 20, 762, 22);
        }

        private static void AddReplayLabel(Transform page, string name, string caption, Font font,
            Color color, int size, float y, float height)
        {
            // Keep the mobile typography sharp at the same logical size.
            var label = GameViewerHelpUi.Label(page, name, caption, font, size * 2,
                color, 63, y, 552, height * 2);
            label.rectTransform.localScale = Vector3.one * .5f;
        }

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

            // Land grab keeps the HUD from before #785 (team badges, the blue stamina row, the local team ring).
            if (territory) BuildTeamHud(hud);
            else BuildCharacterHud(hud, parent);
            var minimap = bindings.sceneController.GetComponent<LabDrumMinimap>() ?? bindings.sceneController.gameObject.AddComponent<LabDrumMinimap>();
            minimap.compactGamePresentation = true; minimap.size = new Vector2(100,72); minimap.lineWidth = 2;
            minimap.labelSize = 10; minimap.lineColor = GameBattleUiColors.Brown; minimap.otherTextColor = GameBattleUiColors.Ink;
            minimap.battleCharacterPortraits = territory ? null : hud.battleCharacterPortraits;
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

        private void BuildTeamHud(LabHud hud)
        {
            RectTransform stamina = (RectTransform)hud.staminaFill.transform.parent;
            const float badgeWidth = 45, badgeHeight = 47, staminaWidth = 241, staminaGap = 9;
            const float designCenterX = 201, bottomRowTop = 800;
            // Use the same center anchor and fixed offsets so the row stays centered as the canvas resizes.
            Guide(stamina, designCenterX - staminaWidth * .5f, bottomRowTop + 4, staminaWidth, 39);
            stamina.anchoredPosition = new Vector2((badgeWidth + staminaGap) * .5f, 0);
            foreach (Transform child in stamina) if (child != hud.staminaFill.transform) child.gameObject.SetActive(false);
            Place(hud.staminaFill.rectTransform, 9, 8, 223, 23);
            var cellSurface = GameFigmaStrokeLibrary.Find("StaminaCells");
            if (cellSurface != null)
                Place(hud.staminaFill.rectTransform, 9 + cellSurface.offset.x, 8 + cellSurface.offset.y,
                    cellSurface.exportSize.x, cellSurface.exportSize.y);
            hud.staminaFill.sprite = theme.staminaCells; hud.staminaFill.color = Color.white;
            if (hud.staminaFill is LabShapeFillImage cells)
            { cells.filledColor = GameBattleUiColors.Blue; cells.emptyColor = GameBattleUiColors.BlueEmpty; }
            staminaFrame = Frame(stamina, "Stage Stamina Outline", new Rect(0,0,241,39), GameBattleUiColors.Brown, 6, 11);
            staminaFrame.UseBrush("Stamina");
            hud.playerCircleSprites = theme.playerBadges;
            hud.territoryBlueBadges = theme.territoryBlueBadges;
            hud.territoryRedBadges = theme.territoryRedBadges;
            RectTransform localBadge = hud.localPlayerCircle.rectTransform;
            Guide(localBadge, designCenterX - badgeWidth * .5f, bottomRowTop, badgeWidth, badgeHeight);
            localBadge.anchoredPosition = new Vector2(-(staminaWidth + staminaGap) * .5f, 0);
            const float neighborTop = 472f;
            if (hud.leftPlayerCircle != null)
                Guide(hud.leftPlayerCircle.rectTransform, -8, neighborTop, badgeWidth, badgeHeight);
            if (hud.rightPlayerCircle != null)
                Guide(hud.rightPlayerCircle.rectTransform, 365, neighborTop, badgeWidth, badgeHeight);
        }

        private void BuildCharacterHud(LabHud hud, RectTransform parent)
        {
            hud.battleCharacterPortraits = LoadCharacterSprites("Portrait");
            hud.battleCharacterNames = CharacterNames;
            hud.playerCircleSprites = theme.playerBadges;
            hud.territoryBlueBadges = theme.territoryBlueBadges;
            hud.territoryRedBadges = theme.territoryRedBadges;
            staminaPanels = LoadCharacterSprites("Panel");
            staminaNames = LoadCharacterSprites("Name");
            hud.battleStaminaPortraits = LoadCharacterSprites("StaminaPortrait");

            // Figma 5548:6617 places the 402×101 panel at y=773 (canvas height 874).
            const float panelTop = 773;
            RectTransform cells = (RectTransform)hud.staminaFill.transform.parent;
            cells.name = "Stamina Cells";
            foreach (Transform child in cells)
                if (child != hud.staminaFill.transform) child.gameObject.SetActive(false);
            Stretch(hud.staminaFill.rectTransform);
            hud.staminaFill.sprite = Resources.Load<Sprite>(CharacterArt + "Cells") ?? theme.staminaCells;
            hud.staminaFill.color = Color.white;

            // Keep the backdrop and portrait out of the cells' transform. Existing shortage,
            // consumption and fever effects stay bound to the same Image.
            var panel = Rect(parent, "Character Stamina Panel");
            panel.SetSiblingIndex(cells.GetSiblingIndex());
            Guide(panel, -3, panelTop - 1, 387, 102);
            // The mask export includes one design unit of stroke outside the cells.
            AlignStaminaLayer(cells, panel, new Rect(150, 66, 638f / 3f, 74f / 3f));
            staminaPanel = panel.gameObject.AddComponent<Image>();
            staminaPanel.raycastTarget = false;
            staminaNameArt = Picture(panel, "Figma Character Name", null, NameBounds[0]);
            staminaName = Rect(panel, "Character Name Fallback").gameObject.AddComponent<Text>();
            Place(staminaName.rectTransform, 83, 56, 80, 42);
            Style(staminaName, theme.bold, 22, GameBattleUiColors.Ink);

            // The portrait's PNG includes the two-unit outside stroke.
            AlignStaminaLayer(hud.localPlayerCircle.rectTransform, panel, new Rect(21, 6, 82, 82));
            hud.localPlayerCircle.preserveAspect = true;
            LayoutNeighbor(hud.leftPlayerCircle, hud.leftNeighborText, -4);
            LayoutNeighbor(hud.rightPlayerCircle, hud.rightNeighborText, 352);
            var composition = hud.GetComponent<LabOrbHudComposition>();
            if (composition != null) composition.presentationStaminaPanel = panel;
        }

        private static void AlignStaminaLayer(RectTransform layer, RectTransform panel, Rect bounds)
        {
            layer.anchorMin = layer.anchorMax = panel.anchorMin;
            layer.pivot = Vector2.one * .5f;
            layer.sizeDelta = bounds.size;
            Vector2 offset = bounds.center - panel.rect.size * .5f;
            layer.anchoredPosition = new Vector2(offset.x, -offset.y);
        }

        private static Sprite[] LoadCharacterSprites(string prefix)
        {
            var sprites = new Sprite[CharacterNames.Length];
            for (int i = 0; i < sprites.Length; i++)
                sprites[i] = Resources.Load<Sprite>(CharacterArt + prefix + (i + 1));
            return sprites;
        }

        private void LayoutNeighbor(Image portrait, Text label, float x)
        {
            if (portrait != null)
            {
                Guide(portrait.rectTransform, x, 475, 54, 54);
                portrait.preserveAspect = true;
            }
            if (label == null || portrait == null) return;
            label.transform.SetParent(portrait.transform, false);
            Place(label.rectTransform, 0, 62, 54, 19);
            Style(label, theme.bold, 16, Color.black);
            label.gameObject.SetActive(true);
        }

        private void RefreshCharacterStamina()
        {
            if (staminaPanel == null) return;
            var net = session.Network;
            var snapshot = net?.Snapshot;
            var players = snapshot?.players;
            var local = players == null ? null : Array.Find(players, player => player != null && player.id == net.LocalPlayerId);
            int number = local == null ? 0 : local.seat + 1;
            int seat = LabHud.BattleCharacterIndex(snapshot, number);
            string team = LabHud.BattleTeamCaption(snapshot, number);
            if (seat == displayedStaminaCharacter && team == displayedStaminaTeam) return;
            displayedStaminaCharacter = seat;
            displayedStaminaTeam = team;
            var hud = bindings.hud;
            bool hasCharacter = seat >= 0 && seat < CharacterNames.Length;
            staminaPanel.sprite = hasCharacter ? staminaPanels[seat] : null;
            staminaPanel.enabled = staminaPanel.sprite != null;
            staminaNameArt.sprite = hasCharacter && team == null ? staminaNames[seat] : null;
            staminaNameArt.enabled = staminaNameArt.sprite != null;
            if (staminaNameArt.enabled) Place(staminaNameArt.rectTransform, NameBounds[seat].x, NameBounds[seat].y,
                NameBounds[seat].width, NameBounds[seat].height);
            staminaName.enabled = !staminaNameArt.enabled;
            AlignStaminaLayer(hud.localPlayerCircle.rectTransform, staminaPanel.rectTransform,
                new Rect(seat == 1 || seat == 4 ? 20 : 21, 6, 82, 82));
            staminaName.text = team ?? (hasCharacter ? CharacterNames[seat] : number > 0 ? "P" + number : string.Empty);
            if (hud.staminaFill is LabShapeFillImage fill)
            {
                Color filled = hasCharacter ? (Color)FilledCells[seat] : GameBattleUiColors.Blue;
                Color empty = hasCharacter ? (Color)EmptyCells[seat] : GameBattleUiColors.BlueEmpty;
                if (fill.filledColor != filled || fill.emptyColor != empty)
                {
                    fill.filledColor = filled;
                    fill.emptyColor = empty;
                    fill.SetVerticesDirty();
                }
            }
        }

        public void ApplyStage(int stage)
        {
            if (theme == null || parts == null) return;
            appliedStage = Mathf.Clamp(stage, 1, 3);
            bool light = GameBattleUiTheme.LightInkForStage(appliedStage);
            Color ink = light ? GameBattleUiColors.Light : GameBattleUiColors.Ink;
            underline.sprite = light ? theme.underlineLight : theme.underlineDark;
            bindings.stageLabel.color = ink; bindings.hud.hpText.color = ink; paintFrame.color = ink;
            if (staminaFrame != null) staminaFrame.color = theme.staminaBorders[appliedStage - 1];
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
            var shortage = bindings.sceneController.GetComponent<LabStaminaShortFeedback>();
            // #788: on the character panel only the 6 cells rise, jitter a little sideways (clear of the name) and turn red.
            if (shortage != null) shortage.LiftGaugeMode = staminaPanel != null;
            bindings.sceneController.GetComponent<LabDrumMinimap>()?.Bind(session.Network, bindings.hud.rightPlayerCircle.rectTransform, theme.medium);
            var composition = bindings.hud.GetComponent<LabOrbHudComposition>();
            if (composition != null)
            {
                composition.presentationBackdrop = boardArtwork.rectTransform;
                composition.presentationStaminaPanel = staminaPanel != null ? staminaPanel.rectTransform : null;
            }
        }

        private void LateUpdate()
        {
            if (session == null || theme == null) return;
            int stage = Mathf.Clamp(GameStageBackgrounds.PresentationStage(session), 1, 3);
            if (appliedStage != stage) ApplyStage(stage);
            RefreshCharacterStamina();
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
            root.Find("Figma Team Outline Paper Mask")?.SetAsFirstSibling();
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
            // Figma fills the logical shape underneath a centred stroke. Draw paper first, then the brush,
            // instead of leaving the old inside-stroke inset as a transparent gap around the paper.
            var mask = Rect(parent, name + " Paper Mask");
            if (guide) Guide(mask,area.x,area.y,area.width,area.height); else Place(mask,area.x,area.y,area.width,area.height);
            mask.SetSiblingIndex(root.GetSiblingIndex());
            var shape = mask.gameObject.AddComponent<LabRoundedGraphic>(); shape.radius = radius; shape.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var paper = Picture(mask, "Paper", theme.paper, new Rect(0,0,area.width,area.height));
            paper.color = new Color(1,1,1,name == "Frame" || name == "Figma Team Outline" ? .6f : 1);
            var frame = root.gameObject.AddComponent<GameUiFrame>(); frame.color = border; frame.stroke = stroke; frame.radius = radius; frame.raycastTarget = false;
            frame.UseBrush(name == "Time" ? "Time" : name == "Paper" ? "Pause" : name == "Figma Team Outline" ? "TeamPaint" : "Paint");
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
