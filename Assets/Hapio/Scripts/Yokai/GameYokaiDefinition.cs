using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    public enum GameYokaiImplementationState
    {
        Planned = 0,
        InDevelopment = 1,
        Integrated = 2
    }

    /// <summary>
    /// Content identity and optional presentation references. This is not a battle rule,
    /// a stage number, or permission to add an unfinished yokai to the active run.
    /// </summary>
    [CreateAssetMenu(menuName = "Hapio/Yokai/Definition")]
    public sealed class GameYokaiDefinition : ScriptableObject
    {
        [Tooltip("Stable lowercase content ID. Independent of Figma order and battle stage numbers.")]
        public string contentId;
        public string displayName;
        public string[] aliases = Array.Empty<string>();
        [Min(1), Tooltip("Reference order in the source design, not the battle sequence.")]
        public int referenceOrder;
        [TextArea(3, 10)] public string description;
        public string referenceUrl;
        public bool isBoss;
        public GameYokaiImplementationState implementationState = GameYokaiImplementationState.Planned;

        [Tooltip("Optional authoring reference; it does not replace the current stage model automatically.")]
        public GameObject visualPrefab;
        public Sprite portrait;
        public AudioClip[] audioClips = Array.Empty<AudioClip>();
        public GameObject[] effects = Array.Empty<GameObject>();

        /// <summary>Content status only. The existing active StageCatalog remains the run authority.</summary>
        public bool IsIntegrated => implementationState == GameYokaiImplementationState.Integrated;

        public void Validate()
        {
            if (!ValidContentId(contentId))
                throw new InvalidOperationException("Yokai content ID must match [a-z][a-z0-9_]*.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException("Yokai display name is required: " + contentId);
            if (referenceOrder < 1)
                throw new InvalidOperationException("Yokai reference order must be positive: " + contentId);
            if (!Enum.IsDefined(typeof(GameYokaiImplementationState), implementationState))
                throw new InvalidOperationException("Unknown yokai implementation state: " + contentId);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { displayName.Trim() };
            foreach (string alias in aliases ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(alias) || !names.Add(alias.Trim()))
                    throw new InvalidOperationException("Yokai aliases must be nonempty and unique, including the display name: " + contentId);
            }
            // Descriptions and art can intentionally be absent while authoring. Existing
            // integrated stages also retain their shared model until individual art is ready.
        }

        private static bool ValidContentId(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z') return false;
            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            }
            return true;
        }
    }
}
