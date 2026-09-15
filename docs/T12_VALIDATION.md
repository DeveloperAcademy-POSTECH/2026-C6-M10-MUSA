> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

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

# T12 · 빌드18 최종 기록

이번 마무리 범위는 최신 사용자 요청인 **상태바 중단 수정**이다. 수정·현재 앱 설치·두 기기 실제 확인은 PASS이며, 전체T12/G5는 완료하지 않았다.

| 구분 | 상태 | 현재 실행 근거 |
|---|---|---|
| 환경 | 확인 | `/path/to/2026-C6-M10-MUSA`, Unity6000.5.7f1/URP17.5.0, Xcode26.6, iPhone17+iPad mini6, 본인 Apple 개발 Team 개인Team, 빌드18 |
| Compile / EditMode / PlayMode | PASS | 886+86=972개. 빌드17의945개 누락0, 실패0, skip0. [원본 비공개 자료: 요약 — 공개 요약](VALIDATION_SUMMARY.md) |
| 첫 경계 검사 | FAIL 보존→수정 후PASS | 정확한1206px 오른쪽 경계의 float계산오차1건. [원본 비공개 자료: 원본 실패XML — 공개 요약](VALIDATION_SUMMARY.md) |
| Mac 두 프로세스 | PASS | 일반Seed·정상102전달(Raw51/Combined51)·공통382지문 일치, 받은구슬 조합·실제 공격자회복5·HP80. [원본 비공개 자료: 독립 대조 — 공개 요약](VALIDATION_SUMMARY.md) |
| iOS export / 앱빌드 / 서명 | PASS | 빌드18·공용iPhone+iPad·개인Team서명·native상태바수정 적용. [원본 비공개 자료: 앱 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 패치 — 공개 요약](VALIDATION_SUMMARY.md) |
| iPhone / iPad 설치·실행 | PASS | 두 CoreDevice 설치 성공, 같은 빌드18 GUID와 실제기기상태 확인. 설치원본은 로컬 archive의 install-*-build18-r1.json |
| 두 기기 상태바 | PASS | iPhone13.431초/시간13.398초감소, iPad11.719초/시간11.669초감소. 같은세션·2인·오류0·native Unity pause0. 사용자 양쪽 “잘돼”. [원본 비공개 자료: 기기대조 — 공개 요약](VALIDATION_SUMMARY.md) |
| 현재빌드 실제180초 종료 | PASS · 자동기록/화면범위 | 같은세션 Defeat·HP100·TIME0·2/2·오류0. 최종 화면을 직접 확인했다. [원본 비공개 자료: 최종 상태 — 공개 요약](VALIDATION_SUMMARY.md) |
| 수정 후 실제Touch 좌측전달 | NOT_RUN | 코드는 수정했고 자동경계20개통과. 실제손조작을 현재빌드에서 다시 확인해야 한다. |
| 실기기100회·전체공격/Victory/Retry / 전체G5 | NOT_RUN | Mac 결과와 빌드17 일부Touch 관찰을 승계하지 않는다. [원본 비공개 자료: 빌드17범위 — 공개 요약](VALIDATION_SUMMARY.md) |
| 실제background / T13 | NOT_RUN | 원래pause·Leave정책을 코드에서보존. 이번에는전경상태바만실제로검사했다. |

원문 두iPhone조건은 사용자승인 iPhone+iPad대체범위와 별개이며 실제두iPhone Gate는NOT_RUN이다. 이번상태바수정에는 남은차단이나추가사람작업이없다. 후속 **T12 남은실기기통합검증**에서는 실제좌우Touch와전체루프관찰이 필요하다. T13을 자동시작하지 않는다.

기존원본1368개 해시를 이관직전 확인하고 T12관련27개만 반영했다. 기존씬13·meta260·Config4·Packages2는보존했으며 무관한URP4/SceneTemplate1은 제외했다. 기존문서와AGENTS는 이력보존 후 현재절만추가했다. 빌드17·18원본/중간실패/기기증거는 로컬Logs와Builds에 보존한다. Git commit/push는 하지 않았다.

[원본 비공개 자료: 원시기록 경로와해시 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 이관계획 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 이관결과 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 사용자관찰 — 공개 요약](VALIDATION_SUMMARY.md).

---

## 이전 중간 기록

아래 내용은 당시 상태와 실패를 그대로 보존한 이력이다. 현재 상태는 위 최종표를 따른다.

# T12 현재 검증 — 빌드 17 부분 완료·빌드 18 수정 진행

2026-09-13 현재 빌드 17은 **iPhone 17 Host+iPad mini 6 Client의 Bonjour 참가·정상 생성·조합·오른쪽 Combined 전달 3회·무조작 180초 Defeat와 결과 유지**까지 확인했다. **T12/G5 전체는 NOT_RUN**이다. 왼쪽/Raw 전달·실기기 100회·전달받은 Raw 조합·실제 공격/회복·Victory·Retry 등은 남아 있다. 원문 두 iPhone 조건도 NOT_RUN이다.

