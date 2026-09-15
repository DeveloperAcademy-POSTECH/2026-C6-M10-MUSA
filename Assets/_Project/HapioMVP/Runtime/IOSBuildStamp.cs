using UnityEngine;

namespace C6.Prototype
{
    [CreateAssetMenu(menuName = "C6/iOS Build Stamp", fileName = "IOSBuildStamp")]
    public sealed class IOSBuildStamp : ScriptableObject
    {
        public string BuildId = "T01-A";
        public string Revision = "1";
    }
}
