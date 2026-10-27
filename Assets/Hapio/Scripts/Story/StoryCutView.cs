using UnityEngine;

namespace Hapio
{
    [DisallowMultipleComponent]
    public sealed class StoryCutView : MonoBehaviour
    {
        public StoryLayer[] layers;
        [Tooltip("Hide the dialogue panel on pages with no speaker or text.")]
        public bool hideEmptyDialogue;
        [Header("Source reference (Figma frame number)")]
        public int sourceFigmaNumber;
        public string sourceFigmaNodeId;
        [Header("Optional dialogue layout for this cut (402 x 874 coordinates)")]
        public bool overrideDialogueLayout;
        [Tooltip("Optional cut-specific text size; zero uses the player default.")]
        [Min(0)] public float dialogueFontSize;
        public Rect dialogueRect = new Rect(84, 740, 259, 56);
        public Rect speakerRect = new Rect(84, 721, 36, 18);
        public Sprite speakerBadge;
        public Rect speakerBadgeRect = new Rect(57, 713, 91, 33);
        public bool centerSpeakerName;
        public Color speakerColor = Color.white;
        public float EntranceDuration
        {
            get
            {
                float duration = 0f;
                if (layers != null)
                    foreach (var layer in layers)
                        if (layer != null) duration = Mathf.Max(duration, layer.EntranceDuration);
                return duration;
            }
        }

        public void Sample(float elapsed)
        {
            if (layers == null) return;
            foreach (var layer in layers)
                if (layer != null) layer.Sample(elapsed);
        }
    }
}
