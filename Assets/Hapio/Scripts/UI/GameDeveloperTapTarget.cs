using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>An invisible, permission-gated entry point; it never changes the saved Canvas layout.</summary>
    public sealed class GameDeveloperTapTarget : MonoBehaviour
    {
        public const int RequiredTaps = 3;
        public const float TapWindowSeconds = 1.2f;
        private const string CanvasName = "Hidden Developer Entry Canvas";
        private Func<bool> canEnter;
        private UnityAction open;
        private UnityEngine.UI.Image hitImage;
        private GameObject overlay;
        public GameObject TapArea => hitImage != null ? hitImage.gameObject : null;
        private int tapCount;
        private double firstTapAt;

        /// <summary>The owner controls lifetime; a separate Scene root keeps Canvas scaling independent.</summary>
        public static GameDeveloperTapTarget Attach(GameObject owner, Func<bool> canEnter, UnityAction open)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            GameDeveloperTapTarget target = owner.GetComponent<GameDeveloperTapTarget>();
            if (target == null) target = owner.AddComponent<GameDeveloperTapTarget>();
            if (target.overlay == null)
            {
                var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                target.overlay = root;
                if (owner.scene.IsValid() && owner.scene.isLoaded) SceneManager.MoveGameObjectToScene(root, owner.scene);
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5000; // Opening/start/fade overlays stay above the hidden target.
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(402, 874);
                scaler.matchWidthOrHeight = .5f;
                var safe = new GameObject("Safe Area", typeof(RectTransform), typeof(GameSafeArea));
                safe.transform.SetParent(root.transform, false);
                var hit = new GameObject("Developer Triple Tap", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                    typeof(GameDeveloperTapForwarder));
                hit.transform.SetParent(safe.transform, false);
                RectTransform rect = (RectTransform)hit.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
                rect.pivot = new Vector2(.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(84, 24);
                target.hitImage = hit.GetComponent<UnityEngine.UI.Image>();
                hit.GetComponent<GameDeveloperTapForwarder>().target = target;
            }
            target.canEnter = canEnter;
            target.open = open;
            target.hitImage.color = Color.clear;
            target.ResetTaps();
            target.overlay.SetActive(target.isActiveAndEnabled);
            target.RefreshAvailability();
            return target;
        }

        private bool CanEnter => canEnter != null && open != null && canEnter();

        private void Update() => RefreshAvailability();

        private void RefreshAvailability()
        {
            bool available = CanEnter;
            if (hitImage != null) hitImage.raycastTarget = available;
            if (!available || tapCount > 0 && Time.realtimeSinceStartupAsDouble - firstTapAt > TapWindowSeconds)
                ResetTaps();
        }

        // Only the dedicated transparent hit area forwards clicks here. Implementing IPointerClickHandler
        // on the owner would make unrelated background graphics bubble their clicks into the gesture.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !CanEnter)
            {
                ResetTaps();
                return;
            }
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
            {
                ResetTaps();
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (tapCount == 0 || now - firstTapAt > TapWindowSeconds)
            {
                tapCount = 0;
                firstTapAt = now;
            }
            if (++tapCount < RequiredTaps) return;
            ResetTaps();
            open.Invoke();
        }

        private void OnEnable()
        {
            if (overlay != null) overlay.SetActive(true);
            RefreshAvailability();
        }

        private void OnDisable()
        {
            ResetTaps();
            if (overlay != null) overlay.SetActive(false);
        }

        private void OnDestroy()
        {
            if (overlay == null) return;
            if (Application.isPlaying) Destroy(overlay);
            else DestroyImmediate(overlay);
        }

        public void ResetTaps() { tapCount = 0; firstTapAt = 0; }
    }

    internal sealed class GameDeveloperTapForwarder : MonoBehaviour, IPointerClickHandler
    {
        internal GameDeveloperTapTarget target;
        public void OnPointerClick(PointerEventData eventData) => target?.OnPointerClick(eventData);
    }
}
