# 전투 UI를 Scene에서 직접 편집하기

> 2026-09-22 그래픽 적용 이후의 정면 배경·투명 HUD 에셋·현재 위치는 [정면 전투 화면 그래픽 적용](FIGMA_FRONT_ARTWORK.md)을 먼저 본다. 아래의 초기 Hierarchy와 2026-09-17 실행 수치는 그래픽 적용 전 기록이다.

> #40 구슬 영역 표시: 현재 씬의 `T09Overlay/OrbWorkspaceBoundary`에서 `OrbAreaTint`의 Image Color/Alpha로 반투명 영역을, `UpperEdge*`·`LowerEdge*`·`*Opening`의 Image와 RectTransform으로 경계 스타일을 조정한다. 이 장식은 모두 Raycast Target이 꺼져 있으며 위치·크기는 실행 중 `T09Hud.OrbWorkspaceScreenRect`를 따라간다. 구현·검증 범위는 [구슬 영역 경계선 기록](ORB_AREA_BORDERLINE_PLAN.md)을 본다.

작성일: 2026-09-17 · Unity 6000.5.7f1 · 대상: `ContinuousTransferBattle.unity`

현재 전투 화면은 **Scene에 저장된 Canvas와 자식 GameObject를 선택해서 수정하는 구조**다. 글자 위치·크기·패널 색상·버튼 배치를 바꾸기 위해 `T09Hud.cs`의 생성 코드를 고칠 필요가 없다. 게임 상태를 표시하고 버튼 입력을 처리하는 기존 코드 연결은 유지한다.

이 문서는 현재 전투 UI의 편집 방법이다. 과거 보고서에서 설명하는 “실행할 때 전투 Canvas를 생성한다”는 방식은 이전 시험 씬에 남아 있으며, 이번에 전환한 현재 게임 씬과 구분한다. 로비 UI·방 목록, 동적으로 생성되는 구슬·투척 미리보기의 제작 방식은 이번 범위에서 바꾸지 않았다.

## 1. 먼저 열어 볼 위치

1. Unity Hub에서 이 저장소 루트를 Unity 6000.5.7f1로 연다.
2. Play를 정지한 상태에서 Project 창의 [ContinuousTransferBattle.unity](../Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity)를 더블 클릭한다. 다른 시험 게임 씬을 함께 실행하지 않도록 이 씬을 연다.
3. Hierarchy에서 `P4ContinuousTransferBattle`을 펼치고 자식 `T09Overlay`를 선택한다. 메뉴 **C6 → UI → Select Battle Canvas**로도 선택할 수 있다. 이 메뉴는 대상 씬이 닫혀 있으면 추가로 여므로, 실행 전 열린 씬 구성을 확인한다.
4. Scene 창에서 2D 보기를 켜고, 선택한 오브젝트에 `F`를 눌러 화면을 맞춘다. Game 창을 함께 열면 실제 화면 비율에서의 배치를 확인하기 쉽다.
5. 저장할 수정은 Play 모드 밖에서 하고 Scene을 저장한다. Play 중 바꾼 위치와 글자는 정지할 때 되돌아갈 수 있다.

`T09Overlay`에는 `Canvas`, `Canvas Scaler`, `Graphic Raycaster`가 있다. 현재 기준은 **Screen Space - Overlay**, 기준 해상도 **390 × 844**, `Scale With Screen Size`다. 기존 터치 좌표 계산은 이 Canvas 방식에 맞춰져 있다.

핵심 Hierarchy는 다음과 같다. 중간의 장식·진단용 항목은 생략했다.

