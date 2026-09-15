> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

<!-- C6:P4:CURRENT:BEGIN -->
## 2026-09-14 P4 빌드24 · 좌우 연속 이동 완료

새 `ContinuousTransferBattle`에서 굴러가는 Raw/Combined가 좌우 끝을 넘어 이웃 화면으로 자동 이동하며 남은 속도·방향·상대 높이를 이어받는다. 직접 드래그 조합·상하/구슬 반발·손떼기3D 투척·최대5인은 유지한다. 잡은 구슬은 손을 놓은 뒤 움직이고, 수신20개 한도 거부 때는 원래 끝에서 정지한다.

자동1,427개·Mac2/3/4/5인32전달과공통상태1,449개(불일치0)·iOS빌드/서명/설치·iPhone17+iPad mini6의 실제Raw자동8전달/사용자정지·화면확인은각범위PASS다. 실기기Combined/3~5대/전체상태비교는NOT_RUN이며 [현재 검증](P4_VALIDATION.md)·[구현 결정](P4_IMPLEMENTATION.md)·[실행 안내](P4_RUNBOOK.md)를 따른다. 아래 미착수/빌드23 기록은 당시 상태로 보존한다. 후속 기능·GitHub 업로드는 자동 진행하지 않는다.
<!-- C6:P4:CURRENT:END -->

<!-- C6:P3:DEVICE23:BEGIN -->
## 2026-09-14 P3 빌드23 · 실기기 3인 후속 확인

iOS 재빌드·서명 후 iPhone17과 iPad mini6에 설치·실행했다. Mac을 포함한 3인 Bonjour 입장·Ready·시작, 두 기기의 기본 2D 물리·직접 조합, iPhone 좌우 전달, iPad 손떼기 3D 투척과 실제 명중을 확인했다. 실제 Touch 투척2회 중 첫 발은 만료됐고 두 번째만 명중해 HP80이 됐다. 명중 당시 자원이100이어서 상한에 따라 실제 회복량은0이었다. [실기기 후속 기록](P3_DEVICE_VALIDATION.md)을 현재 상태로 우선 읽는다.

게임 소스는0795ae5 그대로이며 기존 자동 시험·실패·기기 미연결 기록을 보존한다. 전체 구간의 상태 해시 비교, 실기기4/5대, 실제 Victory/Retry와 실제+5 증가는 이번에 확인하지 않았다. 세 핵심 기능과 기본 실기기 동작을 갖췄으며 후속 조절·새 기능·GitHub 업로드는 별도 요청으로 진행한다.
<!-- C6:P3:DEVICE23:END -->

<!-- C6:P3:BEGIN -->
## 2026-09-14 P3 · 최대 5인 구현과 현재 검증

최신 사용자 요청에 따라 새 `FivePlayerBattle` 씬·빌드23에 2~5인 로비, 입장 순서의 좌우 이웃, 전원 Ready·초기 확인, 참가자별 자원과 공유 전투를 연결했다. P1의 2D 물리·직접 조합과 P2의 정상 손떼기 3D 투척을 통합하며 기존 씬은 보존한다. 아래 과거 2인 제한·P3 미착수 상태는 이전 시점의 기록이다.

자동1,311개와 최종 Mac 2·3·4·5인 실행(공통 상태654개·불일치0), 보관100+비행100, 6번째 거부, iOS 빌드·서명은 각 범위 PASS다. 전체 자동 검사 뒤 마지막 표시·진단 수정은 최종 Mac 실행으로 별도 확인했다. 실기기 설치·Touch·Bonjour·3인 혼합은 NOT_RUN이다. [최종 검증](P3_VALIDATION.md)·[구현 결정](P3_IMPLEMENTATION.md)·[직접 빌드와 조작](P3_RUNBOOK.md)를 먼저 읽는다.

요청된 세 기능의 구현을 갖췄으며 후속 후보는 실기기 조작감 확인이다. 기존 T14~T16·새 기능·GitHub 업로드를 자동 시작하지 않는다. 원본 사용자 변경·씬·메타·과거 증거를 보존하고 실행하지 않은 항목을 PASS로 승계하지 않는다.
<!-- C6:P3:END -->

<!-- C6:P2:BEGIN -->
## 2026-09-14 P2 · 손떼기 3D 투척

