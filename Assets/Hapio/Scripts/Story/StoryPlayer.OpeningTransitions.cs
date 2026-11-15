using UnityEngine;

namespace Hapio
{
    public sealed partial class StoryPlayer
    {
        private StoryCutView outgoingView;
        private CanvasGroup outgoingGroup, fadingDialogue;
        private float blendElapsed, blendSeconds, dialogueFadeElapsed;
        private bool focusGourd;
        private Vector3 outgoingScale;
        private Vector2 outgoingPosition;

        private void PrepareOpeningEntrance(StoryCutView view, StoryCutView previous)
        {
            if (sequence == null || !sequence.smoothOpeningTransitions) return;
            view.minimumEntranceDuration = view.sourceFigmaNumber == 4 ? .55f : .32f;
            // Ordinary paper layers settle close to their authored position. Story actions retain their timing.
            bool cinematic = view.sourceFigmaNumber >= 14 && view.sourceFigmaNumber <= 16;
            foreach (var motion in view.openingMotions ?? System.Array.Empty<OpeningLayerMotion>())
            {
                if (motion == null) continue;
                if (motion.kind == OpeningLayerMotion.Kind.Reveal && !cinematic)
                {
                    motion.delay = 0;
                    motion.seconds = .32f;
                    motion.travel = Vector2.ClampMagnitude(motion.travel, 3f);
                }
                if (motion.kind == OpeningLayerMotion.Kind.Drop && previous != null && previous.sourceFigmaNumber == 14)
                    foreach (var source in previous.openingMotions ?? System.Array.Empty<OpeningLayerMotion>())
                        if (source != null && source.kind == OpeningLayerMotion.Kind.Teeter) motion.CarryDropPose(source);
            }
        }

        private bool StartOpeningBlend(StoryCutView previous)
        {
            if (sequence == null || !sequence.smoothOpeningTransitions || previous == null || activeCloudWipe != null)
                return false;
            ClearOpeningBlend();
            outgoingView = previous;
            outgoingGroup = previous.GetComponent<CanvasGroup>();
            if (outgoingGroup == null) outgoingGroup = previous.gameObject.AddComponent<CanvasGroup>();
            outgoingGroup.alpha = 1;
            outgoingGroup.blocksRaycasts = outgoingGroup.interactable = false;
            // The old, opaque picture covers the fully rendered new one: no fade through the white backing.
            previous.transform.SetAsLastSibling();
            blendElapsed = 0;
            focusGourd = previous.sourceFigmaNumber == 3 && currentView.sourceFigmaNumber == 4;
            blendSeconds = focusGourd ? .55f : currentView.sourceFigmaNumber == 15 ? .18f : .32f;
            var rect = (RectTransform)previous.transform;
            outgoingScale = rect.localScale;
            outgoingPosition = rect.anchoredPosition;
            return true;
        }

        private void AdvanceOpeningBlend(float seconds)
        {
            if (outgoingView == null) return;
            blendElapsed += seconds;
            float t = Mathf.Clamp01(blendElapsed / blendSeconds);
            float ease = Mathf.SmoothStep(0, 1, t);
            outgoingGroup.alpha = 1 - Mathf.SmoothStep(0, 1, focusGourd ? Mathf.InverseLerp(.65f, 1, t) : t);
            if (outgoingView.authoredDialogue != null)
                outgoingView.authoredDialogue.alpha = 1 - Mathf.Clamp01(blendElapsed / .12f);
            if (focusGourd)
            {
                // Figma 3 -> 4 uses the same 402x874 background enlarged to 778x1691, x=-376/y=0.
                var rect = (RectTransform)outgoingView.transform;
                rect.localScale = Vector3.Scale(outgoingScale, Vector3.one * Mathf.Lerp(1, 778f / 402f, ease));
                rect.anchoredPosition = outgoingPosition + new Vector2(-188, -408.5f) * ease;
            }
            if (t >= 1) ClearOpeningBlend();
        }

        private void ClearOpeningBlend()
        {
            var old = outgoingView;
            outgoingView = null;
            outgoingGroup = null;
            // Do not use ReleaseView: finishing an overlap must not stop the new cut's voice.
            if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        }

        private void AdvanceDialogueFade(float seconds)
        {
            if (fadingDialogue == null) return;
            dialogueFadeElapsed += seconds;
            fadingDialogue.alpha = Mathf.SmoothStep(0, 1, dialogueFadeElapsed / .16f);
            if (dialogueFadeElapsed >= .16f) fadingDialogue = null;
        }
    }
}
