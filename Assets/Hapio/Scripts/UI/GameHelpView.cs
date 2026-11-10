using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Local help overlay. It blocks board/UI touches without pausing other players.</summary>
    public sealed class GameHelpView : MonoBehaviour
    {
        public Button helpButton,closeButton;
        public GameObject modal;
        public RectTransform design;
        private GameBattleBindings bindings;
        private GameSession session;
        public bool IsOpen=>modal!=null&&modal.activeSelf;

        public void Bind(GameBattleBindings target,GameSession owner)
        {
            bindings=target;session=owner;
            var hud=target.hud;
            var old=hud.battleUi.transform.Find("Hapio Battle UI Components/Pause Artwork");
            if(old!=null)old.gameObject.SetActive(false);
            foreach(Transform child in hud.pauseButton.transform)child.gameObject.SetActive(false);
            hud.pauseButton.targetGraphic.color=Color.clear;
            GameHudButtonArtwork.Attach(hud.pauseButton,"Pause");
            var pause = (RectTransform)hud.pauseButton.transform;
            helpButton = GameViewerHelpUi.Hit(pause.parent, "Help", 0, 0, 44, 44);
            var help = (RectTransform)helpButton.transform;
            help.anchorMin = pause.anchorMin;
            help.anchorMax = pause.anchorMax;
            help.pivot = pause.pivot;
            // The icons share a top-left inset, even when the tutorial uses a smaller pause hit area.
            Vector2 pauseTopLeft = new Vector2(pause.rect.xMin, pause.rect.yMax);
            Vector2 helpTopLeft = new Vector2(help.rect.xMin, help.rect.yMax);
            help.anchoredPosition = pause.anchoredPosition + pauseTopLeft - helpTopLeft + Vector2.left * 47f;
            GameHudButtonArtwork.Attach(helpButton,"Help");
            helpButton.onClick.AddListener(Open);
            var root=new GameObject("Help Overlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);modal=root;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=GameTutorialDirector.OverlaySortingOrder+10;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(402,874);scaler.matchWidthOrHeight=.5f;
            var dim=GameViewerHelpUi.Rect(root.transform,"Dim",0,0,402,874).gameObject.AddComponent<Image>();
            GameViewerHelpUi.Stretch(dim.rectTransform);dim.color=new Color(0,0,0,.5f);
            design=GameViewerHelpUi.Design(root.transform,"Help Design");
            var theme=Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            GameViewerHelpUi.Surface(design,"HelpPanel",27,223,348,490);
            GameViewerHelpUi.Surface(design,"HelpTitle",98,191,194,59);
            GameViewerHelpUi.Label(design,"Title","도움말",theme.bold,30,GameBattleUiColors.Ink,98,194,194,53);
            closeButton=GameViewerHelpUi.Hit(design,"Close Help",322,237,37,37);
            var closeImage=closeButton.GetComponent<Image>();
            closeImage.sprite=hud.resumeButton.GetComponent<Image>().sprite;closeImage.color=Color.white;
            closeButton.onClick.AddListener(Close);
            string[] titles={"만들기","합치기","굴리기","색칠하기"};
            string[] descriptions={"북을 쳐서 흑백 음양\n구슬을 만드시오.","흑과 백을 합치면\n오행 색 구슬이 돼오.","구슬을 옆으로 밀어\n필요한 친구에게 보내시오.","구슬을 던져 박을 칠하시오.\n다 칠하면 봉인 성공!"};
            for(int i=0;i<4;i++)
            {
                float y=293+i*103.05224f;
                GameViewerHelpUi.Surface(design,"HelpStep"+(i+1),229.903f,y,i==1?121.7127f:120.9795f,85.05224f);
                var badge=GameViewerHelpUi.Rect(design,"Step "+(i+1),51,y+8.4683f,21.2631f,21.3172f).gameObject.AddComponent<LabRoundedGraphic>();
                badge.radius=11;badge.color=GameBattleUiColors.Ink;badge.raycastTarget=false;
                GameViewerHelpUi.Label(badge.transform,"Number",(i+1).ToString(),theme.bold,14,GameBattleUiColors.Light,0,0,21.2631f,21.3172f);
                GameViewerHelpUi.Label(design,"Step Title "+i,titles[i],theme.medium,18,GameBattleUiColors.Ink,81.795f,y+8.7854f,120,21,TextAnchor.MiddleLeft);
                GameViewerHelpUi.Label(design,"Step Body "+i,descriptions[i],theme.medium,16,GameBattleUiColors.Brown,51,y+38.584f,182,38,TextAnchor.UpperLeft);
            }
            modal.SetActive(false);
        }
        public void Open()
        {
            if(session==null||session.Network.IsBattlePaused||session.State.phase!=GameFlowPhase.Playing)return;
            bindings.board.CancelDrag();modal.SetActive(true);GameViewerHelpUi.Fit(design);
        }
        public void Close()=>modal.SetActive(false);
        private void LateUpdate()
        {
            if(helpButton==null)return;
            helpButton.gameObject.SetActive(bindings.hud.pauseButton.gameObject.activeInHierarchy);
            if(IsOpen){if(session.State.phase!=GameFlowPhase.Playing||session.Network.IsBattlePaused)Close();else GameViewerHelpUi.Fit(design);}
        }
    }
}
