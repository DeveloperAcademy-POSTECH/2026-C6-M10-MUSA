using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Displays the result provided by the game. Rows follow the order of the printed card.</summary>
    public sealed class StoryCardRecords : MonoBehaviour
    {
        [Serializable]
        public struct Record
        {
            public string player;
            public string value;
        }

        public Text sealRate;
        public Text timeRemaining;
        public Text[] players;
        public Text[] values;

        public void Bind(float normalizedSealRate, float remainingSeconds, Record[] records)
        {
            float rate = float.IsNaN(normalizedSealRate) ? 0 : Mathf.Clamp01(normalizedSealRate);
            float time = float.IsNaN(remainingSeconds) ? 0 : Mathf.Max(0, remainingSeconds);
            sealRate.text = Mathf.RoundToInt(rate * 100) + "%";
            timeRemaining.text = float.IsInfinity(time) ? "—" : Mathf.CeilToInt(time) + "초";
            for (int i = 0; i < players.Length; i++)
            {
                var row = records != null && i < records.Length ? records[i] : default;
                players[i].text = string.IsNullOrEmpty(row.player) ? "—" : row.player;
                values[i].text = string.IsNullOrEmpty(row.value) ? "—" : row.value;
            }
        }
    }
}
