using System;
using System.Collections;
using UnityEngine;

namespace Hapio
{
    /// <summary>The title's first Start opens the local story; Home owns the continuation to rooms.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameOpeningController : MonoBehaviour
    {
        public StoryPlayer player;
        [Tooltip("The independent opening Canvas. Keep this controller and the lobby outside this object.")]
        public GameObject openingRoot;
        public GameLobbyView lobbyView;

        private static bool openingConsumed;
        private static GameOpeningController current;
        private bool presenting;
        private bool subscribed;
        private Coroutine pendingStart;
        private Coroutine pendingReturn;
        private Action continuation;
        // #635: the hand-over to the tutorial (or the room list) happens under the persistent clouds.
        private bool clouds, cloudsClosing;
        private Coroutine zooming;
        private RectTransform zoomHost;
        private Vector3 zoomHostPosition, zoomHostScale;
        private GameObject hiddenToolbar;
        private GameObject hiddenNavigationToolbar;
        private float hiddenDialogueAlpha = -1f;

        public bool IsPlaying => presenting && player != null && player.IsPlaying;
        public static bool HasPlayedThisRun => openingConsumed;
        public static bool IsPresenting => current != null && current.presenting;
        /// <summary>#629: the player of the opening on screen now, for its music; null when none is shown.</summary>
        public static StoryPlayer PresentingPlayer => IsPresenting ? current.player : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRunState()
        {
            openingConsumed = false;
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PrepareLoadedHome()
        {
            // Also runs when Editor Play is restarted with both domain and scene reload disabled.
            foreach (GameOpeningController controller in UnityEngine.Object.FindObjectsByType<GameOpeningController>())
                if (controller.isActiveAndEnabled) controller.PrepareHome();
        }

        private void Awake() => PrepareHome();

        private void OnEnable()
        {
            if (lobbyView != null) lobbyView.RegisterOpening(this);
        }

        private void PrepareHome()
        {
            if (player != null) { player.playOnStart = false; player.hideProgress = true; }
            EndPresentation();
            if (lobbyView != null) lobbyView.RegisterOpening(this);
        }

        /// <summary>True means this click is owned by the opening, including its failure fallback.</summary>
        public bool TryBeginFromStartButton(Action afterOpening)
        {
            if (C6Lab.LabGameBuildVariant.IsStaffStageDemo || !isActiveAndEnabled) return false;
            if (presenting) return true;
            // The exhibition build gives every new party the opening when they press Start again.
            // The normal game's once-per-app rule remains independent of that build choice.
            if (openingConsumed && !C6Lab.LabGameBuildVariant.IsYaguangDemo) return false;

            // An interrupted or invalid attempt is still consumed, so Home cannot get stuck in a retry loop.
            openingConsumed = true;
            continuation = afterOpening;
            current = this;
            presenting = true;
            if (lobbyView != null) lobbyView.SetOpeningBlocked(true);
            // A completed party may leave the title's background browser running (#476). The demo
            // replays its opening for the next party; room discovery starts again at its hand-over.
            if (C6Lab.LabGameBuildVariant.IsYaguangDemo && GameSession.Instance != null)
                GameSession.Instance.Network.CancelRoomSearch();
            if (!ValidateBindings(out string error))
            {
                Unavailable(error);
                return true;
            }

            try
            {
                Unsubscribe();
                player.playOnStart = false;
                player.hideProgress = true;
                // Re-enter the child lifecycle cleanly when Editor scene reload is disabled as well.
                openingRoot.SetActive(false);
                openingRoot.SetActive(true);
                pendingStart = StartCoroutine(StartAfterRelease());
            }
            catch (Exception exception)
            {
                Unavailable(exception.Message);
            }
            return true;
        }

        private IEnumerator StartAfterRelease()
        {
            // The title click cannot also advance the first page, even with a held touch or key submit.
            yield return null;
            while (StoryTapAdvance.HasHeldPointer) yield return null;
            pendingStart = null;
            try
            {
                player.Finished += OnFinished;
                subscribed = true;
                if (!player.isActiveAndEnabled || !player.Play(player.sequence))
                    Unavailable("The saved opening player could not start.");
            }
            catch (Exception exception)
            {
                Unavailable(exception.Message);
            }
        }

        private bool ValidateBindings(out string error)
        {
            error = "The saved opening requires its player, independent Canvas and Home lobby.";
            if (player == null || openingRoot == null || lobbyView == null || !CanHideOpeningRoot
                || !player.transform.IsChildOf(openingRoot.transform)
                || lobbyView.transform.IsChildOf(openingRoot.transform)) return false;
            error = "The saved opening requires a sequence and cut host.";
            if (player.sequence == null || player.cutHost == null) return false;
            return player.sequence.Validate(out error);
        }

        private bool CanHideOpeningRoot => CanHideRoot(openingRoot);

        private bool CanHideRoot(GameObject target) => target != null
            && !transform.IsChildOf(target.transform)
            && (lobbyView == null || !lobbyView.transform.IsChildOf(target.transform));

        private void Unavailable(string error)
        {
            Debug.LogWarning("HAPIO_OPENING_UNAVAILABLE: " + error, this);
            EndPresentation(continueToRooms: true);
        }

        private void OnFinished(StoryExitReason reason)
        {
            if (pendingReturn != null) return;
            CoverWithClouds(zoom: reason == StoryExitReason.Completed);
            pendingReturn = StartCoroutine(ReturnAfterRelease());
        }

        private IEnumerator ReturnAfterRelease()
        {
            // Keep the opening as an input shield through the release that finished/skipped it, and (#635) until the
            // clouds have wholly covered it: what comes next is only put behind them.
            yield return null;
            while (StoryTapAdvance.HasHeldPointer || cloudsClosing && CloudsUp) yield return null;
            pendingReturn = null;
            EndPresentation(continueToRooms: true);
        }

        private static bool CloudsUp => GameSession.Instance != null && GameSession.Instance.CloudsUp;

        /// <summary>
        /// #635: the clouds close over the opening. Seen to its end, the last picture first drops its words and buttons and
        /// zooms toward the valley as they close; skipped, the clouds simply close over whatever is on the screen.
        /// Without a session or its clouds the opening hands over as before.
        /// </summary>
        private void CoverWithClouds(bool zoom)
        {
            GameCloudCover cover = GameSession.Instance != null ? GameSession.Instance.CloudCover : null;
            if (cover == null || continuation == null) return;
            cloudsClosing = true;
            if (!cover.Close(() => cloudsClosing = false))
            {
                cloudsClosing = false;
                return;
            }
            clouds = true;
            if (!zoom || player == null) return;
            HideReadingControls();
            GameOpeningTransitionArt art = GameOpeningTransitionArt.Load();
            if (art != null && player.cutHost != null) zooming = StartCoroutine(ZoomTowardFocus(art, player.cutHost));
        }

        private void HideReadingControls()
        {
            if (player.CurrentView != null && player.CurrentView.authoredDialogue != null)
                player.CurrentView.authoredDialogue.alpha = 0;
            if (player.dialogueGroup != null)
            {
                hiddenDialogueAlpha = player.dialogueGroup.alpha;
                player.dialogueGroup.alpha = 0f;
            }
            Transform toolbar = player.skipButton != null ? player.skipButton.transform.parent : null;
            if (toolbar != null && openingRoot != null && toolbar != openingRoot.transform && toolbar.IsChildOf(openingRoot.transform)
                && toolbar.gameObject.activeSelf)
            {
                hiddenToolbar = toolbar.gameObject;
                hiddenToolbar.SetActive(false);
            }
            // The redesigned skip lives on a fitted overlay; also hide the original navigation during the final zoom.
            var navigation = openingRoot != null ? openingRoot.transform.Find("Safe Controls/Toolbar") : null;
            if (navigation != null && navigation != toolbar && navigation.gameObject.activeSelf)
            {
                hiddenNavigationToolbar = navigation.gameObject;
                hiddenNavigationToolbar.SetActive(false);
            }
        }

        private void RestoreReadingControls()
        {
            if (hiddenDialogueAlpha >= 0f && player != null && player.dialogueGroup != null) player.dialogueGroup.alpha = hiddenDialogueAlpha;
            hiddenDialogueAlpha = -1f;
            if (hiddenToolbar != null) hiddenToolbar.SetActive(true);
            hiddenToolbar = null;
            if (hiddenNavigationToolbar != null) hiddenNavigationToolbar.SetActive(true);
            hiddenNavigationToolbar = null;
        }

        private IEnumerator ZoomTowardFocus(GameOpeningTransitionArt art, RectTransform host)
        {
            zoomHost = host;
            zoomHostPosition = host.localPosition;
            zoomHostScale = host.localScale;
            Rect page = host.rect;
            Vector2 focus = art.FocusFromMiddle(page.size);
            float baseScale = zoomHostScale.x, seconds = 0f;
            while (true)
            {
                float zoom = art.ZoomAt(seconds);
                (float scale, Vector2 move) = GameOpeningTransitionArt.ZoomPose(zoom, focus, page.size, art.zoomScale, art.focusToCenter);
                host.localScale = zoomHostScale * scale;
                // Grow about the page's middle (the host's pivot is its top-left corner), then move toward the focus.
                host.localPosition = zoomHostPosition + (Vector3)((page.center * (1f - scale) + move) * baseScale);
                if (zoom >= 1f) break;
                yield return null;
                seconds += Time.unscaledDeltaTime;
            }
            zooming = null;
        }

        private void StopZoom()
        {
            if (zooming != null) StopCoroutine(zooming);
            zooming = null;
            if (zoomHost != null)
            {
                zoomHost.localScale = zoomHostScale;
                zoomHost.localPosition = zoomHostPosition;
            }
            zoomHost = null;
        }

        private void EndPresentation(bool continueToRooms = false)
        {
            if (pendingStart != null) StopCoroutine(pendingStart);
            pendingStart = null;
            if (pendingReturn != null) StopCoroutine(pendingReturn);
            pendingReturn = null;
            Action next = continueToRooms ? continuation : null;
            continuation = null;
            presenting = false;
            if (current == this) current = null;
            bool coveredByClouds = clouds;
            clouds = cloudsClosing = false;
            Unsubscribe();
            StopZoom();
            RestoreReadingControls();
            if (player != null) player.Cancel();
            HideOpeningRoot();
            try { next?.Invoke(); }
            finally
            {
                // #635: what came next lifts the clouds once it can be seen (the tutorial once playable, the room list at
                // once). When nothing comes next (the opening was cut short) they go at once.
                GameSession session = GameSession.Instance;
                if (coveredByClouds && session != null)
                {
                    if (next != null) session.LiftCloudsOnceShown();
                    else if (session.CloudCover != null) session.CloudCover.Cancel();
                }
            }
            if (lobbyView != null) lobbyView.SetOpeningBlocked(false);
        }

        private void Unsubscribe()
        {
            if (subscribed && player != null) player.Finished -= OnFinished;
            subscribed = false;
        }

        private void HideOpeningRoot()
        {
            if (CanHideOpeningRoot)
            {
                openingRoot.SetActive(false);
                return;
            }
            // A misbound Home Canvas must remain available for the fallback menu. Only close
            // the player's actual independent Canvas when it cannot contain the lobby/controller.
            Canvas canvas = player != null ? player.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null && CanHideRoot(canvas.gameObject)) canvas.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (presenting) openingConsumed = true;
            EndPresentation();
            if (lobbyView != null) lobbyView.UnregisterOpening(this);
        }
    }
}
