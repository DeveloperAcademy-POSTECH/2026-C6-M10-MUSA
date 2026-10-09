using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>Direct local-IP gameplay requests iOS Local Network permission; Bonjour is unused.</summary>
    public static class LabIosPostprocess
    {
        [PostProcessBuild(100)]
        public static void AddLocalNetworkPurpose(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            string path = Path.Combine(output, "Info.plist");
            var document = new PlistDocument();
            document.ReadFromFile(path);
            document.root.SetString("NSLocalNetworkUsageDescription",
                "같은 로컬 네트워크에서 C6 Physics Lab의 세 기기를 직접 연결합니다.");
            document.WriteToFile(path);
            Debug.Log("C6_LAB_IOS_LOCAL_NETWORK_PURPOSE_OK");
        }
    }
}
