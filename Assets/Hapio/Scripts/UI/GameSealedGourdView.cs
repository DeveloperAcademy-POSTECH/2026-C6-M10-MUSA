using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Dresses the existing live result viewer; existing gesture and navigation owners remain unchanged.</summary>
    public sealed class GameSealedGourdView : MonoBehaviour
    {
        // Measured from the top of each 402×874 background, below its foreground scenery.
        private static readonly float[] ShadowFloorCenterY = { 590f, 598f, 586f };

        public RectTransform design;
        public RawImage background;
        private GameSealedGourdArt art;
        private GameScreen screen;
        private RawImage shadow;
        private int stage=-1;
        public static void Attach(GameScreen screen)
        {
            if(screen.viewerPanel==null||screen.viewerImage==null||screen.viewerPanel.GetComponent<GameSealedGourdView>()!=null)return;
            var view=screen.viewerPanel.AddComponent<GameSealedGourdView>();view.Build(screen);
        }
        private void Build(GameScreen target)
        {
            screen=target;art=Resources.Load<GameSealedGourdArt>("Hapio/UI/SealedGourdArt");
            var theme=Resources.Load<GameBattleUiTheme>(GameBattleUiTheme.ResourcePath);
            transform.SetParent(screen.transform,false);GameViewerHelpUi.Stretch((RectTransform)transform);
            var backdrop=GetComponent<Image>();if(backdrop!=null)backdrop.color=Color.clear;
            var viewport=(RectTransform)screen.viewerImage.transform.parent;
            // Aspect fitting has already wrapped the same RawImage and its gesture surface.
            viewport.SetParent(transform,false);
            screen.resetViewerButton.transform.SetParent(transform,false);screen.closeViewerButton.transform.SetParent(transform,false);
            foreach(Transform child in transform)
                if(child!=viewport&&child!=screen.resetViewerButton.transform&&child!=screen.closeViewerButton.transform)child.gameObject.SetActive(false);
            background=GameViewerHelpUi.Rect(transform,"Stage Background",0,0,402,874).gameObject.AddComponent<RawImage>();
            GameViewerHelpUi.Stretch(background.rectTransform);background.raycastTarget=false;background.transform.SetAsFirstSibling();
            var floor = GameViewerHelpUi.Rect(transform, "Gourd Shadow", 0, 0, 450, 86);
            floor.pivot = new Vector2(.5f, .5f);
            shadow = floor.gameObject.AddComponent<RawImage>();
            shadow.color = new Color(.24f, .17f, .11f, .34f);
            shadow.raycastTarget = false;
            shadow.enabled = false;
            design=GameViewerHelpUi.Design(transform,"Sealed Gourd Design");
            viewport.SetParent(design,false);GameViewerHelpUi.Place(viewport,0,128,402,414);
            GameViewerHelpUi.Label(design,"Rotate Hint","회전해서 박을 확인하시오,",theme.bold,20,new Color32(104,100,92,255),63,664,276,30);
            Dress(screen.resetViewerButton,"박 리셋","ViewerReset",711,theme.medium);
            Dress(screen.closeViewerButton,"결과로 돌아가기","ResultAction",768,theme.medium);
            ApplyStage(1);
        }
        private void Dress(Button button,string label,string key,float y,Font font)
        {
            button.transform.SetParent(design,false);GameViewerHelpUi.Place((RectTransform)button.transform,84,y,234,45);
            foreach(Transform child in button.transform)child.gameObject.SetActive(false);
            GameFigmaButton.Attach(button,key);
            GameViewerHelpUi.Label(button.transform,"Label",label,font,21,new Color32(245,238,225,255),0,0,234,45);
        }
        public void ApplyStage(int value)
        {
            stage = Mathf.Clamp(value, 1, ShadowFloorCenterY.Length);
            int index = stage - 1;
            background.texture = art.stageBackgrounds[index];
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax =
                new Vector2(.5f, 1f - ShadowFloorCenterY[index] / 874f);
            shadow.rectTransform.anchoredPosition = Vector2.zero;
        }
        private void LateUpdate()
        {
            if(design==null)return;
            var session=GameSession.Instance;
            int desired=GameStageBackgrounds.PresentationStage(session);
            if(Mathf.Clamp(desired,1,ShadowFloorCenterY.Length)!=stage)ApplyStage(desired);
            GameViewerHelpUi.Fit(design);
            // Keep the shadow on the stretched backdrop while sizing it with the fitted gourd.
            shadow.rectTransform.localScale = design.localScale;
            shadow.texture = session != null && session.IsViewerOpen ? session.PreviewShadow?.Render() : null;
            shadow.enabled = shadow.texture != null;
        }
    }
}
