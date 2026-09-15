> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T09 / G4 실기기 Core Loop 완료

2026-09-13 **T09와 G4 PASS**. 범위는 통합 지시서5.1/5.2와T09가 정한 한 iPhone의 명시적 개발 모드다. 두 기기 G5/T12 또는 전체 MVP 완료를 뜻하지 않는다. 새 게임 구현이나 다음 Task는 시작하지 않았다.

## 프로젝트와 실행 환경

실제 루트 `/path/to/2026-C6-M10-MUSA`, private 원격 `prototype-author/C6_Prototype`이다. Unity6000.5.7f1/기존 패키지·Config·BattleLoop 씬·빌드13을 그대로 사용했다. 설치 앱 com.wolfuraark.c6prototype, 버전0.1.0/13, 본인 Apple 개발 Team 개인 Team YOUR_TEAM_ID, iPhone17/iPhone18,3/iOS26.6.2, 화면1206×2622와 Safe(0,102,1206,2334)다.

기존 소스379개 해시가 빌드13 근거와 모두 일치했다. 자동 시험 Edit401/Play74, 실패/skip0과 Mac 두 화면·Host/Client·실제180초 결과는 기존 실행 XML/JSON을 재확인했다. 이번 추가 작업은 기존 설치 앱에서 사람 조작과 로그를 대조하는 것이므로 소스 변경·새 빌드·추가 자동 시험은 없었다.

## 실제 기기 결과

같은 session의 round2에서 정상 생성5→직접조합1→실제hit1/HP80/+5 이후 기본180초 설정의 Clock이0이 되어 Defeat가 확정됐다. 사용자가 결과 표시·숫자 고정과 Retry 후 Ready/구슬0/Stamina100/HP100/180초를 확인했다. Reset은 round3, 다음 Start는 round4다. 이 iPhone 로그는 단축시간 사용이 없음을 명시한다. 별도 Mac의 실제180.0016565초 경과 계측과는 구분한다.

| round4 일반 승리 경로 | 결과와 실제 근거 |
|---|---|
| 생성·추가 생성 | PASS · 정상 Raw13개, 13고유ID, 각비용20. 일반 Host Seed, 무료Fixture 없음 |
| 같은 음양 거부 | PASS · 사용자 확인·INVALID_COMBINATION2건. 두 거부의 inventoryRevision26 유지, 네 재료가 나중에 정상 조합으로 사용됨 |
| 조합 | PASS · 실제 TOUCH 성공5회, 재료두ID→새CombinedID1 |
| 발사·피격 | PASS · 실제 TOUCH 경계발사5/실제Rigidbody 피격5, HP100→80→60→40→20→0 |
| 회복 | PASS · 매 실제피격의 공격자에게5. 마지막 회복도 Victory 확정 전에 기록됨 |
| ID 연결 | PASS · 조합결과·터치발사·실제피격·회복의 다섯OrbID 집합 일치 |
| Victory·결과 고정 | PASS · round4 revision5721, HP0, 남은시간/TeamHP152.47698479099995. 사용자 결과 숫자 고정 확인 |
| 승리 후 Retry | PASS · round5 Ready/구슬0·투사체0·HP100·Stamina100·시간180 |
| 재시작 첫 생성 | PASS · round6 첫 Generate Raw1/비용20, Stamina100→80 |

사용자의 추가 플레이 round6에서도 정상 생성10·조합5·피격5·Victory가 기록됐다. 추가 반복을 완료 조건으로 요구한 것이 아니다. 마지막 사람 응답은 round7 Retry 초기화와 round8 Host Start 후 **첫 Generate 단1회·Raw1/100→80**과 연결했다. 모든 반복의 Total Hits를 round4의 명중 횟수로 섞지 않았다.

사람 확인 세 응답은 “결과 화면과 RETRY 초기화 모두 정상”, “같은 음양 거부와 VICTORY·결과 고정 모두 정상”, “초기화와 첫 구슬 1개 생성 모두 정상”이다. 화면 관찰은 이 응답에, 상태 변화·수치·ID는 실제 기기 로그에 근거한다. 새 기기 스크린샷을 찍었다고 주장하지 않는다.

## 진행 조건과 보존

이번 T09를 막는 항목이나 추가 필수 사람 작업은 없다. G4의 한기기 개발 모드 조건을 완료했으므로 **다음 요청 하나는 T10-A: 2인 Lobby·자동 방 탐색·Ready**다. G4 결과를 바탕으로 일반2인 시작 흐름을 연결할 차례이며 T10-B와 별도 요청한다. G5/T12의 두 기기 최종 Core Loop·AT 전체·자동 탐색·전달 검증은 NOT_RUN이다.

스태미나100/시작구슬0/생성20/3초당20/명중+5, 하단전체조합/상단경계발사/잡기확대·빛 규칙은 유지한다. 기존 문서 본문과 기기 실패·성공 근거를 보존하고 현재 상태 블록만 갱신했다. 직전 문서는 history/2026-09-13-t09-input13에 보존했다. 원래 Editor·다른 앱·Git 사용자 변경은 유지했으며 커밋·push·배포는 하지 않았다.

## 근거

- [원본 비공개 자료: G4 완료·라운드별 수치·사람 응답 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 선별 실제 기기 실행 행 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 패배·Retry 당시 관찰 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 소스379개 무변경 확인 — 공개 요약](VALIDATION_SUMMARY.md)
- [원본 비공개 자료: 빌드13 기존 자동·빌드 실행 근거 — 공개 요약](VALIDATION_SUMMARY.md)