새 `ThrowBattle` 씬·빌드22에서 상단은 투척 준비, 정상 손떼기는 방향·세기 기반 중력 비행으로 변경했다. 기존 2D 물리·직접 조합·두 참가자 흐름은 유지한다. 실제 자동·앱·기기 상태와 빌드 경로는 [P2 검증 기록](P2_VALIDATION.md)을 따른다. 최대5인 연결은 P3 후속이며 자동 시작하지 않았다.

[구현·조절값](P2_IMPLEMENTATION.md) · [P2 실행 안내](P2_RUNBOOK.md)
<!-- C6:P2:END -->

<!-- C6:P1:BEGIN -->
## 2026-09-14 새 단계 · 기준 몬스터와 2D 물리

최신 사용자 승인에 따라 P0 기준 몬스터·P1 평면 구슬 물리를 새 PhysicsBattle/build21에 구현했다. 자동1,063개와 Mac두앱 생성·관성·왕복전달·조합·실제표적피격은 PASS, iOS·실기기는 NOT_RUN이다. 현재 공격은 기존 고정 방향이며 P2 손떼기투척·P3 최대5인은 후속이다. 과거 본문은 당시 기록으로 보존한다.

[현재 구현](P1_IMPLEMENTATION.md) · [실제 검증](P1_VALIDATION.md) · [실행 안내](P1_RUNBOOK.md) · [몬스터 규격](MONSTER_TARGET_SPEC.md) · [새 계획](NEXT_PHASE_PLAN.md)
<!-- C6:P1:END -->

<!-- C6:PROTOTYPE:ACCEPTED:UPLOAD:BEGIN -->
## 현재 범위 · 프로토타입 수용 및 GitHub 보관

2026-09-13 사용자는 현재 구현으로 프로토타입이 충분하다고 판단하고, 추가 작업 대신 현재 작업의 GitHub 업로드만 요청했다. 빌드20까지의 구현을 보존하며 추가 T13 중단 시험·강제 종료 승인 요청·T14~T16을 진행하지 않는다. 이후 개발은 새로운 사용자 요청이 있을 때만 시작한다.

이번 지시는 현재 작업의 커밋·기존 비공개 `prototype-author/C6_Prototype` 저장소 업로드를 허용한다. 코드·Unity 씬/메타·설정·문서·검토된 증거를 포함하며, 생성된 앱·Xcode 출력·캐시·원시 로그·서명 자료는 로컬에 보존한다.

사용자의 프로토타입 수용을 미실행 시험의 PASS로 바꾸지 않는다. 기존 실행·미실행·차단 결과는 [빌드20 검증 기록](T13_VALIDATION.md)에 남긴다. 아래의 다음 Task 제안과 과거 시점 상태는 역사 기록이며 현재 작업 재개 지시가 아니다.
<!-- C6:PROTOTYPE:ACCEPTED:UPLOAD:END -->

<!-- C6:T13:BUILD20:BEGIN -->
## T13 빌드20 · 중단 처리 구현과 현재 검증

최신 사용자 지시 “실제로 구현해야할게 얼마나 남았는지 알려주고 그걸 시작해”에 따라 T13을 구현했다. 새 InterruptionBattle 씬의 빌드20에서 유효 응답8초 감시·실제 pause 뒤 수동 연결 화면 복귀·종료 사유 안내를 보완했다. 시작0개·스태미나100·생성20·시간회복20/3초·명중회복5와 기존 씬·Config·메타·사용자 변경을 보존했다.

현재 실행한 자동1,037개(기존996+신규41), Mac 두 앱 상태204쌍 일치, iPhone17+iPad mini6의 응답 중단·오류 정리·새 연결·첫 생성·정상 END는 각 범위 PASS다. T13 전체와 원문 G5는 NOT_RUN이며 과거 빌드 PASS를 승계하지 않았다. 실제 강제 종료는 AGENTS의 프로세스 강제 종료 금지에 대한 자동 승인 검토로 BLOCKED이며 실행하지 않았다. 실제 홈 이동·잠금·Wi-Fi 해제·권한 팝업·현재 빌드 상태바 검사는 NOT_RUN이다.

