# Territory lobby artwork (#303)

Figma file: `wW7ny7lNQ4DXNJ4kD46AuR`.

- Mode popup: `2965:9670`; background group: `2965:9732`.
- Waiting/ready seat layouts: `2965:9887` / `2965:10265`.
- Blue dashed box: `2965:10327`; red dashed box: `2965:10307`.
- Blue/red connections: `2965:10317` / `2965:10319`, assets `e4472.svg` / `df5e5.svg`.
- SVGs retain the original exported paths. Corresponding parent-folder PNGs are 4x rasterizations.
- Dashed-box SVGs include the outer half-stroke: 118x270 bounds around the authored 115x267 rectangle.
- The panel asset `29f34.svg` is byte-identical to the existing Lab `Source/20c44.svg`, so its existing sprite is reused.
- Save lobby presentation changes with `Hapio > Upgrade 2v2 Lobby UI`. The migration is versioned to preserve later manual edits when the menu runs again.

## 1:1 and 3:3 seats (#394)

- `TeamTrioBoxBlue.svg` / `TeamTrioBoxRed.svg`: the 3:3 team page box, the same 3-unit `4 4` dashed stroke and colours as the
  2:2 boxes at 258x270. The parent-folder PNGs are 4x rasterizations (macOS AppKit), like the other boxes.
- `TeamSeatBlue.png` / `TeamSeatRed.png` (parent folder): filled territory seats. The story P1 seat
  (`Assets/Lab/Art/Lobby/544fe.png`) is one ink `#1571A7` with its rim 39% toward white; these keep each pixel's lightness and
  alpha and swap the ink for `#2E61A1` (Blue) and `#CE493D` (Red).
- The 3:3 page reuses the story three-seat triangle (`Assets/Lab/Art/Lobby/4ba8d.png`). Its `<` `>` page arrows and the two page
  dots are placeholders until the designed art arrives.
