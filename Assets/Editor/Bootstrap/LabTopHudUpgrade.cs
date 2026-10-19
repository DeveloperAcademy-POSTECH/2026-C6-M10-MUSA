using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Applies the supplied timer/pause artwork without changing live timer or pause behaviour.</summary>
    public static class LabTopHudUpgrade
    {
        [MenuItem("C6 Lab/Apply New Timer And Pause")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the top HUD.");
            PrepareSprite("TimeFrame");
            PrepareSprite("PauseButton");
            var scene = EditorSceneManager.OpenScene("Assets/Lab/Scenes/Lab.unity");
            var hud = GameObject.Find("LabCanvas").GetComponent<LabHud>();
            var time = (RectTransform)hud.battleUi.transform.Find("FigmaTimeFrame");
            // Supplied PNGs at half resolution, centred on the Figma timer and pause slots.
            Place(time, 27.5f, 25.25f, 78f, 33.5f);
            var image = time.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Lab/Art/UI/TimeFrame.png");
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var oldMask = time.Find("ClockValueMask");
            if (oldMask != null) oldMask.gameObject.SetActive(false);
            var text = hud.timeText;
            text.transform.SetParent(time, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            if (text.font == null) throw new InvalidOperationException("The supplied Shilla Culture Bold font did not import.");
            text.fontSize = 22;
            text.text = "--:--";
            // The OTF itself is the Bold face. Do not add synthetic bold on top of it.
            text.fontStyle = FontStyle.Normal;
            text.resizeTextForBestFit = false;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.black;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();

            var pause = hud.pauseButton;
            Place((RectTransform)pause.transform, 340f, 24f, 33f, 34f);
            var pauseImage = pause.GetComponent<Image>();
            pauseImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Lab/Art/UI/PauseButton.png");
            pauseImage.color = Color.white;
            pauseImage.preserveAspect = true;
            pause.targetGraphic = pauseImage;
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(pause);
            EditorUtility.SetDirty(pauseImage);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2((x + width * .5f) / 402f,
                1f - (y + height * .5f) / 874f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PrepareSprite(string name)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Lab/Art/UI/" + name + ".png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
