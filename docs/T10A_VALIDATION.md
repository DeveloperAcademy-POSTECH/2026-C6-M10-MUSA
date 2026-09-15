> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-A · Lobby·자동 탐색·Ready 검증

2026-09-13 KST. **T10-A 구현·코드 검증과 iOS 빌드14 생성/서명 PASS.** 실제 두 iPhone의 발견·참가와 G5는 T12에서 확인하며 NOT_RUN이다. T10-B는 시작하지 않았다.

## 프로젝트와 환경

실제 루트는 `/path/to/2026-C6-M10-MUSA`, 개인 private 저장소는 `prototype-author/C6_Prototype`이다. Unity6000.5.7f1/URP17.5.0/NGO2.13.1/Transport6.5.0, macOS26.6.2 arm64, Xcode26.6(17F113)/iPhoneOS26.5를 유지했다. 패키지는 바꾸지 않았다. 원래 Editor를 보존하기 위해 별도 검증 사본에서 실행했다.

새 씬은 RoomLobby.unity이며 단일 ScreenLayoutConfig를 참조한다. 시작0구슬·Stamina100/100·생성20·3초당20·유효명중5·180초를 Host 설정에 담았다. 이번 Playing은 두 사람의 시작 계약 확정이며 실제 게임 흐름 연결은 T10-B다.

## 실제 실행 결과

| 항목 | 상태 | 증거·범위 |
|---|---|---|
| 컴파일·저장 씬/Config/UI 연결 | PASS | 최종 소스·기존 씬 보존 |
| EditMode | PASS | 667/667, 실패·skip0 |
| PlayMode | PASS | 84/84, 실패·skip0; 실제 Host·Ready/취소·모의pause 정리 |
| 기존 자동시험 보존 | PASS | 빌드13의 Edit401/Play74 전체 이름이 최종 XML에 존재; 누락0 |
| 실제 Mac Bonjour 수명 | PASS | IPv4/SRV 자동 발견, TXT build/full/Playing 변경,12초만료·재발견·취소·갱신·goodbye·native정리 |
| 실제 Mac 별도 Host/Client | PASS | 발견 방 Join, P1/P2·좌우상대, Config ACK, 비Host Start거절, Ready2→Host Start. 양쪽 room/session/revision/round1/Seed/Config 일치 |
| 만원·다른build·오래된room | PASS | 별도 Mac 참가 요청3건을 실제 Host가 거절 |
| Playing 중 새참가 거절 | PASS | 모델 검증과 실제 Bonjour Playing 행 참가불가; 이 항목의 두기기 실기기는 미실행 |
| Direct IP Fallback | PASS | 별도 Mac 두 실행본에서 동일 시작 계약까지; Bonjour 경로와 별도 기록 |
| Mac 화면 | PASS |390×844 offline/발견/Host/Client/오류/시작확정,560×746 offline/확장fallback 직접 시각 확인. 작은 화면의 아래쪽 입력은 스크롤로 접근 |
| Mac 앱·iOS Xcode 생성 | PASS | 최종 두 BuildReport 오류0·경고0; 출력 Builds/T10-A-r2 |
| iOS 빌드14 앱 컴파일·개인Team 서명 | PASS | Xcode BUILD SUCCEEDED, 실제 앱 codesign strict 확인, Team YOUR_TEAM_ID |
| iOS 실제 산출물 네트워크 선언 | PASS | NSBonjourServices=_c6hapio._udp, Local Network문구, Portrait·기기Family1,2·빌드14, 기본libSystem DNSService 링크 |
| 빌드14 설치·iPhone/iPad 실행/Touch | NOT_RUN | T10-A 요구 범위는 구현·코드 검증. 기존 iPhone의 빌드13을 교체하지 않았다 |
| AT-01/02/03 두 iPhone·G5/T12 | NOT_RUN | Mac 결과를 실기기 완료로 승계하지 않는다. T10A_RUNBOOK.md의 실제 두 iPhone 절차 적용 |

Mac 최종9개 실행 결과는 실제 시스템 Bonjour와 NGO 통신을 사용했다. 한 Mac의 여러 실행본이므로 두 물리 기기 시험은 아니다. Discovery 메타데이터만으로 Ready를 허용하지 않으며 별도 Host 연결·Config 수신·ACK를 통과해야 한다. Mac의 프로토콜 build는14이며 plist의 기본 CFBundleVersion0과 구분한다.

## 발견·수정한 문제

첫 EditMode는647/649였다. 설치 엔진의 JsonUtility가 null p2/start를 빈 DTO로 직렬화하는 증거를 확인하고, 0/1개 배열로 명시해 잘못된 빈 DTO를 거절하도록 수정했다. 최종667개가 통과했다.

첫 Mac 앱 탐색은 주소 해결 시간초과였다. C6Lobby의 로컬 네트워크 스위치가 켜진 것을 UI로 확인했고 설정을 바꾸지 않았다. 같은 앱을 다시 실행해 탐색 수명을 통과했다. 최초 실패의 정확한 원인은 확정하지 않았으며 로그를 보존했다.

다음 실제 연결에서는 Host Config 메시지가 NGO의 기본 비분할 전송 크기를 넘어 OverflowException과 초기수신시간초과가 발생했다. 설치 NGO가 제공하며 기존 공격 동기화에서 쓰는 ReliableFragmentedSequenced로 수정했다. 최종 앱에서 전체9개 실행을 통과했다. IP성공으로 탐색 실패를 덮지 않았으며 r1/r2/r3 결과를 모두 남겼다.

## 보존과 남은 작업

기존 meta198개·씬/meta20개·단일Config 관련6개·T09 구현/시험32개·T09 증거102개를 보존했다. 빌드13 소스379개 중376개는 동일하고 변경3개는 선택적 연결 확장과 두 빌드설정이다. 기존 URP 템플릿4개는 원본과 과거 검증 사본 차이를 그대로 기록했다. 누락·고아meta·중복GUID는0이다. 상세 해시와 범위는 preservation-audit.json/source-manifest.json을 따른다. 현재 문서의 앞부분만 새 결과로 갱신하며 이전 본문은 보존한다. commit/push·배포는 수행하지 않았다.

현재 T10-A 구현 완료를 막는 항목이나 바로 필요한 사람 작업은 없다. 두 iPhone의 최초 권한·자동발견·실제 참가·Touch/Safe Area는 T12에서 사람 확인이 필요하다. 현재 가능한 iPhone+iPad를 두 iPhone 최종 QA로 기록하지 않는다.

다음 요청 하나는 **T10-B · 2인 게임 상태 동기화**다. 이제 확정된 Lobby의 참가자·Seed·Config·round1 계약에 실제 생성·조합·공격·자원·Clock·승패 상태를 연결할 수 있다. 자동 시작하지 않았다.

[원본 비공개 자료: 실행 요약 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 자동 시험 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: Mac 최종 결과 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: iOS 앱/서명 — 공개 요약](VALIDATION_SUMMARY.md) · [원본 비공개 자료: 보존 감사 — 공개 요약](VALIDATION_SUMMARY.md) · [구현 결정](T10A_DECISION.md) · [반복·기기 절차](T10A_RUNBOOK.md)