사용자는 마지막 재현 후 **“끝나고 나오는 화면은 문제없어보여. 상태바 열었을때 중단 처리만 수정하자”**라고 확인했다. 결과 화면 보호 UI는 추가하지 않는다. 빌드 18은 상태바 등 전경 시스템 화면에서 타이머/연결이 유지되도록 수정 중이다. 빌드 18 Prepare와 경계값 최소 수정 후 자동 검사972개(Edit886/Play86)는 PASS다. 기존945개를 유지하고 신규27개가 추가됐으며 실패·skip·누락은0이다. 첫 EditMode의 경계값1개 FAIL은 과거 시도 근거로 보존했다. Mac 사전 실행은 진행 중이며 최종 iOS export·앱 서명·기기 실행은 아직 NOT_RUN이다. **빌드 17의 기기 PASS를 빌드 18에 승계하지 않는다.**

이 파일은 stage의 현재 검증 초안이다. 아래 이전 작성 시점의 기록을 보존하며, 실제 저장소 문서·과거 원본 로그는 수정하지 않았다. 상태는 PASS/FAIL/NOT_RUN/BLOCKED만 사용한다. 완료된 capture 명령의 PASS와 해당 캡처 속 게임 성공은 구분한다.

## 빌드별 실행 현황

| 구분 | 빌드 | 상태 | 확인된 범위·근거 |
|---|---|---|---|
| Compile·Prepare | 17 | PASS | 기존 `prepare-r1`과 새 IntegratedDeviceBattle 연결 |
| 자동 검사 | 17 | PASS | Edit859+Play86=945, 기존914 유지·신규31, 실패/누락0. [원본 비공개 자료: 자동 결과 — 공개 요약](VALIDATION_SUMMARY.md) |
| Mac 사전 실행 | 17 | PASS | 직접 IP, Raw50+Combined50+연결흐름2=102회, 공통 snapshot376개 불일치0. 실기기 횟수로 계산하지 않음. [원본 비공개 자료: Mac 결과 — 공개 요약](VALIDATION_SUMMARY.md) |
| iOS export·앱 빌드/서명·두 기기 실행 | 17 | PASS | 동일 빌드17/Build GUID `f27c25af281f48dea08b190053dbd97b`의 실제 iPhone/iPad 보고서·로그 확인. [원본 비공개 자료: 기기 audit — 공개 요약](VALIDATION_SUMMARY.md) |
| Bonjour 실제 발견·참가·같은 방 | 17 | PASS | iPad `route=BONJOUR`, iPhone Host, 같은 room/session/Config. 직접 IP로 대체한 결과가 아님. |
| 무조작 실제180초·양쪽 Defeat·결과 고정 | 17 | PASS | 세 번째 세션 `dde2f8b7556a48bf9d56b3f117701551`, 일반 Seed785918467. 결과 필드와 HUD를 따로 비교. |
| 상태바에서 타이머/연결 지속 | 17 | FAIL | 두 번째 세션에서 iPhone OnApplicationPause→Leave·연결 오류. 사용자가 상태바를 드래그했다고 명시. 최신 요구에 맞게 수정할 대상. |
| 좌우·Raw/Combined 균형100회와 T12/G5 전체 | 17 | NOT_RUN | 오른쪽 Combined Touch3회만 확인됨. 아래 남은 AT 참조. |
| 최초 Prepare·씬 연결 | 18 | PASS | root가 확인한 빌드18 Prepare 범위. 이후 native 변경의 최종 검사와 구분. |
| 최초 전체 EditMode | 18 | FAIL | 886개 중885 PASS·실패1·skip0. 1206 너비의 정확한 경계값 판정1개 실패를 원본 [원본 비공개 자료: XML — 공개 요약](VALIDATION_SUMMARY.md)에 보존. |
| 새 native 패치 회귀 | 18 | PASS | 최초 및 최종 재검사에서 T12ForegroundLifecyclePatchTests 7개 모두 PASS. 소스 변환·범위·보존/거부 검사이며 실제 iOS 상태바 동작 증거는 아님. |
| 경계값 수정 후 전체 자동 재검사 | 18 | PASS | Edit886/Play86=972, 기존945 유지·신규27, 실패·skip·누락0. [원본 비공개 자료: 최종 요약 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: Edit XML — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: Play XML — 공개 요약](VALIDATION_SUMMARY.md). |
| Mac 사전 실행 | 18 | NOT_RUN | `/path/to/2026-C6-M10-MUSA/Logs/T12/session-20260913/mac-build18-r1` 실행 진행 중. 원본 명령/영수증/공통 지문을 요약 완료 후 대조. |
| 최종 iOS export·앱 빌드·기기 실행 | 18 | NOT_RUN | 현재 빌드의 완료 증거 확인 후 갱신. |
| 원문 실제 두 iPhone Gate | 17·18 | NOT_RUN | 사용자 승인 iPhone17+iPad 대체 조합만 사용. |