[적용 결정](T13_DECISION.md)·[검증 기록](T13_VALIDATION.md). 현재 결정과 증거를 우선 읽고 아래의 과거 기록은 보존한다. 다음 구현 후보는 T14 무결성·진단 감사 하나이며 자동 시작하지 않는다. 이 요청은 T13 구현 착수 허용이며 미실행 Gate를 PASS로 바꾸는 허용이 아니다. Git commit·push는 수행하지 않았다.
<!-- C6:T13:BUILD20:END -->

<!-- C6:T12:FOLLOWUP19:BEGIN -->
## T12 빌드19 · 추가 조작·승리·초기화 확인

이번 요청의 추가 검증과 기록 정리를 완료했다. 실제 수신Raw 조합/공격 거부·Combined 전달/물리피격 및 별도 정상5hit·공격자회복5·Victory·Retry·첫Generate를 확인했다. 추가 사람 조작을 요구하지 않았고 게임/앱/Config 변경·100회 반복은 없다.

현재 확인 항목은 PASS이며 전체T12/G5 최종판정은 NOT_RUN이다. 빌드18의100회PASS와 빌드19의100회NOT_RUN을 분리한다. 정확한 짧은 외곽거리 직접재현은NOT_RUN으로 남기며 과도한 손 위치 맞추기를 추가 요구하지 않는다. [상세 결과·실패 보존·다음 판정](T12_BUILD19_FOLLOWUP.md)을 따른다. T13은 시작하지 않았다.
<!-- C6:T12:FOLLOWUP19:END -->

<!-- C6:T12:BUILD19:BEGIN -->
## T12 현재 상태 · 빌드19 오른쪽 왕복 확인

빌드19의 자동996개·앱 빌드/서명·두 기기 설치/실행 및 iPhone/iPad 양쪽 오른쪽 Combined 왕복 Touch는 PASS다. 중간 iPad 취소 입력1회와 이후 정상 손떼기 승인·반환을 구분해 기록했다. 최초 빌드18 미전달 원인은 미확정으로 보존한다.

줄어든 외곽 최소거리 구간은 코드 재현/회귀44개PASS이며 실기기에서 그 짧은 거리 자체를 직접 확인한 것은 아니다. 전체T12/G5는 NOT_RUN, 다음은 T12 남은 실기기 확인 하나다. T13은 시작하지 않는다. [최신 기록과 정확한 범위](T12_CONTINUATION.md)를 따른다.
<!-- C6:T12:BUILD19:END -->

<!-- C6:T12:BUILD18:BEGIN -->
## T12 현재 상태 · 빌드18 상태바 중단 수정 완료

2026-09-13 사용자가 지정한 상태바 중단 수정은 **PASS**다. iPhone17 Host와 iPad mini6 Client에서 각각 약13.431초·11.719초 상태바를 펼쳤다가 닫았고, 시간은13.398초·11.669초 감소했다. 같은 방·세션·두 참가자를 유지했으며 pause·연결 오류가 없고 양쪽 clock/snapshot은 계속 진행됐다. 사용자는 두 기기 모두 “잘돼”라고 확인했다. 이후 같은 세션에서 실제180초 종료·Defeat/HP100/TIME0 결과를 확인했다.

자동972개(Edit886/Play86, 기존945개 모두 유지), Mac 실제 두 실행본102전달·공통상태382개 불일치0·받은Combined 실제명중/공격자만+5, iOS export·빌드·개인Team서명·두기기설치/실행은 PASS다. 첫 경계좌표 시험1실패는 보존하고 수정 후 통과했다. 좌측 가까운 구슬의 전달 거리도 보완했으나 **빌드18 실제Touch 좌측 전달은 NOT_RUN**이다.

최신 요청에 따라 이번 마무리는 상태바 수정까지다. 결과 UI·보호버튼은 변경하지 않았다. 전체 T12/G5, 실기기100회, 현재빌드 양쪽 전체조합·전달·공격·Victory/Retry, 실제background 전환은 **NOT_RUN**이다. T13은 시작하지 않았다. 다음 요청 하나는 **T12 남은 실기기 통합 검증**이며, 현재 수정의 실제Touch와 남은 전체루프를 확인해야 G5를 판단할 수 있기 때문이다.

[최종 상세 검증](T12_VALIDATION.md) · [구현](T12_IMPLEMENTATION.md) · [적용 결정](T12_DECISION.md) · [실행 절차](T12_RUNBOOK.md) · [원본 비공개 자료: 근거 목록 — 공개 요약](VALIDATION_SUMMARY.md).

