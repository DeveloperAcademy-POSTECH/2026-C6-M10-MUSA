> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T08 · 진행과 실제 검증

2026-09-13 KST **T08 구현·자동 검증 PASS: Raw Yin+Yang의 1단계 조합과 실제 공격 연결.** 최종 시험370개, Mac 두 화면 비율·별도 Host/Client, iOS 빌드10 프로젝트 생성을 확인했다. **빌드10 실기기는 NOT_RUN이며, T09는 시작하지 않았다.**

## 확인한 프로젝트와 환경

실제 저장소는 `/path/to/2026-C6-M10-MUSA`, 비공개 `prototype-author/C6_Prototype`이다. Unity6000.5.7f1 / URP17.5.0 / InputSystem1.20.0 / TestFramework1.7.0 / NGO2.13.1 / Transport6.5.0, macOS26.6.2 arm64 / Xcode26.6(17F113) / SDK26.5를 유지했다.

원본 Editor가 열려 있어 `/private/tmp/C6_Prototype_T08_Verification`에서 설치 Unity의 배치 명령으로 검증했다. 현재 씬은 CombinationSmoke.unity, 출력은 Builds/T08/다. 앱0.1.0/iOS빌드10, iOS ID `com.wolfuraark.c6prototype`, Mac ID `com.wolfuraark.c6prototype.t08.desktop`이다. Mac plist BundleVersion은0이며 빌드10은 iOS 식별자다.

선행 T07의 소스301개 해시·실행 XML300개·원시로그20개·Mac/iOS 산출물을 재확인했다. 이전 현재문서6개는 [원본 비공개 자료: T07 역사 기록 — 공개 요약](VALIDATION_SUMMARY.md)에 보존했다. 문서 승인이나 이전 기기 결과를 현재 빌드 PASS로 승계하지 않았다.

## 구현과 검증 결과

같은 소유자의 서로 다른 Idle Raw Yin+Yang 두 재료를 Host가 동시에 Consumed로 바꾸고 새 ID의 Combined/None/Idle 하나를 만든다. 실패·중복·소비된재료·종류·소유권·전달/발사 예약을 검증한다. 조합 비용이나 보너스는 없으며, 결과는 하단 ORB GRID 안의 Drop시 실제 두 표시 중심 중간점이다.

시작구슬0·Stamina100/100·매생성Raw1/비용20·연속3초당20·실제공격자+5·보관20을 유지했다. 일반 HostSeed 생성과 명시적인 혼합Raw5 Fixture를 구분한다. T08에는 무료 Combined 버튼이 없고 실제 공격 시험은 조합 결과를 사용했다.

| 항목 | 결과 | 실행 근거와 범위 |
|---|---|---|
| 컴파일·별도 저장 씬·참조 | PASS | ResourceSmoke를 보존한 CombinationSmoke, 같은 Config/카메라/실제 Collider |
| EditMode | PASS | 최종316/316, 실패·skip0. 기존255 + 조합·통신·표시중심61 |
| PlayMode | PASS | 최종54/54, 실패·skip0. 기존45 + 저장씬·실제NGO·Drop·물리·수명9 |
| 조합 성공·거부·원자성·중복 | PASS | 양방향, 새ID1/Consumed2, 같은음양/같은ID/타소유/Raw+Combined/Combined+Combined/소비된재료 거부 |
| 자원·보관·행동경합 | PASS | 무비용·거부보존·20→19→생성20, 공유예약·두재료잠금·조회·Reset |
| 입력·Raw 공격 거부 | PASS | 접촉만 미조합, 최근접전체후보, 실제표시중심, viewport밖Up차단, Raw Zone진입보존 |
| 중단·응답 확인 | PASS 범위 제한 | 실제예약경합·컴포넌트중단 통합시험, nonce/순번/좌표/인벤토리revision 통신시험. 실제Wi-Fi패킷유실 주입은 미실행 |
| 조합 결과 실제 공격·+5 | PASS | 새Combined ID 그대로 실제 Rigidbody/Collider 피격, 공격자보너스5 한 번 |
| Mac 화면 두 비율 | PASS | 390×844·560×746, 빈시작·유료생성·조합·피격·Fixture·같은음양거부·빈Reset. 원본PNG 종류·음양·결과표시 직접검토 |
| 실제 두 프로세스 | PASS | NGO/UDP포트25093, 일반유료생성Host6/Client5, 각각조합2회·정확한중복요청확인 |
| Host/Client 동기화 | PASS | 총조합4/실제hit4, HP20·Attack revision53·활성Raw3·ID/소유/상태/session/round 동일 |
| 피격 회복 귀속 | PASS | Host2+Client2, 각hit+5, Host이벤트4·Client0·Client Rigidbody0 |
| Mac 빌드·iOS export | PASS | BuildReport 둘 다 오류·경고0, 실제Xcode프로젝트·빌드10·Portrait·최소15·DeviceFamily1,2·LocalNetwork문구 |
| 빌드10 앱 빌드·서명·설치·기기 | NOT_RUN | T08은 iOS프로젝트 생성까지. 실제Drop 성공·거부는 T09 사람·기기 확인 |
| 빌드10 Touch·Safe Area·FPS·G4 | NOT_RUN | Mac 비율·자동Pointer를 기기근거로 기록하지 않음 |
| T09 Team Clock·승패·한판Reset | NOT_RUN | 다음 별도 요청 Task |

