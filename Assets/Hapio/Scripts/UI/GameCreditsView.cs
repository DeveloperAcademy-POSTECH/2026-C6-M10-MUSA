using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// Home 타이틀 오른쪽 위의 "크레딧" 버튼과 크레딧 창 (#602). 버튼과 창은 공통 로비 Prefab에 저장되어 있고
    /// (Hapio > Upgrade Home Credits), 이 컴포넌트는 열기/닫기와 글 채우기만 한다. 글은 <see cref="source"/>
    /// 텍스트 파일에서 읽으므로 출처가 늘어나면 그 파일만 고치면 된다.
    /// </summary>
    public sealed class GameCreditsView : MonoBehaviour
    {
        [Tooltip("타이틀 화면 오른쪽 위의 \"크레딧\" 버튼. 타이틀이 보일 때만 보인다.")]
        public Button openButton;
        [Tooltip("크레딧 창 전체 (어두운 뒤판 + 한지 카드).")]
        public GameObject panel;
        public Button closeButton;
        [Tooltip("카드 바깥 어두운 곳을 눌러도 닫힌다.")]
        public Button dismissButton;
        public Text body;
        public ScrollRect scroll;
        [Tooltip("크레딧 글. 기본 Assets/Hapio/Art/UI/Credits/Credits.txt")]
        public TextAsset source;
        [Tooltip("창이 열려 있는 동안 보여야 하는 화면. 이 화면이 사라지면(방 목록으로 가는 등) 창도 닫는다.")]
        public GameObject titlePage;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (dismissButton != null) dismissButton.onClick.AddListener(Close);
            Fill();
            if (panel != null) panel.SetActive(false);
        }

        public void Fill()
        {
            if (body != null && source != null) body.text = source.text.Replace("\r\n", "\n").TrimEnd();
        }

        public void Open()
        {
            if (panel == null || titlePage != null && !titlePage.activeInHierarchy) return;
            Fill();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            if (scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (IsOpen && titlePage != null && !titlePage.activeInHierarchy) Close();
        }
    }
}