빌드17의 실행 근거는 [원본 비공개 자료: 기기 원본 대조 audit — 공개 요약](VALIDATION_SUMMARY.md)에 파일 경로·SHA256·세션별 비교 기준으로 남겼다. 9개의 `*-result.json`과 원본 JSON271개·JSONL78개를 읽었고 파싱 오류0이다. 반복 수집된 같은 보고서를 별도 성공 횟수로 계산하지 않았다. `capture`가 PASS여도 `gameError=PARTICIPANT_DISCONNECTED`인 두 번째 실행은 실패 상태로 보존했다.

## 빌드17 세 실행을 구분한 원인 판정

| 실행 | 확인한 결과 | 판정 가능한 범위 |
|---|---|---|
| 첫 세션 `f131b53e89ec444a835464515e3b3a1f` | 일반 Seed3279724385, 각 기기 정상 Generate2회·매회 Raw1/비용20, iPhone Yin+Yang 조합1회, 동일 Combined ID 오른쪽 Touch3회, 정상180초 Defeat 뒤 이탈 | iPad 로그에 UI pointer-click→EndDevelopmentTest→Leave 호출 경로가 있다. 사용자는 결과 버튼을 누르지 않았다고 보고했다. 실제 클릭 의도·stale touch·Result 전환 오류 중 원인은 확정하지 않는다. 이 기록을 삭제하거나 사용자 잘못으로 결론내리지 않는다. |
| 두 번째 세션 `44672806e5d840f896329bfb15321ec7` | 일반 Seed1769248081, 약103.222초 남은 Playing 상태에서 연결 오류 | iPhone 로그의 `OnApplicationPause→Leave`와 상태바 드래그 사용자 보고가 일치한다. 이 실행은 정상180초 종료가 아니다. 상태바를 열어도 지속되어야 한다는 최신 요구에 대한 빌드17 실패로 분리한다. |
| 세 번째 세션 `dde2f8b7556a48bf9d56b3f117701551` | 일반 Seed785918467, 실제180초 후 양쪽 Defeat 유지·오류 없음 | 무조작 재현에서 정상 Result 경로가 확인됐다. 사용자가 결과 화면은 문제없다고 확인했으며 UI 보호 수정은 취소했다. 첫 세션의 미확정 원인을 소급 확정하거나 두 번째 상태바 실패를 지우지 않는다. |

세 번째 세션의 Host 시작 시각964.3001035053856·종료 시각1144.3001035053856의 차이는 **180초**다. 첫 Defeat clock 샘플은 iPhone 08:41:44.345085 UTC, iPad 08:41:44.398196 UTC다. `timeout-repro2-ended`의 08:42:34/35와 `timeout-repro2-frozen`의 08:43:51은 그 후의 결과 확인 캡처 시점이다.

두 기기에서 ended→frozen 사이 Monster HP100·Team HP0·TIME0·개인 Stamina100/100·구슬0·명중0·Defeat·입력 불가·Pending없음이 동일했다. 결과 제목과 최종 수치 HUD도 유지됐다. Aggregate revision은 iPhone2452→3090, iPad2454→3089로 계속 증가하므로 **전체 hash 고정을 결과 고정의 조건으로 사용하지 않았다.** 같은 세션·같은 round/revision의 공통 proof는 첫 실행2519개, 두 번째867개, 세 번째3089개이며 hash 불일치는 모두0이다.

세 번째 Playing의 가까운 UTC 샘플337쌍에서 표본 시각 차이는 최대0.089초, 표시 TIME 차이는 최대0.211초로 사전 기준1초 이내였다. 이는 표본 비교이며 모든 프레임을 동시에 촬영한 측정은 아니다. 구슬 TouchBegins 누계는 Playing/ended/frozen에서 iPhone11·iPad2로 증가0이다. 이 카운터는 구슬 드래그 시작 누계이므로 모든 UI·시스템 Touch의 부재를 단독 증명하지 않는다. 무조작·결과 화면 정상은 별도의 사용자 관찰과 연결해 기록했다.

## 현재 AT 판정 — 빌드17·승인된 iPhone/iPad 범위

최신 규칙인 시작0개/100, Generate1개/20, 시간회복20/3초, 실제 공격자 hit+5를 적용한다. 원문 AT 번호와 목적은 보존한다. 아래 PASS를 빌드18 또는 원문 두 iPhone 환경에 복사하지 않는다.

