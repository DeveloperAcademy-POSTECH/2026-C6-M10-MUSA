> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

<!-- C6:T12:FOLLOWUP19:BEGIN -->
## T12 빌드19 · 추가 조작·승리·초기화 확인

이번 요청의 추가 검증과 기록 정리를 완료했다. 실제 수신Raw 조합/공격 거부·Combined 전달/물리피격 및 별도 정상5hit·공격자회복5·Victory·Retry·첫Generate를 확인했다. 추가 사람 조작을 요구하지 않았고 게임/앱/Config 변경·100회 반복은 없다.

현재 확인 항목은 PASS이며 전체T12/G5 최종판정은 NOT_RUN이다. 빌드18의100회PASS와 빌드19의100회NOT_RUN을 분리한다. 정확한 짧은 외곽거리 직접재현은NOT_RUN으로 남기며 과도한 손 위치 맞추기를 추가 요구하지 않는다. [상세 결과·실패 보존·다음 판정](T12_BUILD19_FOLLOWUP.md)을 따른다. T13은 시작하지 않았다.
<!-- C6:T12:FOLLOWUP19:END -->

## 2026-09-13 최신 확인 · 빌드19 오른쪽 왕복 Touch

빌드19에서 **iPhone 오른쪽 → iPad 왼쪽 도착 → iPad 오른쪽 → iPhone 왼쪽 도착**을 실제 Touch와 두 요청의 Host 승인·최종 소유권으로 확인했다. 사용자도 마지막 iPhone 왼쪽 도착을 확인했다. 같은 Combined `c4dafecddf6b47a2a3afb88e82cf0ca2`의 transferCount는0→1→2이며 복제 없이 최종 iPhone에만 표시됐다. 양쪽 오른쪽 전달은 모두 UP에서 요청됐고, 손떼기 전 유지 관찰도 남겼다.

중간 iPad 입력1회는 TouchPhase.Canceled로 끝나 요청이 생성되지 않았다. 사용자 최초 왕복 정상 답변 직후에도 구슬이 iPad에 남아 있어 이 차이를 확인했고, 이후 정상 UP 요청과 반환을 새 근거로 기록했다. 취소된 시도는 성공으로 세지 않았다. 취소를 일으킨 물리적/OS 원인을 단정하지 않으며, 실제 재시도의 기록은 오른쪽 끝 x1488에서 정상 UP다. 안내한2~3mm를 측정값으로 기록하지 않는다. 최초 빌드18 iPhone 미전달은 별도의 원인 미확정 사례로 보존한다.

처음 준비한 round1의 Combined와 실제 조작한 round2의 Combined는 다르다. 정상 HostStart 뒤 RuntimeConfig의 디버그 도구 값이 true→false가 되는 정상 전환을 첫 준비 스크립트가 잘못 거절했다. 실패 원본을 보존하고 HostStart를 재전송하지 않은 채 정상 생성2회/각20·조합·전달로 준비를 완료했다. 이후 사용자가 시작한 round2에서 정상 생성4회·직접 조합·왕복을 진행한 기록과 분리했다. 최종 캡처는 정상180초 후 양쪽 Defeat·HP100·명중0·시간0·연결2명·오류0이다.

**확인 범위:** 빌드19 설치·실행, 실제 오른쪽 Combined 왕복, 입력 취소/정상 손떼기 구분은 PASS다. 자동996개 검사와 앱 빌드·서명도 PASS다. 실제 두 성공 드래그는 기존 화면폭18% 거리 기준도 충족했으므로, 이번에 줄인 외곽 최소거리 구간의 실기기 직접 재현을 통과 처리하지 않는다. 해당 구간의 코드 재현·회귀44개는 PASS지만 실제 짧은 외곽 드래그는 NOT_RUN이다. 이전 빌드18의100전달·전체 승패는 해당 빌드 근거로 보존한다.

**전체 T12/G5는 NOT_RUN**이며 다음 확인 하나는 **T12 남은 실기기 확인**이다. 새로 줄인 외곽 거리의 직접 조작과 현재 빌드의 남은 전체 전투 항목을 구분해 확인해야 최종 판정할 수 있다. 추가 기능이나 T13을 시작하지 않았다. 기존 Config·meta·이전 씬·게임 소스는 이번 실기기 확인에서 더 바꾸지 않았다.

