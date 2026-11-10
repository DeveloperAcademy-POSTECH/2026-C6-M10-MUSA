using UnityEngine;

namespace Hapio
{
    public sealed class GameOverCharacterView : MonoBehaviour
    {
        public RectTransform design;
        public GameObject bul, geumok;
        public UnityEngine.UI.Text dialogue;
        public UnityEngine.UI.Button homeButton;
        private bool selected;

        public void Present(bool canReturnHome)
        {
            if (!selected)
            {
                bool useGeumok = Random.Range(0, 2) == 1;
                bul.SetActive(!useGeumok);
                geumok.SetActive(useGeumok);
                dialogue.text = useGeumok ? "지금, 누가 나갔지?" : "얼른 다시 돌아와!";
                selected = true;
            }
            homeButton.interactable = canReturnHome;
        }

        private void LateUpdate()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.scaleFactor : 1f;
            Rect safe = Screen.safeArea;
            design.localScale = Vector3.one * Mathf.Min(safe.width / factor / 402f, safe.height / factor / 874f);
        }
    }
}