| 번호 | 현재 상태 | 확인된 내용과 남은 증거 |
|---|---|---|
| AT-01 방 생성 | PASS | 실제 iPhone Host 방과 iPad에서 참가 가능한 동일 방 확인. |
| AT-02 발견한 방 참가 | PASS | 실제 iPad Bonjour route·같은 room/session 연결 확인. |
| AT-03 순서·인접 관계 | NOT_RUN | P1/P2 좌석·Ready/Start는 확인. 양쪽 Left/Right 표시의 전체 실기기 관찰은 별도 확인 필요. |
| AT-04 초기 생성 | PASS | 빈 시작·Stamina100·각 첫 Generate Raw1/비용20 확인. 원문 첫5개 일괄 생성으로 기록하지 않음. |
| AT-05 올바른 음양 조합 | PASS | iPhone 본인 생성 Yin+Yang 두 ID 종료·새 Combined ID1개. 전달받은 Raw 조합은 T12 전체흐름에서 추가 필요. |
| AT-06 같은 음양 거부 | NOT_RUN | 현재 빌드의 확정된 거절·원본 보존 증거 없음. |
| AT-07 왼쪽 전달 | NOT_RUN | 요청 안내와 실제 요청/승인을 구분. 확인한 로그에 왼쪽 전달 없음. |
| AT-08 오른쪽 전달 | NOT_RUN | Combined 오른쪽 Touch3회는 PASS 범위이나 Raw도 포함하는 항목 전체는 아직 미완료. 수신 왼쪽 진입·owner0→1→0→1·동일 ID 보존 확인. |
| AT-09 공격 시작 | NOT_RUN | 실제 launch승인 없음. `TRANSFER_APPROVED` 영수증을 공격으로 계산하지 않음. |
| AT-10 2D→3D | NOT_RUN | 현재 빌드의 실제 발사체 변환 관찰 필요. |
| AT-11 실제 피격 | NOT_RUN | 빌드17 기기 실제 명중0. Rigidbody/Collider·피해1회·최종Consumed 확인 필요. |
| AT-12 Clock·Team HP | PASS | 세 번째 정상180초, 양쪽 TIME/Team HP 감소·종료0·결과고정. 표시차 최대0.211초(표본비교). |
| AT-13 Victory | NOT_RUN | 정상 피격5회·양쪽Victory·고정 결과 필요. |
| AT-14 Defeat | PASS | 세 번째 무조작 정상180초·양쪽Defeat·고정 결과·사용자 화면 확인. 상태바 연결 상실 실행과 구분. |
| AT-15 생성 비용 | PASS | 양쪽 후속 Generate도 Raw1/비용20. 부족자원/상한 거절의 현재 기기 추가 증거는 별도 미실행. |
| AT-16 공격자 회복 | NOT_RUN | 전달받은 Combined 실제 공격자만 hit+5·시간회복분리 필요. |
| AT-17 Raw 공격 거부 | NOT_RUN | Raw보존·무피해·입력잠금 해제의 현재 기기 증거 필요. |
| AT-18 중복·상태 동기화 | NOT_RUN | 같은 세션/round/revision의 공통 proof 총6475개 불일치0은 확인. 좌우/Raw/Combined100회·공격/조합 전체 범위는 미실행. |

첫 실행의 정상 생성 영수증은 매회 직전100→직후80이며, 수집 시점에는 시간회복으로 다시100이었다. 사후 수치만 보고 비용 미차감으로 해석하지 않는다. 조합된 ID `8e79cc03e3684f3483f35d76c043c6bd`의 오른쪽 전달3회는 Host2회·Client1회로 확인됐다. Raw 전달 또는 왼쪽 전달, 전달받은 Raw 조합, 실제 명중은 확인되지 않았다. 화면 전체 Safe Area/가독성·양쪽 높이·확대/빛 효과의 전체 관찰은 추가 확인한다.

## 남은 T12 작업

빌드18의 첫 EditMode 실패는 `ReachableEdgeGestureTests.DerivedThresholdIsInclusiveAndRejectsShorterOutwardMotion(1206.0f)`의 정확한 경계에서 요청이 생성되지 않은 사례다. 최소 수정 후 전체 재검사972개가 PASS했고 원본 실패 XML을 보존한다. 최초 실패 실행과 최종 성공 실행을 구분하며, native 패치7개만으로 전체 실행을 통과 처리하지 않았다.

root는 `T12DeviceDiagnostics`에 OnApplicationFocus/OnApplicationPause의 UTC·세션·phase·remaining을 기록하는 관찰 이벤트를 추가했다. 이는 수동 조작을 대신하거나 로비/전투를 자동 실행하는 기능이 아니다. 다음 실기기 실행에서 상태바 전경 비활성과 실제 백그라운드의 이벤트 순서·Clock 연속성을 대조한다.

빌드18의 최종 컴파일·자동 회귀·iOS export·앱 빌드/서명·설치 후, 전경 상태바를 연 상태에서 타이머가 실제로 계속 흐르고 두 참가자 연결이 유지되는지 새 증거로 확인한다. 실제 백그라운드 이동과 전경 시스템 화면을 구분하며, 원래 실제 백그라운드 중단 정책을 전경 상태바 수정의 성공 근거로 사용하지 않는다.

이후 현재 빌드에서 실제 Touch·왼쪽/Raw 전달·균형100회·수신Raw 조합/수신Combined 공격·실제 hit+5·Victory·Retry 등 남은 AT를 진행한다. 두 기기 승인 범위와 원문 두 iPhone 미실행을 계속 구분한다. **T13을 자동으로 시작하지 않는다.**

---

## 보존: 기기 실행 전 중간 초안

아래는 빌드17의 기기 실행 전 작성한 기록이다. 아래의 설치·AT NOT_RUN은 당시 상태이며, 현재 상태는 위 표를 따른다. 실행 실패와 원본 증거를 포함한 이력을 유지하기 위해 본문을 보존한다.

# T12 중간 검증 — 빌드 17

