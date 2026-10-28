using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    [CreateAssetMenu(menuName = "Hapio/Story/Sequence", fileName = "StorySequence")]
    public sealed class StorySequence : ScriptableObject
    {
        [Tooltip("Stable identifier, independent of the display name or cut order.")]
        public string storyId;
        public List<StoryCutDefinition> cuts = new List<StoryCutDefinition>();

        public bool Validate(out string error)
        {
            if (cuts == null || cuts.Count == 0)
            {
                error = "The story needs at least one cut.";
                return false;
            }
            var ids = new HashSet<string>();
            for (int i = 0; i < cuts.Count; i++)
            {
                var cut = cuts[i];
                if (cut == null || cut.viewPrefab == null || string.IsNullOrWhiteSpace(cut.cutId))
                {
                    error = $"Cut {i + 1} needs an ID and a view prefab.";
                    return false;
                }
                if (!ids.Add(cut.cutId))
                {
                    error = $"Duplicate cut ID: {cut.cutId}";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
