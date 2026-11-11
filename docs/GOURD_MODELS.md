# 박 모델 계약 (#507)

스테이지마다 다른 박 모델을 칠·판정·요괴 반응까지 정식으로 쓰기 위한 계약이다. 계약이 없는 프리팹(흰 박 `Resources/GourdPaint/WhiteGourd`)은 이전 코드 경로를 그대로 탄다.

## 구성 요소

| 코드 | 역할 |
| --- | --- |
| `LabGourdVisualRig` | 박 프리팹 루트에 붙는 계약. 칠·판정·요괴·줄 연결에 쓸 부분을 명시한다. |
| `LabGourdYokaiProfile` | 요괴 클립 안의 시각표·움찔 대상·들썩임. `Legacy`는 흰 박의 기존 상수다. |
| `LabGourdPaint.PrepareVisual(target, prefab)` | `Attach`보다 먼저 스테이지 박을 지정한다. `Attach`는 비어 있을 때만 흰 박을 채운다. |
| `GameStageDefinition.gourdVisualResource` | 스테이지 박 프리팹의 Resources 경로. 비우면 흰 박이다. |
| `GameBattleBindings.MountStageGourd` | Story 모드 현재 스테이지의 경로를 읽어 `PrepareVisual`을 부른다(`Awake` -500, `LabSceneController.Awake` -100보다 먼저). 2:2·Lab 단판·튜토리얼(#477, 1스테이지 설정을 쓰지만 요괴를 내 자리로 돌리는 흰 박 전용 연출)은 흰 박이다. Development 빌드에서 `C6_GOURD_MOUNT ms=`를 남긴다. |

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

- 흰 박의 마운트·판정·칠 binding·요괴 자세는 main에서 기록한 골든(`Assets/Lab/Tests/PlayMode/Golden/`)과 같아야 한다. #473에서 기본 크기를 1.54로 올리며 마운트 골든만 다시 기록했다.
- 1스테이지: `Hapio/Gourds/Stage01_Gumiho_Battle`, 2스테이지: `Stage02_Yagwanggwi_Battle`, 3스테이지: `Stage03_Jigwi_Battle`. 게임 토큰 `C6HapioGame/30`(도입 당시, 현재 기준은 `AGENTS.md`).
- 2스테이지는 #526부터 경량 재출력 `Models/Yagwanggwi_Gourd_100K.fbx`(22렌더러·99,865삼각형, 원본 446,594)를 감싼 `Prefabs/Yagwanggwi_Gourd_100K_GameReady.prefab`에서 굽는다(`GameGourdRigBake.Spec.SourceVariant = "_100K"`). 원본 FBX·GameReady 프리팹은 그대로 보존한다. 새 GameReady는 원본과 같은 루트 배율·모델 위치·Animator 컨트롤러를 쓴다. 두 FBX의 노드 29개와 공연 클립 커브 280개가 같다(최대 차 1.5e-5).
- 흰 박은 Lab 단판, 2:2 땅따먹기(스토리 스테이지 정의가 없음), 튜토리얼 연습 판에만 쓰인다.

## 스테이지 박 굽기 (#507)

`Hapio > Gourds > Bake All Stage Gourds`(스테이지별 `Bake Stage N ...` 메뉴도 있음) 또는 `-executeMethod Hapio.Editor.GameGourdRigBaker.BakeAllFromCommandLine`. 계산은 `Hapio.GameGourdRigBake`(Editor 전용, 테스트가 다시 계산해 저장본과 비교), 저장은 `GameGourdRigBaker`가 한다. 다시 실행하면 같은 GUID의 asset을 제자리에서 덮어쓴다.

스테이지마다 `GameGourdRigBake.Spec`(아트 폴더·요괴 이름·StageDefinition)만 다르다. 세 아트는 같은 계층(`<요괴>_Gourd`/`Vessel_Forward_Tilt`/`Lid_Hinge`/`model_part0`·`model_part3`/`Spirit_Peek_Control`/`Eye_Pop_Control`/`Rear_Rope_*`)을 쓰며, 노드는 Blender 복제 접미사(`.001`)를 뗀 이름으로 찾는다. 칠 아래 바탕은 원래 몸통·뚜껑 재질의 `_BaseMap`이다. 출력은 `Assets/Hapio/Art/Gourds/<스테이지>/Generated/`(`Stage0N_PaintShell`·`LidPaint`·`HitShell`·`YokaiPeek`·`YokaiProfile`·`BodyPaint`·`LidPaint` 재질, `BakeReport.txt`)와 `Assets/Hapio/Resources/Hapio/Gourds/<스테이지>_Battle.prefab`이다.

| 스테이지 | 바깥 면 / 몸통 | star-shaped | 판정 편차 평균·최대 | 시간표(프레임) | 렌더러·삼각형 |
| --- | --- | --- | --- | --- | --- |
| 1 구미호 | 7,262 / 14,336 | 100% | 0.013 · 0.045 | 15/26/29/38/45/47, 주기 48 | 30 · 61,505 |
| 2 야광귀 | 6,114 / 12,000 | 100% | 0.013 · 0.045 | 15/26/29/38/45/47, 주기 48 | 17 · 71,011 |
| 3 지귀 | 7,262 / 14,336 | 100% | 0.013 · 0.045 | 15/26/29/38/45/47, 주기 48 | 17 · 66,637 |

1스테이지 눈의 표정(`Fierce narrowed fox eye` BlendShape) 커브도 요괴 클립에 옮긴다. 1스테이지 눈은 `SkinnedMeshRenderer`라 완성 연출·결과 미리보기 복사(`MeshRenderer`만 복사)에는 빠진다(완성 때는 뚜껑이 닫혀 있다).

아래 표는 2스테이지를 처음(#507, 원본 FBX) 구울 때의 단계 설명이며 1·3스테이지도 같은 단계를 거친다.

| 단계 | 내용 |
| --- | --- |
| 크기 | 몸통 높이를 흰 박 전투 높이 2.390325에 맞춘다. 현재 아트는 이미 맞아 배율 1.00000이다. `referenceVisualSize` 1.4(굽기 기준 크기)에서 배율 1(실행 중에는 `LabGourdVisualRig.StageSizeMultiplier` 1.4를 더 곱한다). #473부터 기본 Gourd Visual Size가 1.54라 전투에서는 흰 박과 같이 1.1배로 그린다. 기본 크기를 바꿀 때 박을 다시 굽지 않는다. 루트 배율 17.096은 자식 `Yagwanggwi_Gourd`로 옮기고 루트는 1이다. |
| 칠 공간 | 원점=몸통 중심, +Y=몸통 축(Blender Z-up과 아트의 정적 30° 앞 기울기). `Paint Shell` transform이 그 기울기를 가지므로 화면 모습은 아트와 같다. |
| 칠 껍데기 | `model_part0.001`(이중 벽 40,960삼각형)에서 원점 방향 광선의 가장 바깥 면만 남겨 20,608삼각형. 원점에서 본 바깥 면 비율(star-shaped) 100%. 몸통 바탕 그림(`model_part0_BaseColor`)은 `_UNDERPAINT_MAP` 재질로 칠 아래에 보인다. |
| 뚜껑 | `model_part3.001`을 닫힌 자세의 같은 칠 공간으로 구워 `Lid_Hinge` 아래 `Lid Paint`로 둔다(뚜껑 바탕 `Image_0`). 열려도 칠이 뚜껑과 함께 움직인다. 칠 셰이더 재질을 가진 extra는 자기 재질을 유지한다. |
| 판정 껍데기 | 바깥 벽과 닫힌 뚜껑의 48×24 방향별 최대 반지름(2,208삼각형, Read/Write). 뚜껑 가장자리와 몸통 입구 사이 틈(극에서 30° 링 48방향)은 같은 열의 위아래 값으로 잇는다. 표면과의 편차 평균 0.013·최대 0.046(몸통 반지름 약 0.9). |
| 줄·장식 | 모델 자체 줄 `Rear_Rope_Strand_1-3`·`Binding`·`Eye_Tie`를 삭제하고, 삭제 전 binding 상단 중심을 `Rope Socket`으로 둔다. 키링과 부속 연결 끈은 아래 #701 정책으로 숨긴다. |
| 요괴 | Animator를 지우고 첫 2초(48프레임) 중 뚜껑·정령·눈 커브만 legacy `Animation` 클립으로 옮긴다. 뚜껑 커브에서 시각을 뽑는다: 열림 시작 15f, 다 열림 26f, 열림 유지 29f까지, 내려감 38f, 거의 닫힘 45f, 닫힘 47f. `idleLoop` 꺼짐(평소 닫힘), 움찔 대상 `Eye_Pop_Control`, 들썩임 로컬 Z 0.00005. |
| 결과 | 렌더러 17개, 311,442삼각형(원본 446,594). #526의 100K 재출력으로 다시 구운 현재 값은 위 표의 71,011이다(목표 60k보다는 크다). |

굽기 결과 최신 여부와 형상 기준은 `GameGourdRigBakeTests`가 검사한다. 아트를 바꾼 뒤 이 검사가 실패하면 굽기 도구를 다시 실행한다.


## 스토리 박 키링 숨김 (#701)

구미호 한복·야광귀 짚신·지귀 불꽃 키링은 전투 프리팹의 `Sandal_Dangling_Pivot` 그룹(Blender 복제 접미사 포함)을 비활성화한다. 이 그룹에는 키링 본체·연결 고리·세 가닥 연결 끈이 함께 있으며, 박 본체·뚜껑·요괴·게임의 매달기 줄과 분리돼 있다. 오브젝트·메시·원본 아트·애니메이션 경로는 보존하고 굽기 도구도 같은 비활성화를 적용한다.

전투와 마지막 일격 다시보기는 같은 박 인스턴스를 사용한다. 완료·봉인 박 관람 복사본은 원본 계층의 active 상태를 보존하므로 키링이 다시 나타나지 않는다. 칠/판정 껍데기·줄 연결점·모델 배율·통신 규약은 그대로다. 위 삼각형 표는 비활성 장식까지 포함한 저장 메시 총량이며 이번 변경으로 원본 메시를 줄였다는 뜻은 아니다.
