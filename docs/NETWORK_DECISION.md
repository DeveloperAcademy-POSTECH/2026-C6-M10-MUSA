> **공개 이관본 안내:** 이 문서의 개인 경로·서명 식별자는 예시로 일반화했습니다. `/path/to/...`, `YOUR_TEAM_ID`, `HOST_IPV4`는 자신의 환경 값으로 바꾸세요. 원시 증거와 과거 문서 묶음은 공개하지 않으며 [검증 요약](VALIDATION_SUMMARY.md)·[이관 범위](MIGRATION.md)에서 구분합니다. 과거 기록의 완료·미완료 상태는 새 실행 결과가 아닙니다.

# T02 통신 방식 결정

2026-09-12. 사용자의 다음 작업 요청에 따라 T02 직접 IP 연결만 추가한다. 실행 결과는 [VALIDATION](VALIDATION.md), 반복 절차는 [T02_RUNBOOK](T02_RUNBOOK.md)을 따른다. 이 결정의 채택은 시험 통과를 뜻하지 않는다.

## 패키지와 선택 근거

기존 프로젝트에는 재사용할 통신 구현·패키지가 없었다. GameObject/MonoBehaviour 기반의 두 참가자 Host 권한 구조에 맞춰 Netcode for GameObjects(NGO)와 Unity Transport를 선택했다. Relay·계정·자동 방 탐색은 추가하지 않는다.

| 항목 | 실제 해결 값 | 근거 |
|---|---|---|
| Unity | 6000.5.7f1 | 기존 Editor 유지 |
| `com.unity.netcode.gameobjects` | **2.13.1**, `source: registry`, depth 0 | `Packages/packages-lock.json`, Unity Registry |
| `com.unity.transport` | **6.5.0**, `source: builtin`, depth 0 | 동일 lock 및 설치 Editor의 BuiltInPackages |
| 기존 렌더러 | URP 17.5.0 | 기존 구성 유지 |

설치 Editor에는 NGO 2.13.1 배포본이 포함돼 있으며 Unity 6.5 공식 매뉴얼도 이 버전을 호환 목록에 명시한다. NGO 패키지의 Transport 의존성 선언은 2.6.0이지만, 이 프로젝트에서 실제 해결된 Transport는 Editor에 포함된 **6.5.0**이다. Unity 6.5의 Transport는 Editor 버전에 고정되는 core package이므로 2.6.0으로 임의 교체하지 않는다. [NGO 호환 버전](https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.netcode.gameobjects.html) · [Unity Transport core package](https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.transport.html)

## 연결 계약

- `DirectConnectionSession`이 NetworkManager와 UnityTransport 한 개를 소유한다. Host/Client 모두 같은 프로토콜을 사용하며 씬·Player 오브젝트·게임 상태 동기화는 하지 않는다.
- Host가 포함된 최대 두 참가자의 실제 NGO ID를 표시한다. Host ID는 0이고 Client ID는 연결 시 부여된다. T10-A 최종 P1/P2 Lobby UI와는 구분한다.
- 승인 시 슬롯을 먼저 예약해 동시에 들어온 요청도 두 명을 넘지 못하게 한다. 거부된 추가 접속의 종료는 기존 두 참가자의 연결을 종료하지 않는다.
- 실제 참가자가 연결을 종료하면 남은 쪽도 세션을 끝낸다. 종료 처리가 끝난 뒤 양쪽에서 수동으로 새 Host/Join을 요청해야 한다. 자동 재접속은 없다.
- 연결 이벤트의 ID는 중복 제거한다. Client의 최초 연결 콜백에 전달되는 임시 `PeerClientIds`는 콜백 중 복사한다. 구독은 Manager 생성 시 한 번 등록하고 소유자 파괴 시 제거한다.
- 입력은 점으로 나뉜 IPv4와 포트 1~65535만 받는다. 불명확한 축약·16진수·선행 0 주소, 호스트명, IPv6, 0으로 시작하는 주소, multicast·예약·limited broadcast 주소를 거부한다. Loopback은 명시적인 같은 Mac의 두 프로세스 시험에 허용하며 실제 두 기기 시험을 대체하지 않는다.

