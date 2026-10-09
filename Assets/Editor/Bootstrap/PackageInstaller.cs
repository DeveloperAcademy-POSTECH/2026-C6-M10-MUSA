using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace C6Lab.Editor
{
    public static class PackageInstaller
    {
        private static readonly string[] Add =
        {
            "com.unity.netcode.gameobjects",
            "com.unity.transport"
        };

        private static readonly string[] Remove =
        {
            "com.unity.ai.navigation",
            "com.unity.collab-proxy",
            "com.unity.timeline",
            "com.unity.visualscripting"
        };

        private static AddAndRemoveRequest request;
        private static double deadline;

        public static void Install()
        {
            Debug.Log("C6_LAB_PACKAGE_START add=" + string.Join(",", Add) + " remove=" + string.Join(",", Remove));
            request = Client.AddAndRemove(Add, Remove);
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null) return;
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup <= deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("C6_LAB_PACKAGE_TIMEOUT");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                Debug.LogError("C6_LAB_PACKAGE_FAILED " + request.Error?.message);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("C6_LAB_PACKAGE_OK " + string.Join(",", request.Result.Select(p => p.name + "@" + p.version)));
            EditorApplication.Exit(0);
        }
    }
}