작성 기준은 2026-09-13 T12 자동 검사·Mac 사전 실행·iOS 프로젝트 생성까지 확인된 시점이다. **현재 T12/G5 실기기 전체 검증은 NOT_RUN이다.** 이 문서는 `/path/to/2026-C6-M10-MUSA/Logs/T12/session-20260913/T12_VALIDATION.md`에 작성한 중간 초안이며, 실제 저장소의 현재 문서·과거 검증 이력은 수정하지 않았다. 이후 장치 실행 결과와 증거가 확인되면 해당 항목을 갱신한다.

상태는 PASS / FAIL / NOT_RUN / BLOCKED만 사용한다. 진행 중인 작업도 완료 증거가 확인되기 전에는 NOT_RUN으로 둔다. 문서 승인, 준비된 테스트 또는 이전 빌드의 기기 PASS를 현재 빌드의 실행 완료로 해석하지 않는다.

## 범위·환경·사용자 변경

| 항목 | 이번 검증 기준 |
|---|---|
| Task | T12 / G5, 두 참가자의 전체 전투 흐름과 실기기 검증 |
| 실제 프로젝트 / 검증 복제본 | `/path/to/2026-C6-M10-MUSA` / `/private/tmp/C6_Prototype_T12_Verification` |
| Unity / 새 씬 | Unity 6000.5.7f1 / `Assets/_Project/HapioMVP/Scenes/IntegratedDeviceBattle.unity` |
| 빌드 / iOS Bundle ID | 17 / `com.wolfuraark.c6prototype` |
| 선택한 서명 Team | 본인 Apple 개발 Team 개인 Team `YOUR_TEAM_ID`. 선택 사실과 서명 완료는 별도다. |
| 사용자 승인 실기기 조합 | iPhone 17 Host + iPad mini 6 Client. 연결·잠금 해제 준비 확인은 설치·실행의 증거가 아니다. |
| 원문 두 iPhone 조건 | 실제 두 iPhone 실행·화면 QA는 NOT_RUN. iPad 대체 결과를 두 iPhone 실행으로 확대하지 않는다. |
| 공통 조건 | 같은 Wi-Fi, 세로 화면, 동일 빌드·Config, P1 Host/P2 Client, 각 기기의 Touch·Safe Area·같은 기본 몬스터 구도 |
| 원본 Config | `Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset`, 파일 SHA256 `795a9c0480d4773242ebc64ed99188002dd9a52b276128b65c31433b4c0c73bd` |
| Mac 실행 Config 지문 | `6e3fd1258b8aa457dcdefb9cb843ac3331ad3f9a49225299082480b57dea9011`. 파일 SHA256과 런타임 설정 지문은 서로 다른 값이다. |

최신 사용자 지시를 우선 적용한다. 일반 시작은 **구슬 0개·개인 Stamina 100/100**, 첫 요청과 이후 요청 모두 **Generate당 Raw 1개·비용 20**이다. 시간 회복은 3초당 20을 연속값으로 계산하고, 실제 유효 명중은 실제 공격자에게만 **+5**를 한 번 지급한다. 최대값은 100이다. 원문 AT-04의 첫 Raw 5개/비용 5/최초 배치 플래그, AT-15의 비용 1, AT-16의 회복 1은 이번 판정에 사용하지 않는다. AT 번호와 목적은 보존하며 변경된 수치·시작 조건을 아래 표에 명시한다.

조합은 하단 전체에서 가능하다. 구슬을 잡으면 확대·빛 효과를 표시하고, Combined를 상단 전투 경계로 넘기면 발사를 요청한다. 좌우 전달은 T11의 거리·수평 우세 조건을 만족한 뒤 해당 끝에서 손을 놓을 때 요청한다. 손을 놓기 전 전달하지 않으며, 조합 대상 위의 Drop은 유효·무효 조합 모두 전달보다 우선한다. 상단 발사 경계 판정도 유지한다. 전달 뒤 같은 ID와 속성을 보존하고 반대쪽 경계·정규화된 높이에 배치하며, 새 Gesture 없는 자동 반송은 허용하지 않는다.

적용 근거는 [통합 지시서 5장 T12·8장 AT-01~18](../C6_Hapio_Codex_Integrated_Master_Prompt.md), [현재 프로젝트 규칙](../AGENTS.md), [원본 비공개 자료: T12 결정 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: T12 실행 절차 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 사전 환경 기록 — 공개 요약](VALIDATION_SUMMARY.md)이다.

## 확인한 실행 결과