[원본 비공개 자료: 실제 Touch 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 사용자 관찰의 확인 범위 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 정상 준비 및 최초 실패 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 최종 상태 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 원본 보존 — 공개 요약](VALIDATION_SUMMARY.md).

---

아래는 앞선 저장 시점의 기록이다.

## 2026-09-13 최신 결과 · 가장자리 드래그 보정 빌드19

사용자가 보고한 iPhone Combined 오른쪽 미전달 1회는 **FAIL 경험·원인 미확정**으로 보존한다. 당시 두 잡기 입력에는 전달 후보가 없었고 시작·종료 좌표도 없어 특정 거리 조건 때문이라고 소급하지 않는다. 같은 빌드18 r4의 실제 Touch Raw5·Combined7 전달, 받은 Combined의 실제 명중·HP100→80·공격자 회복5와 후속 공통 상태8191개 일치는 확인했다. iPad Combined 오른쪽 Touch와 받은 Raw 수동 조합은 NOT_RUN이며, 앞선 자동 기기 증거와 구분한다.

별도로 화면 끝 가까이에서 잡으면 기존 최소 이동거리가 남은 화면 거리보다 큰 문제를 재현했다. 수정 전34개 중12개 실패를 보존했다. **빌드19**는 T12 옵션에서만 기존 경계 폭의1/2였던 최소 이동량을1/8로 조정한다. iPhone 약8.29px·iPad 약10.23px의 의도적 이동을 요구하며, 탭·1px 흔들림·손떼기 전 전달은 허용하지 않는다. 최소 이동량보다 남은 거리가 작은 극단 위치는 여전히 거절한다. 화면 안쪽 판정·수평 우세·하단 조합·상단 발사 우선순위와 단일 Config는 유지한다. BEGIN/END/취소 좌표와 실제 요구거리·미성립 사유를 추가로 기록한다.

자동 검사는 **996/996 PASS(Edit910+Play86)**, 기존972개 누락0·신규24개다. 처음 잘못된 필터의0개 실행은 NOT_RUN이며, 전체 검사에서 발생한 기존 검사 기대 빌드 번호18 및 Prepare 전 ProjectSettings18의 두 실패도 각각 보존했다. 번호를 맞추고 기존 Prepare를 실행한 최종 결과만 PASS다. Mac 두 프로세스의 짧은10전달·받은 Raw 조합·받은 Combined 실제 명중·공통91상태 지문 일치, iOS 출력·앱 빌드·개인 Team 서명도 PASS다. 두 기기 설치·실행 PASS. 기존 전달100회·상태바·승패 결과는 빌드18 근거로 남기며 빌드19 실기기 PASS로 승계하지 않는다.

현재 **빌드19 실제 Touch 및 전체 T12/G5는 NOT_RUN**이다. 다음 확인은 양쪽 앱의 방 생성·참가·Ready 후, 정상 구슬로 가장자리의 오른쪽 전달을 집중 재확인하는 T12 작업 하나다. 화면 끝에 가까이 잡은 경우와 손떼기 전 유지, 반대쪽 도착을 로그와 함께 확인한다. T13은 시작하지 않는다. 실제 두 iPhone Gate도 사용자 승인 iPhone17+iPad 대체 범위와 별개로 NOT_RUN이다.

실제 저장소의 관련9개 파일만 반영했고 기존 소스·문서·AGENTS를 이력으로 보존했다. 이전 씬·meta·Config·패키지와 무관한 검증 복제본 URP 변경은 반영하지 않았다. 결과 UI와 상태바 처리 코드는 바꾸지 않았다. Git commit/push는 하지 않았다.

[원본 비공개 자료: 빌드18 실제 Touch와 미성립 기록 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 판정 보정 결정 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 자동 검사 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: iOS 빌드 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 소스 보존 — 공개 요약](VALIDATION_SUMMARY.md).

---

아래는 앞선 실행 시점의 기록이다.

