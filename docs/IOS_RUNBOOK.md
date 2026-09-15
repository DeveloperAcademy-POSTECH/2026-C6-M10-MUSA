> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

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

<!-- C6:T13:BUILD20:BEGIN -->
## T13 빌드20 · 중단 처리 구현과 현재 검증

최신 사용자 지시 “실제로 구현해야할게 얼마나 남았는지 알려주고 그걸 시작해”에 따라 T13을 구현했다. 새 InterruptionBattle 씬의 빌드20에서 유효 응답8초 감시·실제 pause 뒤 수동 연결 화면 복귀·종료 사유 안내를 보완했다. 시작0개·스태미나100·생성20·시간회복20/3초·명중회복5와 기존 씬·Config·메타·사용자 변경을 보존했다.

현재 실행한 자동1,037개(기존996+신규41), Mac 두 앱 상태204쌍 일치, iPhone17+iPad mini6의 응답 중단·오류 정리·새 연결·첫 생성·정상 END는 각 범위 PASS다. T13 전체와 원문 G5는 NOT_RUN이며 과거 빌드 PASS를 승계하지 않았다. 실제 강제 종료는 AGENTS의 프로세스 강제 종료 금지에 대한 자동 승인 검토로 BLOCKED이며 실행하지 않았다. 실제 홈 이동·잠금·Wi-Fi 해제·권한 팝업·현재 빌드 상태바 검사는 NOT_RUN이다.

[적용 결정](T13_DECISION.md)·[검증 기록](T13_VALIDATION.md). 현재 결정과 증거를 우선 읽고 아래의 과거 기록은 보존한다. 다음 구현 후보는 T14 무결성·진단 감사 하나이며 자동 시작하지 않는다. 이 요청은 T13 구현 착수 허용이며 미실행 Gate를 PASS로 바꾸는 허용이 아니다. Git commit·push는 수행하지 않았다.
<!-- C6:T13:BUILD20:END -->

T13 현재 결과물: `Builds/T13/build20/iOS`(Xcode), `Builds/T13/build20/C6Prototype.app`(서명 앱), `Builds/T13/build20/macOS/C6Interruption.app`(Mac 검사 앱). 개인 Team `YOUR_TEAM_ID`, Bundle ID `com.wolfuraark.c6prototype`. [명시적 기기 진단 실행](T13_DEVICE_DIAGNOSTICS.md)을 따른다.

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

현재 앱: `Builds/T12-r2/DerivedData/Build/Products/Debug-iphoneos/C6Prototype.app`, 새 씬 `Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity`. 이전 출력은 `Builds/T12-r1/`이며 원시 기록은 `Logs/T12/session-20260913/`에 있다. 상태바 수정은 이 T12 단독 iOS 출력에서만 적용한다.

<!-- T11 current status 2026-09-13 -->
## T11 좌우 전달 — 현재 상태

T11 구현·코드 검증은 PASS다. 새 `OrbTransferBattle.unity`/빌드16에서 하단 자유 조합을 유지하며 좌우 끝에서 손을 놓으면 같은 구슬을 상대의 반대 경계로 전달한다. 시작0개·스태미나100·비용20·시간회복20/3초·유효명중 공격자회복5는 유지한다.

자동914개(기존837+신규77), 실제Mac 두 프로세스 전달17회(Raw8/Combined9), 동일snapshot211개 지문 일치와 수신Combined 실제명중을 확인했다. iOS빌드16 출력·앱빌드·개인Team서명은 PASS다. 빌드16 설치·실제Touch·Bonjour/Wi-Fi·T12/G5전체는 NOT_RUN이며 과거 기기PASS를 승계하지 않는다. 첫 두 Mac 진단 실패도 보존했다.

상세 결과는 [T11 검증](T11_VALIDATION.md), 적용 차이는 [T11 구현](T11_IMPLEMENTATION.md)·[결정](T11_DECISION.md), 조작·T12용100회 분배는 [실행 절차](T11_RUNBOOK.md)를 따른다. T11 미해결 차단은 없으며 다음 요청은 T12 실기기 전체 전투 검증 하나다. 다음 Task는 자동 시작하지 않는다.
<!-- T11 current status end -->

