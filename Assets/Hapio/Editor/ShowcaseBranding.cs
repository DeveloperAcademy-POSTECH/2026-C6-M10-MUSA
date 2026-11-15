using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Build-scoped branding. Never changes the saved normal-game identity or title prefab.</summary>
    public static class ShowcaseBranding
    {
        public const string ProductName = "합이오_쇼케이스";
        public const string ExecutableName = "HapioShowcase";
        public const string LogoPath = "Assets/Hapio/Art/Branding/ShowcaseLogo.png";
        public const string IconPath = "Assets/Hapio/Art/Branding/ShowcaseIcon.png";
        // TextColor/SecondaryLightIvory/100, exact sRGB components from Hapio.tokens.json.
        public static readonly Color Background = new Color(.9819607734680176f, .9647058844566345f, .9450980424880981f, 1f);

        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath) == null)
                throw new InvalidOperationException("Prepare the showcase wordmark: Hapio/Demo/Prepare Showcase Branding.");
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null || icon.width != 1024 || icon.height != 1024)
                throw new InvalidOperationException("The showcase app needs its opaque 1024px icon. Prepare Showcase Branding first.");
        }

        public static void WithTemporarySettings(BuildTarget target, Action build)
        {
            Validate();
            var named = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            bool show = PlayerSettings.SplashScreen.show, unityLogo = PlayerSettings.SplashScreen.showUnityLogo;
            var logos = PlayerSettings.SplashScreen.logos;
            var animation = PlayerSettings.SplashScreen.animationMode;
            var style = PlayerSettings.SplashScreen.unityLogoStyle;
            Color background = PlayerSettings.SplashScreen.backgroundColor;
            Sprite landscape = PlayerSettings.SplashScreen.background, portrait = PlayerSettings.SplashScreen.backgroundPortrait;
            bool blur = PlayerSettings.SplashScreen.blurBackgroundImage;
            float opacity = PlayerSettings.SplashScreen.overlayOpacity;
            Texture2D[] oldIcons = target == BuildTarget.iOS ? null : PlayerSettings.GetIcons(named, IconKind.Application);
            var platformIcons = new List<(PlatformIconKind kind, PlatformIcon[] slots, Texture2D[][] textures)>();
            if (target == BuildTarget.iOS)
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(named))
                {
                    var slots = PlayerSettings.GetPlatformIcons(named, kind);
                    platformIcons.Add((kind, slots, slots.Select(slot => (Texture2D[])slot.GetTextures().Clone()).ToArray()));
                }
            try
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath)) };
                PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
                PlayerSettings.SplashScreen.backgroundColor = Background;
                PlayerSettings.SplashScreen.background = null;
                PlayerSettings.SplashScreen.backgroundPortrait = null;
                PlayerSettings.SplashScreen.blurBackgroundImage = false;
                PlayerSettings.SplashScreen.overlayOpacity = 1f;
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                if (target == BuildTarget.iOS)
                    foreach (var entry in platformIcons)
                    {
                        foreach (var slot in entry.slots) slot.SetTexture(icon, 0);
                        PlayerSettings.SetPlatformIcons(named, entry.kind, entry.slots);
                    }
                else PlayerSettings.SetIcons(named, Enumerable.Repeat(icon, PlayerSettings.GetIconSizes(named, IconKind.Application).Length).ToArray(), IconKind.Application);
                build();
            }
            finally
            {
                PlayerSettings.SplashScreen.show = show;
                PlayerSettings.SplashScreen.showUnityLogo = unityLogo;
                PlayerSettings.SplashScreen.logos = logos;
                PlayerSettings.SplashScreen.animationMode = animation;
                PlayerSettings.SplashScreen.unityLogoStyle = style;
                PlayerSettings.SplashScreen.backgroundColor = background;
                PlayerSettings.SplashScreen.background = landscape;
                PlayerSettings.SplashScreen.backgroundPortrait = portrait;
                PlayerSettings.SplashScreen.blurBackgroundImage = blur;
                PlayerSettings.SplashScreen.overlayOpacity = opacity;
                if (target == BuildTarget.iOS)
                    foreach (var entry in platformIcons)
                    {
                        for (int i = 0; i < entry.slots.Length; i++) entry.slots[i].SetTextures(entry.textures[i]);
                        PlayerSettings.SetPlatformIcons(named, entry.kind, entry.slots);
                    }
                else PlayerSettings.SetIcons(named, oldIcons, IconKind.Application);
            }
        }

        [MenuItem("Hapio/Demo/Prepare Showcase Branding")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Finish Play mode and compilation before preparing branding.");
            var title = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hapio/Story/AnimatedTitle.prefab");
            var source = title.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(text => text.text == "합이오");
            if (source.font == null) throw new InvalidOperationException("The main title font is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(LogoPath));
            Bake(source, LogoPath, 2048, 1024, Color.clear, 570);
            Bake(source, IconPath, 1024, 1024, Background, 310);
            Validate();
            Debug.Log("HAPIO_SHOWCASE_BRANDING_READY: main-title wordmark and icon prepared.");
        }

        private static void Bake(UnityEngine.UI.Text source, string path, int width, int height, Color background, int fontSize)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Showcase branding camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = background;
                camera.orthographic = true; camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
                camera.allowHDR = false; camera.allowMSAA = false; camera.aspect = width / (float)height;
                camera.scene = preview;
                var canvasObject = new GameObject("Wordmark canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, preview);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                var textObject = new GameObject("Main title wordmark", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                textObject.transform.SetParent(canvasObject.transform, false);
                textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                var text = textObject.GetComponent<UnityEngine.UI.Text>();
                text.font = source.font; text.fontStyle = source.fontStyle; text.text = source.text;
                text.fontSize = fontSize; text.color = source.color; text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.font.RequestCharactersInTexture(text.text, fontSize, text.fontStyle);
                Canvas.ForceUpdateCanvases();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.Create(); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, path == IconPath ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                // App-store icons must be opaque; logo PNG retains transparent padding.
                if (path == IconPath)
                {
                    Color32[] pixels = image.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
                    image.SetPixels32(pixels); image.Apply();
                }
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = path == LogoPath ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true; importer.alphaIsTransparency = path == LogoPath;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