```text
P4ContinuousTransferBattle
└─ T09Overlay                         ← 전투 Canvas
   ├─ SafeArea
   │  ├─ UpperSafeViewport
   │  │  └─ UpperHudContent
   │  │     ├─ Header
   │  │     │  ├─ Brand
   │  │     │  ├─ BattlePhase
   │  │     │  └─ ConnectionStatus
   │  │     ├─ BattleStats
   │  │     │  ├─ MonsterHpPanel
   │  │     │  └─ PersonalStaminaPanel
   │  │     ├─ BattleClock
   │  │     ├─ TeamHp
   │  │     ├─ PersonalStorage
   │  │     ├─ ResourceMode
   │  │     └─ ResourceProgress
   │  └─ LowerSafeViewport
   │     └─ LowerHudContent
   │        └─ ResourceControls
   │           ├─ ActionStatus / ActionDetail
   │           ├─ ResourceButtons
   │           │  ├─ GenerateButton
   │           │  │  └─ Caption
   │           │  └─ DebugFixtureButton
   │           └─ SessionButtons
   │              ├─ DevHostButton / JoinButton
   │              ├─ HostStartButton
   │              └─ EndTestButton
   ├─ ViewportDivider
   └─ ConfirmedBattleResult            ← 보통 비활성 상태
      └─ ResultSafeArea
         └─ ResultPanel
            ├─ ResultTitle / ResultCaption
            ├─ FinalBattleValues / ResultNextStep
            └─ ResultActions
               ├─ HostRetry
               ├─ HostLobby
               └─ CloseSession
```

## 2. 바로 해 볼 수 있는 편집 예시

### 예시 A — 제목 위치와 크기 바꾸기

`UpperHudContent → Header → Brand`를 선택한다. Inspector의 `Text`에서 글자·Font Size·Color를 바꾼다. 이 프로젝트는 이 UI에 uGUI의 기존 `Text` 컴포넌트를 사용한다.

위치는 `Rect Transform`에서 조절한다. `Rect Tool`로 드래그하거나 Inspector의 좌표를 바꾼다. Anchor가 한 점에 고정돼 있으면 주로 `Pos X / Pos Y / Width / Height`가 보이고, 좌우로 늘어나도록 설정돼 있으면 `Left / Right` 같은 여백 값이 보인다. 위치 입력 칸이 다르다고 다른 UI 방식인 것은 아니다.

처음에는 Anchor와 Pivot을 유지하고 위치·여백·크기만 조금씩 바꾸는 편이 결과를 이해하기 쉽다. `Brand`의 글자와 색은 현재 상태 갱신 코드가 바꾸지 않으므로 저장한 편집을 그대로 사용한다.

### 예시 B — Generate 버튼 모양과 배치 바꾸기

`LowerHudContent → ResourceControls → ResourceButtons → GenerateButton`을 선택한다.

- 버튼 크기와 위치: `Rect Transform`을 수정한다. 여러 버튼을 함께 옮길 때는 부모 `ResourceButtons`를 옮긴다.
- 배경: `Image`의 Color 또는 Source Image를 수정한다.
- 눌림·비활성 색: `Button`의 Transition이 Color Tint일 때 Colors 값을 수정한다.
- 글자 크기·정렬: 자식 `Caption`의 `Text`를 수정한다.

`GENERATE / 20` 문구 자체는 실행 중 생성 비용과 대기 상태에 맞춰 코드가 갱신한다. 위치·글꼴 크기와 동작 상태에 따른 문구는 별개의 설정이다. 비용을 바꾸려면 UI 글자만 고치는 것이 아니라 게임 Config와 자원 규칙을 수정해야 한다.

`Button → On Click()`이 비어 있어도 정상이다. **`T09BattleController.Start()`가 실행 중 기존 버튼에 기능을 등록한다. 같은 Generate·Start·Retry 기능을 Inspector에 다시 추가하지 않는다.** 같은 기능을 중복 등록하면 한 번 클릭했는데 여러 요청이 발생할 수 있다.

### 예시 C — 스태미나 패널 꾸미기

`BattleStats → PersonalStaminaPanel`의 `Image` 색과 `Rect Transform`을 바꾼다. 자식 `StaminaValue`는 숫자, `ContinuousStaminaTrack`은 게이지 배경이다. 그 안의 `ConfirmedStaminaFill`은 실제 회복량에 따라 길이가 변하는 채움 부분이다.

