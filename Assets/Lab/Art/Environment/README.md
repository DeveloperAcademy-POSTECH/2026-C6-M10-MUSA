# Background Studio

Select `BattleStage/Background Studio` in Lab.

- **Preserve Layout / 이미지만 적용** changes artwork and appearance without replacing GameObjects or changing their Transforms.
- **Relayout / 원형 배치 재계산** explicitly recalculates position, rotation and scale. A confirmation explains the difference.
- Each Decorations foldout has PNG, category, tint, opacity, visibility, thickness, depth layers, cross plane, billboard and wind controls.
- `boxHeight` is the layout rectangle height; image replacement fits inside the current rectangle and preserves its aspect ratio.
- Existing inactive cards are retained when counts decrease, so their references are not destroyed.
- For an individual override, select a PNG child, disable **Use Group Appearance**, and edit its `LabEnvironmentCard`. Its Transform can always be edited independently.
- Thickness is world-space distance between image layers. Cross Plane adds a narrow side plane. This approximates volume; it is not a watertight silhouette extrusion.
- Wind Amount bends the upper part in the shader; the base, Transform and collision stay fixed. Wind Speed is adjustable. Play mode shows the animation.
- Billboard is evaluated in the shader for the rendering camera, without rotating the scene Transform. Use sparingly for small vegetation.
- New artwork uses a soft transparent unlit shader. No collider is added. Texture alpha and feathered ink edges are retained.
- Ground and Board Backing stay under their original parents, preserving the gameplay floor movement and lower-board composition.
- Previous cloud/sun objects are inactive; original PNG assets remain available. No target, paint, network or gameplay camera code is changed.

Save the scene after editing. P1–P5 buttons are Scene View direction references, not a simulation of phone HUD or device aspect ratio.
