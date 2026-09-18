using UnityEngine;

namespace C6.Prototype.Presentation
{
    /// <summary>
    /// #4 orb artwork. Optional sprites shown instead of the generated circle layers.
    /// An empty slot keeps the previous generated artwork for that orb type.
    /// Sprites should be square with the orb circle filling the canvas (circle edge = collider edge).
    /// </summary>
    [CreateAssetMenu(menuName = "C6/Presentation/Orb Art Set", fileName = "OrbArtSet")]
    public sealed class OrbArtSet : ScriptableObject
    {
        [SerializeField] private Sprite yinSprite;
        [SerializeField] private Sprite yangSprite;
        [SerializeField] private Sprite combinedSprite;
        [Tooltip("Hide the YIN / YANG / COMB caption under orbs that use artwork. LOCKED is still shown.")]
        [SerializeField] private bool hideLabels = true;

        public Sprite YinSprite => yinSprite;
        public Sprite YangSprite => yangSprite;
        public Sprite CombinedSprite => combinedSprite;
        public bool HideLabels => hideLabels;

        public Sprite SpriteFor(bool combined, bool yin) => combined ? combinedSprite : yin ? yinSprite : yangSprite;
    }
}
