# 숨김 개발 설정 진입 (#564)

개발용 Hapio의 일반 플레이 화면에서는 DEV·DEV SOLO 버튼을 표시하지 않는다. 기존 설정·Host 권한·LAN/핫스팟 연결·게임 판정은 유지한다. 기존 Lab 전용 화면은 이 변경 대상이 아니다.

## 사용법

- **홈·방 로비:** 화면의 안전 영역 기준 맨 위 중앙 빈 공간을 **1.2초 안에 3회 터치**한다. Mac에서는 같은 곳을 3번 클릭한다.
- **전투:** Host가 같은 영역을 3회 터치하면 세션을 일시정지하고 기존 DEV 설정을 연다. 설정창을 닫아도 자동 재개하지 않으며 기존 일시정지 메뉴에서 재개한다.
- **SOLO:** 홈에서 숨김 DEV를 연 뒤 설정창 오른쪽 위 `SOLO` 또는 `2:2 SOLO`를 누른다. 방 참가 중에는 SOLO를 표시하지 않는다.
- 설정 값 변경·APPLY·재시작·DEV 다음 스테이지는 기존 동작을 사용한다.

표시가 없는 입력 영역은 402×874 기준 **84×24**, Safe Area 상단 중앙이다. 시간 표시·일시정지 버튼의 위치는 바꾸지 않는다.

## 입력·권한

- 1·2회만 누르면 아무 동작도 하지 않는다. 처음 누른 때부터 1.2초를 넘기면 새 묶음으로 센다.
- 우클릭·드래그·진입 불가 상태·owner 비활성화 시 누적 입력을 초기화한다.
- **App Store / Release (`Development Build` OFF):** `UNITY_EDITOR || DEVELOPMENT_BUILD`가 아니면 숨김 입력 영역을 생성하지 않으며 `OpenDeveloper()`도 거절한다. 영역을 반복 터치해도 DEV에 진입하지 않는다. 기존 `DeveloperBuildAvailable` 권한 제한도 유지한다.
- **Mac·iOS Development Build / Unity Editor:** 숨김 진입을 사용한다. Mac과 iOS의 차이가 아니라 Development Build 선택 여부가 기준이다.
- 로딩·오프닝·튜토리얼·결과 화면·설정창이 열린 동안에는 진입하지 않는다.
- 방 로비와 전투 설정은 Host만 연다. 참가자에게는 숨김 입력이 클릭을 가로채지 않는다.
- 입력용 Canvas는 화면 레이아웃과 별개이며, Scene·Prefab의 저장된 배치와 원본 에셋은 수정하지 않는다. Scene 제거 때 입력 영역도 제거한다.

## 코드 연결

- `Assets/Hapio/Scripts/UI/GameDeveloperTapTarget.cs`: 3회 터치 집계, Safe Area 입력 영역, 생성·정리.
- `Assets/Hapio/Scripts/UI/GameDeveloperAccess.cs`: Home·Lobby·Battle 허용 조건과 기존 설정창 연결, SOLO 내부 배치.
- `GameScreen` → 메뉴 진입 연결, 기존 DEV 표시 숨김.
- `GameLobbyView` → 원본 SOLO 버튼과 이벤트를 설정창 안에서 사용.
- `GameBattleBindings` → 기존 Host 일시정지·`LabDeveloperMode.Open` 연결. 기존 Lab 단독 모드는 유지.

검사 결과는 `VALIDATION.md`의 #564 항목을 따른다. 실제 iOS 터치·일반 Wi-Fi·핫스팟 실기기 확인 여부는 자동 검사와 구분한다.