# C6_Prototype · 환경과 iOS 실행 절차

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

## 현재 T07 · 빌드9

2026-09-13 현재 씬은 `ResourceSmoke.unity`이며 최종 생성물은 `Builds/T07-v2/iOS/Unity-iPhone.xcodeproj`다. iOS export는 성공했고, 빌드9의 Xcode 앱 빌드·서명·설치·실기기 실행은 아직 하지 않았다. 개인 Team `YOUR_TEAM_ID`는 향후 앱 빌드 때 명시한다. 생성된 프로젝트의 Team 필드는 비어 있다.

최신 규칙은 시작 구슬0개·Stamina100/100·생성20·3초당20 회복·실제 공격자+5다. 아래의 빌드8/AttackSmoke/Fixture/20회 내용은 **이전 T06 실행 절차와 기록**이다. 현재 T07 실행은 [T07_RUNBOOK](T07_RUNBOOK.md), 상태는 [VALIDATION](VALIDATION.md)을 따른다. G4의 한 판 실기기 통합은 T09에서 확인한다.

## 이전 T06 절차와 기록

현재 **T06/G3 PASS**다. [T06_RUNBOOK](T06_RUNBOOK.md)의 절차로 `Builds/T06-v3/iOS/Unity-iPhone.xcodeproj`를 생성했고, `Builds/T06-v3/DerivedData/Build/Products/Debug-iphoneos/C6Prototype.app`를 실제 컴파일·서명·설치·실행했다. 앱0.1.0/빌드8, 본인 Apple 개발 Team 개인 Team YOUR_TEAM_ID, 기존 앱 ID, iPhone+iPad 공용·Portrait·IL2CPP·최소15.0을 확인했다. iPhone17/iOS26.6.1에서 직접20회·Reset3회와 화면을 확인했다. [원본 비공개 자료: 이번 기기 근거 — 공개 요약](VALIDATION_SUMMARY.md)와 [전체 결과](VALIDATION.md)를 따른다. T07은 시작하지 않았다.

T05 빌드7은 Xcode 프로젝트 생성까지만 실행했던 과거 단계다. [원본 비공개 자료: 당시 문서 — 공개 요약](VALIDATION_SUMMARY.md)와 `Builds/T05-v3/`를 보존했으며, 빌드7 기기 검증을 소급 PASS로 바꾸지 않는다. 아래 T03/T01은 당시 절차다. 이번 실기기 공격20회는 iPhone Host 단독이며 두 iOS 기기 동시 공격·기기 만료/FPS는 NOT_RUN이다.

이전 T03 공용 앱의 빌드·실행은 [T03_RUNBOOK](T03_RUNBOOK.md)을 따른다. 빌드5를 같은 개인 Team·앱 ID로 iPhone17과 iPad에 설치했고100건 동기화·첫 권한·실패 후 수동 재시도를 확인했다. [사용자 승인 기기 변경](G2_DEVICE_CHANGE.md)을 적용한 G2는 PASS다. 아래는 보존된 T01 절차이며 T02 연결 절차도 [T02_RUNBOOK](T02_RUNBOOK.md)에 보존했다.

2026-09-12 T01/G1 PASS. A 실행과 B 재설치·실행 및 사용자 화면 확인을 완료했다. 구체적인 시험 결과와 한계는 [VALIDATION](VALIDATION.md)을 따른다. [원본 비공개 자료: T00 당시 준비 기록 — 공개 요약](VALIDATION_SUMMARY.md)은 보존했다.

## T01 최소 앱 설치·재설치 (2026-09-12)

현재 작업은 사용자의 후속 요청으로 시작한 T01이다. 아래 절은 T00 당시의 “아직 없음/미설정” 설명을 갱신한다. 실행 결과는 [VALIDATION.md](VALIDATION.md)에 별도로 기록한다.

