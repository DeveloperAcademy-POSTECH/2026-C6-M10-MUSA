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