일반 생성의 음양 수는 보장하지 않으므로 Host는 시간 회복 후 비용20으로1개를 추가 생성했다. 총11Raw 중8재료가4Combined가 되고4개가 실제로 발사돼 Raw3개가 남았다. 무료 개발Combined를 조합 결과로 세지 않았다.

최종 개인자원 snapshot도 양쪽에서 동일했다(Host13.056561386666692, Client32.98961583333344). Playing중 자동회복이 계속되므로 서로 다른 시점까지 항상 같은 수치라는 뜻은 아니다. +5영수증의 약10⁻¹⁵ 부동소수점 차이는 허용오차 내에서 확인했다.

## 검토 보강과 보존

첫 EditMode314·PlayMode54도 통과했다. 승인응답보다 인벤토리 갱신이 늦는 경우를 검토해 보강하고 통신시험2개를 추가한 뒤 최종316+54를 재실행했다. 실제패킷유실 재현과는 구분한다. 컨트롤러만 중단돼 조회가 멈추는 경로는 실제예약경합 통합시험으로 확인했다. 이번 컴파일·시험·빌드·Probe 실행실패는 없었다.

기존 Assets/Packages275개 중271개가 동일하고 공유변경4개는 HostOrbRegistry 원자조합, OrbModel 종류기능, OrbGestureEngine 선택적표시중심인자, ResourceSession 명시적혼합Fixture API다. Config·패키지·기존meta155·씬/meta16·증거119·과거history66을 보존했다. ProjectSettings는 현재씬·빌드10·Mac ID에 필요한2개만 변경했다. 현재소스337개 중검증사본333개가 일치하고 기존URP템플릿4개는 원본을 유지하며 차이를patch로 기록했다. baseline에 없는UserSettings의 해시보존까지 주장하지 않는다.

원래 Editor·T03 Host·이전산출물을 보존했다. AGENTS는 현재Task단락만 갱신한다. 기존 문서 본문을 유지하고 T08상세는 별도문서로 추가한다. 커밋·push·배포는 하지 않았다. 원시로그는 로컬Logs/T08에 보관하고 공유근거에는 검토한 실행행·해시를 남겼다.

## 진행을 막는 항목·사람 작업·다음 Task

T08 완료를 막는 항목이나 지금 필요한 사람 작업은 없다. 빌드10 앱빌드·설치와 실제Touch 검증은 미실행이다. 선택한 개인 Apple Team은 본인 Apple 개발 Team의 YOUR_TEAM_ID이며 Xcode export의 Team필드는 비어 있어 앱빌드 때 명시해야 한다. G2의 iPhone17+iPad 승인을 전체두iPhone QA로 확대하지 않는다.

다음 요청은 **T09 Team Clock·승패·Reset** 하나다. 생성→조합→실제피격→회복이 연결됐으므로 시간·승패·전체Reset을 묶어 한iPhone Core Loop를 검증할 순서다. T09는 아직 시작하지 않았다.

## 근거

- [원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: EditMode316 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: PlayMode54 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 휴대폰 조합 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 태블릿 거부 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실제+5 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: Mac Host — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Mac Client — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실행 행 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: iOS 설정 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 소스 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 — 공개 요약](VALIDATION_SUMMARY.md)
- [반복 절차](T08_RUNBOOK.md) · [구현 결정](T08_DECISION.md) · [사용자 자원 변경](T07_RULE_CHANGE.md)