아래는 이전 작성 시점 기록을 보존한 것이다.
<!-- C6:T12:BUILD18:END -->

<!-- T11 current status 2026-09-13 -->
## T11 좌우 전달 — 현재 상태

T11 구현·코드 검증은 PASS다. 새 `OrbTransferBattle.unity`/빌드16에서 하단 자유 조합을 유지하며 좌우 끝에서 손을 놓으면 같은 구슬을 상대의 반대 경계로 전달한다. 시작0개·스태미나100·비용20·시간회복20/3초·유효명중 공격자회복5는 유지한다.

자동914개(기존837+신규77), 실제Mac 두 프로세스 전달17회(Raw8/Combined9), 동일snapshot211개 지문 일치와 수신Combined 실제명중을 확인했다. iOS빌드16 출력·앱빌드·개인Team서명은 PASS다. 빌드16 설치·실제Touch·Bonjour/Wi-Fi·T12/G5전체는 NOT_RUN이며 과거 기기PASS를 승계하지 않는다. 첫 두 Mac 진단 실패도 보존했다.

상세 결과는 [T11 검증](T11_VALIDATION.md), 적용 차이는 [T11 구현](T11_IMPLEMENTATION.md)·[결정](T11_DECISION.md), 조작·T12용100회 분배는 [실행 절차](T11_RUNBOOK.md)를 따른다. T11 미해결 차단은 없으며 다음 요청은 T12 실기기 전체 전투 검증 하나다. 다음 Task는 자동 시작하지 않는다.
<!-- T11 current status end -->

# C6_Prototype · 진행 및 검증

<!-- C6:T10B:BEGIN -->
## 현재 T10-B · 2인 게임 상태 동기화 완료

2026-09-13 **T10-B 구현·코드 검증 PASS**다. 자동 837개(Edit751/Play86), 실제 Mac 두 실행본의 생성·조합·명중·승패·Retry·180초 종료, 동일 상태 번호 3,856개 지문 일치와 최대 시간 표시 차이 약 0.051초를 확인했다. iOS 빌드15 생성·앱 빌드·개인 Team 서명도 PASS다. 빌드15 설치·실기기·G5 전체/T12는 NOT_RUN이며, 기존 iPhone 빌드13 결과를 승계하지 않는다.

새 씬은 `Assets/_Project/HapioMVP/Scenes/TwoPlayerBattle.unity`, 최종 앱 출력은 `Builds/T10-B-r3/`다. 원본 Config·이전 씬·메타데이터·과거 기록과 사용자 규칙을 보존했다. 전달·카메라 확장은 추가하지 않았다. 다음 요청 하나는 **T11 좌우 전달**이며 아직 시작하지 않았다.

[현재 검증](T10B_VALIDATION.md) · [구현 구조](T10B_IMPLEMENTATION.md) · [반복 절차](T10B_RUNBOOK.md) · [적용 결정](T10B_DECISION.md). 아래 본문은 이전 시점 기록이다.
<!-- C6:T10B:END -->


<!-- C6:T10A:BEGIN -->
## 현재 T10-A · Lobby·자동 방 탐색·Ready 완료

2026-09-13 T10-A를 구현하고 자동751개(Edit667/Play84), 실제Mac9개 실행, iOS빌드14 생성·앱빌드·개인Team서명을 확인했다. 발견방참가와직접IP를분리했으며 P1/P2·설정ACK·두Ready·HostStart계약이양쪽에서일치했다. 빌드14 설치/실기기와두iPhone AT-01/02/03·G5/T12는NOT_RUN이다. 현재Playing은시작합의완료이며게임전체동기화는다음별도 **T10-B**다.

[현재 검증](T10A_VALIDATION.md) · [구현 결정](T10A_DECISION.md) · [기기 절차](T10A_RUNBOOK.md). 기존 T09 게임·Config·과거증거를보존했다. 아래는이전시점기록이다.
<!-- C6:T10A:END -->


<!-- C6:T09:BEGIN -->
## 현재 T09 / G4 · 실기기 Core Loop 완료

빌드13의 iPhone17 DEV SOLO에서 정상 생성·같은 음양 거부·터치 조합5회·실제 명중5회·각회복+5·Victory/Defeat·고정 결과·Retry·재시작 첫Generate Raw1/비용20을 사람 확인과 기기 로그로 대조했다. **T09와 G4는 한 기기의 명시적 개발 모드 범위에서 PASS다.**

