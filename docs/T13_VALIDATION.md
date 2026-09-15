> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T13 중단·연결 실패 처리 — 빌드20 검증

2026-09-13. **핵심 구현·자동 검증 및 제한된 실기기 시험은 완료했다. T13 전체 판정과 원문 G5는 NOT_RUN을 유지한다.** 홈 이동·잠금·Wi-Fi 해제 등 실제 OS 동작은 아래와 같이 별도 미실행으로 남는다.

## 대상과 실행 범위

- 프로젝트: `/path/to/2026-C6-M10-MUSA`. 실행 사본: `/private/tmp/C6_Prototype_T13_Verification`.
- Unity 6000.5.7f1·URP 17.5.0, Xcode 26.6(17F113)·iOS SDK 26.5. 기존 엔진·패키지를 유지했다.
- 새 씬: `Assets/_Project/HapioMVP/Scenes/InterruptionBattle.unity`, 빌드20. iOS GUID: `b20b85b126b94739bf6b658caaa17872`.
- 본인 Apple 개발 Team 개인 Team `YOUR_TEAM_ID`, Bundle ID `com.wolfuraark.c6prototype`. iPhone 17 Host + iPad mini 6 Client는 기존 사용자 승인 범위다. 원문 iPhone 2대 시험으로 표기하지 않는다.
- 시작 구슬0·스태미나100, Raw1 생성 비용20, 시간 회복20/3초, 유효 명중 공격자+5, 180초·몬스터HP100·피해20인 기존 Config를 사용했다.
- Mac은 두 프로세스·직접 IP·자동 조작, iOS는 실제 두 기기·명시적 개발 진단 실행(`C6_T13_DEVICE_RUN`)이다. 이번 iOS 결과는 손가락 조작이나 Bonjour 탐색 재검증이 아니다.

## 실행 결과

| 항목 | 상태 | 실제 확인과 한계 |
|---|---|---|
| Compile·새 씬 Prepare | PASS | 초기 API 인자 오류를 수정한 재실행에서 빌드20 씬 생성과 성공 종료 확인. 첫 실패 로그도 보존. |
| EditMode | PASS | 948개 실행·948 통과·실패0·누락0. |
| PlayMode | PASS | 89개 실행·89 통과·실패0·누락0. 모의 pause/focus 시험을 실제 OS 시험으로 해석하지 않는다. |
| 기존 테스트 보존 | PASS | 총 1,037개 실행. 기존 996개 이름 모두 유지·신규41개·누락0. 과거 PASS 승계가 아닌 현재 XML 집계다. |
| Mac Host–Client | PASS | 동일 세션·revision 스냅샷204쌍, 불일치0. 응답 보류 후 감시 오류 관측7.61초. 잘못된 제어38개가 감시 시간을 연장하지 않았으며, 이전 세션 Generate 요청1개는 새 상태에 영향 없음. 모의 pause 정리·새 연결도 확인. |
| iOS Xcode 생성·빌드·서명 | PASS | 최신 출력 `build20-device`에서 앱 생성·서명. 실제 보관 위치는 `Builds/T13/build20`이다. |
| iPhone·iPad 설치·실행 | PASS | 양쪽 빌드20 GUID 확인. 최초 진단 실행 인자 전달 문제를 환경 변수 명시 방식으로 수정한 뒤 실제 실행 확인. |
| 실제 기기 응답 중단 감시 | PASS — 해당 시나리오 | 앱의 응답만 의도적으로 보류했다. iPhone `PEER_GAME_RESPONSE_TIMEOUT`, iPad `PARTICIPANT_DISCONNECTED`; 오류 revision207·논리 해시 동일. Client 보류부터 오류 관측까지8.269초. Wi-Fi 해제나 프로세스 종료 시험은 아니다. |
| 오류 표시·입력 정리 | PASS — 해당 시나리오 | 실제 기기 로그와 양쪽 스크린샷에서 Network Error·멈춘 결과 표시 확인. 시간 만료 패배로 바꾸지 않았다. |
| 새 방·새 인증값·첫 생성 | PASS | 이어진 별도 실행에서 새 방·새 nonce 확인, 각자 첫 Generate로 구슬1개씩 생성. 오래된 프로세스의 명령은 `COMMAND_PROCESS_BOOT_MISMATCH`로 거부. 자동 재접속은 사용하지 않았다. |
| 정상 END 정리 | PASS | 마지막에는 앱의 END로 양쪽 Idle Lobby·빈 세션 확인. 프로세스를 종료하지 않았다. |
| 초기 iOS 실행기 | FAIL 보존 | 첫 실행은 기존 방이 남아 있는데 빈 시작을 가정했다. r2는 정리된 null snapshot을 `JsonUtility`가 기본값 객체로 기록해 실행기의 null 단정이 실패했다. underlying 오류·정리 관측과 실행기 전체 FAIL을 분리한다. |
| 보정한 이어서 실행 | PASS — 한정 범위 | 빈 연결 문맥·실제 attachment로 정리 상태를 확인하고 새 방·첫 생성을 시험했다. 앞선 실패 기록을 덮어쓰거나 전체 시나리오 재통과로 바꾸지 않았다. |
| 실제 Host/Client 강제 종료 | BLOCKED | 자동 승인 검토가 AGENTS의 프로세스 강제 종료 금지에 따라 SIGKILL 실행을 거부했다. 해당 명령과 종료 시험은 실행되지 않았다. |
| 실제 홈 이동·잠금·Wi-Fi 해제·권한 팝업 | NOT_RUN | 현재 빌드20에서 해당 OS 조작을 실행하지 않았다. |
| 현재 빌드 상태바·제어 센터 | NOT_RUN | 네이티브 처리 보존·소스 검증과 실기기 재실행을 구분한다. 빌드18의 사용자 확인은 과거 증거로 유지한다. |
| T13 전체 / 원문 G5 | NOT_RUN / NOT_RUN | 구현 착수 지시와 부분 시험 완료를 전체 실기기 Gate PASS로 해석하지 않는다. |

