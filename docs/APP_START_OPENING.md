# 게임 시작 버튼의 오프닝 (#354 / #356 / #359)

## 현재 흐름

**앱 실행 → 기존 타이틀 → 게임 시작 → 오프닝28컷·28페이지 → 완료 또는 건너뛰기 → (한지 구름, #635) → 튜토리얼(앱 실행당 한 번, #477) → 방 목록**이다. #654의 1–28번 최신 프레임과 23번 사용법 확대 컷을 포함하며 완료 후 게임 시작 버튼을 다시 누르지 않는다. 튜토리얼을 이미 했으면 구름이 걷히며 바로 방 목록이 나온다.

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
| `Assets/Hapio/Story/Opening/Cuts/OP_001.asset`~`OP_028.asset` | 화자·대사 페이지·대기 시간·음성 |
| `Assets/Hapio/Story/Opening/Views/*.prefab` | 원본 그림·레이어 배치·등장 연출 |
| `Assets/Hapio/Story/StoryPreview.unity` | 제작용 독립 미리보기. 기존 타이틀과 다시 보기 유지 |

대사·컷 편집법은 [팝업북 스토리 제작](POPUP_STORY_WORKFLOW.md)을 따른다. 게임용 Prefab과 미리보기는 같은 컷 데이터를 참조하므로 컷 수정이 양쪽에 반영된다. Home의 기존 메뉴·타이틀 배치를 다시 생성하지 않는다.

`Game Opening`은 저장 상태에서 비활성이고 컨트롤러가 재생할 때만 켠다. 컨트롤러는 이 Canvas 밖에 있어야 하며, `player`는 Canvas의 StoryPlayer, `lobbyView`는 기존 Home의 GameLobbyView를 참조한다. Canvas 안의 오디오 리스너도 재생 종료 시 함께 꺼진다. 기존 Home 카메라는 변경하지 않는다.

## 28프레임 개정 (#654)

피그마 `4553:2390`의 1–28번을 순서대로 사용한다. `Revision28.json`에 이미지별 좌표·회전·크롭과 텍스트 구간별 크기를 보존하고, `Hapio > Story > Apply Approved 28 Frame Opening`으로 에셋을 재생성한다. 이 도구는 타이틀·Home 연결·공용 구름 전환을 재생성하지 않는다. 구형 컷 생성 메뉴 대신 이 메뉴를 사용한다.

대사창과 화자 표시는 각 컷의 `Authored Dialogue` 아래에 있으며, 원본 줄바꿈·위치·20/30pt 같은 부분 강조를 네이티브 Text로 유지한다. 공유 대사창은 해당 컷에서 숨긴다. 대사를 수정할 때는 `Revision28.json`과 검토용 `ApprovedScript.json`을 함께 갱신한다.

`OpeningLayerMotion`은 컷의 시간으로 계산하므로 이전 장면으로 돌아가도 위치가 누적되지 않는다. 일반 등장은 그림을 찌그러뜨리지 않는 짧은 이동과 페이드다. 7번 한숨은 왼쪽부터 펼쳐져 오른쪽으로 펼쳐진 뒤 유지되고, 12번 빗자루는 서로 다른 박자로 쓸며, 13번 소품은 포물선으로 날고 떨어진다. 14번 호리병은 기울어 흔들리고, 15번은 2.5초 낙하 후 파편과 연기로 이어진다. 15번 낙하 중에는 탭으로 시간을 건너뛰지 않는다. 16번 요괴는 연기 중심에서 세 방향으로 빠져나가며 연기가 옅어진다.

같은 그림의 9→10번과 19→20번은 `artworkContinuityKey`로 연결한다. `StoryPlayer`가 그림 인스턴스와 재생 시간을 유지하고 새 컷의 대사 그룹만 교체한다. 이전 버튼으로 돌아갈 때도 등장 연출을 다시 시작하지 않는다. 컷 번호와 대사 위치·강조는 각 프레임의 원본을 유지한다.

배경은 `OpeningBackgroundBaker`가 피그마의 sRGB 합성 방식으로 흰 바탕과 미리 합성한다. `Revision28.json`의 `backgroundSourcePath`, `figmaOpacity`, `opacityInSource`로 원본과 적용 방식을 기록한다. 필터가 적용된 피그마 PNG에 이미 들어 있는 투명도를 중복 적용하지 않으며, Unity에서는 불투명 그림으로 표시한다. 4번은 최신 피그마의 불투명도 100%다.

7번 한숨은 등장한 뒤 고정된 모양으로 남는다. 13번의 상자 두 개와 빗자루 두 개는 화면 중간까지 내려오는 서로 다른 포물선을 그린다. 15번은 원본 바닥 그림자를 고정한 채 호리병이 가까워질수록 그림자가 커지고 진해진다.

15번의 파편·연기·그림자 배치는 수정된 Figma를 따른다. 낙하 끝점은 그림자 중심으로 계산하며, 회전한 호리병의 투명 여백 대신 실제 그림의 바닥 접점을 맞춘다. 착지 전까지 온전한 호리병을 유지하고, 바닥에 닿은 뒤 0.12초 동안 그 자리에서 파편으로 전환한다. 파편은 위로 솟는 등장 이동 없이 최종 위치에 나타난다.

마지막 28번은 기존 #635 확대·구름 전환을 그대로 호출하고, 이때 컷 내부의 대사도 함께 숨긴다. 배경은 페이지 바깥으로 2pt 확장해 얇은 흰 가장자리를 방지한다. Live2D 타이틀 에셋은 수정하지 않는다.

## 코드 경로

1. `GameOpeningController.Awake`와 앱 초기 `AfterSceneLoad`는 오프닝을 숨기고 `GameLobbyView`에 컨트롤러를 등록한다. 타이틀은 바로 사용할 수 있으며 이 단계에서 재생·방 검색을 시작하지 않는다.
2. `SubsystemRegistration`에서 앱 실행 내 상태를 초기화한다. `GameLobbyView.EnterGame`이 첫 게임 시작을 `TryBeginFromStartButton`에 전달한다. `StartAfterRelease`는 최소 다음 프레임과 실제 포인터 해제를 기다려 첫 클릭이 첫 컷 진행으로 이어지지 않게 한다. 처리 중 연타는 새 재생을 만들지 않는다. Editor의 Domain/Scene Reload를 끈 실행도 타이틀 초기화 경로를 유지한다. 실제 Editor 옵션 검증 여부는 [검증 기록](VALIDATION.md)에서 구분한다.
3. `GameLobbyView.SetOpeningBlocked`는 기존 배경·메뉴를 숨기며 `LateUpdate`의 재표시를 막는다. 모든 메뉴 버튼의 실제 콜백도 차단한다.
4. `GameScreen`은 Home의 오프닝 중 DEV 표시와 열기·적용을 막는다. 기존 로비·전투·결과의 동작은 유지한다.
5. `StoryPlayer.Finished(Completed/Skipped)` → `ReturnAfterRelease`에서 최소 다음 프레임과 모든 터치·마우스 왼쪽 버튼 해제를 기다린 뒤 `EndPresentation`에서 구독 해제·`Cancel`·Canvas 숨기기를 수행한다. 원래 시작 버튼이 넘긴 다음 단계(`GameSession.StartTutorialOrRooms`: 튜토리얼, 이미 했으면 방 목록)를 한 번 호출한 다음 로비 차단을 해제한다. #635부터는 이 기다림에 "한지 구름이 다 덮일 때까지"가 더해진다(아래 구름 전환). 방 검색은 이때 시작하며 종료 터치로 방 카드까지 누르지 않는다.
6. 참조 누락·재생 실패는 `HAPIO_OPENING_UNAVAILABLE` 경고 후 방 목록으로 진행한다. 이 검사는 실제 게임 시작을 눌렀을 때 수행하며, 컨트롤러 비활성화·Scene 이탈은 정리만 하고 방 검색을 시작하지 않는다. Home Canvas를 잘못 연결해도 메뉴가 포함된 객체를 끄지 않고, 확인 가능한 실제 독립 오프닝 Canvas만 닫는다.

완료·스킵·중단한 시도는 해당 앱 실행에서 소비한 것으로 처리해 Home 진입 때 재시도 루프에 빠지지 않는다. `Cancel`은 재생·컷·음성을 정리하고 AUTO 값은 다음 `Play`에서 초기화한다.

## 컷 전환과 화면 터치 (#356)

- 컷마다 Scene을 로드하지 않는다. `StoryPlayer.ShowCut`은 같은 Cut Host에 다음 Prefab을 만들고 `Sample(0)`과 UI 초기화를 끝낸 뒤 이전 그림을 비활성화·제거한다.
- `StoryLayer.isBackground`가 켜진 레이어는 원래 위치·크기·불투명도1을 유지하고 등장 대기·페이드·흔들림에서 제외한다. 전경 레이어의 기존 등장 시간·펼침·이동·흔들림 값은 보존한다. 원본24컷과 #364의19-1 확대 컷의 첫 배경에 이 역할을 저장했다. 새 배경을 수작업으로 추가할 때도 Inspector에서 이 역할을 지정한다.
- 게임용 Prefab의 `Tap To Continue`는 투명한 전체 화면 입력 객체다. 그림·대사 위, 기존 Safe Controls 아래에 있어 무대사 컷에서도 작동하고 버튼을 가리지 않는다. `StoryTapAdvance`는 누르기가 아닌 손을 뗀 뒤의 클릭만 기존 진행 경로로 전달한다. 드래그·다중 포인터는 취소한다.
- `StoryPlayer.touchOnly`가 켜진 게임용은 AUTO를 실행하지 않는다. 사용자 입력용 `RequestNext/RequestPrevious/RequestSkip`은 같은 프레임 중복 처리를 막고 실제 다중 터치를 차단한다. 제작용 및 기존 코드의 직접 `Next/Previous/Skip` 호출은 동기 API를 유지한다.
- 완료·건너뛰기 직후에도 오프닝 Canvas와 Home 입력 차단을 유지한다. 최소 다음 프레임과 모든 터치·마우스 왼쪽 버튼 해제를 확인한 뒤 방 목록을 열어 종료 입력이 방 카드에 전달되지 않게 한다. 비활성화·참조 오류의 복구는 즉시 정리한다.

## 오프닝 → 튜토리얼 구름 전환 (#635)

검은 "튜토리얼 준비 중…" 화면 대신, 오프닝 마지막 장면에서 바로 튜토리얼로 넘어가는 연출이다.

- **끝까지 보면:** 마지막 장(OP_028)을 넘기는 순간 대사창과 위 버튼(건너뛰기·이전·다음)이 사라지고, 장면이 산골짜기 쪽으로 빠르게 확대된다. 기본은 장면 가로 53.3%·세로 25.4% 지점(산봉우리와 절 지붕 사이)을 향해 0.62초 동안 2.5배이며, 처음 30% 시간에 66%까지 커지고 끝에서 부드럽게 멈춘다. 확대하면서 그 지점이 화면 가운데로 오고, 장면 밖 빈 곳은 어떤 값에서도 보이지 않게 이동량을 막는다. 동시에 #630 공용 한지 구름이 0.62초에 화면을 덮는다.
- **중간에 건너뛰면:** 확대·숨기기 없이 보던 화면 위로 구름만 덮는다.
- **넘어가는 순간:** 구름이 다 덮이고 모든 터치·마우스 버튼이 떨어진 뒤에만 다음 단계(튜토리얼, 이미 했으면 방 목록)를 부른다. 확대한 장면과 숨긴 버튼은 이때 원래대로 돌려 둔다.
- **튜토리얼로 가면:** 전투 장면을 준비하는 동안 구름은 글씨 없이 덮인 채 기다린다(검은 "튜토리얼 준비 중…" 없음). 튜토리얼을 할 수 있게 되면 0.80초 동안 걷힌다. 구름이 떠 있는 동안 튜토리얼 시계가 멈추고, 첫 대사는 구름이 다 걷힌 뒤 원래 대기 시간이 지나 나온다.
- **방 목록으로 가면:** 방 목록이 그려지면 바로 걷힌다. 준비에 실패해 방 목록으로 가도 같다.
- **안전장치:** 다음 화면이 15초 안에 준비되지 않으면 그래도 걷힌다(`GameSession.CloudHoldLimitSeconds`). 구름이 떠 있는 동안에는 아래 화면이 눌리지 않는다. 오프닝이 도중에 끊기면(컨트롤러 비활성화·Scene 이탈) 구름을 바로 치운다.
- **구현:** 구름은 `GameSession` 아래의 `GameCloudCover`가 그린다. 장면이 바뀌어도 남고, 장면 전환 페이드(30100) 위·로딩 화면(30150) 아래(정렬 30120)에 있다. 덮기 → 덮은 채 기다리기 → 걷기를 `Close`·`Open`으로 나눠 쓴다. `GameOpeningController`가 확대·덮기·넘기기를, `GameSession.CloudCover.cs`가 걷는 시점을 정한다.
- **오프닝 없이 튜토리얼을 바로 열면**(검사·개발용 `TryStartTutorial`): 구름이 없으니 예전처럼 "튜토리얼 준비 중…" 가림막이 덮는다.

| 조절할 것 | 위치 |
| --- | --- |
| 확대 지점·배율·시간·진행 곡선·가운데로 끌어오는 정도 | `Assets/Hapio/Resources/Hapio/Story/OpeningTransitionArt.asset` (`Zoom Focus`·`Zoom Scale`·`Zoom Seconds`·`Zoom Curve`·`Focus To Center`) |
| 구름 모양·경로·덮기 0.62초·덮인 채 0.10초·걷기 0.80초 | `Assets/Hapio/Story/StageStories/Views/StoryCloudWipe.prefab` (#630 공용, 스테이지 스토리와 함께 바뀜) |
| 어떤 구름 프리팹을 쓸지 | `Assets/Hapio/Resources/Hapio/UI/CloudCoverArt.asset` |

### 마지막 장면 가장자리 흰 선 (#635)

OP_024 배경 그림(`72eb0.png`)의 맨 오른쪽 한 줄이 반투명(50%)이고, 장면 경계가 화면 픽셀 중간에 걸리면 경계 픽셀에서 밝은 종이 바탕이 섞여 좌우 끝에 흰 선처럼 보였다. 배경 그림 크기만 같은 비율·같은 가운데로 장면보다 2pt씩 키워(x −2 ~ 404) 가장자리를 장면 밖으로 보냈고, 넘친 부분은 장면(`Page 402x874`)의 RectMask2D가 잘라 낸다. 그림 파일은 바꾸지 않았다. 이 프리팹은 `Hapio/Story/Import Prepared Figma Cuts 6-24`로 만든 것이라 그 메뉴를 다시 실행하면 이 조정이 사라진다. 그때는 `StoryWorkspaceTests.LastCutBackgroundReachesPastThePageSoNoPaperLineShows`가 실패해 알려 주며, 같은 크기로 다시 맞춘다.

## 에셋 연결 도구

`Hapio > Story > Connect Opening To Home`은 처음 연결하거나 누락된 연결을 확인하는 Editor 도구다.

- 처음에는 저장된 StoryPreview의 UI만 복제해 게임용 Prefab을 만든다. 원본 그림·컷·대본·미리보기는 덮어쓰지 않는다.
- 미리보기용 타이틀 진입 컴포넌트를 복제본에서 제거하고 실제 Opening 순서에 연결한다. 카메라·라이트·EventSystem을 추가로 복제하지 않는다.
- 이미 유효한 연결이 있으면 그대로 둔다. 잘못된 기존 연결은 오류를 표시해 Inspector에서 수리하도록 한다. 재실행으로 사용자가 조정한 배치를 초기화하지 않는다.
- 수정 중인 Home이 열려 있으면 먼저 저장해야 한다. Play 중에는 실행할 수 없다.
- Home이 Prefab과 순서 데이터를 직접 참조하므로 기존 Hapio 빌드 경로의 에셋 의존성에 포함된다.

`Hapio > Story > Upgrade Opening Touch And Backgrounds`는 #356의 한정 갱신 도구다. 현재 연결된 컷의 기존 첫 배경을 검증한 뒤 배경 역할을 기록하고, 게임용 Prefab에 터치 영역·수동 모드만 연결한다. Home·StoryPreview·그림·대본·RectTransform 배치를 재생성하지 않는다. #654 개정 컷은 고정 배경과 `OpeningLayerMotion`을 사용하므로 이 구형 레이어 업그레이드의 대상이 아니다. `stable` 먹선은 배경 역할과 다르다. 사용자가 배경 구조를 변경해 검증에 실패하면 해당 컷을 직접 확인해야 한다.

## 검증 범위와 남은 확인

`GameOpeningAssetTests`는 저장 연결·초기 숨김·원본 미리보기·Canvas/안전 영역을, `GameOpeningControllerTests`는 최초 타이틀 대기·게임 시작·전체 페이지·메뉴 차단·스킵/방 목록·실패·잘못된 연결 복구를 확인한다. 메뉴 전용 검사는 첫 게임 시작의 오프닝을 실제로 건너뛴 뒤 같은 실행에서 타이틀로 돌아와 기존 메뉴를 검사한다. DEV·SOLO 전용 검사는 초기 타이틀에서 진행한다.

각 변경의 실행 건수·환경·성공/실패와 빌드 여부는 [검증 기록](VALIDATION.md)의 #354·#356·#359/#360 절을 구분해 따른다. #356의 `StoryBackgroundTests`는 배경 고정·다음 배경 준비 순서, `StoryTapAdvanceTests`는 실제 raycast와 터치 이벤트의 한 단계 진행·버튼 우선·취소 경계를 검사한다. 이 검사를 실제 iOS 손가락 조작으로 해석하지 않는다. 오디오 미할당 소스의 정리 검사를 실제 음성 청취로 해석하지 않는다. 13번 컷 효과음 미할당과 iOS 안전 영역·모바일 메모리/성능은 실제 추가 확인이 필요한 범위다.

### Continuous cinematic and authored speaker colors

Frames 14 (2.3 seconds), 15 (3.35 seconds) and 16 (3.2 seconds) opt into `StoryCutView.autoContinueAfterSeconds`. They advance without AUTO or taps, carry excess frame time into the next cut, and ignore surface/Next input during the sequence. Frame 17 returns to manual dialogue progression. Previous replays a cinematic cut from its start; Skip remains available. This is the exception to the default tap-to-continue rule for silent cuts. Frame 4 keeps the pointing-hand artwork stationary from entry so its gourd close-up is not interrupted by a separate hand entrance.

Each authored dialogue badge now uses the existing `Opening/SpeakerPalette.asset` and `SpeakerBadgeColor` material, preserving the brush silhouette and text layout. All six speaker colors and the dark lettering for the yellow badge are applied to the saved prefabs and retained by the revision builder. Dialogue-only swaps (9/10 and 19/20) also replace the speaker colors.
