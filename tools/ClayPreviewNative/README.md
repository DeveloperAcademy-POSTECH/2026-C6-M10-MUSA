# Mac comparison image picker

This is an optional Cocoa `NSOpenPanel` plug-in for the isolated clay comparison app. It does not write the selected image, import it into Unity, or retain a native allocation. The Editor uses its own guarded `EditorUtility.OpenFilePanelWithFilters` path instead.

`ClayPreviewMacImagePickerBuild.StageForPreview()` explicitly compiles a universal **arm64 + x86_64** development bundle into `Temp/ClayPreviewNative`, imports a temporary copy with **Mac standalone only / Editor disabled**, and returns an `IDisposable` stage. The preview build wraps only its `BuildPipeline.BuildPlayer` call in this stage. Disposal removes the temporary asset and its metadata. A build preprocessor refuses game or non-Mac builds if the stage remains after an interruption. The recovery menu is `C6 Lab > Experiments > Clay Sticker > Remove Stale Mac Picker Stage`.

The compiler tool invokes the equivalent of this command from the project root, then writes a BNDL `Info.plist`, verifies both architectures with `lipo`, and applies an ad-hoc development signature:

```sh
xcrun clang++ -std=c++17 -fobjc-arc -arch arm64 -arch x86_64 \
  -mmacosx-version-min=11.0 -bundle -framework Cocoa \
  tools/ClayPreviewNative/ClayPreviewMacImagePicker.mm \
  -o Temp/ClayPreviewNative/ClayPreviewMacImagePicker.bundle/Contents/MacOS/ClayPreviewMacImagePicker
```

The source SHA256, project-relative compiler command and verified architectures are recorded in the temporary `Build.txt`. No compiled native binary or user file path belongs in source control. This is a local development plug-in, with no release notarization claim.

Unity invokes exported C ABI `HapioClayChooseImage` with caller-owned UTF-8 buffers. Return codes are **1 selected / 0 cancelled / -1 error**. The panel runs synchronously on the application's main thread. No global shader or game setting is changed. PNG/JPEG bytes are checked by the shared C# header validator before decoding (maximum 16 MiB, maximum 2048 on either axis).

Backgrounds remain sRGB. Surface images become neutral linear RGBAHalf data: `RGB = clamp(linear luminance - alpha-weighted mean + 0.5)`, `A = original alpha`, with transparent RGB fixed at 0.5. A uniform image is exactly neutral rather than a new pigment color. The final textures are not CPU-readable; the owning successful import `Result` disposes them after the caller replaces its material reference.

Native plug-in deployment follows Unity's [desktop plug-in documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/PluginsForDesktop.html). This tool does not add the bundle to normal game dependencies or normal Build Settings.
