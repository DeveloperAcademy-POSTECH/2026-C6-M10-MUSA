using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>
    /// 장면이 바뀌어도 남는 한지 구름 덮개 (#635). #630의 구름(StoryCloudWipe)으로 화면을 덮고, 다 덮이면 알려 준 뒤 덮인 채로
    /// 기다리다가, <see cref="Open"/>을 들으면 걷힌다. GameSession 아래에 있어 Home에서 전투로 넘어가는 동안에도 사라지지 않는다.
    /// - 덮이는 동안·덮여 있는 동안·걷히는 동안 아래 화면의 터치를 모두 막는다. 쉬는 동안에는 꺼져 있어 아무것도 막지 않는다.
    /// - 장면 전환 페이드(30100) 위, 로딩 화면(30150) 아래에 그린다. 터치 먹물(30050)은 구름 아래다.
    /// - 시간은 실제 시간으로 흐른다(일시정지·느린 화면과 무관).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCloudCover : MonoBehaviour
    {
        /// <summary>Over the scene fade (30100), the start cover (30000) and the tutorial (20000); under the loading screen (30150).</summary>
        public const int SortingOrder = 30120;

        private StoryCloudWipe wipe;
        private Action whenCovered;
        private bool openAsked;

        /// <summary>The clouds are on the screen: closing, covering or lifting.</summary>
        public bool IsUp => wipe != null && wipe.IsPlaying;
        /// <summary>The screen is wholly covered (and stays so until <see cref="Open"/>, then while the clouds begin to lift).</summary>
        public bool IsCovered => IsUp && wipe.HasCovered;
        /// <summary>Covered and waiting to be told to lift.</summary>
        public bool IsHolding => IsCovered && !openAsked;
        /// <summary>Told to lift: the clouds go as soon as the screen has been covered.</summary>
        public bool IsOpening => IsUp && openAsked;
        /// <summary>Seconds for the clouds to cover the screen.</summary>
        public float CloseSeconds => wipe != null ? wipe.SwapTime : 0f;
        /// <summary>Seconds from the first cloud to the last one gone when nothing waits in between.</summary>
        public float Duration => wipe != null ? wipe.Duration : 0f;

        public static GameCloudCover Create(Transform parent, GameCloudCoverArt art = null)
        {
            art = art != null ? art : GameCloudCoverArt.Load();
            if (art == null || art.cloudWipe == null)
            {
                Debug.LogWarning("HAPIO_CLOUD_COVER_ART_MISSING: Resources/" + GameCloudCoverArt.ResourcePath);
                return null;
            }
            var go = new GameObject("GameCloudCover", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // Built switched off: the clouds wake (and fit the screen) only when asked to cover it.
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = .5f;
            var cover = go.AddComponent<GameCloudCover>();
            cover.wipe = Instantiate(art.cloudWipe, go.transform, false);
            var rect = (RectTransform)cover.wipe.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return cover;
        }

        /// <summary>
        /// Starts covering the screen. <paramref name="onCovered"/> runs once the screen is wholly covered; the clouds then
        /// stay until <see cref="Open"/>. False when the clouds are already up or cannot play (the caller goes on without them).
        /// </summary>
        public bool Close(Action onCovered)
        {
            if (wipe == null || IsUp) return false;
            whenCovered = onCovered;
            openAsked = false;
            gameObject.SetActive(true);
            if (wipe.BeginManual(Covered)) return true;
            Hide();
            return false;
        }

        /// <summary>Lifts the clouds: now if the screen is covered, or straight after it has been.</summary>
        public void Open()
        {
            if (IsUp) openAsked = true;
        }

        /// <summary>Takes the clouds away at once without telling anyone (the host is leaving).</summary>
        public void Cancel()
        {
            whenCovered = null;
            if (wipe != null) wipe.Cancel();
            Hide();
        }

        private void Covered()
        {
            Action next = whenCovered;
            whenCovered = null;
            next?.Invoke();
        }

        private void Update()
        {
            if (!IsUp) { Hide(); return; }
            // Wholly covered and not yet told to lift: hold here, however long the next screen takes.
            if (IsHolding) return;
            wipe.Advance(Time.unscaledDeltaTime);
            if (!wipe.IsPlaying) Hide();
        }

        private void Hide()
        {
            openAsked = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
