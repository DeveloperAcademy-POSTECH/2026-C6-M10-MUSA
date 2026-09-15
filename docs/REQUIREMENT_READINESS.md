> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T00 / G0 요구사항 Readiness

2026-09-12. 통합본 v1.0 / 검토 r03의 1·2·3.2·5·7·8장을 신규 프로젝트의 실제 파일과 대조한 정적 요구사항 검토다. 아래 `충족`은 요구가 명확하고 구현·시험 경로가 있다는 판단이며, 기능 구현 또는 시험 PASS를 뜻하지 않는다.

대상은 `/path/to/2026-C6-M10-MUSA`이다. `ProjectSettings/ProjectVersion.txt`에서 Unity 6000.5.7f1, `Packages/manifest.json`에서 URP 17.5.0·Input System 1.20.0·Test Framework 1.7.0을 확인했다. `Assets/Scenes/SampleScene.unity`와 템플릿 Readme 스크립트, T00 환경 점검용 Editor 스크립트가 있으며 게임 시스템은 없다. 템플릿·패키지 존재로 게임 검증을 승계하지 않는다.

| 요구 영역 | CLEAR | IMPLEMENTABLE | TESTABLE | MVP-RELEVANT | 판단 근거와 실행 구분 |
|---|---|---|---|---|---|
| 목표 | 충족 | 충족 | 충족 | 충족 | 같은 Wi-Fi의 실제 iPhone 2대에서 협동 전투 흐름 검증으로 한정(2.1). T12 전체 루프와 T16 관찰로 판단. 게임 실행 NOT_RUN. |
| 성공 기준 | 충족 | 충족 | 충족 | 충족 | G1~G6의 기기 증거와 AT-01~18을 구분(5.2·8장). 빌드 A→수정 B, 직접 IP 요청 100회, 기기 드래그 20회, 양방향 전달 100회 등 실행 기준이 있다. 관련 시험 NOT_RUN. |
| 핵심 경험 | 충족 | 충족 | 충족 | 충족 | 화면 경계를 넘는 전달, 교환·음양 조합 협력, 실제 3D 피격 피드백(2.1). T16 사람 관찰로 기술 성공과 협동 경험을 별도 판단. 관찰 NOT_RUN. |
| 핵심 루프 | 충족 | 충족 | 충족 | 충족 | Lobby→Ready/Host Start→생성→전달/조합→공격/회복→Result→Reset(2.1·2.8). Scenario A·D로 연결을 시험. 미구현·NOT_RUN. |
| 입력 | 충족 | 충족 | 충족 | 충족 | Touch 우선, 동일 경로 Mouse 디버그, 한 Pointer/구슬, UI 제외, Attack Zone·Swipe·Drop 중재가 명시(2.7). 기존 Input System 활용 가능; T05/T06 기기 조작 검증 NOT_RUN. |
| P0 | 충족 | 충족 | 충족 | 충족 | 2.3에 기능·보조 검증·제외 범위가 분리됨. 직접 IP는 T02/T03 선행 위험 검증이고 T10-A 자동 탐색을 대체하지 않음. P0 기능 미구현·NOT_RUN. |
| 규칙 | 충족 | 충족 | 충족 | 충족 | 최초 Raw5/비용5 원자성, 이후1/비용1, Host Seed, Yin+Yang, 공격자 유효 hit만 +1, ID·소유권·중복 검증이 정의(2.4~2.9). T07/T08/T14 자동·기기 시험 NOT_RUN. |
| 인원·기기 | 충족 | 충족 | 충족 | 충족 | 실제 iPhone 2대, P1 Host/P2 Client, 세로·Safe Area, 같은 Wi-Fi(2.2). 좌우 상대는 같고 수신 경계는 반대. 사양은 확정; 기기 확보·연결·성능·화면 확인 NOT_RUN. |
| 네트워크 | 충족 | 충족 | 충족 | 충족 | Host 확정, 신뢰성 있는 중요 이벤트, session/round/request/revision, Snapshot, 이탈 시 중단(2.11). 패키지·Discovery 방식은 담당 Task의 공학적 선택. 직접 IP·Discovery·Host-Client 시험 각각 NOT_RUN. |
| 승패 | 충족 | 충족 | 충족 | 충족 | Host 충돌 처리 시각이 종료보다 빠를 때만 피해; Monster HP≤0 및 Team HP>0이면 Victory, 정확한 종료는 Defeat(2.8). 결과 고정·늦은 hit 차단·Reset 이전 요청 거부까지 시험 가능. T09/T10-B 미구현·NOT_RUN. |
| 시나리오 | 충족 | 충족 | 충족 | 충족 | A 정상 루프, B 잘못된 조합/Raw 공격, C 실제 시간 만료·경계·Debug 분리, D Reset/Lobby(7장). 일반 Seed/Fixture·180초/단축시간을 별도 기록. 전부 NOT_RUN. |
| Acceptance | 충족 | 충족 | 충족 | 충족 | AT-01~18 GIVEN/WHEN/THEN과 보강 VT가 있고 구현 Task·실기기 Gate가 연결됨(5.3·8장). XML 실행 수·실패·누락, 빌드·Config·기기·증거를 기록해야 함. AT/VT 실행 NOT_RUN. |

