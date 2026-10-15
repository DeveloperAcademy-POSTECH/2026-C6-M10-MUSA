using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    public static class LabPlayerCircleUpgrade
    {
        [MenuItem("C6 Lab/Preview Player 5 Circles")]
        public static void PreviewPlayerFive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            Undo.RecordObjects(new UnityEngine.Object[] { hud.leftPlayerCircle, hud.localPlayerCircle, hud.rightPlayerCircle }, "Preview P5 circles");
            hud.leftPlayerCircle.sprite = hud.playerCircleSprites[3];
            hud.localPlayerCircle.sprite = hud.playerCircleSprites[4];
            hud.rightPlayerCircle.sprite = hud.playerCircleSprites[0];
            Canvas.ForceUpdateCanvases();
        }

        public static void RenderPreview()
        {
            EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            var canvas = hud.GetComponent<Canvas>();
            var cameraObject = new GameObject("Circle preview camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(402, 874, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.486f, .486f, .486f);
            camera.cullingMask = 1 << hud.gameObject.layer;
            camera.transform.position = new Vector3(0, 0, -100);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(402, 874, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 402, 874), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "player-circle-preview.png"), texture.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
        }

        [MenuItem("C6 Lab/Apply Player Circles")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            hud.playerCircleSprites = new Sprite[5];
            for (int i = 0; i < 5; i++)
            {
                string path = "Assets/Lab/Art/UI/Player" + (i + 1) + "Circle.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                hud.playerCircleSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var parent = hud.battleUi.transform;
            hud.leftPlayerCircle = Circle(parent, "LeftNeighborBadge", -7, 437, hud.playerCircleSprites[0]);
            hud.rightPlayerCircle = Circle(parent, "RightNeighborBadge", 360, 437, hud.playerCircleSprites[2]);
            hud.localPlayerCircle = Circle(parent, "LocalPlayerCircle", 177, 809, hud.playerCircleSprites[1]);
            hud.leftNeighborBadge = hud.leftPlayerCircle.gameObject;
            hud.rightNeighborBadge = hud.rightPlayerCircle.gameObject;
            if (hud.seatText != null) hud.seatText.gameObject.SetActive(false);
            if (hud.leftNeighborText != null) hud.leftNeighborText.gameObject.SetActive(false);
            if (hud.rightNeighborText != null) hud.rightNeighborText.gameObject.SetActive(false);
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("C6_PLAYER_CIRCLES_APPLIED");
        }

        private static Image Circle(Transform parent, string name, float x, float y, Sprite sprite)
        {
            Transform found = parent.Find(name);
            var rect = found != null ? (RectTransform)found : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2((x + 24) / 402f, 1f - (y + 24) / 874f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(48, 48);
            rect.gameObject.SetActive(true);
            rect.SetAsLastSibling();
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
