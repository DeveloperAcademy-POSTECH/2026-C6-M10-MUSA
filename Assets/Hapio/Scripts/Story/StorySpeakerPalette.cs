using System;
using UnityEngine;

namespace Hapio
{
    [CreateAssetMenu(menuName = "Hapio/Story/Speaker Palette", fileName = "SpeakerPalette")]
    public sealed class StorySpeakerPalette : ScriptableObject
    {
        [Serializable] public sealed class Style
        {
            [InspectorName("화자")] public string speaker;
            [InspectorName("다른 표기")] public string[] aliases;
            [InspectorName("이름표 배경색")] public Color badgeColor = Color.white;
            [InspectorName("이름 글자색")] public Color textColor = Color.white;
        }

        [InspectorName("공통 이름표 재질")] public Material badgeMaterial;
        [InspectorName("캐릭터별 색상")] public Style[] styles = new Style[0];

        public bool TryGet(string speaker, out Style style)
        {
            style = null;
            if (string.IsNullOrWhiteSpace(speaker) || styles == null) return false;
            string key = speaker.Trim();
            foreach (var entry in styles)
            {
                if (entry == null) continue;
                if (string.Equals(entry.speaker, key, StringComparison.Ordinal)) { style = entry; return true; }
                if (entry.aliases == null) continue;
                foreach (var alias in entry.aliases)
                    if (string.Equals(alias, key, StringComparison.Ordinal)) { style = entry; return true; }
            }
            return false;
        }
    }
}
