# Lobby artwork (#263)

Source: Figma file `wW7ny7lNQ4DXNJ4kD46AuR`, section `2658:12818`.

- Start: `2658:14046`; browser: `2658:14080`, `2658:15592`, `2658:15710`.
- Position layouts: `2658:12541`, `2658:16616`, `2658:17058`.
- Occupied states: `2658:16221`, `2658:16282`, `2658:16904`, `2670:17916`.
- `Source/` preserves the downloaded PNG/SVG originals. Root PNGs are 4× rasterizations of visible original SVGs, preserving their paths and proportions for Unity uGUI. Tiny vector layers obscured by the background painting and off-frame reference images are retained as source only.
- `Source/3a857.png` is the original 1536×1024 painting, placed at 1347×898 logical units in Figma. `LabLobbyView.BackgroundUv` reproduces the two design crops without altering that source image.
- Dashed slots P2–P5: `56dc7`, `c4379`, `f037d`, `814ee`. Filled P1–P5: `544fe`, `6ece5`, `2859c`, `c2f7a`, `2b4c9`. Text is dynamic and uses the supplied Shilla Culture Medium font.
- Lines for 3/4/5 participants: `4ba8d`, `cbb55`, `ec7bb`. Room sheet: `20c44`; empty-browser sheet: `81e78`.
- Five-player P4/P5 positions follow the existing P-number game ring, as approved by the user, rather than the swapped labels in the reference.

Reapply in Unity with **C6 Lab → Apply Figma Lobby Design** while outside Play Mode and after saving the Scene. Existing battle, DEV and network references are preserved.

## Image quality

Use a native-resolution Game View (for example 1206×2622), then **Fit**/a scale below 1 to see the whole screen. The 402×874 reference is a layout size: rendering at that size and zooming to 1.5× magnifies the framebuffer, including text and every UI edge.

`LabLobbyTextureQuality` keeps source dimensions, no mipmaps, clamp sampling, and explicit uncompressed RGBA32 settings for Standalone/iPhone. It reapplies these rules on future lobby-image imports. The Figma background's actual source is only 1536×1024; a portrait crop uses approximately 458×997 source pixels. Crisp 3× background detail requires a higher-resolution original (approximately 4041×2694 or larger for the same composition). Enlarging the current bitmap does not restore missing detail.

## Shared room list

The two browser references are `2658:15592` and `2658:14080`. Product pending rooms request `C6Lab-Room`, `C6Lab-Room (2)`, etc. with native NoAutoRename collision checks, retry occupied numbers, and confirm names in creation order. Legacy direct Host/DEV keeps native automatic renaming. Confirmed names appear as 수장고-1/수장고-2/수장고-3 for everyone. Numeric sorting preserves 1,2,10 order. Rows alternate #E9DFCA/#EEE6D6 and remain attached to their actual network join key. Normal DEV visibility and Open/Apply actions require the connected Host. Offline physics tuning is an explicit Editor menu path.

Creating a room publishes a creator-owned pending room and resets the capacity form. Multiple pending cards retain independent capacities, confirmed names and reserved ports. Only their creator can activate them as the real P1 Host; other players see a waiting-for-host state until then. Activation transfers the same Bonjour registration to the game connection, so the room name and join key stay unchanged. Other pending rooms remain advertised while the creator plays one room. Unentered rooms are removed when the app/Scene closes. The position-page back label follows `2658:12570` at (27,81), left aligned inside a 44-point tap target.
