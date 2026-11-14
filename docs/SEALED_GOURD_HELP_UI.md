# 봉인 박 관람·도움말 UI (#645)

## 원본

- Figma 파일 `wW7ny7lNQ4DXNJ4kD46AuR`: 관람 `4606:4611`, 도움말 화면 `4606:5355`, 도움말 컴포넌트 `4624:1855`, 공통 버튼 `4234:1028`.
- 402×874 기준, 기존 Shilla Culture Bold/Medium 폰트를 사용한다.
- `Art/UI/Strokes/Source/manifest.json`에 원본 노드, 논리 크기와 브러시 바깥 여백을 기록했다. 새 PNG는 원본 Figma 노드를 3배로 내보낸 것으로, 기본/눌림의 GRINDHOUSE 선, 도움말 제목 VERITE 선, 박 리셋 HEIST 선과 단계별 그림 테두리를 포함한다. 결과로 돌아가기는 기존 `ResultAction` 원본을 재사용한다.

## 연결

- `GameBattleBindings`가 `GameHelpView`를 연결한다. 도움말은 로컬 팝업이며 다른 참가자를 일시정지하지 않는다. 전체 화면 Dim이 뒤쪽 uGUI 입력을 가리고, 열 때 진행 중인 보드 드래그를 취소한다. 포인터를 직접 읽는 지귀 부적 쓰기·연기 문지르기는 `LabOrbBoard.ExternalModalOpen`이 켜진 동안 입력을 무시한다. 공유 일시정지와 100% 완료 연출이 시작되면(`IsFinishingBattle`, phase는 아직 Playing) 닫히고 다시 열 수 없다. 일시정지는 기존 버튼과 세션 경로를 유지한다.
- `GameHudButtonArtwork`가 실제 포인터 Down/Up/Exit에 맞춰 두 원본 이미지를 전환한다. 튜토리얼의 전면 탭 레이어에도 두 버튼의 프록시가 있다. 도움말 프록시는 실제 도움말 버튼 위치를 잡은 뒤에만 켜진다.
- `GameScreen`의 기존 관람 RawImage·입력 어댑터·리셋·닫기 버튼에 `GameSealedGourdView`를 연결한다. 이미지 종횡비와 회전/줌 입력은 기존 `GamePreviewAspect`·`GamePreviewInput`이 담당한다. 내부 콘텐츠는 안전 영역에 맞추고 배경은 화면 전체를 채운다.
- `GameGourdPreviewPresentation`은 Hapio 관람 카메라 배경을 투명하게 하고, 박 복사본을 현재 게임 세션에 붙인다. 관람 복사본은 세션과 함께 정리된다. 관람은 박 모델 아래만 복사하므로 전투의 매달림 줄(`Rope`·`Upper Rope`·`Continuous Rope`)은 처음부터 들어가지 않는다.

## 스테이지 배경

`Resources/Hapio/UI/SealedGourdArt.asset`이 세 배경을 연결한다. Story는 현재 stageIndex, 땅따먹기는 기존 배경 기준인 2번을 선택한다.

배경은 402×874 비율을 유지한 채 화면을 덮도록 `uvRect`로 잘라 표시하고, 그림자 위치도 잘린 배경의 바닥선에 맞춘다. 바닥 그림자는 `GameGourdFloorShadow`가 관람용 박의 3D 메시를 스테이지별 바닥 위치에 투영해 만든다. 박과 윗장식이 회전하면 윤곽을 갱신하고, 확대·축소하면 관람 카메라 거리에 맞춰 크기를 2^(2·zoom−1)배로 바꾼다. 안내 글·버튼 글자·그림자 색은 `GameBattleUiColors`의 토큰(PrimaryBrown/400·SecondaryLightIvory/200·PrimaryBrown/700)을 쓴다. 두 렌더 텍스처는 관람을 열 때 생성하고 관람 복사본이 제거될 때 해제한다.

`Hapio/UI/Rebuild Sealed Gourd Backgrounds`는 재생을 멈추고 저장된 Battle 씬을 연 상태에서 실행한다. 각 `GameStageBackgrounds`의 Studio와 바닥만 804×1748 PNG로 만든다. 박·줄·캐릭터·조합대·보드 뒤 덮개·HUD는 캡처하지 않는다. 비활성 스테이지의 `LabEnvironmentCard`는 메시를 명시적으로 Refresh한 후 임시 렌더러에 복사한다. 원본의 재질 속성 블록과 그리기 순서를 유지하며 원본 씬은 저장하지 않는다.

검증 실행 결과와 미검증 플랫폼은 `VALIDATION.md`를 참고한다.

## 확대해도 박이 사각형에서 잘리지 않게

- 관람 박은 디자인 402×874의 y 128 영역 안 402×402 사각형에 그렸다. 그래서 확대하면 사각형 경계에서 박이 직선으로 잘렸다(배경은 화면 전체라 잘린 선이 화면 가운데에 보였다).
- `GameGourdPreviewPresentation`이 `LabGourdResultPreview.ExtendFrame(1.7, 2.8)`을 부른다.
  - 카메라 거리와 확대 범위(zoom 0~1)는 그대로다.
  - 렌더 텍스처를 가로 1.7배·세로 2.8배(512² → 870×1434)로 키우고, 시야각도 같은 배율로 넓힌다. tan(세로 반각)이 2.8배가 된다.
  - 그래서 가운데 사각형은 이전과 같은 크기·위치로 보이고, 확대한 박은 화면 끝에서만 잘린다.
  - 세로 2.8배는 사각형 중심(y≈335)에서 디자인 아래끝까지 덮는 값이다. 가로 1.7배는 iPad처럼 더 넓은 화면을 덮는 값이다.
