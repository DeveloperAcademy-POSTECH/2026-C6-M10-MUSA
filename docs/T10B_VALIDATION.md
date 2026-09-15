> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-B · 2인 게임 상태 동기화 검증

2026-09-13 **T10-B 구현·코드 검증 PASS**다. 자동 837개, 실제 Mac 두 프로세스의 두 라운드와 동일 상태 비교, iOS 빌드 15의 생성·앱 빌드·개인 Team 서명을 확인했다. 빌드 15 설치·실기기와 G5/T12 전체 Gate는 NOT_RUN이다.

## 선행과 환경

실제 프로젝트는 `/path/to/2026-C6-M10-MUSA`, 비공개 원격은 `prototype-author/C6_Prototype`이다. T10-A의 실제 소스 434개, EditMode 667개·PlayMode 84개, Mac 9개, iOS 빌드 14 서명 근거와 T09 빌드 13의 iPhone G4 기록을 먼저 확인했다. 실제 저장소의 작업 전 파일 890개를 해시로 보존했다.

Unity 6000.5.7f1, URP 17.5.0, NGO 2.13.1, Transport 6.5.0, macOS 26.6.2 arm64, Xcode 26.6(17F113)을 유지한다. 기존 Editor가 열려 있어 `/private/tmp/C6_Prototype_T10B_Verification`에서 검증했다. 기존 Editor·T03 Host·앱 빌드·로그를 종료하거나 삭제하지 않았다.

## 구현 범위

새 `TwoPlayerBattle.unity`에서 로비 시작 합의를 기존 생성·조합·발사·물리·자원·승패 처리에 연결했다. Host Config와 Seed를 런타임 복사본에 적용하며 저장된 Config asset은 바꾸지 않는다. 시작은 구슬 0개·Stamina 100이며, 생성 1개/20·연속 회복 20/3초·실제 명중 공격자 +5를 유지한다.

Host의 논리 Snapshot 하나를 Client가 검증한 뒤 함께 적용한다. 초기 상태 지문 ACK 전에는 시계를 시작하지 않으며, Retry는 새 라운드 초기 상태를 다시 확인한다. OrbId 뷰와 로컬 드래그 위치를 유지한다. T11 전달과 카메라 확장은 추가하지 않았다.

## 자동 시험

EditMode 최종 751/751, PlayMode 86/86, 총 837개 PASS다. 실패·skip·누락은 0이며 기존 751개를 모두 유지하고 86개를 추가했다. 실행 XML과 이전 시험 이름 대조는 증거 파일을 따른다.

- 새 전체 상태 검증: Config·Seed·송신자·owner·동일 라운드/revision·초기 미수신·결과 고정·새 라운드 초기화.
- 승인 응답과 확정 상태 도착 순서, 이전·낮은 revision, Pending 유지와 이후 해제.
- 실제 저장 씬의 한 연결, 자동 시작 차단, 1인 로비 우회 거부, 런타임 Config와 원본 보존.
- 기존 Host 권한·동시/중복 요청·실제 물리·자원·조합·승패·입력 시험 유지.

## 실패 이력

첫 EditMode는 735개 중 723 PASS / 12 FAIL이었다. 새 시험의 이벤트 횟수 초기화 누락과, Unity JsonUtility가 누락된 배열 필드를 기본 빈 배열로 만드는 동작을 확인했다. 두 번째는 744개 중 738 PASS / 6 FAIL로, 명시적인 JSON null 역시 빈 배열로 변환되는 동작을 추가 확인했다. 전송 전 최상위 필드의 존재·중복·실제 배열 토큰을 검증하고, 실제 설치 Unity를 거친 세 번째 747개가 통과했다. null을 빈 구슬 객체로 만드는 이전 inline DTO 동작도 실행으로 재현했다.

첫 Mac 앱 빌드는 개발 전용 Probe에서 설치 Unity가 금지한 GetInstanceID 호출 때문에 실패했다. 동일 뷰 객체의 참조 비교로 바꾼 후 다시 빌드했다. 과거 빌드와 실패 로그를 남겼다. 시험 판정 기준과 기본 180초 시간은 결과에 맞춰 바꾸지 않았다.

첫 Mac 두 프로세스 실행은 각각 게임 흐름 PASS였지만 전체 비교는 FAIL이었다. 동일 라운드/revision 4,222개 중 56개에서 비교 지문이 달랐다. 원시 기록에서 JsonUtility 왕복 후 Host 시간의 소수점 끝자리 약 1e-15 차이를 확인했다. 화면 시간 차이는 409개 표본에서 최대 약 0.051초로 사전 기준 1초 이내였다. 논리 수치 허용 오차 1e-6과 지문 정확 일치 기준을 유지하고, 그 정밀도에 맞는 고정 숫자 표현으로 비교 구현을 보완한다. 첫 실패 결과와 양쪽 기록은 보존한다.

## Mac·iOS·실기기

