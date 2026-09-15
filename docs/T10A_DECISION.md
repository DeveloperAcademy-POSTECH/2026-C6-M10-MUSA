> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-A · Lobby와 시작 계약

2026-09-13. T09/G4 완료 근거를 확인한 뒤 사용자의 다음 작업 요청으로 T10-A 하나를 적용했다. 현재 실행 상태는 T10A_VALIDATION.md에서 별도 기록한다.

- 기존 T02의 DirectConnectionSession에 선택적 승인 payload/protocol/Host 승인 함수를 더한다. 설정하지 않는 T02~T09는 기존 protocol2와 2인 제한을 유지한다. T10-A는 protocol10/build14다.
- 새 RoomLobby 씬과 Lobby assembly가 같은 ScreenLayoutConfig를 읽는다. 이전 게임 씬·Config·패키지를 교체하지 않는다. 현재0구슬/100시작/생성20/3초당20/명중5/180초를 Host Config에 담는다.
- Host=P1, 참가자=P2이며 두 명일 때 좌우 상대는 같은 다른 참가자다. 최대2명은 NGO 승인 시 즉시 예약하여 동시에 도착한3번째 요청도 거절한다.
- 특정 Bonjour 서비스 `_c6hapio._udp`의 실제 등록·탐색·IPv4/SRV 해석을 사용한다. Direct IP는 별도 경로다. 발견 정보는 안내 값이며 참가 시 protocol/build/room/nonce를 다시 확인한다.
- Host는 임의 roomId/sessionId/Seed와 원본 Config JSON/SHA256, 초기 P1/P2 상태를 전송한다. Client는 지원 schema/범위/필수값과 자신에게 보낸 nonce를 확인한 뒤 Config hash를 ACK한다. 수신 Config는 세션 메모리에 채택하고 로컬 asset을 덮어쓰지 않는다.
- Ready는 초기 ACK 후에만 가능하다. 요청의 실제 NGO sender, room/session/requestId/sequence/revision을 검증하며 같은 요청을 재사용하지 않는다. 비Host Start는 거절하고, 두 Ready가 모이면 Host가 Playing과 round1/Seed/Config 시작 계약을 확정한다.
- 일반 Snapshot은 revision이 증가할 때만 반영한다. 이미 방송으로 받은 상태와 같은 revision의 응답, 또는 요청 당시 revision 이후의 늦은 응답은 검증 후 pending만 해제하며 최신 상태를 되돌리지 않는다.
- T10-A Playing은 시작 합의 완료 표시다. 게임 전체 Snapshot·생성/조합/공격 연결은 T10-B다. 실제 전투를 실행한 것으로 표시하지 않는다.
- 한 참가자가 나가면 방 전체를 종료한다. 자동 재접속·Relay·외부 서버·추가 계정·3인 모드는 추가하지 않는다. 초기상태ACK12초/응답8초는 DEMO_TUNING_VALUE다.

Bonjour의 설치 SDK·공식 근거와 네이티브 수명 정책은 T10A_DISCOVERY_IMPLEMENTATION.md를 따른다. 프로토콜 승인은 설치 NGO2.13.1의 Documentation~/basics/connection-approval.md와 Runtime/Core/NetworkManager.cs를 확인했다.

## 보존·적용 차이

원본 Editor가 열려 있으므로 별도 `/private/tmp/C6_Prototype_T10A_Verification`에서 검증한다. 기존 T09 문서6개를 `docs/history/2026-09-13-t09-g4`에 복사하고 작업 전768파일 해시를 `docs/evidence/T10-A/pre-change-manifest.json`에 남겼다. 실제 루트 AGENTS의 현재 Task 단락만 T10-A로 갱신했다. 새 코드·씬·빌드설정 및 기존 연결 함수의 선택적 확장이 이번 변경이다. 사용자 변경·기존 meta·이전 증거·소스는 보존하며 최종 대조를 기록한다. Git commit/push·배포는 하지 않는다.