## 2026-09-13 최신 결과 · 정상 5회 명중과 Victory 완료

Defeat 후 Retry에 사용자가 “잘 들어와”라고 답했고 양쪽 round2 READY·구슬0·Stamina100·HP100·180초를 확인했다. 정상 Host Start와 생성·전달·조합·실제 명중을 이어 **양쪽 Victory·HP0·이번 라운드 명중5회·결과 고정까지 PASS**다. 전체 T12/G5는 현재 빌드의 실제 손 조작 확인이 남아 NOT_RUN이며 T13은 시작하지 않았다.

| 범위 | 상태 | 근거 |
|---|---|---|
| 실제 Defeat 후 Retry 초기화 | PASS | 사용자 확인과 양쪽 동일 round2 Ready/빈 구슬/100/HP100/180·초기 상태 ACK. |
| 받은 Raw → iPad 조합 → Combined 반환 → iPhone 실제 명중 | PASS | 정상 생성 Raw·반대 음양 조합 새ID·같은 Combined ID 반환/발사/최종 소비. Raw/Combined 전달 각1회만 실행. |
| 정상 실제 명중5회·공격자 회복 | PASS | HP100→80→60→40→20→0, 공격자 P1→P2→P1→P2→P1, 매회 회복5·비공격자 추가 명중보상0. 매 발사 전 정상 Generate 비용20. |
| 같은 Victory·결과 고정 | PASS · 자동 기록/기기 스크린샷 관찰 | 양쪽 Victory/HP0/이번 라운드5hit, TIME39.78634799999964·개인 자원·구슬·입력 상태 고정. 정상180초 라운드의 약140.214초에 승리. |
| 양쪽 상태 일치 | PASS | 공통 round2/revision3356개 지문 불일치0, Victory106개 포함. |
| 수집기의 실제 읽기 오류 회복 | PASS | 읽기69묶음/73시도 중 임시 JSON 소실3건·socket60 1건 모두 두 번째 수집 성공. 기존15초 예산 준수·FAIL4개 보존. 기기별 명령32건 각1회·재전송0. |
| 기존 전달100회 | PASS 유지 | Raw50+Combined50·송신자/방향별25개. 이번에는 반복하지 않았고 별도 연결 흐름 전달은100 집계에서 제외. |
| Victory 이후 사용자 Retry·현재 Touch | NOT_RUN | 이전 Ready를 Victory 후 Reset으로 계산하지 않는다. 새 r4의 실제 손 조작 확인 대기. |

run t12-device-r3/session8b672fd2567f4fdb8c33e3c3f8662655/**round2**/일반Seed1278725084의 결과다. 이전 round1의4hit·Defeat·실패와 섞지 않았다. HUD VALID HITS9는 누계4+5이며 이번 라운드는 roundHits5로 대조했다. Config hash6e3fd1258b8aa457dcdefb9cb843ac3331ad3f9a49225299082480b57dea9011, 시작0/100·Generate1개/20·시간회복20/3초·실제 공격자 명중회복5·180초를 유지한다. Fixture·Debug 공급·자원/피해 주입·시간 단축은 없다.

프로젝트는 `/path/to/2026-C6-M10-MUSA`, Unity6000.5.7f1/URP17.5.0/Xcode26.6 및 본인 Apple 개발 Team 개인 Team의 빌드18 그대로다. iPhone17 Host+iPad mini6 Client 대체 조합과 원문 두 iPhone Gate(NOT_RUN)를 구분한다. 게임 소스·씬·에셋·Config·앱을 변경하거나 재빌드하지 않았으며 기존972개 검사 근거와 실패 원본을 보존했다.

최초 checkpoint3 저장은 자동 승인 검토 시스템의 사용량 한도 오류로 실행 전 거절됐다. 사용자 “이전 작업 이어서해” 후 기존1463개 파일이 그대로임을 확인하고 새 승인 검토를 거쳐 증거 저장을 재개했다. 거절된 명령이 실행됐다고 기록하지 않는다.

