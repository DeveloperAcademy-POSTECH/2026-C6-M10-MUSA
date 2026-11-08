# Figma seat confirmation assets

File: `wW7ny7lNQ4DXNJ4kD46AuR`. Shared components `3908:186`, territory components `4062:654`. Original SVG exports are preserved in this folder. PNGs one directory above are rasterized at 4× (288 dpi) without path edits. Runtime images use their natural PNG bounds / 4; seat logical boxes remain 63×63.

| Asset | Component / use |
| --- | --- |
| `09a14.svg` | `3908:192`, story 345×449 card surface, including exterior stroke |
| `90e0e.svg` | Territory 1:1 / 2:2 card surface as supplied in frame `4063:681` |
| `1f1fa.svg` | Territory 3:3 frame `4063:738`, logical 345×479 card |
| `a4418.svg` | Standalone 3:3 card reference `4074:1031` (preserved source variant) |
| `5089e.svg` / `3c326.svg` / `79dac.svg` | `3908:223` / `3908:236` / `3908:252`, story connection lines |
| `5794c.svg` | `4074:984` / `4074:1017`, 4×42.5 vertical teammate connection |
| `c7fa7.svg` / `e327d.svg` | P1 / Blue team filled and empty marker |
| `a3291.svg` / `c701d.svg` | P2 / Red team filled and empty marker |
| `1fdc3.svg` / `6a571.svg` | P3 green filled and empty marker |
| `e006a.svg` / `7e76b.svg` | P4 yellow filled and empty marker |
| `b2348.svg` / `aac1f.svg` | P5 brown filled and empty marker |
| `82ba1.svg` | P6 filled marker (`4067:794`), byte-identical to `a3291.svg`; its component orientation is unrotated |

The paper texture reuses `Menu/BrowserPaper.png` and the exact shared `3908:190` crop. P numbers, room name, counts and host designation stay editable/dynamic UI Text; outlined example count text is not used as a static image. Team boundaries are Figma native dashed rounded-rectangle primitives, implemented by `GameDashedRoundedGraphic` (2 point stroke, radius 16). UI colors and token paths are in `GameLobbySeatColors`; gameplay palette/settings are unaffected.
