> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T09 · 시간·승패·Reset과 실제 검증

<!-- C6:T09:INPUT13 -->
## 현재 T09 / G4 · 실기기 Core Loop 완료

빌드13의 iPhone17 DEV SOLO에서 정상 생성·같은 음양 거부·터치 조합5회·실제 명중5회·각회복+5·Victory/Defeat·고정 결과·Retry·재시작 첫Generate Raw1/비용20을 사람 확인과 기기 로그로 대조했다. **T09와 G4는 한 기기의 명시적 개발 모드 범위에서 PASS다.**

기존 소스379개는 변경 없이 보존했다. 빌드13의 자동475개(Edit401/Play74)와 Mac 두 화면/두참가자/실제180초 근거를 재확인했으며 이번에는 새 구현·재빌드·자동시험 반복을 하지 않았다. 두 기기 최종 통합·G5/T12·전체 MVP 완료는 별도다.

[현재 G4 검증](T09_G4_VALIDATION.md) · [원본 비공개 자료: 실기기 완료 근거 — 공개 요약](VALIDATION_SUMMARY.md). 다음 요청 하나는 **T10-A 2인 Lobby·방 탐색·Ready**이며 아직 시작하지 않았다. 아래는 이전 작성 시점의 기록을 보존한 것이다.
<!-- C6:T09:INPUT13:END -->

2026-09-13 KST 현재 **T09 구현·빌드12 자동 검증 PASS, 실제 iPhone Core Loop 재검증 대기**다. 빌드11은 기기 조합 조작에서 FAIL이 확인되어 수정했다. 빌드12 앱 빌드·서명·설치·실행은 PASS지만, 직접 조합→피격→회복→결과→Reset 확인이 끝나기 전 **G4 PASS로 기록하지 않는다.**

## 프로젝트와 환경

실제 루트는 `/path/to/2026-C6-M10-MUSA`, 비공개 원격은 `prototype-author/C6_Prototype`이다. Unity6000.5.7f1 / URP17.5.0 / InputSystem1.20.0 / TestFramework1.7.0 / NGO2.13.1 / Transport6.5.0, macOS26.6.2 arm64 / Xcode26.6(17F113) / SDK26.5를 유지했다. 선택한 iPhone17은 이번 실행 로그에서 **iOS26.6.2**, 1206×2622, Safe Area(0,102,1206,2334)로 확인됐다. 이전 iOS26.6.1 기록을 현재 환경으로 승계하지 않았다.

원래 Editor를 보존하고 `/private/tmp/C6_Prototype_T09_Verification`에서 설치 Unity CLI로 시험·빌드했다. 현재 씬은 BattleLoop.unity, 최종 출력은 Builds/T09-build12다. 앱0.1.0/빌드12, iOS ID com.wolfuraark.c6prototype, 본인 Apple 개발 Team 개인 Team YOUR_TEAM_ID이다. 같은 앱을 선택한 iPhone17에 실제 설치·실행했다. 앱은 Portrait, 최소15.0, iPhone+iPad 공용이며 이번 기기 시험은 한 iPhone의 명시적 DEV SOLO다.

## 구현과 실행 결과

시작0구슬·Stamina100/100·매 Generate Raw1/비용20·연속 회복20/3초·실제 유효명중 공격자+5·상한100·보관20을 유지했다. Host가 Start 시 새 round와 시작/종료 시각을 확정하며 기본180초·Team HP 감소1/초를 사용한다. 충돌 처리 시각이 deadline보다 엄격히 작을 때만 피해를 적용하고, 최종 유효 +5를 반영한 뒤 Victory를 확정한다. 정확한 deadline 및 이후는 Defeat다.

| 항목 | 결과 | 실제 근거와 범위 |
|---|---|---|
| EditMode | PASS | 최종381/381, 실패·skip0 |
| PlayMode | PASS | 최종65/65, 실패·skip0. 저장 씬·실제 NGO·Drop·Rigidbody·결과·Reset |
| 기본2인/개발1인 | PASS | 일반 Host1명은 Lobby/Start거부,2명Ready 후 Host Start. DEV SOLO는 명시적으로 선택 |
| AT-12 시계 | PASS / 자동 | 최종 Mac 빌드12에서 실제180.00282025초 후 Defeat. 단축·합성시각 시험과 구분 |
| AT-13 승리 | PASS / 자동 | 일반 Seed/유료 생성/실제 조합5/실제 물리 피격5/공격자+5 후 Victory |
| AT-14 패배·경계 | PASS / 자동 | 실제180초, 실제2초 단축 만료·비행 취소, 정확한deadline·이후·콜백순서 순수시험 |
| 결과 고정·Pending | PASS | 시간·HP·스태미나 정지, 생성/조합/전달/발사 차단, 남은 투사체와 미확정 요청 정리 |
| Reset/Retry | PASS / 자동 | Host만 새 round, Ready/구슬0/HP100/Stamina100/180초, 다시 Start 후 첫Generate Raw1/20, 이전 요청 거부 |
| 조작 회귀 | PASS / 자동 | 이웃4쌍×양방향 중심8회·가장자리95% 잡기8회·긴Swipe2회, 여러 Move 후 Up. 명시적 기하 Fixture |
| 최종 Mac 두 화면 | PASS | 390×844·560×746에서 정상 생성·목표중심 Drop·명중·결과·Reset. 이미지 직접 검토 |
| 최종 Mac Host/Client | PASS | 정상 유료생성 Host7/Client6, 조합3/2, 실제hit5·각+5, Raw3잔존, nonce외 최종상태 완전일치 |
| Client 권한 제한 | PASS | Client 물리 Rigidbody0·회복 작성0, Host3/Client2 공격자 회복 귀속 |
| Mac/iOS 프로젝트 생성 | PASS | 실제 BuildReport와 산출물, 빌드12의 공용 iOS 선언 확인 |
| 빌드12 iOS 앱·서명·설치·실행 | PASS | Xcode BUILD SUCCEEDED, 실제 서명 검증·Team·앱12·설치 success·기기 C6_T09_READY build12 |
| 빌드11 실제 조합 Core Loop | FAIL | 정상Generate5/TOUCH17, 모두 전달로 선행 판정·NOT_A_LAUNCH, 조합/명중/회복0. 사람도 조작 문제 보고 |
| 빌드12 직접Touch Core Loop·G4 | NOT_RUN / 확인 대기 | 새 앱 첫 중심 조합·HP80 명중을 사람에게 요청. 완료·Reset 근거 확보 전 PASS 금지 |
| 두 iPhone QA·자동 탐색·T10-A/B | NOT_RUN | 이번 범위 밖. G2 iPhone+iPad 대체 승인을 확대하지 않음 |

