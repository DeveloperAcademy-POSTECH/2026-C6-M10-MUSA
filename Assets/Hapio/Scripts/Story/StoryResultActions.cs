using UnityEngine;
using UnityEngine.Events;
namespace Hapio
{
    /// <summary>Integration points for the owner of the stage/result flow.</summary>
    public sealed class StoryResultActions : MonoBehaviour
    {
        public UnityEvent onInspectGourd = new UnityEvent();
        public UnityEvent onRetry = new UnityEvent();
        public UnityEvent onExit = new UnityEvent();
        public void InspectGourd() => onInspectGourd.Invoke();
        public void Retry() => onRetry.Invoke();
        public void Exit() => onExit.Invoke();
    }
}
