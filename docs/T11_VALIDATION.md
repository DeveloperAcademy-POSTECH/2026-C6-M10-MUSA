> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T11 검증 결과 — 구현·코드 PASS, 실기기 NOT_RUN

실행일: 2026-09-13. 실제 루트는 `/path/to/2026-C6-M10-MUSA`, 검증은 별도 복사본 `/private/tmp/C6_Prototype_T11_Verification`에서 수행했다. T10-B 검증 대상476개 파일이 시작 시 현재 저장소와 일치했고, 작업 전1044개 파일을 해시로 기록했다.

## 환경과 최종 빌드

Unity6000.5.7f1 / URP17.5.0 / Input System1.20.0 / Test Framework1.7.0 / NGO2.13.1 / Transport6.5.0, macOS26.6.2 arm64와 Xcode26.6을 사용했다. 패키지 버전은 유지했다.

최종 씬은 `Assets/_Project/HapioMVP/Scenes/OrbTransferBattle.unity`, 앱은0.1.0/빌드16, Bundle ID는 `com.wolfuraark.c6prototype`다. iOS15 이상·iPhone/iPad·세로, 승인된 본인 Apple 개발 Team 개인 Team `YOUR_TEAM_ID`로 서명했다.

- Mac: `Builds/T11-r3/macOS/C6Transfer.app`
- Xcode: `Builds/T11-r3/iOS/Unity-iPhone.xcodeproj`
- 서명 앱: `Builds/T11-r3/DerivedData/Build/Products/Debug-iphoneos/C6Prototype.app`

## 실행 결과

| 구분 | 상태 | 실행 증거 |
| --- | --- | --- |
| 새 씬 준비·컴파일 | PASS | Prepare 로그, 새 씬 저장 검증4개 |
| EditMode | PASS | 828실행/828통과/실패0/누락0 |
| PlayMode | PASS | 86실행/86통과/실패0/누락0 |
| 기존 시험 유지 | PASS | 기존837개 모두 포함, 새 시험77개, 합계914개 |
| Mac 앱 빌드 | PASS | 최종 r3 BuildReport 오류0/경고0 |
| 실제 Mac Host/Client | PASS | 두 독립 앱, 일반 Seed1989234191, Raw8/Combined9 전달17회 |
| 동일 통합 상태 비교 | PASS | 같은 round/revision211개 지문 일치, 불일치0 |
| 수신 Raw 조합·Combined 실제 명중 | PASS | 반환 Raw ID를 실제 재료로 사용; 마지막 수신 Client가 같은 Combined ID 발사, Host 실제 피격1회·양쪽 HP80 |
| Unity iOS 출력 | PASS | 최종 r3 Xcode 생성, BuildReport 오류0/경고0 |
| Xcode 앱 빌드·서명 | PASS | BUILD SUCCEEDED, 서명검증·Team·Bundle·빌드16·네트워크 선언 확인 |
| 빌드16 설치·실제 화면·Touch | NOT_RUN | 기기 앱을 설치·실행하지 않음 |
| 빌드16 Bonjour/실제 Wi-Fi | NOT_RUN | 이번 두 프로세스 시험은127.0.0.1 직접IP |
| T12·G5 전체 실기기 Gate | NOT_RUN | 별도 요청과 기기 검증 필요 |

핵심 근거는 [원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 시험 수와 기존 시험 대조 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: 최종 Mac 비교 — 공개 요약](VALIDATION_SUMMARY.md), [원본 비공개 자료: iOS 빌드16 — 공개 요약](VALIDATION_SUMMARY.md)이다. 원시 로그는 `Logs/T11/`에 로컬 보관한다.

Xcode 경고32개는 Unity 생성 코드의 deprecated API, 빈 object symbol, App Store 아이콘, 빌드 단계 출력 설정 경고다. 앱 빌드·서명은 성공했으며 경고 내역은 [원본 비공개 자료: 경고 집계 — 공개 요약](VALIDATION_SUMMARY.md)에 보존했다. App Store 배포는 이번 범위가 아니다.

## 전달·오류·화면 근거

Host390×844, Client560×746의 실제 창 크기를 결과 파일로 확인하고 캡처도 검토했다. 요청한 크기만 보고 기록하지 않았다. 전달 전후 살아 있는 ID 집합은17회 모두 동일하며 종류·음양은 유지되고 owner와 전달 횟수만 올바르게 변했다. Left→상대Right, Right→상대Left와 정규화 높이가 일치했다. 수신 화면의 정규화 높이 최대 오차는 약5.96e-8이었다. 일반 상태 갱신이 View를 재생성하거나 위치를 바꾸지 않았고 새 제스처 전 자동 반송은 없었다.

