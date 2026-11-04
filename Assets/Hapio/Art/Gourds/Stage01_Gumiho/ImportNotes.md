# Stage 01 — Gumiho gourd

Use `Prefabs/Gumiho_Gourd_GameReady.prefab` for development placement. File names and folder layout follow Stage02_Yagwanggwi.

## Assets

- `Models/Gumiho_Gourd.fbx`: visible render geometry only, 85,799 triangles and 35 renderers. Hidden authoring backups, the sandal, tail charms and numeral 9 are excluded. Mesh reductions and curve conversion are already evaluated; importing does not require Blender. Read/Write is disabled in the ModelImporter.
- `Materials/`: 15 explicit URP Lit materials remapped by the FBX importer. The lid uses the baked red/yellow/teal motifs, the ribbons retain yellow/teal, and the unpatterned hanbok charm has a red lining and yellow ties, without a tassel.
- `Textures/`: extracted source color/normal images, baked body/lid/ribbon colors, body/ribbon tangent normals, and metallic (R) / smoothness (A). Unused original ORM is kept outside the project. Blender procedural microstructure is sampled into textures; the paper and cloth normal strengths are reduced in Unity for a restrained surface. Lighting and tone mapping can still differ from Blender.
- `Animations/Gumiho_Gourd.controller`: loops the embedded `Gumiho_Performance` clip, 24 fps, frames 1–97, approximately 4 seconds. Two emergence beats include lid/creature motion, cloth flutter and eye expression.
- `Prefabs/Gumiho_Gourd_GameReady.prefab`: outer placement root, model child, URP materials and Animator. Body height is 2.390325 Unity units, matching stage2. The root origin is the closed-pose body bounds center; uniform root scale is approximately 17.094973. The hanging rope is excluded from size matching.

## Animation and integration

Both eye meshes retain the `Fierce narrowed fox eye` BlendShape. Both ribbons retain `Gentle cloth flutter`. The pupils remain round with fixed scale throughout the clip. Generic animation and all four BlendShape bindings are imported; the clip remains inside the FBX rather than duplicated as an external .anim.

The looping controller is a model preview, not gameplay-event integration. Battle Scene, StageDefinition, GourdModelPreviewSet and model-selection scripts are unchanged. The developer must select this prefab for stage1 in the display replacement flow; do not assign the whole gourd to the `yokaiVisual` interior-only slot. Existing collision and paint rules remain the responsibility of that integration.

The authoring .blend is preserved separately from Assets. FBX texture paths are relative and native-source metadata contains only its file name.

## Verification

In the current Unity Editor:

- 85,799 imported triangles, 35 renderers and 15 external URP materials.
- Zero missing materials, unsupported shaders or unresolved animation transform paths.
- 424 animation bindings, including both eye and both cloth BlendShapes.
- Eye weights reach 0 and 100 while pupil scale stays constant at sampled times 0, 0.65, 1.2, 1.5, 2 and 3.2 seconds.
- Animator evaluation produces a transform change. Closed/front, emergence/front and rear renders were inspected in an isolated preview scene.
- Body center and height match the placement convention used by stage2. The active Battle Scene remained unmodified.

No gameplay, network, paint/collision integration, device performance or full test-suite pass is claimed. The console also contained Transport/domain-lifecycle errors; this asset validation is not a zero-console-errors claim.