채움 부분의 색은 편집할 수 있지만, 가로 Anchor의 최대값은 실행 중 자원 비율로 갱신된다. 게이지 전체 크기를 바꾸려면 부모 `ContinuousStaminaTrack`을 수정한다. `TwentyMarker1`~`TwentyMarker4`는 20 단위의 시각적 구분선이다.

### 예시 D — 결과 화면 미리 보기

Play가 정지된 상태에서 `T09Overlay → ConfirmedBattleResult`를 선택하고 Inspector 맨 위 활성 체크를 켠다. `ResultPanel`의 크기·배경, 결과 글자, `ResultActions`의 버튼을 편집한다. `FinalBattleValues`와 `ResultNextStep`에 예시 문구를 넣어 긴 숫자가 들어갈 공간도 확인할 수 있다.

편집을 마치면 결과 Overlay를 다시 비활성화하고 저장한다. 실행 시에는 코드가 처음에 숨기고 실제 Victory/Defeat에서 표시하며 결과 문구와 Host 전용 버튼의 표시 여부를 갱신한다. 결과 Overlay가 켜진 동안에는 뒤쪽 화면의 입력을 가린다.

현재는 Client 결과 화면에서 `CloseSession`을 자동으로 전체 너비로 늘리지 않는다. Scene에서 지정한 위치를 유지하므로, Host 전용 Retry/Lobby 버튼이 숨겨진 모습도 배치 검토에 포함한다.

## 3. 직접 편집하는 값과 코드가 관리하는 값

| 대상 | 직접 편집할 부분 | 계속 코드가 관리하는 부분 |
|---|---|---|
| `Header`, `BattleStats`와 개별 글자·패널 | 위치·크기·여백·글자 크기·정렬·대부분의 색 | HP·시간·참가자·연결 상태·구슬 수 같은 실제 값 |
| `ResourceButtons`, `SessionButtons`와 개별 버튼 | 배치·배경·눌림 스타일·Caption 크기 | 클릭 기능, interactable, 일부 진단 버튼의 표시 여부 |
| `ResourceControls` | 하단 패널의 배치·높이·색 | 패널 위쪽 경계를 하단 구슬 작업 영역의 시작점으로 사용 |
| `ContinuousStaminaTrack` | 전체 위치·크기·색 | `ConfirmedStaminaFill`의 채움 비율 |
| `ConfirmedBattleResult`와 내부 패널 | 패널·글자·버튼의 배치와 스타일 | 표시 시점·결과 수치·Host/Client 버튼 표시·승패 제목 색 |
| `ResourceMode` | 위치·크기·정렬 | 일반/진단 문구와 상태 색 |
| `SafeArea`, `ResultSafeArea` | 자식 UI의 배치를 편집 | 실행 기기의 안전 영역에 맞춘 Anchor |
| `UpperSafeViewport`, `LowerSafeViewport` | 그 안의 자식 UI를 편집 | 상하 카메라 영역과 안전 영역에 맞춘 범위 |
| `UpperHudContent`, `LowerHudContent` | 그 안에 배치한 패널·글자를 편집 | 화면 비율에 따른 컨테이너 위치·너비·축척 |
| `ViewportDivider` | 선 색 등 시각 속성 | 상하 화면 경계 위치와 선 두께 |

안전 영역과 상하 컨테이너는 해상도가 달라져도 UI가 화면 밖으로 밀리지 않도록 관리한다. 그 컨테이너 자체의 위치를 바꾸면 화면 크기 갱신 때 다시 맞춰질 수 있다. 일반적인 화면 재배치는 그 **안의 패널·글자·버튼**을 옮겨서 진행한다.

하단 `ResourceControls`의 현재 기본 높이는 144다. 높이거나 위로 올리면 구슬이 움직이는 빈 영역이 줄어든다. 이 경계는 실제 구슬 배치·크기 계산에도 사용하므로 큰 변경 후에는 굴림·좌우 전달·투척 조작을 함께 확인한다.