| 실제 송신자/방향 | Raw | Combined |
| --- | ---: | ---: |
| Host Left | 2 | 2 |
| Host Right | 2 | 3 |
| Client Left | 2 | 2 |
| Client Right | 2 | 2 |

Client 첫 전달에서 응답2개를 폐기했고 약3초 후 Query로 원 영수증을 확인했다. 정상 프로토콜로 동일 request를 한 번 더 보낸 Host 로그는 최초 승인과 duplicate 승인 각각1회이며 둘 다 transferCount2다. 소유권 변경은 추가되지 않았다. 주입 횟수만으로 중복 처리 성공을 판정하지 않았다.

새 자동77개는 권한23·영수증/wire12·snapshot16·씬4·제스처22다. 잘못된 owner, 상대 만원·이탈, 높이 범위·비유한값, 상태·sequence·중복·변조 조회, 발사/조합 경합, 반환 후 지연 승인, 전달 직후 발사, Reset·terminal·누락된 상태 갱신을 검사한다. 실제 패킷 손실 전체나 모든 접속 해제 타이밍을 완전히 재현한 결과는 아니다. 자동 포인터는 Touch 횟수에 합산하지 않았다.

최종 통합 상태는 양쪽 HP80·roundHits1·revision192로 저장됐다. 양측 완료 표식과 최종 증거 뒤 Client가 종료되어 Host 결과의 `gameError=PARTICIPANT_DISCONNECTED`가 남았다. 이는 성공 전 상태로 바꾸어 쓰지 않았으며 frozen `finalGame` 및 전달 증거와 분리한다.

## 실패한 시도 보존

- `mac-r1`: 개별 플레이는 끝났지만 Host 통합 상태가HP100인 시점에 최종 기록을 저장해 Client HP80과 비교가 실패했다. 초기에 빈 AssertionError 문자열을 성공으로 잘못 해석한 요약은 `invalid-summary-before-recorder-fix.json`으로 보존하고 공식 `summary.json`을 FAIL로 정정했다. 실패를 PASS로 집계하지 않는다. 최종 도구는 예외 유무를 확인하며 명중 후 통합 상태까지 기다린다.
- `mac-r2`: 진단용 intent JSON이 작성되는 중에 다른 프로세스가 읽어 Client null 예외, Host 대기 실패가 발생했다. 임시 파일을 완성한 뒤 원자적으로 공개하도록 수정했다. 양쪽 FAIL 결과를 보존한다.
- `mac-r3`: 위 기록 문제를 수정한 새 앱으로 모든 단계와 비교를 다시 실행해 PASS를 확인했다. 앞선 실패를 반복 횟수에서 제외하고 별도로 남겼다.

마지막 수정은 개발 Mac 진단 코드와 비교 도구에 한정된다. EditMode/PlayMode 컴파일 범위와 실제 게임 코드에는 추가 변경이 없으며 최종 Mac/iOS 빌드는 같은 최종 소스로 생성했다.

## 보존과 다음 작업

기존Config·meta·씬·과거증거와 사용자 변경을 보존했다. 필요한 기존 소스10개와 빌드 설정2개를 수정하고 새 소스·시험·씬16파일을 추가했다. 기존 문서 본문은 보존한 채 최신 상태를 앞에 붙였고 AGENTS는 진행 상태만 갱신했다. 원본 자동 렌더 설정4개와 검증용 SceneTemplateSettings는 이관하지 않았다. 반영 목록·소스 해시·최종 감사 결과는 `docs/evidence/T11/`에서 확인한다. commit/push는 수행하지 않았다.

T11을 막는 미해결 항목은 없다. 다음 요청은 **T12: 실제 기기의 전체 전투와 전달 검증** 하나다. 현재 알려진 가용 기기는 iPhone17과 iPad이며, 정식 두 iPhone 조건과 대체 검증 범위를 구분해야 한다. 사람은 대상 기기를 연결·잠금 해제하고 실제 화면·Touch·경계 높이·Ready부터Result까지를 확인한다. 과거 빌드13 등의 실기기 PASS를 빌드16에 승계하지 않는다.

T12용100회는 송신자/방향별25회, Raw50/Combined50으로 [실행 절차](T11_RUNBOOK.md)에 준비했다. 이번에 수행한 횟수는 Mac17회다. 사용자에게100번 수동 조작을 자동 요구하지 않으며 기기용 자동100회 기능은 아직 구현·실행하지 않았다. T12·다음 Task는 자동 시작하지 않는다.