기존 소스379개는 변경 없이 보존했다. 빌드13의 자동475개(Edit401/Play74)와 Mac 두 화면/두참가자/실제180초 근거를 재확인했으며 이번에는 새 구현·재빌드·자동시험 반복을 하지 않았다. 두 기기 최종 통합·G5/T12·전체 MVP 완료는 별도다.

[현재 G4 검증](T09_G4_VALIDATION.md) · [원본 비공개 자료: 실기기 완료 근거 — 공개 요약](VALIDATION_SUMMARY.md). 다음 요청 하나는 **T10-A 2인 Lobby·방 탐색·Ready**이며 아직 시작하지 않았다. 아래는 이전 작성 시점의 기록을 보존한 것이다.
<!-- C6:T09:END -->

## 현재 T08

2026-09-13 현재 **T08 음양 조합 구현·자동 검증을 완료**했다. 시험370개·Mac 두 비율/별도Host-Client·iOS빌드10 프로젝트 생성을 확인했다. 빌드10 앱빌드·서명·설치·실기기와 T09/G4는 NOT_RUN이다.

현재 씬은 CombinationSmoke.unity, 출력은 Builds/T08/다. 시작0구슬·Stamina100/100·생성20·3초당20·유효명중+5를 유지한다. 다음 요청은 T09 Team Clock·승패·Reset 하나이며 아직 시작하지 않았다.

[현재 T08 검증](T08_VALIDATION.md) · [조합 규칙과 위치](T08_DECISION.md) · [실행 절차](T08_RUNBOOK.md)

## 이전 T07 작성 시점의 기록

아래 본문은 기존 문서를 보존한 것이다. 현재 구현·실행 여부는 위 T08 문서를 따른다.

2026-09-13 KST **T07 구현·검증 PASS — 개인 Stamina·개별 생성·실제 명중 회복.** 최신 사용자 규칙을 적용했고 자동 시험 300개, 최종 Mac 렌더 두 비율·두 프로세스 Host/Client, iOS 빌드 9 프로젝트 생성을 확인했다. **G4 전체 실기기 Gate는 T09에서 확인하며 아직 NOT_RUN이다.** T08은 시작하지 않았다.

선행 T06의 실제 소스 267개 해시, 시험 238개, 빌드 8 iPhone 직접 20회·Reset 3회 근거를 먼저 재확인했다. 당시 문서 7개를 [원본 비공개 자료: T06 역사 기록 — 공개 요약](VALIDATION_SUMMARY.md)에 보존했다. 빌드 8 실기기 PASS를 이번 빌드 9에 승계하지 않았다.

## 프로젝트·환경·적용 규칙

- 실제 저장소 `/path/to/2026-C6-M10-MUSA`, 비공개 `prototype-author/C6_Prototype`이다. 기존 사용자 변경과 이전 구현을 유지했다.
- Unity 6000.5.7f1 / URP 17.5.0 / Input System 1.20.0 / Test Framework 1.7.0 / NGO 2.13.1 / Transport 6.5.0, macOS 26.6.2 arm64 / Xcode 26.6(17F113) / SDK 26.5를 사용했다. 패키지를 추가하거나 업그레이드하지 않았다.
- 원본 Unity Editor가 열려 있어 `/private/tmp/C6_Prototype_T07_Verification`에서 설치 Editor의 배치 명령으로 검증했다. Mac과 iOS의 실제 활성 대상을 각각 지정했다. 기존 Editor·T03 Host·이전 빌드와 로그를 종료하거나 삭제하지 않았다.
- 현재 씬은 `ResourceSmoke.unity`, 최종 산출물은 `Builds/T07-v2/`다. Mac ID는 `com.wolfuraark.c6prototype.t07.desktop`, iOS는 `com.wolfuraark.c6prototype`, 버전 0.1.0/빌드 9다. Mac plist BundleVersion은 0이며 진단의 빌드 9는 iOS 식별자다.
- 시작 구슬 0개, 최대·시작 Stamina 100/100, 모든 생성은 Raw 1개/비용 20이다. 최초 전용 배치·플래그는 없다. 일반 음양은 Host Seed와 개인 성공 생성 순번으로 결정한다.
- 시간 회복은 초당 20/3의 연속 수치다. 실제 명중 보너스는 공격자에게 +5를 한 번만 준다. 최대 100, 보관 상한 20을 적용한다. 일시 중단·TargetCleared·종료 중 시간 회복은 멈추며, 복귀 시 중단 시간을 소급 회복하지 않는다. 단순 창 포커스 상실은 일시 중단으로 처리하지 않는다.
- 명시적 `DEBUG FIXTURE`의 음5/양5와 별도 `DEV HIT ORB` Combined 공급은 일반 생성과 분리한다. 아직 조합 구현은 없다. 원문과의 차이는 [사용자 규칙 변경](T07_RULE_CHANGE.md)에 기록했다.

