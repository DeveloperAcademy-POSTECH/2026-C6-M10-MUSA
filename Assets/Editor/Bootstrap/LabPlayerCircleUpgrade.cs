using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    public static class LabPlayerCircleUpgrade
    {
        // P1..P5, and P6 for 3:3 territory (#394).
        private const int Circles = LabModel.TerritoryMaximumParticipants;
        private static readonly string[] BattleScenes = { "Assets/Lab/Scenes/Lab.unity", "Assets/Hapio/Scenes/Battle.unity" };

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
            hud.playerCircleSprites = new Sprite[Circles];
            for (int i = 0; i < Circles; i++) hud.playerCircleSprites[i] = ImportCircle(i + 1);
            // Figma 2527:7969, 402×874: plate (-165,459) + badge offsets.
            var parent = hud.battleUi.transform;
            hud.leftPlayerCircle = Circle(parent, "LeftNeighborBadge", -6, 480, hud.playerCircleSprites[0]);
            hud.rightPlayerCircle = Circle(parent, "RightNeighborBadge", 365, 480, hud.playerCircleSprites[2]);
            hud.localPlayerCircle = Circle(parent, "LocalPlayerCircle", 65, 809, hud.playerCircleSprites[1]);
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

        /// <summary>
        /// #394: add the P6 circle (a purple copy of P5's art with its own number) to the battle HUD and drum minimap of
        /// the Lab and the game Battle Scene, after P1..P5. Nothing else in either Scene changes; running it again does nothing.
        /// </summary>
        [MenuItem("C6 Lab/Add Player 6 Circle")]
        public static void AddPlayerSix()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            Sprite six = ImportCircle(Circles);
            foreach (string path in BattleScenes)
            {
                if (!File.Exists(path)) continue;
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool changed = false;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (LabHud hud in root.GetComponentsInChildren<LabHud>(true))
                            changed |= Extend(ref hud.playerCircleSprites, six, hud);
                        foreach (LabDrumMinimap map in root.GetComponentsInChildren<LabDrumMinimap>(true))
                            changed |= Extend(ref map.playerSprites, six, map);
                    }
                    if (changed && !EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Could not save " + path + ".");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("C6_PLAYER_SIX_CIRCLE_APPLIED");
        }

        private static bool Extend(ref Sprite[] sprites, Sprite six, UnityEngine.Object owner)
        {
            if (sprites == null || sprites.Length != Circles - 1) return false;
            sprites = sprites.Concat(new[] { six }).ToArray();
            EditorUtility.SetDirty(owner);
            return true;
        }

        private static Sprite ImportCircle(int number)
        {
            string path = "Assets/Lab/Art/UI/Player" + number + "Circle.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image Circle(Transform parent, string name, float x, float y, Sprite sprite)
        {
            Transform found = parent.Find(name);
            var rect = found != null ? (RectTransform)found : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2((x + 22.5f) / 402f, 1f - (y + 23.5f) / 874f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(45, 47);
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
