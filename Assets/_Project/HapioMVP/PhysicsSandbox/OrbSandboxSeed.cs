using C6.Prototype.Orbs;
using UnityEngine;

namespace C6.Prototype.PhysicsSandbox
{
    /// <summary>A stable, preplaced scene object. Preview artwork is replaced by the game's OrbView on Play.</summary>
    [DisallowMultipleComponent]
    public sealed class OrbSandboxSeed : MonoBehaviour
    {
        [Range(0, 1)] public int initialBoard;
        public OrbPolarity polarity;
        public Transform previewArtwork;
        public OrbView View { get; internal set; }
        public int CurrentBoard { get; internal set; }
    }
}