상단 `BattleClock`, `TeamHp`, `PersonalStorage`와 하단 쪽 `ResourceMode`, `ResourceProgress`의 화면 좌표는 `ThrowBattleFraming`이 몬스터를 배치할 빈 공간을 계산할 때 사용한다. 이 글자를 몬스터 쪽으로 옮기면 카메라가 몬스터를 보여 주는 크기·위치도 달라질 수 있다. 몬스터 영역을 완전히 새로 설계할 때는 이 연결도 함께 조정한다.

## 4. 기존 코드 연결을 유지하는 방법

`P4ContinuousTransferBattle`을 선택하면 Inspector에 `T09 Hud`가 있다. 이 컴포넌트의 Canvas·Button·Text·RectTransform 참조가 방금 편집한 GameObject들을 가리킨다. 오브젝트 이름을 적어 찾는 것과 달리 **Scene에 저장한 참조**로 연결돼 있다.

| `T09Hud` 참조 | 연결되는 오브젝트 예시 | 역할 |
|---|---|---|
| `Canvas` | `T09Overlay` | 로비/전투 화면 전환 시 전투 UI 전체를 표시·숨김 |
| `Generate Button` | `GenerateButton`의 Button | 기존 생성 요청으로 연결 |
| `Generate Caption` | `GenerateButton/Caption`의 Text | 비용·대기 문구 표시 |
| `Start Button` | `HostStartButton`의 Button | 기존 Host Start로 연결 |
| `Hp Label`, `Clock Label`, `Stamina Label` | `MonsterHpValue`, `BattleClock`, `StaminaValue`의 Text | 게임 상태 표시 |
| `Footer` | `ResourceControls`의 RectTransform | 구슬 영역의 아래 경계 계산 |
| `Result Overlay`, `Retry Button` | `ConfirmedBattleResult`, `HostRetry` | 결과 표시와 기존 Retry 요청 |

Inspector의 표시 이름에는 공백이 들어갈 수 있다. 코드의 `GenerateButton`과 Inspector의 `Generate Button`은 같은 참조다.

기존 오브젝트의 위치·색·크기를 바꾸면 이 참조는 유지된다. 버튼을 새것으로 교체하거나 컴포넌트를 삭제했다면 해당 참조를 다시 연결해야 한다. 예를 들어 Generate 버튼 교체에는 `Generate Button`뿐 아니라 `Generate Caption`도 새 버튼의 자식 Text로 지정한다. 새 Button의 Target Graphic도 자신의 Image를 가리켜야 한다.

`T09Hud`와 `T09BattleController`, `SplitScreenLayout`은 기존 게임 루트에 그대로 둔다. Canvas는 같은 씬의 그 루트 아래에 유지하고, 연결된 버튼도 Canvas 안에 둔다. 단순 배치를 위해 전체 루트를 새로 만들거나 컴포넌트를 떼어 옮길 필요가 없다.

진단용 Host/Join/Solo 입력은 협동 게임에서 숨겨지지만 기존 코드와 과거 시험 씬의 연결을 위해 참조가 남아 있다. 안 보이는 항목도 삭제하기 전에 연결 용도를 확인한다. 비활성 항목도 연결 검사 대상이다.

## 5. 편집 후 확인 순서

