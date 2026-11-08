# 전투 UI 컴포넌트

Figma 파일 `wW7ny7lNQ4DXNJ4kD46AuR`의 화면 4185:2672(1), 4203:1039(2), 4203:1589(3), 컴포넌트 섹션 4234:1028을 사용한다. 배경·박·충돌용 조합대 윤곽은 이 자료에 포함하지 않는다.

- `Source`: Figma 원본 SVG/PNG. `BoardLayers.json`은 원본 조합대 레이어의 배치다. `StaminaCells.svg`는 Figma의 33×23, 간격 5인 여섯 셀을 네이티브 도형으로 구성한 마스크다.
- `BoardStage1~3.png`: 각 스테이지의 원본 조합대 레이어를 합성한 투명 텍스처. Figma 4229:3949·4238·4282의 배치로 구슬 아래에 표시한다. 기존 판을 화면 크기로 늘리는 방식은 쓰지 않는다. 정상 합성 중에는 기존 판 Renderer만 숨기고 Transform·메시·물리 윤곽은 유지한다. PNG 밖의 투명 영역에는 스테이지 배경이 이어진다. 입력 카메라의 별도 한지 덧칠 패스를 끄고, 같은 스테이지 카메라 시점을 연장한 표시 카메라를 사용한다.
- `21af4`/`34af9`: 어두운/밝은 스테이지 밑줄. `ead0b`/`57d73`: 점령 바 눈금. `50224`/`3aff5`, `4da2d`/`a794d`: 어두운/밝은 청·홍 깃발.
- `Player1~5`, `Mini1~6`, `DrumIcon`, `TeamBlue`, `TeamRed`: 플레이어·북 위치 표시 부품. P6 큰 표시는 동일한 P6 소형 에셋을 확대한다.
- `07b57` / `a49d1`은 각각 2:2와 3:3 팀 위치 표시의 연결선이다.
- `Position2v2Red/Blue`, `Position3v3Red/Blue`: Figma의 완성 위치 컴포넌트. 실제 실행 화면은 같은 마커를 사용해 현재 P번호·팀·북 소유자에 맞춰 배치한다. 게임의 앞 절반 P번호=청팀 규칙을 유지한다.
- `e3ff0`, `1b7bc`, `ca403`, `02a2c`, `31348`: 금·수·화·목·토 구슬. `GenerateDrum`: 새 스트로크의 구슬 생성 북.

공용 테마는 `Resources/Hapio/UI/BattleUiTheme.asset`, 구슬 표현은 `BattleOrbAppearance.asset`이다. `Hapio > Upgrade Battle UI Components`는 임포트 설정과 두 테마 에셋만 갱신한다. Scene은 저장하지 않는다. 런타임은 `GameBattleUiPresentation`과 공용 `GameUiFrame`으로 시간/일시정지/표제/진행률/스태미나 컴포넌트를 구성한다. 수치·입력은 기존 LabHud와 LabTeamBar가 계속 소유한다.

색은 `../Source/Hapio.tokens.json`의 sRGB 값을 `GameBattleUiColors`에 명시한다. 1스테이지는 어두운 글자, 2·3은 밝은 글자, 스태미나 외곽선은 `TextColor/Stemina/Stage1~3`을 사용한다. 도색 판정용 LabPaintPalette는 바꾸지 않는다.
