using UnityEngine;
namespace C6.Prototype.Orbs
{
    public interface IOrbPointerSink
    {
        bool BeginPointer(int pointerId, Vector2 rawPosition, bool startedOverUi);
        void MovePointer(int pointerId, Vector2 rawPosition);
        void EndPointer(int pointerId, Vector2 rawPosition);
        void CancelPointer(int pointerId);
        void CancelInteractions(string reason);
    }
}
