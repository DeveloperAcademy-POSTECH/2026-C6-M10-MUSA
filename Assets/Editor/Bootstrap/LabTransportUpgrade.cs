using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Replace only the saved Lab transport component, preserving Scene references.</summary>
    public static class LabTransportUpgrade
    {
        public static void UpgradeOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != "Assets/Lab/Scenes/Lab.unity")
                throw new InvalidOperationException("Open Assets/Lab/Scenes/Lab.unity before upgrading its transport.");

            LabNetwork network = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                network = root.GetComponentInChildren<LabNetwork>(true);
                if (network != null) break;
            }
            if (network == null) throw new InvalidOperationException("LabNetwork is missing from the saved Scene.");
            NetworkManager manager = network.manager != null ? network.manager : network.GetComponent<NetworkManager>();
            if (manager == null) throw new InvalidOperationException("NetworkManager is missing from the saved Scene.");

            var existing = network.GetComponent<LabDualStackTransport>();
            if (existing != null)
            {
                network.transport = existing;
                manager.NetworkConfig.NetworkTransport = existing;
                EditorUtility.SetDirty(network);
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            UnityTransport previous = network.transport != null ? network.transport : network.GetComponent<UnityTransport>();
            if (previous == null || previous.gameObject != network.gameObject)
                throw new InvalidOperationException("Expected one UnityTransport on the LabNetwork GameObject.");

            var replacement = network.gameObject.AddComponent<LabDualStackTransport>();
            EditorUtility.CopySerializedManagedFieldsOnly(previous, replacement);
            network.transport = replacement;
            manager.NetworkConfig.NetworkTransport = replacement;
            UnityEngine.Object.DestroyImmediate(previous);
            EditorUtility.SetDirty(network);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(replacement);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("C6_LAB_DUAL_STACK_SCENE_UPGRADED");
        }
    }
}
