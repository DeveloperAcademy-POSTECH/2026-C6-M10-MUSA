using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    [Serializable]
    public struct GameStageValue
    {
        public int index;
        public float value;
        public GameStageValue(int index, float value) { this.index = index; this.value = value; }
    }

    /// <summary>Only stage-specific defaults; all shared battle values originate in LabConfig.</summary>
    [CreateAssetMenu(menuName = "Hapio/Stage Definition")]
    public sealed class GameStageDefinition : ScriptableObject
    {
        public int stageIndex;
        public string stageId;
        public string displayName;
        public LabStageYokai yokai;
        [Tooltip("Optional content identity. Combat order, rules and tuning remain owned by this stage definition.")]
        public GameYokaiDefinition content;
        [Tooltip("Optional decorative model. It never replaces the paint mesh or hit collider.")]
        public GameObject yokaiVisual;
        public GameStageValue[] defaultValues = Array.Empty<GameStageValue>();
    }
}