## 요구사항 차단과 미검증 환경

검토한 12영역에서 T00의 정리 또는 다음 T01의 준비 범위를 막는 미해결 사양 충돌은 발견하지 않았다. 이는 원문 예상 `BLOCKER COUNT: 0`의 승계가 아니라 위 항목별 검토 결과다. 기존 게임 구현이 없으므로 기존 3인·자동 회복·오행 구현과의 충돌도 현재 없다.

Signing·Bundle ID·연결 iPhone·신뢰·Developer Mode·Wi-Fi 권한은 요구사항 불명확성이 아니라 이후 빌드·실기기 검증의 환경 의존성이다. 실제 접근/설정 여부와 현재 환경 Blocker 판정은 `IOS_RUNBOOK.md` 및 T00 실행 증거를 따른다. 이 정적 검토로 환경 PASS를 선언하지 않는다. 템플릿 iOS Bundle ID가 그대로인 점은 확인했으며 실제 계정에 맞는 값은 T01에서 확정해야 한다.

## 후속 Task에서 기록할 선택·위험

- T01: 실제 Bundle ID·서명 접근, 세로 화면·Safe Area·Touch 확인, 최소 앱 Build A→수정 B의 설치/실행 증거를 준비한다.
- T02/T10-A: 설치 버전과 iOS 제약의 공식 자료를 근거로 네트워크 패키지·버전 및 LAN Discovery/Bonjour 방식을 선택한다. T00에서 추가 설치하지 않는다.
- T05/T06: 입력 거리·Zone, 발사 위치·물리/Collider/Rigidbody 세부값과 화면 연결감을 `DEMO_TUNING_VALUE`로 기록한다.
- T07: Host Seed 난수 알고리즘·분포·거부 요청의 난수 진행 정책을 `DEMO_ASSUMPTION`으로 기록한다. 일반 Seed와 Fixture를 분리한다.
- T09/T10-B: 표시 보간 허용 오차를 시험 전에 정하고, 같은 revision의 논리 상태와 순간 화면 오차를 구분한다.
- 최초 비용으로 Stamina가 0이 되며 음양 편중·재료 부족·미명중으로 자원이 고갈될 수 있다. T07~T12에서 실제 흐름을 관찰할 검증 위험이며 자동 회복·무료 공급으로 규칙을 바꾸지 않는다. Debug 공급은 별도 시험 준비로 표시한다.

다음 요청 후보는 **T01 / G1 최소 앱 설치·재설치 하나**다. 대상 프로젝트는 위 신규 폴더, 예정 씬은 `Assets/_Project/HapioMVP/Scenes/IOSBuildSmoke.unity`, 예정 Xcode 출력은 `Builds/iOS/T01`이다. 경로 확정은 씬/빌드 산출물 생성이나 실행을 뜻하지 않는다. T01을 자동 착수하지 않으며 G1 실제 iPhone Gate 증거 없이 G2 기능으로 진행하지 않는다.
