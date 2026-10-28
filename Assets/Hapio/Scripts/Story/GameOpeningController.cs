using System;
using System.Collections;
using UnityEngine;

namespace Hapio
{
    /// <summary>Local, once-per-app-run opening. Room and battle navigation stay with the existing Home screen.</summary>
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
        private Coroutine pendingReturn;

        public bool IsPlaying => presenting && player != null && player.IsPlaying;
        public static bool HasPlayedThisRun => openingConsumed;
        public static bool IsPresenting => current != null && current.presenting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRunState()
        {
            openingConsumed = false;
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginLoadedHome()
        {
            // Also runs when Editor Play is restarted with both domain and scene reload disabled.
            foreach (GameOpeningController controller in UnityEngine.Object.FindObjectsByType<GameOpeningController>())
                if (controller.isActiveAndEnabled) controller.BeginOpening();
        }

        private void Awake()
        {
            if (player != null) player.playOnStart = false;
            if (!openingConsumed)
            {
                current = this;
                presenting = true;
                if (lobbyView != null) lobbyView.SetOpeningBlocked(true);
            }
            else HideOpeningRoot();
        }

        private void Start() => BeginOpening();

        private void BeginOpening()
        {
            if (openingConsumed)
            {
                if (current != this)
                {
                    HideOpeningRoot();
                    if (lobbyView != null) lobbyView.SetOpeningBlocked(false);
                }
                return;
            }

            // An interrupted or invalid attempt is still consumed, so Home cannot get stuck in a retry loop.
            openingConsumed = true;
            current = this;
            presenting = true;
            if (lobbyView != null) lobbyView.SetOpeningBlocked(true);
            if (!ValidateBindings(out string error))
            {
                Unavailable(error);
                return;
            }

            try
            {
                Unsubscribe();
                player.playOnStart = false;
                // Re-enter the child lifecycle cleanly when Editor scene reload is disabled as well.
                openingRoot.SetActive(false);
                openingRoot.SetActive(true);
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
            EndPresentation();
        }

        private void OnFinished(StoryExitReason reason)
        {
            if (pendingReturn == null) pendingReturn = StartCoroutine(ReturnAfterRelease());
        }

        private IEnumerator ReturnAfterRelease()
        {
            // Keep the opening as an input shield through the release that finished/skipped it.
            yield return null;
            while (StoryTapAdvance.HasHeldPointer) yield return null;
            pendingReturn = null;
            EndPresentation();
        }

        private void EndPresentation()
        {
            if (pendingReturn != null) StopCoroutine(pendingReturn);
            pendingReturn = null;
            presenting = false;
            if (current == this) current = null;
            Unsubscribe();
            if (player != null) player.Cancel();
            HideOpeningRoot();
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
        }
    }
}