1. Scene을 저장한다.
2. **C6 → UI → Validate Battle UI Connections**를 실행한다. 성공 로그는 `C6_EDITABLE_UI_CONNECTIONS_VALID`다. 필수 참조 누락, Canvas 모드·소속, 기존 컨트롤러 연결, Canvas 내부 Missing Script를 검사한다.
3. Game 창에서 휴대폰의 세로 비율과 iPad처럼 넓은 비율을 각각 확인한다. 글자가 잘리는지, 버튼이 겹치는지, 상하 빈 영역이 남는지 본다.
4. Play에서 협동 로비의 기존 방 생성·참가·Ready·Host Start 흐름을 거쳐 전투 화면을 확인한다. Play 직후 로비가 표시되는 것은 기존 게임 흐름이다.
5. Generate를 한 번 눌러 요청과 구슬이 한 번만 처리되는지 확인한다. 하단 패널이나 상단 상태 표시를 크게 움직였다면 구슬 조작과 몬스터 표시도 확인한다.
6. Play를 정지한 뒤 편집한 위치가 Scene에 남아 있는지 확인한다.

연결 검사 성공은 배치 품질·다인 동기화·iOS 빌드·실제 터치 시험의 성공을 의미하지 않는다. 큰 UI 변경의 최종 확인은 대상 기기에서 수행한다.

**C6 → UI → Prepare Editable Battle UI**는 최초 전환용 메뉴다. 이미 전환한 씬에서는 기존 UI를 검사하고 선택하며 재생성하지 않는다. 버튼을 옮긴 뒤 이 메뉴를 실행해도 기본 배치로 초기화하는 용도로 동작하지 않는다. 참조가 끊겼다면 Inspector에서 원래 대상에 다시 연결하고 검사한다.

## 6. 유지보수할 때 읽을 코드

| 파일 | 확인할 메서드/부분 | 알아볼 내용 |
|---|---|---|
| [T09Hud.cs](../Assets/_Project/HapioMVP/Battle/T09Hud.cs) | 직렬화 참조, `Awake`, `ValidateSceneHierarchy` | 저장된 Canvas를 사용하고 필수 연결을 확인하는 방식 |
| 같은 파일 | `SetResources`, `SetBattle`, `SetStatus`, `SetControls` | 실행 중 바뀌는 숫자·문구·버튼 상태 |
| 같은 파일 | `RefreshRegions`, `OrbWorkspaceScreenRect` | 화면 비율 대응과 하단 구슬 영역 |
| [T09BattleController.cs](../Assets/_Project/HapioMVP/Battle/T09BattleController.cs) | `Start`, `GenerateOrb` | 버튼 이벤트를 기존 생성 요청으로 연결하는 위치 |
| [T10GameSession.cs](../Assets/_Project/HapioMVP/GameSync/T10GameSession.cs) | `SetGameVisible`, `UpdateHud` | 로비/전투 전환과 동기화된 상태 표시 |
| [ThrowBattleFraming.cs](../Assets/_Project/HapioMVP/Battle/ThrowBattleFraming.cs) | `TryEffectiveRect`, `ApplyFraming` | UI 사이에 몬스터가 보이도록 카메라를 맞추는 연결 |
| [EditableBattleUiTools.cs](../Assets/_Project/HapioMVP/Editor/EditableBattleUiTools.cs) | `Prepare`, `ValidateCurrent` | Unity의 오브젝트·씬 저장 API로 UI를 준비하고 확인하는 메뉴 |

`T09Hud`에는 과거 씬용 `CreateUI` 코드가 남아 있다. 현재 씬의 모양을 고치려는 목적이라면 이 코드를 수정하지 않고 Scene을 편집한다. 과거 시험 씬까지 같은 UI로 바꾸는 작업은 별도 전환으로 진행한다. `.unity`나 `.meta`의 내용을 텍스트로 직접 고쳐 참조를 연결하지 않는다.

## 7. 이번 변경의 실행 확인 기록

2026-09-17, Unity 6000.5.7f1에서 실행했다. 현재 작업 폴더의 실제 Editor에서 씬을 생성·저장하고 연결 검사 메뉴를 실행했다. 자동 검사는 열려 있는 작업 씬과 충돌하지 않도록 소스·씬·패키지를 대조한 별도 검증 복사본에서 실행했다. 그 폴더를 Unity Hub나 GitHub Desktop의 개발 저장소로 등록하지 않았다.

