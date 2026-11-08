# Browser empty-room paper

## Color token source

`Hapio.tokens.json` preserves the supplied Figma export byte-for-byte (99 sRGB colors). See [the color system guide](../../../../../docs/COLOR_SYSTEM.md) for token paths, HEX and actual alpha values. It is a reference source; importing it does not replace runtime UI or gameplay colors.

- Figma file: `wW7ny7lNQ4DXNJ4kD46AuR`.
- Frame: `2965:8042`; visible paper: `2965:8090`.
- `BrowserEmptyPaper.svg` is the original export (`843da.svg`), logical size 345×308.864.
- `../BrowserEmptyPaper.png` is a 4× rasterization (1380×1235), used by the shared Hapio lobby empty-room paper.
- Existing Lab source artwork and its import metadata are unchanged.

## Shared menu update (3905 / 3908 / 4263 / 4268)

- File: `wW7ny7lNQ4DXNJ4kD46AuR`; components `3905:101`, `3908:186`.
- Browser states: `3905:237`, `3905:303`; mode examples `4263:1331`, `4263:1563`, `4268:1786`, `4268:1843`.
- `../Menu/BrowserPaper.png`: original `624c4.png` (693×1024), shared paper component `3908:190`. Display at 704×1041, offset (-233,-125), cropped to 402×874. Preserve NPOT dimensions and disable mipmaps/compression.
- Clouds reuse the byte-identical `Assets/Hapio/Art/Story/Title/5e97a.png`, at (-250,167,337,112) and (286,31,235,78), clipped to the browser frame.
- `MenuBrowserEmptyPaper.svg`: original `0f692.svg` from `3905:176`; root size 353.086×340.958. `../Menu/BrowserEmptyPaper.png` is its 4× rasterization, placed at (-2.32168,-2.95752) relative to the 348×335 empty-list panel. The source SVG is preserved without path changes.
- Button/card primitives use the existing resolution-independent `LabRoundedGraphic` and editable UI Text. Primary brown `#51473B`, disabled `#979189`, ink `#312B23`, muted `#6E6F61`, modal paper `#EDDBC7`, light text `#FAF6F1`.
- The title artwork (`3905:226`) remains the existing animated title; this update changes its start button only.
