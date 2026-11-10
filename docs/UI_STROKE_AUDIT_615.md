# #615 Figma UI stroke audit

Branch: `fix/#615-ui-stroke`. Figma file: `wW7ny7lNQ4DXNJ4kD46AuR`.

## Scope and findings

The supplied 54 nodes were inspected for stroke weight, alignment, paint opacity, corner radius and dash patterns. Four game-over/territory-result nodes are excluded at the user's request; no #603 changes were imported. Screens share components, so this is a property/component audit, not 54 independent runtime screenshot comparisons.

Missing silhouettes came from substituting plain rounded primitives for Figma HEIST, SLASHER and GRINDHOUSE brush strokes. The implementation uses original background-only exports; dynamic text and Button navigation remain native Unity UI. Existing seat/card/connector, player/badge, flag, tick, orb and board SVG art already contains its outlines and receives no second outline. Stroke-free text, backgrounds and annotation frames receive no invented stroke.

| Area | Correction |
| --- | --- |
| Start / create / mode / seal | Original HEIST silhouettes; 8/5/5/8 design-unit centred strokes; separate ready/disabled colours |
| Capacity chips / room rows | Original SLASHER surfaces; 6-unit centred strokes, separate fill/stroke opacity and selected/pressed states |
| Time / pause icon | GRINDHOUSE 4-unit centred outlines; logical paper fill stays beneath the brush |
| Story / territory progress | GRINDHOUSE 3-unit centred outlines; existing stage ink and progress values retained |
| Stamina | GRINDHOUSE 6-unit outer frame plus original six cells including their 2-unit outlines |
| Team seat boundary | Existing 2-unit INSIDE dashed line, 4/4 dash/gap; centreline radius corrected to retain 16-unit outside corner |

## Assets and reproduction

`Assets/Hapio/Art/UI/Strokes/Source/manifest.json` records original node IDs, logical dimensions, outside-stroke offsets and exported dimensions. SVGs are unmodified Figma exports with text/children omitted for background-only components. `Source/StaminaCells.png` is the original 3× six-cell Figma export. Other PNGs are rasterized at 3× from SVG with resvg. HUD PNG RGB is normalized to white while retaining the original alpha, so existing dynamic stage/fill colours are not multiplied by baked colours. Button surfaces retain original RGB and alpha, including the overlapping semi-transparent fill and stroke.

Sprites use FullRect, bilinear/clamp, no mipmaps and no compression. `GameFigmaStrokeLibrary` maps export bounds onto the existing logical RectTransform without expanding click bounds. Scroll clipping has 4-unit allowance for centred strokes. `Hapio > Upgrade Figma UI Strokes` imports and saves the shared lobby prefab and battle theme; existing menu/seat/battle authoring paths preserve these bindings.

## Supplied nodes

| Node | Design | Scope |
| --- | --- | --- |
| 3905:225 | 타이틀_new | Audited; shared components |
| 3905:237 | 로비 화면_빈 방_new | Audited; shared components |
| 3905:303 | 로비 화면_방 있음_new | Audited; shared components |
| 3905:101 | 로비/모드 선택 컴포넌트 | Audited; shared components |
| 4263:1331 | 모드 선택 화면 / 4인 예시 | Audited; shared components |
| 4263:1563 | 모드 선택 화면 / 2인 예시 | Audited; shared components |
| 4268:1786 | 모드 선택 화면 / 3,5인 예시 | Audited; shared components |
| 4268:1843 | 모드 선택 화면 / 6인 예시 | Audited; shared components |
| 4067:798 | 3:3_준비 완료_new | Audited; shared components |
| 4063:738 | 3:3_위치 확인1_new | Audited; shared components |
| 4063:855 | 2:2 준비완료_new | Audited; shared components |
| 4063:681 | 2:2_위치 확인1_new | Audited; shared components |
| 4063:805 | 1:1 준비완료_new | Audited; shared components |
| 4063:632 | 1:1 위치 확인1_new | Audited; shared components |
| 3897:622 | 5인_위치 확인4_new | Audited; shared components |
| 3897:498 | 5인_위치 확인2_new | Audited; shared components |
| 3897:560 | 5인_위치 확인3_new | Audited; shared components |
| 3897:685 | 5인_위치 확인5_new | Audited; shared components |
| 3887:5484 | 5인_위치 확인_방장이 위치를 수정할 때_new | Audited; shared components |
| 3887:5465 | 5인_위치 확인 완료_new | Audited; shared components |
| 3897:436 | 5인_위치 확인1_new | Audited; shared components |
| 3897:370 | 4인_위치 확인4_new | Audited; shared components |
| 3897:308 | 4인_위치 확인3_new | Audited; shared components |
| 3887:5446 | 4인_위치 확인_방장이 위치를 수정할 때_new | Audited; shared components |
| 3897:249 | 4인_위치 확인2_new | Audited; shared components |
| 3897:190 | 4인_위치 확인1_new | Audited; shared components |
| 3887:5427 | 4인_위치 확인 완료_new | Audited; shared components |
| 3897:131 | 3인_위치 확인3_new | Audited; shared components |
| 3897:75 | 3인_위치 확인2_new | Audited; shared components |
| 3887:5409 | 3인_위치 확인_방장이 위치를 수정할 때_new | Audited; shared components |
| 3887:5391 | 3인_위치 확인 완료_new | Audited; shared components |
| 3864:3734 | 3인_위치 확인1_new | Audited; shared components |
| 3897:74 | 자리 확인 / 봉인 대기 버튼 | Audited; shared components |
| 3912:519 | 자리 확인 / 봉인 시작 버튼 | Audited; shared components |
| 3908:196 | 자리 확인 / 방장 표시 | Audited; shared components |
| 3912:515 | 자리 확인 / 봉인 준비 완료 안내 | Audited; shared components |
| 3908:194 | 자리 확인 / 방 이름 | Audited; shared components |
| 3897:71 | 자리 확인 / 안내 | Audited; shared components |
| 3908:190 | 자리 확인 / 한지 배경 | Audited; shared components |
| 3908:271 | 자리 확인 / 좌석 배치 | Audited; shared components |
| 3908:222 | 자리 확인 / 좌석 마커 | Audited; shared components |
| 3908:192 | 자리 확인 / 카드 표면 | Audited; shared components |
| 3908:189 | 자리 확인 / 방 나가기 | Audited; shared components |
| 4062:654 | 땅따먹기 자리 확인 컴포넌트 | Audited; shared components |
| 4234:1028 | 전투 화면 컴포넌트 | Audited; shared components |
| 4185:2672 | 전투 화면 / Stage 1 | Audited; shared components |
| 4203:1039 | 전투 화면 / Stage 2 | Audited; shared components |
| 4203:1589 | 전투 화면 / Stage 3 | Audited; shared components |
| 4229:5583 | 전투 화면 / 2:2 땅따먹기 | Audited; shared components |
| 4229:7267 | 전투 화면 / 3:3 땅따먹기 | Audited; shared components |
| 4379:1991 | 게임 오버 화면 / 불이 | Excluded |
| 4379:2004 | 게임 오버 화면 / 금옥이 | Excluded |
| 4379:1822 | 땅따먹기 결과 / 홍팀 승리 | Excluded |
| 4405:3467 | 땅따먹기 결과 / 청팀 승리 | Excluded |

