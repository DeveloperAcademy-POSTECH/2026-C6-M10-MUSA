# Watercolor brush masters

These three 1254×1254 RGBA images are the unmodified, AI-generated approved master pack. They are kept under `Editor` and are not used as CPU gameplay patterns. Re-bake with **C6 Lab → Experiments → Bake Watercolor Brushes**.

The baker fits the entire original in a 384×384 content square within a 512×512 output, leaving at least 64 transparent texels at every edge. Source alpha and grayscale RGB both contribute: alpha alone does not retain the pigment pools in these originals.

Runtime data at `Resources/GourdPaint/WatercolorBrushes`:

| Channel | Meaning |
|---|---|
| R | Pigment density: alpha × clamp(0.28 + 1.7 × (1 − luminance), 0, 1) |
| G | Area-weighted original alpha coverage |
| B | Interior chamfer distance from coverage > 0.02, divided by 64 texels |
| A | Same coverage as G |

All data textures are linear, Bilinear, Clamp, RGBA32 without mipmaps or compression; CPU Read/Write is disabled. `Version.txt` is SHA-256 of the baked Round, Growth and Overlap PNG bytes concatenated in that order. Profile and brush identity are pinned by the runtime journal.

These channels control visual composition only. Existing `GourdPaint/Patterns` masks still define gameplay cells and growth.
