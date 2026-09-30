# 초기 프로토타입 씬·코드·테스트 정리

작성일: 2026-09-30

작업 브랜치: `chore/legacy-prototype-cleanup`

관련 이슈: [#61](https://github.com/DeveloperAcademy-POSTECH/2026-C6-M10-MUSA/issues/61)

## 현재 사용할 실행 경로

| 목적 | 사용 위치 |
|---|---|
| Unity 프로젝트 루트 | 저장소 루트의 `Assets`, `Packages`, `ProjectSettings` |
| 게임 Scene | `Assets/_Project/HapioMVP/Scenes/ContinuousTransferBattle.unity` |
| 공용 게임 설정 | `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset` |
| 요괴 Prefab | `Assets/_Project/HapioMVP/Prefabs/BenchmarkMonster.prefab` |
| 빌드·Scene 검사 | `Assets/_Project/HapioMVP/Editor/ContinuousTransferBuild.cs` |
| 초보자용 기기 빌드 절차 | [BUILD_README.md](../BUILD_README.md) |
| 게임 시작·조작 절차 | [P4_RUNBOOK.md](P4_RUNBOOK.md) |

현재 빌더의 `Prepare()`는 저장된 현행 Scene과 핵심 참조를 검사한다. Scene이 없으면 옛 Scene을 복제해 만들지 않고 중단한다. Build Settings에는 `ContinuousTransferBattle.unity`만 남겼으며, Editor Play에서도 이 Scene을 연다.

## 실제 정리 범위

초기 단계마다 별도 Scene과 전용 빌더·HUD·Controller·Probe·테스트가 추가됐다. 이들은 현재 게임의 실행 경로가 아니었다. 핵심 조합·전투·로비·네트워크 검사를 현행 Scene으로 옮겨 실행한 뒤 다음을 제거했다.

- 옛 HapioMVP Scene 17개: `IOSBuildSmoke`, `DirectConnectionSmoke`, `CounterSmoke`, `BattleLayout`, `OrbInputSmoke`, `ResourceSmoke`, `AttackSmoke`, `CombinationSmoke`, `BattleLoop`, `RoomLobby`, `IntegratedDeviceBattle`, `TwoPlayerBattle`, `InterruptionBattle`, `FivePlayerBattle`, `PhysicsBattle`, `ThrowBattle`, `OrbTransferBattle`. Unity 기본 `SampleScene`과 기본 `TutorialInfo`·`Readme` 템플릿도 제거했다.
- 위 Scene만 만드는 Editor 빌더, 구형 Scene 전용 HUD·Controller·검증 Probe, 별도 `PhysicsSandbox`, 쓰이지 않는 초기 Counter 구현과 빌드 스탬프를 제거했다. 현재 빌드 번호 25에서 실행되지 않는 T12/T13 기기 진단 코드와 해당 프로토콜 테스트도 제거했다. 현행 iOS 전경 유지 후처리와 테스트는 보존했다.
- 구형 Scene만 여는 PlayMode·EditMode 테스트와 이제 비어 있는 테스트 어셈블리 폴더를 제거했다. `SavedRoomLobbyPlayTests`와 `DirectConnectionPlayTests`는 현행 Scene·프로토콜에 맞춰 수정했고, `CurrentBattleFlowPlayTests`에 현행 Scene의 핵심 조합·투척 검사를 추가했다. 기존 씬 테스트의 현행 참조 검사는 `P4SceneTests`와 `EditableBattleUiSceneTests`로 옮겼다.
- 현행 Scene에 연결돼 있던 P4 자동 검증 Probe 컴포넌트를 제거했다. 다른 UI 위치·크기 값은 바꾸지 않았다. 프로젝트 기본 Scene도 현행 전투 Scene으로 바꿨다.

현재 게임의 `DirectConnectionSession`, `T09BattleController`, `T09Hud`, `T10GameSession`, `T10LobbySession`처럼 이름에 T 번호가 있어도 현행 Scene에서 쓰는 코드는 보존했다. `ContinuousTransferBuild`, L1/L2 네트워크 진단 도구, 현재 `OrbArtSet_Placeholder.asset`, URP에서 참조하는 `SampleSceneProfile.asset`도 보존했다. `docs/`의 과거 보고서·검증 기록은 작성 당시의 증거로 남겨 두되, 삭제된 Scene의 현재 실행 안내로 해석하지 않는다.

현행 Scene 검사 중 같은 속성의 음·양 구슬도 조합되지 않는 동작을 발견했다. 조합 후보 검사와 Host 승인 검사에서 ID 문자열 대신 `OrbRecord`의 실제 속성을 비교하도록 두 호출 지점을 수정했다. 현행 조합 테스트가 이를 검증한다.

## 검증 기록

상태는 이번 브랜치에서 **실제로 실행한 범위**에만 붙였다. 자동 테스트 통과를 기기 실행으로 확대 해석하지 않는다.

| 확인 항목 | 상태 | 근거와 범위 |
|---|---|---|
| 삭제 에셋 참조·`.meta` | PASS | 삭제 에셋 GUID 130개에 대해 남은 Unity YAML/설정 참조 0건, `.meta` 누락·고아 0건 정적 점검. Build Settings에는 현행 Scene 1개. |
| 저장된 Scene·Prefab 연결 | PASS | Unity Editor에서 `ContinuousTransferBuild.ValidateSavedScene()` 성공, 누락 컴포넌트·필수 참조 검사 통과. |
| Unity 컴파일 | PASS | Unity 6000.5.7f1 Editor 재컴파일 완료, `failed=false`, 오류 0건. |
| EditMode | PASS | 최신 `main` 변경을 합친 최종 브랜치에서 2026-09-30 전체 1,638개 실행, 1,638 PASS, 0 FAIL. 결과 원본은 작업 호스트의 `/private/tmp/c6-cleanup-final-editmode-20260930.json`에 임시 보관. |
| PlayMode | PASS | 2026-09-30 전체 82개 실행, 82 PASS, 0 FAIL. 결과 원본은 작업 호스트의 `/private/tmp/c6-cleanup-final-playmode-20260930.json`에 임시 보관. |
| macOS/iOS 출력 | NOT_RUN | 이 정리 브랜치에서 새 앱/Xcode 프로젝트를 출력하지 않음. |
| 실기기 설치·조작 | NOT_RUN | 이 정리 브랜치에서 새 빌드를 기기에 설치·실행하지 않음. |

정리 시작 시점 `main`(74134ff7)의 같은 날 PlayMode 기준은 **164개 중 125 PASS, 39 FAIL**이었다. 기존 실패의 다수는 옛 Scene을 여는 테스트였다. 정리 후 테스트 수가 달라졌으므로 두 숫자의 차이를 기능 오류 39건 수정으로 계산하지 않는다. 옮긴 현행 Scene 검사 중 로비 18개, 직접 연결 6개, 전투 흐름 2개는 각각 별도 실행에서도 통과했고 최종 전체 82개 실행에도 포함됐다.

작업 중 최신 `main`의 몬스터 방해 효과 변경(6b799ed4)을 병합했다. 새 검사 하나가 저장된 UI와 다른 텍스트 정렬값을 고정으로 요구해, 방해 효과 참조·입력 차단 검사는 유지하고 정렬값 단정만 제거했다. Scene의 기존 UI 배치는 변경하지 않았다.

과거 [P4 검증](P4_VALIDATION.md), [#56 로비 선택 검증](LOBBY_ELEMENT_SELECTION_IMPLEMENTATION.md), [공개 검증 요약](VALIDATION_SUMMARY.md)은 각 기록의 당시 코드·환경을 설명한다. 해당 기록을 현재 브랜치의 새 빌드·기기 결과로 승계하지 않는다.
