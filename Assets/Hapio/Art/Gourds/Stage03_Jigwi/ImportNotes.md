# Stage 03 — Jigwi gourd

Use `Prefabs/Jigwi_Gourd_GameReady.prefab` for development placement. Folder layout and placement follow Stage01/02.

- `Models/Jigwi_Gourd.fbx`: 90,931 triangles, 22 renderers. Hidden authoring backups, old hanbok/sandal charms and eye stalks are excluded. Evaluated mesh reductions and curves are included; Blender is not required for import. Read/Write is disabled.
- `Textures/`: 12 color/normal textures. Paper, red lid motifs, navy/beige cloth and the curled fire charm are baked from the authoring shaders. Tangent normals approximate the procedural surface detail.
- `Materials/`: 12 external URP Lit materials explicitly remapped by ModelImporter. Baked color textures use a white tint. Procedural roughness is approximated by constants; lighting can differ from Blender.
- `Animations/Jigwi_Gourd.controller`: one looping embedded `Jigwi_Performance` clip, Generic, 24 fps, frames 1–97, 4 seconds. Lid/creature motion, keyring swing, cloth flutter, rounded Yagwanggwi-style eye expressions, blinking and gaze remain. Separate pupil dilation and forward eye-pop motion are excluded. Two cloth BlendShape bindings are preserved.
- `Prefabs/Jigwi_Gourd_GameReady.prefab`: outer placement root and model child with Animator. Closed-pose body bounds center is the origin. Uniform root scale is approximately 17.09497; body size is approximately 1.86658 × 2.390325 × 2.45772 Unity units. Hanging rope is excluded from size matching.

The looping Animator is an art preview. Gameplay-event integration, model selection, paint and collision integration belong to the development team. Do not assign this whole-gourd prefab to an interior-only yokaiVisual slot. Battle scenes, stage definitions and existing stage assets were not changed. The authoring blend remains outside Assets.

Current Editor validation is recorded in `docs/VALIDATION.md`. No gameplay/device or full test-suite pass is claimed.
