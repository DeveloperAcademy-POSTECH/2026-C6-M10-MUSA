using UnityEngine;

namespace Hapio
{
    [DisallowMultipleComponent]
    public sealed class StoryCutView : MonoBehaviour
    {
        public StoryLayer[] layers;
        public StoryVisualBeat[] beats;
        public UnityEngine.UI.Button continueButton;
        [Header("Optional music for this cut")]
        public AudioClip music;
        [Range(0, 1)] public float musicVolume;
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
                if (beats != null)
                    foreach (var beat in beats)
                        if (beat != null) duration = Mathf.Max(duration, beat.Duration);
                return duration;
            }
        }

        public void Sample(float elapsed)
        {
            if (layers != null)
                foreach (var layer in layers)
                    if (layer != null) layer.Sample(elapsed);
            if (beats != null)
                foreach (var beat in beats)
                    if (beat != null) beat.Sample(elapsed);
        }
    }
}
