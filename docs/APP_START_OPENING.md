# 앱 시작 오프닝 (#354)

## 현재 흐름

**앱 실행 → 오프닝24컷·26페이지 → 완료 또는 건너뛰기 → 기존 타이틀 → 시작 입력 → 방 목록**이다.

- Hapio의 `Home.unity`가 진입점이다. 기존 `Hapio/Development Build` 경로를 사용한다. Lab 단판과 전투·결과 Scene 목록은 유지한다.
- 현재 ProjectSettings의 Product Name은 기존 `C6_Physics_Lab`이므로 Mac 창에는 그 이름이 보인다. 검증한 앱의 실제 첫 Scene은 Hapio Home이며, 이 연결 작업에서 프로젝트·앱 이름을 변경하지 않았다.
- 오프닝은 앱 실행당 한 번이다. 같은 실행 중 로비·전투·결과에서 Home으로 돌아와도 반복하지 않는다. 앱을 완전히 종료했다가 다시 실행하면 다시 재생한다. 설치 이력이나 PlayerPrefs는 사용하지 않는다.
- 자동 진입은 대사 AUTO와 구분한다. 기본은 수동 다음이며, 기존 이전·다음·AUTO·건너뛰기 조작을 사용한다. 게임용 화면에서는 미리보기의 다시 보기 버튼을 숨겼다.
- 방 생성 전 각 기기의 로컬 표현이다. Host/Join·Ready·전투 동기화 메시지와 규약을 변경하지 않는다.

## 연결과 편집 위치

| 위치 | 책임 |
| --- | --- |
| `Assets/Hapio/Scenes/Home.unity`의 `Game Opening Controller` | 앱 실행 내 재생 상태와 Home 복귀 연결 |
| `Assets/Hapio/Story/GameOpening.prefab` | 편집 가능한 독립 Canvas, 대사·제어 버튼·안전 영역·로컬 오디오 |
| `Assets/Hapio/Story/Opening/Opening.asset` | 실제 오프닝 순서 |
| `Assets/Hapio/Story/Opening/Cuts/OP_001.asset`~`OP_024.asset` | 화자·대사 페이지·대기 시간·음성 |
| `Assets/Hapio/Story/Opening/Views/*.prefab` | 원본 그림·레이어 배치·등장 연출 |
| `Assets/Hapio/Story/StoryPreview.unity` | 제작용 독립 미리보기. 기존 타이틀과 다시 보기 유지 |

대사·컷 편집법은 [팝업북 스토리 제작](POPUP_STORY_WORKFLOW.md)을 따른다. 게임용 Prefab과 미리보기는 같은 컷 데이터를 참조하므로 컷 수정이 양쪽에 반영된다. Home의 기존 메뉴·타이틀 배치를 다시 생성하지 않는다.

`Game Opening`은 저장 상태에서 비활성이고 컨트롤러가 재생할 때만 켠다. 컨트롤러는 이 Canvas 밖에 있어야 하며, `player`는 Canvas의 StoryPlayer, `lobbyView`는 기존 Home의 GameLobbyView를 참조한다. Canvas 안의 오디오 리스너도 재생 종료 시 함께 꺼진다. 기존 Home 카메라는 변경하지 않는다.

## 코드 경로

1. `GameOpeningController.Awake`가 첫 실행의 메뉴 표시·입력을 막는다. `Start` 또는 앱 초기 `AfterSceneLoad`에서 한 번만 `StoryPlayer.Play`를 호출한다.
2. `SubsystemRegistration`에서 앱 실행 내 상태를 초기화한다. Editor 재시작 시 Domain Reload가 꺼진 경우도 초기 진입 대상으로 처리한다. 실제 Editor 옵션 검증 여부는 [검증 기록](VALIDATION.md)에서 구분한다.
3. `GameLobbyView.SetOpeningBlocked`는 기존 배경·메뉴를 숨기며 `LateUpdate`의 재표시를 막는다. 모든 메뉴 버튼의 실제 콜백도 차단한다.
4. `GameScreen`은 Home의 오프닝 중 DEV 표시와 열기·적용을 막는다. 기존 로비·전투·결과의 동작은 유지한다.
5. `StoryPlayer.Finished(Completed/Skipped)` → `EndPresentation`에서 구독 해제·`Cancel`·Canvas 숨기기·로비 차단 해제를 수행한다. 이후 기존 타이틀 버튼이 기존 방 목록을 연다.
6. 참조 누락·재생 실패는 `HAPIO_OPENING_UNAVAILABLE` 경고 후 타이틀로 진행한다. Home Canvas를 잘못 연결해도 메뉴가 포함된 객체를 끄지 않고, 확인 가능한 실제 독립 오프닝 Canvas만 닫는다.

완료·스킵·중단한 시도는 해당 앱 실행에서 소비한 것으로 처리해 Home 진입 때 재시도 루프에 빠지지 않는다. `Cancel`은 재생·컷·음성을 정리하고 AUTO 값은 다음 `Play`에서 초기화한다.

## 에셋 연결 도구

`Hapio > Story > Connect Opening To Home`은 처음 연결하거나 누락된 연결을 확인하는 Editor 도구다.

- 처음에는 저장된 StoryPreview의 UI만 복제해 게임용 Prefab을 만든다. 원본 그림·컷·대본·미리보기는 덮어쓰지 않는다.
- 미리보기용 타이틀 진입 컴포넌트를 복제본에서 제거하고 실제 Opening 순서에 연결한다. 카메라·라이트·EventSystem을 추가로 복제하지 않는다.
- 이미 유효한 연결이 있으면 그대로 둔다. 잘못된 기존 연결은 오류를 표시해 Inspector에서 수리하도록 한다. 재실행으로 사용자가 조정한 배치를 초기화하지 않는다.
- 수정 중인 Home이 열려 있으면 먼저 저장해야 한다. Play 중에는 실행할 수 없다.
- Home이 Prefab과 순서 데이터를 직접 참조하므로 기존 Hapio 빌드 경로의 에셋 의존성에 포함된다.

## 검증 범위와 남은 확인

`GameOpeningAssetTests`는 저장 연결·원본 미리보기·Canvas/안전 영역을, `GameOpeningControllerTests`는 실제 Home 실행·전체 페이지·메뉴 차단·스킵/복귀·실패·잘못된 연결 복구를 확인한다. 기존 Home 관련 검사는 첫 오프닝을 실제로 건너뛴 뒤 원래 흐름을 검사한다.

이번 작업의 실행 건수·환경·성공/실패와 빌드 여부는 [검증 기록](VALIDATION.md)의 #354 절을 따른다. 오디오 미할당 소스의 정리 검사를 실제 음성 청취로 해석하지 않는다. 13번 컷 효과음 미할당과 iOS 안전 영역·모바일 메모리/성능은 실제 추가 확인이 필요한 범위다.
