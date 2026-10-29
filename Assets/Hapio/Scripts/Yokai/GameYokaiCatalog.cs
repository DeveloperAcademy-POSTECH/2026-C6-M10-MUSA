using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// Authoring inventory of all yokai content, including planned entries. It neither
    /// chooses battle order nor installs new rules in the existing three-stage run.
    /// </summary>
    [CreateAssetMenu(menuName = "Hapio/Yokai/Catalog")]
    public sealed class GameYokaiCatalog : ScriptableObject
    {
        public GameYokaiDefinition[] entries = Array.Empty<GameYokaiDefinition>();

        public void Validate()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("The yokai content catalog must contain at least one definition.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var referenceOrders = new HashSet<int>();
            foreach (GameYokaiDefinition definition in entries)
            {
                if (definition == null)
                    throw new InvalidOperationException("The yokai content catalog contains a missing definition.");
                definition.Validate();
                if (!ids.Add(definition.contentId))
                    throw new InvalidOperationException("Yokai content IDs must be unique: " + definition.contentId);
                if (!referenceOrders.Add(definition.referenceOrder))
                    throw new InvalidOperationException("Yokai reference orders must be unique: " + definition.referenceOrder);
            }
        }

        public bool TryGet(string contentId, out GameYokaiDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(contentId)) return false;
            Validate();
            foreach (GameYokaiDefinition candidate in entries)
            {
                if (!string.Equals(candidate.contentId, contentId, StringComparison.Ordinal)) continue;
                definition = candidate;
                return true;
            }
            return false;
        }

        public GameYokaiDefinition Get(string contentId)
        {
            if (TryGet(contentId, out GameYokaiDefinition definition)) return definition;
            throw new KeyNotFoundException("No yokai content definition has ID: " + contentId);
        }
    }
}
