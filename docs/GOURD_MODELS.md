# 박 모델 계약 (#507)

스테이지마다 다른 박 모델을 칠·판정·요괴 반응까지 정식으로 쓰기 위한 계약이다. 계약이 없는 프리팹(흰 박 `Resources/GourdPaint/WhiteGourd`)은 이전 코드 경로를 그대로 탄다.

## 구성 요소

| 코드 | 역할 |
| --- | --- |
| `LabGourdVisualRig` | 박 프리팹 루트에 붙는 계약. 칠·판정·요괴·줄 연결에 쓸 부분을 명시한다. |
| `LabGourdYokaiProfile` | 요괴 클립 안의 시각표·움찔 대상·들썩임. `Legacy`는 흰 박의 기존 상수다. |
| `LabGourdPaint.PrepareVisual(target, prefab)` | `Attach`보다 먼저 스테이지 박을 지정한다. `Attach`는 비어 있을 때만 흰 박을 채운다. |
| `GameStageDefinition.gourdVisualResource` | 스테이지 박 프리팹의 Resources 경로. 비우면 흰 박이다. |
| `GameBattleBindings.MountStageGourd` | Story 모드 현재 스테이지의 경로를 읽어 `PrepareVisual`을 부른다(`Awake` -500, `LabSceneController.Awake` -100보다 먼저). 2:2와 Lab 단판은 흰 박이다. Development 빌드에서 `C6_GOURD_MOUNT ms=`를 남긴다. |

프리팹을 직접 참조하지 않고 경로 문자열로 두는 이유: StageCatalog를 읽을 때 모든 스테이지의 모델이 메모리에 올라가지 않게 하기 위해서다.

## `LabGourdVisualRig` 필드

| 필드 | 조건 |
| --- | --- |
| `paintShell` | 칠이 그려지는 몸통 `MeshRenderer`. 로컬 원점은 몸통 중심, +Y가 칠 극축이다. 셰이더의 `normalize(positionOS)`와 `LabPaintSurface.CellFor`가 같은 공간을 쓴다. |
| `paintExtras[]` | 같은 칠 공간을 쓰는 추가 렌더러(예: 뚜껑). 닫힌 자세 기준으로 구운 메시. 몸통과 같은 재질·property block을 받는다. |
| `hitShell` | `paintShell`의 **identity 직계 자식** `MeshFilter`. 렌더러가 없고 메시 Read/Write가 켜져 있어야 한다(런타임 MeshCollider). 판정 메시이며, identity라서 명중 지점의 로컬 좌표가 곧 칠 공간 좌표다. |
| `paintMaterial` | `C6Lab/GourdPaint` 셰이더 재질(`_PaintMap`·`_PaintMapReady`·`_PainterMap`). |
| `ropeSocket` | 줄 연결점. 없으면 판정 상단 중심. 연속 줄(`LabHangingTarget.SetRopeSocket`)에 쓴다. |
| `referenceVisualSize`, `sizeAnchor` | 크기 정책. 배율은 `Gourd Visual Size / referenceVisualSize`이고, `sizeAnchor`(루트 기준 점)가 제자리에 있도록 위치를 옮긴다. 흰 박은 1.8과 위로 0.84 지점에 해당한다. |
| `yokaiAnimation`, `yokaiProfile` | 요괴 반응용 legacy `Animation`과 시간표. Animation이 없으면 요괴 반응이 없다. 시간표가 없으면 흰 박 시간표다. |

`Validate`가 실패하면(필드 누락, hitShell이 identity가 아님, 재질 셰이더 불일치, 시간표 순서 오류 등) `C6_GOURD_RIG_FALLBACK prefab= reason=`을 남기고 흰 박을 마운트한다. 같은 빌드면 모든 기기가 같은 결과를 내므로 판정이 기기마다 어긋나지 않는다.

## 요괴 반응

트리거(콤보 예고·콤보 창·명중 움찔, 폭탄·방해 등장, 섬광 예고·번쩍임, 흔들어 풀기)는 모델과 무관하게 `LabGourdYokai`가 정한다. 프로필에서 읽는 것은 클립 안의 시각(뚜껑 열림·올라옴·두리번·눈 복귀·내려감·닫힘·길이), 두리번 눈 각도, 움찔·튀어나옴 대상 경로(`bodyPath`), 들썩임 축·크기, 평소 반복 여부(`idleLoop`)다. `idleLoop`을 끄면 평소에는 뚜껑이 닫힌 시각(`lidClosedAt`)에 멈춰 있고 게임 이벤트에만 나온다.

## 스테이지 박을 추가하는 절차

1. 아트 원본 프리팹은 수정하지 않는다. 굽기 도구(#507 PR-2 `GameGourdRigBaker`)로 `Resources/Hapio/Gourds/` 아래에 계약 프리팹과 생성 메시·프로필을 만든다.
2. 해당 `StageDefinition.gourdVisualResource`에 경로를 넣는다(굽기 도구가 SerializedObject로 설정).
3. 판정 모양이 바뀌므로 게임 접속 토큰을 올린다.
4. 실제 전투에서 칠·판정·요괴 반응·완성 연출·결과 미리보기·PNG 내보내기를 확인한다.

## 현재 상태

- PR-1(이 문서 도입): 계약과 흰 박 legacy 경로만 있다. 세 스테이지 모두 `gourdVisualResource`가 비어 있어 화면 변화가 없다. 흰 박의 마운트·판정·칠 binding·요괴 자세는 main에서 기록한 골든(`Assets/Lab/Tests/PlayMode/Golden/`)과 같아야 한다.
- 2스테이지 야광귀 박 연결은 PR-2에서 한다.