Victory 이후 iPad 실제 background→Pause→Leave와 Phone 참가자 이탈이 기록됐다. 마지막 성공과 이후 연결 종료를 구분한다. 사용자의 재개 요청에 따라 같은 앱을 **t12-device-r4**로 다시 실행했고, 두 기기 build18 시작 로그를 확인했다. 새 실행은 이전 방/Ready/Touch 성공을 승계하지 않는다.

다음 요청 하나는 **T12 실제 Touch 마무리**다. 새 방/Ready/Start 후 정상 생성, Raw 좌우 왕복, 받은 Raw 상단 공격 거부·음양 조합, Combined 좌우 전달과 실제 수신자의 상단 드래그 발사를 한 세트로 안내했다. 확대/빛·손떼기 전달·반대쪽 진입과 화면을 확인한다. 손 조작 중에는 콘솔만 읽고 완료 후 상태를 수집한다. 이미 확인한 전달100회·상태바·180초 Defeat·자동5회 Victory는 반복하지 않는다.

[원본 비공개 자료: Victory 독립 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 실제 수집 회복 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Defeat 후 Retry 확인 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 관찰과 대기 항목 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 원본 보존 — 공개 요약](VALIDATION_SUMMARY.md).

---

아래는 앞선 실행 시점의 기록이다.

## 2026-09-13 후속 실행 · 전달100회 완료, 실제 명중4회

사용자의 “진행 완료”를 새 방/Bonjour 참가/양쪽 Ready 확인으로 적용하고 같은 빌드18에서 정상 Host Start를 실행했다. **Raw50 + Combined50, 총100회 전달은 PASS**다. 실제 명중4회와 매번 실제 공격자 회복5도 PASS다. **Victory·현재 빌드 실제 Touch·T12/G5 전체는 NOT_RUN**이며 T13을 시작하지 않았다.

| 확인 범위 | 상태 | 실제 근거 |
|---|---|---|
| 방 생성·Bonjour 참가·P1/P2·좌우 이웃·Ready | PASS | 새 r3 양쪽 같은 room/session, iPhone P1/좌우P2, iPad P2/좌우P1, 두 Ready와 canStart. 사용자 보고·실제 상태·두 화면 대조. |
| Raw50 + Combined50 | PASS | 일반 Seed의 별개 정상 세션으로 수행. 고유 승인100개, P1/P2 각각 Left25·Right25, Raw/Combined 각각50. ID·속성·소유권·개수·반대쪽 진입·실제 뷰 보존. 별도 연결 흐름 전달3회는100 분배에서 제외. |
| 받은 Raw 조합·같은 음양 거부·Raw 공격 거부 | PASS | 자동 정상 요청으로 실제 기기에서 승인/거부·재료 종료/새ID·원본/HP 보존 확인. 실제 Touch는 별도 미실행. |
| 받은 Combined의 실제 투척자 | PASS | iPad가 조합하고 iPhone이 받은 Combined를 실제 발사·피격. 같은 ID 최종 소비, HP100→80, 실제 공격자인 iPhone에만 +5. |
| 실제 명중4회 | PASS | HP100→80→60→40→20, 공격자 P1→P2→P1→P2, 각 명중 회복5·비공격자 추가 보상0. 매 발사 직전 정상 생성비20으로 회복 여유 확보. |
| 양쪽 상태 일치 | PASS | r3 공통 같은 round/revision 3177개 지문 불일치0. 서로 다른 revision의 beforeHash를 직접 같다고 요구하지 않는다. |
| 현재 결과 | PASS · 자동 기록/화면 범위 | 정상180초 후 양쪽 Defeat·HP20·명중4·TIME0·Stamina100, 오류0·연결 유지. 09:47→09:51 캡처의 게임 결과 필드 고정. aggregate revision은 계속 증가하므로 전체 hash 고정과 구분. |
| 다섯 번째 공격·Victory | NOT_RUN | 마지막 조합까지 완료했으나 파일 회수 실패와 후속 확인 중 시간이 끝나 발사하지 못함. 준비된 Combined를 실제 명중으로 세지 않는다. |
| 현재 빌드 손 조작·Result/Retry 사람 확인 | NOT_RUN | 좌측 가까운 경계의 수정 후 Touch, 양쪽 조합/전달/상단 발사·확대빛·피드백 확인 필요. 결과/Retry 질문은 현재 응답 대기. |

