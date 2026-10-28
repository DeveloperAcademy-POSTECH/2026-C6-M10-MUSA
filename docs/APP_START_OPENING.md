# 게임 시작 버튼의 오프닝 (#354 / #356 / #359)

## 현재 흐름

**앱 실행 → 기존 타이틀 → 게임 시작 → 오프닝24컷·26페이지 → 완료 또는 건너뛰기 → 방 목록**이다. 완료 후 게임 시작 버튼을 다시 누르지 않는다.

- Hapio의 `Home.unity`가 진입점이다. 기존 `Hapio/Development Build` 경로를 사용한다. Lab 단판과 전투·결과 Scene 목록은 유지한다.
- 현재 ProjectSettings의 Product Name은 기존 `C6_Physics_Lab`이므로 Mac 창에는 그 이름이 보인다. 검증한 앱의 실제 첫 Scene은 Hapio Home이며, 이 연결 작업에서 프로젝트·앱 이름을 변경하지 않았다.
- 오프닝은 앱 실행당 첫 게임 시작에서 한 번이다. 같은 실행 중 타이틀로 돌아온 뒤 게임 시작을 누르면 바로 방 목록을 연다. 앱을 완전히 종료했다가 다시 실행하면 다음 게임 시작에서 재생한다. 타이틀의 DEV·SOLO는 오프닝을 자동 시작하지 않는다. 설치 이력이나 PlayerPrefs는 사용하지 않는다.
- 게임용은 화면 터치로 수동 진행한다. 등장 중 터치는 현재 연출을 완료하고, 이후 터치는 다음 대사 또는 다음 컷으로 이동한다. 대사가 없는 컷도 터치를 기다린다. 이전·다음·건너뛰기 버튼은 유지하며 AUTO·다시 보기는 숨긴다. 제작용 StoryPreview의 AUTO·재생·이전 기능은 유지한다.
- 방 생성 전 각 기기의 로컬 표현이다. Host/Join·Ready·전투 동기화 메시지와 규약을 변경하지 않는다.

## 연결과 편집 위치

| 위치 | 책임 |
| --- | --- |
| `Assets/Hapio/Scenes/Home.unity`의 `Game Opening Controller` | 첫 게임 시작의 재생 상태와 방 목록 진행 연결 |
| `Assets/Hapio/Story/GameOpening.prefab` | 편집 가능한 독립 Canvas, 대사·제어 버튼·안전 영역·로컬 오디오 |
| `Assets/Hapio/Story/Opening/Opening.asset` | 실제 오프닝 순서 |
| `Assets/Hapio/Story/Opening/Cuts/OP_001.asset`~`OP_024.asset` | 화자·대사 페이지·대기 시간·음성 |
| `Assets/Hapio/Story/Opening/Views/*.prefab` | 원본 그림·레이어 배치·등장 연출 |
| `Assets/Hapio/Story/StoryPreview.unity` | 제작용 독립 미리보기. 기존 타이틀과 다시 보기 유지 |

대사·컷 편집법은 [팝업북 스토리 제작](POPUP_STORY_WORKFLOW.md)을 따른다. 게임용 Prefab과 미리보기는 같은 컷 데이터를 참조하므로 컷 수정이 양쪽에 반영된다. Home의 기존 메뉴·타이틀 배치를 다시 생성하지 않는다.

`Game Opening`은 저장 상태에서 비활성이고 컨트롤러가 재생할 때만 켠다. 컨트롤러는 이 Canvas 밖에 있어야 하며, `player`는 Canvas의 StoryPlayer, `lobbyView`는 기존 Home의 GameLobbyView를 참조한다. Canvas 안의 오디오 리스너도 재생 종료 시 함께 꺼진다. 기존 Home 카메라는 변경하지 않는다.

## 코드 경로

1. `GameOpeningController.Awake`와 앱 초기 `AfterSceneLoad`는 오프닝을 숨기고 `GameLobbyView`에 컨트롤러를 등록한다. 타이틀은 바로 사용할 수 있으며 이 단계에서 재생·방 검색을 시작하지 않는다.
2. `SubsystemRegistration`에서 앱 실행 내 상태를 초기화한다. `GameLobbyView.EnterGame`이 첫 게임 시작을 `TryBeginFromStartButton`에 전달한다. `StartAfterRelease`는 최소 다음 프레임과 실제 포인터 해제를 기다려 첫 클릭이 첫 컷 진행으로 이어지지 않게 한다. 처리 중 연타는 새 재생을 만들지 않는다. Editor의 Domain/Scene Reload를 끈 실행도 타이틀 초기화 경로를 유지한다. 실제 Editor 옵션 검증 여부는 [검증 기록](VALIDATION.md)에서 구분한다.
3. `GameLobbyView.SetOpeningBlocked`는 기존 배경·메뉴를 숨기며 `LateUpdate`의 재표시를 막는다. 모든 메뉴 버튼의 실제 콜백도 차단한다.
4. `GameScreen`은 Home의 오프닝 중 DEV 표시와 열기·적용을 막는다. 기존 로비·전투·결과의 동작은 유지한다.
5. `StoryPlayer.Finished(Completed/Skipped)` → `ReturnAfterRelease`에서 최소 다음 프레임과 모든 터치·마우스 왼쪽 버튼 해제를 기다린 뒤 `EndPresentation`에서 구독 해제·`Cancel`·Canvas 숨기기를 수행한다. 원래 시작 버튼이 넘긴 `GameSession.EnterRoomBrowser`를 한 번 호출한 다음 로비 차단을 해제한다. 방 검색은 이때 시작하며 종료 터치로 방 카드까지 누르지 않는다.
6. 참조 누락·재생 실패는 `HAPIO_OPENING_UNAVAILABLE` 경고 후 방 목록으로 진행한다. 이 검사는 실제 게임 시작을 눌렀을 때 수행하며, 컨트롤러 비활성화·Scene 이탈은 정리만 하고 방 검색을 시작하지 않는다. Home Canvas를 잘못 연결해도 메뉴가 포함된 객체를 끄지 않고, 확인 가능한 실제 독립 오프닝 Canvas만 닫는다.