- `GamePreviewAspect`는 기존 사각형 RawImage를 투명하게 남겨 회전·확대 제스처를 그대로 받게 한다. 회전 속도는 이 사각형 크기로 계산한다.
- 그 뒤에 터치를 받지 않는 `Gourd preview frame` RawImage를 같은 중심에 사각형×배율 크기로 두고 큰 텍스처를 표시한다.
- Lab 관람(`ExtendFrame`을 부르지 않음)은 바뀌지 않는다.


## 관람 복귀 버튼의 대체 배경 (#720)

`결과로 돌아가기`는 기존 ResultAction 원화와 이미지에 의존하지 않는 둥근 단색 배경을 각각 한 번 준비한다. 라이브러리 항목·Sprite·Texture가 유효하면 원화를 쓰고, 사용할 수 없으면 `GameLobbySeatColors.Versus` 색의 대체 배경을 표시한다. 정상 원화의 크기234×45·위치·글자·클릭과 리셋 버튼은 유지한다.

관람 화면을 열 때와 표시 중 참조 상태가 달라졌을 때 다시 확인한다. 같은 이름의 라이브러리 항목이 교체된 경우에도 복귀 버튼만 `GameFigmaSurface.ReloadSource()`로 캐시를 갱신하며, 기존 다른 UI의 `Show(string)` 동작은 바꾸지 않는다. 참조가 복원되면 원화로 돌아간다. 상태가 같을 때 매 프레임 요소를 새로 만들거나 메시를 다시 생성하지 않는다.

이 보완은 참조 누락과 재오픈 경계를 처리한다. 다른 컴퓨터에서 관찰된 실제 문제 빌드의 원인을 재현·확정했다는 뜻은 아니다. Scene 다섯 개로 만드는 일반 빌드에서도 같은 runtime 코드가 사용되며 별도 DEV 설정은 없다.


## 관람 사본 준비와 결과 화면 복구 (#725)

완성 박 관람은 클리어 판정과 별도의 로컬 준비 작업이다. `GameSession.GourdViewerState`는 `Disabled / Pending / Ready / Failed`를 구분한다. 실제 관람 사본과 출력 텍스처가 만들어져야 `Ready`이며 CPU 지도 일치만으로 관람 버튼을 켜지 않는다. 준비 중에는 기존 카드 버튼을 `봉인 박 준비 중…`, 영구 실패에는 `봉인 박 준비 실패`로 표시한다. 정상 준비·기능 꺼짐 상태의 기존 문구와 위치·색·클릭 조건은 보존한다.

- 전투의 승인된 Victory를 받으면 완료 연출 준비와 별개로 표시용 모델을 먼저 보존한다. 이미 모든 그림이 준비된 경우 기존 `LabGourdResultPreview.TryCreate`의 사본을 그대로 사용한다. 기존 완료 처리의 12초 예산을 다시 시작하거나 새 전체 화면 대기를 추가하지 않는다.
- 지연된 경우 `LabGourdPreviewPreparation`이 모델/재질 사본·최종 CPU 색/P 지도·라운드 고정 표현 설정을 소유한다. Battle이 해제돼도 원래 표면 인접 관계나 판정/도색량을 다시 계산하지 않고, 같은 라운드의 디스크 기록과 기존 고해상도/클레이/스탬프 렌더러로 그림을 재생한다. 실제 전투 Rigidbody·Collider·스크립트·오디오는 복사하지 않는다.
- 최종 CPU 지도와 기록의 완료 revision/hash, 전체 연산 적용과 GPU 완료가 일치할 때만 출력 텍스처를 관람 사본에 연결한다. 결과 화면에서 준비가 끝나면 기존 버튼이 활성화된다. 땅따먹기는 기존 청홍 표현과 수채 제외 정책을 유지한다.
- 준비 작업은 실행/스테이지/전투/라운드에 귀속한다. 새 페이지 또는 GPU 적용/완료가 진전하지 않는 유휴 30초에는 실패로 정리한다. 이는 결과 화면 뒤에서 수행되는 복구의 정체 한도이며 플레이 시간이나 새로운 로딩 대기 시간이 아니다. 기록·전송 오류와 원본 live GPU 오류를 `PaintArtRecordingError`로 구분한다. 준비 실패는 클리어·시간·기여도·다음 진행을 취소하지 않으며 불완전한 박이나 이전 박을 대신 표시하지 않는다.
- 다음 스테이지·로비·홈·종료 전에는 작업을 취소하고 사본/재질/GPU 자원을 해제한다. 공유 기록은 Network 소유이므로 준비 작업이 직접 폐기하지 않는다. 완성 자료가 이미 남아 있는 최종 연결 종료와 아직 못 받은 자료가 사라진 경우를 구분한다.

연결 지도: `GameSession.GourdViewer.cs`가 준비 상태와 수명을 관리하고 `GameSession.cs`가 Victory/Scene 전환에 연결한다. `LabGourdPreviewPreparation`은 Battle과 독립적인 재생, `LabGourdResultPreview`는 독립 모델·지도·카메라/출력의 소유를 담당한다. 기존 회전·확대·리셋·복귀와 #720 대체 배경은 유지한다. 실제 검사 범위와 실기기 확인 여부는 `docs/VALIDATION.md`의 #725 기록을 따른다.

최신 종료 흐름(#717/#721)과의 통합에서는 `ResultNeedsPreview`를 공통 조건으로 쓴다. 시간 종료 때 완전히 칠해지지 않은 땅따먹기는 관람 준비 없이 Disabled로 결과를 표시하고, 종료 문구/호루라기의 기존 대기 뒤에 결과로 이동한다. Story의 준비 중 작업 인계는 그 대기 후 Battle 참조 해제 직전에 한다.