같은 빌드·Config를 사용했고 게임 소스·씬·패키지·Config·앱 서명은 변경하지 않았다. 기존972개 시험과 빌드 근거를 유지하며 새 게임 빌드/시험 반복은 하지 않았다. 원문 두 iPhone 조건은 사용자 승인 iPhone17+iPad 대체 범위와 별개로 NOT_RUN이다.

Raw50은 이전 session33e41ac2adcb4c29bca81e0723db7aca/round2/Seed1479802266, Combined50과명중4회는 session8b672fd2567f4fdb8c33e3c3f8662655/round1/Seed1278725084다. Config hash는6e3fd1258b8aa457dcdefb9cb843ac3331ad3f9a49225299082480b57dea9011로 같다. 모든 실행은 정상180초·정상 생성/비용/자동 회복이며 Fixture·Debug 공급·시간 단축·피해 주입은 없다.

실패도 그대로 보존한다. Combined 절차 중2건은 socket60 파일 회수 실패 뒤 같은 dispatch의 완료 파일만 다시 읽었다. 첫 Victory 절차는 생성 명령 전 사전 읽기 실패였고 실제 송신0이다. 다음 절차의 네 번째 추가 공격 준비에서는 생성/조합이 정상 완료됐으나, 진단 JSON의 원자적 교체 중 사라진 .json.tmp를 전체 폴더 수집이 읽으려다 Cocoa260/POSIX2로 실패했다. 이 실패를 게임 연결 실패로 바꾸지 않는다. 기존 전체 절차 FAIL은 유지하고 확인된 명중4회만 별도 PASS로 기록한다.

Mac 진단 수집기 새 버전은 확인된 socket60 및 현재 run의 .json.tmp 소실에 한해 읽기만 최대2회 재수집한다. 실패 시도마다 새 증거를 남기고 기존 시간 제한을 공유한다. COPY TO/게임 명령 재전송은 없다. 문법 및 보존 오류 분류8/8 확인, 새 수집기의 양쪽 현재 capture는 PASS다. 수정 후 같은 오류가 실제로 재발했을 때의 재수집 분기 실증은 NOT_RUN이다. 이는 기기 앱 변경이 아니며 원래 수집기·실패 원본도 보존한다.

현재 사람 작업은 양쪽 DEFEAT·HP20·TIME0과 결과 고정을 확인한 뒤 iPhone RETRY로 양쪽 READY·구슬0·Stamina100·HP100·180초가 되는지 확인하는 것이다. 아직 Host Start는 보류한다. **다음 요청 하나는 T12 실제 Touch와 Victory 마무리**다. 전달100회·상태바·180초 Defeat를 재반복하지 않고, 남은 실제 조작과 승리/초기화가 확인돼야 G5를 판단한다.

[원본 비공개 자료: 전달100회 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 명중4회와 최종 상태 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 최신 두 기기 상태 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 사용자 관찰 구분 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 원본 보존 경로 — 공개 요약](VALIDATION_SUMMARY.md).

---

아래는 앞선 중간 저장 시점의 기록이다.

# T12 남은 실기기 통합 검증 · 중간 저장

2026-09-13 사용자의 “다음 작업 진행해”에 따라 T12를 재개했다. 이어 “다시 해봐” 요청으로 두 기기에 같은 빌드18 앱을 다시 실행했다. **T12/G5 전체는 NOT_RUN**이며 T13은 시작하지 않았다.

프로젝트는 `/path/to/2026-C6-M10-MUSA`, Unity6000.5.7f1·URP17.5.0·Xcode26.6이다. 본인 Apple 개발 Team 개인 Team으로 서명된 빌드18을 iPhone17 Host와 iPad mini6 Client에서 사용한다. 원문 두 iPhone 조건과 사용자 승인 대체 조합을 구분한다. 실제 앱·소스·Config 지문과 기존 자동972개 증거를 다시 대조했으며, 이번 재개에서는 게임 소스 변경·재빌드·자동 시험 재실행을 하지 않았다.