| 검증 구분 | 현재 상태 | 확인한 근거와 범위 |
|---|---|---|
| Compile·새 씬 준비 | PASS | `prepare-r1` exit 0, `C6_T12_PREPARED`, 빌드 17·새 씬·기존 단일 Config 연결. [원본 비공개 자료: 기록 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 사전 요약 — 공개 요약](VALIDATION_SUMMARY.md) |
| EditMode | PASS | 859/859, 실패·skip·inconclusive 0. 기존 828개 유지·신규 31개, 누락 0. [원본 비공개 자료: XML — 공개 요약](VALIDATION_SUMMARY.md) |
| PlayMode | PASS | 86/86, 실패·skip·inconclusive 0. 기존 86개 유지, 누락 0. [원본 비공개 자료: XML — 공개 요약](VALIDATION_SUMMARY.md) |
| 자동 검사 합계·기존 검사 유지 | PASS | 합계 945/945 = 기존 914 + 신규 31. XML 실행 결과와 이름별 유지·누락 검사를 확인했다. [원본 비공개 자료: 요약 — 공개 요약](VALIDATION_SUMMARY.md) |
| macOS 앱 빌드 | PASS | `C6_T12_BUILD_COMPLETE target=StandaloneOSX`, `/path/to/2026-C6-M10-MUSA/Builds/T12-r1/macOS/C6Integrated.app`. [원본 비공개 자료: 로그 — 공개 요약](VALIDATION_SUMMARY.md) |
| Mac 두 프로세스 사전 실행 | PASS | `t12-mac-r1`, DIRECT_IP, 일반 Seed 3798873737. 자동 전달 102회, 받은 Raw 조합·받은 Combined 실제 피격, 공통 snapshot 376개 지문 불일치 0. [원본 비공개 자료: 요약 — 공개 요약](VALIDATION_SUMMARY.md) |
| iOS Xcode 프로젝트 생성 | PASS | `C6_T12_BUILD_COMPLETE target=iOS`, 출력 `/path/to/2026-C6-M10-MUSA/Builds/T12-r1/iOS`. Bonjour plist 적용 marker도 확인했다. [원본 비공개 자료: 로그 — 공개 요약](VALIDATION_SUMMARY.md) |
| 빌드 17 Xcode 앱 빌드·개인 Team 서명 | NOT_RUN | 작성 시점 빌드 작업 진행 중이며 최종 성공·서명 확인 결과는 아직 반영하지 않았다. [원본 비공개 자료: 진행 로그 — 공개 요약](VALIDATION_SUMMARY.md) |
| 빌드 17 iPhone 설치·실행 | NOT_RUN | 현재 빌드의 설치 결과·실행 식별·장치 로그 확인이 필요하다. |
| 빌드 17 iPad 설치·실행 | NOT_RUN | 현재 빌드의 설치 결과·실행 식별·장치 로그 확인이 필요하다. |
| 두 기기 Bonjour 자동 방 발견·선택·참가 | NOT_RUN | Mac의 직접 IP 성공과 plist 생성은 실기기 탐색 결과가 아니다. |
| 두 기기 실제 Touch·화면·Safe Area | NOT_RUN | 자동 명령 전송·Mac 화면 비율 검사는 실제 손 조작 증거가 아니다. |
| 실기기 좌우 전달 100회·정보 보존 | NOT_RUN | Mac의 102회를 실기기 100회로 계산하지 않는다. |
| 승인된 iPhone 17+iPad의 AT-01~18·T12 전체 | NOT_RUN | 아래의 현재 빌드 실기기 증거가 아직 없다. |
| 원문 두 iPhone의 AT-01~18·G5 | NOT_RUN | 실제 두 iPhone 환경에서 실행한 결과가 없다. |

위 자동 검사 수는 결과 XML의 실행 수·성공·실패·누락에 따른다. 테스트 파일 존재 또는 빌드 준비만으로 PASS를 기록한 것이 아니다. `preflight-summary.json`의 작성 당시 `implementationTests=NOT_RUN`은 이후 나온 `tests-summary.json`과 XML로 갱신된 항목이며, 사전 기록 원본은 그대로 보존한다.

## Mac 사전 실행의 의미와 제한

`mac-r1/summary.json`에는 `physicalDevice=false`, `joinRoute=DIRECT_IP`가 명시되어 있다. Raw 50회 series와 Combined 50회 series의 **100회**에, 받은 Raw를 조합하고 Combined를 돌려보내는 연결 흐름의 **2회**가 더해져 총 **102회**다. 두 프로세스가 각 전달 결과를 관찰한 후 다음 요청으로 진행했다. 376개의 공통 snapshot에서 지문 불일치는 0개였다. 실제 기기 수신 화면·Touch·자동 방 탐색에 대한 판정은 포함하지 않는다.

[원본 비공개 자료: Host 최초 캡처 — 공개 요약](VALIDATION_SUMMARY.md)와 [원본 비공개 자료: 마지막 캡처 — 공개 요약](VALIDATION_SUMMARY.md)에서 동일 일반 Seed와 `debugTestMode=false`, `developmentSolo=false`, `shortDuration=false`를 확인했다. 기본 전투시간은 180초, 생성 비용은 20, 회복 속도는 초당 20/3, 유효 피격 보너스는 5다. 마지막 캡처는 Playing·Monster HP 80·실제 명중 1회다. 따라서 이 Mac 사전 실행으로 현재 빌드의 Victory·Defeat·Retry 전체를 통과 처리하지 않는다.

100회 반복은 이번 명시된 시험 횟수의 정보 보존 결과다. 긴 시간 또는 다른 네트워크에서의 안정성을 보증하는 수치로 확대하지 않는다. 진단 자동 요청은 정상 Host 승인 경로를 사용하되 사람의 실제 Touch 횟수에는 포함하지 않는다.

