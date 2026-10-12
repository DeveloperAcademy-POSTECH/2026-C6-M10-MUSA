using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace C6Lab.Editor
{
    /// <summary>Local Host/Join and Bonjour room discovery need iOS Local Network permission.</summary>
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
                "같은 로컬 네트워크나 개인용 핫스팟에서 C6 Physics Lab 방을 찾고 기기를 연결합니다.");
            PlistElementArray services = document.root.CreateArray("NSBonjourServices");
            services.AddString("_c6lab._udp");
            document.WriteToFile(path);
            Debug.Log("C6_LAB_IOS_LOCAL_NETWORK_PURPOSE_OK");
        }
    }
}
