# Stage 02 — Yagwanggwi gourd

Models/Yagwanggwi_Gourd.fbx contains visible render objects and a combined 24 fps, 4 second performance. Hidden authoring backups are excluded.

Prefabs/Yagwanggwi_Gourd_GameReady.prefab includes URP Lit materials and a looping Animator controller. The performance is an authoring preview; gameplay event integration is pending. Existing Battle/Lab prefabs and stage definitions have not been replaced.

Textures include extracted source images, baked base color / tangent normals for the procedural paper and ribbon materials, and Unity metallic (R) / smoothness (A) maps. Procedural roughness is approximated by material values.

The import has 22 renderers and 446,594 triangles. Mobile optimization and gameplay paint/collision integration are pending.

Verified in Unity Editor: 0 missing materials, supported URP shaders, 0 unresolved animation paths, and actual transform changes during Animator playback. No gameplay or device test is claimed.

## Placement prefab

Use Prefabs/Yagwanggwi_Gourd_GameReady.prefab for development placement. The outer root has uniform scale 17.096283; its origin is the body bounds center in the initial closed pose. Body dimensions are approximately 1.867 x 2.390 x 2.458 Unity world units, matching the current gameplay gourd height (2.390325). Hanging rope is excluded from size matching. The model child retains its original scale and Animator hierarchy; the redundant small prefab layer has been unpacked and removed. Gameplay collision, painting and stage event integration are still pending; the looping controller is a preview.

## Texture cleanup

Byte-identical duplicate color, normal and metallic/smoothness images share the original textures across materials. Unused original ORM images and the paper reference image were removed after baking. Prefab materials and animation were revalidated.

## Battle bake (#507)

The game uses `Resources/Hapio/Gourds/Stage02_Yagwanggwi_Battle.prefab`, baked from the GameReady prefab by `Hapio > Gourds > Bake Stage 2 Yagwanggwi Gourd` (`Hapio.Editor.GameGourdRigBaker`). The art prefab, FBX and materials are not modified. The bake writes `Generated/` (outer-wall paint shell, closed-lid paint mesh, 48 x 24 hit shell, legacy 2 s yokai clip, yokai timeline, two paint materials with the body art under the paint) and `Generated/BakeReport.txt`. It removes the model's own hanging rope (`Rear_Rope_*`) and the looping Animator. When a lighter re-export arrives, replace the art and run only the bake again.
