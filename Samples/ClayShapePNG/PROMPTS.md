# 스플래시 입력 PNG 샘플

내장 imagegen으로 개별 생성한 3종이다. 생성 원본은 보존하고, 448×448 영역을 512×512 투명 캔버스 중앙에 내보냈다. 최종 파일은 RGBA PNG이며 실제 Alpha와 사방 최소32px 여백을 검사했다. 게임 코드·기본 브러시에 자동 연결하지 않는다.

- `01_broad_splash.png`: 넓고 둥근 중심·불규칙한 둥근 돌기·분리 방울, 주홍색.
- `02_flowing_splash.png`: 대각선으로 흐르는 두꺼운 몸통·둥근 꼬리·분리 방울, 파란색.
- `03_cluster_splash.png`: 세 덩어리가 넓게 이어진 군집·완만한 굴곡·분리 방울, 초록색.

비교 앱에서 `Choose outline PNG…`로 선택한다. 윤곽 모드에서는 RGB 색을 쓰지 않고 Alpha만 사용하므로, 보이는 스티커 색은 앱 팔레트를 따른다.

## 생성 프롬프트

공통: Create one production-ready 2D PNG silhouette asset for a Unity clay paint-splash sticker. Exactly 512 by 512 pixels square. Truly transparent background with an alpha channel; fully opaque flat color inside the splash. No paper, no checkerboard drawn into the image, no border, text, labels, shadows, glow, highlights, bevel, lighting, or 3D perspective. Orthographic flat top-down silhouette. Keep every opaque pixel including droplets within the central 80% of the canvas: at least 50 pixels of fully transparent padding on ALL four sides of a 512px image. Only ONE splash design per image, not a sheet. Smooth large rounded organic lobes, broad connections, no thin pointed spikes, no tiny noisy speckles. A clearly defined opaque alpha edge with only minimal antialiasing. The color will be replaced by the game; outline clarity is the priority.

1. Variant 1: a broad, near-round liquid splash with a large full central body and 7 to 9 uneven, bulbous rounded lobes. Several lobes long and others short, gentle asymmetric organic outline, one or two small ROUND detached droplets near the body, all safely inside the padding. Flat solid warm coral red fill.
2. Variant 2: an elongated flowing paint splash, oriented diagonally lower-left to upper-right, one thick connected flowing body with a wide rounded head and 4 or 5 thick soft trailing fingers, every fingertip rounded. Not a flame, not a lightning bolt, no sharp taper. Two generous round detached drops alongside it, safely within the padding. Flat solid medium blue fill.
3. Variant 3: a compact cluster of three large overlapping rounded splash masses forming ONE connected irregular horizontal organic blob. It has 9 to 12 large soft scallops overall, shallow valleys and very broad joins, one medium circular detached droplet and one smaller droplet. No holes, no branches, no stipple. Flat solid forest green fill.