### 현재 구성

| 항목 | 값·근거 |
|---|---|
| Unity / 렌더러 | 6000.5.7f1 / URP17.5.0 유지 |
| 입력 / 시험 | Input System1.20.0 / Test Framework1.7.0 유지 |
| 시험 씬 | `Assets/_Project/HapioMVP/Scenes/IOSBuildSmoke.unity` |
| 빌드 표시 원본 | `Assets/_Project/HapioMVP/Config/IOSBuildStamp.asset` 한 개 |
| A / B 표시 | `C6-T01-A`, Revision1 / `C6-T01-B`, Revision2 |
| 앱 ID / 버전 | `com.wolfuraark.c6prototype` / 0.1.0. 이번 개발용으로 선택한 식별자이며 기존 등록 확인을 뜻하지 않는다. |
| iOS 설정 | Portrait, iPhoneOnly, DeviceSDK, IL2CPP; 자동 회전 비활성 |
| UI | 빌드 ID·Revision·버튼·클릭 횟수, Safe Area 적용, 목표60fps |
| 서명 Team | 사용자가 선택한 본인 Apple 개발 Team 개인 Team. 인증서·프로필·개인 키는 저장소에 복사하지 않는다. |
| 시험 기기 | 사용자가 선택한 연결된 iPhone17, iOS26.6.1. Developer Mode enabled·booted 확인; 앱 실행 근거와는 별개다. |
| Xcode / SDK | 26.6 / iphoneos26.5. `DEVELOPER_DIR`를 실행 프로세스에만 지정한다. |

동일 앱의 재설치를 검증하므로 A와 B의 Bundle ID·개인 Team은 같아야 한다. 앱 삭제 후 B만 설치한 결과는 A→B 수정 버전 재설치와 구분한다. 다른 시험의 Build ID, 로그, 화면 확인 결과를 현재 빌드에 승계하지 않는다.

### Editor 메뉴

`C6 > T01 > Prepare Build A Scene` 또는 `Prepare Build B Scene`으로 빌드 표시와 iOS 설정을 준비한다. `Export iOS Build A` / `Export iOS Build B`는 해당 버전을 준비하고 Xcode 프로젝트를 생성한다. 기본 출력은 프로젝트 아래 `Builds/iOS/T01/A` / `B`다.

이미 내용이 있는 출력 폴더는 덮어쓰지 않는다. 재실행할 때는 기존 출력·서명 변경을 보존한 채 새 `C6_T01_OUTPUT_ROOT`를 명시한다. 이 환경 변수는 Unity 실행 전에 지정해야 한다. 미저장 Untitled 씬이 열려 있고 시험 씬을 처음 만드는 상황에서는 저장된 사용자 씬을 교체하지 않도록 작업이 중단된다. 사용자 씬을 강제로 닫거나 저장하지 않는다.

### 반복 CLI

실제 사용할 프로젝트가 Unity Editor에서 열려 있지 않은지 먼저 확인한다. 같은 프로젝트에 두 번째 batchmode를 실행하지 않는다. Editor가 열려 있으면 Editor 메뉴를 쓰거나, 실행용 별도 사본에 `Assets`·`Packages`·`ProjectSettings`를 보존 복사하고 사본에서 검증한다. 사본 결과를 본 프로젝트에 연결할 때는 관련 소스·씬·Config·패키지의 해시 일치를 기록한다.

현재 T01의 실제 출력은 본 프로젝트 `Builds/iOS/T01/A`, `B`, `DerivedData-A`, `DerivedData-B`이며 로그는 `Logs/T01/`에 보관했다. 본 프로젝트의 미저장 씬을 보존하기 위해 `/private/tmp/C6_Prototype_T01_Verification` 사본에서 Unity 검증을 실행했다. 아래 `C6_WORK`는 Editor가 열지 않은 검증 대상 경로로 정한다. 실행마다 새 로그·출력 경로를 사용한다.