## 실제 실행 결과

| 항목 | 결과 | 실행 근거·범위 |
|---|---|---|
| 컴파일·새 씬·단일 Config 연결 | PASS | 별도 저장 씬과 새 참조 이관, 기존 씬/meta 보존 |
| EditMode | PASS | 255/255, 실패·skip 0. 기존 199 + 자원 42 + 자원 통신 8 + 확장 공격 통신 6 |
| PlayMode | PASS | 최종 45/45, 실패·skip 0. 기존 39 + 실제 T07 Host 통합 6 |
| 빈 시작·모든 생성 비용·실패 원자성 | PASS | 시작 0/100, 다섯 즉시 생성·각 비용20, 여섯 번째 부족 거절, 사전 보유 Fixture·보관 상한·중복·이전 요청·Seed 정책 |
| 시간 회복·Max·중단/재시작 | PASS | 기본 3초 실제 경과, 연속 수치·100 제한, 모의 앱 일시 중단·컴포넌트 중단·새 세션/빈 Reset |
| 실제 명중 +5·마지막 명중 | PASS | 실제 Rigidbody/Collider, 공격자만 한 번, 마지막 HP0 명중도 +5. 다른 공격자·중복·미명중의 보너스 없음과 Max 처리 |
| 최종 Mac 빌드·렌더 | PASS | 390×844·560×746에서 빈 시작·생성·시간 회복·20개 배치·실제 +5·빈 Reset. 최종 PNG 직접 검토 |
| 실제 Host/Client 자원·구슬 동기화 | PASS | 별도 Mac 두 프로세스·시험 포트25082. 개인별 정상 생성5, Debug 상태에서 개인20/총40 구슬. 최종 두 개인 수치·활성 Orb 목록·session/round·HP0 일치 |
| 네트워크 명중 보너스 귀속 | PASS | Host1회+Client4회 요청이 실제 Host 물리 5hit로 이어짐. 각 공격자 +5, Host 이벤트5·Client 이벤트0·Client Rigidbody0 |
| 최종 iOS export | PASS | 실제 Xcode 프로젝트·Portrait·최소15.0·Device Family1,2·Local Network 문구·빌드9 확인. 오류·경고0 |
| 빌드9 Xcode 앱 빌드·서명·설치·기기 실행 | NOT_RUN | T07은 iOS 프로젝트 생성까지. 선택한 개인 Team은 YOUR_TEAM_ID이며 export의 Team 필드는 비어 있어 앱 빌드 때 명시한다 |
| 빌드9 iPhone/iPad Touch·Safe Area·FPS | NOT_RUN | Mac 화면 비율·Debug Pointer·모의 일시 중단을 실기기 증거로 기록하지 않음 |
| T08 조합·T09 한 판 통합·G4 Gate | NOT_RUN | 별도 후속 Task |

자동 시험 300개는 기존 238개를 유지하고 새 62개를 더한 결과다. 테스트 실행 수·실패·누락은 XML로 확인했다. 다섯 생성 사이에도 시간이 흐르므로 앱 표시가 정확히 0이어야 한다는 조건은 두지 않았다. 각 승인 영수증의 비용20과 경과 시간 회복을 따로 검증했다. 네트워크 최종 개인 수치는 JSON 왕복의 약3.6×10⁻¹⁵ 차이로, 허용 오차10⁻⁹ 이내에서 일치했다. ID·라운드·순서·보관 수는 정확히 같고 명중 보너스5도 부동소수점 오차 범위에서 각각 확인했다.

## 부분 Acceptance와 한계

