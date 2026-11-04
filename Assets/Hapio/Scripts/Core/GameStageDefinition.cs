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
        [Tooltip("Optional decorative model. It never replaces the paint mesh or hit collider.")]
        public GameObject yokaiVisual;
        [Tooltip("#507 이 스테이지 박 프리팹의 Resources 경로(확장자 없이). 비우면 흰 박이다. 프리팹을 직접 참조하면 "
            + "StageCatalog를 읽을 때 모든 스테이지의 모델이 메모리에 올라가므로 경로로 둔다.")]
        public string gourdVisualResource = string.Empty;
        public GameStageValue[] defaultValues = Array.Empty<GameStageValue>();
    }
}