사용 API는 NGO 2.13.1의 `SetConnectionData`, `StartHost`/`StartClient`, `OnConnectionEvent`, 승인 응답, `NetworkManager.Shutdown`이다. 종료 완료 전에 새 연결을 시작하지 않는다. [연결 이벤트](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/advanced-topics/connection-events.html) · [연결 승인](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/connection-approval.html) · [NetworkManager 수명주기](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/core/networkmanager.html)

## 기본값의 단일 원본

아래 값은 통합 사양의 게임 밸런스 값이 아닌 **DEMO_ASSUMPTION**이다. 원본은 `Assets/_Project/HapioMVP/Networking/DirectConnectionSession.cs`의 공개 상수이며 UI와 실행 도구는 이를 참조한다.

| 상수 | 값 | 의미 |
|---|---:|---|
| `DefaultPort` | 7777 | 입력 초기 포트 |
| `ConnectionTimeoutSeconds` | 8초 | 앱의 연결 대기 제한 |
| `ConnectAttemptIntervalMilliseconds` / `ConnectAttemptCount` | 500ms / 16회 | 같은 연결 시도의 Transport handshake 설정 |
| `DisconnectTimeoutMilliseconds` | 8000ms | 통신 두절 제한 |
| `ApprovalTimeoutSeconds` | 8초 | 승인 handshake 제한 |
| `ProtocolVersion` | 2 | T02 연결 프로토콜 구분 |

Handshake의 패킷 재시도는 종료된 세션의 자동 재접속과 다르다. 시험 도구의 관측 대기 시간도 제품의 연결 제한값과 구분한다. Host 포함 정원 2는 `TwoParticipantAdmissionPolicy.Capacity`가 적용한다.

## iOS 직접 unicast 제약

`DirectConnectionBuild.ApplyLocalNetworkPurpose`가 생성된 `Info.plist`에 `NSLocalNetworkUsageDescription`을 적용한다. 실제 생성 파일의 값도 확인한다. 현재 용도는 같은 Wi-Fi의 다른 iPhone과 직접 통신하는 것이다. Bonjour 서비스·broadcast·multicast를 사용하지 않으므로 이 Task에서 `NSBonjourServices` 또는 multicast entitlement를 추가하지 않는다.

직접 UDP unicast 송신에도 Local Network 접근이 필요하다. 허용 여부를 반환하는 일반 API가 없으므로 시간 초과만으로 권한 거부를 단정하지 않는다. 주소·포트, 같은 Wi-Fi, 공유기 기기 간 격리, iOS 설정의 로컬 네트워크 접근을 각각 확인한다. 첫 권한 알림에 응답하는 동안 연결이 만료되면 허용 후 수동 재시도한다. 권한 동작은 앱을 전경에 둔 실제 기기에서 확인한다. [Apple NSLocalNetworkUsageDescription](https://developer.apple.com/documentation/bundleresources/information-property-list/nslocalnetworkusagedescription) · [Apple TN3179](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy)

## 검증 경계와 보존

T01/G1의 실제 A 실행 → B 재설치·실행 근거는 보존한다. T02가 활성 빌드 씬이 되므로 기존 T01 시험은 보존된 T01 씬 자체를 검사하고, PlayMode에서 해당 씬을 명시적으로 로드하도록 조정한다. 현재 활성 T02 씬은 내보내기 설정과 실제 앱 실행으로 확인한다. 과거 T01 PASS를 새 T02 빌드의 PASS로 승계하지 않는다.

EditMode42개·PlayMode8개, 실제 별도 Mac 프로세스의 연결·종료·새 연결·정원 초과 거부·시간 초과가 통과했다. 추가로 iPhone17과 Mac의 실제 LAN 연결·수동 재연결을 사용자 화면 관측과 양쪽 로그로 확인했다. 실제 결과·로그·XML 수는 VALIDATION에 기록했다. 두 iPhone 시험은 T03과 함께 수행하도록 준비하며 아직 **NOT_RUN**이다. G2 PASS에는 T03의 두 기기 100개 고유 요청·승인 수·상태 일치 검증이 필요하다. 직접 IP 성공은 AT-02 자동 방 탐색 성공이 아니다. T03 구현이나 Git push를 자동 수행하지 않는다.