```sh
C6_WORK=/private/tmp/C6_Prototype_T01_Verification
C6_UNITY=/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity
C6_RUN_ID=$(date +%Y%m%d-%H%M%S)
C6_LOGS="$C6_WORK/Logs/T01/$C6_RUN_ID"
C6_OUTPUT="$C6_WORK/Builds/iOS/T01/$C6_RUN_ID"
mkdir -p "$C6_LOGS"

env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  "$C6_UNITY" -batchmode -nographics -quit \
  -projectPath "$C6_WORK" -buildTarget iOS \
  -executeMethod C6.Editor.IOSSmokeBuild.PrepareA \
  -logFile "$C6_LOGS/prepare-A.log"
```

시험 실행에는 `-quit`을 넣지 않는다. 각 실행이 종료된 뒤 다음 명령을 실행하고 종료 코드와 XML 결과를 확인한다. 종료0만으로 시험 PASS를 기록하지 않으며 XML의 실제 total/passed/failed/skipped 수를 읽는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  "$C6_UNITY" -batchmode -nographics \
  -projectPath "$C6_WORK" -buildTarget iOS \
  -runTests -testPlatform EditMode \
  -testResults "$C6_LOGS/editmode.xml" -logFile "$C6_LOGS/editmode.log"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  "$C6_UNITY" -batchmode -nographics -screen-width 390 -screen-height 844 \
  -projectPath "$C6_WORK" -buildTarget iOS \
  -runTests -testPlatform PlayMode \
  -testResults "$C6_LOGS/playmode.xml" -logFile "$C6_LOGS/playmode.log"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  C6_T01_OUTPUT_ROOT="$C6_OUTPUT" \
  "$C6_UNITY" -batchmode -nographics -quit \
  -projectPath "$C6_WORK" -buildTarget iOS \
  -executeMethod C6.Editor.IOSSmokeBuild.ExportA \
  -logFile "$C6_LOGS/export-A.log"
```

생성 결과에서 `A/Unity-iPhone.xcodeproj/project.pbxproj`, `Info.plist`, Unity export 영수증을 확인한다. 현재 도구는 `Logs/T01/export-{A 또는 B}-{UTC시각}-{GUID}.json`으로 실행별 영수증을 남긴다. 최초 A 실행의 `export-A.json`도 과거 증거로 보존한다. Unity export 성공은 앱 컴파일·서명·기기 실행 성공이 아니다.

별도로 설치된 Unity CLI1.0.0-beta.8도 확인했다. 현재 연결된 Editor가0개여서 이번 T01 자동 시험·export에는 설치 Editor 자체의 위 명령을 사용했다. 연결용 패키지를 추가하거나 환경을 변경하지 않았다.

### Xcode 앱 빌드와 개인 Team 서명

생성된 Xcode 프로젝트를 대상으로 미서명 컴파일을 먼저 실행한다. 아래 명령·서명 명령은 절차이며 실제 실행 결과는 검증 표의 증거를 따른다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "$C6_OUTPUT/A/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$C6_OUTPUT/DerivedData-A" \
  CODE_SIGNING_ALLOWED=NO > "$C6_LOGS/xcode-A-unsigned.log" 2>&1
```

그 다음 Xcode에 등록된 본인 Apple 개발 Team 개인 Team을 사용해 자동 서명으로 빌드한다. `C6_PERSONAL_TEAM_ID`는 실제 확인한 개인 Team ID를 로컬 셸에서 지정한다. 문서용 예시 값을 그대로 실행하거나 회사 Team으로 대체하지 않는다.

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "$C6_OUTPUT/A/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$C6_OUTPUT/DerivedData-A" \
  -allowProvisioningUpdates \
  CODE_SIGNING_ALLOWED=YES CODE_SIGN_STYLE=Automatic \
  DEVELOPMENT_TEAM="$C6_PERSONAL_TEAM_ID" \
  > "$C6_LOGS/xcode-A-signed.log" 2>&1