## AT-01~18 실기기 증거 준비

다음 표의 상태는 **현재 빌드 17의 사용자 승인 iPhone 17+iPad 조합에서 필요한 실기기 판정**이다. 이전 Task나 Mac 자동 결과를 이 칸에 승계하지 않는다. 원문 두 iPhone 판정도 현재 모두 NOT_RUN이다. 각 결과에는 runId·빌드·Config·세션·round/revision·해당 명령 또는 Touch·양쪽 로그/화면·관찰 시각을 연결한다.

| 번호 | 목적·이번 적용 기준 | 현재 상태 | 필요한 실기기 증거 |
|---|---|---|---|
| AT-01 | 파티 방 생성 | NOT_RUN | iPhone의 CREATE ROOM 실제 Touch, 새 방 식별자와 P1 Host 표시, 참가 가능한 Lobby 상태 및 Host 로그. |
| AT-02 | 자동으로 발견한 방 참가 | NOT_RUN | 같은 Wi-Fi에서 iPad FIND ROOMS로 방 발견, 발견한 목록의 JOIN 실제 선택, 양쪽 같은 방·2인 연결 표시와 Bonjour 발견/참가 로그. 직접 IP 결과는 별도 기록. |
| AT-03 | P1/P2 순서·좌우 이웃 관계 | NOT_RUN | 양쪽 내부 좌석 0/1과 UI P1/P2의 매핑, iPhone Host·iPad Client, 양쪽 Left/Right가 상대를 가리키는 표시 및 같은 Lobby revision. 양쪽 Ready·Host Start도 연결해 기록. |
| AT-04 | 빈 시작과 첫 개별 생성 | NOT_RUN | 양쪽 READY/시작 시 구슬 0개·Stamina 100, Playing 후 각각 첫 Generate Touch로 Raw 1개만 생성, 고유 ID·비용 20이 같은 Host 승인에서 확정된 기록. 첫 5개 일괄 생성 또는 최초 배치 플래그를 기대하지 않음. |
| AT-05 | 올바른 Yin+Yang 조합 | NOT_RUN | 하단 전체에서 반대 음양 Raw 두 개를 실제 Drag & Drop. 전달받은 Raw를 포함한 조합에서 재료 2개 종료·새 ID의 Combined 1개·Polarity None·한 번 승인, 양쪽 동일 상태와 화면 확인. |
| AT-06 | 같은 음양 조합 거부 | NOT_RUN | Yin+Yin 또는 Yang+Yang 실제 Drop 후 원본 두 ID와 자원 보존·Combined 미생성·입력 잠금 해제 확인. 끝 근처 대상 Drop도 전달로 잘못 전환되지 않는 관찰과 거절 기록. |
| AT-07 | 왼쪽 전달 | NOT_RUN | 양쪽 송신자가 Raw/Combined를 왼쪽 끝으로 끌고 손을 뗀 Touch. 송신 화면 제거·수신 오른쪽 진입·같은 ID/종류/음양·owner/전달 횟수 한 번 변경·높이 보존 확인. 손을 놓기 전 전달 및 새 Gesture 없는 자동 반송이 없어야 함. |
| AT-08 | 오른쪽 전달 | NOT_RUN | 양쪽 송신자의 오른쪽 끝 Release와 수신 왼쪽 진입을 Raw/Combined 모두 확인. AT-07과 같은 상대여도 Entry Side를 구분하고, 서로 다른 화면 비율의 위/가운데/아래 높이와 ID/속성을 대조. |
| AT-09 | 공격 시작·중복 요청 방지 | NOT_RUN | Combined를 상단 전투 경계로 실제 Drag한 첫 진입의 요청과 Host 승인, 승인 후 2D 뷰 제거. 같은 ID의 재발사·전달·조합이 대기 중 중복 승인되지 않는 요청/상태 기록. |
| AT-10 | 같은 ID의 2D→3D 전환 | NOT_RUN | 양쪽에서 Combined 2D 뷰가 사라지고 같은 OrbId의 PROJECTILE이 생성·비행하는 화면과 로그. 공격자 정규화 가로 위치를 사용하며, 최종 CONSUMED는 명중/만료 시점임을 상태 기록으로 확인. |
| AT-11 | 실제 몬스터 피격·피해 한 번 | NOT_RUN | Host Rigidbody 비행·Monster Collider 실제 충돌, 동일 hit/OrbId의 피해 20 한 번, Projectile 제거·최종 CONSUMED·양쪽 HP 일치. Debug Damage·타이머 피해·Force 판정 없이 확인. |
| AT-12 | Host Clock에 따른 Team HP 감소·표시 일치 | NOT_RUN | 같은 세션·round·Host 종료 시각에서 Playing 동안 시간과 Team HP 감소를 양쪽 연속 캡처/샘플로 대조. 사전 허용 TIME 차이 1초 이내, 초당 Team HP 1 감소에 대응하는 표시 차이도 기록. Lobby/Ready/Result에서는 고정. |
| AT-13 | 정상 Victory | NOT_RUN | 180초 종료 전 실제 유효 피격을 반복해 Monster HP 0, Host Victory 판정·양쪽 같은 결과, 시간/HP/자원/입력 고정. 화면과 실제 hit 5회의 승인·충돌 로그를 연결. Force Victory는 제외. |
| AT-14 | 정상 180초 Defeat | NOT_RUN | 별도 일반 라운드에서 실제 180초 Clock 경과·Team HP/TIME 0·Host Defeat·양쪽 같은 결과와 숫자/입력 고정. 단축 시간·강제 판정·연결 종료를 시간 만료 증거로 사용하지 않음. |
| AT-15 | 개별 생성 비용·거절 시 보존 | NOT_RUN | 후속 Generate도 Raw 1개·비용 20임을 정상 요청/승인으로 확인. 부족 자원 또는 상한으로 거부된 요청은 추가 구슬/비용 차감 없이 종료. 정상 시간 회복분을 따로 계산해 전후 표시를 단순 뺄셈으로 오판하지 않음. |
| AT-16 | 실제 공격자에게만 명중 회복 +5 | NOT_RUN | 전달받은 Combined의 생성자/조합자와 실제 발사자를 구분. 공격자 자원에 여유를 만든 뒤 실제 유효 hit의 +5 이벤트가 한 번 발생하고 상대에게 동일 hit 보너스가 없음을 양쪽 플레이어별 자원 로그로 확인. 시간 회복·상한 100·중복 hit/미명중을 구분. |
| AT-17 | Raw 공격 거부·원본 보존 | NOT_RUN | Raw를 상단 경계로 실제 Drag한 뒤 Projectile·피해가 없고 같은 Raw ID가 남는 화면/거절 기록. 조작 가능 상태로 돌아와 영구 Pending이 남지 않음을 확인. |
| AT-18 | 중복 방지·양쪽 권한 상태 일치 | NOT_RUN | 승인된 전달 100회(Raw 50/Combined 50)와 조합/발사 과정의 같은 session/round/revision 상태를 비교. 한 ID의 유효 소유자·조작 가능한 뷰가 하나이며, 양쪽 Monster HP·Team HP·Battle State와 각 플레이어별 Stamina가 일치함을 지문/원본 snapshot/화면으로 확인. 서로 다른 두 플레이어의 개인 Stamina가 같아야 한다는 뜻은 아님. |

