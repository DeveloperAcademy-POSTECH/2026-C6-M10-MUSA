using C6.Prototype.Presentation;
using UnityEngine;
namespace C6.Prototype.Attack
{
    [DisallowMultipleComponent]
    public sealed class AttackLaunchFrame : MonoBehaviour
    {
        [SerializeField] private ScreenLayoutConfig config;
        public void Configure(ScreenLayoutConfig source) => config = source;
        public ProjectileLaunchBasis Basis => new ProjectileLaunchBasis(config.LaunchOrigin, Vector3.right, config.LaunchWidth, config.LaunchAim);
    }
}