| 확인 범위 | 상태 | 근거와 제한 |
|---|---|---|
| 정상 Raw 50회 전달 | PASS | 실제 두 기기에서 정상 생성한 Raw1개/비용20을 전달. 고유 요청50개·양쪽 승인50개·같은 ID와 속성 보존. 각 송신자 Left13/Right12. 자동 요청이며 실제 손 조작은 별도다. |
| 추가 Raw 전달1회 | PASS | 총 전달횟수51. 완료 파일 회수에서 오류가 났으나 같은 명령의 결과만 다시 수집했다. 실제 실행1회·요청 재전송0. 균형100회 집계에서 제외한다. |
| 전달받은 Raw 조합 | PASS | iPhone 생성 Raw를 iPad가 반대 음양과 조합. 재료2개 종료·새 Combined ID1개, 나머지 구슬 보존. |
| Raw 공격 거부 | PASS | RAW_CANNOT_LAUNCH, 원본·HP 보존, 발사체·피해 없음. 자동 요청 범위다. |
| 정상 Retry 초기화 | PASS | 기존 Defeat에서 round2 Ready·구슬0·Stamina100·HP100·시간180을 두 기기 상태/화면으로 확인. 이후 Host Start 정상. 실제 손의 Retry 확인은 미실행. |
| Combined 50회 / 전달 총100회 | NOT_RUN | Raw50만 집계했다. Combined는 양쪽 송신자 Left12/Right13 계획으로 이어가면 전체 각25회가 된다. |
| 받은 Combined 실제 피격·공격자 +5 / Victory | NOT_RUN | 현재 실기기 실행에서 아직 확인하지 않았다. Mac이나 이전 빌드 결과로 대체하지 않는다. |
| 실제 손의 좌우 전달·조합·자동 발사 / 전체 AT | NOT_RUN | 특히 가까운 왼쪽 경계의 수정 후 Touch와 양쪽 화면 관찰이 남았다. |
| 새 r3 실행 | PASS | 두 기기 빌드18 앱과 방 생성 화면 확인. CREATE/FIND/JOIN/Ready는 아직 수행되지 않았다. |

앞선 전체 자동 절차는 파일 회수 중 CoreDevice socket 오류60으로 FAIL을 기록했다. 게임 내 Raw 추가전달은 이미 성공했으므로 동일 dispatch 결과만 읽어 별도 PASS 근거로 남겼다. 전체 실패 원본을 덮어쓰지 않았다. 이후 시도한 Retry도 참가자 연결이 종료된 상태여서 HOST_RESULT_REQUIRED로 FAIL이었다. iPad 로그에 실제 background(appState2/pause1)와 Leave 호출을 확인했다. 무엇이 실제 백그라운드 전환을 일으켰는지는 확정하지 않는다. 이전 상태바 foreground inactive 유지 PASS와 구분하며 결과 UI 보호나 T13 기능을 추가하지 않았다. r3 로비 캡처 후에도 iPad 실제 background 로그가 관찰됐다.

현재 필요한 사람 작업은 두 앱을 전경에 두고 **iPhone CREATE ROOM → iPad FIND ROOMS·JOIN → 양쪽 I'M READY**까지 진행하는 것이다. HOST START는 자동 검증 준비 후 실행한다. 다음 작업 하나는 **T12 남은 실기기 검증 계속**이며, Combined50·실제 피격과 양쪽 Touch 확인이 있어야 G5를 판단할 수 있기 때문이다.

[원본 비공개 자료: 기기 중간 대조 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 기존 빌드 근거 재확인 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 새 로비 상태 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 중간 상태 및 원본 archive — 공개 요약](VALIDATION_SUMMARY.md).

Raw50와 이어진 조합은 run t12-device-r2/session33e41ac2adcb4c29bca81e0723db7aca/round2/일반Seed1479802266/180초다. wire Config hash는6e3fd1258b8aa457dcdefb9cb843ac3331ad3f9a49225299082480b57dea9011이다. 공통 round2 상태1057개의 지문 불일치는0이다. Fixture·Debug 공급·시간 단축·자원 주입은 사용하지 않았다. 과거 원본과 실패 기록을 보존했으며 Git commit/push는 하지 않았다.
