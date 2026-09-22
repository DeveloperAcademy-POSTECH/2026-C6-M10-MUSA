# 고목 엿판 조합 영역 UI

기준 씬: `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity` · Unity 6000.5.7f1

## 현재 구성

사용자가 선택한 평면 2D 고목 엿판 시안을 구슬 조합 영역에 적용했다. 분홍색 팀 HP/시간 바는 `T09Overlay/ViewportDivider/TeamTimeBar`로 옮겨 공격 영역과 조합 영역의 경계가 된다. 기존 시간 게이지의 Fill과 `T09Hud` 직렬화 참조는 그대로 사용한다. 생성 버튼과 5칸 스태미나는 `LowerHudContent/ResourceControls` 아래에서 각각 하단 기준 Y 113, 42로 내려갔다. `ResourceControls` 높이는 188에서 152로 줄었다.

실제 구슬 작업 영역의 하단은 `T09Hud.OrbWorkspaceScreenRect`가 `ResourceControls`의 화면 상단으로 계산한다. 따라서 이번 변경으로 구슬의 물리적 이동 가능 영역도 아래로 넓어진다. `OrbPhysicsSandbox`의 같은 기본 footer 높이도 152로 맞췄다.

## 이미지와 그리기 순서

- `Assets/_Project/HapioMVP/Art/UI/WornYeotBoard.png`는 주변 배경 없이 판만 꽉 채운 단독 목재 이미지다. 2D 평면을 정면에서 본 판자와 철제 모서리 장식을 사용한다.
- 씬의 `T04 Orb Camera/OrbWoodenPlate`는 `C6Orbs` 레이어의 `SpriteRenderer`다. 구슬보다 낮은 sorting order 0으로 그려져 구슬을 가리지 않는다. Collider와 입력 가로채기는 없다.
- `OrbWoodenPlateView`는 매 프레임 HUD의 실제 구슬 작업 사각형을 해당 카메라 좌표로 바꿔 목판의 위치·크기를 맞춘다. 화면 비율이나 안전 영역이 바뀌어도 같은 영역을 따른다.
- 이전 `OrbWorkspaceBoundary`는 연결과 편집 기록을 위해 남겨 두되, 그 아래 9개 `Graphic`을 비활성화했다. 이전 반투명 레이어나 청록 선을 다시 켜면 목판 위에 겹친다.

## Unity에서 직접 조정하기

1. Play를 정지하고 `ContinuousTransferBattle` 씬을 연다.
2. 목판 이미지와 색은 `T04 Orb Camera → OrbWoodenPlate → Sprite Renderer`에서 확인한다. 크기와 위치는 실행 중 `OrbWoodenPlateView`가 구슬 이동 영역에 맞춰 다시 계산한다.
3. 시간 바 장식은 `T09Overlay → ViewportDivider → TeamTimeBar`, 버튼은 `SafeArea → LowerSafeViewport → LowerHudContent → ResourceControls → ResourceButtons`, 스태미나는 같은 `ResourceControls → PersonalStaminaPanel`에서 조정한다.
4. 구슬 영역을 더 넓히려면 버튼 위치만 내리지 말고 `ResourceControls`의 높이와 `ScreenLayoutConfig.MinimalBattleFooterHeight` 및 `OrbPhysicsSandbox` 가정도 함께 맞춘다. 변경 후 터치·물리·좌우 전달을 확인한다.

기존 `C6 → UI → Apply Figma Front Artwork` 메뉴는 현재 목판 배치가 있으면 이전 시간 바/버튼 위치로 되돌리는 일을 막기 위해 오류로 중단한다. 새 배치를 한 번 적용하는 전용 메뉴는 `C6 → UI → Apply Worn Yeot Board Layout`이며, 목판이 이미 있으면 중복 생성을 막기 위해 오류로 중단한다.

## 확인 상태

2026-09-22 · Unity 6000.5.7f1 · `ContinuousTransferBattle` 저장 씬 기준.

| 항목 | 상태 | 실제 근거 |
|---|---|---|
| 스크립트 컴파일 | PASS | 열린 Editor의 `recompile_status=completed`, `compilationFailed=false` |
| 저장 UI 씬 EditMode | PASS | `EditableBattleUiSceneTests` 5/5, 실패·건너뜀·미확정 0 |
| 전투 씬 계약 EditMode | PASS | `P4SceneTests` 6/6, 실패·건너뜀·미확정 0 |
| UI·생성 PlayMode | PASS | `EditableBattleUiPlayTests` 4/4, 실패·건너뜀·미확정 0. 목판의 구슬 뒤 정렬·영역 추종과 버튼 1회 생성 포함 |
| 구슬 물리 PlayMode | PASS | `OrbPhysicsSandboxPlayTests` 9/9, 실패·건너뜀·미확정 0. 새 footer 높이와 기존 좌우 전달·속도 유지 포함 |
| 390×844 Game 창 시각 점검 | PASS · Editor 한정 | 정면 배경·요괴·HP/시간 바·고목 엿판·하단 생성/스태미나의 배치와 겹침을 확인 |
| 새 iOS 빌드·실기기 조작·다인 화면 | NOT_RUN | 이번 배치로 새 앱을 출력하거나 iPhone/iPad에 설치하지 않았음 |

이전 기기 검증을 이번 새 화면의 실기기 PASS로 승계하지 않는다.
