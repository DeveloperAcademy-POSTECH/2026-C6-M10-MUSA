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

        /// <summary>박 색상 콤보 실패의 긴 진동(#222)은 Core Haptics를 쓴다. 옛 기기도 실행되도록 약하게(weak) 연결한다.</summary>
        [PostProcessBuild(101)]
        public static void LinkCoreHaptics(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            string path = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(path);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreHaptics.framework", true);
            project.WriteToFile(path);
            Debug.Log("C6_LAB_IOS_CORE_HAPTICS_LINKED");
        }
    }
}