| 확인 항목 | 상태 | 실제 실행 근거 |
|---|---|---|
| 변경 소스 컴파일 | PASS | 실제 Editor 메뉴 실행 및 자동 검사 실행 완료 |
| 전체 EditMode | PASS | 1,438개 실행·1,438개 통과·실패0·누락0. 새 UI 검사5개 포함 |
| 전체 PlayMode | FAIL | 149개 실행·144개 통과·실패5·누락0. 아래 기존 오류와 구분 |
| 현재 씬의 새 UI PlayMode | PASS | 3개 실행·3개 통과. 저장 Canvas/버튼 유지, 자원·결과 갱신, 클릭1회→Raw1개·비용20 |
| 변경 전 기준본의 실패 항목 재실행 | FAIL · 기존 문제 확인 | 기준 커밋 `8b59ea5`에서도 같은5개가 같은 예외로 실패 |
| 실제 Editor 씬·참조 확인 | PASS | Canvas 내부75개 Transform, 연결 검사 메뉴 성공, 세로 비율 Scene에서 Generate의 RectTransform·Image·Button 편집 항목 확인 |
| 이번 변경의 새 앱 빌드·실기기 터치·다인 실행 | NOT_RUN | 이번 작업에서 수행하지 않음. 이전 앱26의 결과를 승계하지 않음 |

전체 PlayMode에서 실패한 항목은 `ApprovedGameScenePlayTests` 2개와 `InterruptionScenePlayTests` 3개다. 과거 `TwoPlayerBattle`·`InterruptionBattle` 씬에 `ThrowBattleFraming`이 없어 `T10GameSession.Awake()`에서 예외가 발생한다. 변경 전 기준본에서도 같은 실패를 재현했으며 이번 작업에서 해당 과거 씬과 게임 코드를 수정하지 않았다. **전체 검사 통과로 기록하지 않는다.**

새 PlayMode 검사는 현재 저장 씬을 실제로 로드하되 로비 감독을 끄고 기존 개발용 단독 Host 흐름을 사용하는 명시적 UI 시험이다. 다인 로비 성공이나 실제 기기의 Touch 성공 증거는 아니다. 버튼 한 번당 요청 한 번, 생성 비용20, Raw1개, 편집한 위치·색·글자 크기 보존, 결과 버튼 위치 유지, 구슬 작업 영역과 몬스터 화면 범위를 확인했다.

검사 작성 중 Unity 6.5에서 제거된 `GetInstanceID()`를 `GetEntityId()`로 수정했다. 카메라가 없는 미리보기의 Canvas 모드 자동 보정 때문에 불명확했던 조건은 `WorldSpace` 거부 검사로 바꿨다. 수정 전 실패 기록과 최종 XML을 구분해 보관했으며 위 숫자는 수정 후 결과다.

보존 검사에서는 기존 사용자 변경13개가 바이트 단위로 동일했고, 현재 씬의 기존 직렬화 오브젝트171개 중 삭제0개였다. 기존 항목 변경은 게임 루트의 자식 연결과 `T09Hud` 참조2개뿐이며 UI와 EventSystem에 필요한 항목을 추가했다. `.meta`의 기존 GUID, 다른 씬·Prefab·게임 로직·Config·패키지를 교체하지 않았다.

공개 가능한 실행 수·소스 해시·XML 해시는 [검증 요약 JSON](validation/EDITABLE_BATTLE_UI_20260917.json)에 기록했다. 원시 XML·Editor 로그·기준본 비교·보존 검사 원본은 작업 머신의 비공개 임시 검증 기록에 보관하며 공개 저장소에는 포함하지 않는다.

작업 당시 열려 있던 미저장 `CounterSmoke` 씬은 강제 저장하거나 닫지 않았다. 전투를 Play할 때는 `ContinuousTransferBattle`을 단독으로 열고, Unity가 다른 씬의 저장 여부를 묻는다면 그 씬의 본인 변경을 확인해서 처리한다.
