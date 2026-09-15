> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T10-B · 2인 게임 상태 동기화 결정

2026-09-13. 이 결정은 T10-B 시험 실행 전에 기록했다. 구현·실행 완료를 뜻하지 않는다.

- 최신 사용자 규칙: 시작 구슬0, Stamina100/100, Generate1개/20, 연속 회복20/3초, 실제 명중 공격자+5. 기존 Config asset 하나를 보존한다. 참가자는 Lobby의 P1 Host/P2 Client이며 Host Config·Seed를 승인한 런타임 복사본에 적용한다.
- T10-A의 시작 합의 후 게임 READY Snapshot 수신·지문 ACK까지 Host 시계를 시작하지 않는다. 생성·조합·발사·물리·명중 보너스는 기존 Host 서비스만 실행한다.
- Host LateUpdate에서 세 서비스의 논리 상태를 한 Snapshot으로 묶는다. 새 aggregate revision을 사용하고 Client는 전체 검증 후 함께 적용한다. 개별 서비스 Snapshot 전송은 이 씬에서만 중지한다. 요청·영수증·누락 조회는 기존 경로를 유지한다.
- 비교는 동일 session/round/aggregate revision에 한정한다. ID·owner·enum·정수·Seed·Config 지문은 정확히 같아야 한다. 수치 검증 허용 오차는 1e-6이며 직렬화된 동일 Snapshot의 지문은 nonce를 제외하고 정확히 일치해야 한다.
- 진행 중 표시 시간의 양쪽 허용 오차는 **1.0초**다. 공통 Host deadline과 함께 Host realtime/NGO ServerTime 대응점을 보내며 Client 자체 앱 시작 시각을 Host deadline에서 빼지 않는다. 결과 판정은 Host만 하며 terminal 수치는 고정한다. 시험 결과에 맞춰 이 기준을 바꾸지 않는다.
- 초기 상태 없이 Playing 수신, Config/Seed 불일치, 낮은 revision·이전 round·다른 sender·미승인 owner는 거부한다. Retry는 새 round의 빈 초기 상태를 다시 확인하고 Host Start를 기다린다.
- 자신의 구슬만 2D로 표시하고 기존 OrbId 뷰를 유지한다. 로컬 드래그 위치는 매 프레임 전송하지 않는다. 전달과 카메라 확장은 T11 이후 별도 요청이다.
- Editor/같은 Mac의 두 실제 프로세스/iOS export/앱 빌드·서명/설치/실기기 결과를 분리한다. T12 두 iPhone 실기기 Gate를 Mac 결과로 대체하지 않는다.
