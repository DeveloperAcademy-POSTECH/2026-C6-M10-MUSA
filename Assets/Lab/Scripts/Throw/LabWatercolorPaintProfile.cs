using UnityEngine;

namespace C6Lab
{
    /// <summary>Presentation-only authoring asset, separate from the Host-owned LabConfig gameplay values.</summary>
    [CreateAssetMenu(menuName = "C6 Lab/Watercolor Paint Profile")]
    public sealed class LabWatercolorPaintProfile : ScriptableObject
    {
        public LabWatercolorPaintSettings settings = new LabWatercolorPaintSettings();
    }
}
