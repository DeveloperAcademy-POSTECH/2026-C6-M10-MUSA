using System;
using UnityEngine;

namespace C6.Prototype.Attack
{
    /// <summary>Identifies the fixed training target. HP remains in the host authority service.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterHitTarget : MonoBehaviour
    {
        [SerializeField] private string targetId = "t06-training-target";
        public string TargetId => targetId;

        public void Configure(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A target ID is required.", nameof(id));
            targetId = id;
        }
    }
}