```

Bundle ID는 Unity export의 앱 대상에 이미 설정돼 있으므로 전역 Xcode 옵션으로 Framework 대상까지 덮어쓰지 않는다. 서명된 `.app`의 실제 경로·Bundle ID·빌드 번호·서명을 확인하고, 사용자가 선택한 iPhone17에 설치한다. 기기 식별자와 서명 원시 정보는 로컬 로그에만 보관한다. 앱 설치는 실행 증거를 대체하지 않는다.

`C6_DEVICE_ID`는 선택한 iPhone의 실제 식별자를 로컬에서 지정한다. 아래는 A 설치·실행 예시이며 B는 출력·로그 이름을 함께 바꾼다. 콘솔 연결은 앱이 종료될 때까지 유지된다.

```sh
codesign --verify --deep --strict --verbose=2 \
  "$C6_OUTPUT/DerivedData-A/Build/Products/Debug-iphoneos/C6Prototype.app"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcrun devicectl device install app --device "$C6_DEVICE_ID" \
  "$C6_OUTPUT/DerivedData-A/Build/Products/Debug-iphoneos/C6Prototype.app" \
  --json-output "$C6_LOGS/device-install-A.json"
```

```sh
env DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcrun devicectl device process launch --device "$C6_DEVICE_ID" \
  --terminate-existing --console com.wolfuraark.c6prototype \
  --json-output "$C6_LOGS/device-launch-A.json" \
  > "$C6_LOGS/device-console-A.log" 2>&1
```

### 실제 A 실행 → 수정 B 재설치

1. A를 실행해 기기 화면에 `C6-T01-A / Revision1`이 나타나는지 확인한다. 버튼을 직접 눌러 횟수가 한 번씩 증가하는지 기록한다. 기기 화면 또는 사용자 관측과 앱 실행 로그를 같은 빌드에 연결한다.
2. A 실행 근거를 확보한 뒤 `PrepareB` 또는 `ExportB`로 `C6-T01-B / Revision2`로 변경한다. A와 같은 `C6_OUTPUT`의 아직 비어 있는 `B` 폴더에 Unity export를 실행한다. CLI는 위 `ExportA`를 `ExportB`로 바꾸고 로그 파일도 `export-B.log`로 구분한다.
3. `B/Unity-iPhone.xcodeproj`를 같은 Bundle ID·개인 Team으로 앱 빌드·서명한다. B의 DerivedData와 로그는 A와 구분한다. A가 설치된 같은 iPhone에 B를 재설치한다.
4. B를 실행해 실제 화면의 `C6-T01-B / Revision2`와 클릭 횟수 동작을 확인한다. `C6_T01_SMOKE_READY build=C6-T01-B revision=2 count=0` 및 실제 입력의 `C6_T01_TAP` 로그를 보관한다.
5. A 실행과 B 재설치·실행의 날짜·기기 모델/iOS·앱 ID·빌드 ID·증거 경로를 함께 기록한다. 실제 B 실행 증거가 없으면 G1은 NOT_RUN 또는 구체적인 차단 원인이 있는 BLOCKED로 남긴다.

자동 PlayMode 입력 시험은 실제 손가락 입력의 근거가 아니다. 60fps 설정은 실측60fps 결과가 아니며, 실제 Safe Area·회전 화면 확인도 별도로 기록한다. G1 결과와 무관하게 T02는 자동 시작하지 않는다.

### 사람에게 필요한 작업

기기 잠금 해제와 화면 확인·실제 버튼 입력은 사람이 수행한다. 계정 로그인·기기 신뢰·프로비저닝 또는 설치 허용이 실제로 막히면 해당 오류와 필요한 조작만 안내한다. 현재 개인 Team과 시험 기기 선택은 이미 사용자가 답했으므로 다시 선택을 요청하지 않는다. 다음 G2 시험에는 실제 iPhone 두 대와 같은 Wi-Fi가 필요하다.

원시 Unity/Xcode/기기 로그, 인증서, 프로필은 Git에 올리지 않는다. 실행 수가 있는 XML·검토한 요약·소스 해시만 `docs/evidence/T01/`에 공유한다.
