# L2 방 탐색·참가 통합 기록

작성일: 2026-09-15 · 상태: **L2 완료 / L3 이후 NOT_RUN** · 앱26 / 기존 게임 계약24

이번 요청은 L1에서 확인한 핫스팟 IPv6 통신 경로를 실제 게임 방 입장에 연결하는 L2다. 패키지·클라우드 서비스 추가 없이 기존 IPv4도 유지한다. 아래 결과는 실제 XML·독립 앱·기기 기록과 사용자 확인으로 판단했다.

## 구현 범위

1. Bonjour의 A·AAAA 응답을 모으고 같은 방의 주소 후보를 병합한다. scope가 필요한 link-local IPv6는 숫자 인터페이스 범위를 보존한다. 주소 계열별 후보가 골고루 포함되며 최대8개를 사용한다.
2. Host가 같은 포트에서 IPv4와 IPv6를 함께 수신한다. Client는 stock Unity Transport UDP를 사용하며 scope를 보존한 목적지를 전달한다. NGO의 승인·순서 보장·재전송·메시지 분할 처리를 유지한다.
3. 한 JOIN의 room/build/nonce는 고정한다. 연결 전 경로 실패만 다음 주소로 진행하며, Host의 명시적 거부나 이미 입장한 세션의 단절은 자동 재시도하지 않는다.
4. 이전 NetworkManager의 Shutdown·콜백 해제·프레임 경계를 지난 뒤 새 후보를 시작한다. 취소·실제 백그라운드 전환은 대기 중인 후보 전환도 취소한다.
5. 전송 연결, Host 승인, Config·최초 상태 확인을 구분한다. 최초 상태가 확인돼야 입장 화면을 표시한다. Direct IP는 숫자 IPv6도 받는다.

기존 구슬 물리·소유권·전달·조합·3D 피격·자원·180초·결과/Retry 규칙은 이번 변경 대상이 아니다. 해당 게임 기능의 전체 회귀와3~5인 플레이 검사는 L3, 일반 Wi-Fi·인터넷 없는 LAN 등의 실환경 비교는 L4다.

## 코드 확인 지도

경로의 공통 앞부분은 `Assets/_Project/HapioMVP/`다.

| 파일 | 유지보수 시 확인할 내용 |
|---|---|
| `Networking/DualStackUnityTransport.cs` | 전역 설정을 바꾸지 않는 인스턴스별 driver 구성, 기존 NGO pipeline 사용 |
| `Networking/DualStackUdpNetworkInterface.cs` | Host의 두 주소 계열 수신, 포트 충돌과 소켓 정리 |
| `Networking/DualStackEndpoint.cs`, `L1TransportEndpoint.cs` | 버전 고정 주소 변환과 IPv6 scope 보존 |
| `Networking/DirectConnectionValidation.cs` | 숫자 IPv4/IPv6 검증, link-local scope 필수, 잘못된 주소 거절 |
| `Networking/ConnectionCandidatePlan.cs` | 후보 복사·중복 제거·개수·전체 시간 제한 |
| `Networking/DirectConnectionSession.cs` | 후보 시도·취소·manager 교체·늦은 콜백 차단·실패 단계 |
| `Lobby/Discovery/DiscoveryAddressCandidates.cs` | 계열별 후보 선택과 주소 응답 변환 |
| `Lobby/Discovery/DiscoveryRecoveryState.cs`, `DiscoveryRoomCatalog.cs` | 제한된 재시도, lease와 만료, 메타데이터 일치 확인 |
| `Lobby/Discovery/BonjourRoomDiscovery.cs` | 실제 Bonjour 핸들 수명, A/AAAA 조회·갱신·제거 |
| `Lobby/T10LobbySession.cs` | 승인 payload 고정, 최초 Config/상태 확인과 오류 구분 |
| `Lobby/T10LobbyController.cs`, `T10LobbyHud.cs` | 진행 단계·취소·IPv6 입력·실패 안내 |
| `GameSync/L2ConnectionProbe.cs` | 개발 빌드의 로컬 증거, 명시적인 Mac 자동 시험 |
| `Editor/L2ConnectionBuild.cs` | 기존 P4 씬으로 앱26 출력, iOS lifecycle 후처리 확인 |

## 전송 어댑터 선택 이유

처음 시도한 Baselib 관리 바인딩은 Unity 내부 전용으로 컴파일되지 않았다. 최종 Host 경로는 C# `System.Net.Sockets`의 non-blocking UDP 두 개와 설치 UTP의 공개 `INetworkInterface`·`WrapToUnmanaged()` 확장점을 사용한다. Client는 기존 UDP interface를 유지한다. 패키지 내부 접근 권한을 우회하거나 패키지 소스를 수정하지 않는다.

