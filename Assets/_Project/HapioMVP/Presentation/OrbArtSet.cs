using System;
using UnityEngine;

namespace C6.Prototype.Presentation
{
    /// <summary>
    /// Orb artwork. Optional sprites shown instead of the generated circle layers.
    /// Element tables (오행) are looked up first; the three generic sprites are the fallback.
    /// An empty slot keeps the previous generated artwork for that orb type.
    /// Sprites should be square with the orb circle filling the canvas (circle edge = collider edge).
    /// </summary>
    [CreateAssetMenu(menuName = "C6/Presentation/Orb Art Set", fileName = "OrbArtSet")]
    public sealed class OrbArtSet : ScriptableObject
    {
        [Serializable]
        public sealed class ElementArt
        {
            public OrbElement element;
            public Sprite yin;
            public Sprite yang;
        }

        [Serializable]
        public sealed class CombinedArt
        {
            public OrbElement yinElement;
            public OrbElement yangElement;
            public Sprite sprite;
        }

        [Header("Fallback (no element / missing entry)")]
        [SerializeField] private Sprite yinSprite;
        [SerializeField] private Sprite yangSprite;
        [SerializeField] private Sprite combinedSprite;
        [Tooltip("Hide the YIN / YANG / COMB caption under orbs that use artwork. LOCKED is still shown.")]
        [SerializeField] private bool hideLabels = true;

        [Header("오행 (element) artwork")]
        [SerializeField] private ElementArt[] elements = Array.Empty<ElementArt>();
        [SerializeField] private CombinedArt[] combined = Array.Empty<CombinedArt>();

        public Sprite YinSprite => yinSprite;
        public Sprite YangSprite => yangSprite;
        public Sprite CombinedSprite => combinedSprite;
        public bool HideLabels => hideLabels;

        public Sprite SpriteFor(bool isCombined, bool yin) => isCombined ? combinedSprite : yin ? yinSprite : yangSprite;

        public Sprite RawSprite(OrbElement element, bool yin)
        {
            if (element != OrbElement.None && elements != null)
                foreach (var entry in elements)
                    if (entry != null && entry.element == element)
                    {
                        var sprite = yin ? entry.yin : entry.yang;
                        if (sprite != null) return sprite;
                    }
            return yin ? yinSprite : yangSprite;
        }

        public Sprite CombinedSpriteFor(OrbElement yinElement, OrbElement yangElement)
        {
            if (yinElement != OrbElement.None && yangElement != OrbElement.None && combined != null)
                foreach (var entry in combined)
                    if (entry != null && entry.yinElement == yinElement && entry.yangElement == yangElement && entry.sprite != null)
                        return entry.sprite;
            return combinedSprite;
        }
    }
}