실기기 100회 계획은 P1 왼쪽 / P1 오른쪽 / P2 왼쪽 / P2 오른쪽 각각 25회다. Raw/Combined 분배는 각각 13/12, 12/13, 12/13, 13/12로 합계 Raw 50·Combined 50을 맞춘다. 재전송된 중복 패킷·거절·타임아웃은 승인된 전달 횟수에 포함하지 않으며 실패 시도는 보존한다. 사람이 100번 조작해야 하는 절차로 만들지 않고 명시적으로 시작한 진단 자동 요청을 사용하되, 표의 실제 Touch·확대/빛 효과·양쪽 진입 화면 관찰은 별도로 수행한다.

## AT 외 필수 통합 확인과 남은 작업

| 항목 | 현재 상태 | 완료에 필요한 증거 |
|---|---|---|
| 화면 구도·Safe Area·입력 피드백 | NOT_RUN | 두 실제 기기의 상단 3D/하단 2D·동일 기본 몬스터 구도, 잘리지 않는 HUD/버튼/글자, 잡을 때 확대·빛 효과, 조합/전달/발사 피드백의 사용자 관찰과 화면. |
| 결과 이후 Retry·재시작 | NOT_RUN | Victory와 Defeat 이후 Retry에서 새 라운드 READY·구슬 0·Stamina 100·Monster HP 100·TIME 180이 양쪽에 확인된 뒤 Host Start. 첫 Generate Raw 1개와 새 라운드 ID, 이전 요청/구슬 비활성 확인. |
| 시간·명중 회복 분리 | NOT_RUN | 정상 연속 시간 회복 20/3초와 유효 hit 보너스 +5의 개별 원인 로그. 대기·결과의 수치 고정과 최대 100 제한. |
| 기기 설치·실행 증거 묶음 | NOT_RUN | 빌드 17 앱·서명·설치/실행 식별, 각 기기/OS·Config·일시·runId, 원본 로그·스크린샷·사용자 Touch 관찰의 경로. |

현재 확인된 자동 검사·Mac 실행·iOS export에는 미해결 FAIL이 없다. Xcode 최종 결과, 설치·실행, 실제 Bonjour·Touch·AT 결과는 아직 확인 전이므로 전체 T12/G5를 PASS로 선언하지 않는다. 실제 기기 절차는 사용자 승인 iPhone 17+iPad 조합으로 계속 진행하며, 사람의 방 생성/발견/선택·실제 구슬 조작·화면 관찰을 요청해야 한다. 기기 연결·잠금 해제 또는 개발자 모드 등 실행 장애가 발생하면 그때의 실제 오류와 필요한 사람 작업을 별도로 기록한다.

이번 요청의 다음 작업은 **현재 T12의 실기기 검증과 증거 정리**다. **T13을 자동으로 시작하지 않는다.**