Host는 같은 포트의 IPv4 소켓과 IPv6 전용 소켓을 별도로 열어 한 UTP 연결 테이블에 전달한다. 한 계열의 포트가 이미 사용 중이면 조용히 재사용하지 않고 Host 시작을 실패시킨다. I/O는 매 프레임 정해진 수만 처리하고, 재전송은 원래 UTP reliable pipeline이 맡는다. 관리 코드와 소켓 주소 변환에는 비용이 있으므로 전체 게임·5인 부하의 성능 확인은 L3에서 별도로 수행한다. 검증 대상은 현재 macOS/iOS이며 다른 플랫폼의 동작을 보장하지 않는다.

IPv6 scope의 낮은 수준 표현은 L1에서 검증한 `L1TransportEndpoint`에 격리한다. Unity 버전과 endpoint 크기 검사를 통과해야 실행하며, Unity/Transport를 바꾸면 주소 변환과 실기기 검사를 다시 해야 한다.

## 제한 시간과 재시도 정책

후보당 상한18초는 전송 연결창8초+승인 제한8초+정리 여유2초다. 전체 JOIN 상한은60초이며 주소 후보는 최대8개다. 이는 연결 호환성 검증용 조정값이며 모든 후보를 반드시 끝까지 시도한다는 의미가 아니다. 전송 계층이 먼저 실패를 확정하면18초 이전에 다음 후보로 넘어간다. 게임 중 응답 제한8초는 기존값을 유지한다.

Bonjour 첫 응답 제한8초, 서비스당 최대3회 시도와1·2초 간격, 광고 갱신3초와 lease12초를 사용한다. 방/서비스 수는32로 제한한다. 같은 오래된 TXT 캐시를 다시 읽었다고 살아 있는 방으로 갱신하지 않는다.

NGO는 통신 종료에도 `DisconnectReason` 문자열을 생성한다. 이를 서버가 보낸 명시적 거절과 구별해야 한다. 버전 불일치·정원 초과·이미 시작·중복 참가 등은 주소를 바꿔 우회하지 않는다.

Host가 nonce를 이미 승인했지만 Client가 승인 응답을 받기 전에 경로가 끊기면 이전 참가 예약이 잠시 남을 수 있다. 이때 동일 nonce의 다음 시도가 명시적으로 거절되면 중단한다. nonce를 바꿔 중복 검증을 우회하지 않으며, 무응답 주소→정상 주소 검사를 이 특수 상황의 성공으로 확대하지 않는다.

## 실행 기록

| 항목 | 상태 | 근거 |
|---|---|---|
| 최초 컴파일 | FAIL | 외부 asmdef에서 internal Baselib Binding 접근 불가. 테스트 실행 전 중단 |
| 수정 후 Compile | PASS | Unity6000.5.7f1 EditMode/PlayMode·Mac 플레이어 컴파일 |
| EditMode | PASS | 최종 중복 제외572개. 전체572 중571 통과 후 새 테스트의 배열 Count 단언 수정, 해당32개 재검사 모두 통과. 누락/skip/inconclusive0 |
| PlayMode | PASS | 22/22, 실패/skip/inconclusive0. 재시도·이전 콜백 무시·취소 경계·Host 재시작·로비 수명 |
| Mac 실제 NGO 입장 | PASS | 독립 Host/Client 앱 쌍4회. IPv6 후보 전환·명시 거절·두 취소 시점·IPv4 재입장·중복 좌석0 |
| iOS 출력·앱 빌드·서명 | PASS | Xcode 출력·IL2CPP 앱 빌드·개발 서명 검증, 빌드26·기존 Bundle·Bonjour 서비스 확인 |
| iPhone·iPad 설치·실행 | PASS | 두 기기 설치/실행 exit0, 각 기기의 앱26 초기 로비 JSON 수집 |
| iPhone 핫스팟 Host → iPad 게임 JOIN | PASS | 앱26에서 방 발견·입장·최초 상태·양쪽 Ready 확인. iPad는 IPv6 후보1/3, 두 기기 room/session/config/roster/revision·초기 ACK 일치 |
| L3 전체 게임/다인 회귀 | NOT_RUN | 다음 단계 |
| L4 일반 Wi-Fi·인터넷 없는 LAN | NOT_RUN | 별도 환경 시험 |

원시 로그·기기 식별자·주소·서명 정보는 로컬 증거 폴더에만 보존한다. 공개 문서에는 요약과 집계만 남긴다.

## Mac에서 동일 검사를 다시 실행하기

검사 앱은 일반 게임 씬을 사용한다. `C6_L2_CHECKS`는 아래 빌더가 Development 플레이어에만 추가한다. 휴대기기는 관찰 기록만 남기며 자동으로 방을 만들거나 게임을 시작하지 않는다. Editor의 전역 define을 수정할 필요가 없다.

프로젝트를 Editor에서 사용 중이면 별도 깨끗한 checkout에서 실행한다. 결과 폴더는 매번 새 경로를 지정한다. 아래 `PROJECT`와 `OUT`은 자신의 경로로 바꾼다.

