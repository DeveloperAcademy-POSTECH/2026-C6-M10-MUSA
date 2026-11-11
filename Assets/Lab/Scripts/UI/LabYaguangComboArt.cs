using UnityEngine;

namespace C6Lab
{
    /// <summary>#668: authored art only. Combo sequence and judging remain in LabGourdCombo.</summary>
    public static class LabYaguangComboArt
    {
        public const string Path = "Yaguang/Combo/";
        public static readonly Rect PanelReference = new Rect(71f, 107f, 207f, 51f);
        public static Vector2 PanelAnchor(Vector2 viewport)
        {
            float fit = Fit(viewport);
            Vector2 center = PanelReference.center;
            return new Vector2(.5f + (center.x - 349f * .5f) * fit / Mathf.Max(1f, viewport.x),
                .5f + (765f * .5f - center.y) * fit / Mathf.Max(1f, viewport.y));
        }
        public static Rect OverGauge(Rect gauge)
        {
            float width = gauge.width * .98f;
            Vector2 size = new Vector2(width, width * 51f / 207f);
            return new Rect(gauge.center - size * .5f, size);
        }
        public static Sprite Load(string name) => Resources.Load<Sprite>(Path + name);
        public static Sprite Orb(LabOrbElement element)
        {
            switch (element)
            {
                case LabOrbElement.Fire: return Load("Fire");
                case LabOrbElement.Water: return Load("Water");
                case LabOrbElement.Wood: return Load("Wood");
                case LabOrbElement.Metal: return Load("Metal");
                case LabOrbElement.Earth: return Load("Earth");
                default: return null;
            }
        }
        public static string CountPath(string word)
        {
            switch (word.TrimEnd('!'))
            {
                case "하나": return Path + "CountOne";
                case "둘": return Path + "CountTwo";
                case "셋": return Path + "CountThree";
                case "넷": return Path + "CountFour";
                default: return null;
            }
        }
        public static float Fit(Vector2 viewport) => Mathf.Max(.01f, Mathf.Min(viewport.x / 349f, viewport.y / 765f));
    }
}