완료·스킵·중단한 시도는 해당 앱 실행에서 소비한 것으로 처리해 Home 진입 때 재시도 루프에 빠지지 않는다. `Cancel`은 재생·컷·음성을 정리하고 AUTO 값은 다음 `Play`에서 초기화한다.

## 컷 전환과 화면 터치 (#356)

- 컷마다 Scene을 로드하지 않는다. `StoryPlayer.ShowCut`은 같은 Cut Host에 다음 Prefab을 만들고 `Sample(0)`과 UI 초기화를 끝낸 뒤 이전 그림을 비활성화·제거한다.
- `StoryLayer.isBackground`가 켜진 레이어는 원래 위치·크기·불투명도1을 유지하고 등장 대기·페이드·흔들림에서 제외한다. 전경 레이어의 기존 등장 시간·펼침·이동·흔들림 값은 보존한다. 기존24컷의 첫 배경에만 이 역할을 저장했다. 새 배경을 수작업으로 추가할 때도 Inspector에서 이 역할을 지정한다.
- 게임용 Prefab의 `Tap To Continue`는 투명한 전체 화면 입력 객체다. 그림·대사 위, 기존 Safe Controls 아래에 있어 무대사 컷에서도 작동하고 버튼을 가리지 않는다. `StoryTapAdvance`는 누르기가 아닌 손을 뗀 뒤의 클릭만 기존 진행 경로로 전달한다. 드래그·다중 포인터는 취소한다.
- `StoryPlayer.touchOnly`가 켜진 게임용은 AUTO를 실행하지 않는다. 사용자 입력용 `RequestNext/RequestPrevious/RequestSkip`은 같은 프레임 중복 처리를 막고 실제 다중 터치를 차단한다. 제작용 및 기존 코드의 직접 `Next/Previous/Skip` 호출은 동기 API를 유지한다.
- 완료·건너뛰기 직후에도 오프닝 Canvas와 Home 입력 차단을 유지한다. 최소 다음 프레임과 모든 터치·마우스 왼쪽 버튼 해제를 확인한 뒤 방 목록을 열어 종료 입력이 방 카드에 전달되지 않게 한다. 비활성화·참조 오류의 복구는 즉시 정리한다.

## 에셋 연결 도구

`Hapio > Story > Connect Opening To Home`은 처음 연결하거나 누락된 연결을 확인하는 Editor 도구다.

- 처음에는 저장된 StoryPreview의 UI만 복제해 게임용 Prefab을 만든다. 원본 그림·컷·대본·미리보기는 덮어쓰지 않는다.
- 미리보기용 타이틀 진입 컴포넌트를 복제본에서 제거하고 실제 Opening 순서에 연결한다. 카메라·라이트·EventSystem을 추가로 복제하지 않는다.
- 이미 유효한 연결이 있으면 그대로 둔다. 잘못된 기존 연결은 오류를 표시해 Inspector에서 수리하도록 한다. 재실행으로 사용자가 조정한 배치를 초기화하지 않는다.
- 수정 중인 Home이 열려 있으면 먼저 저장해야 한다. Play 중에는 실행할 수 없다.
- Home이 Prefab과 순서 데이터를 직접 참조하므로 기존 Hapio 빌드 경로의 에셋 의존성에 포함된다.

`Hapio > Story > Upgrade Opening Touch And Backgrounds`는 #356의 한정 갱신 도구다. 승인된24컷의 기존 첫 배경을 검증한 뒤 배경 역할을 기록하고, 게임용 Prefab에 터치 영역·수동 모드만 연결한다. Home·StoryPreview·그림·대본·RectTransform 배치를 재생성하지 않는다. 기존 생성 도구로 컷을 새로 만들었다면 이 업그레이드 메뉴를 다시 실행하거나 새 배경의 `isBackground`를 직접 지정한다. `stable` 먹선은 배경 역할과 다르다. 사용자가 배경 구조를 변경해 검증에 실패하면 해당 컷을 직접 확인해야 한다.

## 검증 범위와 남은 확인

`GameOpeningAssetTests`는 저장 연결·초기 숨김·원본 미리보기·Canvas/안전 영역을, `GameOpeningControllerTests`는 최초 타이틀 대기·게임 시작·전체 페이지·메뉴 차단·스킵/방 목록·실패·잘못된 연결 복구를 확인한다. 메뉴 전용 검사는 첫 게임 시작의 오프닝을 실제로 건너뛴 뒤 같은 실행에서 타이틀로 돌아와 기존 메뉴를 검사한다. DEV·SOLO 전용 검사는 초기 타이틀에서 진행한다.

각 변경의 실행 건수·환경·성공/실패와 빌드 여부는 [검증 기록](VALIDATION.md)의 #354·#356·#359/#360 절을 구분해 따른다. #356의 `StoryBackgroundTests`는 배경 고정·다음 배경 준비 순서, `StoryTapAdvanceTests`는 실제 raycast와 터치 이벤트의 한 단계 진행·버튼 우선·취소 경계를 검사한다. 이 검사를 실제 iOS 손가락 조작으로 해석하지 않는다. 오디오 미할당 소스의 정리 검사를 실제 음성 청취로 해석하지 않는다. 13번 컷 효과음 미할당과 iOS 안전 영역·모바일 메모리/성능은 실제 추가 확인이 필요한 범위다.