```bash
PROJECT="/path/to/2026-C6-M10-MUSA"
OUT="/private/tmp/c6-l2-new-run"
env -u UNITY_RUN_TIMEOUT -u UNITY_TEST_TIMEOUT -u UNITY_BUILD_TIMEOUT \
  C6_L2_OUTPUT_ROOT="$OUT" unity build "$PROJECT" \
  --target StandaloneOSX --execute-method C6.Editor.L2ConnectionBuild.BuildMac \
  --output-path "$OUT/macOS/C6LocalNetwork.app" --allow-dirty-build \
  --log-file "$OUT-build.log" --no-tail
python3 "$PROJECT/tools/run_l2_connections.py" \
  "$OUT/macOS/C6LocalNetwork.app" --output-root "$OUT-checks"
```

`summary.json`만 보고 끝내지 말고 각 Host/Client의 `result.json`이 종료 PASS인지, 프로세스 exit가0인지, 최초 상태와 참가자 대조가 있는지 확인한다. 실행기가 이 조건을 함께 검사한다. 실패한 실행이나 시간 초과 결과는 지우지 않는다. 실행기는 프로세스를 강제 종료하지 않고 미완료 작업과 로그를 보존한다.

대표 시험은 첫 주소를 의도적으로 응답 없는 시험용 주소로 넣는다. 이때 stock UTP가 내는 정확한 연결 실패 오류1회만 예상 결과로 기록한다. 그 외 오류나 두 번째 오류를 무시하지 않는다. 일반 실기기 관찰에서는 이 예외 처리를 적용하지 않는다.

기기 빌드는 같은 빌더의 `C6.Editor.L2ConnectionBuild.ExportIOS`와 `--target iOS`를 사용하고 출력 경로를 `$OUT/iOS`로 지정한다. 생성된 Xcode 프로젝트에서 본인의 승인된 개발 Team과 연결 기기를 선택해 서명·설치한다. 저장소에는 개인 Team·기기 ID를 넣지 않는다.

## 이번 실행의 수치와 보존 범위

- Mac 실패 후보의 UTP 종료는8.269초에 확인됐다. IPv6 다음 후보 시작 후 전송 연결까지0.108초, 최초 상태 확인까지0.168초였다. 한 JOIN 시작부터 최초 확인까지는8.477초다. 네 시나리오의 정상 IPv4 재입장 초기 확인은0.147~0.186초였다. 같은 Mac 안의 시험값이며 무선 환경 성능으로 일반화하지 않는다.
- 명시적 BUILD_MISMATCH는0.144초에 후보1에서 종료됐고 후보2를 시도하지 않았다. 취소 검사에서는 Idle 상태를 약1.3초 더 관찰한 뒤 새 입장을 별도로 시작했다.
- Mac 빌드 오류0·경고17, iOS Unity 출력 오류0·경고15였다. 기존 진단 코드의 deprecated 탐색 API와 새 Probe의 `FindFirstObjectByType` 경고 등이 남아 있다. 경고0으로 기록하지 않는다.
- Unity import/build 과정에서 임시 검증 폴더의 `Assets/Settings/Mobile_RPAsset.asset`·`PC_RPAsset.asset`가 자동 직렬화됐다. 원본 렌더링 에셋을 보존하기 위해 이2개는 코드 이관에서 제외한다. 검증 앱은 설치된 동일 URP가 import한 상태로 빌드됐다.
- Packages의 manifest/lock, ProjectSettings, 저장 씬19개의 기준 해시를 대조했다. 게임 Config·Prefab·기존 meta는 변경하지 않았다.
- 원시 XML/프로세스/빌드/기기 로그와 서명 정보는 로컬 `c6-l2-checks` 증거 폴더에 남는다. 공개 검증 요약은 [L2 실행 요약](validation/L2_20260915.json)을 따른다.

## 실제 핫스팟 확인과 다음 단계

사용자가 iPhone 개인용 핫스팟을 제공하고 같은 iPhone에서 CREATE ROOM을 눌렀다. iPad에서 FIND ROOMS·JOIN 후 같은 방의 P1·P2와 양쪽 Ready 표시가 정상임을 확인했다. 기기 로그에서는 Client가 IPv6 첫 후보(전체3개)를 사용했고, JOIN 시작부터 연결0.310초·최초 상태 확인0.523초가 측정됐다. 중복 참가자0, runtime error0, 증거 잘림0이었다. Ready 버튼의 시각적 결과는 사용자 확인이며 Probe가 Ready flag를 직접 기록한 것으로 표현하지 않는다.

이 시험에는 개발용 USB 케이블이 연결돼 있었다. 케이블을 분리한 확인·일반 공유기 Wi-Fi·인터넷 없는 LAN·게임 시작 이후 플레이와 승패/Retry는 이 결과에 포함하지 않는다. L1의 진단 데이터 왕복과 달리 이번에는 실제 게임 로비의 승인·Config·최초 상태와 Ready까지 확인했다.

다음 요청은 **L3 게임·다인 회귀**다. 전송 경로가 바뀐 상태에서 기존 구슬·자원·피격·결과/Retry와 IPv4/IPv6 혼합3~5인 상태가 유지되는지 확인하는 단계다. 다양한 실제 네트워크와 케이블 분리 검사는 L4에서 별도로 진행한다.