8초는 유효 응답의 마지막 수신 시점을 기준으로 한 감시 설정이다. 위 7.61초와8.269초는 각각 시험 명령·외부 관측 시점을 기준으로 하므로 정밀한 OS 타임아웃 측정값으로 비교하지 않는다.

초기 Unity CLI 실행 실패, `CancelInteractions(string)` 인자 누락에 따른 컴파일 실패, 서명 단계 sandbox 오류, 진단 활성화 인자 누락은 실패 기록으로 보존한다. 수정·허용된 실행 환경에서 성공한 재시도의 결과만 각각 PASS에 반영했다.

## 근거와 보존

집계는 [원본 비공개 자료: 테스트 감사 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: Mac 결과 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 실기기 결과 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 소스 이관 영수증 — 공개 요약](VALIDATION_SUMMARY.md)을 따른다. 실기기 원본은 `device-evidence/t13-ios-02/` 아래 `scenario-r2`, `scenario-continuation`, `final-cleanup`과 각 기기 로그·스크린샷이다. `scenario-r2/summary.json`의 FAIL은 그대로 보존한다.

의도한 파일26개를 이관했고 기준 파일505개 보존을 확인했다. 검증 사본에서 Unity가 변경한 렌더 설정 에셋4개와 새 `SceneTemplateSettings.json`은 제외했다. 이 차이를 단순 포맷 변경이라고 단정하지 않으며, 시험한 앱은 해당 사본에서 생성됐으므로 이관 후 전체 소스·렌더 결과·바이너리가 완전히 동일하다고 주장하지 않는다. [원본 비공개 자료: 제외 사유·해시 감사 — 공개 요약](VALIDATION_SUMMARY.md)를 함께 보존한다.

원본 로그는 `Logs/T13/build20/evidence.tar.gz`에 보존했다. SHA-256 `aec4104d4b7c3d7467b6249e1ab16b47c021f3d8affdc0794c7d4bda6d99afc2`, 964개 항목·내용 파일963개 해시 대조 PASS·2,158,268 bytes다. [원본 비공개 자료: 아카이브 영수증 — 공개 요약](VALIDATION_SUMMARY.md)을 따른다. 원시 로그는 Git 제외 경로이며 Git commit·push는 수행하지 않았다.


실기기 오류 화면: [원본 비공개 자료: iPhone — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: iPad — 공개 요약](VALIDATION_SUMMARY.md). 두 화면을 직접 열어 오류 안내와 값 고정을 확인했다.
