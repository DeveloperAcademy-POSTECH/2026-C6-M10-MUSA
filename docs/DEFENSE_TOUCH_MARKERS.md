# 방어 입력 위치 안내 (#39)

기준 씬: `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity`

## 화면 동작

몬스터의 경고가 **이 플레이어를 대상으로 할 때만** 상단 전투 영역의 좌우에 방어 안내가 나타난다. 안내는 얇은 원, 손 아이콘, 원 둘레를 채우는 홀딩 진행 표시로만 구성했다. 기존 붉은 화면 가장자리 경고는 유지한다. 두 손을 각각 좌우 방어 영역에서 시작해 누르는 동안 진행 테두리가 1.2초에 걸쳐 차며, 손을 떼면 초기화된다. 로컬 방어 자세가 성립하면 기존과 같은 연녹색으로 바뀐다. 실제 방어 성공은 계속 Host가 판정한다.

씬의 `DefenseZoneLeft`·`DefenseZoneRight`는 기존과 동일한 입력용 `RectTransform`이다. 새 `DefenseTouchLeft`·`DefenseTouchRight`는 그 안에 놓인 **시각적 중심 안내**이며, 각 원의 바깥까지가 정확한 입력 경계라는 뜻은 아니다. 모든 그림의 `Raycast Target`은 꺼져 있어 터치를 가로채지 않는다. 구슬 작업 영역과 팀 시간 바는 수정하지 않았다.

## Unity에서 조정할 곳

Play를 멈추고 `ContinuousTransferBattle`을 연 다음 `T09Overlay → SafeArea → UpperSafeViewport → UpperHudContent → DefenseZoneLeft/Right → DefenseTouchLeft/Right`를 선택한다. 안내의 위치·크기는 이 자식 오브젝트의 `RectTransform`에서 조정한다. 각 자식의 `Outline`, `HoldProgress`, `HandIcon`은 씬에 저장된 uGUI `Image`다. `DefenseTouchMarker`의 Warning/Progress/Stance 색상은 Inspector에서 조정할 수 있다. 입력 가능 범위를 바꾸려는 것이 아니라면 부모 `DefenseZoneLeft/Right`의 크기와 앵커는 그대로 둔다.

손 아이콘은 `Art/UI/DefenseHand.png`, 원 테두리는 `Art/UI/DefenseRing.png`다. 둘 다 스프라이트이며 현재 저장 씬의 `MonsterAttackWarning`이 안내 두 개의 표시 상태를 제어한다. `Apply Defense Touch Markers` 메뉴는 최초 설치용이며 기존 안내가 있으면 중복 생성하지 않는다.

## 검증 범위

2026-09-22, 열린 Unity 6000.5.7f1 Editor에서 스크립트 컴파일이 통과했다. 아래 EditMode·PlayMode 검사는 실제 실행 수 기준이며 실패·건너뜀·미확정은 0개다. iOS 출력과 기기 확인은 별도 단계로 기록한다.

| 검사 | 결과 | 범위 |
|---|---|---|
| `MonsterDefenseInputTests` EditMode | PASS 7/7 | 기존 두 손 입력·경고 상태와 새 손 아이콘·홀딩 테두리 표시/숨김 |
| `EditableBattleUiSceneTests` EditMode | PASS 5/5 | 저장 UI 참조, 두 안내의 기본 숨김과 입력 비차단 |
| `P4SceneTests` EditMode | PASS 6/6 | 입력 영역·경고 참조와 원형 안내 연결 보존 |
| `EditableBattleUiPlayTests` PlayMode | PASS 5/5 | 실제 씬 로드 후 안내 위치·상태 전환, 기존 구슬 생성 및 HUD 회귀 |
| Unity iOS Xcode 프로젝트 출력 | PASS | 앱 빌드 번호 26, `BuildReport.result=Succeeded`, 출력 검증 PASS. `BuildReport.totalErrors=1`이며, 같은 시각 Editor 로그에 외부 명령의 5초 시간 초과가 기록됨 |
| Xcode Debug 앱 빌드·서명 | PASS | 개인 개발 Team의 자동 서명 빌드 종료 코드 0, `codesign --verify --deep --strict` 통과 |
| iPhone 17 설치·실행 | PASS | 앱26 설치·실행 명령 성공, 실행 후 앱 프로세스 유지 확인 |
| iPhone 17 화면 표시 | PASS (사용자 확인) | 새 방어 안내가 표시된 앱 화면이 정상임을 사용자 확인 |
| 실제 다인 공격 중 두 손 홀드·Host 방어 판정 | NOT_RUN | 이번 기기 확인에서 공격·방어 입력 전체 시나리오는 실행하지 않음 |

Unity 출력 기록은 로컬 `/private/tmp/c6-defense39-ios-20260922-01/l2-build-iOS.json`에 있다. 이 임시 경로는 저장소에 포함하지 않는다. PlayMode 검사는 로컬 개발용 fixture를 사용하며, 실제 다인 공격 대상 선정이나 iPhone 두 손 방어 성공을 대신하지 않는다.