AT-04의 기존 최초5 배치 조건은 최신 사용자 지시의 빈 시작·100·개별1/20으로 대체했다. AT-15 생성 비용, AT-16 실제 공격자 회복과 중복 차단, AT-11 회복 연결을 T07 범위에서 확인했다. 실제 조합을 거친 공격이나 정식 승패·Team HP·180초 전투·전체 Reset은 아직 아니다.

일반 Seed와 Debug Fixture 결과를 구분했다. 일반 생성 시험은 Raw만 만들며, 실제 명중 시험의 Combined는 명시적인 무료 개발 도구다. 네트워크 검증은 같은 Mac의 별도 실행본과 실제 NGO/UDP 연결이며 두 iOS 기기의 Wi-Fi 시험으로 확대하지 않는다. 이번 작업에서 사람에게 반복 터치를 요청하지 않았다.

## 실패·수정과 보존

첫 컴파일에서 설치된 NUnit에 없는 `Is.AnyOf` 시험 문법을 발견해 지원되는 제약식으로 수정했다. 추가 중단 회귀 시험의 첫 실행은44/45였다. 종료 후 재사용하는 정지 NetworkManager까지 즉시 파괴해야 한다는 잘못된 시험 조건을 고쳤다. 실제 로그의 Ended→Stopping→Idle과 비수신 상태·빈 자원·명시적 새 세션을 검증하며 씬 제거 후 완전 정리 조건은 유지했다. 최종45/45를 확인했다.

첫 태블릿 렌더에서 새 상단 패널이 표적 머리를 가렸다. T07 패널 높이와 내부 간격만 줄이고 카메라·물리·55/45 비율은 유지했다. 최종 두 화면 비율을 다시 렌더했다. 초기 구슬 누락으로 의심했던 부분은 원본 PNG의 구슬·라벨 픽셀을 대조해 모두 존재하는 것을 확인했으며 근거 없는 위치 수정은 하지 않았다.

최종 Mac과 iOS BuildReport는 모두 오류·경고0이다. 첫 Mac 빌드의 경고4개는 기존 T04Capture 2개와 새 Probe 2개였다. 새 Probe는 설치 Editor가 지원하는 API로 정리했고 기존 T04 소스는 보존했다. 이전 컴파일·시험·렌더·빌드 결과를 삭제하지 않았다.

기존 Assets/Packages 241개 중 235개는 동일하며 의도한 기존 에셋 변경은 단일 Config 코드/asset, HostOrbRegistry, AttackSession, AttackWire, 해당 전송 시험의 6개다. 기존 meta 136개·씬/meta 14개·증거 90개·과거 문서 66개를 보존했다. 누락·고아 meta·중복 GUID는0이다. 현재 소스 301개 중 297개가 검증 사본과 일치하며 기존 URP 템플릿4개는 원본을 유지하고 Unity 처리 차이를 별도 patch에 기록했다. AGENTS는 현재 Task 단락과 충돌한 생성·회복 규칙만 변경했다. 커밋·push·배포는 하지 않았다. 전체 Git 공백 검사에서 나온 PackageManagerSettings.asset 39행의 기존 후행 공백은 작업 전 해시와 같아 보존했다.

## 사람 작업과 다음 요청

T07 완료를 막는 항목이나 지금 필요한 사람 작업은 없다. 빌드9의 실제 기기 실행은 미실행이며, T09에서 생성→조합→실제 명중→회복→승패→Reset을 한 판으로 확인해야 G4를 완료할 수 있다. G2에서 승인된 iPhone17+iPad 대체 결과를 전체 두 iPhone QA로 확대하지 않는다.

다음 요청은 **T08 Raw Yin+Yang의 1단계 조합** 하나다. 이제 Host가 생성한 일반 Raw 두 재료를 같은 권한·ID·자원 규칙 안에서 Combined 하나로 바꿀 수 있다. 아직 시작하지 않았다.

## 근거

- [원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: EditMode255 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: PlayMode45 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 휴대폰 비율 20개 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 태블릿 비율 20개 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실제 명중 +5 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: Mac Host — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Mac Client — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 검토한 실행 로그 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: iOS 생성 설정 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 소스 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 집계 — 공개 요약](VALIDATION_SUMMARY.md)
- [T07 반복 절차](T07_RUNBOOK.md) · [구현 결정](T07_DECISION.md) · [최신 사용자 변경](T07_RULE_CHANGE.md)