| 항목 | 결과 | 실제 근거 |
| --- | --- | --- |
| 최종 Mac 앱 빌드 | PASS | BuildReport 오류·경고 0, 최종 출력 `Builds/T10-B-r3/macOS/C6Game.app` |
| 로비 → 초기 게임 ACK → round 1 시작 | PASS | 실제 두 NGO 프로세스, Client 초기 ACK 2초 보류 중 입력·시간 시작 차단 |
| 양쪽 생성·조합·경계 발사·실제 명중 | PASS | 일반 Seed, P1 3회/P2 2회 실제 명중, Host만 총 5회 공격자 +5; Client Rigidbody·회복 발생 0 |
| 응답 누락·중복·이전 round 요청 | PASS | Client 생성 응답 1회 의도적 누락→Query 해제, 생성 패킷 1회 중복과 이전 round 패킷 1회 전송, 추가 생성 없음 |
| OrbId 뷰·로컬 드래그 보존 | PASS | Snapshot 수신 중 동일 뷰 객체와 드래그 위치 확인, 자기 보관 구슬만 표시 |
| Victory·Retry·새 초기 ACK·재시작 | PASS | 결과 고정, round 2 빈 상태 확인 후 Start에서 round 추가 증가 없음, 각자 첫 Generate 1개 |
| 기본 180초·Defeat·공통 시계 | PASS | 두 번째 판 실제 180초, HP100·Time0·TeamHP0의 고정 결과; 370개 비교 표본의 최대 표시 차이 0.051071초 ≤ 사전 기준 1.0초 |
| 같은 상태 번호의 논리 비교 | PASS | 두 round의 Ready/Playing/Victory/Defeat 공통 Snapshot 3,856개, 지문 불일치 0 |
| iOS 프로젝트 생성 | PASS | 실제 Xcode 프로젝트, Portrait·최소 iOS15·Family1,2·Local Network/Bonjour 선언, BuildReport 오류·경고 0 |
| iOS 앱 빌드·서명 | PASS | 빌드15·`com.wolfuraark.c6prototype`·개인 Team `YOUR_TEAM_ID`, 서명·실행 파일/Framework 해시 확인 |
| 빌드15 설치·실기기·Touch·Safe Area·FPS | NOT_RUN | 이전 iPhone 빌드13을 교체하지 않았으며 과거 기기 PASS를 승계하지 않음 |
| 빌드15의 Bonjour 발견방 참가 | NOT_RUN | 이번 Mac 게임 통합은 직접 IP. T10-A 빌드14의 별도 Bonjour 결과는 과거 근거로 보존 |
| 두 iPhone·G5 전체·T12 | NOT_RUN | 후속 실제 기기 Gate |

Mac은 같은 컴퓨터의 390×844 Host와 560×746 Client다. 화면 PNG를 직접 확인했으며, 이를 실기기 Touch·Safe Area·Wi-Fi·FPS로 확대하지 않는다. 두 번째 최종 판의 대기 중 독립 iOS export도 실행했지만, 시간 표시 기준은 바꾸지 않았다.

최종 결과를 동결한 뒤 Client Probe가 명시적으로 종료하면 Host에는 `PARTICIPANT_DISCONNECTED`가 기록된다. 이는 측정 종료 후 정리 단계이며, 이미 동결한 결과·지문은 보존한다. 자동 재접속이나 Host Migration은 추가하지 않았다.

비교 보완 후 실제 관측값의 Unity 반복 전송·문화권·음수 0·정수 최대값 회귀 4개를 더해 최종 EditMode 751개가 통과했고, PlayMode 86개 및 최종 Mac 두 판도 재실행했다. 수치 표현은 사전에 정한 1e-6 정밀도에 맞춰 고정했으며 ID·정수·revision과 지문 정확 일치 조건을 낮추지 않았다.

## 사람 작업과 다음 Task

T10-B 코드 검증과 T12 실기기 Gate는 분리한다. 실제 두 iPhone 조건은 아직 충족되지 않았다. G2에서 승인된 iPhone 17+iPad 대체 결과를 두 iPhone 최종 QA로 확대하지 않는다. 다음 요청 후보는 T11 좌우 전달 하나다. 두 참가자의 Host 권한·구슬 ID·Pending·동기화가 연결된 상태에서 소유자 전달을 추가할 순서다. 아직 T11은 시작하지 않았다.

## 보존 및 적용 차이

실제 저장소에 새 파일 42개와 의도한 기존 파일 변경 13개를 반영했다. 테스트 실행기가 남긴 빈 Resources 폴더의 새 메타는 게임 소스에서 제외했다. 기존 `.meta` 229개·씬/메타 22개·Config 4개·과거 증거 305개를 해시로 보존했다. 검증 사본과 원본의 기존 URP 템플릿 4개 차이는 그대로 기록하고 원본을 유지했다. 패키지 변경·업그레이드·커밋·push·배포는 하지 않았다.

AGENTS는 현재 Task 상태 문단만 갱신하고 이전 규칙을 유지했다. 기존 문서는 새 T10-B 상태를 앞에 추가하며 과거 본문을 보존했다. 새 옵션은 T10-B 씬에서만 활성화되어 기존 씬의 개발 흐름을 유지한다. Git 공백 검사에서 기존 PackageManagerSettings.asset 39행의 후행 공백은 작업 전과 같은 파일임을 확인하고 유지한다.

[원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 자동 시험 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Mac 비교 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: iOS 빌드15 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 대조 — 공개 요약](VALIDATION_SUMMARY.md)

[구현 결정](T10B_DECISION.md) · [구현 구조](T10B_IMPLEMENTATION.md) · [반복 절차](T10B_RUNBOOK.md)