최종 Mac GUID는 `79c456700cee4ed68a284e1e44c3d7ee`다. Host/Client는 최종 Victory·HP0·남은 시간/Team HP175.112494917·round2와 개인 자원·잔존 구슬까지 nonce만 제외하고 정확히 일치했다. 모든 최종 자동 Drop은8회 이동 표본으로 목표 중심에 놓았으며 Up 전 조합/전달이 없었다. 화면·자동 포인터는 실제 Touch 증거가 아니다.

## 실기기에서 발견한 실패와 수정

빌드11의 사람 확인은 READY·0구슬·100/100·180초·화면 정상까지다. 정상 생성5회가 각각20을 차감한 후 실제 터치17회가 모두 가로 전달로 먼저 판정되어 거절됐다. 조합·피격·회복은0회이고 나중에 HP100·시간0 Defeat로 종료됐다. Core Loop를 FAIL로 보존한다.

기존5열의 이웃 중심 거리는 약0.196 화면폭으로 기존 Swipe 기준0.18보다 컸다. 이전 자동 시험은 목표 중심보다 앞에서 놓아 이 실제 조작 문제를 놓쳤다. 새 회귀 시험을 수정 전 코드에 실행했을 때 중심/가장자리 드래그2개가 실제 FAIL, 긴Swipe1개는 PASS였다. 수정 후 세 시험 모두 통과했다.

T09 표시 영역만 중앙에서 가로로 좁혀 이웃 간격+잡기 반지름+여유4화면px가 기존 Swipe 기준보다 작아지도록 했다. 공용 제스처·0.18/0.08/1.25·최초판정 우선순위·논리 구슬 좌표·같은 Config·기존 T07/T08은 보존했다. 긴 가로 드래그는 여전히 전달로 먼저 잡힌다. 빌드12 실제 기기 재검증은 별도 기록한다.

## 보존·이슈·진행 조건

선행 T08 소스337개·시험370개·원시로그11개·산출물을 재확인했다. T08 현재문서7개는 history/2026-09-13-t08/에 보존했다. 최종 소스375개 중 검증 복사본371개가 일치하며 기존 URP 템플릿4개 차이는 원본을 보존하고 patch로 기록했다. 기존 meta·씬·패키지 파일을 보존했다. 기준595파일 대비 삭제는 없다. 원래 Editor·이전 Mac Host·기존 빌드와 실패 로그를 강제로 종료하거나 지우지 않았다.

빌드11의 첫 설치는 기기 잠금으로 실패했고, 사용자가 잠금 해제한 후 성공했다. 중간에 실패 기록 저장이 자동 승인 검토의 사용량 한도 오류로 거절됐으나 재개 시 정상 사용 가능 상태를 확인한 뒤 동일 작업을 완료했다. 우회하거나 실패를 숨기지 않았다. 빌드11 첫 Mac 컴파일의 기존 T04 obsolete 경고2개는 당시 로그에 보존했다.

네트워크 기록기는 동료 종료 후 final 필드를 읽는 문제를 별도 보완했다. 원래 실행의 승리 체크포인트는 정상이며, 보완 실행에서 종료 전 snapshot 전체가 일치함을 다시 확인했다. 빌드12에는 이 보완과 실제 중심 Drop 기록이 함께 포함됐다. 일반 Seed/개발용 Raw공급/단축시간/실제180초/실제Touch를 구분한다.

현재 남은 사람 작업은 **빌드12의 직접 조합→피격→회복→결과→Retry·첫 생성** 확인이다. 다음 요청 하나는 **T09 실기기 Core Loop 재검증 완료**이며 G4 통과 후 T10-A Lobby·자동 탐색·Ready 순서다. 커밋·push·배포와 T10-A/B를 시작하지 않았다.

## 근거

- [원본 비공개 자료: 최종 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Edit381 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Play65 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 수정 전 재현과 수정 후 결과 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 빌드11 실기기 실패 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 중심 조합 화면 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Victory — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Retry — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 180초 Defeat — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 최종 두 참가자 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실행 행 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 소스 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 — 공개 요약](VALIDATION_SUMMARY.md)
- [구현 결정](T09_DECISION.md) · [반복 절차](T09_RUNBOOK.md)
